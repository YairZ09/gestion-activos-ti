using System.Security.Claims;
using System.Text;
using AssetManagement.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AssetManagement.Api.Security;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    public const string RoleClaim = "role";
    public const string NameClaim = "unique_name";
    public const string SubjectClaim = "sub";

    public static SymmetricSecurityKey BuildKey(string key) => new(Encoding.UTF8.GetBytes(key));

    public AccessToken Create(int userId, string username, string role)
    {
        var o = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(o.ExpiryMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            NotBefore = DateTime.UtcNow,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(SubjectClaim, userId.ToString()),
                new Claim(NameClaim, username),
                new Claim(RoleClaim, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            SigningCredentials = new SigningCredentials(BuildKey(o.Key), SecurityAlgorithms.HmacSha256)
        };
        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(JwtTokenService.SubjectClaim), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token sin identificador de usuario.");
}
