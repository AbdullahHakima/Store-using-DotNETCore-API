namespace Store.API.DTOs
{
    public class FailedTransferedProductsToCategoryDto
    {
        public Guid ProductId { get; set; }
        public string error { get; set; }
    }
}
