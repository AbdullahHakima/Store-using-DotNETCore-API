namespace Store.API.DTOs
{
    public class SuccessAdjustmentDto
    {
        public Guid productId { get; set; }
        public string productName { get; set; }
        public int OldStock { get; set; }
        public int NewStock { get; set; }
    }
}
