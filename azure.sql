/* =========================================================================================
   BASE DE DATOS: HomologatorAzure
   PROPÓSITO:     Autenticación (Users, UserReports) y Persistencia de Operación (Registros y Logs)
   MOTOR:         SQL Server (Local .\SQLEXPRESS y Azure SQL Database)
   ========================================================================================= */

USE [master];
GO

IF DB_ID(N'HomologatorAzure') IS NOT NULL
BEGIN
    ALTER DATABASE [HomologatorAzure] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [HomologatorAzure];
END
GO

CREATE DATABASE [HomologatorAzure];
GO

USE [HomologatorAzure];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Usuarios del Sistema
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

-- 2. Asignación de Reportes a Usuarios (ReportId es una referencia lógica hacia HomologatorConfig.Reports)
CREATE TABLE dbo.UserReports (
    UserId   INT NOT NULL,
    ReportId INT NOT NULL, -- Clave foránea lógica hacia HomologatorConfig.dbo.Reports(Id)
    CONSTRAINT PK_UserReports      PRIMARY KEY CLUSTERED (UserId, ReportId),
    CONSTRAINT FK_UserReports_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);

-- 3. RECEPCIÓN: Persistencia de lotes generados (PK: Lote)
CREATE TABLE dbo.RegistrosRecepcion (
    Lote                     NVARCHAR(100)  NOT NULL, -- PK: Identificador único del lote de recepción
    FechaHoraCreacionArchivo DATETIME2(0)   NOT NULL CONSTRAINT DF_RegRec_Fecha  DEFAULT (SYSUTCDATETIME()),
    Productor                NVARCHAR(150)  NULL,
    Kg                       DECIMAL(18, 2) NULL,
    Estado                   VARCHAR(20)    NOT NULL CONSTRAINT DF_RegRec_Estado DEFAULT ('Cargado'),
    ReportId                 INT            NOT NULL, -- Clave foránea lógica hacia HomologatorConfig.dbo.Reports(Id)
    CONSTRAINT PK_RegistrosRecepcion        PRIMARY KEY CLUSTERED (Lote),
    CONSTRAINT CK_RegistrosRecepcion_Estado CHECK (Estado IN ('Cargado', 'Generado'))
);

CREATE NONCLUSTERED INDEX IX_RegistrosRecepcion_Report_Fecha
    ON dbo.RegistrosRecepcion (ReportId, FechaHoraCreacionArchivo DESC);

-- 4. CALIDAD: Persistencia de lotes generados (PK: Lote)
CREATE TABLE dbo.RegistrosCalidad (
    Lote                     NVARCHAR(100) NOT NULL, -- PK: Mismo identificador único de lote
    FechaHoraCreacionArchivo DATETIME2(0)  NOT NULL CONSTRAINT DF_RegCal_Fecha  DEFAULT (SYSUTCDATETIME()),
    Productor                NVARCHAR(150) NULL,
    Estado                   VARCHAR(20)   NOT NULL CONSTRAINT DF_RegCal_Estado DEFAULT ('Cargado'),
    ReportId                 INT           NOT NULL, -- Clave foránea lógica hacia HomologatorConfig.dbo.Reports(Id)
    CONSTRAINT PK_RegistrosCalidad        PRIMARY KEY CLUSTERED (Lote),
    CONSTRAINT CK_RegistrosCalidad_Estado CHECK (Estado IN ('Cargado', 'Generado'))
);

CREATE NONCLUSTERED INDEX IX_RegistrosCalidad_Report_Fecha
    ON dbo.RegistrosCalidad (ReportId, FechaHoraCreacionArchivo DESC);

-- 5. PRODUCCIÓN: Persistencia de tarjas/pallets generados
CREATE TABLE dbo.RegistrosProduccion (
    LoteProceso              NVARCHAR(100) NOT NULL,
    CodigoPallet             NVARCHAR(100) NOT NULL,
    FechaHoraCreacionArchivo DATETIME2(0)  NOT NULL CONSTRAINT DF_RegProd_Fecha  DEFAULT (SYSUTCDATETIME()),
    Productor                NVARCHAR(150) NULL,
    Calibre                  NVARCHAR(50)  NULL,
    Estado                   VARCHAR(20)   NOT NULL CONSTRAINT DF_RegProd_Estado DEFAULT ('Cargado'),
    ReportId                 INT           NOT NULL, -- Clave foránea lógica hacia HomologatorConfig.dbo.Reports(Id)
    CONSTRAINT PK_RegistrosProduccion        PRIMARY KEY CLUSTERED (ReportId, LoteProceso, CodigoPallet),
    CONSTRAINT CK_RegistrosProduccion_Estado CHECK (Estado IN ('Cargado', 'Generado'))
);

CREATE NONCLUSTERED INDEX IX_RegistrosProduccion_LoteProceso
    ON dbo.RegistrosProduccion (ReportId, LoteProceso)
    INCLUDE (CodigoPallet, Productor, Calibre, FechaHoraCreacionArchivo, Estado);

/* =========================================================================================
   CARGA DE DATOS INICIALES DE SEGURIDAD
   ========================================================================================= */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
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
-- Datos: dbo.UserReports (3 filas)
-- -------------------------------------------------------------
INSERT INTO dbo.[UserReports] ([UserId], [ReportId]) VALUES
    (2, 2),
    (3, 1),
    (4, 3);
GO

