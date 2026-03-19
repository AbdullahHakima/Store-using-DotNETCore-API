using Microsoft.EntityFrameworkCore;
namespace Store.Infrastructure.Presistence
{
    public static class CompiledQueries
    {
        // Compiled query to get a product by its ID, including the category name
        public static readonly Func<ApplicationDbContext, Guid, Task<ProductDto?>> GetProductById
            = EF.CompileAsyncQuery((ApplicationDbContext context, Guid productId) =>
            context.Products.Where(p => p.Id == productId && p.IsActive).Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                Price = p.Price,
                QuantityInStock = p.StockQuantity
            }).FirstOrDefault());


        public static readonly Func<ApplicationDbContext, Guid, IAsyncEnumerable<ProductDto>> GetProductsByCategoryId
                = EF.CompileAsyncQuery((ApplicationDbContext context, Guid categoryId)
                    => context.Products.Where(p => p.CategoryId == categoryId && p.IsActive).Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        CategoryName = p.Category.Name,
                        Price = p.Price,
                        QuantityInStock = p.StockQuantity
                    })
                );
    }
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public int QuantityInStock { get; set; }
    }
}
