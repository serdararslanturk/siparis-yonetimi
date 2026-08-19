using Acr.Filo.Application.Common;

namespace Acr.Filo.Application.Definitions;

// VadeGun/TemsilciId/TemsilciAd yalnızca tur="customers" için doludur; diğer türlerde null.
public sealed record DefinitionDto(int Id, string Ad, bool IsActive, byte[] RowVersion,
    int? VadeGun = null, int? TemsilciId = null, string? TemsilciAd = null);
public sealed record CreateDefinitionRequest(string Ad, int? VadeGun = null, int? TemsilciId = null);
public sealed record UpdateDefinitionRequest(string Ad, bool IsActive, byte[] RowVersion,
    int? VadeGun = null, int? TemsilciId = null);

public interface IDefinitionService
{
    // tur: "customers" | "suppliers" | "brands"
    Task<Result<PagedResult<DefinitionDto>>> ListAsync(string tur, PageQuery q, CancellationToken ct);
    Task<Result<IReadOnlyList<DefinitionDto>>> AllActiveAsync(string tur, CancellationToken ct); // dropdown
    Task<Result<DefinitionDto>> CreateAsync(string tur, CreateDefinitionRequest req, CancellationToken ct);
    Task<Result<DefinitionDto>> UpdateAsync(string tur, int id, UpdateDefinitionRequest req, CancellationToken ct);
    Task<Result> DeleteAsync(string tur, int id, CancellationToken ct); // soft-delete
}
