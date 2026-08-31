<#
.SYNOPSIS
    Собирает проект Unity без открытия редактора и печатает ошибки.

.DESCRIPTION
    Unity умеет работать в пакетном режиме: компилирует проект, выполняет
    указанный метод и пишет всё в лог. Значит, ошибки компиляции можно
    получить одной командой — и показать их тому, у кого редактора нет.

    Скрипт делает три вещи:
      1) находит нужную версию Unity по ProjectSettings/ProjectVersion.txt;
      2) запускает её в пакетном режиме с самопроверкой движка;
      3) выдёргивает из лога ошибки компиляции и итог проверки.

    ВАЖНО: файл обязан быть сохранён в UTF-8 С BOM. Windows PowerShell 5.1
    читает .ps1 без BOM в системной ANSI-кодировке, и русские строки ниже
    ломают разбор ещё до первой выполненной команды — с сообщениями вида
    «Missing argument in parameter list», указывающими куда угодно, только
    не на настоящую причину.

.PARAMETER Project
    Папка проекта Unity — та, внутри которой лежит Assets.
    По умолчанию берётся текущая, если в ней есть Assets.

.PARAMETER Log
    Куда писать полный лог. По умолчанию unity-check.log рядом со скриптом.

.PARAMETER NoSelfCheck
    Только компиляция, без запуска самопроверки движка.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\unity-check.ps1
    powershell -ExecutionPolicy Bypass -File Tools\unity-check.ps1 -Project "C:\Unity\Sinbinder"
#>

param(
    [string]$Project = "",
    [string]$Log = "",
    [switch]$NoSelfCheck
)

$ErrorActionPreference = "Stop"

function Fail($message) {
    Write-Host "ОШИБКА: $message" -ForegroundColor Red
    exit 2
}

# ---------- найти проект ----------

if (-not $Project) {
    $candidate = (Get-Location).Path
    while ($candidate -and -not (Test-Path (Join-Path $candidate "Assets"))) {
        $parent = Split-Path $candidate -Parent
        if ($parent -eq $candidate) { $candidate = ""; break }
        $candidate = $parent
    }
    if (-not $candidate) {
        Fail "не нашёл папку проекта Unity. Укажи её: -Project ""C:\путь\к\проекту"""
    }
    $Project = $candidate
}

if (-not (Test-Path (Join-Path $Project "Assets"))) {
    Fail "в $Project нет папки Assets — это не проект Unity"
}

Write-Host "Проект: $Project"

# ---------- найти Unity нужной версии ----------

$versionFile = Join-Path $Project "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $versionFile)) { Fail "не найден $versionFile" }

$version = (Select-String -Path $versionFile -Pattern 'm_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value
Write-Host "Версия Unity: $version"

# Unity ставят двумя способами, и путь у них разный:
#   через Hub — <корень>\Hub\Editor\<версия>\Editor\Unity.exe;
#   вручную — в произвольную папку, причём версия может оказаться
#   во вложенной: "C:\Program Files\Unity 6000.3.2f1\6000.3.22f1\Editor".
# Поэтому версию не вычисляем из пути (там она врёт), а спрашиваем
# у самого Unity.exe: он сообщает её в ProductVersion как
# "6000.3.22f1_<ревизия>".
function Get-UnityVersion($exe) {
    try { return ((Get-Item $exe).VersionInfo.ProductVersion -split '_')[0] }
    catch { return "" }
}

$scanDirs = @()

foreach ($root in @("$env:ProgramFiles\Unity\Hub\Editor",
                    "${env:ProgramFiles(x86)}\Unity\Hub\Editor",
                    "$env:LOCALAPPDATA\Unity\Hub\Editor")) {
    if (Test-Path $root) {
        $scanDirs += Get-ChildItem $root -Directory -ErrorAction SilentlyContinue
    }
}

foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
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

$unity = ($editors | Where-Object { $_.Version -eq $version } | Select-Object -First 1).Path

if (-not $unity -and $editors.Count -gt 0) {
    # Нужной версии нет. Берём самую свежую и предупреждаем: чужая версия
    # выдаёт чужие ошибки, и принимать их за свои — хуже, чем не собрать.
    $fallback = $editors | Sort-Object Version -Descending | Select-Object -First 1
    $unity = $fallback.Path
    Write-Host "Версия $version не установлена, беру $($fallback.Version) — ошибки могут отличаться" -ForegroundColor Yellow
}

if (-not $unity) { Fail "Unity не найден. Установи версию $version через Unity Hub" }

Write-Host "Редактор: $unity"

# ---------- запуск ----------

# Лог кладём в Logs\ проекта, а не рядом со скриптом: скрипт лежит внутри
# Assets, и всё, что туда попало, Unity импортирует как ассет и обвешивает
# .meta — семьдесят килобайт мусора в проекте после каждого прогона.
if (-not $Log) {
    $logDir = Join-Path $Project "Logs"
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
    $Log = Join-Path $logDir "unity-check.log"
}
if (Test-Path $Log) { Remove-Item $Log -Force }

$arguments = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", "`"$Project`"",
    "-logFile", "`"$Log`""
)
if (-not $NoSelfCheck) {
    $arguments += @("-executeMethod", "Sinbinder.Tests.SelfCheckMenu.RunBatch")
}

Write-Host "Запускаю Unity в пакетном режиме. Первый раз это долго — "  -NoNewline
Write-Host "он импортирует ассеты."
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
$code = $process.ExitCode

if (-not (Test-Path $Log)) { Fail "Unity не создал лог $Log" }
$lines = Get-Content $Log

# ---------- прерванный запуск ----------

# Unity мог упасть, не дойдя до компиляции: чаще всего потому, что проект
# уже открыт в редакторе — два экземпляра одну папку не делят. Список
# ошибок тогда пуст, и бодрое «ошибок компиляции нет» читалось бы как
# успех, хотя не собиралось вообще ничего.
$abort = $lines | Select-String -Pattern 'Aborting batchmode' | Select-Object -First 1
$abortReason = @()
if ($abort) {
    $from = $abort.LineNumber - 1
    $to = [Math]::Min($from + 4, $lines.Count - 1)
    $abortReason = $lines[$from..$to] | Where-Object { $_.Trim() -ne "" }
}

# ---------- ошибки компиляции ----------

$compile = $lines | Select-String -Pattern '\.cs\(\d+,\d+\):\s*error\s+CS\d+' |
           ForEach-Object { $_.Line.Trim() } | Select-Object -Unique

Write-Host ""
if ($compile) {
    Write-Host "ОШИБКИ КОМПИЛЯЦИИ: $($compile.Count)" -ForegroundColor Red
    $compile | ForEach-Object { Write-Host "  $_" }
} elseif ($abortReason) {
    Write-Host "UNITY ПРЕРВАЛ РАБОТУ, ДО КОМПИЛЯЦИИ НЕ ДОШЛО:" -ForegroundColor Red
    $abortReason | ForEach-Object { Write-Host "  $($_.Trim())" }
} else {
    Write-Host "Ошибок компиляции нет." -ForegroundColor Green
}

# ---------- итог самопроверки ----------

$selfCheck = $lines | Select-String -Pattern 'SINBINDER SELFCHECK|ПРОВЕРКА ДВИЖКА|  ✗ ' |
             ForEach-Object { $_.Line.Trim() }

if ($selfCheck) {
    Write-Host ""
    Write-Host "САМОПРОВЕРКА ДВИЖКА:"
    $selfCheck | ForEach-Object { Write-Host "  $_" }
}

# ---------- прочие исключения ----------

$exceptions = $lines | Select-String -Pattern '^\w*Exception:' |
              ForEach-Object { $_.Line.Trim() } | Select-Object -Unique -First 10

if ($exceptions) {
    Write-Host ""
    Write-Host "ИСКЛЮЧЕНИЯ:" -ForegroundColor Yellow
    $exceptions | ForEach-Object { Write-Host "  $_" }
}

Write-Host ""
Write-Host "Полный лог: $Log"
Write-Host "Код выхода Unity: $code"

exit $code
