using Microsoft.EntityFrameworkCore;
using Store.Domain.Entities;
using Store.Infrastructure.Interceptors;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Infrastructure.Presistence;

public class ApplicationDbContext:DbContext
{
    #region DbSets
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Tag> Tags { get; set; }
    #endregion

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        // Apply all configurations from the assembly where ApplicationDbContext is located
        // This will automatically apply any IEntityTypeConfiguration implementations found in the assembly
        // This approach promotes a clean separation of concerns and keeps the OnModelCreating method organized
        // It also allows for easier maintenance and scalability as new entity configurations can be added without
        // modifying the ApplicationDbContext class
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global query filters to exclude soft-deleted entities
        // This ensures that any query against these entities will automatically exclude those marked as deleted
        // It promotes data integrity and prevents accidental retrieval of soft-deleted records in the application
        modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(oi => !oi.IsDeleted);
        modelBuilder.Entity<Payment>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<Tag>().HasQueryFilter(t => !t.IsDeleted);
        base.OnModelCreating(modelBuilder);
    }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    optionsBuilder.AddInterceptors(new AuditInterceptor());
    //    base.OnConfiguring(optionsBuilder);
    //}

}
