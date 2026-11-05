<#
.SYNOPSIS
    Automated Velopack Release & GitHub Deployment Script for ASI ERP.
.PARAMETER Version
    The version tag for this release (e.g. 2.0.1).
.PARAMETER GithubToken
    Optional personal access token for uploading to GitHub Releases.
.PARAMETER RepoUrl
    GitHub repository URL (default: https://github.com/Qnext232/AsiErp).
#>
param(
    [string]$Version = "2.0.0",
    [string]$GithubToken = "",
    [string]$RepoUrl = "https://github.com/develop232-bot/asi-billing-software"
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$PublishDir = Join-Path $ProjectDir "publish\AsiErp_Standalone"
$ReleasesDir = Join-Path $ProjectDir "Releases"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " 🚀 ASI ERP - Velopack Release Builder (v$Version)" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Clean previous release artifacts for this version
if (Test-Path $ReleasesDir) {
    Write-Host "[1/4] Cleaning previous release folder..." -ForegroundColor Gray
}

# 2. Publish standalone binary
Write-Host "[2/4] Publishing standalone .NET binary..." -ForegroundColor Green
dotnet publish "$ProjectDir\AsiErp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$PublishDir"

# Copy keys and icon
Copy-Item "$ProjectDir\firebase-key.json" "$PublishDir\" -Force
Copy-Item "$ProjectDir\app.ico" "$PublishDir\" -Force

# 3. Package with Velopack
Write-Host "[3/4] Creating Velopack release packages..." -ForegroundColor Green
vpk pack --packId AsiErp `
         --packVersion $Version `
         --packDir "$PublishDir" `
         --mainExe AsiErp.exe `
         --icon "$ProjectDir\app.ico" `
         --packAuthors "Afrah Samiya Industries Pvt Ltd" `
         --packTitle "ASI ERP" `
         --outputDir "$ReleasesDir"

# 4. Upload to GitHub if token provided
if (-not [string]::IsNullOrWhiteSpace($GithubToken)) {
    Write-Host "[4/4] Uploading release v$Version to GitHub ($RepoUrl)..." -ForegroundColor Yellow
    vpk upload github --repoUrl "$RepoUrl" --token "$GithubToken" --outputDir "$ReleasesDir" --publish
    Write-Host "✅ Successfully published release v$Version to GitHub!" -ForegroundColor Green
} else {
    Write-Host "[4/4] Release generated locally in '$ReleasesDir'." -ForegroundColor Cyan
    Write-Host "To publish to GitHub, either run:" -ForegroundColor Gray
    Write-Host "  vpk upload github --repoUrl `"$RepoUrl`" --token `"<YOUR_GITHUB_TOKEN>`" --outputDir `"$ReleasesDir`" --publish" -ForegroundColor White
    Write-Host "Or drag and drop files from 'Releases\' into your GitHub Release page." -ForegroundColor Gray
}

Write-Host "`n🎉 Velopack Setup file: $ReleasesDir\AsiErp-win-Setup.exe" -ForegroundColor Green
