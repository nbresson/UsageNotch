param(
    [string]$Output = (Join-Path $PSScriptRoot '..\publish')
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = [IO.Path]::GetFullPath($Output)

# La détection de Visual Studio par le compilateur AOT échoue si cette variable est définie (voir les notes du Plan 1).
if (Test-Path Env:NoDefaultCurrentDirectoryInExePath) { Remove-Item Env:NoDefaultCurrentDirectoryInExePath }

if (Test-Path $out) {
    # Filet de sécurité : ne supprimer que ce qui ressemble à une publication précédente de UsageNotch, jamais
    # une racine de disque, le dépôt lui-même, un de ses ancêtres, ou un dossier au contenu inattendu.
    $outTrimmed = $out.TrimEnd('\')
    $rootTrimmed = $root.TrimEnd('\')
    $estRacineDisque = [IO.Path]::GetPathRoot($out).TrimEnd('\') -eq $outTrimmed
    $estDepotOuAncetre = ($outTrimmed -eq $rootTrimmed) -or $rootTrimmed.StartsWith("$outTrimmed\", [StringComparison]::OrdinalIgnoreCase)
    $ressemblePublicationPrecedente = ((Get-ChildItem -LiteralPath $out -Force | Measure-Object).Count -eq 0) -or
        (Test-Path (Join-Path $out 'UsageNotch.App.exe'))
    if ($estRacineDisque -or $estDepotOuAncetre -or -not $ressemblePublicationPrecedente) {
        throw "Suppression refusée : « $out » ne ressemble pas à une publication UsageNotch précédente (racine de disque, dépôt ou ancêtre du dépôt, ou contenu inattendu)."
    }
    Remove-Item -Recurse -Force $out
}

dotnet publish (Join-Path $root 'src\UsageNotch.App') -c Release -r win-x64 --self-contained false -o $out
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication de l'application" }

$hookOut = Join-Path $out 'hook-aot'
dotnet publish (Join-Path $root 'src\UsageNotch.Hook') -c Release -r win-x64 -o $hookOut
if ($LASTEXITCODE -ne 0) { throw 'Échec de la publication du hook' }

Get-ChildItem $out -Filter 'UsageNotch.Hook.*' -File | Remove-Item
Copy-Item (Join-Path $hookOut 'UsageNotch.Hook.exe') $out
Remove-Item -Recurse -Force $hookOut

Write-Host "UsageNotch publié dans $out"
