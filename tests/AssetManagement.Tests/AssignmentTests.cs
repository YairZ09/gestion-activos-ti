using System.Net;
using System.Net.Http.Json;
using static AssetManagement.Tests.TestHelpers;

namespace AssetManagement.Tests;

[Collection("api")]
public sealed class AssignmentTests(ApiFixture api)
{
    [Fact]
    public async Task Assign_then_return_updates_status_and_writes_full_audit_trail()
    {
        var admin = await api.AdminAsync();
        var op = await api.OperatorAsync();
        var asset = await CreateAssetAsync(admin);
        var employee = await CreateEmployeeAsync(admin);

        var assign = await AssignAsync(op, asset.Id, employee.Id, "Entrega inicial");
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
        var assigned = await assign.ReadAsync<AssignmentResult>();
        Assert.Equal("Asignado", assigned.Asset.Status);
        Assert.Equal(employee.Id, assigned.Asset.AssignedEmployeeId);

        var ret = await ReturnAsync(op, asset.Id, "Regular", "Con rayones");
        Assert.Equal(HttpStatusCode.OK, ret.StatusCode);
        var returned = await ret.ReadAsync<AssignmentResult>();
        Assert.Equal(assigned.AssignmentId, returned.AssignmentId);
        Assert.Equal("Disponible", returned.Asset.Status);
        Assert.Null(returned.Asset.AssignedEmployeeId);

        var history = await (await admin.GetAsync($"/api/assets/{asset.Id}/history")).ReadAsync<List<Movement>>();
        Assert.Equal(["Devolucion", "Asignacion", "Alta"], history.Select(m => m.MovementType));
        Assert.Equal("operador", history[0].PerformedBy);
        Assert.Equal("operador", history[1].PerformedBy);
        Assert.Contains("Regular", history[0].Notes);
        Assert.Equal("Colaborador " + employee.EmployeeNumber, history[1].EmployeeName);
    }

    [Fact]
    public async Task Asset_can_be_reassigned_after_return()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);
        var a = await CreateEmployeeAsync(admin);
        var b = await CreateEmployeeAsync(admin);

        Assert.Equal(HttpStatusCode.Created, (await AssignAsync(admin, asset.Id, a.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReturnAsync(admin, asset.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AssignAsync(admin, asset.Id, b.Id)).StatusCode);
    }

    [Fact]
    public async Task Assigning_an_already_assigned_asset_returns_409()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);
        var a = await CreateEmployeeAsync(admin);
        var b = await CreateEmployeeAsync(admin);

        Assert.Equal(HttpStatusCode.Created, (await AssignAsync(admin, asset.Id, a.Id)).StatusCode);
        var second = await AssignAsync(admin, asset.Id, b.Id);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("ASSET_ALREADY_ASSIGNED", await second.ErrorCodeAsync());
    }

    [Fact]
    public async Task Inactive_employee_cannot_receive_assets()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);
        var inactive = await CreateEmployeeAsync(admin, active: false);

        var response = await AssignAsync(admin, asset.Id, inactive.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EMPLOYEE_INACTIVE", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Unknown_asset_or_employee_returns_404()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);
        var employee = await CreateEmployeeAsync(admin);

        var noAsset = await AssignAsync(admin, 99999999, employee.Id);
        Assert.Equal(HttpStatusCode.NotFound, noAsset.StatusCode);
        Assert.Equal("ASSET_NOT_FOUND", await noAsset.ErrorCodeAsync());

        var noEmployee = await AssignAsync(admin, asset.Id, 99999999);
        Assert.Equal(HttpStatusCode.NotFound, noEmployee.StatusCode);
        Assert.Equal("EMPLOYEE_NOT_FOUND", await noEmployee.ErrorCodeAsync());
    }

    [Theory]
    [InlineData("Mantenimiento")]
    [InlineData("Retirado")]
    public async Task Asset_in_maintenance_or_retired_cannot_be_assigned(string status)
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);
        var employee = await CreateEmployeeAsync(admin);
        await admin.PutAsJsonAsyncStatus(asset, status);

        var response = await AssignAsync(admin, asset.Id, employee.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ASSET_NOT_AVAILABLE", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Return_without_active_assignment_returns_409()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);

        var response = await ReturnAsync(admin, asset.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NO_ACTIVE_ASSIGNMENT", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Return_validates_condition()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);
        var employee = await CreateEmployeeAsync(admin);
        await AssignAsync(admin, asset.Id, employee.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await ReturnAsync(admin, asset.Id, "Roto")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReturnAsync(admin, asset.Id, "")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_assignments_of_the_same_asset_only_one_succeeds()
    {
        const int contenders = 10;
        var admin = await api.AdminAsync();
        var op = await api.OperatorAsync();
        var asset = await CreateAssetAsync(admin);
        var employees = new List<Employee>();
        for (var i = 0; i < contenders; i++) employees.Add(await CreateEmployeeAsync(admin));

        using var gate = new ManualResetEventSlim(false);
        var tasks = employees.Select((e, i) => Task.Run(() =>
        {
            gate.Wait();
            return AssignAsync(i % 2 == 0 ? admin : op, asset.Id, e.Id);
        })).ToList();
        gate.Set();
        var responses = await Task.WhenAll(tasks);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(contenders - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        foreach (var loser in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal("ASSET_ALREADY_ASSIGNED", await loser.ErrorCodeAsync());

        var history = await (await admin.GetAsync($"/api/assets/{asset.Id}/history")).ReadAsync<List<Movement>>();
        Assert.Single(history, m => m.MovementType == "Asignacion");
        var current = await (await admin.GetAsync($"/api/assets/{asset.Id}")).ReadAsync<Asset>();
        Assert.Equal("Asignado", current.Status);
    }

    [Fact]
    public async Task Concurrent_assign_and_return_cycles_keep_a_consistent_state()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);
        var employees = new List<Employee>();
        for (var i = 0; i < 10; i++) employees.Add(await CreateEmployeeAsync(admin));
        await AssignAsync(admin, asset.Id, employees[0].Id);

        var returns = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => ReturnAsync(admin, asset.Id))));

        Assert.Equal(1, returns.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, returns.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}

internal static class AssignmentTestExtensions
{
    public static async Task PutAsJsonAsyncStatus(this HttpClient admin, Asset asset, string status)
    {
        var response = await admin.PutAsJsonAsync($"/api/assets/{asset.Id}", new
        {
            serialNumber = asset.SerialNumber, category = asset.Category, brand = asset.Brand, model = "Latitude", ownershipType = "Propio",
            status, currentLocation = asset.CurrentLocation
        });
        response.EnsureSuccessStatusCode();
    }
}
