using Microsoft.AspNetCore.Mvc;
using Store.API.Controllers;
using Store.API.Tests.Common;
using Store.Domain.Entities;

namespace Store.API.Tests.Controllers;

public class CategoriesControllerTests
{
    [Fact]
    public async Task TransferProducts_ReturnsBadRequest_WhenProductIdsEmpty()
    {
        using var db = TestDbContextFactory.Create();
        var controller = new CategoriesController(db);

        var result = await controller.TransferProducts(Guid.NewGuid(), []);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task TransferProducts_ReturnsNotFound_WhenTargetCategoryMissing()
    {
        using var db = TestDbContextFactory.Create();
        var controller = new CategoriesController(db);

        var result = await controller.TransferProducts(Guid.NewGuid(), [Guid.NewGuid()]);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task TransferProducts_UpdatesCategory_WhenDataIsValid()
    {
        using var db = TestDbContextFactory.Create();
        var sourceCategoryId = Guid.NewGuid();
        var targetCategoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        db.Categories.AddRange(
            new Category { Id = sourceCategoryId, Name = "Old", Description = "Old", RowVersion = [1], CreatedAt = DateTime.UtcNow },
            new Category { Id = targetCategoryId, Name = "New", Description = "New", RowVersion = [1], CreatedAt = DateTime.UtcNow });

        db.Products.Add(new Product
        {
            Id = productId,
            Name = "Item",
            Price = 10,
            StockQuantity = 3,
            IsActive = true,
            CategoryId = sourceCategoryId,
            RowVersion = [1],
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new CategoriesController(db);

        var result = await controller.TransferProducts(targetCategoryId, [productId]);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(targetCategoryId, db.Products.Single().CategoryId);
    }
}
