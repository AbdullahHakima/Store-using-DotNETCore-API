using Store.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.Entities;

public class Customer: BaseEntity
{
    public string Name { get; set; }= null!;
    public string Email { get; set; }=null!;
    public string? PhoneNumber { get; set; }

    public Address Address { get; set; } = null!;

    public ICollection<Order> Orders { get; set; } = [];
}
