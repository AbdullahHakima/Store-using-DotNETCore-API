namespace Store.API.DTOs
{
    public class ProductAdjustment
    {
        public Guid ProductId { get; set; }
        public int QuantityChange { get; set; }

    }
}
