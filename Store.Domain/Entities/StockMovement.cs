using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.Entities
{
    public class StockMovement:BaseEntity
    {
        public Guid ProductId { get; set; }
        public Guid OrderId { get; set; }
        public int QuantityChange { get; set; }
        public string Reason { get; set; } = null!;
        public DateTime MovementData { get; set; }
        public Product Product { get; set; }=null!;
        public Order Order { get; set; }=null!;

    }
}
