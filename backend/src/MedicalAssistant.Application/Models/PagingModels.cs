namespace MedicalAssistant.Application.Models;

public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (normalizedPage, normalizedSize);
    }

    public static int ClampPage(int page, int pageSize, int totalCount)
    {
        if (totalCount <= 0)
            return 1;

        var lastPage = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Math.Clamp(page, 1, lastPage);
    }
}
