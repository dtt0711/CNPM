namespace TourismHcm.Contracts.Places;

public sealed class PlaceQueryRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Category { get; init; }
    public string? District { get; init; }
    public string? Status { get; init; }
    public string? SortBy { get; init; }
    public bool Descending { get; init; }
}
