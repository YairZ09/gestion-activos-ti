namespace AssetManagement.Api.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record AssetDto(
    int Id, string AssetCode, string? SerialNumber, string Category, string Brand, string Model,
    string OwnershipType, int? SupplierId, string? SupplierName, string Status, string CurrentLocation,
    DateOnly? PurchaseDate, DateOnly? RentalEndDate, DateTime CreatedAt, DateTime UpdatedAt,
    int? AssignedEmployeeId, string? AssignedEmployeeName);

public sealed record AssetMovementDto(
    int Id, int AssetId, string MovementType, string? FromStatus, string? ToStatus,
    string? FromLocation, string? ToLocation, int? EmployeeId, string? EmployeeName, int? AssignmentId,
    int PerformedByUserId, string PerformedBy, DateTime PerformedAt, string? Notes);

public sealed record AssignmentResultDto(int AssignmentId, AssetDto Asset);

public sealed record EmployeeDto(int Id, string EmployeeNumber, string FullName, string Email, string? Department, bool IsActive, DateTime CreatedAt);

public sealed record SupplierDto(int Id, string Name, string? TaxId, string? Email, string? Phone, bool IsActive, DateTime CreatedAt, IReadOnlyList<string> Services);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, string Username, string Role);

public sealed record AuthUser(int Id, string Username, string PasswordHash, string Role, bool IsActive, bool IsLockedOut);
