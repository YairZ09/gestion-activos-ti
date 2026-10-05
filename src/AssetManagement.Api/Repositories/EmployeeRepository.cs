using AssetManagement.Api.Data;
using AssetManagement.Api.Models;
using Microsoft.Data.SqlClient;

namespace AssetManagement.Api.Repositories;

public sealed class EmployeeRepository(SqlDb db)
{
    private static EmployeeDto Map(SqlDataReader r) => new(
        r.GetInt32(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("EmployeeNumber")), r.GetString(r.GetOrdinal("FullName")),
        r.GetString(r.GetOrdinal("Email")), r.GetStringOrNull("Department"), r.GetBoolean(r.GetOrdinal("IsActive")), r.GetUtc("CreatedAt"));

    public Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct) =>
        db.QuerySingleAsync("dbo.usp_Employee_GetById", Map, ct, P.Int("@Id", id));

    public async Task<PagedResult<EmployeeDto>> SearchAsync(EmployeeQuery q, CancellationToken ct)
    {
        var total = 0;
        var items = await db.QueryAsync("dbo.usp_Employee_Search", r =>
            {
                total = r.GetInt32(r.GetOrdinal("TotalCount"));
                return Map(r);
            }, ct,
            P.Str("@Search", Allowed.Clean(q.Search), 150), P.Bit("@IsActive", q.IsActive),
            P.Int("@Page", q.Page), P.Int("@PageSize", q.PageSize));
        return new PagedResult<EmployeeDto>(items, q.Page, q.PageSize, total);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest r, CancellationToken ct)
    {
        var id = await db.ScalarIntAsync("dbo.usp_Employee_Create", ct,
            P.Str("@EmployeeNumber", r.EmployeeNumber.Trim(), 30), P.Str("@FullName", r.FullName.Trim(), 150),
            P.Str("@Email", r.Email.Trim(), 150), P.Str("@Department", Allowed.Clean(r.Department), 100), P.Bit("@IsActive", r.IsActive));
        return (await GetByIdAsync(id, ct))!;
    }
}
