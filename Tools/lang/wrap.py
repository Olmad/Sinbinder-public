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


IDENT_FIELD = re.compile(r'\b(const|readonly)\s+string\s+\w*(Name|Key|Id|Tag|Path)\s*=\s*$')


def skip_reason(src, start):
    line_start0 = src.rfind('\n', 0, start) + 1
    if IDENT_FIELD.search(src[line_start0:start]):
        return 'поле-идентификатор (…Name, …Key): имя объекта, а не текст'
    clean = blank_comments(src)
    why = call_skip(clean, start, declared_params(clean))
    if why:
        return why
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


def chains(src, lits):
    """Литералы, склеенные «+» (строка, разбитая по длине): одна фраза — один ключ."""
    out, cur = [], []
    for lit in lits:
        if cur and re.fullmatch(r'\s*\+\s*', src[cur[-1][1]:lit[0]]):
            cur.append(lit)
        else:
            if cur:
                out.append(cur)
            cur = [lit]
    if cur:
        out.append(cur)
    return out


def chain_call(src, chain):
    """Цепочка → Loc.T(…) или Loc.F(…, места); None — руками."""
    pieces, args, interp = [], [], False
    for start, end, lit in chain:
        if lit.startswith(('$', '@$')):
            parts = split_interpolated(lit)
            if parts is None:
                return None
            fmt, a = parts
            # номера мест продолжаются через куски
            shift = len(args)
            fmt = re.sub(r'\{(\d+)([^}]*)\}', lambda m: '{' + str(int(m.group(1)) + shift) + m.group(2) + '}', fmt)
            pieces.append(fmt)
            args.extend(a)
            interp = True
        else:
            pieces.append(lit)
    if interp:
        # в простых кусках фигурные скобки — буквальные: удвоить для string.Format
        fixed = []
        for (start, end, lit), piece in zip(chain, pieces):
            if lit.startswith(('$', '@$')):
                fixed.append(piece)
            else:
                fixed.append(piece.replace('{', '{{').replace('}', '}}'))
        pieces = fixed
    glue = [src[chain[k][1]:chain[k + 1][0]] for k in range(len(chain) - 1)]
    body = pieces[0]
    for g, piece in zip(glue, pieces[1:]):
        body += g + piece
    # имя души при показе — через Loc.Name: в записи и в логике оно русское
    args = [f'Loc.Name({a})' if re.fullmatch(r'[\w.\[\]()?]+\.DisplayName', a) else a for a in args]
    if args:
        return f'Loc.F({body}' + ''.join(', ' + a for a in args) + ')'
    return f'Loc.T({body})'


def wrap(src):
    changes, kept = [], []
    for chain in chains(src, literals(src)):
        if not any(CYR.search(lit) for _, _, lit in chain):
            continue
        start, end = chain[0][0], chain[-1][1]
        first = chain[0][2]
        why = skip_reason(src, start)
        if why:
            kept.append((start, first, why))
            continue
        if in_static_table(src, start):
            if any(lit.startswith(('$', '@$')) for _, _, lit in chain):
                kept.append((start, first, 'интерполяция в статической таблице — руками'))
                continue
            body = src[start:end]
            changes.append((start, end, first, f'Loc.N({body})'))
            kept.append((start, first, 'статическая таблица: Loc.N, перевести при показе — Loc.T(поле)'))
            continue
        new = chain_call(src, chain)
        if new is None:
            kept.append((start, first, 'дословная интерполяция — руками'))
            continue
        changes.append((start, end, first, new))

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
