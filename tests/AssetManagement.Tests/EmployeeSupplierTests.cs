using System.Net;
using System.Net.Http.Json;
using static AssetManagement.Tests.TestHelpers;

namespace AssetManagement.Tests;

[Collection("api")]
public sealed class EmployeeSupplierTests(ApiFixture api)
{
    [Fact]
    public async Task Create_and_get_employee()
    {
        var admin = await api.AdminAsync();
        var created = await CreateEmployeeAsync(admin);

        var fetched = await (await admin.GetAsync($"/api/employees/{created.Id}")).ReadAsync<Employee>();
        Assert.Equal(created.EmployeeNumber, fetched.EmployeeNumber);
        Assert.True(fetched.IsActive);
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@b")]
    [InlineData("@empresa.com")]
    [InlineData("con espacio@empresa.com")]
    public async Task Employee_with_invalid_email_is_rejected(string email)
    {
        var admin = await api.AdminAsync();
        var response = await admin.PostAsJsonAsync("/api/employees", new { employeeNumber = Unique("E"), fullName = "Alguien", email });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_employee_number_returns_409()
    {
        var admin = await api.AdminAsync();
        var first = await CreateEmployeeAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/employees", new { employeeNumber = first.EmployeeNumber, fullName = "Otro", email = "otro@empresa.example" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EMPLOYEE_NUMBER_DUPLICATE", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Employee_search_filters_by_active_flag()
    {
        var admin = await api.AdminAsync();
        var inactive = await CreateEmployeeAsync(admin, active: false);

        var page = await (await admin.GetAsync($"/api/employees?search={inactive.EmployeeNumber}&isActive=false")).ReadAsync<Paged<Employee>>();
        Assert.Equal(inactive.Id, Assert.Single(page.Items).Id);

        var none = await (await admin.GetAsync($"/api/employees?search={inactive.EmployeeNumber}&isActive=true")).ReadAsync<Paged<Employee>>();
        Assert.Empty(none.Items);
    }

    [Fact]
    public async Task Create_supplier_with_services_and_reject_invalid_ones()
    {
        var admin = await api.AdminAsync();
        var supplier = await CreateSupplierAsync(admin, "Compra", "Mantenimiento");
        Assert.Equal(["Compra", "Mantenimiento"], supplier.Services.OrderBy(s => s));

        var invalid = await admin.PostAsJsonAsync("/api/suppliers", new { name = Unique("Prov"), services = new[] { "Venta" } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var empty = await admin.PostAsJsonAsync("/api/suppliers", new { name = Unique("Prov"), services = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Duplicate_supplier_name_returns_409()
    {
        var admin = await api.AdminAsync();
        var supplier = await CreateSupplierAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/suppliers", new { name = supplier.Name, services = new[] { "Compra" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SUPPLIER_NAME_DUPLICATE", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Unknown_employee_or_supplier_returns_404()
    {
        var admin = await api.AdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/employees/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/suppliers/99999999")).StatusCode);
    }
}
