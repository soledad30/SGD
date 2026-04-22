param(
    [switch]$SkipTests,
    [switch]$SkipLint,
    [switch]$SkipStaticAnalysis,
    [switch]$SkipPublish,
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\publish"
)

$ErrorActionPreference = "Stop"

function Run-Step {
    param(
        [string]$Name,
        [string]$Command
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    Write-Host "    $Command"
    Invoke-Expression $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Step '$Name' failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Release preflight started..." -ForegroundColor Green

Run-Step -Name "Restore solution" -Command "dotnet restore `".\GestorDocumentoApp.sln`""

if (-not $SkipLint) {
    Run-Step -Name "Lint (dotnet format verify)" -Command "dotnet format `".\GestorDocumentoApp.sln`" --verify-no-changes --no-restore --severity warn"
}
else {
    Write-Host "==> Lint skipped by parameter." -ForegroundColor Yellow
}

if (-not $SkipStaticAnalysis) {
    Run-Step -Name "Build with static analysis" -Command "dotnet build `".\GestorDocumentoApp.sln`" -c $Configuration --no-restore /p:EnableNETAnalyzers=true /p:AnalysisLevel=latest /warnaserror"
}
else {
    Run-Step -Name "Build solution" -Command "dotnet build `".\GestorDocumentoApp.sln`" -c $Configuration --no-restore"
}

if (-not $SkipTests) {
    Run-Step -Name "Run tests" -Command "dotnet test `".\GestorDocumentoApp.sln`" -c $Configuration --no-build"
}
else {
    Write-Host "==> Tests skipped by parameter." -ForegroundColor Yellow
}

if (-not $SkipPublish) {
    Run-Step -Name "Publish web app" -Command "dotnet publish `".\GestorDocumentoApp\GestorDocumentoApp.csproj`" -c $Configuration -o `"$OutputDir`""
}
else {
    Write-Host "==> Publish skipped by parameter." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Preflight finished successfully." -ForegroundColor Green
Write-Host "Next steps:"
Write-Host "1) Apply migrations on target DB."
Write-Host "2) Deploy published artifacts."
Write-Host "3) Verify /health endpoint after startup."
