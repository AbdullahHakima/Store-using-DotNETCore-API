using Microsoft.AspNetCore.Mvc;
using Store.API.Controllers;
using Store.API.Tests.Common;
using Store.Domain.Entities;

namespace Store.API.Tests.Controllers;

public class QueryLabControllerTests
{
    [Fact]
    public async Task DeferredDemo_FiltersByMinPriceAndActiveFlag()
    {
        using var db = TestDbContextFactory.Create();
        var categoryId = Guid.NewGuid();

        db.Categories.Add(new Category { Id = categoryId, Name = "General", Description = "D", RowVersion = [1], CreatedAt = DateTime.UtcNow });
        db.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "A", Price = 100, StockQuantity = 1, IsActive = true, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Name = "B", Price = 50, StockQuantity = 1, IsActive = true, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Name = "C", Price = 150, StockQuantity = 1, IsActive = false, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = new QueryLabController(db);

        var result = await controller.DeferredDemo(80, true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var products = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        Assert.Single(products);
    }

    [Fact]
    public async Task PagedDemo_ReturnsOkWithRequestedPage()
    {
        using var db = TestDbContextFactory.Create();
        var categoryId = Guid.NewGuid();

        db.Categories.Add(new Category { Id = categoryId, Name = "General", Description = "D", RowVersion = [1], CreatedAt = DateTime.UtcNow });
        db.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "A", Price = 20, StockQuantity = 1, IsActive = false, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Name = "B", Price = 30, StockQuantity = 1, IsActive = false, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Name = "C", Price = 40, StockQuantity = 1, IsActive = false, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = new QueryLabController(db);

        var result = await controller.PagedDemo(2, 2);

        Assert.IsType<OkObjectResult>(result);
    }
}
