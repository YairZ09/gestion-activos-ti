using AssetManagement.Api.Data;
using AssetManagement.Api.Errors;
using AssetManagement.Api.Models;
using Microsoft.Data.SqlClient;

namespace AssetManagement.Api.Repositories;

public sealed class AssetRepository(SqlDb db)
{
    private static AssetDto MapAsset(SqlDataReader r) => new(
        r.GetInt32(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("AssetCode")), r.GetStringOrNull("SerialNumber"),
        r.GetString(r.GetOrdinal("Category")), r.GetString(r.GetOrdinal("Brand")), r.GetString(r.GetOrdinal("Model")),
        r.GetString(r.GetOrdinal("OwnershipType")), r.GetIntOrNull("SupplierId"), r.GetStringOrNull("SupplierName"),
        r.GetString(r.GetOrdinal("Status")), r.GetString(r.GetOrdinal("CurrentLocation")),
        r.GetDateOrNull("PurchaseDate"), r.GetDateOrNull("RentalEndDate"), r.GetUtc("CreatedAt"), r.GetUtc("UpdatedAt"),
        r.GetIntOrNull("AssignedEmployeeId"), r.GetStringOrNull("AssignedEmployeeName"));

    private static AssetMovementDto MapMovement(SqlDataReader r) => new(
        r.GetInt32(r.GetOrdinal("Id")), r.GetInt32(r.GetOrdinal("AssetId")), r.GetString(r.GetOrdinal("MovementType")),
        r.GetStringOrNull("FromStatus"), r.GetStringOrNull("ToStatus"), r.GetStringOrNull("FromLocation"), r.GetStringOrNull("ToLocation"),
        r.GetIntOrNull("EmployeeId"), r.GetStringOrNull("EmployeeName"), r.GetIntOrNull("AssignmentId"),
        r.GetInt32(r.GetOrdinal("PerformedByUserId")), r.GetString(r.GetOrdinal("PerformedBy")), r.GetUtc("PerformedAt"),
        r.GetStringOrNull("Notes"));

    public Task<AssetDto?> GetByIdAsync(int id, CancellationToken ct) =>
        db.QuerySingleAsync("dbo.usp_Asset_GetById", MapAsset, ct, P.Int("@Id", id));

    public async Task<PagedResult<AssetDto>> SearchAsync(AssetQuery q, CancellationToken ct)
    {
        var total = 0;
        var items = await db.QueryAsync("dbo.usp_Asset_Search", r =>
            {
                total = r.GetInt32(r.GetOrdinal("TotalCount"));
                return MapAsset(r);
            }, ct,
            P.Str("@Search", Allowed.Clean(q.Search), 100), P.Str("@Status", Allowed.Clean(q.Status), 20),
            P.Str("@Category", Allowed.Clean(q.Category), 50), P.Int("@Page", q.Page), P.Int("@PageSize", q.PageSize));
        return new PagedResult<AssetDto>(items, q.Page, q.PageSize, total);
    }

    public async Task<AssetDto> CreateAsync(CreateAssetRequest r, int userId, CancellationToken ct)
    {
        var id = await db.ScalarIntAsync("dbo.usp_Asset_Create", ct,
            P.Str("@AssetCode", r.AssetCode.Trim(), 30), P.Str("@SerialNumber", Allowed.Clean(r.SerialNumber), 80),
            P.Str("@Category", r.Category.Trim(), 50), P.Str("@Brand", r.Brand.Trim(), 80), P.Str("@Model", r.Model.Trim(), 80),
            P.Str("@OwnershipType", r.OwnershipType, 10), P.Int("@SupplierId", r.SupplierId),
            P.Str("@CurrentLocation", r.CurrentLocation.Trim(), 150), P.Date("@PurchaseDate", r.PurchaseDate),
            P.Date("@RentalEndDate", r.RentalEndDate), P.Int("@UserId", userId), P.Str("@Notes", Allowed.Clean(r.Notes), 500));
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<AssetDto> UpdateAsync(int id, UpdateAssetRequest r, int userId, CancellationToken ct)
    {
        await db.ExecuteAsync("dbo.usp_Asset_Update", ct,
            P.Int("@Id", id), P.Str("@SerialNumber", Allowed.Clean(r.SerialNumber), 80),
            P.Str("@Category", r.Category.Trim(), 50), P.Str("@Brand", r.Brand.Trim(), 80), P.Str("@Model", r.Model.Trim(), 80),
            P.Str("@OwnershipType", r.OwnershipType, 10), P.Int("@SupplierId", r.SupplierId), P.Str("@Status", r.Status, 20),
            P.Str("@CurrentLocation", r.CurrentLocation.Trim(), 150), P.Date("@PurchaseDate", r.PurchaseDate),
            P.Date("@RentalEndDate", r.RentalEndDate), P.Int("@UserId", userId), P.Str("@Notes", Allowed.Clean(r.Notes), 500));
        return (await GetByIdAsync(id, ct))!;
    }

    public Task<List<AssetMovementDto>> GetHistoryAsync(int assetId, CancellationToken ct) =>
        db.QueryAsync("dbo.usp_Asset_GetHistory", MapMovement, ct, P.Int("@AssetId", assetId));

    public async Task<AssignmentResultDto> AssignAsync(int assetId, AssignRequest r, int userId, CancellationToken ct)
    {
        var assignmentId = await db.ScalarIntAsync("dbo.usp_Asset_Assign", ct,
            P.Int("@AssetId", assetId), P.Int("@EmployeeId", r.EmployeeId), P.Int("@UserId", userId),
            P.Str("@Notes", Allowed.Clean(r.Notes), 500));
        return new AssignmentResultDto(assignmentId, (await GetByIdAsync(assetId, ct))!);
    }

    public async Task<AssignmentResultDto> ReturnAsync(int assetId, ReturnRequest r, int userId, CancellationToken ct)
    {
        var assignmentId = await db.ScalarIntAsync("dbo.usp_Asset_Return", ct,
            P.Int("@AssetId", assetId), P.Str("@Condition", r.Condition, 20), P.Int("@UserId", userId),
            P.Str("@Notes", Allowed.Clean(r.Notes), 500));
        return new AssignmentResultDto(assignmentId, (await GetByIdAsync(assetId, ct))!);
    }
}
