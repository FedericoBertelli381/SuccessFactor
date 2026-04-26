param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "artifacts\staging\SuccessFactor.Blazor"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectPath = Join-Path $repoRoot "src\SuccessFactor.Blazor\SuccessFactor.Blazor.csproj"
$publishPath = Join-Path $repoRoot $OutputPath

Write-Host "STEP 54 - Publish staging"
Write-Host "Project: $projectPath"
Write-Host "Output:  $publishPath"

dotnet publish $projectPath `
    --configuration $Configuration `
    --output $publishPath `
    --no-restore `
    -p:EnvironmentName=Staging

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish exited with code $LASTEXITCODE"
}

Write-Host "Publish staging completato."
Write-Host "Ricordarsi di copiare appsettings.Staging.json reale sull'ambiente target; non usare il file .example.json in produzione."
