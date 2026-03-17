using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Domain.Entities;
using Store.Infrastructure.Presistence;
using System.Diagnostics;

namespace Store.API.Controllers;

public class QueryLabController : Controller
{
    private readonly ApplicationDbContext _db;
    public QueryLabController(ApplicationDbContext context)
    {
        _db = context;
    }

    [HttpGet("deferred")]
    public async Task<IActionResult> DeferredDemo([FromQuery] decimal? minPrice, [FromQuery] bool? activeOnly)
    {
        var query = _db.Products.AsQueryable();
        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);
        if (activeOnly == true)
            query = query.Where(p => p.IsActive);
        return Ok(await query.Select(p =>
        new
        {
            p.Id, p.Name, p.Price
        }).ToListAsync()
        );
    }
    [HttpGet("eager")] public async Task<IActionResult> EagerDemo() { var orders = await _db.Orders.Include(o => o.Customer).Include(o => o.Items).ThenInclude(i => i.Product).Include(o => o.Payments).AsNoTracking().ToListAsync(); return Ok(orders); }
    [HttpGet("projection")]
    public async Task<IActionResult> ProjectionDemo()
    {
        var result = await _db.Orders.Select(o =>
        new
        {
            o.OrderNumber,
            o.Status,
            CustomerName = o.Customer.Name,
            ItemCount = o.Items.Count(),
            TotalAmount = o.Items.Sum(i => i.UnitPrice * i.Quantity),
            ProductNames = o.Items.Select(i => i.Product.Name)
        }).ToListAsync();
        return Ok(result);
    }
    [HttpGet("aggregation")]
    public async Task<IActionResult> AggregationDemo()
    {
        var result = await _db.Products.GroupBy(p => p.Category.Name)
            .Select(g =>
            new {
                Category = g.Key,
                Count = g.Count(),
                AvgPrice = g.Average(p => p.Price),
                TotalStock = g.Sum(p => p.StockQuantity)
            }).OrderByDescending(x => x.Count).ToListAsync();
        return Ok(result);
    }
    [HttpGet("paged")]
    public async Task<IActionResult> PagedDemo([FromQuery] int page = 1, [FromQuery] int pageSize = 2)
    {
        var query = _db.Products.Where(p => !p.IsActive).OrderBy(p => p.Price);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .Select(p =>
                               new
                               {
                                   p.Name, p.Price
                               })
                               .ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }
   // [HttpGet("search")] public async Task<IActionResult> SearchDemo([FromQuery] string term = "") { var results = await _db.Products.Where(p => p.Name.Contains(term) || p.Category.Name.Contains(term)).Select(p => new { p.Name, Category = p.Category.Name, p.Price }).ToListAsync(); return Ok(results); }



    [HttpGet("search")]
    public async Task<IActionResult>Search([FromQuery]string? name,
                                            [FromQuery]Guid? categoryId,
                                            [FromQuery] decimal? minPrice,
                                            [FromQuery] decimal? maxPrice,
                                            [FromQuery] bool? inStockOnly,
                                            [FromQuery]int pageNumber=1,
                                            [FromQuery]int pageSize=10)
    {

        pageSize = Math.Min(pageSize, 50);// is a way to make the user not leak all products in one page
        var query= _db.Products
            .Where(p=>p.IsActive)
            .AsQueryable(); // here just make the Query ready when i use it in filterations 


        if(!string.IsNullOrEmpty(name))
         query = query.Where(p => p.Name.Contains(name));
        if(categoryId != null)
            query=query.Where(p=>p.CategoryId == categoryId);
        if(minPrice != null)
            query=query.Where(p=>p.Price >= minPrice.Value);
        if(maxPrice != null)
            query=query.Where(p=>p.Price <= maxPrice.Value);
        if(inStockOnly is true)
        {
            query = query.Where(p => p.StockQuantity > 0);
        }

        var totalCount= await query.CountAsync();

        var items= await query
            .OrderBy(q=>q.Name)
            .Skip((pageNumber-1)*pageSize)
            .Take(pageSize)
            .Select(q=> new
        {
            q.Id,
            q.Name,
            Category =q.Category.Name,
            Stock=q.StockQuantity,
            q.Price,
            Tags=q.Tags.Select(t=>t.Name).ToList(),
        }).AsNoTracking().ToListAsync();


        return Ok(new
        {
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            CurrentPage=pageNumber,
            Items = items,
        });
    }
}
