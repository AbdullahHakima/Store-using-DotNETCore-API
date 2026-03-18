using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.API.DTOs;
using Store.Infrastructure.Presistence;

namespace Store.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    // Direct Injection of the ApplicationDbContext into the ProductController to access the database context and perform CRUD operations on products.
    private readonly ApplicationDbContext _db;
    public ProductsController(ApplicationDbContext context)
    {
        _db = context;
    }
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        var products = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Tags)
            .AsNoTracking()          // read-only — no change tracking overhead
            .ToListAsync();

        return Ok(products);
    }

    // GET api/products/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Tags)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return NotFound();
        return Ok(product);
    }

    // GET api/products/by-category/{categoryId}
    // Projection with Select — only fetches the columns you need
    [HttpGet("by-category/{categoryId:guid}")]
    public async Task<IActionResult> GetByCategory(Guid categoryId)
    {
        var products = await _db.Products
            .Where(p => p.CategoryId == categoryId)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.StockQuantity,
                CategoryName = p.Category.Name,
                Tags = p.Tags.Select(t => t.Name)
            })
            .AsNoTracking()
            .ToListAsync();

        return Ok(products);
    }



    [HttpPost("adjust-stock")]
    public async Task<IActionResult> adjustProductStock([FromBody] List<ProductAdjustment> adjustments)
    {
        if (adjustments == null || adjustments.Count == 0) 
            return BadRequest("No adjustments provided.");
        var productIds = adjustments.Select(a => a.ProductId);
        var products = await _db.Products.Where(p=> productIds.Contains(p.Id)). ToListAsync();

        var failers = new List<FailersAdjustment>();
        var success = new List<SuccessAdjustmentDto>();
        foreach (var adjustment in adjustments)
        {
            var product = products.FirstOrDefault(p => p.Id == adjustment.ProductId);
            if (product is null)
            {
                failers.Add(new FailersAdjustment
                {
                    productId = adjustment.ProductId,
                    reason = "Product not found."
                });
                continue; // Skip to the next adjustment
            }
                if(adjustment.QuantityChange == 0)
                {
                    failers.Add(new FailersAdjustment
                    {
                        productId = product.Id,
                        reason = "No change in stock quantity."
                    });
                    continue; // Skip to the next adjustment
                }
                if (product.StockQuantity + adjustment.QuantityChange < 0)
                {
                    failers.Add(new FailersAdjustment
                    {
                        productId = product.Id,
                        reason = $"Insufficient stock for adjustment. Current:{product.StockQuantity}, adjustment:{adjustment.QuantityChange}"
                    });
                    continue;
                }
                var oldStock = product.StockQuantity;
                success.Add(new SuccessAdjustmentDto
                {
                    productId = product.Id,
                    productName = product.Name,
                    OldStock = oldStock,
                    NewStock = oldStock + adjustment.QuantityChange
                });
                product.StockQuantity = oldStock + adjustment.QuantityChange;
            
        }
        if (success.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
        return Ok(new
        {
            Success = success,
            Failures = failers
        });
    }

}
