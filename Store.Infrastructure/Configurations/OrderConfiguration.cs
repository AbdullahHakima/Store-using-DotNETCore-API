using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Infrastructure.Configurations;

public class OrderConfiguration:IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(20);
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();


        // Store the enum as a string in the database by HasConversion<string>()
        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.Notes)
            .HasMaxLength(500);

        builder.Property(o => o.RowVersion)
            .IsRowVersion();


        // Relationships between order and orderItems which is a one-to-many relationship
        // When an order is deleted, all related order items will also be deleted (cascade delete)
        builder.HasMany(o=>o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);


        // Relationships between order and payment which is a one-to-many relationship
        // this prevent to delete an order if there are related customer, ensuring data integrity and preventing orphaned customer records.
        builder.HasOne(o=>o.Customer)
            .WithMany(c=>c.Orders)
            .HasForeignKey(o=>o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationships between order and payment which is a one-to-many relationship
        // this prevent to delete an order if there are related payment, ensuring data integrity and preventing orphaned payment records.
        builder.HasMany(o=>o.Payments)
            .WithOne(p=>p.Order)
            .HasForeignKey(p=>p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);




    }
}
