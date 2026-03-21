using Store.Application.Common;
using Store.Application.DTOs.Products.Requests;
using Store.Application.DTOs.Products.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Interfaces;

public interface IProductService
{

    public Task<Result<ProductDetailResponse>> GetByIdAsync(Guid id);
    public Task<Result<ProductSearchResponse>> SearchAsync(ProductSearchRequest searchRequest);
}
