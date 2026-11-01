/* =========================================================================================
   PROYECTO: Homologador de Packings Externos (DDC) — Versión Simplificada (Auditoría DBA)
   MOTOR:    Microsoft SQL Server (Local .\SQLEXPRESS y destino Azure SQL Database)
   BASE:     DataHomologator_Migracion
   OBJETIVO: Esquema 100% relacional auditado y simplificado (13 tablas):
             1. Cero GUIDs (UNIQUEIDENTIFIER): Uso de INT IDENTITY(1,1) + Codigo ('0001', '0002', '0003').
             2. Seguridad simplificada: Users absorbe Roles, UserRoles y UserProfiles mediante CHECK constraints.
             3. Pipelines y Reglas de Mapeo COMPLETOS (migra los 9 MappingPipelines y las 316 MappingRules
                reales desde DataHomologator para que el Excel/CSV se siga generando exactamente igual).
             4. Persistencia simplificada en 3 tablas independientes (solo registra qué Lote se generó):
                - RegistrosRecepcion  (PK: Lote)
                - RegistrosCalidad    (PK: Lote — misma PK que Recepción, tabla independiente)
                - RegistrosProduccion (PK: Id INT IDENTITY + N filas por cada LoteProceso)
   ========================================================================================= */

USE [master];
GO

IF DB_ID(N'DataHomologator_Migracion') IS NOT NULL
BEGIN
    ALTER DATABASE [DataHomologator_Migracion] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [DataHomologator_Migracion];
END
GO

CREATE DATABASE [DataHomologator_Migracion];
GO

-- Asignar autorización a 'sa' para que "Database Diagrams" en SSMS funcione inmediatamente
ALTER AUTHORIZATION ON DATABASE::[DataHomologator_Migracion] TO [sa];
GO

USE [DataHomologator_Migracion];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================================================================================
   BLOQUE 1: SEGURIDAD Y ACCESO POR REPORTE (2 TABLAS EN LUGAR DE 5)
   ========================================================================================= */

-- 1. Usuarios (Absorbe Roles, UserRoles y UserProfiles sin perder ninguna validación)
CREATE TABLE dbo.Users (
    Id                 INT IDENTITY(1,1) NOT NULL,
    Codigo             AS RIGHT('0000' + CAST(Id AS VARCHAR(10)), 4) PERSISTED,
    Email              NVARCHAR(256)     NOT NULL,
    DisplayName        NVARCHAR(150)     NOT NULL,
    PasswordHash       NVARCHAR(255)     NOT NULL,
    Rol                VARCHAR(20)       NOT NULL, -- 'Admin' | 'Operator'
    Perfil             NVARCHAR(30)      NULL,     -- 'Recepción' | 'Calidad' | 'Producción' (NULL para Admin)
    IsActive           BIT               NOT NULL CONSTRAINT DF_Users_IsActive      DEFAULT (1),
    CreatedAt          DATETIME2(0)      NOT NULL CONSTRAINT DF_Users_CreatedAt     DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Users        PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Users_Email  UNIQUE NONCLUSTERED (Email),
    CONSTRAINT CK_Users_Rol    CHECK (Rol IN ('Admin', 'Operator')),
    CONSTRAINT CK_Users_Perfil CHECK (Perfil IS NULL OR Perfil IN (N'Recepci' + NCHAR(243) + N'n', N'Calidad', N'Producci' + NCHAR(243) + N'n'))
);

/* =========================================================================================
   BLOQUE 2: CONFIGURACIÓN DE ORÍGENES, CONSULTAS, LOOKUPS, PIPELINES Y REPORTES (8 TABLAS)
   ========================================================================================= */

-- 2. Orígenes de Datos (Conexiones SQL de Packings Externos consumidas vía VPN de usuario)
CREATE TABLE dbo.DataSources (
    Id                INT IDENTITY(1,1) NOT NULL,
    Codigo            AS RIGHT('0000' + CAST(Id AS VARCHAR(10)), 4) PERSISTED,
    Name              NVARCHAR(150)     NOT NULL,
    Server            NVARCHAR(250)     NOT NULL,
    [Database]        NVARCHAR(150)     NOT NULL,
    AuthType          INT               NOT NULL CONSTRAINT DF_DataSources_AuthType DEFAULT (0),
    UsernameEncrypted NVARCHAR(1000)    NULL,
    PasswordEncrypted NVARCHAR(1000)    NULL,
    ConnectionTimeout INT               NOT NULL CONSTRAINT DF_DataSources_Timeout  DEFAULT (30),
    IsActive          BIT               NOT NULL CONSTRAINT DF_DataSources_IsActive DEFAULT (1),
    CreatedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_DataSources_Created  DEFAULT (SYSUTCDATETIME()),
    UpdatedAt         DATETIME2(0)      NULL,
    CONSTRAINT PK_DataSources      PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_DataSources_Name UNIQUE NONCLUSTERED (Name)
);

-- 3. Definiciones de Consultas SQL de Extracción
CREATE TABLE dbo.QueryDefinitions (
    Id           INT IDENTITY(1,1) NOT NULL,
    Codigo       AS RIGHT('0000' + CAST(Id AS VARCHAR(10)), 4) PERSISTED,
    Name         NVARCHAR(150)     NOT NULL,
    DataSourceId INT               NOT NULL,
    CommandType  INT               NOT NULL CONSTRAINT DF_QueryDef_CmdType  DEFAULT (0),
    CommandText  NVARCHAR(MAX)     NOT NULL,
    Description  NVARCHAR(500)     NULL,
    IsActive     BIT               NOT NULL CONSTRAINT DF_QueryDef_IsActive DEFAULT (1),
    CreatedAt    DATETIME2(0)      NOT NULL CONSTRAINT DF_QueryDef_Created  DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    DATETIME2(0)      NULL,
    CONSTRAINT PK_QueryDefinitions            PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_QueryDefinitions_Name       UNIQUE NONCLUSTERED (Name),
    CONSTRAINT FK_QueryDefinitions_DataSource FOREIGN KEY (DataSourceId) REFERENCES dbo.DataSources(Id)
);

-- 4. Parámetros de Consultas SQL
CREATE TABLE dbo.QueryParameters (
    Id                INT IDENTITY(1,1) NOT NULL,
    QueryDefinitionId INT               NOT NULL,
    SortOrder         INT               NOT NULL CONSTRAINT DF_QueryParam_SortOrder DEFAULT (1),
    Name              NVARCHAR(100)     NOT NULL,
    SqlType           NVARCHAR(50)      NOT NULL CONSTRAINT DF_QueryParam_SqlType   DEFAULT ('nvarchar'),
    DefaultValue      NVARCHAR(500)     NULL,
    Description       NVARCHAR(250)     NULL,
    CONSTRAINT PK_QueryParameters          PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_QueryParameters_QueryDef FOREIGN KEY (QueryDefinitionId)
        REFERENCES dbo.QueryDefinitions(Id) ON DELETE CASCADE
);

-- 5. Catálogo de Tablas de Equivalencia (Lookups)
CREATE TABLE dbo.LookupTables (
    Id          INT IDENTITY(1,1) NOT NULL,
    Codigo      AS RIGHT('0000' + CAST(Id AS VARCHAR(10)), 4) PERSISTED,
    Name        NVARCHAR(150)     NOT NULL,
    Description NVARCHAR(500)     NULL,
    CreatedAt   DATETIME2(0)      NOT NULL CONSTRAINT DF_LookupTables_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2(0)      NULL,
    CONSTRAINT PK_LookupTables      PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_LookupTables_Name UNIQUE NONCLUSTERED (Name)
);

-- 6. Pares de Equivalencia (Código Packing -> Código SAP)
CREATE TABLE dbo.LookupEntries (
    Id            INT IDENTITY(1,1) NOT NULL,
    LookupTableId INT               NOT NULL,
    KeyValue      NVARCHAR(250)     NOT NULL,
    MappedValue   NVARCHAR(250)     NOT NULL,
    ExtraField1   NVARCHAR(250)     NULL,
    CONSTRAINT PK_LookupEntries           PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_LookupEntries_Table_Key UNIQUE NONCLUSTERED (LookupTableId, KeyValue),
    CONSTRAINT FK_LookupEntries_Table     FOREIGN KEY (LookupTableId)
        REFERENCES dbo.LookupTables(Id) ON DELETE CASCADE
);

-- 7. Pipelines de Mapeo hacia SAP (Se mantienen completos para generar el Excel/CSV de cada reporte)
CREATE TABLE dbo.MappingPipelines (
    Id          INT IDENTITY(1,1) NOT NULL,
    Codigo      AS RIGHT('0000' + CAST(Id AS VARCHAR(10)), 4) PERSISTED,
    Name        NVARCHAR(150)     NOT NULL,
    Description NVARCHAR(500)     NULL,
    IsActive    BIT               NOT NULL CONSTRAINT DF_MappingPipelines_IsActive DEFAULT (1),
    CreatedAt   DATETIME2(0)      NOT NULL CONSTRAINT DF_MappingPipelines_Created  DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2(0)      NULL,
    CONSTRAINT PK_MappingPipelines      PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_MappingPipelines_Name UNIQUE NONCLUSTERED (Name)
);

-- 8. Reglas de Transformación Completas del Pipeline (Todas las columnas que usa TransformationEngine)
CREATE TABLE dbo.MappingRules (
    Id                INT IDENTITY(1,1) NOT NULL,
    PipelineId        INT               NOT NULL,
    SortOrder         INT               NOT NULL,
    TargetColumn      NVARCHAR(150)     NOT NULL,
    RuleType          NVARCHAR(30)      NOT NULL,
    SourceColumn      NVARCHAR(1000)    NULL,
    FixedValue        NVARCHAR(500)     NULL,
    LookupTableId     INT               NULL,
    LookupKeyColumn   NVARCHAR(150)     NULL,
    LookupValueColumn NVARCHAR(150)     NULL,
    DateFormat        NVARCHAR(50)      NULL,
    Validation        NVARCHAR(250)     NULL,
    IsRequired        BIT               NOT NULL CONSTRAINT DF_MappingRules_Required DEFAULT (0),
    IsIdentity        BIT               NOT NULL CONSTRAINT DF_MappingRules_Identity DEFAULT (0),
    SubstringStart    INT               NULL,
    SubstringLength   INT               NULL,
    CONSTRAINT PK_MappingRules          PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_MappingRules_Pipeline FOREIGN KEY (PipelineId)    REFERENCES dbo.MappingPipelines(Id) ON DELETE CASCADE,
    CONSTRAINT FK_MappingRules_Lookup   FOREIGN KEY (LookupTableId) REFERENCES dbo.LookupTables(Id)
);

-- 9. Reportes Configurados (Une Query + MappingPipeline Completo + Formato Excel/CSV + Área de Persistencia)
CREATE TABLE dbo.Reports (
    Id                INT IDENTITY(1,1) NOT NULL,
    Codigo            AS RIGHT('0000' + CAST(Id AS VARCHAR(10)), 4) PERSISTED,
    Name              NVARCHAR(150)     NOT NULL,
    Description       NVARCHAR(500)     NULL,
    TargetArea        NVARCHAR(30)      NOT NULL, -- 'Recepción' | 'Calidad' | 'Producción'
    QueryDefinitionId INT               NOT NULL,
    MappingPipelineId INT               NOT NULL,
    OutputFormat      NVARCHAR(30)      NOT NULL CONSTRAINT DF_Reports_Format   DEFAULT ('Excel'),
    OutputSheetName   NVARCHAR(100)     NULL,
    OutputFilePattern NVARCHAR(250)     NOT NULL,
    IsActive          BIT               NOT NULL CONSTRAINT DF_Reports_IsActive DEFAULT (1),
    CreatedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_Reports_Created  DEFAULT (SYSUTCDATETIME()),
    UpdatedAt         DATETIME2(0)      NULL,
    CONSTRAINT PK_Reports            PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Reports_Name       UNIQUE NONCLUSTERED (Name),
    CONSTRAINT CK_Reports_TargetArea CHECK (TargetArea IN (N'Recepci' + NCHAR(243) + N'n', N'Calidad', N'Producci' + NCHAR(243) + N'n')),
    CONSTRAINT FK_Reports_QueryDef   FOREIGN KEY (QueryDefinitionId) REFERENCES dbo.QueryDefinitions(Id),
    CONSTRAINT FK_Reports_Pipeline   FOREIGN KEY (MappingPipelineId) REFERENCES dbo.MappingPipelines(Id)
);

-- 10. Asignación de Reportes por Usuario (Mantiene el logueo y acceso por usuario/reporte)
CREATE TABLE dbo.UserReports (
    UserId   INT NOT NULL,
    ReportId INT NOT NULL,
    CONSTRAINT PK_UserReports        PRIMARY KEY CLUSTERED (UserId, ReportId),
    CONSTRAINT FK_UserReports_User   FOREIGN KEY (UserId)   REFERENCES dbo.Users(Id)   ON DELETE CASCADE,
    CONSTRAINT FK_UserReports_Report FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id) ON DELETE CASCADE
);

/* =========================================================================================
   BLOQUE 3: PERSISTENCIA DE REGISTROS GENERADOS Y AUDITORÍA (4 TABLAS)
   ========================================================================================= */

-- 11. RECEPCIÓN: Tabla independiente cuya PK es 'Lote'
CREATE TABLE dbo.RegistrosRecepcion (
    Lote                     NVARCHAR(100)  NOT NULL, -- PK: Identificador único del lote de recepción
    FechaHoraCreacionArchivo DATETIME2(0)   NOT NULL CONSTRAINT DF_RegRec_Fecha  DEFAULT (SYSUTCDATETIME()),
    Productor                NVARCHAR(150)  NULL,     -- Información general extraída
    Kg                       DECIMAL(18, 2) NULL,     -- Información general extraída
    Estado                   VARCHAR(20)    NOT NULL CONSTRAINT DF_RegRec_Estado DEFAULT ('Cargado'),
    ReportId                 INT            NOT NULL,
    CONSTRAINT PK_RegistrosRecepcion        PRIMARY KEY CLUSTERED (Lote),
    CONSTRAINT CK_RegistrosRecepcion_Estado CHECK (Estado IN ('Cargado', 'Generado')),
    CONSTRAINT FK_RegistrosRecepcion_Report FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);

CREATE NONCLUSTERED INDEX IX_RegistrosRecepcion_Report_Fecha
    ON dbo.RegistrosRecepcion (ReportId, FechaHoraCreacionArchivo DESC);

-- 12. CALIDAD: Tabla independiente que comparte la misma estructura PK 'Lote' (sin FK bloqueante hacia Recepción)
CREATE TABLE dbo.RegistrosCalidad (
    Lote                     NVARCHAR(100) NOT NULL, -- PK: Mismo identificador único de lote (relación 1:1 lógica, tablas independientes)
    FechaHoraCreacionArchivo DATETIME2(0)  NOT NULL CONSTRAINT DF_RegCal_Fecha  DEFAULT (SYSUTCDATETIME()),
    Productor                NVARCHAR(150) NULL,     -- Información general extraída
    Estado                   VARCHAR(20)   NOT NULL CONSTRAINT DF_RegCal_Estado DEFAULT ('Cargado'),
    ReportId                 INT           NOT NULL,
    CONSTRAINT PK_RegistrosCalidad        PRIMARY KEY CLUSTERED (Lote),
    CONSTRAINT CK_RegistrosCalidad_Estado CHECK (Estado IN ('Cargado', 'Generado')),
    CONSTRAINT FK_RegistrosCalidad_Report FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);

CREATE NONCLUSTERED INDEX IX_RegistrosCalidad_Report_Fecha
    ON dbo.RegistrosCalidad (ReportId, FechaHoraCreacionArchivo DESC);

-- 13. PRODUCCIÓN: Tabla independiente donde existen N registros por cada 'LoteProceso'
CREATE TABLE dbo.RegistrosProduccion (
    LoteProceso              NVARCHAR(100) NOT NULL, -- Lote de proceso (AN051xxxxx)
    CodigoPallet             NVARCHAR(100) NOT NULL, -- Identificador único de tarja/pallet del packing
    FechaHoraCreacionArchivo DATETIME2(0)  NOT NULL CONSTRAINT DF_RegProd_Fecha  DEFAULT (SYSUTCDATETIME()),
    Productor                NVARCHAR(150) NULL,     -- Información general extraída
    Calibre                  NVARCHAR(50)  NULL,     -- Información general extraída
    Estado                   VARCHAR(20)   NOT NULL CONSTRAINT DF_RegProd_Estado DEFAULT ('Cargado'),
    ReportId                 INT           NOT NULL,
    CONSTRAINT PK_RegistrosProduccion        PRIMARY KEY CLUSTERED (ReportId, LoteProceso, CodigoPallet),
    CONSTRAINT CK_RegistrosProduccion_Estado CHECK (Estado IN ('Cargado', 'Generado')),
    CONSTRAINT FK_RegistrosProduccion_Report FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);

CREATE NONCLUSTERED INDEX IX_RegistrosProduccion_LoteProceso
    ON dbo.RegistrosProduccion (ReportId, LoteProceso)
    INCLUDE (CodigoPallet, Productor, Calibre, FechaHoraCreacionArchivo, Estado);

-- 14. BITÁCORA DE EJECUCIONES POR USUARIO Y REPORTE
CREATE TABLE dbo.ExecutionLogs (
    Id              INT IDENTITY(1,1) NOT NULL,
    ReportId        INT               NOT NULL,
    UserId          INT               NULL,
    ExecutedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_ExecLogs_Date DEFAULT (SYSUTCDATETIME()),
    RowsExtracted   INT               NOT NULL,
    RowsOutput      INT               NOT NULL,
    DurationSeconds DECIMAL(10, 3)    NOT NULL,
    Status          VARCHAR(20)       NOT NULL, -- 'Success' | 'Empty' | 'Failed'
    ErrorMessage    NVARCHAR(2000)    NULL,
    CONSTRAINT PK_ExecutionLogs        PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ExecutionLogs_Report FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id),
    CONSTRAINT FK_ExecutionLogs_User   FOREIGN KEY (UserId)   REFERENCES dbo.Users(Id)
);
GO

/* =========================================================================================
   MIGRACIÓN ÍNTEGRA DE CONFIGURACIONES, PIPELINES Y LAS 316 REGLAS DESDE [DataHomologator]
   (Convierte todos los GUIDs antiguos a IDs secuenciales 0001, 0002, 0003...)
   ========================================================================================= */

-- 1. Migrar Usuarios (con su Rol y Perfil reales, sin MustChangePassword ni UpdatedAt)
INSERT INTO dbo.Users (Email, DisplayName, PasswordHash, Rol, Perfil, IsActive, CreatedAt)
SELECT
    u.Email,
    u.DisplayName,
    u.PasswordHash,
    CASE WHEN EXISTS (
        SELECT 1 FROM [DataHomologator].dbo.UserRoles ur
        JOIN [DataHomologator].dbo.Roles r ON r.Id = ur.RoleId
        WHERE ur.UserId = u.Id AND r.Name = 'Admin'
    ) THEN 'Admin' ELSE 'Operator' END AS Rol,
    up.Name AS Perfil,
    u.IsActive,
    u.CreatedAt
FROM [DataHomologator].dbo.Users u
LEFT JOIN [DataHomologator].dbo.UserProfiles up ON up.Id = u.ProfileId
ORDER BY u.CreatedAt;

-- 2. Migrar DataSources reales
INSERT INTO dbo.DataSources (Name, Server, [Database], AuthType, UsernameEncrypted, PasswordEncrypted, ConnectionTimeout, IsActive, CreatedAt, UpdatedAt)
SELECT Name, Server, [Database], AuthType, UsernameEncrypted, PasswordEncrypted, ConnectionTimeout, IsActive, CreatedAt, UpdatedAt
FROM [DataHomologator].dbo.DataSources
ORDER BY CreatedAt;

-- 3. Migrar QueryDefinitions reales
INSERT INTO dbo.QueryDefinitions (Name, DataSourceId, CommandType, CommandText, Description, IsActive, CreatedAt, UpdatedAt)
SELECT
    qd.Name,
    dsNew.Id,
    qd.CommandType,
    qd.CommandText,
    qd.Description,
    qd.IsActive,
    qd.CreatedAt,
    qd.UpdatedAt
FROM [DataHomologator].dbo.QueryDefinitions qd
JOIN [DataHomologator].dbo.DataSources dsOld ON dsOld.Id = qd.DataSourceId
JOIN dbo.DataSources dsNew ON dsNew.Name = dsOld.Name
ORDER BY qd.Name;

-- 4. Migrar QueryParameters reales
INSERT INTO dbo.QueryParameters (QueryDefinitionId, SortOrder, Name, SqlType, DefaultValue, Description)
SELECT
    qdNew.Id,
    qp.SortOrder,
    qp.Name,
    qp.SqlType,
    qp.DefaultValue,
    qp.Description
FROM [DataHomologator].dbo.QueryParameters qp
JOIN [DataHomologator].dbo.QueryDefinitions qdOld ON qdOld.Id = qp.QueryDefinitionId
JOIN dbo.QueryDefinitions qdNew ON qdNew.Name = qdOld.Name
ORDER BY qdNew.Id, qp.SortOrder;

-- 5. Migrar LookupTables reales
INSERT INTO dbo.LookupTables (Name, Description, CreatedAt, UpdatedAt)
SELECT Name, Description, CreatedAt, UpdatedAt
FROM [DataHomologator].dbo.LookupTables
ORDER BY Name;

-- 6. Migrar LookupEntries reales
INSERT INTO dbo.LookupEntries (LookupTableId, KeyValue, MappedValue, ExtraField1)
SELECT
    ltNew.Id,
    le.KeyValue,
    le.MappedValue,
    le.ExtraField1
FROM [DataHomologator].dbo.LookupEntries le
JOIN [DataHomologator].dbo.LookupTables ltOld ON ltOld.Id = le.LookupTableId
JOIN dbo.LookupTables ltNew ON ltNew.Name = ltOld.Name
ORDER BY ltNew.Id, le.KeyValue;

-- 7. Migrar SOLO los 3 MappingPipelines oficiales requeridos para Andinexia
INSERT INTO dbo.MappingPipelines (Name, Description, IsActive, CreatedAt, UpdatedAt)
SELECT Name, Description, IsActive, CreatedAt, UpdatedAt
FROM [DataHomologator].dbo.MappingPipelines
WHERE Name IN ('Calidad Andinexia', 'Recepciones 26-10') OR Name LIKE 'Pipeline SAP Producc%'
ORDER BY Name;

-- 8. Migrar únicamente las reglas pertenecientes a los 3 pipelines oficiales (122 reglas)
INSERT INTO dbo.MappingRules (
    PipelineId, SortOrder, TargetColumn, RuleType, SourceColumn, FixedValue,
    LookupTableId, LookupKeyColumn, LookupValueColumn, DateFormat, Validation,
    IsRequired, IsIdentity, SubstringStart, SubstringLength
)
SELECT
    mpNew.Id,
    mr.SortOrder,
    mr.TargetColumn,
    mr.RuleType,
    mr.SourceColumn,
    mr.FixedValue,
    ltNew.Id,
    mr.LookupKeyColumn,
    mr.LookupValueColumn,
    mr.DateFormat,
    mr.Validation,
    mr.IsRequired,
    mr.IsIdentity,
    mr.SubstringStart,
    mr.SubstringLength
FROM [DataHomologator].dbo.MappingRules mr
JOIN [DataHomologator].dbo.MappingPipelines mpOld ON mpOld.Id = mr.PipelineId
JOIN dbo.MappingPipelines mpNew ON mpNew.Name = mpOld.Name
LEFT JOIN [DataHomologator].dbo.LookupTables ltOld ON ltOld.Id = mr.LookupTableId
LEFT JOIN dbo.LookupTables ltNew ON ltNew.Name = ltOld.Name
ORDER BY mpNew.Id, mr.SortOrder;

-- Parametrización de identidad dinámica en Producción:
-- Se define como clave compuesta (LOTEPROCESO + CODIGO PALLET) en las reglas del pipeline
UPDATE mr
SET mr.IsIdentity = 1
FROM dbo.MappingRules mr
JOIN dbo.MappingPipelines mp ON mp.Id = mr.PipelineId
WHERE mp.Name LIKE 'Pipeline SAP Producc%'
  AND mr.TargetColumn IN ('LOTEPROCESO', 'CODIGO PALLET');

UPDATE mr
SET mr.IsIdentity = 0
FROM dbo.MappingRules mr
JOIN dbo.MappingPipelines mp ON mp.Id = mr.PipelineId
WHERE mp.Name LIKE 'Pipeline SAP Producc%'
  AND mr.TargetColumn = 'LOTERECEPCION';

-- 9. Migrar los 3 Reports principales para Andinexia
INSERT INTO dbo.Reports (
    Name, Description, TargetArea, QueryDefinitionId, MappingPipelineId,
    OutputFormat, OutputSheetName, OutputFilePattern, IsActive, CreatedAt, UpdatedAt
)
SELECT
    r.Name,
    r.Description,
    CASE
        WHEN r.Name LIKE N'%Calidad%'    THEN N'Calidad'
        WHEN r.Name LIKE N'%Producc%'   THEN N'Producci' + NCHAR(243) + N'n'
        ELSE N'Recepci' + NCHAR(243) + N'n'
    END AS TargetArea,
    qdNew.Id,
    mpNew.Id,
    r.OutputFormat,
    r.OutputSheetName,
    r.OutputFilePattern,
    r.IsActive,
    r.CreatedAt,
    r.UpdatedAt
FROM [DataHomologator].dbo.Reports r
JOIN [DataHomologator].dbo.QueryDefinitions qdOld ON qdOld.Id = r.QueryDefinitionId
JOIN dbo.QueryDefinitions qdNew ON qdNew.Name = qdOld.Name
JOIN [DataHomologator].dbo.MappingPipelines mpOld ON mpOld.Id = r.MappingPipelineId
JOIN dbo.MappingPipelines mpNew ON mpNew.Name = mpOld.Name
WHERE r.Name NOT LIKE '%Recepciones Andinexia 26/27%'
ORDER BY r.Name;

-- 10. Migrar las asignaciones de UserReports para los 3 reportes
INSERT INTO dbo.UserReports (UserId, ReportId)
SELECT uNew.Id, rNew.Id
FROM [DataHomologator].dbo.UserReports ur
JOIN [DataHomologator].dbo.Users uOld ON uOld.Id = ur.UserId
JOIN dbo.Users uNew ON uNew.Email = uOld.Email
JOIN [DataHomologator].dbo.Reports rOld ON rOld.Id = ur.ReportId
JOIN dbo.Reports rNew ON rNew.Name = rOld.Name;
GO
