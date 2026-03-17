using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    [HttpPost("/api/orders/{id}/Payments")]
    public async Task<IActionResult> Payments([FromQuery] Guid orderId,
                                        [FromBody]decimal amount,
                                        [FromBody]string method,
                                        string? referenceCode)
    {
        var order = _db.Orders.SingleOrDefault(q => q.Id == orderId);
        if (order is null)
            return NotFound($"The order with id:{orderId} is not exist!");
        if (order.Status != OrderStatus.Confirmed)
            return BadRequest("Can not make a payment for not confirmed orders");
        if (amount <= 0) return BadRequest("Can not make a payment with amount nagative or zero");
        if (amount > order.TotalAmount) return BadRequest($"The payment can be exceeds the order amount:{order.TotalAmount}");

     using var transaction = _db.Database.BeginTransaction();
        try
        {
            order.TotalAmount -= amount;
            if (order.TotalAmount == 0) order.IsPaid = true;
            Payment newPayment = new Payment()
            {
                OrderId = orderId,
                Amount = amount,
                Order = order,
                Method = Enum.Parse<PaymentMethod>(method),
                PaidAt = DateTime.UtcNow,
            };

            _db.Payments.Add(newPayment);
            _db.SaveChanges();
            transaction.Commit();
            return Ok(new
            {
                PaymentId = newPayment.Id,
                OrderId=orderId,
                AmountPaid=newPayment.Amount,
                RemaniningBalance= order.TotalAmount,
                order.IsPaid
            });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return BadRequest(ex.Message );
        }
        
    }
}
