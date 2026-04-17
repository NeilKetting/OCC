param (
    [string]$Version
)

$ErrorActionPreference = "Stop"

$Project = "..\OCC.Client\OCC.Client.Desktop\OCC.Client.Desktop.csproj"

# Auto-detect version if not provided
if (-not $Version) {
    Write-Host "Auto-detecting version from project file..."
    $Version = ([xml](Get-Content $Project)).Project.PropertyGroup.Version
    if ($Version) { $Version = $Version.Trim() }
    if (-not $Version) {
        throw "Could not detect version in $Project. Please provide it manually."
    }
}

# Check for vpk
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "Velopack CLI (vpk) not found. Installing..."
    dotnet tool install -g vpk
}

$PublishDir = ".\publish"
$ReleaseDir = ".\Releases"

Write-Host "Building and Publishing version $Version (Self-Contained win-x64)..."

# Clean publish dir
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

# Dotnet publish
dotnet publish $Project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfContained=true -o $PublishDir /p:Version=$Version

Write-Host "Packing release..."

# Create Releases dir if not exists
if (-not (Test-Path $ReleaseDir)) { New-Item -ItemType Directory -Path $ReleaseDir }

# Run vpk
# Including icon and metadata to match the batch script
vpk pack -u "OCC.Beta" --packTitle "Orange Circle Construction" --packAuthors "Origize63" -v $Version -p $PublishDir -e "OCC.Client.Desktop.exe" -o $ReleaseDir --icon "..\OCC.Client\OCC.Client.Desktop\Assets\app.ico"

Write-Host "--------------------------------------------------------"
Write-Host "Release created in $ReleaseDir"
Write-Host "To enable updates:"
Write-Host "1. Create a public folder or GitHub Release."
Write-Host "2. Upload all files from $ReleaseDir (Setup.exe, RELEASES, .nupkg) to that location."
Write-Host "3. Ensure UpdateService.cs points to that URL."
Write-Host "--------------------------------------------------------"
