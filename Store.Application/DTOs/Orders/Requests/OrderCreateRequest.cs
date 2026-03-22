using Store.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.DTOs.Orders.Requests
{
    public record OrderCreateRequest
    {
        public List<OrderItems> Items { get; init; }
        public Guid CustomerId { get; init; }
    }
}
public record OrderItems
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
