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
RE_NEW = re.compile(r'\bnew\s+([A-Z]\w+)\s*[\(\{]')
SPLIT_ARGS = re.compile(r',(?![^<>()]*[>)])')


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
            for name, params in RE_METHOD_DECL.findall(s):
                if name in KEYWORD_CALLS:
                    continue
                self.methods[name].append(self.param_types(params))

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
                for sig in self.methods[name]:
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

    def run(self):
        self.duplicate_types()
        self.braces()
        self.linq()
        self.missing_usings()
        self.string_for_enum()
        self.use_before_declaration()
        self.file_names()
        self.editor_guards()
        self.input_handler()
        self.unknown_new()
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
