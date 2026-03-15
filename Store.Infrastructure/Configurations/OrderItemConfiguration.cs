using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Infrastructure.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(oi => oi.Id);


        builder.Property(oi => oi.Quantity)
            .IsRequired();
        // use has Precisioin 18 ,2 for decimal values to ensure proper storage of currency values
        // 18 is the number if digits in total and 2 is the number of digits after the decimal point
        //example if you have a price of 123456789012345.67 it will be stored correctly
        //but if you have a price of 1234567890123456.78 it will throw an error because it exceeds the precision defined
        builder.Property(oi => oi.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        // Ignore the LineTotal property because it is a calculated property and we don't want to store it in the database
        builder.Ignore(oi => oi.LineTotal);

        builder.Property(oi => oi.RowVersion)
            .IsRowVersion();

        builder.HasOne(oi=>oi.Product)
            .WithMany(p=>p.OrderItems)
            .HasForeignKey(oi=>oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);



    }
}
