param(
    [string]$Output = (Join-Path $PSScriptRoot '..\publish')
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = [IO.Path]::GetFullPath($Output)

# La détection de Visual Studio par le compilateur AOT échoue si cette variable est définie (voir les notes du Plan 1).
if (Test-Path Env:NoDefaultCurrentDirectoryInExePath) { Remove-Item Env:NoDefaultCurrentDirectoryInExePath }

if (Test-Path $out) { Remove-Item -Recurse -Force $out }

dotnet publish (Join-Path $root 'src\UsageNotch.App') -c Release -r win-x64 --self-contained false -o $out
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication de l'application" }

$hookOut = Join-Path $out 'hook-aot'
dotnet publish (Join-Path $root 'src\UsageNotch.Hook') -c Release -r win-x64 -o $hookOut
if ($LASTEXITCODE -ne 0) { throw 'Échec de la publication du hook' }

Get-ChildItem $out -Filter 'UsageNotch.Hook.*' -File | Remove-Item
Copy-Item (Join-Path $hookOut 'UsageNotch.Hook.exe') $out
Remove-Item -Recurse -Force $hookOut

Write-Host "UsageNotch publié dans $out"
