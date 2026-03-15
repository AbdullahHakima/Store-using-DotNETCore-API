namespace Store.Domain.Entities;

public class OrderItem:BaseEntity
{
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }


    public Guid ProductId { get; set; }
    public Guid OrderId { get; set; }

    public Product Product { get; set; } = null!;
    public Order Order { get; set; } = null!;

    public decimal LineTotal => Quantity * UnitPrice;// Calculated property for total price of the order item

}