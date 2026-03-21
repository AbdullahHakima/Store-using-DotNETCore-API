using Bogus;
using Microsoft.EntityFrameworkCore;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Domain.ValueObjects;

namespace Store.Infrastructure.Presistence
{
    public class Seeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            if (await db.Products.CountAsync() >= 100) return;

            var faker = new Faker { Locale = "en" };

            // ── Categories ───────────────────────────────────────────
            var categories = new List<Category>
        {
            new() { Name = "Electronics",      Description = "Phones, laptops, accessories" },
            new() { Name = "Clothing",         Description = "T-shirts, jackets, shoes" },
            new() { Name = "Food & Beverages", Description = "Packaged food, drinks" },
            new() { Name = "Home & Garden",    Description = "Furniture, tools, decor" },
            new() { Name = "Sports",           Description = "Equipment, apparel, gear" },
        };
            await db.Categories.AddRangeAsync(categories);

            // ── Tags ─────────────────────────────────────────────────
            var tags = new List<Tag>
        {
            new() { Name = "On Sale",   Color = "#e74c3c" },
            new() { Name = "New",       Color = "#2ecc71" },
            new() { Name = "Popular",   Color = "#f39c12" },
            new() { Name = "Limited",   Color = "#9b59b6" },
            new() { Name = "Clearance", Color = "#1abc9c" },
        };
            await db.Tags.AddRangeAsync(tags);

            // ── Products — 1000 rows ──────────────────────────────────
            var productFaker = new Faker<Product>()
                .RuleFor(p => p.Name, f => f.Commerce.ProductName())
                .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
                .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(5, 2000), 2))
                .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 500))
                .RuleFor(p => p.IsActive, f => f.Random.Bool(0.85f)) // 85% active
                .RuleFor(p => p.Category, f => f.PickRandom(categories))
                .RuleFor(p => p.Tags, f =>
                {
                    // 0–3 random tags per product
                    var count = f.Random.Int(0, 3);
                    return f.PickRandom(tags, count).ToList();
                });

            var products = productFaker.Generate(1000);
            await db.Products.AddRangeAsync(products);

            // ── Customers — 50 rows ───────────────────────────────────
            var customerFaker = new Faker<Customer>()
                .RuleFor(c => c.Name, f => f.Name.FullName())
                .RuleFor(c => c.Email, f => f.Internet.Email())
                .RuleFor(c => c.PhoneNumber, f => f.Phone.PhoneNumber())
                .RuleFor(c => c.Address, f => new Address(
                    f.Address.StreetAddress(),
                    f.Address.City(),
                    f.Address.Country()));

            var customers = customerFaker.Generate(50);
            await db.Customers.AddRangeAsync(customers);

            // Save everything before creating orders
            // (orders need real product and customer Ids)
            await db.SaveChangesAsync();

            // Reload so we have real Ids
            var savedProducts = await db.Products.Where(p => p.IsActive && p.StockQuantity > 0).ToListAsync();
            var savedCustomers = await db.Customers.ToListAsync();

            // ── Orders — 200 rows ─────────────────────────────────────
            var orders = new List<Order>();

            for (int i = 0; i < 200; i++)
            {
                var customer = faker.PickRandom(savedCustomers);
                var status = faker.PickRandom<OrderStatus>();
                var orderDate = faker.Date.Past(1);

                var order = new Order
                {
                    OrderNumber = $"ORD-{(i + 1):D4}",
                    OrderDate = orderDate,
                    CustomerId = customer.Id,
                    Status = status,
                    Items = [],
                    Payments = []
                };

                // 1–5 items per order
                var itemCount = faker.Random.Int(1, 5);
                var pickedProducts = faker.PickRandom(savedProducts, itemCount).ToList();

                foreach (var product in pickedProducts)
                {
                    var qty = faker.Random.Int(1, 3);
                    order.AddOrderItem(product, qty);
                }

                // Paid orders get a payment
                if (status == OrderStatus.Confirmed ||
                    status == OrderStatus.Shipped ||
                    status == OrderStatus.Delivered)
                {
                    order.IsPaid = faker.Random.Bool(0.7f);

                    if (order.IsPaid)
                    {
                        order.Payments.Add(new Payment
                        {
                            Amount = order.TotalAmount,
                            Method = faker.PickRandom<PaymentMethod>(),
                            PaidAt = orderDate.AddDays(faker.Random.Int(0, 3)),
                            RefrenceCode = faker.Finance.CreditCardNumber()
                        });
                    }
                }

                orders.Add(order);
            }

            await db.Orders.AddRangeAsync(orders);
            await db.SaveChangesAsync();

            Console.WriteLine($"[Seeder] Done — {products.Count} products, " +
                              $"{customers.Count} customers, {orders.Count} orders.");
        }
    }

}
