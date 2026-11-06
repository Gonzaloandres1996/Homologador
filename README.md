# Homologador de Cargas Masivas DDC (DataHomologator)

Sistema de extracción, transformación y homologación de datos operativos desde packings externos hacia formatos estándar de carga masiva para SAP (Recepción de Materia Prima, Calidad y Producción), integrado con los servicios corporativos de **GRM en Azure**.

---

## 1. Descripción y Alcance del Proyecto

El **Homologador DDC** resuelve la interoperabilidad entre los sistemas informáticos heterogéneos de los packings agrícolas externos (SQL Server en planta/DAIA) y el ERP corporativo SAP.

### Alcance Operativo
El sistema opera sobre **3 reportes oficiales certificados** para la operación de packings externos:
1. **Calidad Andinexia** (`ReportId = 1`, `TipoProceso = 2`, `Pipeline = 1`): Homologación de análisis de calidad, tolerancias, defectos y parámetros fitosanitarios.
2. **Recepciones 26-10** (`ReportId = 2`, `TipoProceso = 1`, `Pipeline = 3`): Homologación de recepción de fruta, productores, variedades, guías y pesajes hacia formato SAP.
3. **Pipeline SAP Producción** (`ReportId = 3`, `TipoProceso = 3`, `Pipeline = 2`): Homologación de procesos de embalaje, rendimientos, calibres y tarjas/pallets hacia formato SAP.

### Principios de Diseño
* **Gobierno centralizado en GRM (V1–V4)**: La autenticación, los permisos por área, las tablas de equivalencias, las reglas de mapeo, la configuración de consultas/orígenes de datos y el registro histórico de lotes generados residen de forma centralizada en las APIs corporativas de GRM en Azure (`GRM.Authentication.Api` y `GRM.Calidad.Api`), eliminando la necesidad de instalar o mantener bases SQL Server locales (`HomologatorConfig`) en las estaciones de trabajo.
* **Cero almacenamiento redundante de payloads**: No se almacenan copias temporales ni archivos JSON con datos crudos del packing en la nube. La fuente de verdad operativa es la base de datos de origen (DAIA) y el respaldo físico es el archivo Excel/CSV generado.
* **Desacople de red hacia DAIA**: Las APIs en Azure nunca abren conexión directa hacia las redes internas de los packings externos. La estación de trabajo del operador (con VPN/red corporativa activa) descifra en memoria las credenciales recibidas desde GRM V4 mediante AES-256 (`EncryptionService`) y ejecuta la consulta de solo lectura mediante ADO.NET directo.
* **Determinismo e idempotencia**: El sistema rastrea en GRM (`api/conversor/persistencias`) únicamente qué identificadores (`Lote` o `Lote|CodigoPallet`) ya fueron generados con estado `Cargado`, evitando duplicaciones en ejecuciones posteriores.
* **API local integrada**: Al iniciar, la aplicación de escritorio levanta en el mismo proceso una instancia local de `Homologador.AzureApi` (`http://localhost:5080`) mediante Kestrel si no detecta otra instancia activa.

---

## 2. Arquitectura de la Solución

La solución se organiza en cuatro proyectos dentro de `Homologador.sln` (.NET 10):

```mermaid
flowchart TD
    subgraph UI ["Homologador.WinForms (Presentación Desktop)"]
        direction TB
        MainForm["MainForm (Menú, Grilla y Ejecución)"]
        LoginForm["LoginForm (Autenticación Corporativa)"]
        TokenMgr["GrmTokenManager (Ciclo de vida JWT GRM)"]
        ApiClient["HomologadorApiClient (Consumo HTTP)"]
        DdcTheme["DdcTheme (Diseño corporativo DDC)"]
    end

    subgraph Core ["Homologador.Core (Dominio, Mappers GRM y Motores ETL)"]
        direction TB
        TransEngine["TransformationEngine (Mapeos en memoria)"]
        QueryExec["ExternalQueryExecutor (ADO.NET DAIA)"]
        FileGen["FileGeneratorService (ClosedXML / CSV)"]
        IdExtractor["RowIdentityExtractor (Identidad dinámica)"]
        Crypto["EncryptionService (AES-256)"]
        GrmMappers["GrmConfigurationMapper (V4)\nGrmRuleMapper (V3)\nGrmEquivalenceMapper (V2)\nReportProcessMap"]
    end

    subgraph API ["Homologador.AzureApi (Host Local Integrado :5080)"]
        direction TB
        Host["AzureApiHost (Kestrel Embedded)"]
        AuthHandler["GrmAuthenticationHandler + GrmProfileAccess"]
        RuleReader["GrmRuleReader (Cliente HTTP GRM V2/V3/V4)"]
        Controllers["Controllers (Auth, Reports, Records, Admin)"]
    end

    subgraph Externo ["Servicios Corporativos y Orígenes de Datos"]
        GrmAuth["GRM.Authentication.Api\n(/api/authentication/login, /api/user/profile)"]
        GrmCalidad["GRM.Calidad.Api\n(/persistencias, /equivalencias, /reglas, /configuracion)"]
        PackDB[("SQL Server DAIA / Packing (Read-Only vía VPN)")]
    end

    MainForm --> ApiClient
    ApiClient --> TokenMgr
    TokenMgr -->|"1. Login y Renovación JWT"| GrmAuth
    ApiClient -->|"2. Persistencias (V1) y Equivalencias (V2)"| GrmCalidad
    ApiClient -->|"3. Sesión (/api/auth/me) y Contexto (/api/reports/*)"| Controllers
    Controllers --> AuthHandler
    AuthHandler -->|"Valida perfil y roles"| GrmAuth
    Controllers --> RuleReader
    RuleReader -->|"Consulta Reglas (V3) y Configuración (V4)"| GrmCalidad
    RuleReader --> GrmMappers
    RuleReader --> Crypto
    MainForm --> TransEngine
    MainForm --> QueryExec
    MainForm --> FileGen
    MainForm --> IdExtractor
    QueryExec -->|"4. ADO.NET TCP 1433 (VPN)"| PackDB
```

### Proyectos

* **`Homologador.WinForms`**: Aplicación de escritorio en Windows Forms (.NET 10) con actualización automática vía Velopack. Gestiona el inicio de sesión corporativo con GRM, el renderizado de reportes autorizados según el perfil del usuario, la grilla de resultados (`Pendiente` / `Cargado`) y la exportación de archivos.
* **`Homologador.Core`**: Librería de dominio y motores ETL sin dependencias de UI ni Web. Contiene las entidades, contratos DTO, los mappers de integración GRM (`GrmConfigurationMapper`, `GrmRuleMapper`, `GrmEquivalenceMapper`, `ReportProcessMap`), el motor de transformación en memoria, el servicio de cifrado AES-256, la generación de archivos Excel/CSV y el extractor de identidad de filas.
* **`Homologador.AzureApi`**: Host ASP.NET Core embebido en el puerto local `5080`. Valida tokens y perfiles contra `GRM.Authentication.Api`, orquesta la lectura de configuración V4 y reglas V3 desde `GRM.Calidad.Api`, descifra credenciales de orígenes de datos con `EncryptionService` y expone los contratos REST consumidos por el cliente WinForms.
* **`Homologador.Security.Checks`**: Suite ejecutable de 90 pruebas automatizadas (`Program.cs` y `V4Checks.cs`) que verifica de extremo a extremo la seguridad de tokens, permisos por área, persistencias (V1), equivalencias (V2), reglas (V3) y configuración sin base SQL local (V4).

---

## 3. Integración con Servicios Corporativos GRM (Fases V1 a V4)

La arquitectura reemplaza las antiguas bases SQL Server locales (`HomologatorConfig` y el uso operativo de `HomologatorAzure`) por cuatro contratos sobre las APIs corporativas de GRM en Azure:

| Fase | Componente / Endpoint GRM | Responsabilidad Funcional | Clases Responsables |
| :--- | :--- | :--- | :--- |
| **V1 (Autenticación y Persistencias)** | `GRM.Authentication.Api` (`/api/authentication/login`, `/api/user/profile`) y `GRM.Calidad.Api` (`GET/POST /api/conversor/persistencias?tipoProceso=1\|2\|3`) | Inicio de sesión con credenciales corporativas, autorización por roles (`Conversor_Recepcion`, `Conversor_Calidad`, `Conversor_Produccion`) y lectura/registro de lotes generados por proceso. | `GrmTokenManager`, `GrmAuthenticationHandler`, `GrmProfileAccess`, `HomologadorApiClient` |
| **V2 (Equivalencias / Lookups)** | `GRM.Calidad.Api` (`GET /api/conversor/equivalencias`) | Obtención dinámica de las tablas de homologación (`idParametro`: `2=Especies`, `3=Esponjas`, `4=Polvo Tierra`, `5=Tratamiento`). | `GrmEquivalenceMapper`, `HomologadorApiClient`, `GrmRuleReader` |
| **V3 (Reglas de Transformación)** | `GRM.Calidad.Api` (`GET /api/conversor/reglas?tipoProceso=1\|2\|3`) | Obtención ordenada de las reglas de mapeo (`Valor1` a `Valor14`) para cada reporte y pipeline sin límites fijos de cantidad de reglas. | `GrmRuleMapper`, `ReportProcessMap`, `GrmRuleReader` |
| **V4 (Configuración y Orígenes DAIA)** | `GRM.Calidad.Api` (`GET /api/conversor/configuracion[?tipoProceso=1\|2\|3]`) | Reconstrucción en memoria de `Reports`, `QueryDefinitions`, `QueryParameters` y `DataSources` (`idAplicacion = 2`), validando integridad referencial y descifrando localmente las credenciales de conexión a DAIA con `EncryptionService`. | `GrmConfigurationContracts`, `GrmConfigurationMapper`, `GrmRuleReader`, `ReportsController` |

### Notas sobre Seguridad y Gobierno Administrativo
* **Descifrado local de credenciales DAIA**: GRM almacena y transmite las credenciales de `DataSources` cifradas (`CfgUsernameEncrypted`, `CfgPasswordEncrypted`). El descifrado ocurre exclusivamente en memoria dentro de la estación de trabajo mediante la clave AES configurada en `appsettings.json` (`Encryption:Key`), sin exponer textos cifrados ni claves en mensajes de error o diagnósticos.
* **Sin fallback silencioso a SQL local**: Ante cualquier fallo de red, timeout o incoherencia en los datos devueltos por GRM, la API local rechaza la operación con códigos HTTP explícitos (`401`, `403`, `502`, `503`) sin recurrir a bases de datos locales.
* **Rutas heredadas bloqueadas**: La política `HomologadorAdmin` se encuentra deshabilitada por diseño (`RequireAssertion(_ => false)`) y las rutas locales de persistencia en `RecordsController` responden `HTTP 410 Gone`, dado que toda la administración y persistencia se gobierna desde GRM.

---

## 4. Flujo del Sistema de Punta a Punta

```mermaid
sequenceDiagram
    autonumber
    actor Op as Operador (WinForms)
    participant UI as MainForm / HomologadorApiClient
    participant API as AzureApi Local (:5080)
    participant GRM_Auth as GRM.Authentication.Api
    participant GRM_Cal as GRM.Calidad.Api (V1-V4)
    participant Engine as TransformationEngine / Extractor
    participant DAIA as SQL Server DAIA (VPN)
    participant FS as Sistema de Archivos Local

    Op->>UI: Ingresa usuario y contraseña corporativa
    UI->>GRM_Auth: POST /api/authentication/login
    GRM_Auth-->>UI: Bearer JWT + ExpiresIn
    UI->>API: GET /api/auth/me (Bearer JWT)
    API->>GRM_Auth: GET /api/user/profile
    GRM_Auth-->>API: Datos de usuario y roles (Conversor_*)
    API-->>UI: UserSessionDto con áreas habilitadas

    UI->>API: GET /api/reports/my-reports
    API->>GRM_Cal: GET /api/conversor/configuracion (V4) + /reglas (V3)
    GRM_Cal-->>API: Reportes activos autorizados y conteo de reglas
    API-->>UI: Listado de tarjetas de reportes

    Op->>UI: Selecciona reporte (ej: Recepción / Calidad / Producción)
    UI->>API: GET /api/reports/{id}/execution-context
    API->>GRM_Cal: GET /api/conversor/configuracion?tipoProceso={p} (V4)
    API->>GRM_Cal: GET /api/conversor/reglas?tipoProceso={p} (V3)
    Note over API: Valida integridad V4 y descifra credenciales DAIA con AES-256
    API-->>UI: ExecutionContextDto (Reporte, DataSource, Query, Parámetros, Reglas V3)
    UI->>GRM_Cal: GET /api/conversor/equivalencias (V2)
    GRM_Cal-->>UI: Tablas de equivalencia anexadas al contexto
    UI->>GRM_Cal: GET /api/conversor/persistencias?tipoProceso={p} (V1)
    GRM_Cal-->>UI: Historial de registros ya generados ("Cargado")

    Op->>UI: Pulsa "Actualizar / Cargar"
    UI->>DAIA: ExternalQueryExecutor ejecuta SP / consulta de solo lectura (vía VPN)
    DAIA-->>UI: Filas crudas extraídas
    UI->>Engine: TransformationEngine.Transform(rules, lookups, rawRows)
    Engine-->>UI: Filas homologadas en memoria
    UI->>Engine: RowIdentityExtractor.ExtractRowMetadata()
    Engine-->>UI: Claves únicas por fila (Lote o Lote|CodigoPallet)
    Note over UI: Filtra claves ya presentes en GRM y marca las nuevas como "Pendiente"

    Op->>UI: Pulsa "Generar Archivo"
    UI->>Engine: FileGeneratorService.GenerateFile(columnas, filas, formato)
    Engine-->>UI: Archivo Excel (.xlsx) o CSV
    UI->>FS: Guarda archivo en carpeta de salida configurada
    UI->>GRM_Cal: POST /api/conversor/persistencias (tipoProceso, registros)
    GRM_Cal-->>UI: Confirmación de lotes registrados
    UI->>Op: Actualiza filas confirmadas de "Pendiente" a "Cargado"
```

---

## 5. Requisitos, Ejecución y Pruebas

### Prerrequisitos
* **Windows x64** compatible con **.NET 10** y **.NET 10 SDK**.
* **Conectividad HTTPS** hacia los servicios corporativos de GRM en Azure.
* **Conectividad de red / VPN** hacia el servidor SQL Server de origen (**DAIA**) con los mismos permisos de red del entorno operativo.
* **No se requiere SQL Server local**: No es necesario instalar SQL Server Express ni restaurar las bases `HomologatorConfig` o `HomologatorAzure` para operar la aplicación.
* **Preservación de `appsettings.json`**: No modificar ni eliminar `Encryption:Key` en `src/Homologador.AzureApi/appsettings.json`, ya que se utiliza para descifrar las credenciales de `DataSources` entregadas por GRM V4.

### Compilación y Ejecución
Abrir PowerShell en la raíz del repositorio y ejecutar:

```powershell
dotnet restore
dotnet build Homologador.sln -c Release
dotnet run --project src\Homologador.WinForms\Homologador.WinForms.csproj
```

> **Nota operativa:** El comando anterior inicia la interfaz WinForms y levanta automáticamente la API local integrada en `http://localhost:5080`. No iniciar `Homologador.AzureApi` por separado y verificar que no exista una instancia previa ocupando el puerto `5080`.

### Ejecución de la Suite de Pruebas Automatizadas (90 Checks)
Para validar la seguridad, autenticación, persistencias (V1), equivalencias (V2), reglas (V3) y configuración (V4) sin afectar datos de producción en GRM ni consultar DAIA:

```powershell
dotnet run --project tests\Homologador.Security.Checks\Homologador.Security.Checks.csproj -c Release
```

### Publicación de Nuevas Versiones (Velopack + GitHub Releases)
El sistema cuenta con actualización automática mediante **Velopack** apuntando a los *GitHub Releases* del repositorio oficial. Para generar y publicar una nueva versión hacia los operadores, actualizar `<Version>` en los `.csproj`, fusionar a `main` y ejecutar:

```powershell
.\publicar_release.ps1 -Version "1.2.0" -GitHubToken "<TU_TOKEN_GITHUB>"
```
