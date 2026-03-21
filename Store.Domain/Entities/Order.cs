using Store.Domain.Enums;

namespace Store.Domain.Entities;

public class Order: BaseEntity
{
    public string OrderNumber { get; set; } = null!;
    public OrderStatus Status { get; set; }= OrderStatus.Pending;
    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public bool IsPaid { get; set; }

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
    public virtual ICollection<OrderItem> Items { get; set; } = [];    
    public virtual ICollection<Payment> Payments { get; set; } = [];
    public virtual ICollection<StockMovement> StockMovements { get; set; } = [];

    public void AddOrderItem(Product product,int quantity)
    {
        var item = new OrderItem
        {
            ProductId = product.Id,
            Quantity = quantity,
            UnitPrice = product.Price
        };
        ((List<OrderItem>) Items).Add(item);
        ReCalculateTotal();
    }

    private void ReCalculateTotal()
        => TotalAmount=Items.Sum(i=>i.UnitPrice*i.Quantity);

    public void ConfirmOrder()
    {
        if(Status!=OrderStatus.Pending)
           throw new InvalidOperationException("The order can only be confirmed if it is in pending status.");
        Status = OrderStatus.Confirmed;
    }
    public void CancelOrder()
    {
        if (Status == OrderStatus.Shipped)
            throw new InvalidOperationException("The order cannot be canceled if it has already been shipped.");
        Status = OrderStatus.Cancelled;
    }
}