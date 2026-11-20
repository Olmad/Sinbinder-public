# Проверки сна (TutorialChecks) без dotnet SDK — компилятором и рантаймом из папки Unity 6,
# как Tools/compile/unity_csc.ps1. Те же исходники, что в TutorialChecks.csproj.
#
#   powershell -File Tools/tutorial/run-unity-roslyn.ps1 [-Unity "<папка Editor>"]
# Код выхода не 0 — проверка упала (имя упавшей — в выводе).
param(
    [string]$Unity = 'D:\Unity 6000.3.2f1\6000.3.22f1\Editor'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$data = Join-Path $Unity 'Data'
$dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
$fw = Get-ChildItem (Join-Path $data 'NetCoreRuntime\shared\Microsoft.NETCore.App') -Directory |
      Sort-Object { [version]$_.Name } | Select-Object -Last 1
foreach ($p in $dotnet, $csc, $fw.FullName) { if (-not (Test-Path $p)) { throw "Нет $p" } }
$work = Join-Path $PSScriptRoot '.work'
New-Item -ItemType Directory -Force $work | Out-Null

# Исходники — из TutorialChecks.csproj, чтобы список не разошёлся с CI.
$proj = [xml](Get-Content (Join-Path $PSScriptRoot 'TutorialChecks.csproj') -Raw -Encoding UTF8)
$src = $proj.Project.ItemGroup.Compile | ForEach-Object { (Resolve-Path (Join-Path $PSScriptRoot $_.Include)).Path }
$refs = Get-ChildItem $fw.FullName -Filter *.dll |
        Where-Object { $_.Name -match '^(System|netstandard|mscorlib|Microsoft\.CSharp)' -and $_.Name -notmatch 'Native' }
$out = Join-Path $work 'TutorialChecks.dll'
$rsp = @('-nologo', '-target:exe', '-langversion:latest', '-nostdlib+', '-nowarn:CS1701,CS1702,CS8632', "-out:`"$out`"") +
       ($refs | ForEach-Object { "-r:`"$($_.FullName)`"" }) + ($src | ForEach-Object { "`"$_`"" })
[IO.File]::WriteAllLines((Join-Path $work 'c.rsp'), $rsp)
& $dotnet $csc "@$(Join-Path $work 'c.rsp')"
if ($LASTEXITCODE -ne 0) { Write-Host 'Не собралось.'; exit 1 }
$cfg = '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"' + $fw.Name + '"}}}'
[IO.File]::WriteAllText((Join-Path $work 'TutorialChecks.runtimeconfig.json'), $cfg)

# Проверки читают исходники по путям от корня проекта.
Push-Location $root
try { & $dotnet $out; $code = $LASTEXITCODE } finally { Pop-Location }
exit $code
