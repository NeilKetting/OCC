param (
    [string]$Version
)

$ErrorActionPreference = "Stop"

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$Project = Join-Path $PSScriptRoot "..\src\OCC.Mobile.Android\OCC.Mobile.Android.csproj"

# Auto-detect version from project file if not provided
if (-not $Version) {
    Write-Host "Auto-detecting version from project file..."
    $Version = ([xml](Get-Content $Project)).Project.PropertyGroup.Version
    if ($Version) { $Version = $Version.Trim() }
    if (-not $Version) {
        throw "Could not detect version in $Project. Please provide it manually."
    }
}

$OutputFolder = Join-Path $PSScriptRoot ".\releases_mobile"
if (-not (Test-Path $OutputFolder)) { New-Item -ItemType Directory -Path $OutputFolder }

Write-Host "========================================================"
Write-Host "[MOBILE] BUILDING AND PACKAGING APK (v$Version)"
Write-Host "========================================================"

# 1. Clean and Build APK
Write-Host "[BUILD] Compiling Android APK..."
# We use -r android-arm64 for modern tablets. You can add more RIDs if needed.
dotnet publish $Project -c Release -f net10.0-android -r android-arm64 --self-contained true -p:Version=$Version -p:ApplicationVersion=1 -o $OutputFolder

if ($LASTEXITCODE -ne 0) {
    Write-Error "[ERROR] Build failed."
    exit $LASTEXITCODE
}

# 2. Locate and rename APK for clarity
$ApkPath = Get-ChildItem -Path $OutputFolder -Filter "*.apk" -Recurse | Select-Object -First 1
if ($ApkPath) {
    $NewName = "OCC.Mobile-$Version.apk"
    Move-Item $ApkPath.FullName (Join-Path $OutputFolder $NewName) -Force
    Write-Host "[SUCCESS] APK created: $NewName"
} else {
    Write-Warning "[WARNING] Could not find the generated .apk file in $OutputFolder"
}

Write-Host "--------------------------------------------------------"
Write-Host "To enable updates:"
Write-Host "1. Go to https://github.com/NeilKetting/OCC.Mobile/releases"
Write-Host "2. Create a new release with tag 'v$Version'."
Write-Host "3. Upload the APK file from $OutputFolder to the release assets."
Write-Host "4. The app will automatically detect and prompt for the update."
Write-Host "--------------------------------------------------------"
