using Microsoft.EntityFrameworkCore;
using Store.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Infrastructure.Configurations;

public class MovementStockConfiguration:IEntityTypeConfiguration<StockMovement>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<StockMovement> builder)
    {


        builder.ToTable("StockMovements");

        builder.HasKey(t => t.Id);

        builder.Property(sm=>sm.ProductId)
               .IsRequired();

        builder.Property(sm=>sm.OrderId)
               .IsRequired();

        builder.Property(sm => sm.RowVersion)
            .IsRowVersion();

        builder.Property(sm => sm.QuantityChange)
               .IsRequired();

        builder.Property(sm => sm.MovementData)
               .IsRequired();



        builder.HasOne(sm => sm.Product)
               .WithMany(p => p.StockMovements)
               .HasForeignKey(sm => sm.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sm => sm.Order)
               .WithMany(o => o.StockMovements)
               .HasForeignKey(sm => sm.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(sm => sm.Reason)
               .IsRequired()
               .HasMaxLength(300);
    }
}
