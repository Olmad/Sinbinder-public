#!/usr/bin/env bash
# Прогон демо целиком, без человека. macOS и Linux.
#
#   ./Tools/demo-walkthrough.sh [путь_к_проекту] [секунд_на_всё] [all]
#
# Третий аргумент «all» — то же прохождение со всеми выключателями дня
# (голос, причина, удар, добыча, лагерь, склад); отчёт — отдельным файлом.
#
# Брат unity-check.sh. Тот компилирует и гоняет самопроверку движка;
# этот запускает демо и нажимает за игрока — от палатки до склепа.
#
# ВАЖНО: без -quit. DemoWalkthrough сам входит в Play, сам заканчивает
# и сам зовёт EditorApplication.Exit с кодом 0 или 1. Ключ -quit убил бы
# редактор до того, как прогон начался.
set -uo pipefail

project="${1:-}"
limit="${2:-900}"
mode="${3:-}"
method="Run"
suffix=""
if [ "$mode" = "all" ]; then method="RunAll"; suffix="-all"; fi

if [ -z "$project" ]; then
    project="$PWD"
    while [ "$project" != "/" ] && [ ! -d "$project/Assets" ]; do
        project="$(dirname "$project")"
    done
fi

if [ ! -d "$project/Assets" ]; then
    echo "ОШИБКА: не нашёл папку проекта Unity. Укажи её первым аргументом." >&2
    exit 2
fi

version_file="$project/ProjectSettings/ProjectVersion.txt"
[ -f "$version_file" ] || { echo "ОШИБКА: нет $version_file" >&2; exit 2; }
version="$(sed -n 's/^m_EditorVersion: *//p' "$version_file" | tr -d '\r')"

echo "Проект: $project"
echo "Версия Unity: $version"

# UNITY=... в окружении перебивает поиск: установка бывает какая
# угодно, и упереться в неё — потерять прогон, а не найти ошибку.
# Диск, на котором лежит сам проект. На другом ПК тот же носитель
# получит другую букву, и путь, прибитый к «D», перестанет
# работать молча. Ищем Unity рядом с проектом, а не по букве.
here="$(echo "$project" | cut -d/ -f1-2)"

unity=""
for candidate in \
    "${UNITY:-}" \
    "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity" \
    "$HOME/Unity/Hub/Editor/$version/Editor/Unity" \
    "/opt/unity/editors/$version/Editor/Unity" \
    "/c/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe" \
    "/c/Program Files/Unity $version/Editor/Unity.exe" \
    "/c/Program Files/Unity 6000.3.2f1/$version/Editor/Unity.exe" \
    "/d/Unity 6000.3.2f1/$version/Editor/Unity.exe" \
    "$here/Unity 6000.3.2f1/$version/Editor/Unity.exe" \
    "$here/Unity/$version/Editor/Unity.exe"
do
    [ -x "$candidate" ] && { unity="$candidate"; break; }
done

[ -n "$unity" ] || { echo "ОШИБКА: Unity $version не найден" >&2; exit 2; }

# Лог — в Logs/, а не рядом со скриптом: всё, что лежит в Assets,
# редактор пытается импортировать, и растущий лог он импортирует
# без конца — «infinite import loop» прямо посреди прогона.
log="$project/Logs/demo-walkthrough$suffix.log"
report="$project/Logs/demo-walkthrough$suffix.txt"
rm -f "$log" "$report"

# Снимки шагов пересобираются каждым прогоном. Старые не чистить
# нельзя: шаги перенумеровываются, и в папке оказываются кадры
# от двух разных прогонов вперемешку.
rm -rf "$project/Docs/Образцы/прохождение"

# Графику не отключаем. Прогон входит в Play, ведёт NavMeshAgent и жмёт
# кнопки совета; -nographics отняло бы у него половину того, что он
# проверяет, и провалы были бы не игры, а запуска.
echo "Запускаю прогон. Срок — $limit сек, дальше считаем, что он завис."
"$unity" -batchmode \
    -projectPath "$project" -logFile "$log" \
    -executeMethod "Sinbinder.EditorTools.DemoWalkthrough.$method" &
pid=$!

waited=0
while kill -0 "$pid" 2>/dev/null && [ "$waited" -lt "$limit" ]; do
    sleep 2
    waited=$((waited + 2))
done

if kill -0 "$pid" 2>/dev/null; then
    echo
    echo "ЗАВИС: Unity не вышел за $limit сек. Убиваю."
    echo "Это само по себе провал: прогон обязан заканчиваться сам."
    kill "$pid" 2>/dev/null
    sleep 2
    kill -9 "$pid" 2>/dev/null
    code=124
else
    wait "$pid"
    code=$?
fi

echo
if [ -f "$report" ]; then
    cat "$report"
else
    echo "ОТЧЁТА НЕТ: $report не создан."
    echo "Значит, прогон не дошёл даже до первого шага — смотри ошибки компиляции ниже."
fi

echo
errors="$(grep -E '\.cs\([0-9]+,[0-9]+\): *error CS[0-9]+' "$log" 2>/dev/null | sort -u)"
if [ -n "$errors" ]; then
    echo "ОШИБКИ КОМПИЛЯЦИИ: $(echo "$errors" | wc -l)"
    echo "$errors" | sed 's/^/  /'
fi

echo
echo "Отчёт: $report"
echo "Полный лог Unity: $log"
echo "Код выхода: $code   (0 — прошло, 1 — есть провалы, 124 — зависло)"
exit $code
