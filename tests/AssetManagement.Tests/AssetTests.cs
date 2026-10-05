using System.Net;
using System.Net.Http.Json;
using static AssetManagement.Tests.TestHelpers;

namespace AssetManagement.Tests;

[Collection("api")]
public sealed class AssetTests(ApiFixture api)
{
    [Fact]
    public async Task Create_asset_sets_status_Disponible_and_records_Alta_movement()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);

        Assert.Equal("Disponible", asset.Status);
        var history = await (await admin.GetAsync($"/api/assets/{asset.Id}/history")).ReadAsync<List<Movement>>();
        var alta = Assert.Single(history);
        Assert.Equal("Alta", alta.MovementType);
        Assert.Equal("admin", alta.PerformedBy);
    }

    [Fact]
    public async Task Duplicate_asset_code_returns_409()
    {
        var admin = await api.AdminAsync();
        var first = await CreateAssetAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = first.AssetCode, category = "Laptop", brand = "X", model = "Y", ownershipType = "Propio", currentLocation = "A"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ASSET_CODE_DUPLICATE", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Duplicate_serial_number_returns_409_but_missing_serials_are_allowed()
    {
        var admin = await api.AdminAsync();
        var first = await CreateAssetAsync(admin);

        var dup = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = Unique("TI"), serialNumber = first.SerialNumber, category = "Laptop", brand = "X", model = "Y", ownershipType = "Propio", currentLocation = "A"
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("SERIAL_NUMBER_DUPLICATE", await dup.ErrorCodeAsync());

        for (var i = 0; i < 2; i++)
        {
            var noSerial = await admin.PostAsJsonAsync("/api/assets", new
            {
                assetCode = Unique("TI"), category = "Periferico", brand = "X", model = "Y", ownershipType = "Propio", currentLocation = "A"
            });
            Assert.Equal(HttpStatusCode.Created, noSerial.StatusCode);
        }
    }

    [Fact]
    public async Task Rented_asset_requires_supplier()
    {
        var admin = await api.AdminAsync();
        var body = new { assetCode = Unique("TI"), category = "Monitor", brand = "LG", model = "27", ownershipType = "Arrendado", currentLocation = "A" };

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/assets", body)).StatusCode);

        var unknownSupplier = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = Unique("TI"), category = "Monitor", brand = "LG", model = "27", ownershipType = "Arrendado", supplierId = 999999, currentLocation = "A"
        });
        Assert.Equal(HttpStatusCode.NotFound, unknownSupplier.StatusCode);
        Assert.Equal("SUPPLIER_NOT_FOUND", await unknownSupplier.ErrorCodeAsync());

        var supplier = await CreateSupplierAsync(admin, "Arrendamiento");
        var ok = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = Unique("TI"), category = "Monitor", brand = "LG", model = "27", ownershipType = "Arrendado",
            supplierId = supplier.Id, rentalEndDate = "2027-12-31", currentLocation = "A"
        });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task Server_side_validation_rejects_invalid_payloads()
    {
        var admin = await api.AdminAsync();

        var missing = await admin.PostAsJsonAsync("/api/assets", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var badOwnership = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = Unique("TI"), category = "Laptop", brand = "X", model = "Y", ownershipType = "Prestado", currentLocation = "A"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badOwnership.StatusCode);

        var tooLong = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = Unique("TI"), category = new string('x', 51), brand = "X", model = "Y", ownershipType = "Propio", currentLocation = "A"
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var badCode = await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = "TI 001; DROP TABLE Assets", category = "Laptop", brand = "X", model = "Y", ownershipType = "Propio", currentLocation = "A"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/assets?status=Inventado")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/assets?pageSize=1000")).StatusCode);
    }

    [Fact]
    public async Task Search_filters_by_text_status_and_category_with_paging()
    {
        var admin = await api.AdminAsync();
        var brand = Unique("Marca");
        for (var i = 0; i < 3; i++) await CreateAssetAsync(admin, category: "Laptop", brand: brand);
        var monitor = await CreateAssetAsync(admin, category: "Monitor", brand: brand);

        var all = await (await admin.GetAsync($"/api/assets?search={brand}")).ReadAsync<Paged<Asset>>();
        Assert.Equal(4, all.TotalCount);

        var laptops = await (await admin.GetAsync($"/api/assets?search={brand}&category=Laptop")).ReadAsync<Paged<Asset>>();
        Assert.Equal(3, laptops.TotalCount);
        Assert.All(laptops.Items, a => Assert.Equal("Laptop", a.Category));

        var page = await (await admin.GetAsync($"/api/assets?search={brand}&page=2&pageSize=3")).ReadAsync<Paged<Asset>>();
        Assert.Single(page.Items);
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(2, page.TotalPages);

        await ChangeStatusAsync(admin, monitor, "Mantenimiento");
        var inMaintenance = await (await admin.GetAsync($"/api/assets?search={brand}&status=Mantenimiento")).ReadAsync<Paged<Asset>>();
        Assert.Equal(monitor.Id, Assert.Single(inMaintenance.Items).Id);
    }

    [Fact]
    public async Task Update_records_status_and_location_changes_in_history()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin, location: "Almacen");

        var response = await admin.PutAsJsonAsync($"/api/assets/{asset.Id}", new
        {
            serialNumber = asset.SerialNumber, category = asset.Category, brand = asset.Brand, model = "Latitude", ownershipType = "Propio",
            status = "Mantenimiento", currentLocation = "Taller", notes = "Falla de bateria"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await (await admin.GetAsync($"/api/assets/{asset.Id}/history")).ReadAsync<List<Movement>>();
        Assert.Contains(history, m => m.MovementType == "CambioEstado" && m.FromStatus == "Disponible" && m.ToStatus == "Mantenimiento" && m.Notes == "Falla de bateria");
        Assert.Contains(history, m => m.MovementType == "CambioUbicacion" && m.ToLocation == "Taller");
        Assert.All(history, m => Assert.Equal("admin", m.PerformedBy));
    }

    [Fact]
    public async Task Invalid_status_transitions_are_rejected_and_retired_is_final()
    {
        var admin = await api.AdminAsync();
        var asset = await CreateAssetAsync(admin);

        var manual = await ChangeStatusAsync(admin, asset, "Asignado");
        Assert.Equal(HttpStatusCode.Conflict, manual.StatusCode);
        Assert.Equal("INVALID_STATUS_TRANSITION", await manual.ErrorCodeAsync());

        Assert.Equal(HttpStatusCode.OK, (await ChangeStatusAsync(admin, asset, "Retirado")).StatusCode);
        var back = await ChangeStatusAsync(admin, asset, "Disponible");
        Assert.Equal(HttpStatusCode.Conflict, back.StatusCode);
        Assert.Equal("INVALID_STATUS_TRANSITION", await back.ErrorCodeAsync());
    }

    [Fact]
    public async Task Unknown_asset_returns_404()
    {
        var admin = await api.AdminAsync();
        var response = await admin.GetAsync("/api/assets/99999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ASSET_NOT_FOUND", await response.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/assets/99999999/history")).StatusCode);
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient admin, Asset asset, string status) =>
        admin.PutAsJsonAsync($"/api/assets/{asset.Id}", new
        {
            serialNumber = asset.SerialNumber, category = asset.Category, brand = asset.Brand, model = "Latitude", ownershipType = "Propio",
            status, currentLocation = asset.CurrentLocation
        });
}
