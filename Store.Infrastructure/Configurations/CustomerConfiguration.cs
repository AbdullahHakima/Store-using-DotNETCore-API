using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;
using Store.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Infrastructure.Configurations;

public class CustomerConfiguration:IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
       builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(200);
        builder.HasIndex(c => c.Email)
            .IsUnique();

        builder.Property(c => c.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        builder.OwnsOne(c=>c.Address, address=>
            {
           address.Property(a => a.Street)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("Street");
            address.Property(a => a.City)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("City");
            address.Property(a => a.Country)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("Country");
        });

    }
}
