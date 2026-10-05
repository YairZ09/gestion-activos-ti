using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Api.Models;

public static class Allowed
{
    public static readonly string[] Statuses = ["Disponible", "Asignado", "Mantenimiento", "Retirado"];
    public static readonly string[] Ownership = ["Propio", "Arrendado"];
    public static readonly string[] ServiceTypes = ["Compra", "Mantenimiento", "Arrendamiento"];
    public static readonly string[] ReturnConditions = ["Bueno", "Regular", "Danado"];

    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$";

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class LoginRequest
{
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}

public abstract class AssetRequestBase : IValidatableObject
{
    [StringLength(80)] public string? SerialNumber { get; set; }
    [Required, StringLength(50)] public string Category { get; set; } = "";
    [Required, StringLength(80)] public string Brand { get; set; } = "";
    [Required, StringLength(80)] public string Model { get; set; } = "";
    [Required, AllowedValues("Propio", "Arrendado", ErrorMessage = "OwnershipType debe ser Propio o Arrendado.")]
    public string OwnershipType { get; set; } = "";
    [Range(1, int.MaxValue)] public int? SupplierId { get; set; }
    [Required, StringLength(150)] public string CurrentLocation { get; set; } = "";
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? RentalEndDate { get; set; }
    [StringLength(500)] public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (OwnershipType == "Arrendado" && SupplierId is null)
            yield return new ValidationResult("Un activo arrendado debe tener proveedor (SupplierId).", [nameof(SupplierId)]);
        if (RentalEndDate is not null && OwnershipType == "Propio")
            yield return new ValidationResult("RentalEndDate solo aplica a activos arrendados.", [nameof(RentalEndDate)]);
        if (PurchaseDate is not null && RentalEndDate is not null && RentalEndDate < PurchaseDate)
            yield return new ValidationResult("RentalEndDate no puede ser anterior a PurchaseDate.", [nameof(RentalEndDate)]);
    }
}

public sealed class CreateAssetRequest : AssetRequestBase
{
    [Required, StringLength(30, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9\-_]*$", ErrorMessage = "AssetCode solo admite letras, numeros, guion y guion bajo.")]
    public string AssetCode { get; set; } = "";
}

public sealed class UpdateAssetRequest : AssetRequestBase
{
    [Required, AllowedValues("Disponible", "Asignado", "Mantenimiento", "Retirado", ErrorMessage = "Status debe ser Disponible, Asignado, Mantenimiento o Retirado.")]
    public string Status { get; set; } = "";
}

public sealed class AssignRequest
{
    [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class ReturnRequest
{
    [Required, AllowedValues("Bueno", "Regular", "Danado", ErrorMessage = "Condition debe ser Bueno, Regular o Danado.")]
    public string Condition { get; set; } = "";
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class CreateEmployeeRequest
{
    [Required, StringLength(30), RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9\-_]*$", ErrorMessage = "EmployeeNumber solo admite letras, numeros, guion y guion bajo.")]
    public string EmployeeNumber { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, StringLength(150), RegularExpression(Allowed.EmailPattern, ErrorMessage = "El correo electronico no tiene un formato valido.")]
    public string Email { get; set; } = "";
    [StringLength(100)] public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CreateSupplierRequest : IValidatableObject
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(30)] public string? TaxId { get; set; }
    [StringLength(150), RegularExpression(Allowed.EmailPattern, ErrorMessage = "El correo electronico no tiene un formato valido.")]
    public string? Email { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [Required, MinLength(1, ErrorMessage = "Indique al menos un tipo de servicio.")]
    public List<string> Services { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var invalid = Services.Where(s => !Allowed.ServiceTypes.Contains(s)).ToList();
        if (invalid.Count > 0)
            yield return new ValidationResult("Servicios permitidos: Compra, Mantenimiento, Arrendamiento.", [nameof(Services)]);
    }
}

public abstract class PagedQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class AssetQuery : PagedQuery
{
    [StringLength(100)] public string? Search { get; set; }
    [AllowedValues(null, "Disponible", "Asignado", "Mantenimiento", "Retirado", ErrorMessage = "Status invalido.")]
    public string? Status { get; set; }
    [StringLength(50)] public string? Category { get; set; }
}

public sealed class EmployeeQuery : PagedQuery
{
    [StringLength(150)] public string? Search { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class SupplierQuery : PagedQuery
{
    [StringLength(150)] public string? Search { get; set; }
}
