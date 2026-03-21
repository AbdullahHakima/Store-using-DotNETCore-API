using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.API.DTOs;
using Store.Application.Interfaces;
using Store.Infrastructure.Presistence;
using Store.API.Extenssions;
using Store.Application.DTOs.Products.Requests;

namespace Store.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    // Direct Injection of the ApplicationDbContext into the ProductController to access the database context and perform CRUD operations on products.
    private readonly IProductService  productService;
    private readonly ApplicationDbContext _db;
    public ProductsController(IProductService productService,ApplicationDbContext db)
    {
        this.productService = productService;
        this._db=db;
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
    //[HttpGet("{id:guid}")]
    //public async Task<IActionResult> GetById(Guid id)
    //{
    //    var product = await _db.Products
    //        .Include(p => p.Category)
    //        .Include(p => p.Tags)
    //        .AsNoTracking()
    //        .FirstOrDefaultAsync(p => p.Id == id);

    //    if (product is null) return NotFound();
    //    return Ok(product);
    //}


    [HttpGet("/{id}/GetById")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id)
    {
        var result = await productService.GetByIdAsync(id);
        return result.ToActionResult(this);
    }
    [HttpGet("search")]
    public async Task<IActionResult> SearchForProduct([FromQuery] ProductSearchRequest productSearch)
    {
        var result = await productService.SearchAsync(productSearch);
        return result.ToActionResult(this);

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



    //[HttpPost("adjust-stock")]
    //public async Task<IActionResult> adjustProductStock([FromBody] List<ProductAdjustment> adjustments)
    //{
    //    if (adjustments == null || adjustments.Count == 0) 
    //        return BadRequest("No adjustments provided.");
    //    var productIds = adjustments.Select(a => a.ProductId);
    //    var products = await _db.Products.Where(p=> productIds.Contains(p.Id)). ToListAsync();

    //    var failers = new List<FailersAdjustment>();
    //    var success = new List<SuccessAdjustmentDto>();
    //    foreach (var adjustment in adjustments)
    //    {
    //        var product = products.FirstOrDefault(p => p.Id == adjustment.ProductId);
    //        if (product is null)
    //        {
    //            failers.Add(new FailersAdjustment
    //            {
    //                productId = adjustment.ProductId,
    //                reason = "Product not found."
    //            });
    //            continue; // Skip to the next adjustment
    //        }
    //            if(adjustment.QuantityChange == 0)
    //            {
    //                failers.Add(new FailersAdjustment
    //                {
    //                    productId = product.Id,
    //                    reason = "No change in stock quantity."
    //                });
    //                continue; // Skip to the next adjustment
    //            }
    //            if (product.StockQuantity + adjustment.QuantityChange < 0)
    //            {
    //                failers.Add(new FailersAdjustment
    //                {
    //                    productId = product.Id,
    //                    reason = $"Insufficient stock for adjustment. Current:{product.StockQuantity}, adjustment:{adjustment.QuantityChange}"
    //                });
    //                continue;
    //            }
    //            var oldStock = product.StockQuantity;
    //            success.Add(new SuccessAdjustmentDto
    //            {
    //                productId = product.Id,
    //                productName = product.Name,
    //                OldStock = oldStock,
    //                NewStock = oldStock + adjustment.QuantityChange
    //            });
    //            product.StockQuantity = oldStock + adjustment.QuantityChange;

    //    }
    //    if (success.Count > 0)
    //    {
    //        await _db.SaveChangesAsync();
    //    }
    //    return Ok(new
    //    {
    //        Success = success,
    //        Failures = failers
    //    });
    //}


    //[HttpGet("search")]
    //public async Task<IActionResult> searchWithFilters([FromQuery] string? name,
    //                                                   [FromQuery] Guid? categoryId,
    //                                                   [FromQuery] decimal? minPrice,
    //                                                   [FromQuery] decimal? maxPrice,
    //                                                   [FromQuery] bool? InStock,
    //                                                   [FromQuery] int pageSize = 10,
    //                                                   [FromQuery] int page = 1)
    //{
    //    pageSize = Math.Clamp(pageSize, 1, 50);// clamp used to restrict the number of products in single page between min value (1) to the max value(50) 
    //                                           // if the user enter more that the max value 50 the clamp automatically set it to 50 and
    //                                           // if the user enter less than the min value 1 the clamp automatically set it to 1
    //    var query = _db.Products.Where(p => p.IsActive)
    //                            .AsQueryable();


    //    if (!string.IsNullOrEmpty(name))
    //        query = query.Where(q => q.Name.Contains(name));// i used contains instead of using equality operator beacasue the quality operator need the user to enter the ful name of the product which is not realistic 
    //                                                        // in searching about products and the contains() is translated to the Sql as LIKE %name% which is more flexible for searching about products by their names
    //    if (categoryId.HasValue)
    //        query = query.Where(q => q.CategoryId == categoryId);
    //    if (minPrice.HasValue)
    //        query = query.Where(q => q.Price >= minPrice);
    //    if (maxPrice.HasValue)
    //        query = query.Where(q => q.Price <= maxPrice);
    //    if (InStock.HasValue)
    //        query = query.Where(q => q.StockQuantity > 0 == InStock.Value);// if the user want to search about products that are in stock the query will filter the products that have stock quantity more than 0
    //                                                                       // and if the user want to search about products that are not in stock the query will filter the products that have stock quantity equal to 0
    //    var totalItems = await query.CountAsync();

    //    var items = await query.OrderBy(p => p.Name)
    //        .Skip((page - 1) * pageSize)
    //        .Take(pageSize)
    //        .Select(p => new
    //        {
    //            p.Id,
    //            p.Name,
    //            p.Price,
    //            Stock = p.StockQuantity,
    //            CategoryName = p.Category.Name,
    //            Tags = p.Tags.Select(t => t.Name).ToList()
    //        }).AsNoTracking().ToListAsync(); // order by name to have a consistent order of products across pages


    //    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

    //    return Ok(new
    //    {
    //        page,
    //        totalItems,
    //        totalPages,
    //        items,
    //    });
    //}


    [HttpPost("adjust-stock")]
    public async Task<IActionResult> AdjustmentStocks([FromBody] List<ProductAdjustment> adjustments)
    {
        // Validate the input
        if (adjustments is null || adjustments.Count == 0) return BadRequest("Adjustments list cannot be null or zero.");


        // load only needed products to minimize the data transfer and memory usage
        var products = await _db.Products.Where(p => adjustments.Select(a => a.ProductId).Contains(p.Id)).ToListAsync();

        #region Alternative way to find the missing product ids without using LINQ's Except method
        //var productIds = products.Select(p => p.Id).ToHashSet();
        //var missingProductIds = (adjustments.Select(a=>a.ProductId)).Except(productIds);
        #endregion
        var failers = new List<FailersAdjustment>();
        var success = new List<SuccessAdjustmentDto>();
        foreach (var adjustment in adjustments)
        {
            //using the if contiue chaining for validate the failers first
            //if the adjustment is vaild will be added to the success list after modify the product stock 

            // fist check if the product exist or not
            var product = products.FirstOrDefault(p => p.Id == adjustment.ProductId);
            if (product == null) continue;

            //second check for the adjsutment value 
            if (adjustment.QuantityChange == 0)
            {
                failers.Add(new FailersAdjustment
                {
                    productId = adjustment.ProductId,
                    reason = "No change in stock quantity."
                });
                continue;
            }

            //thrid check for the product stock if it after add the adjsutment will casue the stock be under Zero or not
            if (adjustment.QuantityChange + product.StockQuantity < 0)
            {
                failers.Add(new FailersAdjustment
                {
                    productId = adjustment.ProductId,
                    reason = $"Insufficient stock for adjustment. Current:{product.StockQuantity}, adjustment:{adjustment.QuantityChange}"
                });
                continue;
            }
            // if the adjustment is vaild we will add it to the success list and modify the product stock

            var oldStock = product.StockQuantity;
            product.StockQuantity += adjustment.QuantityChange;
            var newStock = product.StockQuantity;
            success.Add(new SuccessAdjustmentDto
            {
                productId = product.Id,
                productName = product.Name,
                OldStock = oldStock,
                NewStock = newStock
            });
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

    [HttpGet("GetByIdV1")]
    public async Task<IActionResult> GetByIdWithCompiledQuery(Guid id)
    {
        var product = await CompiledQueries.GetProductById(_db, id);
        if (product is null) return NotFound();
        return Ok(product);
    }
    [HttpGet("{id}/GetByIdV2")]
    public async Task<IActionResult> GetByIdWithCompiledQueryV2([FromRoute] Guid id)
    {
        var product = await _db.Products.Where(p => p.Id == id && p.IsActive)
                                        .Select(p => new ProductDto
                                        {
                                            Id = p.Id,
                                            Name = p.Name,
                                            CategoryName = p.Category.Name,
                                            Price = p.Price,
                                            QuantityInStock = p.StockQuantity
                                        }).FirstOrDefaultAsync();
        if (product is null) return NotFound();
        return Ok(product);
    }
    [HttpGet("GetByCategory")]
    public async Task<IActionResult> GetProductByCategoryId(Guid categoryId)
    {
        var products = new List<ProductDto>();
        await foreach (var product in CompiledQueries.GetProductsByCategoryId(_db, categoryId))
        {
            products.Add(product);
        }
        if (!products.Any()) return NotFound($"there is not products belong to the reference categoryId{categoryId} yet!");
        return Ok(products);
    }


    [HttpPatch("deactivate-by-category/{categoryId}")]
    public async Task<IActionResult> DeactivateProductsByCategory([FromRoute]Guid categoryId)
    {
        int affectedRows = await _db.Products.Where(p => p.CategoryId == categoryId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
        
        return Ok(new
        {
            NumberOfRowsAffected = affectedRows,
        });
    }
    [HttpPatch("apply-discount/{categoryId}/v1")]
    public async Task<IActionResult> ApplyDiscountv1([FromRoute] Guid categoryId,
                                                   [FromBody] decimal percentage)
    {
        if (percentage <= 0 || percentage >= 100)
            return BadRequest("Percentage must be between 0 and 100.");
        var products = await _db.Products.Where(p => p.CategoryId == categoryId && p.IsActive).ToListAsync();
        if (!products.Any()) return NotFound($"there is not active products belong to the reference categoryId{categoryId} yet!");
        decimal discount = percentage / 100;
        foreach (var product in products)
        {
            product.Price *= (1 -  discount);// percentage:10=> price:572 totalDiscount=> price-= price * (10/100)
                                                                         // price-= price*0.1 => price= price - 0.1*price => price= 0.9*price 
        }
        int numberOfRowsAffected = await _db.SaveChangesAsync();
        return Ok(new
        {
            NumberOfRowsAffected = numberOfRowsAffected,
        });
    }
    [HttpPatch("apply-discount/{categoryId}/v2")]
    public async Task<IActionResult> ApplayDiscountV2([FromRoute] Guid categoryId,
                                                      [FromBody] decimal percentage)
    {
        if (percentage <= 0 || percentage >= 100)
            return BadRequest("Percentage must be between 0 and 100.");
        decimal discount = percentage / 100;
        int affectedRows = await _db.Products.Where(p => p.CategoryId == categoryId && p.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty
            (p => p.Price,
            p=>p.Price*(1-discount)// percentage:10=> price:572 totalDiscount=> price-= price * (10/100)
                                   // price-= price*0.1 => price= price - 0.1*price => price= 0.9*price
            ));
        // i don't need to use SaveChangesAsync() because the ExecuteUpdateAsync method already execute the update query directly in the database
        // without loading the entities into memory and tracking them
        return Ok(new
        {
            numberOfRowsAffected=affectedRows,
        });

    }

    [HttpDelete("hard-delete-inactive")]
    public async Task<IActionResult> HardDeleteInActiveProduct()
    {
        // find all OrderItems first beacuse of the constraints on the products table is ristict on delete
        // and if i try to delete the products without delete the related order items first i will get an exception
        // because of the foreign key constraint

        await using var transaction = await _db.Database.BeginTransactionAsync();
        {
            try
            {
                await _db.OrderItems.Where(oi => !oi.Product.IsActive).ExecuteDeleteAsync();

                // This method will delete all products that are not active (IsActive = false) from the database.
                int numberOfDeleteProducts = await _db.Products.Where(p => !p.IsActive).ExecuteDeleteAsync();
                await transaction.CommitAsync();
                return Ok(new
                {
                    TotalCountOfDeleteProducts = numberOfDeleteProducts
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while deleting products: {ex.Message}");
            }
        }
        
    }


}


