using Store.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.DTOs.Orders.Responses;

public record OrderConfirmResponse
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = null!;
    public string Status { get; init; }=null!;
    public IEnumerable<ItemsProcessed>? items { get; init; }
    public IEnumerable<InsufficientProductStock>? insufficients { get; init; }
    public bool IsMovementCreated { get; init; }
    public IEnumerable<Movement>? movements { get; init; }

}
public record ItemsProcessed
{
    public string ProductName { get; init; }=null!;
    public int Quantity { get; init; }
}
public record InsufficientProductStock
{
    public Guid ProductId { get; init; }
    public string reason { get; init; }=null!;

}
public record Movement
{
    public string ProductName { get; init; }
    public int QuantityChange { get; init; }
    public string Reason { get; init; }
}

