using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Color)
            .HasMaxLength(7)// rgb hex color code length
            .IsRequired()
            .HasDefaultValue("#cccccc");

        builder.Property(t => t.RowVersion)
            .IsRowVersion();

        // Configure many-to-many relationship with Product
        // This will create a join table named "ProductTags" with ProductId and TagId as foreign keys
        // The join table will be automatically created by EF Core
        // So we don't need to define a separate entity for it
        builder.HasMany(t=>t.Products)
            .WithMany(p=>p.Tags).UsingEntity(j=>j.ToTable("ProductTags"));

    }
}
