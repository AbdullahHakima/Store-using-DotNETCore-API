using Microsoft.EntityFrameworkCore;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Infrastructure.Presistence
{
    public class Seeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            // If any categories exist, we already seeded — skip
            if (await db.Categories.AnyAsync()) return;

            // ── Categories ───────────────────────────────────────────
            var electronics = new Category
            {
                Name = "Electronics",
                Description = "Phones, laptops, accessories"
            };
            var clothing = new Category
            {
                Name = "Clothing",
                Description = "T-shirts, jackets, shoes"
            };
            var food = new Category
            {
                Name = "Food & Beverages",
                Description = "Packaged food, drinks"
            };

            await db.Categories.AddRangeAsync(electronics, clothing, food);

            // ── Tags ─────────────────────────────────────────────────
            var tagSale = new Tag { Name = "On Sale", Color = "#e74c3c" };
            var tagNew = new Tag { Name = "New", Color = "#2ecc71" };
            var tagPopular = new Tag { Name = "Popular", Color = "#f39c12" };

            await db.Tags.AddRangeAsync(tagSale, tagNew, tagPopular);

            // ── Products ─────────────────────────────────────────────
            var phone = new Product
            {
                Name = "Smartphone X12",
                Description = "6.5 inch display, 128GB storage",
                Price = 599.99m,
                StockQuantity = 50,
                Category = electronics,
                Tags = [tagNew, tagPopular]
            };
            var laptop = new Product
            {
                Name = "Laptop Pro 15",
                Description = "Intel i7, 16GB RAM, 512GB SSD",
                Price = 1199.99m,
                StockQuantity = 20,
                Category = electronics,
                Tags = [tagPopular]
            };
            var shirt = new Product
            {
                Name = "Cotton T-Shirt",
                Price = 19.99m,
                StockQuantity = 200,
                Category = clothing,
                Tags = [tagSale]
            };
            var coffee = new Product
            {
                Name = "Ground Coffee 500g",
                Price = 12.50m,
                StockQuantity = 150,
                Category = food,
                Tags = [tagNew]
            };

            await db.Products.AddRangeAsync(phone, laptop, shirt, coffee);

            // ── Customers ────────────────────────────────────────────
            var ahmed = new Customer
            {
                Name = "Ahmed Hassan",
                Email = "ahmed@example.com",
                PhoneNumber = "+20-100-000-0001",
                Address = new Address("10 Tahrir St", "Cairo", "Egypt")
            };
            var sara = new Customer
            {
                Name = "Sara Khalil",
                Email = "sara@example.com",
                PhoneNumber = "+20-100-000-0002",
                Address = new Address("5 Corniche Rd", "Alexandria", "Egypt")
            };

            await db.Customers.AddRangeAsync(ahmed, sara);

            // ── Orders ───────────────────────────────────────────────
            // Notice we use order.AddItem() — not order.Items.Add()
            // This is the aggregate pattern: the root controls its children
            var order1 = new Order
            {
                OrderNumber = "ORD-0001",
                OrderDate = DateTime.UtcNow.AddDays(-5),
                Customer = ahmed,
                Items = [],
                Payments = []
            };
            order1.AddOrderItem(phone, quantity: 1);
            order1.AddOrderItem(coffee, quantity: 2);
            order1.ConfirmOrder();

            var order2 = new Order
            {
                OrderNumber = "ORD-0002",
                OrderDate = DateTime.UtcNow.AddDays(-2),
                Customer = sara,
                Items = [],
                Payments = []
            };
            order2.AddOrderItem(shirt, quantity: 3);
            order2.AddOrderItem(laptop, quantity: 1);

            await db.Orders.AddRangeAsync(order1, order2);

            // ── Payments ─────────────────────────────────────────────
            var payment1 = new Payment
            {
                Order = order1,
                Amount = order1.TotalAmount,
                Method = PaymentMethod.Card,
                PaidAt = DateTime.UtcNow.AddDays(-5),
                RefrenceCode = "TXN-ABC123"
            };

            await db.Payments.AddAsync(payment1);

            // One SaveChangesAsync — everything above is tracked in memory
            // EF inserts them all in one transaction in the correct order
            await db.SaveChangesAsync();
        }
    }
}
