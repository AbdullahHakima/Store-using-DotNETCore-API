using Microsoft.EntityFrameworkCore;
using Store.Application.Common;
using Store.Application.DTOs.Products.Requests;
using Store.Application.DTOs.Products.Responses;
using Store.Application.Interfaces;
using Store.Infrastructure.Presistence;


namespace Store.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext context;
    public ProductService(ApplicationDbContext context)
    { 
        this.context = context;
    }
    public async Task<Result<ProductDetailResponse>> GetByIdAsync(Guid id)
    {
        //check for the product 
        var product = await context.Products.Where(p => p.Id == id).Select(p => new
        {
            p.Id,
            CategoryName = p.Category.Name,
            p.Description,
            p.Name,
            p.Price,
            p.IsActive,
            Stock = p.StockQuantity,
            Tags = p.Tags.Select(t => t.Name),
        }).AsNoTracking().FirstOrDefaultAsync();
        if(product is null)
           return Result<ProductDetailResponse>.NotFound($"their is not product founded with Id:{id}");
        return Result<ProductDetailResponse>.Success(new ProductDetailResponse(id, product.Name, product.Description, product.Price, product.Stock, product.IsActive, product.CategoryName, product.Tags));
    }

    public async Task<Result<ProductSearchResponse>> SearchAsync(ProductSearchRequest searchRequest)
    {
        //// to avoid make the user able to load all available products of the query as default or applying filters 
        //searchRequest.PageSize = Math.Clamp(searchRequest.PageSize, 1, 50);
        
        // check for the prodcuts using the searchRequest model

        var query = context.Products.Where(p => p.IsActive)
                                    .AsQueryable();
        //apply query chaining  
        //seach by name so check for name value
        if (!string.IsNullOrEmpty(searchRequest.Name))
            query = query.Where(q => q.Name.Contains(searchRequest.Name));
        //search by categoryId
        if (searchRequest.CategoryId.HasValue)
            query = query.Where(q => q.CategoryId == searchRequest.CategoryId);
        //seach by in/out Stock
        if (searchRequest.InStock==true)
            query = query.Where(q => q.StockQuantity > 0);//if InStock = true so should view only product that has quantaties
        //search by minPrice
        if (searchRequest.MinPrice.HasValue)
            query = query.Where(q => q.Price >= searchRequest.MinPrice);
        //search by maxPrice
        if (searchRequest.MaxPrice.HasValue)
            query = query.Where(q => q.Price <= searchRequest.MaxPrice);
        
        int TotalCount = await query.CountAsync();
        int TotalPages = (int)Math.Ceiling(TotalCount / (double)searchRequest.PageSize);

        List<ProductSummary> Items = await query.OrderBy(q => q.Name)
                                            .Skip((searchRequest.Page-1) * searchRequest.PageSize)
                                            .Take(searchRequest.PageSize)
                                            .Select(p => new ProductSummary
                                            (
                                                p.Id,
                                                p.Name,
                                                p.Price,
                                                p.StockQuantity,
                                                p.Category.Name,
                                                p.Tags.Select(t=>t.Name).ToList()
                                            )).AsNoTracking().ToListAsync();
        //all filter are setted already return the Result 

        //there is a handle incase of after applying all filters there is no products that match the requested criteria
        if (!Items.Any()) return Result<ProductSearchResponse>.NotFound("there is not product founded after applying the requested criterias");
        var result = new ProductSearchResponse(TotalCount, TotalPages, searchRequest.Page, Items);
        return Result<ProductSearchResponse>.Success(result);


    }
}
