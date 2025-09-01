param(
    [string]$OutputFile = "migrations.sql",
    [switch]$Idempotent,
    [string]$FromMigration = "",
    [string]$ToMigration = ""
)

$projectPath = ".\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj"
$startupPath = ".\HomeMonitoring.Web\HomeMonitoring.Web.csproj"

$command = "dotnet ef migrations script"

if ($FromMigration -ne "") {
    $command += " $FromMigration"
}

if ($ToMigration -ne "") {
    $command += " $ToMigration"
}

if ($Idempotent) {
    $command += " -i"
}

$command += " -p $projectPath -s $startupPath -o $OutputFile"

Write-Host "Generating migration script..." -ForegroundColor Green
Write-Host "Command: $command" -ForegroundColor Yellow

Invoke-Expression $command

if ($LASTEXITCODE -eq 0) {
    Write-Host "Migration script generated successfully: $OutputFile" -ForegroundColor Green
} else {
    Write-Host "Failed to generate migration script" -ForegroundColor Red
}
