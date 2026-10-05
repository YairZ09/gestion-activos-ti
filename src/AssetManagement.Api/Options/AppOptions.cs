namespace AssetManagement.Api.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "AssetManagement.Api";
    public string Audience { get; set; } = "AssetManagement.Clients";
    public int ExpiryMinutes { get; set; } = 60;
    /// <summary>Clave de firma HS256 (minimo 32 caracteres). Se entrega por variable de entorno Jwt__Key.</summary>
    public string Key { get; set; } = "";
}

public sealed class SecurityOptions
{
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int LoginRequestsPerMinute { get; set; } = 10;
}

public sealed class DatabaseOptions
{
    public bool AutoMigrate { get; set; } = true;
    public bool SeedSampleData { get; set; }
}

public sealed class SeedOptions
{
    public string? AdminPassword { get; set; }
    public string? OperatorPassword { get; set; }
}
