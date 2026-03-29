using Microsoft.AspNetCore.Mvc;
using Moq;
using Store.API.Controllers;
using Store.API.DTOs;
using Store.API.Tests.Common;
using Store.Application.Common;
using Store.Application.DTOs.Orders.Requests;
using Store.Application.DTOs.Orders.Responses;
using Store.Application.Interfaces;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Domain.ValueObjects;

namespace Store.API.Tests.Controllers;

public class OrderControllerTests
{
    [Fact]
    public async Task CreateOrder_ReturnsOk_WhenApplicationServiceSucceeds()
    {
        using var db = TestDbContextFactory.Create();
        var service = new Mock<IOrderService>();
        var request = new OrderCreateRequest { CustomerId = Guid.NewGuid(), Items = [] };

        service.Setup(s => s.CreateAsync(request)).ReturnsAsync(Result<OrderCreateResponse>.Success(new OrderCreateResponse
        {
            OrderNumber = "ORD-100",
            TotalToPay = 10
        }));

        var controller = new OrderController(db, service.Object);

        var result = await controller.CreateOrder(request);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ProcessPayment_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        using var db = TestDbContextFactory.Create();
        var controller = new OrderController(db, Mock.Of<IOrderService>());

        var result = await controller.ProcessPayment(Guid.NewGuid(), new PaymentDto { Amount = 10, Method = "Cash" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ProcessPayment_ReturnsBadRequest_WhenOrderNotConfirmed()
    {
        using var db = TestDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Alex",
            Email = "alex@example.com",
            Address = new Address("Street", "City", "US"),
            RowVersion = [1],
            CreatedAt = DateTime.UtcNow
        });

        db.Orders.Add(new Order
        {
            Id = orderId,
            OrderNumber = "ORD-200",
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            TotalAmount = 100,
            RowVersion = [1],
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new OrderController(db, Mock.Of<IOrderService>());

        var result = await controller.ProcessPayment(orderId, new PaymentDto { Amount = 10, Method = "Cash" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task OrdersSummary_ReturnsOkWithAggregatedData()
    {
        using var db = TestDbContextFactory.Create();
        var customerId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "A",
            Email = "a@example.com",
            Address = new Address("Street", "City", "US"),
            RowVersion = [1],
            CreatedAt = DateTime.UtcNow
        });

        db.Orders.AddRange(
            new Order { Id = Guid.NewGuid(), OrderNumber = "S-1", CustomerId = customerId, Status = OrderStatus.Pending, TotalAmount = 50, IsPaid = false, RowVersion = [1], CreatedAt = DateTime.UtcNow },
            new Order { Id = Guid.NewGuid(), OrderNumber = "S-2", CustomerId = customerId, Status = OrderStatus.Confirmed, TotalAmount = 75, IsPaid = true, RowVersion = [1], CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = new OrderController(db, Mock.Of<IOrderService>());

        var result = await controller.OrdersSummary();

        Assert.IsType<OkObjectResult>(result);
    }
}
