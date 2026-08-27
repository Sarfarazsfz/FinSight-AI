namespace FinSight.Application.DTOs.Reconciliation;

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } =
        Array.Empty<T>();

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }
}
