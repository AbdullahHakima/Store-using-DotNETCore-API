using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.API.DTOs;
using Store.Infrastructure.Presistence;

namespace Store.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public CategoriesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost("{targetCategoryId}/transfer-products")]
    public async Task<IActionResult> TransferProducts([FromRoute] Guid targetCategoryId,
                                                      [FromBody] List<Guid> productIds)
    {
        var success = new List<ProductsSuccessTransferedDto>();
        var failed = new List<FailedTransferedProductsToCategoryDto>();

        if (productIds == null||productIds.Count==0) return BadRequest("their are no refered products sent");

        if(targetCategoryId == Guid.Empty) return BadRequest("target category id is not valid");

        var targetCategory = await _db.Categories.FindAsync(targetCategoryId);
        if (targetCategory is null)
            return NotFound("target category is not found");

        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).Include(p=>p.Category).ToListAsync();


        var missingProductIds = productIds.Except(products.Select(p => p.Id)).ToList();
                   foreach (var missingProductId in missingProductIds)
            {
                failed.Add(new FailedTransferedProductsToCategoryDto
                {
                    ProductId = missingProductId,
                    error = $"product with id {missingProductId} is not found"
                });
            }
        

        foreach (var product in products)
        {
            if(product.CategoryId == targetCategoryId)
            {
                failed.Add(new FailedTransferedProductsToCategoryDto
                {
                    ProductId = product.Id,
                    error = $"product with id {product.Id} already belongs to category with id {targetCategoryId}"
                });
                continue;
            }
            var oldProductCategory= product.Category.Name;
            product.CategoryId= targetCategoryId;
            success.Add(new ProductsSuccessTransferedDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                OldCategory = oldProductCategory,
                NewCategory = targetCategory.Name
            });
        }
        if(success.Count>0)
            await _db.SaveChangesAsync();


        return Ok(new
        {
            TargetCategoryName= targetCategory.Name,
            Success = success,
            Failed = failed
        });
    }
}
