# Компиляция скриптов игры компилятором самой Unity 6 — без лицензии и без запуска редактора.
#
# compile.py собирает на NuGet-сборках Unity 2021 и заглушках пакетов и требует dotnet SDK.
# Этот скрипт берёт то, что уже лежит на диске: dotnet и Roslyn из папки редактора,
# модули UnityEngine/UnityEditor той же версии и собранные пакеты из Library/ScriptAssemblies
# (URP, навигация, UI). Поэтому он ближе к настоящей сборке и работает на ПК без входа в Hub.
#
# Три прохода, как у Unity: игра в сборке игрока (без UNITY_EDITOR), игра в редакторе,
# редакторные скрипты поверх второй. asmdef в проекте нет — всё идёт в Assembly-CSharp(-Editor).
#
#   powershell -File Tools/compile/unity_csc.ps1 [-Unity "<папка Editor>"]
# Код выхода 1 — есть ошибки. Library нужна: без неё нет пакетов.
param(
    [string]$Unity = 'D:\Unity 6000.3.2f1\6000.3.22f1\Editor'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$data = Join-Path $Unity 'Data'
$dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
$work = Join-Path $PSScriptRoot '.work\csc'
$packs = Join-Path $root 'Library\ScriptAssemblies'
foreach ($p in $dotnet, $csc, $packs) { if (-not (Test-Path $p)) { throw "Нет $p" } }
New-Item -ItemType Directory -Force $work | Out-Null

$managed = Get-ChildItem (Join-Path $data 'Managed\UnityEngine') -Filter *.dll
$engine = $managed | Where-Object { $_.Name -notlike 'UnityEditor*' }
$editor = $managed | Where-Object { $_.Name -like 'UnityEditor*' }
$std = @(Join-Path $data 'NetStandard\ref\2.1.0\netstandard.dll')
# Своё старое Assembly-CSharp не подкладываем: проверяем нынешние исходники.
$pk = Get-ChildItem $packs -Filter *.dll | Where-Object { $_.Name -notlike 'Assembly-CSharp*' }
$pkRuntime = $pk | Where-Object { $_.Name -notmatch 'Editor|CodeGen|TestRunner|TestFramework|PerformanceTesting' }

$all = Get-ChildItem (Join-Path $root 'Assets') -Recurse -Filter *.cs -File
$game = $all | Where-Object { $_.FullName -notmatch '\\Editor\\' }
$edit = $all | Where-Object { $_.FullName -match '\\Editor\\' }
$nowarn = 'CS0414,CS0649,CS0169,CS0067,CS0618,CS0612,CS0168,CS0219,CS1998,CS0162,CS0108,CS0114,CS0105,CS0436,CS8632'

function Invoke-Pass([string]$name, $sources, $refs, [string]$defines) {
    $out = Join-Path $work "$name.dll"
    $rsp = Join-Path $work "$name.rsp"
    $lines = @('-nologo', '-noconfig', '-nostdlib+', '-target:library', '-langversion:9.0', '-deterministic',
               '-codepage:65001', "-nowarn:$nowarn", "-out:`"$out`"")
    if ($defines) { $lines += "-define:$defines" }
    $lines += $refs | ForEach-Object { "-r:`"$_`"" }
    $lines += $sources | ForEach-Object { "`"$($_.FullName)`"" }
    [IO.File]::WriteAllLines($rsp, $lines, (New-Object Text.UTF8Encoding($false)))
    $text = & $dotnet $csc "@$rsp" 2>&1 | Out-String
    $errors = $text -split "`r?`n" | Where-Object { $_ -match ': error ' } |
              ForEach-Object { $_.Replace($root + '\', '') } | Sort-Object -Unique
    [pscustomobject]@{ Name = $name; Errors = @($errors); Dll = $out }
}

$results = @()
$results += Invoke-Pass 'Player' $game (@($engine.FullName) + $std + @($pkRuntime.FullName)) ''
$inEditor = Invoke-Pass 'InEditor' $game (@($engine.FullName) + @($editor.FullName) + $std + @($pk.FullName)) 'UNITY_EDITOR'
$results += $inEditor
if ($inEditor.Errors.Count -eq 0) {
    $results += Invoke-Pass 'EditorScripts' $edit (@($engine.FullName) + @($editor.FullName) + $std + @($pk.FullName) + $inEditor.Dll) 'UNITY_EDITOR'
} else {
    $results += [pscustomobject]@{ Name = 'EditorScripts'; Errors = @('не собирались: игра в редакторе не собралась'); Dll = $null }
}

$total = 0
foreach ($r in $results) {
    "`n$($r.Name): " + $(if ($r.Errors.Count -eq 0) { 'чисто' } else { "ошибок $($r.Errors.Count)" })
    $r.Errors | ForEach-Object { '  ' + $_ }
    $total += $r.Errors.Count
}
if ($total) { "`nОшибок: $total."; exit 1 }
"`nЧисто: три прохода собрались компилятором Unity 6."
exit 0
