<#
.SYNOPSIS
    Прогоняет демо целиком без человека и печатает отчёт.

.DESCRIPTION
    Брат unity-check.ps1. Тот компилирует и гоняет самопроверку движка;
    этот запускает DemoWalkthrough — он входит в Play и нажимает
    за игрока от палатки до склепа.

    ВАЖНО: без -quit. DemoWalkthrough сам заканчивает прогон и сам зовёт
    EditorApplication.Exit с кодом 0 или 1; -quit убил бы редактор
    до первого шага.

    ВАЖНО: файл обязан быть сохранён в UTF-8 С BOM — по той же причине,
    что и unity-check.ps1.

.PARAMETER Project
    Папка проекта Unity — та, внутри которой лежит Assets.

.PARAMETER TimeoutSeconds
    Сколько ждать. Прогон обязан заканчиваться сам; если не кончился,
    это провал, а не повод ждать дальше.

.PARAMETER All
    То же прохождение со всеми выключателями дня (голос, причина, удар,
    добыча, лагерь, склад). Отчёт — отдельным файлом, *-all.txt.

.PARAMETER UnityExe
    Путь к Unity.exe, если редактор стоит не там, куда его ставит Hub
    (флешка, своя папка). То же — переменная SINBINDER_UNITY. Как
    в unity-check.ps1.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\demo-walkthrough.ps1
    powershell -ExecutionPolicy Bypass -File Tools\demo-walkthrough.ps1 -All
#>

param(
    [string]$Project = "",
    [int]$TimeoutSeconds = 900,
    [switch]$All,
    [string]$UnityExe = ""
)

$suffix = if ($All) { "-all" } else { "" }
$method = if ($All) { "RunAll" } else { "Run" }

$ErrorActionPreference = "Stop"

if (-not $Project) {
    $dir = (Get-Location).Path
    while ($dir -and -not (Test-Path (Join-Path $dir "Assets"))) {
        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    $Project = $dir
}

if (-not (Test-Path (Join-Path $Project "Assets"))) {
    Write-Error "Не нашёл папку проекта Unity. Укажи её через -Project."
    exit 2
}

$versionFile = Join-Path $Project "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $versionFile)) {
    Write-Error "Нет $versionFile"
    exit 2
}
$version = (Select-String -Path $versionFile -Pattern '^m_EditorVersion: *(.+)$').Matches[0].Groups[1].Value.Trim()

Write-Host "Проект: $Project"
Write-Host "Версия Unity: $version"

# Unity ставят двумя способами, и путь у них разный: через Hub —
# <корень>\Hub\Editor\<версия>\Editor\Unity.exe; вручную — в произвольную
# папку, причём версия может оказаться во вложенной ("C:\Program Files\
# Unity 6000.3.2f1\6000.3.22f1\Editor" — ровно этот случай на машине
# автора). Поэтому не гадаем по пути, а спрашиваем у самого Unity.exe —
# та же находка, что уже работает в unity-check.ps1.
function Get-UnityVersion($exe) {
    try { return ((Get-Item $exe).VersionInfo.ProductVersion -split '_')[0] }
    catch { return "" }
}

# Указанный путь — первым: папку, выбранную руками, обходом не угадать.
$unity = ""
if (-not $UnityExe -and $env:SINBINDER_UNITY) { $UnityExe = $env:SINBINDER_UNITY }
if ($UnityExe) {
    if (-not (Test-Path $UnityExe -PathType Leaf)) {
        Write-Error "Нет файла $UnityExe (-UnityExe или SINBINDER_UNITY)."
        exit 2
    }
    $unity = (Resolve-Path $UnityExe).Path
    $given = Get-UnityVersion $unity
    if ($given -and $given -ne $version) {
        Write-Host "Указан Unity $given, проекту нужен $version — прогон может отличаться" -ForegroundColor Yellow
    }
}

$scanDirs = @()
# Диск проекта — тоже место поиска: 25 сентября Unity переехал вместе
# с проектом на флешку, а обход одного Program Files флешку не видит.
$projectDrive = [System.IO.Path]::GetPathRoot((Resolve-Path $Project).Path)

foreach ($root in @("$env:ProgramFiles\Unity\Hub\Editor",
                    "${env:ProgramFiles(x86)}\Unity\Hub\Editor",
                    "$env:LOCALAPPDATA\Unity\Hub\Editor",
                    (Join-Path $projectDrive "Unity\Hub\Editor"),
                    (Join-Path $projectDrive "Program Files\Unity\Hub\Editor"))) {
    if (Test-Path $root) { $scanDirs += Get-ChildItem $root -Directory -ErrorAction SilentlyContinue }
}
foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)},
                    $projectDrive, (Join-Path $projectDrive "Program Files"))) {
    if ($base -and (Test-Path $base)) {
        $scanDirs += Get-ChildItem $base -Directory -Filter "Unity*" -ErrorAction SilentlyContinue
    }
}

$editors = @()
$seen = @{}
foreach ($dir in $scanDirs) {
    Get-ChildItem $dir.FullName -Recurse -Depth 2 -Filter "Unity.exe" -File -ErrorAction SilentlyContinue |
        ForEach-Object {
            if ($seen.ContainsKey($_.FullName)) { return }
            $seen[$_.FullName] = $true
            $v = Get-UnityVersion $_.FullName
            if ($v) { $editors += [PSCustomObject]@{ Version = $v; Path = $_.FullName } }
        }
}

if (-not $unity) {
    $unity = ($editors | Where-Object { $_.Version -eq $version } | Select-Object -First 1).Path
}
if (-not $unity -and $editors.Count -gt 0) {
    $fallback = $editors | Sort-Object Version -Descending | Select-Object -First 1
    $unity = $fallback.Path
    Write-Host "Версия $version не установлена, беру $($fallback.Version) — прогон может отличаться" -ForegroundColor Yellow
}

if (-not $unity) {
    Write-Error "Unity $version не найден нигде из обычных мест. Укажи путь: -UnityExe ""X:\путь\Editor\Unity.exe"" (или переменная SINBINDER_UNITY)."
    exit 2
}
Write-Host "Редактор: $unity"

# Лог — в Logs\ проекта, а не рядом со скриптом. Скрипт лежит внутри
# Assets, и Unity импортирует туда попавшее как ассет: растущий во время
# прогона лог-файл гонит бесконечный цикл переимпорта (найдено этим же
# прогоном 18 сентября — та же ловушка, что уже решена в unity-check.ps1).
$logDir = Join-Path $Project "Logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$log    = Join-Path $logDir "demo-walkthrough$suffix.log"
$report = Join-Path $Project "Logs\demo-walkthrough$suffix.txt"
Remove-Item $log, $report -ErrorAction SilentlyContinue

# Графику не отключаем: прогон входит в Play, ведёт NavMeshAgent и жмёт
# кнопки совета. С -nographics провалы были бы не игры, а запуска.
Write-Host "Запускаю прогон. Срок — $TimeoutSeconds сек, дальше считаем, что он завис."
$proc = Start-Process -FilePath $unity -PassThru -ArgumentList @(
    "-batchmode",
    "-projectPath", $Project,
    "-logFile", $log,
    "-executeMethod", "Sinbinder.EditorTools.DemoWalkthrough.$method"
)

if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
    Write-Host ""
    Write-Host "ЗАВИС: Unity не вышел за $TimeoutSeconds сек. Убиваю."
    Write-Host "Это само по себе провал: прогон обязан заканчиваться сам."
    $proc.Kill()
    $code = 124
} else {
    $code = $proc.ExitCode
}

Write-Host ""
if (Test-Path $report) {
    Get-Content $report | ForEach-Object { Write-Host $_ }
} else {
    Write-Host "ОТЧЁТА НЕТ: $report не создан."
    Write-Host "Значит, прогон не дошёл даже до первого шага — смотри ошибки компиляции ниже."
}

Write-Host ""
if (Test-Path $log) {
    $errors = Select-String -Path $log -Pattern '\.cs\([0-9]+,[0-9]+\): *error CS[0-9]+' |
              ForEach-Object { $_.Line } | Sort-Object -Unique
    if ($errors) {
        Write-Host "ОШИБКИ КОМПИЛЯЦИИ: $($errors.Count)"
        $errors | ForEach-Object { Write-Host "  $_" }
    }
}

Write-Host ""
Write-Host "Отчёт: $report"
Write-Host "Полный лог Unity: $log"
Write-Host "Код выхода: $code   (0 — прошло, 1 — есть провалы, 124 — зависло)"
exit $code
