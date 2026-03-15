using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Infrastructure.Presistence;

namespace Store.API.Controllers
{
    public class ProductController : Controller
    {
        // Direct Injection of the ApplicationDbContext into the ProductController to access the database context and perform CRUD operations on products.
        private readonly ApplicationDbContext _db;
        public ProductController(ApplicationDbContext context)
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
    }
}
