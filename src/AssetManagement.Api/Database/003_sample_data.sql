DECLARE @Admin INT = (SELECT Id FROM dbo.Users WHERE Username = 'admin');

IF @Admin IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Name = 'Dell Mexico')
        EXEC dbo.usp_Supplier_Create @Name = 'Dell Mexico', @TaxId = 'DEL010101AAA', @Email = 'ventas@dell.example', @Phone = '5550001', @Services = 'Compra,Mantenimiento';
    IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Name = 'Leasing TI SA')
        EXEC dbo.usp_Supplier_Create @Name = 'Leasing TI SA', @TaxId = 'LTI020202BBB', @Email = 'contacto@leasingti.example', @Phone = '5550002', @Services = 'Arrendamiento,Mantenimiento';

    IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeNumber = 'E-0001')
        EXEC dbo.usp_Employee_Create @EmployeeNumber = 'E-0001', @FullName = 'Ana Lopez',   @Email = 'ana.lopez@empresa.example',   @Department = 'Finanzas', @IsActive = 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeNumber = 'E-0002')
        EXEC dbo.usp_Employee_Create @EmployeeNumber = 'E-0002', @FullName = 'Luis Perez',  @Email = 'luis.perez@empresa.example',  @Department = 'Sistemas', @IsActive = 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeNumber = 'E-0003')
        EXEC dbo.usp_Employee_Create @EmployeeNumber = 'E-0003', @FullName = 'Marta Ruiz (baja)', @Email = 'marta.ruiz@empresa.example', @Department = 'RRHH', @IsActive = 0;

    DECLARE @Dell INT = (SELECT Id FROM dbo.Suppliers WHERE Name = 'Dell Mexico');
    DECLARE @Leasing INT = (SELECT Id FROM dbo.Suppliers WHERE Name = 'Leasing TI SA');

    IF NOT EXISTS (SELECT 1 FROM dbo.Assets WHERE AssetCode = 'TI-0001001')
        EXEC dbo.usp_Asset_Create @AssetCode = 'TI-0001001', @SerialNumber = 'SN-DELL-0001', @Category = 'Laptop', @Brand = 'DELL', @Model = 'Latitude 5440',
             @OwnershipType = 'Propio', @SupplierId = @Dell, @CurrentLocation = 'Almacen central', @PurchaseDate = '2025-03-01', @UserId = @Admin, @Notes = 'Carga inicial';
    IF NOT EXISTS (SELECT 1 FROM dbo.Assets WHERE AssetCode = 'TI-0001002')
        EXEC dbo.usp_Asset_Create @AssetCode = 'TI-0001002', @SerialNumber = 'SN-DELL-0002', @Category = 'Laptop', @Brand = 'DELL', @Model = 'Latitude 5440',
             @OwnershipType = 'Propio', @SupplierId = @Dell, @CurrentLocation = 'Almacen central', @PurchaseDate = '2025-03-01', @UserId = @Admin, @Notes = 'Carga inicial';
    IF NOT EXISTS (SELECT 1 FROM dbo.Assets WHERE AssetCode = 'TI-0002001')
        EXEC dbo.usp_Asset_Create @AssetCode = 'TI-0002001', @SerialNumber = 'SN-LG-0001', @Category = 'Monitor', @Brand = 'LG', @Model = '27UL500',
             @OwnershipType = 'Arrendado', @SupplierId = @Leasing, @CurrentLocation = 'Oficina 2', @RentalEndDate = '2027-12-31', @UserId = @Admin, @Notes = 'Carga inicial';
    IF NOT EXISTS (SELECT 1 FROM dbo.Assets WHERE AssetCode = 'TI-0003001')
        EXEC dbo.usp_Asset_Create @AssetCode = 'TI-0003001', @SerialNumber = NULL, @Category = 'Periferico', @Brand = 'Logitech', @Model = 'MX Keys',
             @OwnershipType = 'Propio', @SupplierId = NULL, @CurrentLocation = 'Almacen central', @UserId = @Admin, @Notes = 'Carga inicial';
END
GO
