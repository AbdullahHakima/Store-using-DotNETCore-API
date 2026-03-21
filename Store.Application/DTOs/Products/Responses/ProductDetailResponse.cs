using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.DTOs.Products.Responses
{
    public record ProductDetailResponse(
        Guid Id,
        string Name,
        string? Description,
        decimal Price,
        int Stock,
        bool IsActive,
        string CategoryName,
        IEnumerable<string> Tags);
}
