using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace AssetManagement.Tests;

public static class TestConfig
{
    public const string AdminPassword = "Admin#Test-2026!";
    public const string OperatorPassword = "Operador#Test-2026!";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=AssetManagement_Tests;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";
}

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly Dictionary<string, string> Env = new()
    {
        ["ConnectionStrings__Default"] = TestConfig.ConnectionString,
        ["Jwt__Key"] = "test-signing-key-0123456789-abcdefghijklmnop",
        ["Jwt__ExpiryMinutes"] = "60",
        ["Seed__AdminPassword"] = TestConfig.AdminPassword,
        ["Seed__OperatorPassword"] = TestConfig.OperatorPassword,
        ["Database__SeedSampleData"] = "false",
        ["Security__MaxFailedLoginAttempts"] = "3",
        ["Security__LockoutMinutes"] = "15",
        ["Security__LoginRequestsPerMinute"] = "1000",
    };

    private string? _adminToken, _operatorToken;

    public Task InitializeAsync()
    {
        DropDatabase();
        foreach (var (key, value) in Env) Environment.SetEnvironmentVariable(key, value);
        _ = Server;
        return Task.CompletedTask;
    }

    public new Task DisposeAsync() => Task.CompletedTask;

    private static void DropDatabase()
    {
        var builder = new SqlConnectionStringBuilder(TestConfig.ConnectionString);
        var name = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        SqlConnection.ClearAllPools();
        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "IF DB_ID(@db) IS NOT NULL BEGIN DECLARE @sql NVARCHAR(500) = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@db); EXEC (@sql); END";
        command.Parameters.AddWithValue("@db", name);
        command.ExecuteNonQuery();
    }

    public HttpClient Anonymous() => CreateClient();

    public async Task<HttpClient> AdminAsync() => await ClientWithTokenAsync(_adminToken ??= await LoginAsync("admin", TestConfig.AdminPassword));

    public async Task<HttpClient> OperatorAsync() => await ClientWithTokenAsync(_operatorToken ??= await LoginAsync("operador", TestConfig.OperatorPassword));

    private Task<HttpClient> ClientWithTokenAsync(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return Task.FromResult(client);
    }

    public async Task<string> LoginAsync(string username, string password)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResult>())!.AccessToken;
    }

    public sealed record LoginResult(string AccessToken, string Role, DateTime ExpiresAt);
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
