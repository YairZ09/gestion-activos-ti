using Microsoft.Data.SqlClient;

namespace AssetManagement.Api.Errors;

public static class SqlErrorTranslator
{
    private static readonly Dictionary<int, BusinessException> Custom = new()
    {
        [50001] = new(404, "ASSET_NOT_FOUND", "El activo no existe."),
        [50002] = new(404, "EMPLOYEE_NOT_FOUND", "El colaborador no existe."),
        [50003] = new(409, "ASSET_NOT_AVAILABLE", "El activo no esta disponible (debe estar en estado Disponible)."),
        [50004] = new(409, "EMPLOYEE_INACTIVE", "El colaborador esta inactivo y no puede recibir activos."),
        [50005] = new(409, "NO_ACTIVE_ASSIGNMENT", "El activo no tiene una asignacion activa."),
        [50006] = new(409, "ASSET_ALREADY_ASSIGNED", "El activo ya tiene una asignacion activa."),
        [50007] = new(409, "INVALID_STATUS_TRANSITION", "Transicion de estado no permitida para el activo."),
        [50008] = new(404, "SUPPLIER_NOT_FOUND", "El proveedor no existe."),
        [50009] = new(400, "SUPPLIER_REQUIRED", "Un activo arrendado debe tener proveedor asociado."),
    };

    private static readonly (string Index, BusinessException Error)[] UniqueIndexes =
    [
        ("UX_Assets_AssetCode", new(409, "ASSET_CODE_DUPLICATE", "Ya existe un activo con ese AssetCode.")),
        ("UX_Assets_SerialNumber", new(409, "SERIAL_NUMBER_DUPLICATE", "Ya existe un activo con ese numero de serie.")),
        ("UX_Employees_EmployeeNumber", new(409, "EMPLOYEE_NUMBER_DUPLICATE", "Ya existe un colaborador con ese numero de empleado.")),
        ("UX_Suppliers_Name", new(409, "SUPPLIER_NAME_DUPLICATE", "Ya existe un proveedor con ese nombre.")),
        ("UX_Assignments_ActiveAsset", new(409, "ASSET_ALREADY_ASSIGNED", "El activo ya tiene una asignacion activa.")),
    ];

    public static BusinessException? Translate(SqlException ex)
    {
        foreach (SqlError error in ex.Errors)
            if (Translate(error.Number, error.Message) is { } translated) return translated;
        return null;
    }

    public static BusinessException? Translate(int number, string message)
    {
        if (Custom.TryGetValue(number, out var custom)) return custom;

        switch (number)
        {
            case 2601 or 2627:
                foreach (var (index, mapped) in UniqueIndexes)
                    if (message.Contains(index, StringComparison.OrdinalIgnoreCase)) return mapped;
                return new BusinessException(409, "DUPLICATE_VALUE", "Ya existe un registro con esos datos.");
            case 547:
                return new BusinessException(400, "CONSTRAINT_VIOLATION", "Los datos enviados no cumplen las reglas de integridad.");
            case 2628 or 8152:
                return new BusinessException(400, "VALUE_TOO_LONG", "Alguno de los valores excede la longitud permitida.");
            case 1205:
                return new BusinessException(409, "CONCURRENCY_CONFLICT", "La operacion entro en conflicto con otra concurrente. Intente de nuevo.");
            default:
                return null;
        }
    }
}
