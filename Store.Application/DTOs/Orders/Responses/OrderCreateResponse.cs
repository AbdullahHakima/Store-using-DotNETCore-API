using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.DTOs.Orders.Responses
{
    public record OrderCreateResponse
    {
        public IEnumerable<OrderItemsFailuer>? itemFailuers { get; init; }
        public IEnumerable<OrderItemsSuccess>? itemsSuccesses { get; init; }
        public string OrderNumber { get; init; }
        public decimal TotalToPay { get; init; } 
    }
    public record OrderItemsFailuer
    {

        public Guid ProductId { get; init; }
        public string Reason { get; init; }
    }
    public record OrderItemsSuccess
    {

        public string ProductName { get; init; }
        public int Qunatity { get; init; }
        public decimal TotalPrice { get; init; }
    }
}
