using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.API.DTOs;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Presistence;
using System.Net.NetworkInformation;
using System.Transactions;

namespace Store.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public OrderController(ApplicationDbContext db)
    {
        _db = db;
    }


    //[HttpPost("{id}/Payments")]
    //public async Task<IActionResult> Payments([FromRoute] Guid Id,
    //                                          [FromBody] AddNewPaymentDTO paymentDTO)
    //{
    //    var order = await _db.Orders.Include(o=>o.Payments).FirstOrDefaultAsync(o => o.Id == Id);
    //    if (order is null)
    //        return NotFound($"The order with id:{Id} is not exist!");
    //    if (order.Status != OrderStatus.Confirmed)
    //        return BadRequest("Can not make a payment for not confirmed orders");
    //    if (paymentDTO.Amount <= 0) return BadRequest("Can not make a payment with amount nagative or zero");

    //    if(!Enum.TryParse<PaymentMethod>(paymentDTO.method,ignoreCase:true,out PaymentMethod method))
    //        return BadRequest($"The payment method {paymentDTO.method} is not supported!");

    //    decimal alreadyPaid = order.Payments.Sum(p => p.Amount);
    //    decimal amountRemaining = order.TotalAmount - alreadyPaid;
    //    decimal TotalAfterPayment= alreadyPaid+paymentDTO.Amount;
    //    if (paymentDTO.Amount > amountRemaining) 
    //        return BadRequest(new
    //        {
    //            eror=$"The payment can be exceeds the order amount:{amountRemaining}",
    //            AmoutRemaning = amountRemaining
    //        });

    //    order.IsPaid = TotalAfterPayment >= order.TotalAmount;
    //    var payment = new Payment
    //    {
    //        Amount = paymentDTO.Amount,
    //        Method = method,
    //        RefrenceCode = paymentDTO.ReferenceCode,
    //        OrderId = order.Id,
    //        PaidAt=DateTime.UtcNow,
    //    };
    //    await _db.Payments.AddAsync(payment);
    //    await _db.SaveChangesAsync();
    //    return Ok(new
    //    {
    //        PaymentId = payment.Id,
    //        OrderId = order.Id,
    //        AmountPaid = paymentDTO.Amount,
    //        AmountRemaining = amountRemaining,
    //        order.IsPaid
    //    });
    //}

    [HttpGet("summary")]
    public async Task<IActionResult> OrdersSummary()
    {
        var summary = await _db.Orders.GroupBy(g => g.Status).Select(g => new
        {
            Status = g.Key.ToString(),
            totalOrders = g.Count(),
            TotalRevenue = g.Where(o => o.IsPaid).Sum(o => o.TotalAmount),
            totalCountUnPaid = g.Count(o => !o.IsPaid),
        }).AsNoTracking().ToListAsync();
        return Ok(summary);

    }

    [HttpPost("{id}/payment")]
    public async Task<IActionResult> ProcessPayment([FromRoute] Guid id,
                                                [FromBody] PaymentDto paymentDto)
    {
        //check if the referenced order exists
        var order = await _db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound($"Order with id {id} not found.");

        //check for the order status which must be confirmed before making payment
        if (order.Status != OrderStatus.Confirmed)
            return BadRequest("Payment can only be processed for confirmed orders.");

        //check for the payment amount which must be positive and not exceed the remaining amount
        //

        if (paymentDto.Amount <= 0) return BadRequest("Payment amount must be greater than zero.");
        var totalPaid = order.Payments.Sum(p => p.Amount);

        //check if the order is already fully paid
        if (order.IsPaid)
            return BadRequest("The order is already fully paid.");


        var remainingBalance = order.TotalAmount - totalPaid;
        //check if the payment amount exceeds the remaining balance
        if (paymentDto.Amount > remainingBalance)
            return BadRequest(new
            {
                error = $"Payment amount exceeds the remaining balance.",
                RemainingBalance = remainingBalance
            });


        var payment = new Payment
        {
            Amount = paymentDto.Amount,
            Method = Enum.TryParse<PaymentMethod>(paymentDto.Method, ignoreCase: true, out var method) ? method : throw new ArgumentException("Invalid payment method."),
            RefrenceCode = paymentDto.ReferenceCode,
            OrderId = order.Id,
            PaidAt = DateTime.UtcNow,
        };

        order.IsPaid = paymentDto.Amount + totalPaid >= order.TotalAmount;

        await _db.Payments.AddAsync(payment);

        return Ok(new
        {
            PaymentId = payment.Id,
            OrderId = payment.Order.Id,
            AmountPaid = payment.Amount,
            RemainingBalance = order.TotalAmount - (paymentDto.Amount + totalPaid),
            order.IsPaid,
        });
    }

    // here i use single Query which is working as cartisan product
    // so if the order has 3 items and 2 payments the query will return 6 rows with duplicated data for the order and customer
    // but it is useful when you have small data in the collections and if you inside transaction it will be good to use single query
    // to avoid data inconsistency issues between the multiple queries in the split query

    [HttpGet("{id}/detailsV1")]
    public async Task<IActionResult> GetOrderDetailsv1([FromRoute] Guid id)
    {
        // check if the referenced order exists
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound($"Order with id {id} not found.");

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.OrderDate,
            order.TotalAmount,
            order.IsPaid,
            CustomerName = order.Customer.Name,
            Items = order.Items.Select(i => new
            {
                ProductName = i.Product.Name,
                i.Quantity,
                TotalPrice = i.Quantity * i.UnitPrice
            }),
            Payments = order.Payments.Select(p => new
            {
                p.Id,
                p.Amount,
                p.Method,
                p.RefrenceCode,
                p.PaidAt
            })
        });

    }

    // here i make same Query as detailsV1 but with AsSplitQuery
    // to avoid cartesian explosion problem when we have multiple collections in the same query
    // but for this sample of data the version one is useful 
    // use the SplitQury when you have
    // 1- multiple collections in the same query
    // 2- large data in the collections which can cause performance issues when loading all data in one query
    // 3- you want to optimize the query performance by reducing the amount of data loaded in memory at once
    // do not use SplitQuery when you have
    // 1- small data in the collections which can be loaded in one query without performance issues
    //2- inside a transaction scope where the Db status could changed between the multiple queries which can cause data inconsistency issues
    //3- using the Select() which do not need to load the whole entity columns it just query about what you asking for..
    [HttpGet("{id}/detailsV2")]
    public async Task<IActionResult> GetOrderDetailsv2([FromRoute] Guid id)
    {
        // check if the referenced order exists

        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound($"Order with id {id} not found.");
        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.OrderDate,
            order.TotalAmount,
            order.IsPaid,
            CustomerName = order.Customer.Name,
            Items = order.Items.Select(i => new
            {
                ProductName = i.Product.Name,
                i.Quantity,
                TotalPrice = i.Quantity * i.UnitPrice
            }),
            Payments = order.Payments.Select(p => new
            {
                p.Id,
                p.Amount,
                p.Method,
                p.RefrenceCode,
                p.PaidAt
            })
        });
    }


    [HttpGet("{id}/detailsV3")]
    public async Task<IActionResult> GetOrderDetails([FromRoute] Guid id)
    {

        var order = await _db.Orders
            .Where(o => o.Id == id).
            Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.OrderDate,
                o.TotalAmount,
                o.IsPaid,
                CustomerName = o.Customer.Name,
                Items = o.Items.Select(i => new
                {
                    ProductName = i.Product.Name,
                    i.Quantity,
                    TotalPrice = i.Quantity * i.UnitPrice
                }),
                Payment = o.Payments.Select(p => new
                {
                    p.Id,
                    p.Amount,
                    p.Method,
                    p.RefrenceCode,
                    p.PaidAt
                })

            }
            ).AsNoTracking().FirstOrDefaultAsync();

        // check if the referenced order exists
        if (order is null) return NotFound($"Order with id {id} not found.");
        return Ok(order);

    }


    [HttpPost("{id}/confirm-with-stock")]
    public async Task<IActionResult> ConfirmOrderWithStockCheck([FromRoute] Guid id)
    {
        // load order with its Items and the related products with their stock quantity in one query to avoid data inconsistency
        var order = await _db.Orders.Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
        //check for the order exsit 
        if (order is null) return NotFound($"Order with id {id} not found.");

        //check for order status which must be pending to be confirmed
        if (order.Status != OrderStatus.Pending)
            return BadRequest("Only pending orders can be confirmed.");

        var insufficientProducts = new List<InsufficientProductsDto>();
        // check for the order Items product stock sufficeincy 
        foreach (var item in order.Items)
        {
            //Product.StockQuantity is the current stock quantity for the product in the database 
            //item.Quantity is the quantity of the product in the order which we want to confirm
            if (item.Product.StockQuantity < item.Quantity)
                insufficientProducts.Add(new InsufficientProductsDto
                {
                    ProductId = item.ProductId,
                    Reason = $"Insufficient stock for product {item.Product.Name}. Available: {item.Product.StockQuantity}, Required: {item.Quantity}"
                });
        }
        if (insufficientProducts.Any())
            return BadRequest(new { InsufficientProducts = insufficientProducts });

        // after confirming the order we need to update the stock quantity for the products in the order items
        //create a list of stockmovement records for the products in the order items to record the stock quantity movement for each product in the order items
        var stockMovements= new List<StockMovement>();
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            //there are three things very improtant to be in the same transaction to avoid data inconsistency issues
            //first confirm the order by updating the order status to confirmed

            order.Status = OrderStatus.Confirmed;
            await _db.SaveChangesAsync();
            //second update the stock quantity for the products in the order items 
            //by dicreasing the stock quantity by the order item quantity for each product in the order items 
            //so should traverse the order items and for each item product will decrease the stock quantity by the item quantity
            foreach(var item in order.Items)
            {
                item.Product.StockQuantity= item.Product.StockQuantity-item.Quantity;
            }
            await _db.SaveChangesAsync();

            await transaction.CreateSavepointAsync("StockReduced");
            // make the StockMovement which is the record for the stock quantity movement for each product in the order items
            stockMovements = order.Items.Select(i => new StockMovement
            {
                ProductId = i.ProductId,
                OrderId = order.Id,
                Order = order,
                QuantityChange = -i.Quantity,
                Reason = $"Order {order.OrderNumber}  Confirmed",
                MovementData = DateTime.UtcNow,
                Product = i.Product
            }).ToList();
            await _db.StockMovements.AddRangeAsync(stockMovements);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackToSavepointAsync("StockReduced");
            // by calling transaction.commitAsync() means commit the successful operation before the savePoint so we not lose the updating for both 
            // order status and the stock quantity for the products in the order items but we just lose the creating for the stock movement records
            // which is not critical as we have the data for the order and the products to create the stock movement records later if we want to do so
            await transaction.CommitAsync();
        }
        return Ok(new
        {
            OrderId = order.Id,
            order.OrderNumber,
            Status = order.Status.ToString(),
            ItemsProcessed = order.Items.Select(i => new {
                ProductName = i.Product.Name,
                i.Quantity,
            }),
            MovementRecordsCreated= stockMovements.Select(s => new {
                s.Id,
                s.QuantityChange,
                s.Reason,
            })
        }); 
    }
}
