namespace Store.API.DTOs
{
    public class ProductsSuccessTransferedDto
    {
        public string OldCategory { get; set; }
        public string NewCategory { get; set; }
        public string ProductName { get; set; }
        public Guid ProductId { get; set; }
    }
}
