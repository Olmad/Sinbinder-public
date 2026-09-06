#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Что сейчас разошлось между репозиторием и рабочей копией.

Облачная и локальная сессии не переговариваются напрямую: из облака
локальный не виден, отправка сообщения отбивается. Канал ровно один —
ветка. Значит и настройка должна приходить оттуда, а не из головы:
этот скрипт смотрит на состояние проекта и говорит, что надо сделать
руками, прежде чем работать дальше.

Запускается сам при старте сессии Claude Code (см. .claude/settings.json,
хук SessionStart) и печатает JSON, который читает агент. Запуск руками:

    python3 Tools/handoff.py            человекочитаемо
    python3 Tools/handoff.py --hook     JSON для хука
"""

import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BRANCH = 'claude/3d-horror-survival-game-ckrslc'

# Что Unity считает ассетом и чему полагается .meta. Список намеренно
# не записан руками: захардкоженный перечень устаревает молча — ровно
# тем способом, который этот скрипт и создан ловить.
META_EXTENSIONS = ('.cs', '.md', '.py', '.sh', '.ps1', '.cs.later')

SKIP_DIRS = {'Library', 'Temp', 'obj', 'bin', 'Logs', '.git'}


def git(*args):
    try:
        out = subprocess.run(['git'] + list(args), cwd=ROOT,
                             capture_output=True, text=True, timeout=20)
        return out.stdout.strip() if out.returncode == 0 else ''
    except Exception:
        return ''


def input_handler():
    """Старый Input под настройкой «только новый Input System»."""
    p = os.path.join(ROOT, 'ProjectSettings', 'ProjectSettings.asset')
    if not os.path.exists(p):
        return None
    m = re.search(r'activeInputHandler:\s*(\d+)',
                  open(p, encoding='utf-8', errors='replace').read())
    return m.group(1) if m else None


def scenes_older_than_builder():
    """
    Сцены, собранные раньше, чем последний раз менялся сборщик.
    Сравниваем по коммитам, а не по времени файла: рабочая копия
    могла быть перекачана целиком, и все отметки времени совпадут.
    """
    builder = git('log', '-1', '--format=%ct', '--',
                  'Assets/Scripts/Editor/DemoSceneBuilder.cs')
    scenes = git('log', '-1', '--format=%ct', '--', 'Assets/Scenes')
    if not builder or not scenes:
        return None
    return int(scenes) < int(builder)


def missing_meta():
    """Ассеты без .meta. Создать их может только открытый редактор."""
    out = []
    base = os.path.join(ROOT, 'Assets')

    for root, dirs, files in os.walk(base):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]

        for f in files:
            if f.endswith('.meta'):
                continue
            if not f.endswith(META_EXTENSIONS):
                continue

            full = os.path.join(root, f)
            if os.path.exists(full + '.meta'):
                continue

            out.append(os.path.relpath(full, ROOT).replace(os.sep, '/'))

    return sorted(out)


def behind():
    """Сколько коммитов ветки ещё не забрано."""
    git('fetch', 'origin', BRANCH)
    n = git('rev-list', '--count', 'HEAD..origin/' + BRANCH)
    return int(n) if n.isdigit() else None


def report():
    lines, urgent = [], []

    n = behind()
    if n:
        urgent.append(f'Забрать {n} коммит(ов): git pull origin {BRANCH}')
    elif n is None:
        lines.append('Состояние ветки узнать не удалось — origin недоступен.')

    h = input_handler()
    if h is None:
        urgent.append('activeInputHandler в ProjectSettings не найден — '
                      'проверить ввод нечем.')
    elif h == '1':
        urgent.append('activeInputHandler: 1 — «только новый Input System», '
                      'а весь ввод написан на старом UnityEngine.Input. '
                      'Поставить 2 и перезапустить Unity: иначе не работают '
                      'камера, выделение, жатва душ и кнопки интерфейса.')
    else:
        lines.append(f'Ввод настроен верно (activeInputHandler: {h}). '
                     'Если Unity был открыт до правки — перезапустить его.')

    stale = scenes_older_than_builder()
    if stale:
        urgent.append('Сцены в Assets/Scenes старше сборщика: всё, что '
                      'добавлено в DemoSceneBuilder.cs, в сценах ещё нет. '
                      'Меню Sinbinder → Собрать сцены демо.')
    elif stale is None:
        lines.append('Свежесть сцен определить не удалось.')

    m = missing_meta()
    if m:
        shown = ', '.join(m[:6])
        if len(m) > 6:
            shown += f' и ещё {len(m) - 6}'
        urgent.append(f'Ждут .meta от редактора ({len(m)} шт.; создать, '
                      f'открыв проект, и закоммитить до пересборки сцен): {shown}')

    return urgent, lines


def main():
    urgent, lines = report()

    if '--hook' in sys.argv:
        if not urgent:
            print(json.dumps({'suppressOutput': True}, ensure_ascii=False))
            return 0

        text = ('Состояние проекта Sinbinder на старте сессии. '
                'Разошлось с репозиторием — сделать до остальной работы:\n'
                + '\n'.join(f'{i + 1}. {u}' for i, u in enumerate(urgent))
                + '\n\nПодробности и разделение работ: '
                  'Assets/Scripts/docs/14-HANDOFF.md')

        print(json.dumps({
            'systemMessage': f'Sinbinder: {len(urgent)} расхождени(е/я) '
                             f'с репозиторием — см. контекст.',
            'hookSpecificOutput': {
                'hookEventName': 'SessionStart',
                'additionalContext': text,
            },
        }, ensure_ascii=False))
        return 0

    if urgent:
        print('Сделать до остальной работы:')
        for i, u in enumerate(urgent, 1):
            print(f'  {i}. {u}')
        print()
    for l in lines:
        print(l)
    if not urgent:
        print('Расхождений с репозиторием нет.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
