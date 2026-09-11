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


def line_of(text, pos):
    return text[:pos].count('\n') + 1


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

            for t, nss in self.owner.items():
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
            for m in RE_CALL.finditer(s):
                name, args = m.group(1), m.group(2)
                if name not in self.methods or '"' not in args:
                    continue
                parts = [a.strip() for a in SPLIT_ARGS.split(args)]
                # Вызов вида Тип.Метод(...) сверяем только с методами
                # этого типа. Иначе тёзка из чужого класса даёт ложное
                # срабатывание — и оно тем вреднее, что выглядит настоящим.
                q = re.search(r'(\w+)\s*\.\s*' + re.escape(name) + r'\s*\($',
                              s[:m.end(1) + 1])
                qualifier = q.group(1) if q else None

                for sig, owners in self.methods[name]:
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

    def run(self):
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
        return self.problems


def main():
    quiet = '--quiet' in sys.argv
    files = collect('.')
    if not files:
        print('Файлов .cs не найдено. Запускать из корня репозитория.')
        return 1

    problems = Checker(files).run()

    if not quiet:
        for path, line, text in sorted(problems):
            where = f'{path}:{line}' if line else path
            print(f'{where}: {text}')
        if problems:
            print()

    print(f'Файлов проверено: {len(files)}. Замечаний: {len(problems)}.')
    if not problems:
        print('Чисто. Это не гарантия сборки — только отсутствие ошибок известных видов.')
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
