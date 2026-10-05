using System.Data;
using Microsoft.Data.SqlClient;

namespace AssetManagement.Api.Data;


public sealed class SqlDb
{
    private readonly string _connectionString;

    public SqlDb(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'ConnectionStrings:Default'.");
    }

    public string ConnectionString => _connectionString;

    private async Task<SqlCommand> CreateCommandAsync(string procedure, SqlParameter[] parameters, CancellationToken ct)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedure;
            command.CommandTimeout = 30;
            command.Parameters.AddRange(parameters);
            return command;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<List<T>> QueryAsync<T>(string procedure, Func<SqlDataReader, T> map, CancellationToken ct, params SqlParameter[] parameters)
    {
        await using var command = await CreateCommandAsync(procedure, parameters, ct);
        await using var connection = command.Connection!;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct)) rows.Add(map(reader));
        return rows;
    }

    public async Task<T?> QuerySingleAsync<T>(string procedure, Func<SqlDataReader, T> map, CancellationToken ct, params SqlParameter[] parameters) where T : class
        => (await QueryAsync(procedure, map, ct, parameters)).FirstOrDefault();

    public async Task<int> ScalarIntAsync(string procedure, CancellationToken ct, params SqlParameter[] parameters)
    {
        await using var command = await CreateCommandAsync(procedure, parameters, ct);
        await using var connection = command.Connection!;
        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task ExecuteAsync(string procedure, CancellationToken ct, params SqlParameter[] parameters)
    {
        await using var command = await CreateCommandAsync(procedure, parameters, ct);
        await using var connection = command.Connection!;
        await command.ExecuteNonQueryAsync(ct);
    }
}

public static class P
{
    public static SqlParameter Int(string name, int? value) =>
        new(name, SqlDbType.Int) { Value = value is null ? DBNull.Value : value };

    public static SqlParameter Bit(string name, bool? value) =>
        new(name, SqlDbType.Bit) { Value = value is null ? DBNull.Value : value };

    public static SqlParameter Str(string name, string? value, int size) =>
        new(name, SqlDbType.NVarChar, size) { Value = value is null ? DBNull.Value : value };

    public static SqlParameter Date(string name, DateOnly? value) =>
        new(name, SqlDbType.Date) { Value = value is null ? DBNull.Value : value.Value.ToDateTime(TimeOnly.MinValue) };
}

public static class ReaderExtensions
{
    public static string? GetStringOrNull(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static int? GetIntOrNull(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    public static DateOnly? GetDateOrNull(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : DateOnly.FromDateTime(r.GetDateTime(i));
    }

    public static DateTime GetUtc(this SqlDataReader r, string column) =>
        DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(column)), DateTimeKind.Utc);
}
