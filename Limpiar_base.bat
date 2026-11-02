@echo off
chcp 65001 >nul
echo =================================================================
echo   Homologador de Packings Externos (DDC) - Limpiar Base de Datos
echo   Servidor: .\SQLEXPRESS  ^|  Base: HomologatorAzure
echo =================================================================
echo.
echo Limpiando tablas RegistrosRecepcion, RegistrosCalidad y RegistrosProduccion...

sqlcmd -S ".\SQLEXPRESS" -d "HomologatorAzure" -E -C -I -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; TRUNCATE TABLE dbo.RegistrosRecepcion; TRUNCATE TABLE dbo.RegistrosCalidad; TRUNCATE TABLE dbo.RegistrosProduccion; SELECT (SELECT COUNT(*) FROM dbo.RegistrosRecepcion) AS Recepcion, (SELECT COUNT(*) FROM dbo.RegistrosCalidad) AS Calidad, (SELECT COUNT(*) FROM dbo.RegistrosProduccion) AS Produccion;"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [OK] Base de datos limpia y lista como primera carga (contadores en 0).
) else (
    echo.
    echo [ERROR] No se pudo limpiar la base de datos. Verifique que .\SQLEXPRESS este activo.
)
echo.
timeout /t 4
