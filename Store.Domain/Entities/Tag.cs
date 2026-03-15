namespace Store.Domain.Entities;

public class Tag:BaseEntity
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = "#cccccc";

    public virtual ICollection<Product> Products { get; set; } = [];

}