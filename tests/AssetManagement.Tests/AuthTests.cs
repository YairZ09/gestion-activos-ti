using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AssetManagement.Api.Data;
using AssetManagement.Api.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AssetManagement.Tests;

[Collection("api")]
public sealed class AuthTests(ApiFixture api)
{
    [Fact]
    public async Task Login_with_valid_credentials_returns_jwt_with_role()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/login", new { username = "admin", password = TestConfig.AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<ApiFixture.LoginResult>();
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("Administrador", body.Role);
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Theory]
    [InlineData("admin", "incorrecta")]
    [InlineData("no-existe", "cualquiera")]
    public async Task Login_with_bad_credentials_returns_generic_401(string user, string password)
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/login", new { username = user, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Login_with_missing_fields_returns_400()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/login", new { username = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Private_endpoints_require_a_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync("/api/assets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync("/api/suppliers")).StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var token = BuildToken(expires: DateTime.UtcNow.AddMinutes(-5), signingKey: "test-signing-key-0123456789-abcdefghijklmnop");
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAssetsWith(token)).StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var token = BuildToken(expires: DateTime.UtcNow.AddMinutes(30), signingKey: "another-secret-key-0123456789-abcdefghijk");
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAssetsWith(token)).StatusCode);
    }

    [Fact]
    public async Task Valid_token_is_accepted()
    {
        var token = BuildToken(expires: DateTime.UtcNow.AddMinutes(30), signingKey: "test-signing-key-0123456789-abcdefghijklmnop");
        Assert.Equal(HttpStatusCode.OK, (await GetAssetsWith(token)).StatusCode);
    }

    [Fact]
    public async Task Operator_cannot_create_assets_or_suppliers_but_can_read()
    {
        var op = await api.OperatorAsync();

        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/assets")).StatusCode);
        var create = await op.PostAsJsonAsync("/api/assets", new { assetCode = "TI-NOPE", category = "Laptop", brand = "X", model = "Y", ownershipType = "Propio", currentLocation = "A" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        var supplier = await op.PostAsJsonAsync("/api/suppliers", new { name = "X", services = new[] { "Compra" } });
        Assert.Equal(HttpStatusCode.Forbidden, supplier.StatusCode);
    }

    [Fact]
    public async Task Account_is_locked_after_too_many_failed_attempts_even_with_correct_password()
    {
        var username = Unique("lock");
        await CreateUserAsync(username, "Correcta#2026!");
        var client = api.Anonymous();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username, password = "mala" })).StatusCode);

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "Correcta#2026!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Equal("ACCOUNT_LOCKED", await locked.ErrorCodeAsync());
    }

    [Fact]
    public async Task Successful_login_resets_failed_attempt_counter()
    {
        var username = Unique("reset");
        await CreateUserAsync(username, "Correcta#2026!");
        var client = api.Anonymous();

        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < 2; i++)
                await client.PostAsJsonAsync("/api/auth/login", new { username, password = "mala" });
            var ok = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "Correcta#2026!" });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private async Task CreateUserAsync(string username, string password)
    {
        var db = api.Services.GetRequiredService<SqlDb>();
        await db.ExecuteAsync("dbo.usp_User_Create", CancellationToken.None,
            P.Str("@Username", username, 50), P.Str("@PasswordHash", api.Services.GetRequiredService<PasswordHasher>().Hash(password), 400), P.Str("@Role", "Operador", 20));
    }

    private Task<HttpResponseMessage> GetAssetsWith(string token)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync("/api/assets");
    }

    private static string BuildToken(DateTime expires, string signingKey) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "AssetManagement.Api",
            Audience = "AssetManagement.Clients",
            NotBefore = expires.AddHours(-1),
            Expires = expires,
            Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("unique_name", "admin"), new Claim("role", "Administrador")]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256)
        });
}
