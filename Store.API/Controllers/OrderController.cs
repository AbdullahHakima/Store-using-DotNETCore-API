using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.API.DTOs;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Presistence;
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
        var summary= await _db.Orders.GroupBy( g=> g.Status).Select(g=> new {
            Status = g.Key.ToString(),
            totalOrders=g.Count(),
            TotalRevenue = g.Where(o=>o.IsPaid).Sum(o=>o.TotalAmount),
            totalCountUnPaid=g.Count(o=>!o.IsPaid),
        }).AsNoTracking().ToListAsync();
        return Ok(summary);

    }

    [HttpPost("{id}/payment")]
    public async Task<IActionResult> ProcessPayment([FromRoute] Guid id,
                                                [FromBody] PaymentDto paymentDto)
    {
        //check if the referenced order exists
        var order = await _db.Orders.Include(o=>o.Payments).FirstOrDefaultAsync(o=>o.Id==id);
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
            return BadRequest(new { 
                error=$"Payment amount exceeds the remaining balance.",
                RemainingBalance = remainingBalance });


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
            RemainingBalance = order.TotalAmount- (paymentDto.Amount+totalPaid),
            order.IsPaid,
        });
    }
}
