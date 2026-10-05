using AssetManagement.Api.Data;
using AssetManagement.Api.Models;
using Microsoft.Data.SqlClient;

namespace AssetManagement.Api.Repositories;

public sealed class SupplierRepository(SqlDb db)
{
    private static SupplierDto Map(SqlDataReader r) => new(
        r.GetInt32(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("Name")), r.GetStringOrNull("TaxId"),
        r.GetStringOrNull("Email"), r.GetStringOrNull("Phone"), r.GetBoolean(r.GetOrdinal("IsActive")), r.GetUtc("CreatedAt"),
        (r.GetStringOrNull("Services") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));

    public Task<SupplierDto?> GetByIdAsync(int id, CancellationToken ct) =>
        db.QuerySingleAsync("dbo.usp_Supplier_GetById", Map, ct, P.Int("@Id", id));

    public async Task<PagedResult<SupplierDto>> SearchAsync(SupplierQuery q, CancellationToken ct)
    {
        var total = 0;
        var items = await db.QueryAsync("dbo.usp_Supplier_Search", r =>
            {
                total = r.GetInt32(r.GetOrdinal("TotalCount"));
                return Map(r);
            }, ct,
            P.Str("@Search", Allowed.Clean(q.Search), 150), P.Int("@Page", q.Page), P.Int("@PageSize", q.PageSize));
        return new PagedResult<SupplierDto>(items, q.Page, q.PageSize, total);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest r, CancellationToken ct)
    {
        var id = await db.ScalarIntAsync("dbo.usp_Supplier_Create", ct,
            P.Str("@Name", r.Name.Trim(), 150), P.Str("@TaxId", Allowed.Clean(r.TaxId), 30), P.Str("@Email", Allowed.Clean(r.Email), 150),
            P.Str("@Phone", Allowed.Clean(r.Phone), 30), P.Str("@Services", string.Join(',', r.Services.Distinct()), 100));
        return (await GetByIdAsync(id, ct))!;
    }
}
