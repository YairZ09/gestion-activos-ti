CREATE OR ALTER PROCEDURE dbo.usp_User_GetByUsername
    @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Username, PasswordHash, Role, IsActive,
           CAST(CASE WHEN LockoutEnd IS NOT NULL AND LockoutEnd > SYSUTCDATETIME() THEN 1 ELSE 0 END AS BIT) AS IsLockedOut
    FROM dbo.Users
    WHERE Username = @Username;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_RegisterFailedLogin
    @UserId         INT,
    @MaxAttempts    INT,
    @LockoutMinutes INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
    SET FailedLoginAttempts = CASE WHEN FailedLoginAttempts + 1 >= @MaxAttempts THEN 0 ELSE FailedLoginAttempts + 1 END,
        LockoutEnd          = CASE WHEN FailedLoginAttempts + 1 >= @MaxAttempts
                                   THEN DATEADD(MINUTE, @LockoutMinutes, SYSUTCDATETIME()) ELSE LockoutEnd END
    WHERE Id = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_RegisterSuccessfulLogin
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET FailedLoginAttempts = 0, LockoutEnd = NULL WHERE Id = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_Create
    @Username     NVARCHAR(50),
    @PasswordHash NVARCHAR(400),
    @Role         NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username)
        INSERT dbo.Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Supplier_Create
    @Name     NVARCHAR(150),
    @TaxId    NVARCHAR(30)  = NULL,
    @Email    NVARCHAR(150) = NULL,
    @Phone    NVARCHAR(30)  = NULL,
    @Services NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
        INSERT dbo.Suppliers (Name, TaxId, Email, Phone) VALUES (@Name, @TaxId, @Email, @Phone);
        DECLARE @Id INT = SCOPE_IDENTITY();
        INSERT dbo.SupplierServices (SupplierId, ServiceType)
        SELECT DISTINCT @Id, LTRIM(RTRIM(value)) FROM STRING_SPLIT(@Services, ',') WHERE LTRIM(RTRIM(value)) <> '';
    COMMIT;
    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Supplier_GetById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.Id, s.Name, s.TaxId, s.Email, s.Phone, s.IsActive, s.CreatedAt,
           ISNULL((SELECT STRING_AGG(ss.ServiceType, ',') FROM dbo.SupplierServices ss WHERE ss.SupplierId = s.Id), '') AS Services
    FROM dbo.Suppliers s WHERE s.Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Supplier_Search
    @Search   NVARCHAR(150) = NULL,
    @Page     INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.Id, s.Name, s.TaxId, s.Email, s.Phone, s.IsActive, s.CreatedAt,
           ISNULL((SELECT STRING_AGG(ss.ServiceType, ',') FROM dbo.SupplierServices ss WHERE ss.SupplierId = s.Id), '') AS Services,
           COUNT(*) OVER() AS TotalCount
    FROM dbo.Suppliers s
    WHERE @Search IS NULL OR s.Name LIKE '%' + @Search + '%' OR s.TaxId LIKE '%' + @Search + '%'
    ORDER BY s.Name
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* ---------------------------- COLABORADORES --------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Employee_Create
    @EmployeeNumber NVARCHAR(30),
    @FullName       NVARCHAR(150),
    @Email          NVARCHAR(150),
    @Department     NVARCHAR(100) = NULL,
    @IsActive       BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.Employees (EmployeeNumber, FullName, Email, Department, IsActive)
    VALUES (@EmployeeNumber, @FullName, @Email, @Department, @IsActive);
    SELECT SCOPE_IDENTITY() AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Employee_GetById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, EmployeeNumber, FullName, Email, Department, IsActive, CreatedAt FROM dbo.Employees WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Employee_Search
    @Search   NVARCHAR(150) = NULL,
    @IsActive BIT = NULL,
    @Page     INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, EmployeeNumber, FullName, Email, Department, IsActive, CreatedAt, COUNT(*) OVER() AS TotalCount
    FROM dbo.Employees
    WHERE (@IsActive IS NULL OR IsActive = @IsActive)
      AND (@Search IS NULL OR FullName LIKE '%' + @Search + '%' OR EmployeeNumber LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')
    ORDER BY FullName
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Asset_Create
    @AssetCode       NVARCHAR(30),
    @SerialNumber    NVARCHAR(80)  = NULL,
    @Category        NVARCHAR(50),
    @Brand           NVARCHAR(80),
    @Model           NVARCHAR(80),
    @OwnershipType   NVARCHAR(10),
    @SupplierId      INT           = NULL,
    @CurrentLocation NVARCHAR(150),
    @PurchaseDate    DATE          = NULL,
    @RentalEndDate   DATE          = NULL,
    @UserId          INT,
    @Notes           NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SupplierId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Id = @SupplierId)
        THROW 50008, 'SUPPLIER_NOT_FOUND', 1;
    IF @OwnershipType = 'Arrendado' AND @SupplierId IS NULL
        THROW 50009, 'SUPPLIER_REQUIRED', 1;

    BEGIN TRAN;
        INSERT dbo.Assets (AssetCode, SerialNumber, Category, Brand, Model, OwnershipType, SupplierId, Status,
                           CurrentLocation, PurchaseDate, RentalEndDate)
        VALUES (@AssetCode, @SerialNumber, @Category, @Brand, @Model, @OwnershipType, @SupplierId, 'Disponible',
                @CurrentLocation, @PurchaseDate, @RentalEndDate);
        DECLARE @Id INT = SCOPE_IDENTITY();

        INSERT dbo.AssetMovements (AssetId, MovementType, ToStatus, ToLocation, PerformedByUserId, Notes)
        VALUES (@Id, 'Alta', 'Disponible', @CurrentLocation, @UserId, @Notes);
    COMMIT;
    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Asset_Update
    @Id              INT,
    @SerialNumber    NVARCHAR(80)  = NULL,
    @Category        NVARCHAR(50),
    @Brand           NVARCHAR(80),
    @Model           NVARCHAR(80),
    @OwnershipType   NVARCHAR(10),
    @SupplierId      INT           = NULL,
    @Status          NVARCHAR(20),
    @CurrentLocation NVARCHAR(150),
    @PurchaseDate    DATE          = NULL,
    @RentalEndDate   DATE          = NULL,
    @UserId          INT,
    @Notes           NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    DECLARE @OldStatus NVARCHAR(20), @OldLocation NVARCHAR(150);
    SELECT @OldStatus = Status, @OldLocation = CurrentLocation
    FROM dbo.Assets WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id;
    IF @@ROWCOUNT = 0 THROW 50001, 'ASSET_NOT_FOUND', 1;

    IF @SupplierId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Id = @SupplierId)
        THROW 50008, 'SUPPLIER_NOT_FOUND', 1;
    IF @OwnershipType = 'Arrendado' AND @SupplierId IS NULL
        THROW 50009, 'SUPPLIER_REQUIRED', 1;

    IF @Status <> @OldStatus AND (@Status = 'Asignado' OR @OldStatus IN ('Asignado', 'Retirado'))
        THROW 50007, 'INVALID_STATUS_TRANSITION', 1;

    UPDATE dbo.Assets
    SET SerialNumber = @SerialNumber, Category = @Category, Brand = @Brand, Model = @Model,
        OwnershipType = @OwnershipType, SupplierId = @SupplierId, Status = @Status,
        CurrentLocation = @CurrentLocation, PurchaseDate = @PurchaseDate, RentalEndDate = @RentalEndDate,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Id = @Id;

    IF @Status <> @OldStatus
        INSERT dbo.AssetMovements (AssetId, MovementType, FromStatus, ToStatus, PerformedByUserId, Notes)
        VALUES (@Id, 'CambioEstado', @OldStatus, @Status, @UserId, @Notes);

    IF @CurrentLocation <> @OldLocation
        INSERT dbo.AssetMovements (AssetId, MovementType, FromLocation, ToLocation, PerformedByUserId, Notes)
        VALUES (@Id, 'CambioUbicacion', @OldLocation, @CurrentLocation, @UserId, @Notes);

    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Asset_GetById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.Id, a.AssetCode, a.SerialNumber, a.Category, a.Brand, a.Model, a.OwnershipType, a.SupplierId,
           s.Name AS SupplierName, a.Status, a.CurrentLocation, a.PurchaseDate, a.RentalEndDate, a.CreatedAt, a.UpdatedAt,
           e.Id AS AssignedEmployeeId, e.FullName AS AssignedEmployeeName
    FROM dbo.Assets a
    LEFT JOIN dbo.Suppliers s    ON s.Id = a.SupplierId
    LEFT JOIN dbo.Assignments g  ON g.AssetId = a.Id AND g.ReturnedAt IS NULL
    LEFT JOIN dbo.Employees e    ON e.Id = g.EmployeeId
    WHERE a.Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Asset_Search
    @Search   NVARCHAR(100) = NULL,
    @Status   NVARCHAR(20)  = NULL,
    @Category NVARCHAR(50)  = NULL,
    @Page     INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.Id, a.AssetCode, a.SerialNumber, a.Category, a.Brand, a.Model, a.OwnershipType, a.SupplierId,
           s.Name AS SupplierName, a.Status, a.CurrentLocation, a.PurchaseDate, a.RentalEndDate, a.CreatedAt, a.UpdatedAt,
           e.Id AS AssignedEmployeeId, e.FullName AS AssignedEmployeeName,
           COUNT(*) OVER() AS TotalCount
    FROM dbo.Assets a
    LEFT JOIN dbo.Suppliers s    ON s.Id = a.SupplierId
    LEFT JOIN dbo.Assignments g  ON g.AssetId = a.Id AND g.ReturnedAt IS NULL
    LEFT JOIN dbo.Employees e    ON e.Id = g.EmployeeId
    WHERE (@Status   IS NULL OR a.Status = @Status)
      AND (@Category IS NULL OR a.Category = @Category)
      AND (@Search   IS NULL OR a.AssetCode    LIKE '%' + @Search + '%'
                             OR a.SerialNumber LIKE '%' + @Search + '%'
                             OR a.Brand        LIKE '%' + @Search + '%'
                             OR a.Model        LIKE '%' + @Search + '%')
    ORDER BY a.Id DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Asset_GetHistory
    @AssetId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Assets WHERE Id = @AssetId) THROW 50001, 'ASSET_NOT_FOUND', 1;

    SELECT m.Id, m.AssetId, m.MovementType, m.FromStatus, m.ToStatus, m.FromLocation, m.ToLocation,
           m.EmployeeId, e.FullName AS EmployeeName, m.AssignmentId,
           m.PerformedByUserId, u.Username AS PerformedBy, m.PerformedAt, m.Notes
    FROM dbo.AssetMovements m
    JOIN dbo.Users u          ON u.Id = m.PerformedByUserId
    LEFT JOIN dbo.Employees e ON e.Id = m.EmployeeId
    WHERE m.AssetId = @AssetId
    ORDER BY m.PerformedAt DESC, m.Id DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Asset_Assign
    @AssetId    INT,
    @EmployeeId INT,
    @UserId     INT,
    @Notes      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20), @Location NVARCHAR(150);
    SELECT @Status = Status, @Location = CurrentLocation
    FROM dbo.Assets WITH (UPDLOCK, HOLDLOCK) WHERE Id = @AssetId;
    IF @@ROWCOUNT = 0 THROW 50001, 'ASSET_NOT_FOUND', 1;

    DECLARE @EmployeeActive BIT;
    SELECT @EmployeeActive = IsActive FROM dbo.Employees WHERE Id = @EmployeeId;
    IF @@ROWCOUNT = 0 THROW 50002, 'EMPLOYEE_NOT_FOUND', 1;
    IF @EmployeeActive = 0 THROW 50004, 'EMPLOYEE_INACTIVE', 1;

    IF EXISTS (SELECT 1 FROM dbo.Assignments WHERE AssetId = @AssetId AND ReturnedAt IS NULL)
        THROW 50006, 'ASSET_ALREADY_ASSIGNED', 1;
    IF @Status <> 'Disponible'
        THROW 50003, 'ASSET_NOT_AVAILABLE', 1;

    INSERT dbo.Assignments (AssetId, EmployeeId, AssignedByUserId, AssignNotes)
    VALUES (@AssetId, @EmployeeId, @UserId, @Notes);
    DECLARE @AssignmentId INT = SCOPE_IDENTITY();

    UPDATE dbo.Assets SET Status = 'Asignado', UpdatedAt = SYSUTCDATETIME() WHERE Id = @AssetId;

    INSERT dbo.AssetMovements (AssetId, MovementType, FromStatus, ToStatus, EmployeeId, AssignmentId, PerformedByUserId, Notes)
    VALUES (@AssetId, 'Asignacion', @Status, 'Asignado', @EmployeeId, @AssignmentId, @UserId, @Notes);

    COMMIT;
    SELECT @AssignmentId AS AssignmentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Asset_Return
    @AssetId   INT,
    @Condition NVARCHAR(20),
    @UserId    INT,
    @Notes     NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM dbo.Assets WITH (UPDLOCK, HOLDLOCK) WHERE Id = @AssetId;
    IF @@ROWCOUNT = 0 THROW 50001, 'ASSET_NOT_FOUND', 1;

    DECLARE @AssignmentId INT, @EmployeeId INT;
    SELECT @AssignmentId = Id, @EmployeeId = EmployeeId
    FROM dbo.Assignments WITH (UPDLOCK) WHERE AssetId = @AssetId AND ReturnedAt IS NULL;
    IF @AssignmentId IS NULL THROW 50005, 'NO_ACTIVE_ASSIGNMENT', 1;

    UPDATE dbo.Assignments
    SET ReturnedAt = SYSUTCDATETIME(), ReturnedByUserId = @UserId, ReturnCondition = @Condition, ReturnNotes = @Notes
    WHERE Id = @AssignmentId;

    UPDATE dbo.Assets SET Status = 'Disponible', UpdatedAt = SYSUTCDATETIME() WHERE Id = @AssetId;

    INSERT dbo.AssetMovements (AssetId, MovementType, FromStatus, ToStatus, EmployeeId, AssignmentId, PerformedByUserId, Notes)
    VALUES (@AssetId, 'Devolucion', @Status, 'Disponible', @EmployeeId, @AssignmentId, @UserId,
            CONCAT('Condicion: ', @Condition, CASE WHEN @Notes IS NULL THEN '' ELSE CONCAT('. ', @Notes) END));

    COMMIT;
    SELECT @AssignmentId AS AssignmentId;
END
GO
