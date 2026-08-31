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
    # Корень проекта опознаётся по двум папкам сразу. Одной Assets мало:
    # в самом репозитории скриптов она тоже есть, а ProjectSettings — нет.
    $candidate = (Get-Location).Path
    while ($candidate -and -not (
        (Test-Path (Join-Path $candidate "Assets")) -and
        (Test-Path (Join-Path $candidate "ProjectSettings")))) {
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
if (-not (Test-Path (Join-Path $Project "ProjectSettings"))) {
    Fail "в $Project нет папки ProjectSettings. Похоже, это репозиторий скриптов, а не проект Unity. Укажи настоящий проект параметром -Project"
}

Write-Host "Проект: $Project"

# ---------- найти Unity нужной версии ----------

$versionFile = Join-Path $Project "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $versionFile)) { Fail "не найден $versionFile" }

$version = (Select-String -Path $versionFile -Pattern 'm_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value
Write-Host "Версия Unity: $version"

$roots = @(
    "$env:ProgramFiles\Unity\Hub\Editor",
    "${env:ProgramFiles(x86)}\Unity\Hub\Editor",
    "C:\Program Files\Unity\Hub\Editor",
    "$env:LOCALAPPDATA\Unity\Hub\Editor"
)

$unity = $null
foreach ($root in $roots) {
    $path = Join-Path $root "$version\Editor\Unity.exe"
    if (Test-Path $path) { $unity = $path; break }
}

if (-not $unity) {
    # Версия не совпала — берём любую установленную и честно предупреждаем.
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $any = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
               Sort-Object Name -Descending |
               ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" } |
               Where-Object { Test-Path $_ } |
               Select-Object -First 1
        if ($any) {
            $unity = $any
            Write-Host "Версия $version не установлена, беру $unity" -ForegroundColor Yellow
            break
        }
    }
}

if (-not $unity) { Fail "Unity не найден. Установи версию $version через Unity Hub" }

# ---------- запуск ----------

if (-not $Log) { $Log = Join-Path $PSScriptRoot "unity-check.log" }
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

# ---------- ошибки компиляции ----------

$compile = $lines | Select-String -Pattern '\.cs\(\d+,\d+\):\s*error\s+CS\d+' |
           ForEach-Object { $_.Line.Trim() } | Select-Object -Unique

Write-Host ""
if ($compile) {
    Write-Host "ОШИБКИ КОМПИЛЯЦИИ: $($compile.Count)" -ForegroundColor Red
    $compile | ForEach-Object { Write-Host "  $_" }
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
