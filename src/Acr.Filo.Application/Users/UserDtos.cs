using Acr.Filo.Application.Common;

namespace Acr.Filo.Application.Users;

public sealed record UserListDto(int Id, string Email, string FullName, bool IsActive,
    IReadOnlyCollection<string> Roles, DateTime? LastLoginAtUtc);

public sealed record CreateUserRequest(string Email, string FullName, IReadOnlyCollection<string> Roles);
public sealed record UpdateUserRequest(string FullName, bool IsActive, IReadOnlyCollection<string> Roles, byte[] RowVersion);
public sealed record ResetPasswordResult(string TemporaryHint); // gerçek parola değil, akış bilgisi
public sealed record RoleDto(int Id, string Key, string Name, IReadOnlyCollection<string> Permissions);

public interface IUserService
{
    Task<Result<PagedResult<UserListDto>>> ListAsync(PageQuery q, CancellationToken ct);
    Task<Result<UserListDto>> CreateAsync(CreateUserRequest req, CancellationToken ct);
    Task<Result<UserListDto>> UpdateAsync(int id, UpdateUserRequest req, CancellationToken ct);
    Task<Result> DeactivateAsync(int id, CancellationToken ct);
    Task<Result<IReadOnlyList<RoleDto>>> RolesAsync(CancellationToken ct);
}
