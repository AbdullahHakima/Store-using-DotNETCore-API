namespace Store.API.DTOs
{
    public class InsufficientProductsDto
    {
        public Guid ProductId {  get; set; }
        public string Reason { get; set; } = null!;
    }
}
