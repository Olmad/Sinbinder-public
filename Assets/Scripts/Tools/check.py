#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Статическая проверка исходников Sinbinder.

Не заменяет компилятор — ловит тот класс ошибок, который в этом проекте
встречался чаще всего и который не виден глазами: типы из чужих
пространств имён без using, литералы не того типа в вызовах, LINQ без
System.Linq, использование переменной до объявления, имена файлов,
не совпадающие с именем MonoBehaviour, редакторные скрипты без обёртки.

Всё это — ошибки, из-за которых Unity не собирает проект вообще, то есть
не показывает и остальные. Найти их до открытия редактора дешевле.

Запуск из корня репозитория:
    python3 Tools/check.py
    python3 Tools/check.py --quiet     только итог
"""

import glob
import io
import os
import re
import sys
from collections import defaultdict

SKIP_DIRS = {'.git', 'Library', 'Temp', 'obj', 'Build', 'Builds', 'Logs',
             'Packages', 'ProjectSettings', 'docs', 'Tools', '__pycache__',
             'TutorialInfo'}   # шаблон Unity, не наш код

UNITY_TYPES = {
    # Вложенный тип EventTrigger.Entry: правило видит только последнее
    # имя после new, а вложенных типов Unity не знает (панель приказов,
    # 24 сентября; компилятор его принял).
    'Entry',
    'Vector2', 'Vector3', 'Vector4', 'Quaternion', 'Mathf', 'Debug', 'Time', 'Color', 'Color32',
    'Input', 'Camera', 'Physics', 'Physics2D', 'RaycastHit', 'GameObject', 'Transform',
    'MonoBehaviour', 'ScriptableObject', 'Coroutine', 'WaitForSeconds', 'WaitForSecondsRealtime',
    'WaitForEndOfFrame', 'Texture2D', 'Sprite', 'Image', 'Text', 'Canvas', 'CanvasGroup',
    'RectTransform', 'AudioClip', 'AudioSource', 'NavMeshAgent', 'Light', 'Resources',
    'Application', 'Screen', 'Rect', 'LayerMask', 'Ray', 'Material', 'Shader', 'Gizmos',
    'HideFlags', 'Object', 'Random', 'Renderer', 'Collider', 'Rigidbody', 'AnimationCurve',
    'EditorBuildSettingsScene', 'EditorUtility', 'AssetDatabase', 'MenuItem', 'SerializedObject',
    'GUIStyle', 'GUIContent', 'GUILayout', 'GUI', 'EditorGUILayout', 'EditorGUI', 'EditorStyles',
    'Handles', 'SceneView', 'PrefabUtility', 'EditorSceneManager', 'SceneManager', 'Undo',
    'ParticleSystem', 'ParticleSystemRenderer', 'MinMaxCurve', 'MinMaxGradient',
    # Вложенный ParticleSystem.EmitParams — выброс частиц в точку
    # (Gameplay/HitBurst, SoulFlight, 2 октября); как и Entry выше.
    'EmitParams',
    'Gradient', 'GradientColorKey', 'GradientAlphaKey', 'Volume', 'VolumeProfile',
    'VolumeComponent', 'Bounds', 'Matrix4x4', 'Mesh', 'SkinnedMeshRenderer',
    'MeshRenderer', 'MeshFilter', 'Animator', 'RuntimeAnimatorController',
    'AnimationClip', 'RenderSettings', 'QualitySettings', 'TextureImporter',
    'AssetImporter', 'ModelImporter', 'EditorApplication',
    'RenderTexture', 'RenderTextureFormat', 'Texture', 'Graphics', 'TextureFormat',
    'BuildPipeline', 'BuildPlayerOptions', 'BuildTarget', 'BuildOptions',
    'BuildReport', 'BuildSummary', 'BuildResult', 'EditorBuildSettings',
    # Дорога к воротам в прогоне демо (26 сентября): путь по навмешу.
    'NavMeshPath',
}

DOTNET_TYPES = {
    'String', 'Math', 'Guid', 'DateTime', 'TimeSpan', 'Array', 'List', 'Dictionary', 'HashSet',
    'Queue', 'Stack', 'StringBuilder', 'Regex', 'Exception', 'IEnumerator', 'IEnumerable',
    'Action', 'Func', 'Nullable', 'Convert', 'Enum', 'Tuple', 'KeyValuePair',
}

KEYWORD_CALLS = {'if', 'for', 'while', 'switch', 'foreach', 'return', 'lock', 'catch', 'using'}

# Отложенное до полной версии: расширение .cs.later Unity не знает,
# и файл в сборку не попадает. Первая строка обязана это объявлять,
# иначе через месяц никто не вспомнит, почему код не работает.
LATER_EXT = '.cs.later'
LATER_HEADER = '// ОТЛОЖЕНО ДО ОСНОВНОЙ ИГРЫ'

LINQ = re.compile(
    r'\.(Any|All|Where|Select|SelectMany|FirstOrDefault|LastOrDefault|OrderBy|OrderByDescending'
    r'|GroupBy|Distinct|Aggregate|SingleOrDefault|ToList|ToDictionary)\s*\(')

RE_NAMESPACE = re.compile(r'namespace\s+([\w.]+)')
RE_TYPE_DECL = re.compile(
    r'^\s*(?:public|internal)\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+)*'
    r'(class|struct|enum|interface)\s+(\w+)', re.M)
# readonly здесь обязателен: без него `private readonly struct` не
# опознавался как объявление, и тип, объявленный так, шёл в отчёт
# как несуществующий. Ложная тревога, но она обесценивает весь отчёт:
# анализатор, который врёт, перестают читать.
RE_ANY_TYPE_DECL = re.compile(
    r'^\s*(?:public|internal|private|protected)?\s*'
    r'(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+|ref\s+)*'
    r'(?:class|struct|enum|interface)\s+(\w+)', re.M)
RE_METHOD_DECL = re.compile(
    r'^\s*(?:public|private|protected|internal)\s+(?:static\s+|virtual\s+|override\s+|async\s+)*'
    r'[\w<>,\[\]\?\.]+\s+(\w+)\s*\(([^)]*)\)\s*(?:\{|$)', re.M)
# Вызов метода: и через точку, и без неё — вызов внутри своего же
# класса пишется без квалификации и раньше проверку обходил.
RE_CALL = re.compile(r'(?:\.|(?<![\w.]))(\w+)\s*\(([^();]*)\)')
RE_MONO_CLASS = re.compile(r'public class (\w+)\s*:\s*[^{]*MonoBehaviour')

# Старый ввод: UnityEngine.Input. Под настройкой «только новый Input System»
# каждый такой вызов бросает InvalidOperationException в рантайме,
# и компилятор об этом молчит — потому и проверяем здесь.
RE_OLD_INPUT = re.compile(
    r'(?<![\w.])Input\s*\.\s*'
    r'(GetKey\w*|GetButton\w*|GetMouseButton\w*|GetAxis\w*|mousePosition'
    r'|mouseScrollDelta|touches|touchCount|GetTouch|anyKey\w*|inputString)')
# Квалифицированное имя ловим тоже: `new AOS.BehaviourResolver()` было
# проверке невидимо целиком — она требовала имя типа сразу после new.
# Найдено на собственной ошибке: класс зовут BehaviourResolver,
# а файл BehaviorResolver.cs, и опечатка прошла мимо всех правил.
# Структуры проекта. Нужны отдельно от RE_TYPE_DECL: там важен вид
# объявления, здесь — только то, что тип значимый, и вложенные
# с приватными считаются наравне с публичными.
RE_STRUCT_DECL = re.compile(
    r'^\s*(?:public|internal|private|protected)?\s*'
    r'(?:static\s+|sealed\s+|readonly\s+|ref\s+|partial\s+)*'
    r'struct\s+(\w+)', re.M)
# Свойство и поле: запоминаем объявленный тип, чтобы потом узнать тип
# переменной, выведенной через var. Знак вопроса захватываем нарочно —
# Decision? сравнивать с null можно, и такую строку трогать нельзя.
RE_MEMBER_DECL = re.compile(
    r'^\s*(?:public|internal|protected|private)\s+'
    r'(?:static\s+|readonly\s+|virtual\s+|override\s+|const\s+)*'
    r'([\w<>\[\]\.]+\??)\s+(\w+)\s*(?:\{\s*get|=>|;|=[^=])', re.M)

# Что код ищет в сцене и что создаёт сам. Разница между этими двумя
# списками и есть «написано, но до сцены не доведено».
RE_FIND_TYPE = re.compile(
    r'Find(?:FirstObjectByType|AnyObjectByType|ObjectOfType'
    r'|ObjectsByType|ObjectsOfType)\s*<\s*([A-Za-z0-9_.]+)\s*>')
RE_MAKE_TYPE = re.compile(
    r'(?:AddComponent|AddIfMissing|Require)\s*<\s*([A-Za-z0-9_.]+)\s*>')

RE_NEW = re.compile(r'\bnew\s+(?:[A-Z]\w*\s*\.\s*)*([A-Z]\w+)\s*[\(\{]')
SPLIT_ARGS = re.compile(r',(?![^<>()]*[>)])')


def owner_file(files, name):
    """Файл, в котором объявлен тип. Для места в отчёте."""
    for p in files:
        if os.path.splitext(os.path.basename(p))[0] == name:
            return p
    return name


def collect(root='.'):
    files = []
    for cur, dirs, names in os.walk(root):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for n in names:
            if n.endswith('.cs'):
                files.append(os.path.join(cur, n))
    return sorted(files)


def strip(text):
    """Убирает строковые литералы и комментарии — чтобы не считать их кодом."""
    text = re.sub(r'"(?:[^"\\]|\\.)*"', '""', text)
    text = re.sub(r'//[^\n]*', '', text)
    return re.sub(r'/\*.*?\*/', '', text, flags=re.S)


def decomment(text):
    """
    Убирает комментарии, но **сохраняет строковые литералы**.

    Нужен там, где проверяется содержимое строк. Общий strip() их
    вычищает — и правило, написанное на нём, молча не находит ничего:
    оно смотрит в пустоту и говорит «чисто». Ровно та же поломка,
    что была у scene_presence (13-DRIFT.md), и поймана тем же способом —
    проверкой, что материал вообще есть.
    """
    text = re.sub(r'//[^\n]*', '', text)
    return re.sub(r'/\*.*?\*/', '', text, flags=re.S)


def line_of(text, pos):
    return text[:pos].count('\n') + 1


# ── Перевод: в каком вызове стоит строка (то же, что Tools/lang/wrap.py — держать вместе) ──

def blank_keep(text, literals_too):
    """Комментарии (и, если просят, строки) — пробелами; позиции и переводы строк целы."""
    out = list(text)
    pat = r'//[^\n]*|/\*.*?\*/'
    if literals_too:
        pat = r'(\$@|@\$|\$|@)?"(?:[^"\\\n]|\\.)*"|' + pat
    for m in re.finditer(pat, text, re.S):
        for k in range(m.start(), m.end()):
            if out[k] != '\n':
                out[k] = ' '
    return ''.join(out)


SKIP_CALLS = {
    'Debug.Log', 'Debug.LogWarning', 'Debug.LogError', 'Debug.LogFormat', 'Debug.LogWarningFormat', 'Debug.LogErrorFormat',
    'Tooltip', 'Header', 'MenuItem', 'Obsolete', 'InspectorName', 'ContextMenu', 'AddComponentMenu', 'CreateAssetMenu',
    'new GameObject', 'Find', 'FindWithTag', 'FindGameObjectWithTag', 'Instantiate', 'Load', 'LoadAll',
    'StartsWith', 'EndsWith', 'Contains', 'IndexOf', 'LastIndexOf', 'Equals', 'Replace', 'Split', 'TrimEnd', 'TrimStart',
    'GetString', 'SetString', 'GetInt', 'SetInt', 'GetFloat', 'SetFloat', 'HasKey', 'DeleteKey',
    'StringToHash', 'nameof', 'Loc.T', 'Loc.F', 'Loc.N', 'Loc.Name', 'Shader.Find', 'LayerMask.NameToLayer',
}
NAME_PARAMS = {'name', 'objectName', 'id', 'key', 'tag', 'path', 'label_id'}


def declared_params(clean):
    """{метод: [имена параметров]} по объявлениям в файле (имя параметра — последнее слово)."""
    out = {}
    for m in re.finditer(r'\b[\w<>\[\],.?]+\s+(\w+)\s*\(([^()]*)\)\s*(?:\{|=>|where|$)', clean, re.M):
        name, params = m.group(1), m.group(2).strip()
        if name in ('if', 'while', 'for', 'foreach', 'switch', 'catch', 'using', 'return', 'new', 'lock'):
            continue
        names = []
        for p in params.split(','):
            p = p.split('=')[0].strip()
            if not p:
                continue
            names.append(p.split()[-1])
        out.setdefault(name, names)
    return out


def enclosing_call(clean, start):
    """(вызов, номер аргумента) для литерала, или (None, None)."""
    i, depth, commas = start, 0, 0
    while i > 0:
        i -= 1
        c = clean[i]
        if c in ')]}':
            depth += 1
        elif c in '([{':
            if depth == 0:
                if c != '(':
                    return None, None
                j = i
                while j > 0 and clean[j - 1] in ' \t':
                    j -= 1
                k = j
                while k > 0 and (clean[k - 1].isalnum() or clean[k - 1] in '_.'):
                    k -= 1
                callee = clean[k:j].strip('.')
                before = clean[max(0, k - 5):k]
                if before.rstrip().endswith('new'):
                    callee = 'new ' + callee
                return callee, commas
            depth -= 1
        elif c == ',' and depth == 0:
            commas += 1
        elif c == ';' and depth == 0:
            return None, None
    return None, None


def call_skip(clean, start, params):
    callee, arg = enclosing_call(clean, start)
    if not callee:
        return None
    short = callee.split('.')[-1]
    if callee in SKIP_CALLS or short in SKIP_CALLS or ('PlayerPrefs' in callee):
        return f'вызов {callee}'
    names = params.get(short)
    if names and arg is not None and arg < len(names) and names[arg] in NAME_PARAMS:
        return f'{short}: параметр «{names[arg]}» — имя объекта'
    return None


def inside_debug(clean, start):
    """Строка где-то внутри Debug.Log(…) — на любой глубине скобок: журнал разработчика."""
    i, depth = start, 0
    while i > 0:
        i -= 1
        c = clean[i]
        if c in ')]}':
            depth += 1
        elif c in '([{':
            if depth == 0:
                if c == '{':
                    return False
                j = i
                while j > 0 and clean[j - 1] in ' \t':
                    j -= 1
                k = j
                while k > 0 and (clean[k - 1].isalnum() or clean[k - 1] in '_.'):
                    k -= 1
                if clean[k:j].startswith('Debug.Log'):
                    return True
            else:
                depth -= 1
        elif c == ';' and depth == 0:
            return False
    return False


class Checker:
    def __init__(self, files):
        self.files = files
        self.src = {p: io.open(p, encoding='utf-8', errors='replace').read() for p in files}
        self.problems = []

        self.owner = defaultdict(set)      # тип -> namespace, где объявлен
        self.declared_anywhere = set()     # включая вложенные и приватные
        self.enums = set()
        self.methods = defaultdict(list)   # метод -> список сигнатур
        self.ctors = defaultdict(list)     # тип -> список (мин, макс) аргументов
        self.ctor_files = defaultdict(set) # тип -> файлы, где он объявлен
        self.partial = set()               # типы, разложенные по файлам
        self.structs = set()               # значимые типы: их нельзя сравнить с null
        self.member_type = defaultdict(set)  # имя свойства или поля -> объявленный тип

        for p, s in self.src.items():
            m = RE_NAMESPACE.search(s)
            ns = m.group(1) if m else ''
            for kind, name in RE_TYPE_DECL.findall(s):
                self.owner[name].add(ns)
                if kind == 'enum':
                    self.enums.add(name)
            # Вложенные и приватные типы в owner не попадают (они не видны
            # снаружи), но существуют — иначе new ActiveEmotion читается
            # как обращение к несуществующему типу.
            self.declared_anywhere.update(RE_ANY_TYPE_DECL.findall(s))
            self.structs.update(RE_STRUCT_DECL.findall(s))
            for t, name in RE_MEMBER_DECL.findall(s):
                self.member_type[name].add(t)
            here = set(RE_ANY_TYPE_DECL.findall(s))
            # Конструкторы: имя метода совпадает с именем типа, объявленного
            # в этом же файле. Тёзка-метод в C# невозможен, поэтому сверка
            # по имени здесь надёжна.
            for name in here:
                for m in re.finditer(
                        r'^\s*(?:public|internal|protected|private)\s+'
                        + re.escape(name) + r'\s*\(([^)]*)\)\s*(?::[^;{]*)?\{',
                        s, re.M):
                    self.ctors[name].append(self.arity(m.group(1)))
                self.ctor_files[name].add(p)
                if re.search(r'partial\s+(?:class|struct)\s+' + re.escape(name), s):
                    self.partial.add(name)

            for name, params in RE_METHOD_DECL.findall(s):
                if name in KEYWORD_CALLS:
                    continue
                # Запоминаем и типы, объявленные в этом файле: у одного
                # имени метода бывают тёзки в разных классах, и без хозяина
                # проверка сверяла вызов Одного.Build с сигнатурой Другого.
                self.methods[name].append((self.param_types(params), here))

    @staticmethod
    def param_types(params):
        params = params.strip()
        if not params:
            return []
        out = []
        for part in SPLIT_ARGS.split(params):
            part = re.sub(r'=.*$', '', part.strip()).strip()
            toks = part.split()
            out.append(toks[-2] if len(toks) >= 2 else (toks[0] if toks else ''))
        return out

    @staticmethod
    def arity(params):
        """
        Сколько аргументов принимает список параметров: (минимум, максимум).

        Параметр со значением по умолчанию можно не передавать, params
        принимает сколько угодно — отсюда вилка, а не число.
        """
        params = params.strip()
        if not params:
            return (0, 0)
        parts = Checker.top_level(params)
        least = 0
        for part in parts:
            if 'params ' in part:
                return (least, 99)
            if '=' not in part:
                least += 1
        return (least, len(parts))

    @staticmethod
    def top_level(text):
        """Разбить по запятым нулевой глубины. Скобки и обобщения — глубина."""
        parts, depth, cur = [], 0, ''
        for ch in text:
            if ch in '([{<':
                depth += 1
            elif ch in ')]}>':
                depth -= 1
            if ch == ',' and depth == 0:
                parts.append(cur)
                cur = ''
            else:
                cur += ch
        parts.append(cur)
        return [x for x in (p.strip() for p in parts) if x]

    @staticmethod
    def call_args(body, open_paren):
        """Текст внутри скобок вызова. None, если скобки не закрылись."""
        depth, i = 0, open_paren
        while i < len(body):
            if body[i] in '([{':
                depth += 1
            elif body[i] in ')]}':
                depth -= 1
                if depth == 0:
                    return body[open_paren + 1:i]
            i += 1
        return None

    # Где женскому полу позволено упоминаться. Правило автора: женщины
    # в этом мире — уникальные лица, их нельзя сгенерировать, вытянуть
    # с полки или получить с вылазки. Значит Gender.Female имеет право
    # стоять только там, где персонажа пишут поимённо, — и нигде больше.
    WOMEN_ALLOWED = (
        'Core/Grammar.cs',        # само определение рода
        'Tests/SelfCheck.cs',     # доказательство, а не выдача
    )

    # Клавиша, отведённая под консоль. Занимать её нельзя ничем:
    # консоль открывается одним движением и обязана открываться всегда,
    # а разбирательство «почему тильда делает что-то другое» стоит
    # дороже любой сэкономленной клавиши.
    CONSOLE_KEYS = ('KeyCode.BackQuote', 'KeyCode.Tilde')

    # Её единственный хозяин — сама консоль (24 сентября; запрет автор
    # снял ради неё же). Всем остальным клавиша по-прежнему закрыта.
    CONSOLE_OWNER = 'Dev/CheatConsole.cs'

    # Слова, которые типом не бывают. `return _soulsOnField;` иначе
    # читалось бы как объявление поля и прятало бы ровно ту беду,
    # ради которой правило заведено.
    NOT_A_TYPE = {
        'return', 'new', 'is', 'as', 'in', 'out', 'ref', 'case', 'throw',
        'await', 'yield', 'else', 'do', 'and', 'or', 'not', 'when',
    }

    def dangling_fields(self):
        """
        Обращение к полю, которого в классе нет.

        Заведено 18 сентября, после того как я вынул из `CombatManager`
        список `_soulsOnField`, а `Update()` по нему ходить осталась —
        и **все три проверки сказали «чисто»**. Файл не собрался бы,
        а узнали бы об этом на следующем запуске Unity, то есть тот,
        у кого Unity есть, а не тот, кто сломал.

        Ловится узко и потому надёжно: в этом проекте поля класса
        пишутся с подчёркивания, а локальные переменные — нет. Значит,
        `_имя`, которого нет ни в одном объявлении файла, — ссылка
        на то, чего не существует.

        Объявлением считается строка **без круглых скобок**, кончающаяся
        на `;` или содержащая `=`: так под правило разом попадают
        и `Dictionary<ActionType, int> _actions = new();` (пробел внутри
        обобщённого типа ломал разбор по типу), и `float _a, _b;` —
        оба вида уже подвели первую редакцию этого правила.

        Чего правило не ловит нарочно: поля, унаследованные от базового
        класса в другом файле, и `partial`-классы. И то и другое здесь
        редкость, а правило, которое почти работает, хуже отсутствующего
        — поэтому такие файлы пропускаются целиком.
        """
        for p, s in self.src.items():
            # Здесь нужен именно strip: он гасит и комментарии, и строки.
            # С decomment правило ловило `so.FindProperty("_enemySpot")` —
            # имя чужого поля внутри строки, а не обращение к своему.
            body = strip(s)

            # Наследник чужого класса или partial: поля могут лежать
            # в другом файле, и судить отсюда нельзя.
            if re.search(r'\bpartial\s+(?:class|struct)\b', body):
                continue
            if re.search(r'class\s+\w+\s*:\s*(?!MonoBehaviour\b|ScriptableObject\b)\w',
                         body):
                continue

            # Признак объявления — **тип прямо перед именем**:
            # `Dictionary<ActionType, int> _actions =`, `float _a, _b;`,
            # `string _s = "";`. Отсутствие скобок признаком не годится:
            # половина полей здесь заводится через `= new()`, и первая
            # редакция правила отсеивала их все.
            declared = set()
            for lead, name in re.findall(
                    r'([\w<>,\[\]\.\?]+)\s+(_\w+)\s*(?=[;=,)])', body):
                if lead.rstrip('<>,[].?') in self.NOT_A_TYPE:
                    continue
                declared.add(name)

            used = set(re.findall(r'(?<![\w.])(_\w+)\b', body))

            for name in sorted(used - declared):
                i = body.find(name)
                self.report(p, line_of(body, i),
                            f'{name} — поля с таким именем в классе нет. '
                            'Осталось от удалённого? Файл не соберётся, '
                            'а узнается это только в Unity')

    def console_key(self):
        """
        Клавиша консоли занята чем-то другим.

        Решение автора: на «ё» ничего не вешать, там будет особенная
        консоль. Правило заведено до самой консоли нарочно — занятую
        клавишу освобождать дороже, чем не занимать, а узнают о занятии
        обычно в тот день, когда консоль уже написана.
        """
        for p, s in self.src.items():
            if str(p).replace('\\', '/').endswith(self.CONSOLE_OWNER):
                continue

            body = strip(s)
            for key in self.CONSOLE_KEYS:
                i = body.find(key)
                if i < 0:
                    continue

                self.report(p, line_of(body, i),
                            f'{key} отведена под консоль и занята быть '
                            'не может — выберите другую клавишу')

    def unique_women(self):
        """
        Женщину нельзя сгенерировать.

        Правило не про текст, а про мир: женские лица в этой игре
        уникальны и раздаются вручную. Стоит одному генератору — жатве,
        полке душ, спавнеру охотников — поставить Gender.Female, и
        уникальность кончится молча, а заметит это только тот, кто
        однажды прочитает «Безымянная душа сбежала».

        Держать такое памятью нельзя: генераторов в проекте шесть,
        и завтра их станет семь.
        """
        for p, s in self.src.items():
            flat = p.replace(chr(92), '/')
            if any(flat.endswith(ok) for ok in self.WOMEN_ALLOWED):
                continue

            body = strip(s)
            i = body.find('Gender.Female')
            if i < 0:
                continue

            self.report(p, line_of(body, i),
                        'женский пол назначается вне поимённых списков: '
                        'женщины в этом мире уникальны и не выдаются '
                        'ни жатвой, ни полкой, ни вылазкой')

    LOC_MARK = '// Перевод: текст через Loc'

    # Не текст игрока, а логика или служебное: сравнения, метки case,
    # журнал разработчика, атрибуты редактора, имена объектов. Тот же
    # список, что у Tools/lang/wrap.py — держать вместе.
    LOC_SKIP = [re.compile(x) for x in (
        r'Debug\.Log\w*\s*\(',
        r'\bcase\s+$',
        r'(==|!=)\s*$',
        r'\.(StartsWith|EndsWith|Contains|IndexOf|LastIndexOf|Equals|Replace|Split|TrimEnd|TrimStart)\s*\(\s*$',
        r'\[\s*(Tooltip|Header|MenuItem|Obsolete|InspectorName|ContextMenu|AddComponentMenu|CreateAssetMenu)\b[^\]]*$',
        r'new\s+GameObject\s*\(\s*$',
        r'\b(Find|FindWithTag|FindGameObjectWithTag|Instantiate|Load)\s*\(\s*$',
        r'\.name\s*=\s*$',
        r'PlayerPrefs\.\w+\s*\(\s*$',
        r'nameof\s*\(\s*$',
    )]

    def untranslated(self):
        """
        Русский текст игрока мимо перевода (docs/38-LANG.md).

        Файл, помеченный «// Перевод: текст через Loc», переведён целиком:
        любая новая русская строка в нём обязана идти через Loc.T / Loc.F,
        иначе игрок на другом языке увидит её по-русски — посреди чужого
        языка, и никто этого не заметит, пока не прочтёт.

        Второе: Loc.T в статической таблице. Таблица собирается один раз,
        и строка переводится на язык запуска навсегда — смена языка в меню
        её не тронет. В таблице — Loc.N, перевод — при показе.

        Файлы без пометки не трогаются: перевод идёт файл за файлом.
        """
        cyr = re.compile(r'[А-Яа-яЁё]')
        lit = re.compile(r'(\$@|@\$|\$|@)?"((?:[^"\\\n]|\\.)*)"')
        for p, s in self.src.items():
            if self.LOC_MARK not in s:
                continue
            body = blank_keep(s, literals_too=False)      # позиции те же, что в файле
            clean = blank_keep(s, literals_too=True)
            params = declared_params(clean)
            # Кусок фразы, приклеенный «+» к предыдущему, судится по голове
            # цепочки: «[ВИД] …» + «…», Loc.T("…" + "…"), // ключ на первой строке.
            found = list(lit.finditer(body))
            heads = []
            for k, m in enumerate(found):
                glued = k > 0 and re.fullmatch(r'\s*\+\s*', body[found[k - 1].end():m.start()])
                heads.append(heads[k - 1] if glued else m)
            judged = set()
            for m, head in zip(found, heads):
                if not cyr.search(m.group(2)):
                    continue
                m = head
                start = m.start()
                if start in judged:
                    continue          # цепочка судится один раз — по голове
                judged.add(start)
                line_start = body.rfind('\n', 0, start) + 1
                before = body[line_start:start]
                prev_start = body.rfind('\n', 0, max(0, line_start - 1)) + 1
                wide = body[prev_start:start]

                loc = re.search(r'Loc\.(T|F|N|Name)\s*\(\s*$', before)
                if loc:
                    frozen = self._static_field(body, start) if loc.group(1) in ('T', 'F') else None
                    if frozen == 'static':
                        self.report(p, line_of(body, start),
                                    f'Loc.{loc.group(1)} в статической таблице: строка переведётся '
                                    'один раз при загрузке, смена языка её не тронет — '
                                    'в таблице Loc.N, перевод при показе')
                    elif frozen == 'serialized':
                        self.report(p, line_of(body, start),
                                    f'Loc.{loc.group(1)} в сериализуемом поле: сцена запомнит строку '
                                    'по-русски на миг сборки, и игрок на другом языке прочтёт её '
                                    'по-русски — в поле Loc.N, перевод при показе')
                    continue
                line_end = s.find('\n', start)
                if '// ключ' in s[line_start:line_end if line_end >= 0 else len(s)]:
                    continue          # помечено руками: ключ, а не текст игрока
                if re.match(r'\[[A-ZА-ЯЁ ]+\]', m.group(2)):
                    continue          # «[ВИД] …» — служебная строка для разработчика
                if re.search(r'\b(const|readonly)\s+string\s+\w*(Name|Key|Id|Tag|Path)\s*=\s*$', before):
                    continue          # поле-идентификатор: имя объекта для Find, а не текст
                if any(r.search(before) for r in self.LOC_SKIP) or \
                   self.LOC_SKIP[0].search(wide) or call_skip(clean, start, params) or \
                   inside_debug(clean, start):
                    continue
                self.report(p, line_of(body, start),
                            f'русский текст мимо Loc: «{m.group(2)[:40]}» — игрок на другом '
                            'языке увидит его по-русски (Loc.T / Loc.F, docs/38-LANG.md)')

    # Поле, которое Unity сохраняет в сцене: [SerializeField] или открытое.
    SERIALIZED_FIELD = re.compile(r'(\[SerializeField\]|\bpublic\b)[^;(]*?\bstring\b(\[\])?\s+\w+\s*=(?!>)')

    # Имя души, вещи, титула в конце выражения: w.DisplayName, item.Name,
    # soul.EarnedTitle, SquadRoster.CommanderName (с .ToLowerInvariant() и без).
    # ShownName — имя с ремеслом или титулом (Naming.Full), ActorName — имя
    # в записи боя: оба по-русски, как всякое имя (30 сентября — панель
    # выделенного, совет и пересказ вылазки показывали их голыми).
    NAME_TAIL = re.compile(r'(?:\.\s*(?:Name|DisplayName|EarnedTitle|ShownName|ActorName)|\bCommanderName)'
                           r'(?:\s*\.\s*(?:ToLowerInvariant|ToLower|Trim)\s*\(\s*\))?\s*$')
    NAME_CHAIN = re.compile(r'[\w.?\[\]\s]+(?:\(\s*\))?')

    def raw_names(self):
        """
        Имя мимо Loc.Name (docs/38-LANG.md §3.3). Имя души, вещи, титула —
        данные: в записи и в логике по-русски, переводится только показ.
        В файле, помеченном переводом, имя, вставленное в текст игрока, —
        место Loc.F, кусок Append, дырка $"…{}…" — обязано идти через
        Loc.Name, иначе в английской игре посреди английской фразы стоит
        «Карган Старый Ворон». 29 сентября так нашлось десять мест разом:
        экран конца демо, полка склепа, заголовок экрана вещей, отчёт
        вылазки. Переменная, взятая из имени (string name = w.DisplayName),
        прослеживается до конца файла.
        """
        for p, s in self.src.items():
            if self.LOC_MARK not in s:
                continue
            clean = blank_keep(s, literals_too=True)
            body = blank_keep(s, literals_too=False)

            raw = set()
            for m in re.finditer(r'\b(?:string|var)\s+(\w+)\s*=\s*([^;]+);', clean):
                if any(self._bare_name(piece) for piece, _ in self._pieces(m.group(2), 0)):
                    raw.add(m.group(1))

            def judge(expr, pos, where):
                for piece, at in self._pieces(expr, pos):
                    judge_one(piece, at, where)

            def judge_one(expr, pos, where):
                e = expr.strip()
                if not (self._bare_name(e) or e in raw):
                    return
                line_start = s.rfind('\n', 0, pos) + 1
                line_end = s.find('\n', pos)
                line = s[line_start:line_end if line_end >= 0 else len(s)]
                if '// ключ' in line or inside_debug(clean, pos):
                    return
                if re.search(r'new\s+GameObject|\.name\s*=|PlayerPrefs', line):
                    return
                self.report(p, line_of(s, pos),
                            f'имя мимо Loc.Name: {where} «{e[:40]}» — в английской игре '
                            'оно останется русским посреди английской фразы (docs/38-LANG.md §3.3)')

            # места Loc.F после строки формата и Append
            for m in re.finditer(r'\bLoc\.F\s*\(|\.Append(?:Line)?\s*\(', clean):
                args = self._top_args(clean, m.end() - 1)
                first = 1 if m.group(0).startswith('Loc') else 0
                for a, at in args[first:]:
                    judge(a, at, 'место Loc.F' if first else 'Append')

            # дырки интерполяции
            for m in re.finditer(r'(\$@|@\$|\$)"((?:[^"\\\n]|\\.)*)"', body):
                for h in re.finditer(r'\{([^{}:]+)(?::[^{}]*)?\}', m.group(2)):
                    judge(h.group(1).split(',')[0], m.start(2) + h.start(1), 'дырка $"…"')

            # прямо в надпись: label.text = w.DisplayName;
            for m in re.finditer(r'\.text\s*\+?=\s*([^;]+);', clean):
                judge(m.group(1), m.start(1), 'надпись .text')

            # Через помощника файла: метод кладёт свой параметр в надпись,
            # и имя, переданное ему, видит игрок. 30 сентября так нашлось
            # Row(_store, item.Name, …) — у сундука вещи мешка шли
            # по-русски посреди английского экрана.
            for method, places in self._text_params(clean).items():
                for m in re.finditer(r'\b' + re.escape(method) + r'\s*\(', clean):
                    args = self._top_args(clean, m.end() - 1)
                    for i in places:
                        if i < len(args):
                            judge(args[i][0], args[i][1], f'надпись через {method}()')

            # Имя, рождённое переведённым: душа, вещь, вылазка с Loc.T в имени
            # записывается на языке игрока, и та же игра на другом языке —
            # другие данные. Вылазка к тому же выбирает грех души по хэшу
            # своего названия: перевод менял бы исход.
            for m in re.finditer(r'new\s+(SoulData|InventoryItem|Inventory\.InventoryItem|Mission)\s*\(\s*Loc\.T\s*\(', body):
                self.report(p, line_of(body, m.start()),
                            f'имя {m.group(1)} через Loc.T: имя — данные, в записи и в логике по-русски — '
                            'Loc.N, перевод при показе (Loc.Name, docs/38-LANG.md §3.3)')

    @staticmethod
    def _pieces(expr, pos):
        """
        [(часть, позиция)] выражения: склейка через + и ветки ?: — каждая
        часть показывается игроку сама по себе. «a.ShownName + Break + …»
        и «пусто ? Loc.T(…) : ev.ActorName» — имя в одной из частей.
        """
        parts, depth, start = [], 0, 0
        cut = []
        i = 0
        while i < len(expr):
            c = expr[i]
            if c in '([{':
                depth += 1
            elif c in ')]}':
                depth -= 1
            elif depth == 0 and c == '?' and i + 1 < len(expr) and expr[i + 1] in '?.':
                i += 2
                continue
            elif depth == 0 and c in '+?:':
                cut.append(i)
            i += 1
        for end in cut + [len(expr)]:
            parts.append((expr[start:end], pos + start))
            start = end + 1
        return parts

    @staticmethod
    def _text_params(clean):
        """
        {метод: [номера параметров]} — строковые параметры, которые метод
        этого файла кладёт в надпись целиком (x.text = параметр;).
        """
        found = {}
        decl = re.compile(r'\b(?:void|string|Text|\w+)\s+(\w+)\s*\(([^()]*)\)\s*\{')
        for m in decl.finditer(clean):
            body_start = m.end() - 1
            depth, i = 0, body_start
            while i < len(clean):
                if clean[i] == '{':
                    depth += 1
                elif clean[i] == '}':
                    depth -= 1
                    if depth == 0:
                        break
                i += 1
            body = clean[body_start:i]
            places = []
            for n, param in enumerate(p for p in m.group(2).split(',') if p.strip()):
                words = param.split('=')[0].split()
                if len(words) < 2 or words[-2] != 'string':
                    continue
                if re.search(r'\.text\s*\+?=\s*' + re.escape(words[-1]) + r'\s*;', body):
                    places.append(n)
            if places:
                found[m.group(1)] = places
        return found

    def _bare_name(self, expr):
        """Голое имя: цепочка доступа без обёртки, кончающаяся именем."""
        e = expr.strip()
        return bool(self.NAME_TAIL.search(e)) and bool(self.NAME_CHAIN.fullmatch(e)) \
            and not re.search(r'\bnameof\b', e)

    @staticmethod
    def _top_args(clean, open_at):
        """[(аргумент, позиция)] вызова, чья скобка — clean[open_at]."""
        args, depth, cur_start, i = [], 0, open_at + 1, open_at
        while i < len(clean):
            c = clean[i]
            if c in '([{':
                depth += 1
            elif c in ')]}':
                depth -= 1
                if depth == 0:
                    args.append((clean[cur_start:i], cur_start))
                    return args
            elif c == ',' and depth == 1:
                args.append((clean[cur_start:i], cur_start))
                cur_start = i + 1
            elif c == ';' and depth <= 1:
                return args
            i += 1
        return args

    @staticmethod
    def _static_field(body, start):
        """
        Литерал — в инициализаторе поля, а не в методе. 'static' — поле
        static или const: собирается один раз. 'serialized' — поле, которое
        хранит сцена: её сборщик запишет туда строку на своём языке,
        и в игре инициализатор уже не выполнится. None — не поле.
        """
        clean = re.sub(r'"(?:[^"\\\n]|\\.)*"', lambda m: ' ' * len(m.group(0)), body)
        own = clean[clean.rfind('\n', 0, start) + 1:start]
        if re.search(r'\b(static|const)\b[^;(]*?=(?!>)', own):
            return 'static'
        if Checker.SERIALIZED_FIELD.search(own):
            return 'serialized'
        i, depth = start, 0
        while i > 0:
            i -= 1
            c = clean[i]
            if c in ')]}':
                depth += 1
            elif c in '([{':
                if not (depth == 0 and c == '{'):
                    depth = max(0, depth - 1)
            elif c == ';' and depth == 0:
                return None
            elif c == '\n':
                ls = clean.rfind('\n', 0, i) + 1
                line = clean[ls:i].strip()
                if not line:
                    continue
                if re.search(r'\b(static|const)\b[^;(]*?=(?!>)', line):
                    return 'static'
                if Checker.SERIALIZED_FIELD.search(line):
                    return 'serialized'
                if re.search(r'\)\s*$', line) and \
                   re.search(r'\b(void|string|bool|int|float|IEnumerable|IEnumerator)\b', line):
                    return None
        return None

    def constructor_arity(self):
        """
        CS1729: у типа нет конструктора с таким числом аргументов.

        Заведено по следу: ветка не собиралась из-за `new RelationshipSystem()`,
        а параметрless-конструктор существовал **только в заглушке стенда**.
        Проверка смотрела на стенд и молчала. Это тот же корень, что и
        у округления в заглушке (13-DRIFT.md): прибор предложил то, чего
        в игре нет.

        Осторожность важнее полноты. Пропускаем всё, в чём не уверены:
        типы, объявленные больше чем в одном файле, partial, обобщённые
        и всё, чего в проекте не объявляли.
        """
        for p, s in self.src.items():
            body = strip(s)
            for m in re.finditer(r'\bnew\s+(?:[A-Z]\w*\s*\.\s*)*([A-Z]\w+)\s*\(', body):
                t = m.group(1)

                if t in self.partial or t not in self.ctor_files:
                    continue
                if len(self.ctor_files[t]) != 1:
                    continue          # тёзки в разных файлах — не разобрать
                if t not in self.declared_anywhere:
                    continue

                args = self.call_args(body, m.end() - 1)
                if args is None:
                    continue

                given = len(self.top_level(args))
                shapes = self.ctors[t] or [(0, 0)]   # нет своих — только пустой

                if any(lo <= given <= hi for lo, hi in shapes):
                    continue

                want = ', '.join(f'{lo}' if lo == hi else f'{lo}-{hi}'
                                 for lo, hi in sorted(set(shapes)))
                self.report(p, line_of(body, m.start()),
                            f'CS1729: у типа {t} нет конструктора на {given} '
                            f'аргумент(ов) — есть на {want}. Если такой есть '
                            f'в заглушке стенда, то это заглушка врёт, а не игра')

    def struct_only_type(self, member):
        """
        Тип свойства или поля — но только если ответ однозначен.

        Тёзки в разных классах, обобщённые типы и Nullable пропускаем:
        у Nullable сравнение с null законно, а у тёзки мы не знаем,
        чей именно член перед нами.
        """
        types = self.member_type.get(member)
        if not types or len(types) != 1:
            return None

        t = next(iter(types))
        if t.endswith('?') or '<' in t or '[' in t:
            return None

        return t if t in self.structs else None

    def struct_vs_null(self):
        """
        CS0019: структуру нельзя сравнить с null.

        Заведено по следу: ветка не собиралась из-за `decision == null`,
        где Decision — struct. Ошибка жила в файле, которого не компилировал
        никто, кроме Юнити, и нашлась только на редакторе. Это дёшево
        находить здесь: тип структуры объявлен в проекте, тип свойства —
        тоже.

        Ловим три формы и, как и с конструкторами, пропускаем всё,
        в чём не уверены: лучше промолчать, чем оболгать.
        """
        for p, s in self.src.items():
            body = strip(s)
            hits = []

            # 1. Прямо по члену: что-нибудь.LastDecisionDetail == null
            for m in re.finditer(r'\.(\w+)\s*[!=]=\s*null\b', body):
                t = self.struct_only_type(m.group(1))
                if t:
                    hits.append((m.start(), m.group(1), t))

            # 2. Через var: тип выводится из члена справа.
            local = {}
            for m in re.finditer(r'\bvar\s+(\w+)\s*=\s*[^;]*?\.(\w+)\s*;', body):
                t = self.struct_only_type(m.group(2))
                if t:
                    local[m.group(1)] = t

            # 3. Явным типом: Decision d = ...
            for name in self.structs:
                for m in re.finditer(
                        r'(?<![\w.])' + re.escape(name) + r'\s+(\w+)\s*=[^=]', body):
                    local[m.group(1)] = name

            for var, t in local.items():
                for m in re.finditer(
                        r'(?<![\w.])' + re.escape(var) + r'\s*[!=]=\s*null\b', body):
                    hits.append((m.start(), var, t))

            for pos, what, t in hits:
                self.report(p, line_of(body, pos),
                            f'CS0019: {what} — это структура {t}, '
                            f'её нельзя сравнить с null. Если проверка нужна, '
                            f'сравнивать надо поле или объявлять {t}?')

    def is_scene_component(self, path):
        """Компонент ли это, который вообще может стоять в сцене."""
        return bool(RE_MONO_CLASS.search(self.src.get(path, '')))

    @staticmethod
    def scene_files():
        """
        Сцены проекта, где бы ни стояла текущая папка.

        Раскладок две, и обе рабочие: локально репозиторий — это сама
        папка Assets/Scripts внутри проекта Unity, в облаке он же лежит
        целиком. Плюс запуск из корня проекта. Один жёсткий путь ловил
        только часть из них и молча пропускал остальные.
        """
        roots = (
            os.path.join('Assets', 'Scenes'),                  # корень проекта Unity
            os.path.join('..', 'Scenes'),                      # Assets/Scripts, локально
            os.path.join('..', '..', 'Assets', 'Scenes'),      # Assets/Scripts, облако
        )

        found = []
        for root in roots:
            found.extend(glob.glob(os.path.join(root, '*.unity')))

        return sorted({os.path.abspath(p) for p in found})

    def scene_presence(self):
        """
        Тип ищут в сцене, а его нет ни в одной.

        Заведено по следу, который тянется через весь проект. За один
        день эта поломка нашлась четырежды, и каждый раз её находил
        человек, глазами, случайно:

        - DialogueCameraController не стоял нигде — наезда камеры
          на говорящего не случалось ни разу за всё время;
        - PlayerInventory не стоял нигде — плата после боя уходила в никуда;
        - SoulManager не стоял нигде — души не угасали;
        - навмеша не было ни в одной сцене — никто не мог сделать шага.

        Все четыре — один разрыв: система написана, звена до сцены нет.
        Компилятор молчит, потому что код верен. Стенд молчит, потому что
        сцен не знает. Молчат все, и узнаётся это, только когда кто-то
        сядет играть.

        Ловится дёшево: имя файла равно имени MonoBehaviour (правило
        проекта), у файла есть .meta с GUID, а сцена — текст, в котором
        GUID либо встречается, либо нет. Unity для этого не нужен,
        то есть проверка доступна и облачной сессии.

        Осторожность как везде: если тип кто-то создаёт на ходу
        (AddComponent, AddIfMissing, Require) — молчим, это законно.
        """
        # Имя типа -> GUID его скрипта. Считаем до сцен: по нему видно,
        # настоящий это проект или временная папка самопроверки.
        owner = {}
        for p in self.files:
            meta = p + '.meta'
            if not os.path.exists(meta):
                continue
            m = re.search(r'guid:\s*([0-9a-f]{32})',
                          io.open(meta, encoding='utf-8', errors='replace').read())
            if m:
                owner[os.path.splitext(os.path.basename(p))[0]] = m.group(1)

        scenes = self.scene_files()
        if not scenes:
            # Отсутствие данных — событие, а не ноль. Раньше здесь стоял
            # молчаливый return, и правило целиком не работало при запуске
            # из Assets/Scripts — то есть ровно так, как его запускать
            # велит CLAUDE.md. Отчёт при этом печатал «Чисто»: та же
            # болезнь, против которой правило и заведено, этажом выше.
            if owner:
                self.report('Assets/Scenes', 0,
                            'сцен не найдено — присутствие типов в сценах '
                            'проверить нечем. Запускать из корня проекта '
                            'Unity или из Assets/Scripts')
            return

        here = set()
        for sc in scenes:
            text = io.open(sc, encoding='utf-8', errors='replace').read()
            here.update(re.findall(r'guid:\s*([0-9a-f]{32})', text))

        searched, made = defaultdict(set), set()
        for p, s in self.src.items():
            body = strip(s)
            for m in RE_FIND_TYPE.finditer(body):
                searched[m.group(1).rsplit('.', 1)[-1]].add(p)
            for m in RE_MAKE_TYPE.finditer(body):
                made.add(m.group(1).rsplit('.', 1)[-1])

        for name in sorted(searched):
            if name in made:
                continue
            guid = owner.get(name)
            if guid is None:
                continue        # не наш скрипт: тип движка или обобщённый
            if guid in here:
                continue

            # Абстрактный в сцене стоять не может — там стоят наследники,
            # и поиск по базовому типу находит именно их. Своего GUID
            # у базы в сцене нет и не будет никогда.
            if re.search(r'abstract\s+(?:partial\s+)?class\s+' + re.escape(name),
                         self.src.get(owner_file(self.files, name), '')):
                continue

            # Ищущий сам может быть вне сцен — отладочный спавнер, которого
            # никто не ставит. Тогда и поиск никогда не случится, и говорить
            # не о чем: ложная тревога обесценивает весь отчёт.
            #
            # Но молчать так можно только про компоненты. Статический класс
            # в сцене не стоит и стоять не может, а код его исполняется
            # откуда угодно — значит поиск живой. Именно так устроен
            # TitleCeremony, и без этой оговорки правило проглядело бы
            # ровно ту находку, ради которой заведено.
            if all(self.is_scene_component(p) and
                   owner.get(os.path.splitext(os.path.basename(p))[0]) not in here
                   for p in searched[name]):
                continue

            where = ', '.join(sorted(
                os.path.basename(p) for p in searched[name])[:3])
            self.report(owner_file(self.files, name), 0,
                        f'{name} ищут в сцене ({where}), но его нет ни в одной '
                        f'из {len(scenes)}: поиск всегда вернёт null, и всё, '
                        f'что за ним, молча не произойдёт')

    def report(self, path, line, text):
        self.problems.append((path, line, text))

    # ---------- проверки ----------

    def duplicate_types(self):
        seen = defaultdict(list)
        for p, s in self.src.items():
            m = RE_NAMESPACE.search(s)
            ns = m.group(1) if m else ''
            for _, name in RE_TYPE_DECL.findall(s):
                seen[(ns, name)].append(p)
        for (ns, name), paths in sorted(seen.items()):
            if len(paths) > 1:
                self.report(paths[1], 0, f'CS0101: {ns}.{name} объявлен дважды: {paths}')

    def braces(self):
        for p, s in self.src.items():
            body = strip(s)
            if body.count('{') != body.count('}'):
                self.report(p, 0, f"скобки не сходятся: {body.count('{')} открывающих, {body.count('}')} закрывающих")

    def linq(self):
        for p, s in self.src.items():
            if 'using System.Linq' in s:
                continue
            body = strip(s)
            for m in LINQ.finditer(body):
                # Mathf.Min, Vector2.Min и прочие статические — не LINQ
                before = body[max(0, m.start() - 24):m.start()]
                if re.search(r'(Mathf|Vector2|Vector3|Vector4|Math)$', before):
                    continue
                # Метод с таким именем есть в самом проекте — значит это
                # свой вызов, а не LINQ (SelectionComponent.Select и т.п.).
                if m.group(1) in self.methods:
                    continue
                self.report(p, line_of(body, m.start()),
                            f'возможно CS1061: .{m.group(1)}() без using System.Linq')
                break

    def missing_usings(self):
        for p, s in self.src.items():
            m = RE_NAMESPACE.search(s)
            ns = m.group(1) if m else ''
            visible = set(re.findall(r'using\s+([\w.]+)\s*;', s))
            parts = ns.split('.')
            for i in range(len(parts), 0, -1):
                visible.add('.'.join(parts[:i]))

            body = strip(s)
            declared_here = set(RE_ANY_TYPE_DECL.findall(s))
            # члены перечислений и имена свойств дают ложные срабатывания,
            # поэтому смотрим только на места, где имя стоит как тип
            as_type = re.compile(
                r'(?:^|[\s(<,\[])({name})(?:\s+\w|\s*[<>\)\.,\[]|\s*\{{)')

            # Имя, которого в файле нет словом, не найдёт ни один из четырёх
            # поисков ниже: все они требуют его целиком. Отсев по словам
            # файла — до регулярок: без него правило шло полминуты.
            words = set(re.findall(r'\w+', body))

            for t, nss in self.owner.items():
                if t not in words:
                    continue
                if t in declared_here or not nss or any(n in visible for n in nss):
                    continue
                pat = re.compile(r'(?<![\w.])' + re.escape(t) + r'\s+(?:\w+\s*[;=,)]|\w+\s*\()')
                mm = pat.search(body) or re.search(
                    r'(?<![\w.])' + re.escape(t) + r'\.\w', body) or re.search(
                    r'\bnew\s+' + re.escape(t) + r'\b', body) or re.search(
                    r'<\s*' + re.escape(t) + r'\s*>', body)
                if mm:
                    self.report(p, line_of(body, mm.start()),
                                f'CS0246: {t} объявлен в {sorted(nss)}, а здесь namespace {ns} без using')

    def string_for_enum(self):
        for p, s in self.src.items():
            body = strip(s)
            declared_in_file = set(RE_ANY_TYPE_DECL.findall(s))
            for m in RE_CALL.finditer(s):
                name, args = m.group(1), m.group(2)
                if name not in self.methods or '"' not in args:
                    continue
                parts = [a.strip() for a in SPLIT_ARGS.split(args)]
                # Вызов вида Тип.Метод(...) сверяем только с методами
                # этого типа. Иначе тёзка из чужого класса даёт ложное
                # срабатывание — и оно тем вреднее, что выглядит настоящим.
                # Хвост строки перед вызовом, а не весь файл до него: поиск
                # с якорем в конце по всему началу файла на каждый вызов
                # делал правило квадратичным.
                q = re.search(r'(\w+)\s*\.\s*' + re.escape(name) + r'\s*\($',
                              s[max(0, m.end(1) + 1 - 300):m.end(1) + 1])
                qualifier = q.group(1) if q else None

                # Вызов без хозяина — это метод своего же класса. Сверять
                # его с тёзкой из другого файла нельзя: компилятор туда
                # даже не смотрит, а проверка выдавала ошибку на ровном месте.
                here = declared_in_file
                signatures = self.methods[name]
                if not qualifier:
                    mine = [x for x in signatures if set(x[1]) & here]
                    if mine:
                        signatures = mine

                for sig, owners in signatures:
                    if qualifier and qualifier in self.declared_anywhere \
                            and qualifier not in owners:
                        continue
                    if len(sig) != len(parts):
                        continue
                    for i, (a, t) in enumerate(zip(parts, sig)):
                        base = t.replace('?', '').split('.')[-1]
                        if a.startswith('"') and a.endswith('"') and base in self.enums:
                            self.report(p, line_of(s, m.start()),
                                        f'CS1503: {name}(…) — аргумент {i + 1} объявлен как {t}, передана строка {a}')
                    break

    def use_before_declaration(self):
        """
        Переменная используется выше строки, где объявлена.

        Смотреть надо строго внутри одного блока: одноимённая переменная
        в соседнем методе или в предыдущем витке цикла — не ошибка.
        Поэтому окно поиска обрезается по началу блока, в котором стоит
        объявление, а глубина считается по скобкам.
        """
        decl = re.compile(r'(?:var|[A-Za-z_][\w<>,\[\]\.]*)\s+([a-z_]\w*)\s*=\s*new\b')
        for p, s in self.src.items():
            body = strip(s)

            # глубина вложенности для каждой позиции
            depth = [0] * (len(body) + 1)
            d = 0
            for i, ch in enumerate(body):
                if ch == '{':
                    d += 1
                elif ch == '}':
                    d -= 1
                depth[i + 1] = d

            for m in decl.finditer(body):
                name, pos = m.group(1), m.start()
                own = depth[pos]

                # начало блока: ближайшая позиция слева с меньшей глубиной
                start = pos
                while start > 0 and depth[start] >= own:
                    start -= 1

                window = body[start:pos]
                if re.search(r'(?<![\w.])' + re.escape(name) + r'\s*\.', window):
                    self.report(p, line_of(body, pos),
                                f"CS0841: '{name}' используется выше своего объявления")

    def file_names(self):
        for p, s in self.src.items():
            base = os.path.basename(p)[:-3]
            classes = RE_MONO_CLASS.findall(s)
            if classes and base not in classes:
                self.report(p, 0,
                            f'Unity: имя файла не совпадает с MonoBehaviour {classes} — '
                            f'скрипт нельзя повесить на объект')
            if ' ' in os.path.basename(p):
                self.report(p, 0, 'Unity: пробел в имени файла')

    def editor_guards(self):
        for p, s in self.src.items():
            if 'using UnityEditor' not in s:
                continue
            if os.sep + 'Editor' + os.sep in p:
                continue
            if '#if UNITY_EDITOR' not in s:
                self.report(p, 0, 'сборка плеера упадёт: using UnityEditor без #if UNITY_EDITOR')
                continue
            first_using = s.index('using UnityEditor')
            first_guard = s.index('#if UNITY_EDITOR')
            if first_guard > first_using:
                self.report(p, line_of(s, first_using),
                            'using UnityEditor стоит выше #if UNITY_EDITOR — обёртка не работает')

    def singletons(self):
        """
        Одиночка, назначающий себя в Start.

        Awake случается раньше любого Start в сцене, чей бы он ни был,
        а порядок Start между объектами Unity не определяет никак. Значит
        одиночка, ставящий Instance в Start, доступен другим Start
        через раз — и не падает, а тихо оказывается null. Проверка Instance
        != null, которая стоит почти везде, такую подписку молча пропустит.

        В этом проекте так уже случилось однажды: отказ не поднимал
        ни журнал, ни тишину, и ничего при этом не ломалось.
        """
        for p, s in self.src.items():
            if 'Instance = this' not in s:
                continue

            body = strip(s)

            # Границы метода ищем грубо: от заголовка до следующего
            # объявления метода. Для этой проверки хватает.
            for m in re.finditer(r'void\s+(Awake|Start|OnEnable)\s*\(\s*\)', body):
                nxt = re.search(r'\n\s*(?:private|public|protected|internal|void|IEnumerator)\s',
                                body[m.end():])
                chunk = body[m.end(): m.end() + (nxt.start() if nxt else 4000)]

                if 'Instance = this' in chunk and m.group(1) != 'Awake':
                    self.report(p, line_of(body, m.start()),
                                f'Instance назначается в {m.group(1)}, а не в Awake — '
                                'другие Start увидят null через раз')

    def deferred(self):
        """
        Отложенные файлы: подписаны ли и не зовёт ли их живой код.

        Второе важнее первого. Тип, объявленный только в .cs.later,
        для Unity не существует — и живой файл, который его зовёт,
        не соберётся вообще. Ошибка при этом выглядит как «не найден
        тип», а причина её — в переименовании месячной давности.
        """
        later = []
        for root, dirs, files in os.walk('.'):
            dirs[:] = [d for d in dirs
                       if d not in SKIP_DIRS and not d.startswith('.')]
            for f in files:
                if f.endswith(LATER_EXT):
                    later.append(os.path.join(root, f))

        if not later:
            return

        declared_later = {}      # тип -> файл, где он отложен

        for path in later:
            text = io.open(path, encoding='utf-8', errors='replace').read()

            first = text.split('\n', 1)[0].strip()
            if first != LATER_HEADER:
                self.report(path, 1, 'отложенный файл без заголовка '
                                     f'«{LATER_HEADER}» в первой строке')

            for _, name in RE_TYPE_DECL.findall(text):
                declared_later[name] = path

        if not declared_later:
            return

        for path, text in self.src.items():
            body = strip(text)
            for name, where in declared_later.items():
                if re.search(r'(?<![\w.])' + re.escape(name) + r'(?![\w])', body):
                    self.report(path, 0,
                                f'зовёт {name}, а он отложен в {where} — '
                                'Unity такого типа не увидит')

    def input_handler(self):
        """
        Старый Input под настройкой «только новый Input System».

        Ошибка невидимая вдвойне: компилятор пропускает, а падает оно
        только в рантайме и только на первом нажатии клавиши — то есть
        игра запускается, показывает лагерь и не слушается вообще ничем.
        Ни камеры, ни выделения, ни жатвы душ, ни кнопок интерфейса:
        StandaloneInputModule на EventSystem тоже читает старый Input.

        activeInputHandler: 0 — старый, 1 — новый, 2 — оба.
        """
        settings = os.path.join('ProjectSettings', 'ProjectSettings.asset')
        if not os.path.exists(settings):
            return

        text = io.open(settings, encoding='utf-8', errors='replace').read()
        m = re.search(r'activeInputHandler:\s*(\d+)', text)
        if not m:
            # Настройки нет — это событие, а не ноль: молча решить,
            # что всё в порядке, значит однажды проглядеть тот же баг.
            self.report(settings, 0, 'activeInputHandler не найден — '
                                     'проверить ввод нечем')
            return

        if m.group(1) != '1':
            return

        users = sorted(p for p, s in self.src.items() if RE_OLD_INPUT.search(strip(s)))
        if not users:
            return

        self.report(settings, line_of(text, m.start()),
                    'activeInputHandler: 1 (только новый Input System), '
                    'а старый UnityEngine.Input зовут ' + str(len(users))
                    + ' файлов — в рантайме это исключение на первом же '
                      'нажатии. Ставить 2 (оба) или переписывать ввод.')

        for path in users:
            self.report(path, 0, 'зовёт старый UnityEngine.Input')

    def unknown_new(self):
        known = set(self.owner) | self.declared_anywhere | UNITY_TYPES | DOTNET_TYPES
        for p, s in self.src.items():
            body = strip(s)
            for m in RE_NEW.finditer(body):
                t = m.group(1)
                if t in known or t.startswith(('Unity', 'System', 'Editor')):
                    continue
                self.report(p, line_of(body, m.start()), f'CS0246: тип {t} нигде не объявлен')

    # Файлы, из которых складывается решение воина. Всё, что здесь
    # написано, обязано давать одинаковый ответ на одинаковый вход —
    # это правило проекта, и оно же условие будущей сетевой игры
    # (docs/15-AFTER.md §1, Assets/Scripts/Multiplayer/*.cs.later).
    DECIDES = (
        'AOS Engine/BehaviorResolver.cs',
        'AOS Engine/PhraseGenerator.cs',
        'AOS Engine/AutoBattleContext.cs',
        'AOS Engine/AutoBattleResolver.cs',
        'AOS Engine/CombatDecisionContext.cs',
        'AOS Engine/TemperamentPredictor.cs',
        'AOS Engine/SkillCatalog.cs',
        'Core/Soul/SoulDecay.cs',
        'Core/Soul/ShellBinder.cs',
        'Core/Soul/ShellChoice.cs',
        'Gameplay/BodyWorth.cs',
        'AOS Engine/TitleManager.cs',
        'AOS Engine/TitleDatabase.cs',
    )

    # Ключ к текущему времени, случайности или к тому, что у каждой машины
    # своё. Порядок обхода сцены сюда не попадает намеренно: он ловится
    # глазами, а FindObjectsSortMode.InstanceID в проекте стоит везде.
    UNSTABLE = (
        ('Random.', 'случайность'),
        ('Time.', 'текущее время'),
        ('DateTime.', 'часы машины'),
        ('Guid.NewGuid', 'значение, своё у каждой машины'),
    )

    def determinism(self):
        """
        Решение обязано быть повторяемым.

        Правило «одинаковый вход даёт одинаковый выход» держится в проекте
        с самого начала — ради того, чтобы игрок мог учиться на объяснениях.
        Держится оно тем, что все помнят; проверки не было ни одной.

        Ловим не всё подряд, а только те файлы, из которых складывается
        решение. В остальных Time.deltaTime законен: поворот головы при
        отказе, откат умения, задержка реплики — это показ, а не выбор.
        """
        for p, s in self.src.items():
            flat = p.replace(chr(92), '/')
            if not any(flat.endswith(d) for d in self.DECIDES) \
                    and '/Modules/' not in flat \
                    and not (flat.endswith('Module.cs') and 'AOS Engine' in flat):
                continue

            body = strip(s)
            for token, why in self.UNSTABLE:
                i = body.find(token)
                if i < 0:
                    continue
                self.report(p, line_of(body, i),
                            f'решение обязано быть повторяемым, а {token} '
                            f'даёт {why}. Если это показ, а не выбор — '
                            f'вынести из файла решения')


    # Методы, которые зовёт не код, а движок, редактор или сцена.
    # Список закрытый: всё, чего в нём нет, обязано зваться по имени.
    ENGINE_CALLS = {
        'Awake', 'Start', 'Update', 'FixedUpdate', 'LateUpdate',
        'OnEnable', 'OnDisable', 'OnDestroy', 'OnGUI', 'OnValidate',
        'Reset', 'OnApplicationQuit', 'OnApplicationPause',
        'OnApplicationFocus', 'OnDrawGizmos', 'OnDrawGizmosSelected',
        'OnTriggerEnter', 'OnTriggerExit', 'OnTriggerStay',
        'OnCollisionEnter', 'OnCollisionExit', 'OnCollisionStay',
        'OnMouseDown', 'OnMouseUp', 'OnMouseEnter', 'OnMouseExit',
        'OnMouseOver', 'OnMouseDrag', 'OnBecameVisible', 'OnBecameInvisible',
        'OnAnimatorMove', 'OnAnimatorIK', 'OnPreRender', 'OnPostRender',
        'OnPointerClick', 'OnPointerEnter', 'OnPointerExit', 'OnPointerDown',
        'OnPointerUp', 'OnSelect', 'OnDeselect', 'OnSubmit',
        'ToString', 'Equals', 'GetHashCode', 'Dispose', 'CompareTo',
        'GetEnumerator', 'MoveNext', 'OnBeforeSerialize', 'OnAfterDeserialize',
        'OnPostprocessModel', 'OnPreprocessModel', 'OnPostprocessAllAssets',
    }

    # Приписки, которыми зовут не по имени: редактор, движок, инспектор.
    CALLED_BY_MARK = ('MenuItem', 'ContextMenu', 'RuntimeInitializeOnLoadMethod',
                      'InitializeOnLoadMethod', 'DidReloadScripts',
                      'PostProcessBuild', 'PostProcessScene', 'Test', 'SetUp')

    def orphans(self):
        """
        Публичный метод, которого не зовёт никто.

        Болезнь проекта одна и та же с самого начала: **система написана,
        звена нет.** Восемь случаев за четыре дня, и самый дорогой из них —
        `CombatManager.CollectLootWithSquad`: объявлен, через него
        единственный вызов раздачи добычи, и не вызван ни разу. После боя
        добыча не доставалась никому, а найдено это было чтением цепочки
        руками (`11-MISSING.md` §4).

        Читать цепочки руками можно, но не 240 файлов. Здесь то же самое
        считает машина.

        **Ищем осторожно, чтобы правилу верили.** Имя считается
        использованным, если встречается где угодно ещё: в другом вызове,
        в `nameof`, в подписке `+= Имя`, в строке сцены или префаба
        (`m_MethodName`), — то есть ложных тревог меньше, а пропусков
        больше. Правило, которое кричит зря, перестают читать, и тогда
        оно не стоит ничего.

        Не ловим: методы, которые зовёт движок (список выше), помеченные
        приписками редактора, `override` и `virtual` (их зовут через базу
        или интерфейс), а также конструкторы.
        """
        import re as _re

        bodies = {p: strip(s) for p, s in self.src.items()}
        blob = '\n'.join(bodies.values())

        # Сколько раз каждое слово встречается во всём коде — один раз,
        # а не поиском по всему коду на каждый метод (было пятнадцать секунд).
        from collections import Counter
        # Дырки интерполяции — тоже код: $"{Aftermath.Judged(…)}". strip()
        # вырезает строку целиком, и метод, который зовут только оттуда,
        # числился сиротой (MissionCatalog.Danger, Aftermath.Judged).
        holes = []
        for text in self.src.values():
            for m in _re.finditer(r'(?:\$@|@\$|\$)"((?:[^"\\\n]|\\.)*)"', text):
                holes.extend(_re.findall(r'\{([^{}]+)\}', m.group(1)))
        uses = Counter(_re.findall(r'\w+', blob + '\n' + '\n'.join(holes)))

        # Сцены и префабы зовут метод строкой: m_MethodName: Имя.
        wired = set()
        for root, _dirs, names in os.walk('.'):
            for n in names:
                if n.endswith(('.sh', '.ps1', '.bat', '.yml', '.yaml')):
                    try:
                        with open(os.path.join(root, n), encoding='utf-8',
                                  errors='ignore') as fh:
                            for m in _re.finditer(r'[\w.]+\.(\w+)', fh.read()):
                                wired.add(m.group(1))
                    except OSError:
                        pass
                    continue
                if not n.endswith(('.unity', '.prefab', '.asset')):
                    continue
                try:
                    with open(os.path.join(root, n), encoding='utf-8',
                              errors='ignore') as fh:
                        for m in _re.finditer(r'm_MethodName:\s*(\w+)', fh.read()):
                            wired.add(m.group(1))
                except OSError:
                    continue

        decl = _re.compile(
            r'^[ \t]*public\s+(?:static\s+|async\s+|unsafe\s+|extern\s+)*'
            r'(?!class|struct|enum|interface|delegate|event|const|abstract'
            r'|override|virtual|partial)'
            r'[\w<>\[\],.?]+\s+(\w+)\s*\(', _re.M)

        for p, body in bodies.items():
            flat = p.replace(chr(92), '/')
            stem = os.path.splitext(os.path.basename(flat))[0]

            for m in decl.finditer(body):
                name = m.group(1)

                if name == stem or name in self.ENGINE_CALLS or name in wired:
                    continue

                head = body[max(0, m.start() - 240):m.start()]
                if any(('[' + mark) in head for mark in self.CALLED_BY_MARK):
                    continue

                if uses[name] > 1:
                    continue

                self.orphan_list.append((p, line_of(body, m.start()), name))

    orphan_list = None


    # Сравнения со строкой: case "X", == "X", .Equals("X").
    RE_AWAITS = re.compile(
        r'(?:case\s+|[=!]=\s*|\.Equals\(\s*)"([A-Za-z][\w]{2,40})"')

    # Член перечисления: его имя рождается из ToString() и в исходниках
    # строкой не встречается. Без этого правило врёт на каждом таком.
    RE_ENUM_BODY = re.compile(r'\benum\s+\w+[^{]*\{([^}]*)\}', re.S)

    # Мёртвые ветки, уже найденные и записанные. Список нужен затем,
    # чтобы правило **роняло сборку на новых**: если бы оно просто
    # предупреждало, ветка, написанная сегодня, прошла бы незамеченной
    # ровно так же, как прошли FoundLoot и обида за предательство.
    #
    # Каждая строка здесь — с указанием, где записана. Без записи
    # в списке делать нечего.
    KNOWN_DEAD = {
        # LocationManager: базы локаций нет, CurrentLocation пуст,
        # и четырнадцать эффектов перков молча не срабатывают.
        # Сказано вслух в самом LocationManager.HasTag.
        'InVillage',
        'InCrypt',
        # PhraseGenerator ждёт объяснение от модуля "Engagement",
        # а среди тринадцати голосующих такого нет: есть механика
        # Gameplay/Engagement, но модулем она не стала. Строка
        # «уйти отсюда — значит подставить спину» не печатается
        # ни разу. 11-MISSING.md §8.
        'Engagement',
    }

    def dead_branches(self):
        """
        Ветка, которая ждёт строку, а строку эту не пишет никто.

        Сестра правила <c>orphans</c>. То ловит «написано и не вызвано»,
        это — «вызывается, но никогда не срабатывает», и второе заметить
        глазами ещё труднее: код исполняется, просто всегда мимо.

        Так умерли две ветви памяти воина: `MemoryModule` разбирает
        `FoundLoot` и `AllyBetrayedMe`, а писать их было некому
        (11-MISSING.md §6). Нашлось это чтением цепочек руками — и было
        бы найдено машиной в день, когда ветку написали.

        **Исключение, без которого правило врёт.** Имя члена перечисления
        рождается из `ToString()` и в исходниках строкой не встречается
        вовсе: `CommandKind.Defend.ToString()` даёт «Defend», хотя такой
        строки в коде нет. Проверено на живом случае — `DecisionContext`
        разбирает `"Hold"`, `"Defend"`, `"FallBack"`, и все три законны.
        Поэтому имена членов всех перечислений проекта из проверки
        изъяты.

        Обратную сторону — «пишут, но не ждёт никто» — не ловим нарочно:
        под неё попала бы каждая строка журнала и каждое имя, и правило
        утонуло бы в шуме. А правило, которому не верят, не стоит ничего.
        """
        # Именно decomment, а не strip: strip вычищает строковые
        # литералы, и правило смотрело бы в пустоту. Поймано на себе
        # при первом же запуске — находок было ноль, и ноль этот
        # ничего не значил.
        bodies = {p: decomment(s) for p, s in self.src.items()}
        blob = '\n'.join(bodies.values())

        # Имена всех членов всех перечислений проекта.
        enums = set()
        for body in bodies.values():
            for m in self.RE_ENUM_BODY.finditer(body):
                for part in m.group(1).split(','):
                    name = part.split('=')[0].strip()
                    if re.fullmatch(r'[A-Za-z]\w*', name or ''):
                        enums.add(name)

        seen = 0

        for p, body in bodies.items():
            for m in self.RE_AWAITS.finditer(body):
                seen += 1
                text = m.group(1)
                if text in enums or text in self.KNOWN_DEAD:
                    continue

                # Сколько раз строка встречается не в сравнении.
                made = 0
                for hit in re.finditer('"' + re.escape(text) + '"', blob):
                    before = blob[max(0, hit.start() - 24):hit.start()]
                    if re.search(r'(?:case\s+|[=!]=\s*|\.Equals\(\s*)$', before):
                        continue
                    made += 1

                if made > 0:
                    continue

                self.report(p, line_of(body, m.start()),
                            f'ветка ждёт строку "{text}", а пишет её никто: '
                            f'условие не срабатывает ни разу. Либо соединить '
                            f'того, кто эту строку производит, либо снять ветку')

        # Правило, которому нечего было проверять, обязано сказать это
        # вслух. Молчаливый ноль здесь означал бы не «чисто», а «сломано»:
        # сравнений со строками в проекте заведомо не ноль.
        for text in sorted(self.KNOWN_DEAD):
            alive = False
            for hit in re.finditer('"' + re.escape(text) + '"', blob):
                before = blob[max(0, hit.start() - 24):hit.start()]
                if re.search(r'(?:case\s+|[=!]=\s*|\.Equals\(\s*)$', before):
                    continue
                alive = True
                break

            if alive:
                self.report('Tools/check.py', 0,
                            f'"{text}" числится мёртвой веткой, а её уже пишут — '
                            f'убрать из KNOWN_DEAD, иначе список начнёт '
                            f'покрывать живое')

        # Соразмерно: в проекте из двух файлов сравнений со строкой может
        # не быть честно, а в проекте из двухсот — уже нет. Порог заведён
        # после того, как охрана сработала на крошечном образце
        # самопроверки и была права по букве и неправа по делу.
        if seen == 0 and len(self.src) >= 20:
            self.report('Tools/check.py', 0,
                        'dead_branches не нашло ни одного сравнения со строкой — '
                        'значит смотрит не туда. Ноль замечаний от него ничего '
                        'не значит, пока это так')


    # Первая сцена демо. С неё начинается сборка, и только с неё.
    FIRST_SCENE = "Prologue_Camp"

    def build_list(self):
        """
        Список сцен сборки: нет ли в нём того, чего нет на диске.

        Найдено 17 сентября, за три дня до показа: в
        `EditorBuildSettings.asset` числилась `Prologue_Escape.unity`,
        которой нет ни на диске, ни в сборщике сцен. Осталась от времён,
        когда побег был отдельной сценой, — а её свернули в набег
        («Демо вдвое короче», 7 сентября) и запись забыли.

        Сборку в этом проекте не делали **ни разу**, и потому никто
        не видел: первая же попытка упёрлась бы в недостающую сцену,
        и упёрлась бы в субботу перед показом.

        Проверяем две вещи, и обе — про сборку, а не про вкус:

        * каждая перечисленная сцена **существует**;
        * первой включённой идёт та, с которой демо начинается. Сборка
          стартует с первой в списке: переставь их местами — и игрок
          попадёт в набег, минуя лагерь.
        """
        here = os.path.dirname(os.path.abspath(__file__))

        settings = None
        for up in ('.', '..', os.path.join('..', '..'),
                   os.path.join('..', '..', '..')):
            guess = os.path.join(here, up, 'ProjectSettings',
                                 'EditorBuildSettings.asset')
            if os.path.isfile(guess):
                settings = os.path.normpath(guess)
                break

        if settings is None:
            # Молчать нельзя: правило, не нашедшее файла, обязано сказать
            # это вслух, а не сойти за «чисто» (13-DRIFT.md, scene_presence).
            self.report('ProjectSettings/EditorBuildSettings.asset', 0,
                        'списка сцен сборки не видно — правило build_list '
                        'ничего не проверило')
            return

        root = os.path.dirname(os.path.dirname(settings))

        with io.open(settings, encoding='utf-8', errors='ignore') as fh:
            text = fh.read()

        rows = re.findall(r'-\s+enabled:\s*(\d).*?path:\s*(\S+)', text, re.S)
        first_on = None

        for enabled, path in rows:
            if not os.path.isfile(os.path.join(root, path)):
                self.report('ProjectSettings/EditorBuildSettings.asset', 0,
                            f'в списке сборки есть {path}, а такой сцены '
                            f'на диске нет: сборка упрётся в неё')

            if enabled == '1' and first_on is None:
                first_on = path

        if first_on and self.FIRST_SCENE not in first_on:
            self.report('ProjectSettings/EditorBuildSettings.asset', 0,
                        f'сборка начнётся с {first_on}, а демо начинается '
                        f'с {self.FIRST_SCENE}: игрок попадёт не туда')

    def run(self):
        self.orphan_list = []
        self.determinism()
        self.duplicate_types()
        self.braces()
        self.linq()
        self.missing_usings()
        self.string_for_enum()
        self.use_before_declaration()
        self.file_names()
        self.editor_guards()
        self.input_handler()
        self.singletons()
        self.deferred()
        self.unknown_new()
        self.constructor_arity()
        self.struct_vs_null()
        self.scene_presence()
        self.unique_women()
        self.untranslated()
        self.raw_names()
        self.console_key()
        self.orphans()
        self.dead_branches()
        self.build_list()
        self.dangling_fields()
        return self.problems


def main():
    quiet = '--quiet' in sys.argv
    files = collect('.')
    if not files:
        print('Файлов .cs не найдено. Запускать из корня репозитория.')
        return 1

    checker = Checker(files)
    problems = checker.run()
    orphans = checker.orphan_list or []

    if not quiet:
        for path, line, text in sorted(problems):
            where = f'{path}:{line}' if line else path
            print(f'{where}: {text}')
        if problems:
            print()

    # Сироты печатаются всегда и никогда не роняют выход. Роняли бы —
    # локальная сессия потеряла бы чистый базис, на который опирается
    # каждый её коммит, и правило выключили бы целиком. Разбор сирот —
    # решение автора по каждой (соединить, отложить, удалить), а не
    # ошибка сборки.
    if orphans:
        print(f'Публичных методов, которых не зовёт никто: {len(orphans)}.'
              ' Каждый — «написано, до игры не доведено».')
        if not quiet:
            for path, line, name in sorted(orphans):
                print(f'  {path}:{line}: {name}')
        print()

    print(f'Файлов проверено: {len(files)}. Замечаний: {len(problems)}.')
    if not problems:
        print('Чисто. Это не гарантия сборки — только отсутствие ошибок известных видов.')
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
