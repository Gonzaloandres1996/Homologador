# Script para armar paquete de entrega
$ErrorActionPreference = "Stop"
$migracionDir = $PSScriptRoot
$baseDir = Split-Path -Parent $migracionDir
$targetDir = Join-Path $baseDir "Entrega_Homologador_DDC"

Write-Host "Preparando directorio: $targetDir"
if (Test-Path $targetDir) {
    Remove-Item -Path $targetDir -Recurse -Force
}

$ejecutableDir = Join-Path $targetDir "1_Ejecutable"
$bdDir = Join-Path $targetDir "2_BaseDeDatos"
$fuentesDir = Join-Path $targetDir "3_CodigoFuente"

New-Item -ItemType Directory -Path $ejecutableDir -Force | Out-Null
New-Item -ItemType Directory -Path $bdDir -Force | Out-Null
New-Item -ItemType Directory -Path $fuentesDir -Force | Out-Null

# 1. Ejecutables
Write-Host "1. Copiando ejecutables publicados..."
$publishSource = Join-Path $migracionDir "publish\*"
Copy-Item -Path $publishSource -Destination $ejecutableDir -Recurse -Force

# 2. Base de Datos
Write-Host "2. Copiando script SQL oficial..."
$sqlSource = Join-Path $migracionDir "02_Crear_BD_Simplificada_SSMS.sql"
Copy-Item -Path $sqlSource -Destination $bdDir -Force
Write-Host "-> Script SQL copiado a 2_BaseDeDatos exitosamente."

# 3. Código Fuente (sin bin, obj ni .vs)
Write-Host "3. Copiando codigo fuente limpio..."
$slnSource = Join-Path $migracionDir "Homologador.sln"
Copy-Item -Path $slnSource -Destination $fuentesDir -Force
$srcSource = Join-Path $migracionDir "src"
$srcDest = Join-Path $fuentesDir "src"
& robocopy $srcSource $srcDest /E /XD bin obj .vs /NFL /NDL /NJH /NJS /nc /ns /np
if ($LASTEXITCODE -ge 8) {
    throw "Robocopy failed with code $LASTEXITCODE"
}

# 4. Crear archivos explicativos (LEEME)
$leemeGeneral = @"
========================================================================
PAQUETE DE ENTREGA: HOMOLOGADOR DE PACKINGS EXTERNOS (DDC)
Tecnologia: .NET 10 Windows Forms + SQL Server
========================================================================

CONTENIDO DEL PAQUETE:

1_Ejecutable/
   Aplicacion lista para usar (Homologador.WinForms.exe).
   Requisitos:
   - Windows 10 / 11
   - .NET 10 Desktop Runtime
   - Instancia de SQL Server accesible (por defecto .\SQLEXPRESS)
   * La conexion se configura en 'appsettings.json'.
   * Pantalla de login corporativa 'Homologador carga masiva DDC'. Ingrese con usuario o correo corporativo.

2_BaseDeDatos/
   Contiene el script oficial para levantar la base de datos 'DataHomologator_Migracion':
   - '02_Crear_BD_Simplificada_SSMS.sql'
     Ejecutar en SQL Server Management Studio (SSMS) con F5.
     Crea el esquema relacional completo (14 tablas) y precarga las 122 reglas oficiales validadas.

3_CodigoFuente/
   Solucion completa ('Homologador.sln') y proyectos C# en 'src/'.
   Limpio de archivos binarios temporales (bin/ y obj/).
   Abrir con Visual Studio 2026 / Visual Studio 2022 v17.12+ con soporte .NET 10.
"@
Set-Content -Path (Join-Path $targetDir "LEEME_General.txt") -Value $leemeGeneral -Encoding UTF8

$leemeBD = @"
========================================================================
INSTRUCCIONES DE BASE DE DATOS
========================================================================

Base de datos: DataHomologator_Migracion
Tablas: 14 tablas relacionales (Usuarios, Catalogos, Mapeos, Registros)
Datos iniciales: 122 reglas de homologacion DDC validadas y usuarios registrados del sistema.

INSTRUCCIONES DE INSTALACION:
1. Abrir Microsoft SQL Server Management Studio (SSMS).
2. Conectarse a su servidor (ej. .\SQLEXPRESS o localhost).
3. Abrir el archivo '02_Crear_BD_Simplificada_SSMS.sql'.
4. Presionar F5 o hacer clic en 'Execute'.
5. La base de datos y todos los datos oficiales quedaran listos de forma inmediata.

USUARIOS REGISTRADOS EN EL SISTEMA:
(Puede ingresar con el prefijo de usuario o el correo completo; contraseña predeterminada: admin123.)

1. gonzalo.aravena@ddc.cl
   - Rol: Administrador (acceso a todos los reportes y administracion)
   - Contrasena predeterminada en selector: admin123.

2. michelle.cornejo@ddc.cl
   - Rol: Operador (Perfil: Recepcion)
   - Contrasena predeterminada en selector: admin123.

3. marcela.acuna@ddc.cl
   - Rol: Operador (Perfil: Calidad)
   - Contrasena predeterminada en selector: admin123.

4. eliana.rivas@ddc.cl
   - Rol: Operador (Perfil: Produccion)
   - Contrasena predeterminada en selector: admin123.
"@
Set-Content -Path (Join-Path $bdDir "LEEME_BaseDeDatos.txt") -Value $leemeBD -Encoding UTF8

# 5. Generar archivo .ZIP comprimido
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zipPath = Join-Path $baseDir "Entrega_Homologador_DDC.zip"
Write-Host "5. Comprimiendo a $zipPath..."
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
$files = Get-ChildItem -Path $targetDir -Recurse -File
foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($targetDir.Length + 1)
    $entry = $zip.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
    $entryStream = $entry.Open()
    $fileStream = [System.IO.File]::Open($file.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    $fileStream.CopyTo($entryStream)
    $fileStream.Dispose()
    $entryStream.Dispose()
}
$zip.Dispose()

Write-Host "========================================================="
Write-Host "Paquete generado exitosamente:"
Write-Host "Carpeta: $targetDir"
Write-Host "ZIP:     $zipPath"
Write-Host "========================================================="
