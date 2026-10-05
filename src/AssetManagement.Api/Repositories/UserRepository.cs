using AssetManagement.Api.Data;
using AssetManagement.Api.Models;

namespace AssetManagement.Api.Repositories;

public sealed class UserRepository(SqlDb db)
{
    public Task<AuthUser?> GetByUsernameAsync(string username, CancellationToken ct) =>
        db.QuerySingleAsync("dbo.usp_User_GetByUsername", r => new AuthUser(
            r.GetInt32(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("Username")), r.GetString(r.GetOrdinal("PasswordHash")),
            r.GetString(r.GetOrdinal("Role")), r.GetBoolean(r.GetOrdinal("IsActive")), r.GetBoolean(r.GetOrdinal("IsLockedOut"))),
            ct, P.Str("@Username", username, 50));

    public Task RegisterFailedLoginAsync(int userId, int maxAttempts, int lockoutMinutes, CancellationToken ct) =>
        db.ExecuteAsync("dbo.usp_User_RegisterFailedLogin", ct,
            P.Int("@UserId", userId), P.Int("@MaxAttempts", maxAttempts), P.Int("@LockoutMinutes", lockoutMinutes));

    public Task RegisterSuccessfulLoginAsync(int userId, CancellationToken ct) =>
        db.ExecuteAsync("dbo.usp_User_RegisterSuccessfulLogin", ct, P.Int("@UserId", userId));
}
