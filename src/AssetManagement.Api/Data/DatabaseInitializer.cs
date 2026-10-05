using System.Text.RegularExpressions;
using AssetManagement.Api.Options;
using AssetManagement.Api.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AssetManagement.Api.Data;

public sealed partial class DatabaseInitializer(
    SqlDb db, IOptions<DatabaseOptions> dbOptions, IOptions<SeedOptions> seed, PasswordHasher hasher, ILogger<DatabaseInitializer> logger)
{
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await EnsureDatabaseAsync(ct);
        await RunScriptAsync("001_schema.sql", ct);
        await RunScriptAsync("002_procedures.sql", ct);
        await SeedUsersAsync(ct);
        if (dbOptions.Value.SeedSampleData) await RunScriptAsync("003_sample_data.sql", ct);
        logger.LogInformation("Base de datos lista.");
    }

    private async Task EnsureDatabaseAsync(CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(db.ConnectionString);
        var name = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("La cadena de conexion debe indicar Database/Initial Catalog.");

        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "IF DB_ID(@db) IS NULL BEGIN DECLARE @sql NVARCHAR(400) = N'CREATE DATABASE ' + QUOTENAME(@db); EXEC (@sql); END";
        command.Parameters.AddWithValue("@db", name);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task RunScriptAsync(string fileName, CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Database", fileName);
        var script = await File.ReadAllTextAsync(path, ct);
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        foreach (var batch in GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        await SeedUserAsync("admin", seed.Value.AdminPassword, "Administrador", ct);
        await SeedUserAsync("operador", seed.Value.OperatorPassword, "Operador", ct);
    }

    private Task SeedUserAsync(string username, string? password, string role, CancellationToken ct) =>
        string.IsNullOrEmpty(password)
            ? Task.CompletedTask
            : db.ExecuteAsync("dbo.usp_User_Create", ct,
                P.Str("@Username", username, 50), P.Str("@PasswordHash", hasher.Hash(password), 400), P.Str("@Role", role, 20));
}
