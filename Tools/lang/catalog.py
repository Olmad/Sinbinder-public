#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Таблицы перевода (docs/38-LANG.md): собрать строки из кода, показать
непереведённое, влить перевод.

    python3 Tools/lang/catalog.py                    обновить таблицы, напечатать покрытие
    python3 Tools/lang/catalog.py --todo [часть пути] [--lang en] [--limit N]
                                                     непереведённые строки — JSON {"русская": ""}
    python3 Tools/lang/catalog.py --apply перевод.json [--lang en]
                                                     влить перевод из JSON {"русская": "перевод"}

Строки берутся из вызовов Loc.T("…"), Loc.F("…", …) и Loc.N("…") во всех
скриптах игры (кроме Editor и Tests). Ключ — сама русская строка, как её
увидит игра (экранирование C# раскрыто).

Таблица — Assets/Resources/Lang/<язык>.txt, подмножество PO. Перевод,
которого больше нет в коде, не выбрасывается, а помечается «нет в коде»:
строку могли переписать, и старый перевод — подсказка для нового.
"""

import json
import os
import re
import signal
import sys

# вывод в head — не ошибка
if hasattr(signal, 'SIGPIPE'):
    signal.signal(signal.SIGPIPE, signal.SIG_DFL)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, 'Assets', 'Scripts')
LANG_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'Lang')
# Editor читается: сборщик сцен вписывает в сцену заставки и таблички
# (Loc.N), а показывают их компоненты игры через Loc.T. Прочее в Editor —
# меню и журнал разработчика — через Loc не идёт и в таблицу не попадёт.
SKIP_DIRS = ('Tests', 'Tools')

CALL = re.compile(r'\bLoc\.(T|F|N)\s*\(\s*((?:@?"(?:[^"\\\n]|\\.|"")*"\s*\+\s*)*@?"(?:[^"\\\n]|\\.|"")*")')
PIECE = re.compile(r'(@?)"((?:[^"\\\n]|\\.|"")*)"')


def unescape_cs(s, verbatim):
    if verbatim:
        return s.replace('""', '"')
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == '\\' and i + 1 < len(s):
            e = s[i + 1]
            if e == 'n':
                out.append('\n')
            elif e == 't':
                out.append('\t')
            elif e == 'u' and i + 5 < len(s):
                out.append(chr(int(s[i + 2:i + 6], 16)))
                i += 6
                continue
            else:
                out.append(e)
            i += 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def escape_po(s):
    return s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n').replace('\t', '\\t')


def unescape_po(s):
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == '\\' and i + 1 < len(s):
            e = s[i + 1]
            out.append('\n' if e == 'n' else '\t' if e == 't' else e)
            i += 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def scan():
    """{строка: [файлы]} в порядке первого появления."""
    found = {}
    for root, dirs, files in os.walk(SCRIPTS):
        dirs[:] = sorted(d for d in dirs if d not in SKIP_DIRS)
        for f in sorted(files):
            if not f.endswith('.cs'):
                continue
            path = os.path.join(root, f)
            rel = os.path.relpath(path, SCRIPTS).replace(os.sep, '/')
            src = open(path, encoding='utf-8-sig').read()
            # строки-комментарии (/// <c>Loc.T("Пауза")</c> в описании) — не текст игры
            src = '\n'.join('' if l.lstrip().startswith('//') else l for l in src.split('\n'))
            for m in CALL.finditer(src):
                key = ''.join(unescape_cs(p.group(2), p.group(1) == '@') for p in PIECE.finditer(m.group(2)))
                found.setdefault(key, [])
                if rel not in found[key]:
                    found[key].append(rel)
    return found


ASSET_KEYS = ('Text',)   # поля ассетов с текстом игрока (DialogueDatabase: Lines[].Text)


def scan_assets(found):
    """
    Текст игрока в ассетах Resources: реплики DialogueDatabase лежат
    в ассете, а не в коде. Читается YAML Unity (теги документов снимаются),
    берутся строковые поля из ASSET_KEYS с русскими буквами.
    """
    try:
        import yaml
    except ImportError:
        print('  (нет PyYAML — ассеты не прочитаны)')
        return
    res = os.path.join(ROOT, 'Assets', 'Resources')
    cyr = re.compile(r'[А-Яа-яЁё]')
    for f in sorted(os.listdir(res)):
        if not f.endswith('.asset'):
            continue
        text = open(os.path.join(res, f), encoding='utf-8').read()
        docs = re.split(r'^--- !u!\d+ &-?\d+.*$', text, flags=re.M)
        for doc in docs:
            doc = '\n'.join(l for l in doc.split('\n') if not l.startswith('%'))
            try:
                data = yaml.safe_load(doc)
            except yaml.YAMLError:
                continue

            def walk(x):
                if isinstance(x, dict):
                    for k, v in x.items():
                        if k in ASSET_KEYS and isinstance(v, str) and cyr.search(v):
                            found.setdefault(v, [])
                            ref = 'Resources/' + f
                            if ref not in found[v]:
                                found[v].append(ref)
                        else:
                            walk(v)
                elif isinstance(x, list):
                    for v in x:
                        walk(v)
            walk(data)


def read_table(path):
    """[(id, str)] в порядке файла."""
    if not os.path.isfile(path):
        return []
    rows, mid, mstr, cur = [], None, None, None
    for raw in open(path, encoding='utf-8'):
        line = raw.strip()
        if not line:
            if mid is not None and mstr is not None:
                rows.append((mid, mstr))
            mid = mstr = cur = None
            continue
        if line.startswith('#'):
            continue
        if line.startswith('msgid '):
            if mid is not None and mstr is not None:
                rows.append((mid, mstr))
            mid, mstr, cur = unescape_po(line[6:].strip()[1:-1]), None, 'id'
        elif line.startswith('msgstr '):
            mstr, cur = unescape_po(line[7:].strip()[1:-1]), 'str'
        elif line.startswith('"'):
            piece = unescape_po(line[1:-1])
            if cur == 'id':
                mid += piece
            elif cur == 'str':
                mstr += piece
    if mid is not None and mstr is not None:
        rows.append((mid, mstr))
    return rows


def write_table(path, lang, found, old):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    have = dict(old)
    lines = [
        f'# Sinbinder — перевод: {lang}',
        '# Ключ (msgid) — русская строка из кода, msgstr — перевод. Пустой msgstr — перевода нет,',
        '# игра покажет русскую. Собирает Tools/lang/catalog.py; правила — docs/38-LANG.md.',
        '# Места {0}, {1} — то, что игра подставит (имя, причина…): порядок можно менять, число — нет.',
        '',
    ]
    for key, files in found.items():
        lines.append('#: ' + ' '.join(files))
        lines.append(f'msgid "{escape_po(key)}"')
        lines.append(f'msgstr "{escape_po(have.get(key, ""))}"')
        lines.append('')
    gone = [(k, v) for k, v in old if k not in found and v]
    if gone:
        lines.append('# ───── нет в коде: строку переписали или убрали. Перевод — подсказка для новой ─────')
        lines.append('')
        for k, v in gone:
            lines.append('#. нет в коде')
            lines.append(f'msgid "{escape_po(k)}"')
            lines.append(f'msgstr "{escape_po(v)}"')
            lines.append('')
    open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines))
    meta = path + '.meta'
    if not os.path.isfile(meta):
        import uuid
        open(meta, 'w', encoding='utf-8').write(
            'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nTextScriptImporter:\n'
            '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def placeholders(s):
    return sorted(set(re.findall(r'\{(\d+)[^}]*\}', s)))


def main():
    args = sys.argv[1:]
    lang = 'en'
    if '--lang' in args:
        lang = args[args.index('--lang') + 1]
    path = os.path.join(LANG_DIR, lang + '.txt')

    found = scan()
    scan_assets(found)
    old = read_table(path)

    if '--apply' in args:
        src = args[args.index('--apply') + 1]
        new = json.load(open(src, encoding='utf-8'))
        table = dict(old)
        bad = 0
        for k, v in new.items():
            if k not in found:
                print(f'  нет в коде, пропускаю: {k[:70]}')
                continue
            if placeholders(k) != placeholders(v):
                print(f'  места не сходятся ({placeholders(k)} → {placeholders(v)}): {k[:60]} → {v[:60]}')
                bad += 1
                continue
            table[k] = v
        old = list(table.items())
        print(f'  влито: {len(new) - bad}, с ошибкой в местах: {bad}')

    write_table(path, lang, found, old)
    table = dict(old)

    if '--todo' in args:
        i = args.index('--todo')
        part = args[i + 1] if i + 1 < len(args) and not args[i + 1].startswith('--') else ''
        limit = int(args[args.index('--limit') + 1]) if '--limit' in args else 10 ** 9
        todo = {}
        for key, files in found.items():
            if table.get(key):
                continue
            if part and not any(part in f for f in files):
                continue
            todo[key] = ''
            if len(todo) >= limit:
                break
        print(json.dumps(todo, ensure_ascii=False, indent=1))
        return 0

    per = {}
    for key, files in found.items():
        for f in files:
            done, total = per.get(f, (0, 0))
            per[f] = (done + (1 if table.get(key) else 0), total + 1)
    total = len(found)
    done = sum(1 for k in found if table.get(k))
    print(f'  {lang}: переведено {done} из {total} строк в Loc')
    for f, (d, t) in sorted(per.items(), key=lambda x: (x[1][0] == x[1][1], x[0])):
        mark = 'готово' if d == t else f'{d}/{t}'
        print(f'    {mark:>9}  {f}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
