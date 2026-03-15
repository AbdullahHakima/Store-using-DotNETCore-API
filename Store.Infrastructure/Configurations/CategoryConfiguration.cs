using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;


namespace Store.Infrastructure.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            // Map the Category entity to the "Categories" table in the database
            builder.ToTable("Categories");
            // Set the primary key for the Category entity
            builder.HasKey(c => c.Id);


            builder.Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(c => c.Description)
                .HasMaxLength(500);

            // for the Optimstic Concurrency Control
            // EF Check the RowVersion value when updating or deleting an entity.
            // If the value has changed since it was last read,
            // EF will throw a DbUpdateConcurrencyException, allowing you to handle the conflict appropriately.
            builder.Property(c => c.RowVersion)
                .IsRowVersion();

            // Index on Name for faster search
            builder.HasIndex(c => c.Name);


        }
    }
}
