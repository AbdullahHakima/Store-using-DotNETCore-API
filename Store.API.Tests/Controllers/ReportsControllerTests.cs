using Microsoft.AspNetCore.Mvc;
using Store.API.Controllers;
using Store.API.Tests.Common;
using Store.Domain.Entities;
using Store.Domain.ValueObjects;

namespace Store.API.Tests.Controllers;

public class ReportsControllerTests
{
    [Fact]
    public async Task CustomerSalesReport_ReturnsOkReport()
    {
        using var db = TestDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Jane",
            Email = "jane@example.com",
            Address = new Address("Street", "City", "US"),
            RowVersion = [1],
            CreatedAt = DateTime.UtcNow
        });

        db.Orders.Add(new Order
        {
            Id = orderId,
            OrderNumber = "ORD-1",
            CustomerId = customerId,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 120,
            IsPaid = true,
            RowVersion = [1],
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var controller = new ReportsController(db);

        var result = await controller.CustomerSalesReport(null, null, 1, 10);

        Assert.IsType<OkObjectResult>(result);
    }
}
