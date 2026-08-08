namespace Acr.Filo.Application.Common;

/// <summary>Sunucu taraflı sayfalama. Frontend liste ekranları bunu bekler.</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>Liste sorgu parametreleri. Controller doğrular, servise geçirir.</summary>
public class PageQuery
{
    private const int MaxPageSize = 200;
    private int _pageSize = 20;
    public int Page { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? 20 : value;
    }
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public bool Desc { get; set; } = true;

    public int Skip => (Math.Max(Page, 1) - 1) * PageSize;
}
