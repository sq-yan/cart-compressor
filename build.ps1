[CmdletBinding()]
param([string]$GameDir = $env:REPO_GAME_DIR, [string]$RepolibPath = '')
$ErrorActionPreference = 'Stop'
if (!$GameDir) { throw 'Pass -GameDir or set REPO_GAME_DIR to your R.E.P.O. installation.' }
if (!$RepolibPath) { $RepolibPath = Join-Path $GameDir 'BepInEx/plugins/Zehs-REPOLib/REPOLib.dll' }
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number
& dotnet build (Join-Path $PSScriptRoot 'src/CartCompressor.csproj') -c Release "-p:GameDir=$GameDir" "-p:RepolibPath=$RepolibPath" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& dotnet test (Join-Path $PSScriptRoot 'tests/CartCompressor.Tests.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
$dll = Join-Path $PSScriptRoot 'src/bin/Release/CartCompressor.dll'
$stage = Join-Path $PSScriptRoot "dist/stage-$version"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item -LiteralPath $dll -Destination $stage -Force
foreach ($name in @('manifest.json', 'README.md', 'CHANGELOG.md', 'LICENSE', 'icon.png')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage -Force
}
$zip = Join-Path $PSScriptRoot "dist/CartCompressor-$version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
& dotnet run --project (Join-Path $PSScriptRoot 'tools/Verify.csproj') -c Release "-p:GameDir=$GameDir" -- $dll (Join-Path $GameDir 'REPO_Data/Managed/Assembly-CSharp.dll') $zip
if ($LASTEXITCODE -ne 0) { throw 'Verification failed' }
Write-Host "Created $zip"
