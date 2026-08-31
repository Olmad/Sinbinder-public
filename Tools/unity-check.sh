#!/usr/bin/env bash
# То же самое для macOS и Linux.
#
#   ./Tools/unity-check.sh [путь_к_проекту]
#
# Unity в пакетном режиме компилирует проект и пишет лог; отсюда
# ошибки компиляции и результат самопроверки можно получить без
# открытия редактора.
set -uo pipefail

project="${1:-}"
if [ -z "$project" ]; then
    # По двум папкам сразу: одной Assets мало — в репозитории скриптов
    # она тоже есть, а ProjectSettings нет.
    project="$PWD"
    while [ "$project" != "/" ] && { [ ! -d "$project/Assets" ] || [ ! -d "$project/ProjectSettings" ]; }; do
        project="$(dirname "$project")"
    done
fi

if [ ! -d "$project/Assets" ] || [ ! -d "$project/ProjectSettings" ]; then
    echo "ОШИБКА: не нашёл проект Unity (нужны папки Assets и ProjectSettings)." >&2
    echo "Укажи корень проекта первым аргументом." >&2
    exit 2
fi

version_file="$project/ProjectSettings/ProjectVersion.txt"
[ -f "$version_file" ] || { echo "ОШИБКА: нет $version_file" >&2; exit 2; }
version="$(sed -n 's/^m_EditorVersion: *//p' "$version_file" | tr -d '\r')"

echo "Проект: $project"
echo "Версия Unity: $version"

unity=""
for candidate in \
    "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity" \
    "$HOME/Unity/Hub/Editor/$version/Editor/Unity" \
    "/opt/unity/editors/$version/Editor/Unity"
do
    [ -x "$candidate" ] && { unity="$candidate"; break; }
done

[ -n "$unity" ] || { echo "ОШИБКА: Unity $version не найден" >&2; exit 2; }

log="$(dirname "$0")/unity-check.log"
rm -f "$log"

echo "Запускаю Unity в пакетном режиме. Первый раз это долго."
"$unity" -batchmode -nographics -quit \
    -projectPath "$project" -logFile "$log" \
    -executeMethod Sinbinder.Tests.SelfCheckMenu.RunBatch
code=$?

[ -f "$log" ] || { echo "ОШИБКА: Unity не создал лог" >&2; exit 2; }

echo
errors="$(grep -E '\.cs\([0-9]+,[0-9]+\): *error CS[0-9]+' "$log" | sort -u)"
if [ -n "$errors" ]; then
    echo "ОШИБКИ КОМПИЛЯЦИИ: $(echo "$errors" | wc -l)"
    echo "$errors" | sed 's/^/  /'
else
    echo "Ошибок компиляции нет."
fi

echo
grep -E 'SINBINDER SELFCHECK|ПРОВЕРКА ДВИЖКА|  ✗ ' "$log" | sed 's/^/  /'

echo
echo "Полный лог: $log"
echo "Код выхода Unity: $code"
exit $code
