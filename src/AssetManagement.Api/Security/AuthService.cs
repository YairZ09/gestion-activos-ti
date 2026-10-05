using AssetManagement.Api.Errors;
using AssetManagement.Api.Models;
using AssetManagement.Api.Options;
using AssetManagement.Api.Repositories;
using Microsoft.Extensions.Options;

namespace AssetManagement.Api.Security;

public sealed class AuthService(UserRepository users, PasswordHasher hasher, JwtTokenService tokens, IOptions<SecurityOptions> options)
{
    private static BusinessException InvalidCredentials() => new(401, "INVALID_CREDENTIALS", "Usuario o contrasena incorrectos.");

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        var user = await users.GetByUsernameAsync(request.Username.Trim(), ct);

        if (user is null)
            throw InvalidCredentials();

        if (user.IsLockedOut)
            throw new BusinessException(429, "ACCOUNT_LOCKED", "Demasiados intentos fallidos. Intente de nuevo mas tarde.");

        if (!user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
        {
            await users.RegisterFailedLoginAsync(user.Id, settings.MaxFailedLoginAttempts, settings.LockoutMinutes, ct);
            throw InvalidCredentials();
        }

        await users.RegisterSuccessfulLoginAsync(user.Id, ct);
        var token = tokens.Create(user.Id, user.Username, user.Role);
        return new LoginResponse(token.Token, "Bearer", token.ExpiresAtUtc, user.Username, user.Role);
    }
}
