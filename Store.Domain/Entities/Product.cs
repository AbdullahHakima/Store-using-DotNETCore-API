namespace Store.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }

    public bool IsActive { get; set; }

    public Guid CategoryId { get; set; }

    public Category Category { get; set; }=null!;
    public ICollection<Tag> Tags { get; set; } = [];
    public virtual ICollection<OrderItem> OrderItems { get; set; } = [];

}