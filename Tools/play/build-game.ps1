# Tools/play/build-game.ps1 — собрать игру самому (автор, 29.10: «скрипт для сбора билда, чтобы я сам мог
# собирать… и смогу сам посмотреть почти в любой момент»). Запуск — двойным щелчком по «Собрать игру.cmd»
# в корне проекта.
#
# Что делает:
#   1. Проверяет, что Unity с этим проектом не открыт и не идёт прогон Claude (Unity держит проект один).
#   2. Собирает игру как есть сейчас на диске (GameBuild.BuildBatch) в Build\Sinbinder — это рабочая папка сборки.
#   3. Копирует готовую игру в D:\Claude\builds\<дата время> — играть оттуда. Так новая сборка никогда
#      не пишет поверх запущенной игры (29.10 игра упала ровно от этого).
#   4. Открывает папку с игрой.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)          # корень проекта
$drive = Split-Path -Qualifier $root                                   # буква флешки — на другом ПК бывает другой
$builds = Join-Path $drive 'Claude\builds'
$log = Join-Path $root 'Logs\build-author.log'
# The Unity launcher can retain its timestamp across builds; use a build marker.
$buildMarker = Join-Path $root 'Build\Sinbinder\.sinbinder-author-build.txt'
Remove-Item -LiteralPath $buildMarker -ErrorAction SilentlyContinue

function Fail($text) { Write-Host ''; Write-Host $text -ForegroundColor Red; Write-Host ''; Read-Host 'Нажми Enter, чтобы закрыть'; exit 1 }

# Unity: путь сессии, иначе — любой Unity нужной версии на этом же диске.
$version = (Get-Content (Join-Path $root 'ProjectSettings\ProjectVersion.txt') | Select-String 'm_EditorVersion:').ToString().Split(':')[1].Trim()
$unity = $env:SINBINDER_UNITY
if (-not $unity -or -not (Test-Path $unity)) {
    $unity = Get-ChildItem -Path "$drive\" -Directory -Filter 'Unity*' -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "$version\Editor\Unity.exe" } | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $unity) { $unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path $unity)) { Fail "Не нашёл Unity $version. Ожидал на $drive\Unity…\$version\Editor\Unity.exe." }

# Проект занят — открыт редактор или идёт прогон Claude.
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine -like "*$root*" }
if ($busy) { Fail "Проект сейчас занят: открыт Unity или идёт прогон Claude. Подожди, пока он закончит, и запусти ещё раз." }

# Игра, запущенная из рабочей папки сборки, — её не перезаписать.
$work = Join-Path $root 'Build\Sinbinder'
$running = Get-CimInstance Win32_Process -Filter "Name='Sinbinder.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath -like "$work*" }
if ($running) { Fail "Игра запущена прямо из $work — закрой её (играть лучше из папки в $builds)." }

Write-Host ''
Write-Host "Собираю Sinbinder (Unity $version). Обычно это 5–15 минут." -ForegroundColor Yellow
Write-Host 'Окно можно не трогать: ход сборки — ниже.'
Write-Host ''
Remove-Item $log -ErrorAction SilentlyContinue
$start = Get-Date
$p = Start-Process -FilePath $unity -PassThru -WindowStyle Hidden -ArgumentList @(
    '-batchmode', '-quit', '-projectPath', "`"$root`"",
    '-executeMethod', 'Sinbinder.Utilets.GameBuild.BuildBatch', '-logFile', "`"$log`"")
$null = $p.Handle
$shown = 0
while (-not $p.HasExited) {
    Start-Sleep -Seconds 5
    $min = [int]((Get-Date) - $start).TotalMinutes
    if (Test-Path $log) {
        $lines = @(Get-Content $log -Encoding UTF8 -ErrorAction SilentlyContinue | Select-String '\[СБОРКА\]|error CS|Compiling|Building scene|Packaging assets' | ForEach-Object { $_.Line })
        for ($i = $shown; $i -lt $lines.Count; $i++) { Write-Host ("  [{0} мин] {1}" -f $min, $lines[$i].Trim()) }
        $shown = $lines.Count
    }
    if (((Get-Date) - $start).TotalMinutes -gt 45) { $p.Kill(); Fail ('Сборка шла дольше 45 минут — остановил. Лог: ' + $log) }
}

$text = if (Test-Path $log) { Get-Content $log -Encoding UTF8 -Raw } else { '' }
if ($text -match 'error CS') {
    Write-Host ''
    ($text -split "`n" | Select-String 'error CS' | Select-Object -First 5) | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    Fail "Не собралось: ошибки в коде (выше). Скажи Claude — лог: $log"
}
if ($text -notmatch '\[СБОРКА\] Готово') { Fail "Не собралось. Лог: $log" }
if ($p.ExitCode -ne 0) { Fail "Unity завершилась с кодом $($p.ExitCode). Лог: $log" }

# Даже при строке «Готово» итоговый Player обязан существовать на диске.
$sourceExe = Join-Path $work 'Sinbinder.exe'
$sourceData = Join-Path $work 'Sinbinder_Data'
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    Fail "Unity сообщила об успехе, но Sinbinder.exe отсутствует: $sourceExe"
}
if (-not (Test-Path -LiteralPath $sourceData -PathType Container)) {
    Fail "Unity сообщила об успехе, но папка Sinbinder_Data отсутствует: $sourceData"
}

# Копия — в свою папку с датой; играть оттуда.
# Use a unique destination for every build. Reusing a minute-level directory with
# robocopy /E retains stale files from a previous Player build.
$stamp = Get-Date -Format 'yyyy-MM-dd HH-mm-ss'
$dest = Join-Path $builds ("{0}-{1}" -f $stamp, [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dest -ErrorAction Stop | Out-Null
robocopy $work $dest /E /NFL /NDL /NJH /NJS /NP | Out-Null
$copyCode = $LASTEXITCODE
# robocopy: коды 0..7 = успех/предупреждение, от 8 = ошибка копирования.
if ($copyCode -ge 8) { Fail "Ошибка копирования сборки (robocopy код $copyCode). Папка: $dest" }
$destExe = Join-Path $dest 'Sinbinder.exe'
$destData = Join-Path $dest 'Sinbinder_Data'
if (-not (Test-Path -LiteralPath $destExe -PathType Leaf) -or
    -not (Test-Path -LiteralPath $destData -PathType Container)) {
    Fail "Копирование неполное: нет Sinbinder.exe или Sinbinder_Data. Папка: $dest"
}
if ((Get-Item -LiteralPath $destExe).Length -ne (Get-Item -LiteralPath $sourceExe).Length) {
    Fail "Sinbinder.exe скопирован не полностью: отличается размер. Папка: $dest"
}
# Only mark a build accepted after Unity, robocopy and output validation.
[System.IO.File]::WriteAllText($buildMarker, [DateTime]::UtcNow.ToString('o'))
Copy-Item -LiteralPath $buildMarker -Destination (Join-Path $dest '.sinbinder-author-build.txt') -Force
$size = [int]((Get-ChildItem $dest -Recurse | Measure-Object Length -Sum).Sum / 1MB)
$took = [math]::Round(((Get-Date) - $start).TotalMinutes, 1)

Write-Host ''
Write-Host "Готово за $took мин, $size МБ." -ForegroundColor Green
Write-Host "Игра: $dest\Sinbinder.exe"
Write-Host ''
Start-Process explorer.exe $dest
Read-Host 'Нажми Enter, чтобы закрыть'