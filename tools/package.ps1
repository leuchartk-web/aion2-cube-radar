param([string]$Out = "dist")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$cli = Join-Path $root "src/CubeRadar.Cli/CubeRadar.Cli.csproj"
$version = ([xml](Get-Content $cli)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dist = [IO.Path]::GetFullPath((Join-Path $root $Out))
if (-not $dist.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Output must be inside the project" }
# Unique staging directories avoid overwriting files or stopping an existing application.
$stage = Join-Path $dist ("stage-" + [guid]::NewGuid().ToString("N"))
$publish = Join-Path $stage "build"
$bundle = Join-Path $stage "Radar de cubes FR"
New-Item -ItemType Directory -Force $bundle | Out-Null
dotnet publish $cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:CubeRadarWindowsApp=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $publish --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Compilation failed" }
Copy-Item -LiteralPath (Join-Path $publish "cuberadar.exe") -Destination (Join-Path $bundle "Radar de cubes FR.exe")
Copy-Item -LiteralPath (Join-Path $root "LICENSE"),(Join-Path $root "docs/LIRE-MOI.txt"),(Join-Path $root "docs/GUIDE_FR.md") -Destination $bundle
git -C $root archive --format=zip -o (Join-Path $bundle "source.zip") HEAD
if ($LASTEXITCODE -ne 0) { throw "Source archive failed" }
$zip = Join-Path $dist "radar-cubes-fr-$version-win-x64.zip"
Compress-Archive -LiteralPath $bundle -DestinationPath $zip -Force
Copy-Item -LiteralPath (Join-Path $bundle "Radar de cubes FR.exe") -Destination (Join-Path $dist "Radar.de.cubes.FR.exe")
Get-FileHash -Algorithm SHA256 -LiteralPath $zip,(Join-Path $dist "Radar.de.cubes.FR.exe") | ForEach-Object { "$($_.Hash.ToLower())  $([IO.Path]::GetFileName($_.Path))" } | Set-Content (Join-Path $dist "SHA256SUMS.txt") -Encoding utf8
