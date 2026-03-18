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

    //[HttpGet("GetPaymentDetails")]
    //public async Task<IActionResult> GetPaymant(Guid PaymentId)
    //{
        

    //}
    [HttpPost("{id}/Payments")]
    public async Task<IActionResult> Payments([FromRoute] Guid Id,
                                              [FromBody] AddNewPaymentDTO paymentDTO)
    {
        var order = await _db.Orders.Include(o=>o.Payments).FirstOrDefaultAsync(o => o.Id == Id);
        if (order is null)
            return NotFound($"The order with id:{Id} is not exist!");
        if (order.Status != OrderStatus.Confirmed)
            return BadRequest("Can not make a payment for not confirmed orders");
        if (paymentDTO.Amount <= 0) return BadRequest("Can not make a payment with amount nagative or zero");

        if(!Enum.TryParse<PaymentMethod>(paymentDTO.method,ignoreCase:true,out PaymentMethod method))
            return BadRequest($"The payment method {paymentDTO.method} is not supported!");

        decimal alreadyPaid = order.Payments.Sum(p => p.Amount);
        decimal amountRemaining = order.TotalAmount - alreadyPaid;
        decimal TotalAfterPayment= alreadyPaid+paymentDTO.Amount;
        if (paymentDTO.Amount > amountRemaining) 
            return BadRequest(new
            {
                eror=$"The payment can be exceeds the order amount:{amountRemaining}",
                AmoutRemaning = amountRemaining
            });

        order.IsPaid = TotalAfterPayment >= order.TotalAmount;
        var payment = new Payment
        {
            Amount = paymentDTO.Amount,
            Method = method,
            RefrenceCode = paymentDTO.ReferenceCode,
            OrderId = order.Id,
            PaidAt=DateTime.UtcNow,
        };
        await _db.Payments.AddAsync(payment);
        await _db.SaveChangesAsync();
        return Ok(new
        {
            PaymentId = payment.Id,
            OrderId = order.Id,
            AmountPaid = paymentDTO.Amount,
            AmountRemaining = amountRemaining,
            order.IsPaid
        });
    }
}
