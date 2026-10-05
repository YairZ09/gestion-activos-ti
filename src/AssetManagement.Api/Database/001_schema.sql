IF OBJECT_ID('dbo.Users', 'U') IS NULL
CREATE TABLE dbo.Users (
    Id                  INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    Username            NVARCHAR(50)  NOT NULL,
    PasswordHash        NVARCHAR(400) NOT NULL,
    Role                NVARCHAR(20)  NOT NULL,
    IsActive            BIT           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    FailedLoginAttempts INT           NOT NULL CONSTRAINT DF_Users_Failed DEFAULT 0,
    LockoutEnd          DATETIME2(0)  NULL,
    CreatedAt           DATETIME2(0)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Administrador', 'Operador'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Username')
    CREATE UNIQUE INDEX UX_Users_Username ON dbo.Users (Username);
GO

IF OBJECT_ID('dbo.Suppliers', 'U') IS NULL
CREATE TABLE dbo.Suppliers (
    Id        INT IDENTITY(1,1) CONSTRAINT PK_Suppliers PRIMARY KEY,
    Name      NVARCHAR(150) NOT NULL,
    TaxId     NVARCHAR(30)  NULL,
    Email     NVARCHAR(150) NULL,
    Phone     NVARCHAR(30)  NULL,
    IsActive  BIT           NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT 1,
    CreatedAt DATETIME2(0)  NOT NULL CONSTRAINT DF_Suppliers_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Suppliers_Name')
    CREATE UNIQUE INDEX UX_Suppliers_Name ON dbo.Suppliers (Name);
GO

IF OBJECT_ID('dbo.SupplierServices', 'U') IS NULL
CREATE TABLE dbo.SupplierServices (
    SupplierId  INT         NOT NULL CONSTRAINT FK_SupplierServices_Supplier REFERENCES dbo.Suppliers(Id),
    ServiceType NVARCHAR(20) NOT NULL,
    CONSTRAINT PK_SupplierServices PRIMARY KEY (SupplierId, ServiceType),
    CONSTRAINT CK_SupplierServices_Type CHECK (ServiceType IN ('Compra', 'Mantenimiento', 'Arrendamiento'))
);
GO

IF OBJECT_ID('dbo.Employees', 'U') IS NULL
CREATE TABLE dbo.Employees (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_Employees PRIMARY KEY,
    EmployeeNumber NVARCHAR(30)  NOT NULL,
    FullName       NVARCHAR(150) NOT NULL,
    Email          NVARCHAR(150) NOT NULL,
    Department     NVARCHAR(100) NULL,
    IsActive       BIT           NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT 1,
    CreatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_Employees_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Employees_EmployeeNumber')
    CREATE UNIQUE INDEX UX_Employees_EmployeeNumber ON dbo.Employees (EmployeeNumber);
GO

IF OBJECT_ID('dbo.Assets', 'U') IS NULL
CREATE TABLE dbo.Assets (
    Id              INT IDENTITY(1,1) CONSTRAINT PK_Assets PRIMARY KEY,
    AssetCode       NVARCHAR(30)  NOT NULL,
    SerialNumber    NVARCHAR(80)  NULL,
    Category        NVARCHAR(50)  NOT NULL,
    Brand           NVARCHAR(80)  NOT NULL,
    Model           NVARCHAR(80)  NOT NULL,
    OwnershipType   NVARCHAR(10)  NOT NULL,
    SupplierId      INT           NULL CONSTRAINT FK_Assets_Supplier REFERENCES dbo.Suppliers(Id),
    Status          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Assets_Status DEFAULT 'Disponible',
    CurrentLocation NVARCHAR(150) NOT NULL,
    PurchaseDate    DATE          NULL,
    RentalEndDate   DATE          NULL,
    CreatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_Assets_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_Assets_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Assets_Status    CHECK (Status IN ('Disponible', 'Asignado', 'Mantenimiento', 'Retirado')),
    CONSTRAINT CK_Assets_Ownership CHECK (OwnershipType IN ('Propio', 'Arrendado')),
    CONSTRAINT CK_Assets_RentalSupplier CHECK (OwnershipType <> 'Arrendado' OR SupplierId IS NOT NULL)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Assets_AssetCode')
    CREATE UNIQUE INDEX UX_Assets_AssetCode ON dbo.Assets (AssetCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Assets_SerialNumber')
    CREATE UNIQUE INDEX UX_Assets_SerialNumber ON dbo.Assets (SerialNumber) WHERE SerialNumber IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Assets_Status_Category')
    CREATE INDEX IX_Assets_Status_Category ON dbo.Assets (Status, Category);
GO

IF OBJECT_ID('dbo.Assignments', 'U') IS NULL
CREATE TABLE dbo.Assignments (
    Id               INT IDENTITY(1,1) CONSTRAINT PK_Assignments PRIMARY KEY,
    AssetId          INT           NOT NULL CONSTRAINT FK_Assignments_Asset REFERENCES dbo.Assets(Id),
    EmployeeId       INT           NOT NULL CONSTRAINT FK_Assignments_Employee REFERENCES dbo.Employees(Id),
    AssignedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_Assignments_AssignedAt DEFAULT SYSUTCDATETIME(),
    AssignedByUserId INT           NOT NULL CONSTRAINT FK_Assignments_AssignedBy REFERENCES dbo.Users(Id),
    AssignNotes      NVARCHAR(500) NULL,
    ReturnedAt       DATETIME2(0)  NULL,
    ReturnedByUserId INT           NULL CONSTRAINT FK_Assignments_ReturnedBy REFERENCES dbo.Users(Id),
    ReturnCondition  NVARCHAR(20)  NULL,
    ReturnNotes      NVARCHAR(500) NULL,
    CONSTRAINT CK_Assignments_Condition CHECK (ReturnCondition IS NULL OR ReturnCondition IN ('Bueno', 'Regular', 'Danado'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Assignments_ActiveAsset')
    CREATE UNIQUE INDEX UX_Assignments_ActiveAsset ON dbo.Assignments (AssetId) WHERE ReturnedAt IS NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Assignments_Employee')
    CREATE INDEX IX_Assignments_Employee ON dbo.Assignments (EmployeeId);
GO

IF OBJECT_ID('dbo.AssetMovements', 'U') IS NULL
CREATE TABLE dbo.AssetMovements (
    Id                INT IDENTITY(1,1) CONSTRAINT PK_AssetMovements PRIMARY KEY,
    AssetId           INT           NOT NULL CONSTRAINT FK_Movements_Asset REFERENCES dbo.Assets(Id),
    MovementType      NVARCHAR(20)  NOT NULL,
    FromStatus        NVARCHAR(20)  NULL,
    ToStatus          NVARCHAR(20)  NULL,
    FromLocation      NVARCHAR(150) NULL,
    ToLocation        NVARCHAR(150) NULL,
    EmployeeId        INT           NULL CONSTRAINT FK_Movements_Employee REFERENCES dbo.Employees(Id),
    AssignmentId      INT           NULL CONSTRAINT FK_Movements_Assignment REFERENCES dbo.Assignments(Id),
    PerformedByUserId INT           NOT NULL CONSTRAINT FK_Movements_User REFERENCES dbo.Users(Id),
    PerformedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_Movements_PerformedAt DEFAULT SYSUTCDATETIME(),
    Notes             NVARCHAR(500) NULL,
    CONSTRAINT CK_Movements_Type CHECK (MovementType IN ('Alta', 'Asignacion', 'Devolucion', 'CambioEstado', 'CambioUbicacion'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Movements_Asset')
    CREATE INDEX IX_Movements_Asset ON dbo.AssetMovements (AssetId, PerformedAt DESC, Id DESC);
GO
