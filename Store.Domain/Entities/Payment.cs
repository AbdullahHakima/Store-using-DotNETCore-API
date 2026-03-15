using Store.Domain.Enums;

namespace Store.Domain.Entities
{
    public class Payment:BaseEntity
    {
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public PaymentMethod Method { get; set; }
        public string? RefrenceCode { get; set; }

        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

    }
}