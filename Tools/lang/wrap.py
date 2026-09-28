#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Перевести файл на Loc: обернуть русский текст игрока в Loc.T / Loc.F
(docs/38-LANG.md).

    python3 Tools/lang/wrap.py Assets/Scripts/UI/PauseMenu.cs            показать, что сделает
    python3 Tools/lang/wrap.py Assets/Scripts/UI/PauseMenu.cs --write    сделать

Что оборачивается:
    "Пауза"                     → Loc.T("Пауза")
    $"{name} колеблется: {why}." → Loc.F("{0} колеблется: {1}.", name, why)

Что НЕ трогается — это не текст игрока, а логика или служебное:
    Debug.Log*(…), case "…":, сравнения (== != StartsWith EndsWith Contains
    IndexOf Equals Replace Split), атрибуты [Tooltip(…)] [Header(…)]
    [MenuItem(…)], имена объектов (new GameObject(…), Find(…), .name =),
    ключи PlayerPrefs, строки уже внутри Loc.*.
Всё сомнительное печатается как «оставил» — его смотрит человек.

Правка — начало перевода файла, а не его конец: после неё — прочитать
разницу, пометить файл строкой «// Перевод: текст через Loc» и вписать
перевод в таблицу (Tools/lang/catalog.py).
"""

import re
import sys

CYR = re.compile(r'[А-Яа-яЁё]')

SKIP_BEFORE = [
    r'Debug\.Log\w*\s*\(',
    r'\bcase\s+$',
    r'(==|!=)\s*$',
    r'\.(StartsWith|EndsWith|Contains|IndexOf|LastIndexOf|Equals|Replace|Split|TrimEnd|TrimStart)\s*\(\s*$',
    r'\[\s*(Tooltip|Header|MenuItem|Obsolete|InspectorName|ContextMenu|AddComponentMenu|CreateAssetMenu)\b[^\]]*$',
    r'new\s+GameObject\s*\(\s*$',
    r'\b(Find|FindWithTag|FindGameObjectWithTag|Instantiate|Load)\s*\(\s*$',
    r'\.name\s*=\s*$',
    r'PlayerPrefs\.\w+\s*\(\s*$',
    r'Loc\.(T|F|N|Name)\s*\(\s*$',
    r'nameof\s*\(\s*$',
]
SKIP_RE = [re.compile(p) for p in SKIP_BEFORE]


def literals(src):
    """Строковые литералы файла: (начало, конец, текст литерала). Комментарии и символы пропускаются."""
    i, n = 0, len(src)
    out = []
    while i < n:
        c = src[i]
        if src.startswith('//', i):
            j = src.find('\n', i)
            i = n if j < 0 else j
            continue
        if src.startswith('/*', i):
            j = src.find('*/', i + 2)
            i = n if j < 0 else j + 2
            continue
        if c == "'":
            # символ: 'a', '\n', '\''
            j = i + 1
            if j < n and src[j] == '\\':
                j += 2
            else:
                j += 1
            if j < n and src[j] == "'":
                i = j + 1
                continue
            i += 1
            continue
        if c in '$@"':
            m = re.match(r'(\$@|@\$|\$|@)?"', src[i:])
            if not m:
                i += 1
                continue
            prefix = m.group(1) or ''
            start = i
            j = i + len(m.group(0))
            verbatim = '@' in prefix
            interp = '$' in prefix
            depth = 0
            while j < n:
                ch = src[j]
                if interp and depth == 0 and ch == '{':
                    if src.startswith('{{', j):
                        j += 2
                        continue
                    depth = 1
                    j += 1
                    continue
                if interp and depth > 0:
                    # внутри выражения: пропустить вложенные строки и скобки
                    if ch == '"':
                        k = j + 1
                        while k < n and src[k] != '"':
                            if src[k] == '\\':
                                k += 1
                            k += 1
                        j = k + 1
                        continue
                    if ch == "'":
                        j += 3 if src[j + 1] != '\\' else 4
                        continue
                    if ch == '{':
                        depth += 1
                    elif ch == '}':
                        depth -= 1
                    j += 1
                    continue
                if interp and ch == '}' and src.startswith('}}', j):
                    j += 2
                    continue
                if verbatim:
                    if ch == '"':
                        if src.startswith('""', j):
                            j += 2
                            continue
                        break
                    j += 1
                    continue
                if ch == '\\':
                    j += 2
                    continue
                if ch == '"' or ch == '\n':
                    break
                j += 1
            out.append((start, j + 1, src[start:j + 1]))
            i = j + 1
            continue
        i += 1
    return out


def split_interpolated(lit):
    """$"текст {выражение[,выравнивание][:формат]}" → (формат {0}, [выражения]) или None."""
    m = re.match(r'(\$@|@\$|\$)"', lit)
    body = lit[len(m.group(0)):-1]
    verbatim = '@' in m.group(1)
    if verbatim:
        return None                       # редкость; пусть смотрит человек
    fmt, args = [], []
    i, n = 0, len(body)
    while i < n:
        ch = body[i]
        if body.startswith('{{', i) or body.startswith('}}', i):
            fmt.append(body[i:i + 2])
            i += 2
            continue
        if ch == '{':
            depth, j, quote = 1, i + 1, None
            paren = 0
            split_at = None
            while j < n:
                c = body[j]
                if quote:
                    if c == '\\':
                        j += 2
                        continue
                    if c == quote:
                        quote = None
                    j += 1
                    continue
                if c in '"\'':
                    quote = c
                elif c in '([':
                    paren += 1
                elif c in ')]':
                    paren -= 1
                elif c == '{':
                    depth += 1
                elif c == '}':
                    depth -= 1
                    if depth == 0:
                        break
                elif c in ',:' and paren == 0 and depth == 1 and split_at is None:
                    # тернарник без скобок в интерполяции невозможен — «:» здесь формат
                    split_at = j
                j += 1
            inner = body[i + 1:j]
            if split_at is not None:
                expr = body[i + 1:split_at].strip()
                tail = body[split_at:j]
            else:
                expr, tail = inner.strip(), ''
            fmt.append('{' + str(len(args)) + tail + '}')
            args.append(expr)
            i = j + 1
            continue
        fmt.append(ch)
        i += 1
    return '"' + ''.join(fmt) + '"', args


def skip_reason(src, start):
    line_start = src.rfind('\n', 0, start) + 1
    before = src[line_start:start]
    # многострочный вызов: взять и предыдущую строку
    prev_start = src.rfind('\n', 0, max(0, line_start - 1)) + 1
    wide = src[prev_start:start]
    for r in SKIP_RE:
        if r.search(before) or (r.pattern.startswith('Debug') and r.search(wide)):
            return r.pattern
    return None


_blank_cache = {}


def blank_comments(src):
    """Тот же текст, где комментарии и строки заменены пробелами: позиции целы."""
    key = id(src), len(src)
    if key in _blank_cache:
        return _blank_cache[key]
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        if src.startswith('//', i):
            j = src.find('\n', i)
            j = n if j < 0 else j
            for k in range(i, j):
                out[k] = ' '
            i = j
            continue
        if src.startswith('/*', i):
            j = src.find('*/', i + 2)
            j = n if j < 0 else j + 2
            for k in range(i, j):
                if out[k] != '\n':
                    out[k] = ' '
            i = j
            continue
        i += 1
    for a, b, _ in literals(src):
        for k in range(a, b):
            if out[k] != '\n':
                out[k] = ' '
    res = ''.join(out)
    _blank_cache[key] = res
    return res


def in_static_table(src, start):
    """
    Литерал в инициализаторе статического поля (static readonly, const):
    такая таблица собирается один раз, и Loc.T в ней перевёл бы строку
    на язык запуска навсегда. Там — Loc.N, перевод при показе.
    """
    src = blank_comments(src)
    own = src[src.rfind('\n', 0, start) + 1:start]
    if re.search(r'\b(static|const)\b[^;(]*?=(?!>)', own):
        return True
    i = start
    depth = 0
    while i > 0:
        i -= 1
        c = src[i]
        if c in ')]}':
            depth += 1
        elif c in '([{':
            if depth == 0 and c == '{':
                # открывающая фигурная инициализатора — идём дальше, к объявлению
                pass
            else:
                depth = max(0, depth - 1)
        elif c == ';' and depth == 0:
            break
        elif c == '\n':
            line_start = src.rfind('\n', 0, i) + 1
            line = src[line_start:i].strip()
            if not line:
                continue
            # поле класса со значением: static (readonly или нет) и const —
            # но не свойство «=>», которое считается при каждом чтении
            if re.search(r'\b(static|const)\b[^;(]*?=(?!>)', line):
                return True
            # вышли к методу или к другому члену класса — не таблица
            if re.search(r'\)\s*$', line) and re.search(r'\b(void|string|bool|int|float|IEnumerable|IEnumerator)\b', line):
                return False
    return False


def wrap(src):
    changes, kept = [], []
    for start, end, lit in literals(src):
        text = lit
        if not CYR.search(text):
            continue
        why = skip_reason(src, start)
        if why:
            kept.append((start, lit, why))
            continue
        if in_static_table(src, start):
            if lit.startswith(('$', '@$')):
                kept.append((start, lit, 'интерполяция в статической таблице — руками'))
                continue
            changes.append((start, end, lit, f'Loc.N({lit})'))
            kept.append((start, lit, 'статическая таблица: Loc.N, перевести при показе — Loc.T(поле)'))
            continue
        if lit.startswith(('$', '@$')):
            parts = split_interpolated(lit)
            if parts is None:
                kept.append((start, lit, 'дословная интерполяция — руками'))
                continue
            fmt, args = parts
            new = f'Loc.F({fmt}' + ''.join(', ' + a for a in args) + ')' if args else f'Loc.T({fmt})'
        else:
            new = f'Loc.T({lit})'
        changes.append((start, end, lit, new))

    out = src
    for start, end, lit, new in sorted(changes, reverse=True):
        out = out[:start] + new + out[end:]
    return out, changes, kept


def ensure_using(src):
    if re.search(r'^\s*using\s+Sinbinder\.Core\s*;', src, re.M):
        return src
    if re.search(r'^\s*namespace\s+Sinbinder\.Core\b', src, re.M):
        return src
    m = list(re.finditer(r'^\s*using\s+[\w.]+\s*;\s*$', src, re.M))
    if m:
        pos = m[-1].end()
        return src[:pos] + '\nusing Sinbinder.Core;' + src[pos:]
    return 'using Sinbinder.Core;\n' + src


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    path = sys.argv[1]
    write = '--write' in sys.argv
    src = open(path, encoding='utf-8-sig').read()
    out, changes, kept = wrap(src)

    def line(pos):
        return src.count('\n', 0, pos) + 1

    for start, end, lit, new in changes:
        print(f'  {line(start):5}: {new}')
    if kept:
        print('\n  оставил (логика или служебное — смотреть глазами):')
        for start, lit, why in kept:
            print(f'  {line(start):5}: {lit}    [{why}]')
    print(f'\n  обёрнуто: {len(changes)}, оставлено: {len(kept)}')

    if write and changes:
        out = ensure_using(out)
        raw = open(path, 'rb').read()
        bom = raw.startswith(b'\xef\xbb\xbf')
        data = out.encode('utf-8')
        open(path, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + data)
        print('  записано')
    return 0


if __name__ == '__main__':
    sys.exit(main())
