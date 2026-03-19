using System.ComponentModel.DataAnnotations;

namespace Store.API.DTOs
{
    public class PaymentDto
    {
        [Required]
        public decimal Amount { get; set; }
        [Required]
        public string Method { get; set; }
        public string? ReferenceCode { get; set; }
    }
}
