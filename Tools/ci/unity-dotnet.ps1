# Dotnet-проверки ботов («dotnet run --project …csproj» в .github/workflows) без .NET SDK — компилятором Roslyn и
# рантаймом из папки Unity 6, как Tools/tutorial/run-unity-roslyn.ps1, но для любого проекта: исходники, Compile
# Remove, DefineConstants, ImplicitUsings, Nullable, Reference на Roslyn и ProjectReference — из самого .csproj,
# чтобы список не разошёлся с CI (24.11: на рабочем ПК SDK нет, а CI после пуша упал на двух таких проверках).
#
#   powershell -File Tools/ci/unity-dotnet.ps1 -Project Tools/expeditions/MapChecks.csproj [-WorkDir <папка>] [аргументы…]
#   powershell -File Tools/ci/unity-dotnet.ps1 -All      — все dotnet-шаги workflows (сервер — только сборка)
#
# Отличия от CI: рантайм Unity — .NET 6 (в CI — 8), предупреждения ошибками не считаются.
param(
    [string]$Project,
    [string]$WorkDir,
    [switch]$All,
    [string]$Unity = 'D:\Unity 6000.3.2f1\6000.3.22f1\Editor',
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$data = Join-Path $Unity 'Data'
$dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'
$roslyn = Join-Path $data 'DotNetSdkRoslyn'
$csc = Join-Path $roslyn 'csc.dll'
$fw = Get-ChildItem (Join-Path $data 'NetCoreRuntime\shared\Microsoft.NETCore.App') -Directory |
      Sort-Object { [version]$_.Name } | Select-Object -Last 1
foreach ($p in $dotnet, $csc, $fw.FullName) { if (-not (Test-Path $p)) { throw "Нет $p" } }
$framework = Get-ChildItem $fw.FullName -Filter *.dll |
             Where-Object { $_.Name -match '^(System|netstandard|mscorlib|Microsoft\.CSharp|Microsoft\.Win32)' -and $_.Name -notmatch 'Native' }
$workRoot = Join-Path $PSScriptRoot '.work'

function Get-Prop($xml, $name) {
    $v = $null
    foreach ($g in @($xml.Project.PropertyGroup)) { if ($g -and $g.$name) { $v = [string]$g.$name } }
    return $v
}

function Get-Items($xml, $kind) {
    $list = @()
    foreach ($g in @($xml.Project.ItemGroup)) { if ($g -and $g.$kind) { $list += @($g.$kind) } }
    return $list
}

function Expand-Paths($dir, $pattern) {
    if (-not $pattern) { return @() }
    $full = Join-Path $dir ($pattern -replace '/', '\')
    if ($full -match '\*\*') {
        $base = $full.Substring(0, $full.IndexOf('**')).TrimEnd('\')
        $leaf = Split-Path $full -Leaf
        if (-not (Test-Path $base)) { return @() }
        return @(Get-ChildItem $base -Recurse -File -Filter $leaf | ForEach-Object { $_.FullName })
    }
    return @(Resolve-Path $full -ErrorAction SilentlyContinue | ForEach-Object { $_.Path })
}

# Собрать проект; вернуть путь к .dll. Ссылки проектов — рекурсивно, их .dll — рядом.
function Build-Project([string]$csproj) {
    $csproj = (Resolve-Path $csproj).Path
    $dir = Split-Path $csproj
    $name = [IO.Path]::GetFileNameWithoutExtension($csproj)
    $xml = [xml](Get-Content $csproj -Raw -Encoding UTF8)
    $out = Join-Path $workRoot $name
    New-Item -ItemType Directory -Force $out | Out-Null

    $src = @()
    if ((Get-Prop $xml 'EnableDefaultCompileItems') -ne 'false') {
        $src += Get-ChildItem $dir -Recurse -File -Filter *.cs |
                Where-Object { $_.FullName -notmatch '\\(bin|obj|\.work)\\' } | ForEach-Object { $_.FullName }
    }
    foreach ($c in Get-Items $xml 'Compile') { if ($c.Include) { $src += Expand-Paths $dir $c.Include } }
    $remove = @()
    foreach ($c in Get-Items $xml 'Compile') { if ($c.Remove) { $remove += Expand-Paths $dir $c.Remove } }
    $src = @($src | Where-Object { $remove -notcontains $_ } | Select-Object -Unique)
    if ($src.Count -eq 0) { throw "${name}: нет исходников" }

    $refs = @($framework | ForEach-Object { $_.FullName })
    foreach ($r in Get-Items $xml 'Reference') {
        $file = Join-Path $roslyn ($r.Include + '.dll')
        if (-not (Test-Path $file)) { throw "${name}: ссылка $($r.Include) — нет в $roslyn" }
        $refs += $file
        Copy-Item $file $out -Force
    }
    foreach ($p in Get-Items $xml 'ProjectReference') {
        $dll = Build-Project (Join-Path $dir ($p.Include -replace '/', '\'))
        if (-not $dll) { return $null }
        $refs += $dll
        Copy-Item $dll $out -Force
    }

    if ((Get-Prop $xml 'ImplicitUsings') -eq 'enable') {
        $global = Join-Path $out 'ImplicitUsings.g.cs'
        [IO.File]::WriteAllText($global, (@('System', 'System.Collections.Generic', 'System.IO', 'System.Linq', 'System.Net.Http',
            'System.Threading', 'System.Threading.Tasks') | ForEach-Object { "global using global::$_;" }) -join "`n")
        $src += $global
    }

    $exe = (Get-Prop $xml 'OutputType') -match '^(Exe|WinExe)$'
    $dllPath = Join-Path $out "$name.dll"
    $opts = @('-nologo', $(if ($exe) { '-target:exe' } else { '-target:library' }), '-langversion:latest', '-nostdlib+',
              '-nowarn:CS1701,CS1702,CS8632', '-debug-', "-out:`"$dllPath`"")
    $defines = Get-Prop $xml 'DefineConstants'
    if ($defines) { $opts += '-define:' + (@(($defines -replace '\$\(DefineConstants\)', '') -split '[;,]' | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }) -join ';') }
    if ((Get-Prop $xml 'Nullable') -eq 'enable') { $opts += '-nullable:enable' }
    if ((Get-Prop $xml 'AllowUnsafeBlocks') -eq 'true') { $opts += '-unsafe+' }
    $opts += $refs | ForEach-Object { "-r:`"$_`"" }
    $opts += $src | ForEach-Object { "`"$_`"" }
    $rsp = Join-Path $out 'c.rsp'
    [IO.File]::WriteAllLines($rsp, $opts)
    $log = & $dotnet $csc "@$rsp" 2>&1
    if ($LASTEXITCODE -ne 0) {
        $log | Where-Object { "$_" -match 'error' } | Select-Object -First 12 | ForEach-Object { Write-Host "  $_" }
        return $null
    }
    if ($exe) {
        $cfg = '{"runtimeOptions":{"tfm":"net6.0","rollForward":"Major","framework":{"name":"Microsoft.NETCore.App","version":"' + $fw.Name + '"}}}'
        [IO.File]::WriteAllText((Join-Path $out "$name.runtimeconfig.json"), $cfg)
    }
    return $dllPath
}

function Invoke-Check([string]$csproj, [string]$cwd, [string[]]$arguments, [switch]$BuildOnly) {
    $dll = Build-Project (Join-Path $root $csproj)
    if (-not $dll) { return @{ Ok = $false; Note = 'не собралось' } }
    if ($BuildOnly) { return @{ Ok = $true; Note = 'собрано' } }
    Push-Location (Join-Path $root $cwd)
    try {
        $outLines = & $dotnet $dll @arguments 2>&1
        $code = $LASTEXITCODE
    } finally { Pop-Location }
    $pass = @($outLines | Where-Object { "$_" -match '^\s*(PASS|OK|ок)\b' }).Count
    $tail = @($outLines | ForEach-Object { "$_" } | Where-Object { $_ -match 'FAIL|Exception|ПРОВАЛ|error' } | Select-Object -First 4)
    return @{ Ok = ($code -eq 0); Note = $(if ($code -eq 0) { "PASS $pass" } else { "код $code; " + ($tail -join ' | ') }) }
}

if ($All) {
    # Порядок и рабочие папки — как в .github/workflows (24.11).
    $jobs = @(
        @('Tools/logic/battle-narrator/BattleNarratorChecks.csproj', '.', @()),
        @('Tools/cards/CardChecks.csproj', '.', @()),
        @('Tools/dialogue/DialoguesChecks.csproj', '.', @()),
        @('Tools/loot/LootChecks.csproj', '.', @()),
        @('Tools/loot/BagTimeChecks.csproj', '.', @()),
        # Сервер и LAN-обвязка зовут API .NET 8 (JsonSerializerOptions.UnmappedMemberHandling,
        # NetworkStream.WriteAsync(память) без токена) — на рантайме Unity (.NET 6) не собрать: только CI.
        @('Tools/server/Sinbinder.Server.csproj', '.', 'net8'),
        @('Tools/server/UnityLanHarness/UnityLanHarness.csproj', '.', 'net8'),
        @('Tools/map-editor/MapEditorChecks.csproj', '.', @()),
        @('Tools/aos/AOSIntegrityTests.csproj', '.', @('.')),
        @('Tools/expeditions/ExpeditionChecks.csproj', '.', @()),
        @('Tools/expeditions/MapChecks.csproj', '.', @()),
        @('Tools/expeditions/ReplayChecks.csproj', '.', @()),
        @('Tools/expeditions/AosSessionChecks.csproj', '.', @()),
        @('Tools/expeditions/BoardShimChecks.csproj', '.', @()),
        @('Tools/expeditions/RealAosShimChecks.csproj', '.', @()),
        @('Tools/expeditions/BoardRealAosChecks.csproj', '.', @()),
        @('Tools/expeditions/LootChecks.csproj', '.', @()),
        @('Tools/expeditions/MemoryChecks.csproj', '.', @()),
        @('Tools/tutorial/TutorialChecks.csproj', '.', @()),
        @('Tools/tutorial/SyntaxChecks.csproj', '.', @('Assets/Scripts'))
    )
    $bad = 0; $skipped = 0
    foreach ($j in $jobs) {
        if ($j[2] -is [string] -and $j[2] -eq 'net8') {
            $skipped++
            Write-Host ("{0} {1,-58} {2}" -f 'мимо  ', $j[0], 'нужен .NET 8 — проверяет CI')
            continue
        }
        $r = Invoke-Check $j[0] $j[1] $j[2]
        if (-not $r.Ok) { $bad++ }
        Write-Host ("{0} {1,-58} {2}" -f $(if ($r.Ok) { 'ок    ' } else { 'ПРОВАЛ' }), $j[0], $r.Note)
    }
    Write-Host "провалов: $bad из $($jobs.Count - $skipped) (мимо: $skipped)"
    exit $bad
}

if (-not $Project) { throw 'нужен -Project <csproj> или -All' }
$dll = Build-Project (Join-Path $root $Project)
if (-not $dll) { Write-Host 'Не собралось.'; exit 1 }
Push-Location $(if ($WorkDir) { Join-Path $root $WorkDir } else { $root })
try { & $dotnet $dll @Rest; $code = $LASTEXITCODE } finally { Pop-Location }
exit $code
