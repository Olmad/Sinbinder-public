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

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\demo-walkthrough.ps1
#>

param(
    [string]$Project = "",
    [int]$TimeoutSeconds = 900
)

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

$unity = $null
foreach ($candidate in @(
    "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
    "$env:LOCALAPPDATA\Unity\Hub\Editor\$version\Editor\Unity.exe",
    "D:\Unity\Hub\Editor\$version\Editor\Unity.exe"
)) {
    if (Test-Path $candidate) { $unity = $candidate; break }
}

if (-not $unity) {
    Write-Error "Unity $version не найден. Поправь список путей в этом скрипте."
    exit 2
}

$log    = Join-Path $PSScriptRoot "demo-walkthrough.log"
$report = Join-Path $Project "Logs\demo-walkthrough.txt"
Remove-Item $log, $report -ErrorAction SilentlyContinue

# Графику не отключаем: прогон входит в Play, ведёт NavMeshAgent и жмёт
# кнопки совета. С -nographics провалы были бы не игры, а запуска.
Write-Host "Запускаю прогон. Срок — $TimeoutSeconds сек, дальше считаем, что он завис."
$proc = Start-Process -FilePath $unity -PassThru -ArgumentList @(
    "-batchmode",
    "-projectPath", $Project,
    "-logFile", $log,
    "-executeMethod", "Sinbinder.EditorTools.DemoWalkthrough.Run"
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
