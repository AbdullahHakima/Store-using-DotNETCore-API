using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.DTOs.Orders.Responses;

public record OrderDetailResponse
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; }
    public string CustomerName { get; init; }
    public DateTime OrderDate { get; init; }
    public decimal TotalAmount { get; init; }
    public IEnumerable<ItemSummary> Items { get; init; }

    public IEnumerable<PaymentSummary> Payments { get; init; }
    public bool IsPaid { get; init; }

}
public record ItemSummary
{
    public string ProductName { get; init; }
    public int Quantity { get; init; }
    public decimal TotalPrice { get; init; }
}
public record PaymentSummary
{
    public decimal Amount { get; init; }
    public string Method { get; init; }
    public string? RefrenceCode { get; init; }
    public DateTime PaidAt { get; init; }
}
