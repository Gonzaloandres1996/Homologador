/* =========================================================================================
   PROYECTO: Homologador de Packings Externos (DDC) — Versión Simplificada (Auditoría DBA)
   MOTOR:    Microsoft SQL Server (Local .\SQLEXPRESS y destino Azure SQL Database)
   BASE:     DataHomologator_Migracion
   OBJETIVO: Esquema 100% relacional auditado y simplificado (13 tablas):
             1. Cero GUIDs (UNIQUEIDENTIFIER): Uso de INT IDENTITY(1,1) + Codigo ('0001', '0002', '0003').
             2. Seguridad simplificada: Users absorbe Roles, UserRoles y UserProfiles mediante CHECK constraints.
             3. Pipelines y Reglas de Mapeo COMPLETOS (incluye los 3 MappingPipelines y las 122 MappingRules validadas
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
   BLOQUE 4: CARGA DE CONFIGURACIÓN Y CATÁLOGOS OFICIALES (122 REGLAS / 3 REPORTES)
   Base de datos 100% autocontenida: no requiere de bases de datos intermedias ni externas.
   ========================================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.Users (4 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[Users] ON;
INSERT INTO dbo.[Users] ([Id], [Email], [DisplayName], [PasswordHash], [Rol], [Perfil], [IsActive], [CreatedAt]) VALUES
    (1, N'gonzalo.aravena@ddc.cl', N'System Administrator', N'$2a$11$LG7UuljlXLcRBnkUKpfkz.oowfNjinsCUuIR8vgiY4VJ5bL/ztdRq', N'Admin', NULL, 1, '2026-09-29 11:27:33'),
    (2, N'michelle.cornejo@ddc.cl', N'Operador de Recepción', N'$2a$11$GVj.IFqMDgLppfhoyZdUc.2o97Sdtxramdr0Bjy3duXeikv8ZBXSa', N'Operator', N'Recepci' + NCHAR(243) + N'n', 1, '2026-09-29 11:27:34'),
    (3, N'marcela.acuna@ddc.cl', N'Marcela Acuña', N'$2a$11$CUnpzz67u2Tfewmrp9Te8e839nnQk2vVZVOGo3vLK3Pc8j5SQqjVy', N'Operator', N'Calidad', 1, '2026-10-26 19:45:36'),
    (4, N'eliana.rivas@ddc.cl', N'Eliana Rivas', N'$2a$11$7P/BxUvZEkjuiCHvXPSd7OvI2iNXQuQ24Au6gzrYxTWBz.TPUon2a', N'Operator', N'Producci' + NCHAR(243) + N'n', 1, '2026-10-26 19:45:56');
SET IDENTITY_INSERT dbo.[Users] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.DataSources (1 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[DataSources] ON;
INSERT INTO dbo.[DataSources] ([Id], [Name], [Server], [Database], [AuthType], [UsernameEncrypted], [PasswordEncrypted], [ConnectionTimeout], [IsActive], [CreatedAt], [UpdatedAt]) VALUES
    (1, N'DAIA Producción', N'192.168.3.17', N'daia', 1, N'gnEeSPaZDChK0ta7C91rkgUJdfcvSTD7b8zMWpOJN/Y=', N'qMMe4eqwHZgHaGsYw1/ZvA8Jp7dNCZk97ciHGQBcq1E=', 15, 1, '2026-09-23 14:56:23', '2026-09-29 14:19:30');
SET IDENTITY_INSERT dbo.[DataSources] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.QueryDefinitions (3 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[QueryDefinitions] ON;
INSERT INTO dbo.[QueryDefinitions] ([Id], [Name], [DataSourceId], [CommandType], [CommandText], [Description], [IsActive], [CreatedAt], [UpdatedAt]) VALUES
    (1, N'Calidad ANDINEXIA sql', 1, 1, N'[dbo].[SPC_CALIDAD_PIVOT]', N'Consulta pivot de calidad Andinexia', 0, '2026-10-13 16:43:20', '2026-10-26 14:41:34'),
    (2, N'Producción ANDINEXIA sql', 1, 1, N'EXEC [dbo].[SP_AUTH_VIEW] @view_name = N''viewProduccion''', N'Consulta pivot de producción Andinexia', 0, '2026-10-14 15:14:40', '2026-10-14 15:17:41'),
    (3, N'Recepción Materia Prima (DAIA)', 1, 1, N'EXEC [dbo].[SP_AUTH_VIEW] @view_name = N''viewRecepcion''', N'Extracción de recepciones desde vista autorizada viewRecepcion', 1, '2026-09-23 14:56:24', NULL);
SET IDENTITY_INSERT dbo.[QueryDefinitions] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.QueryParameters (6 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[QueryParameters] ON;
INSERT INTO dbo.[QueryParameters] ([Id], [QueryDefinitionId], [SortOrder], [Name], [SqlType], [DefaultValue], [Description]) VALUES
    (1, 1, 0, N'@CONTROL_TYPE', N'nvarchar', N'RCP', N'Tipo de control'),
    (2, 1, 1, N'@ESPECIE', N'nvarchar', N'CZ', N'Especie'),
    (3, 1, 2, N'@TEMPORADA', N'nvarchar', N'6', N'Temporada'),
    (4, 1, 3, N'@ESPECIE', N'nvarchar', N'Cz', N''),
    (5, 2, 0, N'@view_name', N'nvarchar', N'viewProduccion', N''),
    (6, 3, 0, N'@view_name', N'nvarchar', N'viewRecepcion', N'Nombre de la vista origen');
SET IDENTITY_INSERT dbo.[QueryParameters] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.LookupTables (4 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[LookupTables] ON;
INSERT INTO dbo.[LookupTables] ([Id], [Name], [Description], [CreatedAt], [UpdatedAt]) VALUES
    (1, N'Especies', N'', '2026-09-23 17:57:20', '2026-10-26 17:49:46'),
    (2, N'Esponjas', N'', '2026-10-13 19:42:03', '2026-10-13 19:42:26'),
    (3, N'Polvo Tierra', N'', '2026-10-13 19:45:14', '2026-10-13 19:45:27'),
    (4, N'Tratamiento', N'', '2026-10-13 19:32:54', '2026-10-13 19:33:12');
SET IDENTITY_INSERT dbo.[LookupTables] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.LookupEntries (7 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[LookupEntries] ON;
INSERT INTO dbo.[LookupEntries] ([Id], [LookupTableId], [KeyValue], [MappedValue], [ExtraField1]) VALUES
    (1, 1, N'Cereza', N'CEREZAS', NULL),
    (2, 1, N'Ciruela', N'CIRUELAS', NULL),
    (3, 2, N'no', N'No Cumple', NULL),
    (4, 2, N'si', N'Cumple', NULL),
    (5, 3, N'no', N'Cumple', NULL),
    (6, 3, N'si', N'No Cumple', NULL),
    (7, 4, N'Si', N'Hidrocooler', NULL);
SET IDENTITY_INSERT dbo.[LookupEntries] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.MappingPipelines (3 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[MappingPipelines] ON;
INSERT INTO dbo.[MappingPipelines] ([Id], [Name], [Description], [IsActive], [CreatedAt], [UpdatedAt]) VALUES
    (1, N'Calidad Andinexia', N'Carga de calidad Andinexia', 1, '2026-10-13 16:12:51', '2026-10-26 19:47:36'),
    (2, N'Pipeline SAP Producción', N'Mapeo hacia formato SAP con columnas reales de DAIA', 1, '2026-10-14 15:19:02', '2026-10-28 18:48:19'),
    (3, N'Recepciones 26-10', N'Mapeo hacia formato SAP con columnas reales de DAIA', 1, '2026-10-26 12:06:02', '2026-10-26 19:38:29');
SET IDENTITY_INSERT dbo.[MappingPipelines] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.MappingRules (122 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[MappingRules] ON;
INSERT INTO dbo.[MappingRules] ([Id], [PipelineId], [SortOrder], [TargetColumn], [RuleType], [SourceColumn], [FixedValue], [LookupTableId], [LookupKeyColumn], [LookupValueColumn], [DateFormat], [Validation], [IsRequired], [IsIdentity], [SubstringStart], [SubstringLength]) VALUES
    (1, 1, 1, N'PARTIDA', N'Formatted', N'Lote', N'A51', NULL, NULL, NULL, NULL, NULL, 0, 1, 5, 4),
    (2, 1, 2, N'T1', N'Direct', N'Temperatura Pulpa', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (3, 1, 3, N'T2', N'Direct', N'Temperatura Pulpa', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (4, 1, 4, N'T3', N'Direct', N'Temperatura Pulpa', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (5, 1, 5, N'PESOTOTALMUESTRA', N'Direct', N'Peso Muestra', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (6, 1, 6, N'Blando', N'Direct', N'%MUY BLANDO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (7, 1, 7, N'Sensible', N'Direct', N'%BLANDO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (8, 1, 8, N'Firme', N'Direct', N'%FIRME', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (9, 1, 9, N'Muy firme', N'Direct', N'%MUY FIRME', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (10, 1, 10, N'Precalibre', N'Direct', N'PRECALIBRE', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (11, 1, 11, N'Calibre L', N'Direct', N'L', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (12, 1, 12, N'Calibre XL', N'Direct', N'XL', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (13, 1, 13, N'Calibre J', N'Direct', N'J', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (14, 1, 14, N'Calibre 2J', N'Direct', N'2J', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (15, 1, 15, N'Calibre 3J', N'Direct', N'3J', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (16, 1, 16, N'Calibre 4J', N'Direct', N'4J', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (17, 1, 17, N'Calibre 5J', N'Direct', N'5J', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (18, 1, 18, N'Calibre 6J', N'Direct', N'6J', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (19, 1, 19, N'Falta de Color - 0', N'Direct', N'FALTA DE COLOR', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (20, 1, 20, N'Rojo Claro - 1', N'Fixed', NULL, N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (21, 1, 21, N'Rojo - 2', N'Fixed', NULL, N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (22, 1, 22, N'Rojo Caoba - 3', N'Direct', N'Rojo', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (23, 1, 23, N'Rojo Caoba - 3.3', N'Fixed', NULL, N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (24, 1, 24, N'Rojo Santina - 3.5', N'Direct', N'Rojo caoba', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (25, 1, 25, N'Rojo Caoba 3,8 - 3.8', N'Fixed', NULL, N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (26, 1, 26, N'Rojo Caoba Oscuro - 4', N'Direct', N'Caoba oscuro', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (27, 1, 27, N'Negro - 5', N'Direct', N'Negro', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (28, 1, 28, N'Dano Pajaro', N'Direct', N'DAÑO DE PAJARO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (29, 1, 29, N'Dano de Insecto', N'Direct', N'DAÑO DE INSECTO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (30, 1, 30, N'Fruto Deshidratado', N'Direct', N'FRUTO DESHIDRATADO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (31, 1, 31, N'Fruto Blando', N'Direct', N'FRUTO BLANDO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (32, 1, 32, N'Heridas Abiertas', N'Direct', N'HERIDA ABIERTA', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (33, 1, 33, N'Machucon', N'Direct', N'MACHUCON', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (34, 1, 34, N'Mancha Parda', N'Direct', N'MANCHA PARDA', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (35, 1, 35, N'Media Luna', N'Direct', N'MEDIA LUNA CICATRIZADA', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (36, 1, 36, N'Partidura Apical', N'Direct', N'PARTIDURA APICAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (37, 1, 37, N'Partidura Basal', N'Aggregate', N'PARTIDURA BASAL, MEDIA LUNA', N'SUM', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (38, 1, 38, N'Partidura Longitudinal', N'Direct', N'PARTIDURA LONGITUDINAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (39, 1, 39, N'Partiduras', N'Fixed', NULL, N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (40, 1, 40, N'Pedicelo Deshidratado', N'Direct', N'PEDICELO DESHIDRATADO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (41, 1, 41, N'Pitting', N'Direct', N'PITTING', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (42, 1, 42, N'Pudricion (Piel Seca)', N'Direct', N'PUDRICIÓN', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (43, 1, 43, N'Pudricion Seca', N'Direct', N'PUDRICION SECA', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (44, 1, 44, N'Residuos Químicos', N'Direct', N'RESIDUOS QUIMICOS', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (45, 1, 45, N'Roce', N'Direct', N'ROCE', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (46, 1, 46, N'Sutura Abierta', N'Direct', N'SUTURA SEVERA (ABIERTA)', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (47, 1, 47, N'Varios Condicion', N'Fixed', N'OTRO CONDICION', N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (48, 1, 48, N'Frutos Deformes', N'Direct', N'FRUTO DEFORME', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (49, 1, 49, N'Frutos Dobles', N'Direct', N'FRUTOS DOBLES', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (50, 1, 50, N'Golpe de Sol', N'Direct', N'GOLPE DE SOL', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (51, 1, 51, N'Heridas Cicatrizadas', N'Direct', N'HERIDA CICATRIZADA', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (52, 1, 52, N'Hijuelos', N'Direct', N'HIJUELO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (53, 1, 53, N'Mancha Dorada', N'Fixed', NULL, N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (54, 1, 54, N'Mancha Trips', N'Direct', N'DAÑO TRIPS', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (55, 1, 55, N'Russet', N'Direct', N'RUSSET', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (56, 1, 56, N'Sin Pedicelo', N'Direct', N'FRUTO SIN PEDICELO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (57, 1, 57, N'Sutura Cerrada', N'Direct', N'SUTURA ABIERTA', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (58, 1, 58, N'Varios Calidad', N'Fixed', N'0', N'0', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (59, 1, 59, N'Virosis', N'Direct', N'SINTOMAS DE VIROSIS', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (60, 1, 60, N'Certificacion GGAP', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (61, 1, 61, N'Presencia de Escama', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (62, 1, 62, N'Presencia de Lobesia Botrana', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (63, 1, 63, N'Presencia de Cyidia Molesta', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (64, 1, 64, N'Presencia de Chancito Blanco', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (65, 1, 65, N'Presencia de Arana', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (66, 1, 66, N'Set Point Camion', N'Direct', N'Temperatura de Camión', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (67, 1, 67, N'Solidos Solubles Dark', N'Aggregate', N'brix_caoba, brix_caoba_oscuro', N'AVG', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (68, 1, 68, N'Solidos Solubles Light', N'Aggregate', N'brix_rojo, brix_rojo_caoba', N'AVG', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (69, 1, 69, N'Tipo de Packing', N'Fixed', NULL, N'DDC', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (70, 1, 70, N'Analisis Multiresiduos', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (71, 1, 71, N'Tratamiento', N'Homologation', N'Fruta con Hidro', NULL, 4, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (72, 1, 72, N'Sensibilidad a Partiduria', N'Empty', NULL, N'1 vez por productor', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (73, 1, 73, N'FirmPro Promedio', N'Direct', N'FIRMEZA PROMEDIO', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (74, 1, 74, N'Polvo y Tierra', N'Homologation', N'PRESENCIA DE POLVO', NULL, 3, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (75, 1, 75, N'Esponjas cloradas', N'Homologation', N'ESPONJA', NULL, 2, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (76, 1, 76, N'Estriba de camiones', N'Fixed', N'TIPO DE CUBIERTA', N'Pendiente Cumple o No cumple AN', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (77, 1, 77, N'Nota pedicelo', N'Fixed', NULL, N'Pendiente Campo AN', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (78, 1, 78, N'Dano asociado a la cosecha', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (79, 1, 79, N'Aptitud de Viaje', N'Empty', NULL, N'Ingresa DDC (vacio)', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (80, 1, 80, N'Tipo de Frio', N'Fixed', NULL, N'FC', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (81, 2, 1, N'CENTRO', N'Fixed', NULL, N'AN05', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (82, 2, 2, N'', N'Empty', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (83, 2, 3, N'ALMACEN 1', N'Fixed', NULL, N'PA03', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (84, 2, 4, N'ALMACEN 2', N'Fixed', NULL, N'PA04', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (85, 2, 5, N'ESPECIE', N'Homologation', N'Especie', NULL, 1, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (86, 2, 6, N'LOTERECEPCION', N'Formatted', N'Nro. Proceso', N'AN051', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, 5),
    (87, 2, 7, N'LOTEPROCESO', N'Formatted', N'Nro. Proceso', N'AN051', NULL, NULL, NULL, NULL, NULL, 0, 1, NULL, 5),
    (88, 2, 8, N'CODIGO PALLET', N'Direct', N'Lote', N'Tabla', NULL, NULL, NULL, NULL, NULL, 0, 1, NULL, NULL),
    (89, 2, 9, N'ALTURA', N'Direct', N'TABLA', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (90, 2, 10, N'FECHA CONTABILIZACION', N'Direct', N'Fecha packing', NULL, NULL, NULL, NULL, N'dd.MM.yyyy', NULL, 0, 0, NULL, NULL),
    (91, 2, 11, N'FECHA EMBALAJE', N'Direct', N'Fecha packing', NULL, NULL, NULL, NULL, N'dd.MM.yyyy', NULL, 0, 0, NULL, NULL),
    (92, 2, 12, N'MATERIAL', N'Direct', N'Embalaje_Referido', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (93, 2, 13, N'TIPO MATERIAL(PALLET, BIN, ETC)', N'Fixed', NULL, N'TACO', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (94, 2, 14, N'CANTIDAD', N'Direct', N'Cant.cajas', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (95, 2, 15, N'CALIBRE', N'Direct', N'Calibre', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (96, 2, 16, N'PRODUCTOR', N'Direct', N'Codigo_Interno_Productor', N'Codigo_Interno_Productor', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (97, 2, 17, N'VARIEDAD ROTULADA', N'Direct', N'Codigo Variedad', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (98, 2, 18, N'CALIDAD', N'Direct', N'Categoria', NULL, NULL, NULL, NULL, NULL, N'COM, GRA, DES, PC', 0, 0, NULL, NULL),
    (99, 2, 19, N'KILOS RECEPCION', N'Empty', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (100, 3, 1, N'Centro', N'Fixed', NULL, N'AN05', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (101, 3, 2, N'AlmacenGranel', N'Fixed', NULL, N'FR06', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (102, 3, 3, N'Material', N'Homologation', N'Especie', NULL, 1, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (103, 3, 4, N'Productor', N'Direct', N'Codigo_Interno_Productor', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (104, 3, 5, N'UAT', N'Computed', N'UAT', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (105, 3, 6, N'FechaContabilizacion', N'Direct', N'Fecha Recepción', NULL, NULL, NULL, NULL, N'dd.MM.yyyy', NULL, 0, 0, NULL, NULL),
    (106, 3, 7, N'FechaCosecha', N'Direct', N'Fecha_Cosecha', NULL, NULL, NULL, NULL, N'dd.MM.yyyy', NULL, 0, 0, NULL, NULL),
    (107, 3, 8, N'PredioSDP', N'Computed', N'SDP', N'N/A', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (108, 3, 9, N'Cuartel', N'Computed', N'Cuartel', N'NA', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (109, 3, 10, N'Variedad', N'Direct', N'Codigo Variedad', N'P_ANDINEXIA codddc', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (110, 3, 11, N'GuiadeDespacho', N'Direct', N'Guia de despacho', N'Pendiente', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (111, 3, 12, N'Observacion', N'Direct', N'Observacion', N'Observacion', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (112, 3, 13, N'Patente', N'Direct', N'Patente', N'Pendiente', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (113, 3, 14, N'Conductor', N'Direct', N'Nombre Chofer', N'Pendiente', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (114, 3, 15, N'Kilos', N'Direct', N'Peso lote', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (115, 3, 16, N'Cantidad', N'Direct', N'Cant. bins', N'Pendiente ''cant bins''', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (116, 3, 17, N'Tipo Envase', N'Fixed', NULL, N'EP30', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (117, 3, 18, N'Cntd Envases', N'Direct', N'Cant. bins', NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (118, 3, 19, N'MaterialDestare', N'Fixed', NULL, N'EP20', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (119, 3, 20, N'Cntd MAT_2', N'Direct', N'Totes', N'P_ANDINEXIA "Totes"', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (120, 3, 21, N'Acopio', N'Fixed', NULL, N'AN05', NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL),
    (121, 3, 22, N'Lote', N'Formatted', N'Lote', N'A51', NULL, NULL, NULL, NULL, NULL, 0, 1, 5, 4),
    (122, 3, 23, N'Cliente', N'Empty', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, NULL);
SET IDENTITY_INSERT dbo.[MappingRules] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.Reports (3 filas)
-- -------------------------------------------------------------
SET IDENTITY_INSERT dbo.[Reports] ON;
INSERT INTO dbo.[Reports] ([Id], [Name], [Description], [TargetArea], [QueryDefinitionId], [MappingPipelineId], [OutputFormat], [OutputSheetName], [OutputFilePattern], [IsActive], [CreatedAt], [UpdatedAt]) VALUES
    (1, N'Calidad ANDINEXIA', N'', N'Calidad', 1, 1, N'Excel', N'Data', N'Calidad_{yyyyMMdd_HHmmss}.xlsx', 1, '2026-10-13 17:02:05', NULL),
    (2, N'Carga Masiva SAP Recepción MP', N'Planilla homologada lista para SAP a partir de DAIA', N'Recepci' + NCHAR(243) + N'n', 3, 3, N'Excel', N'RECEPCION', N'SAP_RECEPCION_{yyyyMMdd_HHmmss}.xlsx', 1, '2026-09-23 14:56:27', '2026-10-26 19:37:15'),
    (3, N'Producción ANDINEXIA', N'', N'Producci' + NCHAR(243) + N'n', 2, 2, N'Excel', N'PRODUCCION', N'Proceso_{yyyyMMdd_HHmmss}.xlsx', 1, '2026-10-14 16:55:41', '2026-10-15 13:41:47');
SET IDENTITY_INSERT dbo.[Reports] OFF;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- -------------------------------------------------------------
-- Datos: dbo.UserReports (3 filas)
-- -------------------------------------------------------------
INSERT INTO dbo.[UserReports] ([UserId], [ReportId]) VALUES
    (2, 2),
    (3, 1),
    (4, 3);
GO
