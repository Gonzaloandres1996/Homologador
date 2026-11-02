[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Versión semántica del release (ej: 1.0.0, 1.0.1)")]
    [string]$Version,

    [Parameter(Mandatory = $false, HelpMessage = "Token de GitHub si se desea publicar automáticamente")]
    [string]$GitHubToken
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$projectPath = Join-Path $root "src\Homologador.WinForms\Homologador.WinForms.csproj"
$publishDir = Join-Path $root "publish\win-x64"
$releaseDir = Join-Path $root "Release"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  EMPAQUETADOR DE RELEASE (Velopack + GitHub Releases)" -ForegroundColor Cyan
Write-Host "  Version objetivo: $Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verificar o instalar vpk CLI
if (-not (Get-Command "vpk" -ErrorAction SilentlyContinue)) {
    Write-Host "-> Instalando herramienta global 'vpk' de Velopack..." -ForegroundColor Yellow
    dotnet tool install -g vpk
}

# 2. Publicar binarios
Write-Host "`n1. Compilando y publicando aplicacion..." -ForegroundColor Green
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
dotnet publish $projectPath -c Release -r win-x64 -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Fallo la publicacion de dotnet publish"
}

# 3. Empaquetar con Velopack hacia \Release
Write-Host "`n2. Empaquetando version con Velopack hacia \Release..." -ForegroundColor Green
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

vpk pack --packId "Homologador" `
         --packVersion "$Version" `
         --packDir "$publishDir" `
         --mainExe "Homologador.WinForms.exe" `
         --icon "$root\src\Homologador.WinForms\Brand\ddc-isotipo.ico" `
         --runtime "win-x64" `
         -o "$releaseDir"

if ($LASTEXITCODE -ne 0) {
    throw "Fallo el empaquetado con vpk"
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "  PAQUETES LISTOS EN: $releaseDir" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Get-ChildItem -Path $releaseDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

# 4. Opcional: Subir directamente a GitHub si se proveyó el Token
if ($GitHubToken) {
    Write-Host "`nSubiendo release a GitHub..." -ForegroundColor Yellow
    vpk upload github --repoUrl "https://github.com/Gonzaloandres1996/Homologador" `
                      --publish `
                      --token "$GitHubToken"
    Write-Host "-> Release publicado exitosamente en GitHub!" -ForegroundColor Green
} else {
    Write-Host "`nPROXIMO PASO:" -ForegroundColor Yellow
    Write-Host "Sube los archivos generados en la carpeta '\Release' a tu nuevo Release en GitHub:"
    Write-Host "URL: https://github.com/Gonzaloandres1996/Homologador/releases/new" -ForegroundColor White
}
