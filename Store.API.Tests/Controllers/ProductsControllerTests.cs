using Microsoft.AspNetCore.Mvc;
using Moq;
using Store.API.Controllers;
using Store.API.DTOs;
using Store.API.Tests.Common;
using Store.Application.Common;
using Store.Application.DTOs.Products.Requests;
using Store.Application.DTOs.Products.Responses;
using Store.Application.Interfaces;
using Store.Domain.Entities;

namespace Store.API.Tests.Controllers;

public class ProductsControllerTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsOk_WhenServiceReturnsSuccess()
    {
        using var db = TestDbContextFactory.Create();
        var mockService = new Mock<IProductService>();

        var productId = Guid.NewGuid();
        mockService.Setup(s => s.GetByIdAsync(productId))
            .ReturnsAsync(Result<ProductDetailResponse>.Success(
                new ProductDetailResponse(productId, "Keyboard", "RGB", 99, 4, true, "Electronics", ["Gaming"])));

        var controller = new ProductsController(mockService.Object, db);

        var result = await controller.GetByIdAsync(productId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SearchForProduct_ReturnsBadRequest_WhenServiceFails()
    {
        using var db = TestDbContextFactory.Create();
        var mockService = new Mock<IProductService>();
        var request = new ProductSearchRequest { Name = "x", Page = 1, PageSize = 10 };

        mockService.Setup(s => s.SearchAsync(request))
            .ReturnsAsync(Result<ProductSearchResponse>.BadRequest("invalid search"));

        var controller = new ProductsController(mockService.Object, db);

        var result = await controller.SearchForProduct(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AdjustmentStocks_ReturnsBadRequest_WhenBodyIsEmpty()
    {
        using var db = TestDbContextFactory.Create();
        var controller = new ProductsController(Mock.Of<IProductService>(), db);

        var result = await controller.AdjustmentStocks([]);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateProductsByCategory_ReturnsAffectedRows()
    {
        using var db = TestDbContextFactory.Create();
        var categoryId = Guid.NewGuid();

        db.Categories.Add(new Category { Id = categoryId, Name = "Electronics", Description = "E", RowVersion = [1], CreatedAt = DateTime.UtcNow });
        db.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "A", Price = 5, StockQuantity = 2, IsActive = true, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Name = "B", Price = 7, StockQuantity = 2, IsActive = true, CategoryId = categoryId, RowVersion = [1], CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = new ProductsController(Mock.Of<IProductService>(), db);

        var result = await controller.DeactivateProductsByCategory(categoryId);

        Assert.IsType<OkObjectResult>(result);
        Assert.All(db.Products, p => Assert.False(p.IsActive));
    }

    [Fact]
    public async Task AdjustmentStocks_UpdatesValidProducts_AndSkipsInvalidEntries()
    {
        using var db = TestDbContextFactory.Create();
        var categoryId = Guid.NewGuid();
        var trackedProductId = Guid.NewGuid();

        db.Categories.Add(new Category { Id = categoryId, Name = "Books", Description = "D", RowVersion = [1], CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            Id = trackedProductId,
            Name = "Book",
            Price = 20,
            StockQuantity = 5,
            IsActive = true,
            CategoryId = categoryId,
            RowVersion = [1],
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new ProductsController(Mock.Of<IProductService>(), db);
        var payload = new List<ProductAdjustment>
        {
            new() { ProductId = trackedProductId, QuantityChange = -2 },
            new() { ProductId = trackedProductId, QuantityChange = 0 },
            new() { ProductId = Guid.NewGuid(), QuantityChange = 5 }
        };

        var result = await controller.AdjustmentStocks(payload);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(3, db.Products.Single().StockQuantity);
    }
}
