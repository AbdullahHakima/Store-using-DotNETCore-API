using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Store.API.DTOs
{
    public class AddNewPaymentDTO
    {
        [Required]
        public decimal Amount { get; set; }
        [Required]
        public string method { get; set; }
        public string? ReferenceCode { get; set; }
    }
}
