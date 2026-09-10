#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Самопроверка check.py.

Зачем. У проверки четырнадцать правил, и до сегодня ни одно из них
не было доказано срабатывающим иначе как разовым зондом, который автор
каждый раз удалял. Правило, переставшее ловить после правки выражения,
не падает и не жалуется — оно молчит, а отчёт печатает «Чисто».
В этом проекте «Чисто» читают как «проверено», и это самое опасное
слово из всех.

Повод. За один день трижды подряд соврал не код, а прибор: заглушка
стенда округляла не как Юнити; заглушка предлагала конструктор,
которого в игре нет; а сама проверка не видела ни одного вызова
вида `new Пространство.Тип()`. Разбор — docs/13-DRIFT.md.

Как устроено. На каждое правило — крошечный проект во временной папке,
в котором ошибка сделана нарочно. Правило обязано её назвать. Отдельно
стоит заведомо чистый случай: правило, срабатывающее на всём подряд,
так же бесполезно, как молчащее.

Запуск:  python3 Tools/check_self.py
"""

import importlib.util
import io
import os
import sys
import tempfile


def load_check():
    here = os.path.dirname(os.path.abspath(__file__))
    spec = importlib.util.spec_from_file_location('check', os.path.join(here, 'check.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


CHECK = load_check()


def run(files):
    """Собрать проект во временной папке и вернуть тексты замечаний."""
    with tempfile.TemporaryDirectory() as root:
        for name, text in files.items():
            path = os.path.join(root, name)
            os.makedirs(os.path.dirname(path), exist_ok=True)
            io.open(path, 'w', encoding='utf-8').write(text)

        was = os.getcwd()
        os.chdir(root)
        try:
            found = CHECK.collect('.')
            problems = CHECK.Checker(found).run()
        finally:
            os.chdir(was)

    return [text for _, _, text in problems]


# ── ловушки ───────────────────────────────────────────────────────
# Ключ — правило, которое обязано сработать. Значение — файлы проекта
# и кусок сообщения, по которому видно, что сработало именно оно.

CASES = [
    ('determinism', {
        'AOS Engine/Modules/ProbeModule.cs': '''
namespace Sinbinder.AOS.Modules
{
    public class ProbeModule
    {
        public float Evaluate()
        {
            return UnityEngine.Random.Range(0f, 1f);
        }
    }
}
'''}, 'повторяемым'),

    ('duplicate_types', {
        'A.cs': '''
namespace N
{
    public class Twin { }
}
''',
        'B.cs': '''
namespace N
{
    public class Twin { }
}
''',
    }, 'CS0101'),

    ('braces', {
        'Broken.cs': 'namespace N { public class Broken { }',
    }, 'скобки не сходятся'),

    ('linq', {
        'Linqy.cs': '''
using System.Collections.Generic;
namespace N
{
    public class Linqy
    {
        void M(List<int> xs)
        {
            var y = xs.Where(x => x > 0);
        }
    }
}
'''}, 'System.Linq'),

    ('missing_usings', {
        'Far.cs': '''
namespace Other
{
    public class FarType { }
}
''',
        'Near.cs': '''
namespace Mine
{
    public class Near
    {
        void M()
        {
            FarType f = null;
        }
    }
}
'''}, 'без using'),

    ('string_for_enum', {
        'Teller.cs': '''
namespace N
{
    public enum Mood { Good, Bad }

    public class Teller
    {
        public void Say(Mood mood, int times) { }

        public void Go()
        {
            Say("Good", 1);
        }
    }
}
'''}, 'CS1503'),

    ('use_before_declaration', {
        'Early.cs': '''
using System.Collections.Generic;
namespace N
{
    public class Early
    {
        void M()
        {
            list.Add(1);
            var list = new List<int>();
        }
    }
}
'''}, 'CS0841'),

    ('file_names', {
        'Wrong.cs': '''
using UnityEngine;
namespace N
{
    public class Right : MonoBehaviour { }
}
'''}, 'имя файла не совпадает'),

    ('editor_guards', {
        'Tool.cs': '''
using UnityEditor;
namespace N
{
    public class Tool { }
}
'''}, 'UNITY_EDITOR'),

    ('singletons', {
        'Later.cs': '''
using UnityEngine;
namespace N
{
    public class Later : MonoBehaviour
    {
        public static Later Instance;

        void Start()
        {
            Instance = this;
        }
    }
}
'''}, 'а не в Awake'),

    ('deferred/заголовок', {
        'Old.cs.later': '// не тот заголовок\nnamespace N { public class Old { } }',
    }, 'без заголовка'),

    ('deferred/живой зовёт отложенный', {
        'Gone.cs.later': CHECK.LATER_HEADER + '''
namespace N
{
    public class Gone { }
}
''',
        'Alive.cs': '''
namespace N
{
    public class Alive
    {
        void M()
        {
            Gone g = null;
        }
    }
}
'''}, 'отложен'),

    ('unknown_new', {
        'Maker.cs': '''
namespace N
{
    public class Maker
    {
        void M()
        {
            var thing = new NoSuchThing();
        }
    }
}
'''}, 'CS0246: тип NoSuchThing'),

    ('unknown_new/через точку', {
        'Maker.cs': '''
namespace N
{
    public class Maker
    {
        void M()
        {
            var thing = new Far.NoSuchThing();
        }
    }
}
'''}, 'CS0246: тип NoSuchThing'),

    ('constructor_arity', {
        'Needy.cs': '''
namespace N
{
    public class Needy
    {
        public Needy(int what) { }
    }

    public class User
    {
        void M()
        {
            var n = new Needy();
        }
    }
}
'''}, 'CS1729'),

    ('constructor_arity/через точку', {
        'Needy.cs': '''
namespace N
{
    public class Needy
    {
        public Needy(int what) { }
    }

    public class User
    {
        void M()
        {
            var n = new N.Needy();
        }
    }
}
'''}, 'CS1729'),

    ('input_handler', {
        'ProjectSettings/ProjectSettings.asset': '  activeInputHandler: 1\n',
        'Keys.cs': '''
using UnityEngine;
namespace N
{
    public class Keys
    {
        void M()
        {
            if (Input.GetKeyDown(KeyCode.F)) { }
        }
    }
}
'''}, 'Input System'),
]

# Заведомо чистый проект. Правило, которое сработает здесь, ловит шум,
# а не ошибки — а такое обесценивает весь отчёт разом.
CLEAN = {
    'Quiet.cs': '''
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinbinder.Probe
{
    public class Quiet : MonoBehaviour
    {
        public static Quiet Instance;

        private readonly List<int> _kept = new List<int>();

        void Awake()
        {
            Instance = this;
        }

        public int Best()
        {
            return _kept.Where(x => x > 0).Count();
        }
    }
}
''',
}


def main():
    print('Самопроверка check.py: ловит ли она то, ради чего заведена.\n')

    failed = []

    for name, files, expect in CASES:
        problems = run(files)
        caught = any(expect in text for text in problems)

        print(f'  {"+" if caught else "ПРОВАЛ"}  {name}')
        if not caught:
            failed.append(name)
            print(f'        ждали «{expect}», получили: '
                  + (('; '.join(problems)) if problems else 'ничего'))

    problems = run(CLEAN)
    quiet = not problems
    print(f'  {"+" if quiet else "ПРОВАЛ"}  чистый проект (молчит)')
    if not quiet:
        failed.append('чистый проект')
        for text in problems:
            print(f'        лишнее: {text}')

    total = len(CASES) + 1
    print()

    if failed:
        print(f'Правил проверено: {total}. Не сработало: {len(failed)} — '
              + ', '.join(failed) + '.')
        print('Пока это так, «Чисто» от check.py ничего не значит.')
        return 1

    print(f'Правил проверено: {total}. Все сработали.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
