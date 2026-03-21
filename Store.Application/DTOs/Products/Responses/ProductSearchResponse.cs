using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.DTOs.Products.Responses
{
    public record ProductSearchResponse(
        int TotalCount,
        int TotalPages,
        int CurrentPage,
        IEnumerable<ProductSummary> Items );
    
}
public record ProductSummary(
    Guid Id,
    string Name,
    decimal Price,
    int Stock,
    string CategoryName,
    IEnumerable<string> Tags );