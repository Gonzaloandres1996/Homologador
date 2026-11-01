$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  Homologador de Packings Externos (DDC) - Version Simplificada" -ForegroundColor Cyan
Write-Host "  Windows Forms (.NET 10) + API Azure SQL -> .\SQLEXPRESS" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

dotnet run --project "$scriptDir\src\Homologador.WinForms\Homologador.WinForms.csproj" --no-build
