using System.Net.Http.Json;
using System.Text.Json;

namespace AssetManagement.Tests;

public sealed record Asset(int Id, string AssetCode, string? SerialNumber, string Category, string Brand, string Status,
    string CurrentLocation, int? AssignedEmployeeId);
public sealed record Employee(int Id, string EmployeeNumber, bool IsActive);
public sealed record Supplier(int Id, string Name, string[] Services);
public sealed record Movement(string MovementType, string? FromStatus, string? ToStatus, string? ToLocation, string PerformedBy, string? EmployeeName, string? Notes);
public sealed record Paged<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record AssignmentResult(int AssignmentId, Asset Asset);

public static class TestHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Unique(string prefix = "T") => $"{prefix}-{Guid.NewGuid():N}"[..14];

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static async Task<Asset> CreateAssetAsync(HttpClient admin, string? category = null, string? brand = null,
        string? serial = null, string location = "Almacen central")
    {
        var response = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = Unique("TI"),
            serialNumber = serial ?? Unique("SN"),
            category = category ?? "Laptop",
            brand = brand ?? "DELL",
            model = "Latitude",
            ownershipType = "Propio",
            currentLocation = location
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<Asset>();
    }

    public static async Task<Employee> CreateEmployeeAsync(HttpClient admin, bool active = true)
    {
        var number = Unique("E");
        var response = await admin.PostAsJsonAsync("/api/employees", new
        {
            employeeNumber = number,
            fullName = "Colaborador " + number,
            email = $"{number.ToLowerInvariant()}@empresa.example",
            department = "Sistemas",
            isActive = active
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<Employee>();
    }

    public static async Task<Supplier> CreateSupplierAsync(HttpClient admin, params string[] services)
    {
        var response = await admin.PostAsJsonAsync("/api/suppliers", new { name = Unique("Prov"), services = services.Length == 0 ? ["Compra"] : services });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<Supplier>();
    }

    public static Task<HttpResponseMessage> AssignAsync(HttpClient client, int assetId, int employeeId, string? notes = null) =>
        client.PostAsJsonAsync($"/api/assets/{assetId}/assign", new { employeeId, notes });

    public static Task<HttpResponseMessage> ReturnAsync(HttpClient client, int assetId, string condition = "Bueno", string? notes = null) =>
        client.PostAsJsonAsync($"/api/assets/{assetId}/return", new { condition, notes });
}
