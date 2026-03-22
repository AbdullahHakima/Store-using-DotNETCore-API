using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.DTOs.Orders.Requests
{
    public record OrderConfirmRequest
    {
        public Guid OrderId { get; }
    }
}
