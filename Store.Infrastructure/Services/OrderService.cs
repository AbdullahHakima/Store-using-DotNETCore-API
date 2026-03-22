using Microsoft.EntityFrameworkCore;
using Store.Application.Common;
using Store.Application.DTOs.Orders.Requests;
using Store.Application.DTOs.Orders.Responses;
using Store.Application.Interfaces;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Presistence;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Store.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext context;
    public OrderService(ApplicationDbContext context) => this.context = context;

    public async Task<Result<OrderConfirmResponse>> ConfirmWithStockAsync(Guid id)
    {
        //this about confirming the painding order which the pipline of the orders is once it be created take the paining status 
        //then the system check for the orders items in inventory stocks which ensure each product in the order item has sufficient stock for the painding orders
        // first load the order and its items and item products 
        // i will use the split query ? no beacuse of the query has 3 objects which are order - orderItems - Products 
        //for very rear case that orders has +10 orderItems so i will use the Single Query for this case 

        //loading the order and realted navigations properties 
        var order = await context.Orders.Where(o => o.Id == id)
                                        .Include(o => o.Items).ThenInclude(i => i.Product).SingleOrDefaultAsync();
        //check for the order exsit
        if (order is null) return Result<OrderConfirmResponse>.NotFound($"there is no order founded has Id:{id}");

        //check for order status which can confirm order unless it has status Panding
        if (order.Status != OrderStatus.Pending) return Result<OrderConfirmResponse>.BadRequest($"can not confirm order has already:{order.Status}");

        var insfficients = new List<InsufficientProductStock>();
        //so now should make sure each items in order has sufficient quantities 
        foreach(var item in order.Items)
        {
            //there is three cases here 
            // 1- product's item out of stock
            if (item.Product.StockQuantity == 0)
            {
                insfficients.Add(new InsufficientProductStock 
                { 
                    ProductId = item.ProductId,
                    reason = $"The {item.Product.Name} is out of stock!"
                });
                continue;
            }
            // 2- product stock is low than the order's item product quantity
            if (item.Quantity > item.Product.StockQuantity)
            {
                insfficients.Add(new InsufficientProductStock
                {
                    ProductId = item.ProductId,
                    reason = $"The {item.Product.Name} has insufficient stock => current stock:{item.Product.StockQuantity} - needed quantity:{item.Quantity}"
                });
                continue; 
            }
            // here is not i good way to change the data that has a sensitive for consistency and intergrity which
            //i do not know how many people try to confirm ordres with same items in simultenously and can not confirm the order now also
            // so the appropirate way is using the transaction which it more secure for the integrity
            //// 3- (the vaild case) the product's stock has vaild quantity for the order's item product quantity
            //item.Product.StockQuantity -= item.Quantity;
        }
        // check for insufficient product so the order can be confirmed
        if (insfficients.Count()>0) return Result<OrderConfirmResponse>.BadRequest(string.Empty, new OrderConfirmResponse
        {
            OrderNumber=order.OrderNumber,
            OrderId=order.Id,
            items=null,
            movements=null,
            Status=order.Status.ToString(),// should i change the order status to be cancelled but i let it for now  
            insufficients = insfficients
        });
        // here evey things is okay so sould i make tha 3 things which are 
        //1- change the order status 
        //2- reduce the current stock inventory by the order items quantities 
        //3- make the stock movement record to track the stocks changes later
        var moves = new List<StockMovement>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // 1- consirm the order 
            order.Status = OrderStatus.Confirmed;
            await context.SaveChangesAsync();
            // 2- reduce the stocks 
            foreach(var item in order.Items)
            {
                item.Product.StockQuantity -= item.Quantity;
            }
            await context.SaveChangesAsync();
            //3-  make the movement record
            await transaction.CreateSavepointAsync("StockReduced");
            moves.AddRange(order.Items.Select(i => new StockMovement
            {
                ProductId = i.ProductId,
                Product = i.Product,
                OrderId = i.Order.Id,
                Order = i.Order,
                QuantityChange = -i.Quantity,
                MovementData = DateTime.UtcNow,
                Reason = $"order:{order.OrderNumber} has confirmed.",

            }));
            await context.AddRangeAsync(moves);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackToSavepointAsync("StockReduced");
            await transaction.CommitAsync();

        }
        var response = new OrderConfirmResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            items = order.Items.Select(i => new ItemsProcessed
            {
                ProductName = i.Product.Name,
                Quantity = i.Quantity
            }),
            IsMovementCreated=moves.Any(),
            movements = moves.Select(m=>new Movement
            {
                ProductName=m.Product.Name,
                QuantityChange=m.QuantityChange,
                Reason=m.Reason
            }),
        };
        return Result<OrderConfirmResponse>.Success(response);

    }





    public async Task<Result<OrderCreateResponse>> CreateAsync(OrderCreateRequest request)
    {
        //check is the customer who make the order is exsit or not 
        var customer = await context.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CustomerId);
        if (customer is null) return Result<OrderCreateResponse>.NotFound($"Customer {request.CustomerId} not found.");


        ////check for vaild orderItems from the request
        //if (!request.Items.Any()) return Result<OrderCreateResponse>.BadRequest("there is no items to make the order!");
        var Items = new List<OrderItem>();
        var failuers = new List<OrderItemsFailuer>();
        var orderItems = new List<OrderItemsSuccess>();
        var productIds =  request.Items.Select(i => i.ProductId).ToList();
        //here i hit the db by one query and load all products in the request.items
        var products = await context.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        foreach (var item in request.Items)
        {
            // here check if the product has enough StockQuantity to make this order 
            //if product is null so should add it to the Failuers List 
            // !!! here i hit the db every travers so in N+1 hits this is bad !!!
            //var product = await context.Products/*.Select(p=> new {p.Id, p.StockQuantity})*/.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==item.ProductId);
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null)
            {
                failuers.Add(new OrderItemsFailuer
                {
                    ProductId = item.ProductId,
                    Reason = $"Product you tring to add not founded!"
                });
                continue;
            }
            if(product.StockQuantity == 0)
            {
                 failuers.Add(new OrderItemsFailuer
                    {
                      ProductId = product.Id,
                      Reason = $"product is out of stock!"
                 });
                 continue;
                
            }
            if (product.StockQuantity < item.Quantity)
            {
                failuers.Add(new OrderItemsFailuer
                {
                    ProductId = product.Id,
                    Reason = $"product has not enough stock for sell current stock:{product.StockQuantity} - needed from stock:{item.Quantity}"
                });
                continue;
            }


                // this means will be continue while just drop the out of stock products and low stock quantities
                Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice=item.UnitPrice,
            });
            orderItems.Add(new OrderItemsSuccess
            {
                ProductName = product.Name,
                Qunatity = item.Quantity,
                TotalPrice = item.Quantity * product.Price,
            });

        }
        if (!Items.Any()) return Result<OrderCreateResponse>.BadRequest("All order products are out of stock");
        var lastOrderNumber = await context.Orders.CountAsync();
        var order = new Order
        {
            OrderNumber = $"ORD-{(lastOrderNumber + 1):D4}",
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CustomerId = request.CustomerId,
            IsPaid = false,
            Items =Items 
        };
        
        await context.Orders.AddAsync(order);
        await context.SaveChangesAsync();
        var Response = new OrderCreateResponse
        {
            OrderNumber = order.OrderNumber,
            TotalToPay = orderItems.Sum(oi=> oi.TotalPrice),
            itemsSuccesses=orderItems,
            itemFailuers = failuers,
        };
        return Result<OrderCreateResponse>.Success(Response);
    }

    public async Task<Result<OrderDetailResponse>> GetByIdAsync(Guid id)
    {
        //load the order with the associated properties 
        var order = await context.Orders.Where(o => o.Id == id).OrderBy(o=>o.OrderDate).Select(o => new OrderDetailResponse
        {
            Id = o.Id,
            CustomerName = o.Customer.Name,
            Items = o.Items.Select(i => new ItemSummary { ProductName = i.Product.Name, Quantity = i.Quantity, TotalPrice = i.LineTotal }).ToList(),
            Payments = o.Payments.Select(p => new PaymentSummary
            { Amount = p.Amount, Method = p.Method.ToString(), RefrenceCode = p.RefrenceCode, PaidAt = p.PaidAt }).ToList(),
            OrderDate = o.OrderDate,
            OrderNumber = o.OrderNumber,
            TotalAmount = o.TotalAmount,
            IsPaid=o.IsPaid    
        }).AsNoTracking().SingleOrDefaultAsync();
        // check for the order exist
        if (order is null)
            return Result<OrderDetailResponse>.NotFound($"their is no orders founded By Id:{id}");
        return Result<OrderDetailResponse>.Success(order);
        
    }
}
