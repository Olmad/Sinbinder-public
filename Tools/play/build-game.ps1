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

# Invalidate the previous build marker only after confirming that no active
# Unity session or running working-copy Player blocks a new build.
Remove-Item -LiteralPath $buildMarker -ErrorAction SilentlyContinue

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

# Reject a stale Player even when Unity exits 0 and prints a success marker.
# The working directory can contain output from an earlier build.
$buildStartedUtc = $start.ToUniversalTime()
$exeWrittenUtc = (Get-Item -LiteralPath $sourceExe).LastWriteTimeUtc
if ($exeWrittenUtc -lt $buildStartedUtc) {
    Fail "Unity сообщила об успехе, но Sinbinder.exe не обновлён в этой сборке: $sourceExe"
}
$dataFiles = @(Get-ChildItem -LiteralPath $sourceData -File -Recurse -ErrorAction Stop)
if ($dataFiles.Count -eq 0) {
    Fail "Unity сообщила об успехе, но Sinbinder_Data пуста: $sourceData"
}
if (-not @($dataFiles | Where-Object { $_.LastWriteTimeUtc -ge $buildStartedUtc }).Count) {
    Fail "Unity сообщила об успехе, но в Sinbinder_Data нет файлов этой сборки: $sourceData"
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
# robocopy may report a non-fatal code while leaving an incomplete Player data tree.
# Verify every source file, not just the launcher, before accepting the build.
$sourceFiles = @(Get-ChildItem -LiteralPath $work -File -Recurse -ErrorAction Stop)
foreach ($file in $sourceFiles) {
    $relative = $file.FullName.Substring($work.Length).TrimStart('\\', '/')
    $copied = Join-Path $dest $relative
    if (-not (Test-Path -LiteralPath $copied -PathType Leaf)) {
        Fail "Копирование неполное: отсутствует $relative. Папка: $dest"
    }
    if ((Get-Item -LiteralPath $copied).Length -ne $file.Length) {
        Fail "Копирование неполное: размер $relative отличается. Папка: $dest"
    }
}
# Only mark a build accepted after Unity, robocopy and output validation.
[System.IO.File]::WriteAllText($buildMarker, [DateTime]::UtcNow.ToString('o'))
Copy-Item -LiteralPath $buildMarker -Destination (Join-Path $dest '.sinbinder-author-build.txt') -Force
# «Играть.cmd» (автор, 26.11: «на рабочем ПК ничего не должно сохраняться»). Записи и настройки игра на рабочем
# ПК сама пишет на флешку (Core/DataFolder: рабочие ПК — в <диск>\Claude\рабочие-пк.txt). Но движок Unity пишет
# свой журнал (Player.log) в папку ПК и служебные ключи (размер окна и т. п.) в реестр. Этот запуск на рабочем
# ПК кладёт журнал на флешку, ждёт выхода из игры и стирает за движком реестр и папку. На других ПК — обычный
# запуск: там записи игры лежат как раз в этой папке и реестре.
$launcher = @'
@echo off
chcp 65001 >nul
set "LIST=%~d0\Claude\рабочие-пк.txt"
set "WORK="
if exist "%LIST%" findstr /i /b /c:"%COMPUTERNAME%" "%LIST%" >nul && set "WORK=1"
if not defined WORK (
  start "" "%~dp0Sinbinder.exe"
  exit /b
)
set "LOGDIR=%~d0\Claude\Сохранения Sinbinder\сборка"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
"%~dp0Sinbinder.exe" -logFile "%LOGDIR%\Player.log"
reg delete "HKCU\Software\DefaultCompany\Sinbinder" /f >nul 2>&1
rmdir /s /q "%USERPROFILE%\AppData\LocalLow\DefaultCompany\Sinbinder" >nul 2>&1
rmdir "%USERPROFILE%\AppData\LocalLow\DefaultCompany" >nul 2>&1
'@
[System.IO.File]::WriteAllText((Join-Path $dest 'Играть.cmd'), $launcher.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
$size = [int]((Get-ChildItem $dest -Recurse | Measure-Object Length -Sum).Sum / 1MB)
$took = [math]::Round(((Get-Date) - $start).TotalMinutes, 1)

Write-Host ''
Write-Host "Готово за $took мин, $size МБ." -ForegroundColor Green
Write-Host "Игра: $dest\Sinbinder.exe"
Write-Host "На рабочем ПК запускай «Играть.cmd» — тогда и журнал Unity пишется на флешку, а не на ПК."
Write-Host ''
Start-Process explorer.exe $dest
Read-Host 'Нажми Enter, чтобы закрыть'