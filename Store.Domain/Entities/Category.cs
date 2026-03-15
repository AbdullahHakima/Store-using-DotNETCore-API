using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.Entities;

public class Category: BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }

    public virtual ICollection<Product> Products { get; set; } = [];
}
