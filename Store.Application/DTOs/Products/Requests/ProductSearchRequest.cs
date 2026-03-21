namespace Store.Application.DTOs.Products.Requests;

public record ProductSearchRequest
{

    public string? Name { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool? InStock { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; set; } = 10;
}
