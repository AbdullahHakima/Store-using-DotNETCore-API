using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Infrastructure.Presistence;

namespace Store.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public ReportsController(ApplicationDbContext context) {
        _context = context;
        }

        [HttpGet("customers")]
        public async Task<IActionResult> CustomerSalesReport([FromQuery] DateTime? from,
                                                             [FromQuery] DateTime? to,
                                                             [FromQuery]int pageSize=10,
                                                             [FromQuery]int pageNumber=1)
        {


            pageSize = Math.Clamp(pageSize, 1, 50);

            var query = _context.Customers.Where(c => c.Orders.Any())
                                         .OrderByDescending(c => c.Orders.Sum(o => o.TotalAmount));

            var totalCount= await query.CountAsync();

            var items = await query.Skip((pageNumber - 1) * pageSize)
                             .Take(pageSize)
                             .Select(q => new
            {
                customerId = q.Id,
                customerName = q.Name,
                customerEmail = q.Email,
                totalOrders = q.Orders.Count(o=>(!from.HasValue||o.OrderDate>= from)&&(!to.HasValue||o.OrderDate<=to)),
                totalSpend = q.Orders.Where(o => (!from.HasValue || o.OrderDate >= from) && (!to.HasValue || o.OrderDate <= to)).Sum(o => o.TotalAmount),
                averageOrderValue = Math.Round(q.Orders.Where(o => (!from.HasValue || o.OrderDate >= from) && (!to.HasValue || o.OrderDate <= to)).Average(o => o.TotalAmount), 2),
                lastOrderDate = q.Orders.Where(o => (!from.HasValue || o.OrderDate >= from) && (!to.HasValue || o.OrderDate <= to)).Max(o => o.CreatedAt),
                fullyPaidOrders = q.Orders.Count(o => o.IsPaid&& (!from.HasValue || o.OrderDate >= from) && (!to.HasValue || o.OrderDate <= to)),
            }).AsNoTracking().ToListAsync();
          
            return Ok(new
            {
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                currentPage=pageNumber,
                items,
            });
                
        }

    }
}
