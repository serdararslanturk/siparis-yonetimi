using Acr.Filo.Application.Common;

namespace Acr.Filo.Application.Definitions;

public sealed record DefinitionDto(int Id, string Ad, bool IsActive, byte[] RowVersion);
public sealed record CreateDefinitionRequest(string Ad);
public sealed record UpdateDefinitionRequest(string Ad, bool IsActive, byte[] RowVersion);

public interface IDefinitionService
{
    // tur: "customers" | "suppliers" | "brands"
    Task<Result<PagedResult<DefinitionDto>>> ListAsync(string tur, PageQuery q, CancellationToken ct);
    Task<Result<IReadOnlyList<DefinitionDto>>> AllActiveAsync(string tur, CancellationToken ct); // dropdown
    Task<Result<DefinitionDto>> CreateAsync(string tur, CreateDefinitionRequest req, CancellationToken ct);
    Task<Result<DefinitionDto>> UpdateAsync(string tur, int id, UpdateDefinitionRequest req, CancellationToken ct);
    Task<Result> DeleteAsync(string tur, int id, CancellationToken ct); // soft-delete
}
