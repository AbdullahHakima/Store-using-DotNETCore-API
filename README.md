# Store API

A robust ASP.NET Core Web API for an e-commerce platform, built with **.NET 10** and **Entity Framework Core**. This project demonstrates enterprise-level patterns, Clean Architecture principles, and advanced EF Core features to guarantee data integrity and high performance.

## Architecture

The project is divided into distinct layers to separate concerns:

*   **Store.Domain:** Contains the enterprise models (`Entities`), enums, and core business rules. Models inherit from a `BaseEntity`.
*   **Store.Infrastructure:** Contains the database context (`ApplicationDbContext`), EF Core migrations, entity configurations, interceptors, and data seeding logic.
*   **Store.API:** The presentation layer representing the HTTP endpoints (Controllers), dependency injection configuration, Swagger setup, and application bootstrapping.

## Key Features

*   **Domain-Driven Design (DDD) Patterns:** Entities encapsulate their own behavior. For instance, `Order` manages its `OrderItems` internally (Aggregate Root pattern) and handles total calculation and status transitions itself (`ConfirmOrder()`, `CancelOrder()`).
*   **Automated Auditing:** Uses a custom EF Core `SaveChangesInterceptor` (`AuditInterceptor`) to automatically stamp entities with `CreatedAt` and `UpdatedAt` timestamps during database saves.
*   **Soft Deletion:** Implements Global Query Filters in EF Core. Entities marked as `IsDeleted` are automatically filtered out from all standard `SELECT` queries across the application.
*   **Concurrency Control:** All entities contain a `RowVersion` property configured as a concurrency token to prevent lost updates in highly concurrent scenarios.
*   **Optimized Data Access:** Read operations in the API (like `ProductController`) extensively utilize `.AsNoTracking()` and explicit projections (`.Select()`) to fetch exactly what is needed without change-tracking overhead.
*   **Automated Data Seeding:** A built-in `Seeder` class populates the database with initial categories, tags, products, customers, and orders on application startup if the database is empty.
*   **Circular Reference Handling:** API responses are configured to handle model object cycles safely (e.g., `ReferenceHandler.IgnoreCycles`).

## Domain Models

*   **`Product`**: Represents items available for purchase. Belongs to a `Category` and can have many `Tags`.
*   **`Category`**: Groups related products (e.g., Electronics, Clothing).
*   **`Tag`**: Allows many-to-many descriptive labeling of products.
*   **`Customer`**: Holds user information, including a complex value type `Address`.
*   **`Order`**: The aggregate root for purchases. Holds `OrderItems`, links to a `Customer`, and tracks `OrderStatus`.
*   **`OrderItem`**: Represents line items tied to an `Order` and a `Product`.
*   **`Payment`**: Tracks financial transactions tied to an `Order`.

## Getting Started

### Prerequisites
*   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
*   SQL Server (LocalDB or a dedicated instance)

### Setup & Run
1. Clone the repository.
2. Ensure your `appsettings.json` in the `Store.API` project contains a valid connection string named `DefaultConnection`.
3. Open a terminal in the root directory and apply the EF Core migrations to build the database:
    ```bash
    dotnet ef database update --project ./src/Store.Infrastructure/Store.Infrastructure.csproj
    ```
4. Seed the database with initial data (optional, but recommended for development):
    ```bash
    dotnet run --project ./src/Store.API/Store.API.csproj
    ```
5. Launch the API:
    ```bash
    dotnet run --project ./src/Store.API/Store.API.csproj
    ```

### Using the API
- The API documentation and interactive test client are available via Swagger at `https://localhost:{port}/swagger`.
- Example endpoints:
    - `GET /api/products`: Retrieves a list of products.
    - `POST /api/orders`: Creates a new order.
---

## Tasks

---

### Task 01 — Product Filter & Search

**Level:** Medium
**Endpoint:** `GET /api/products/search`

**Requirements:**
1. Accept optional query params: `name`, `categoryId`, `minPrice`, `maxPrice`, `inStockOnly`
2. Pagination: `page` (default 1), `pageSize` (default 10, max 50)
3. Return: `Id`, `Name`, `Price`, `Stock`, `CategoryName`, `Tags` per product
4. Return pagination metadata: `totalCount`, `totalPages`, `currentPage`
5. All filters optional — no filters returns all active products paged

**Key concepts practiced:**
- Deferred execution — build `IQueryable`, execute once
- Conditional filter chaining
- `Select()` projection — no `Include()` needed
- `AsNoTracking()` on read-only endpoints
- `CountAsync()` before `Skip/Take`
- `Math.Min()` to cap page size

**Final submitted solution:**
```csharp
[HttpGet("search")]
public async Task<IActionResult> Search(
    [FromQuery] string?  name,
    [FromQuery] Guid?    categoryId,
    [FromQuery] decimal? minPrice,
    [FromQuery] decimal? maxPrice,
    [FromQuery] bool?    inStockOnly,
    [FromQuery] int      pageNumber = 1,
    [FromQuery] int      pageSize   = 10)
{
    pageSize = Math.Min(pageSize, 50);

    var query = _db.Products
        .Where(p => p.IsActive)
        .AsQueryable();

    if (!string.IsNullOrEmpty(name))
        query = query.Where(p => p.Name.Contains(name));

    if (categoryId != null)
        query = query.Where(p => p.CategoryId == categoryId);

    if (minPrice != null)
        query = query.Where(p => p.Price >= minPrice.Value);

    if (maxPrice != null)
        query = query.Where(p => p.Price <= maxPrice.Value);

    if (inStockOnly is true)
        query = query.Where(p => p.Stock > 0);

    var totalCount = await query.CountAsync();

    var items = await query
        .OrderBy(q => q.Name)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .Select(q => new
        {
            q.Id,
            q.Name,
            Category = q.Category.Name,
            Stock    = q.Stock,
            q.Price,
            Tags     = q.Tags.Select(t => t.Name).ToList(),
        })
        .AsNoTracking()
        .ToListAsync();

    return Ok(new
    {
        TotalCount  = totalCount,
        TotalPages  = (int)Math.Ceiling(totalCount / (double)pageSize),
        CurrentPage = pageNumber,
        Items       = items,
    });
}
```

**Score progression:** 3.7 → 7.0 → 8.7

---

### Task 02 — Add Payment to Order

**Level:** Hard
**Endpoint:** `POST /api/orders/{id}/payments`

**Requirements:**
1. Request body: `amount`, `method` (Cash / Card / BankTransfer), `referenceCode` (optional)
2. Reject if order not found — `404`
3. Reject if order status is not `Confirmed` — `400`
4. Reject if `amount` ≤ 0 — `400`
5. Reject if payment would exceed remaining balance — `400` with remaining shown
6. Set `Order.IsPaid = true` when total payments ≥ order total
7. Return: `paymentId`, `orderId`, `amountPaid`, `remainingBalance`, `isPaid`

**Key concepts practiced:**
- `[FromRoute]` binding
- Single `[FromBody]` DTO
- `Include(o => o.Payments)` to load existing payments before calculating balance
- `Enum.TryParse` with `ignoreCase: true`
- Never mutate source data — derive remaining balance from existing payments
- One `SaveChangesAsync` saves Payment insert + Order.IsPaid update atomically

**Final submitted solution:**
```csharp
[HttpPost("{id}/Payments")]
public async Task<IActionResult> Payments(
    [FromRoute] Guid id,
    [FromBody]  AddNewPaymentDTO paymentDTO)
{
    var order = await _db.Orders
        .Include(o => o.Payments)
        .FirstOrDefaultAsync(o => o.Id == id);

    if (order is null)
        return NotFound($"The order with id:{id} does not exist.");

    if (order.Status != OrderStatus.Confirmed)
        return BadRequest("Cannot make a payment for an unconfirmed order.");

    if (paymentDTO.Amount <= 0)
        return BadRequest("Payment amount must be greater than zero.");

    if (!Enum.TryParse<PaymentMethod>(paymentDTO.Method, ignoreCase: true, out var method))
        return BadRequest($"The payment method '{paymentDTO.Method}' is not supported.");

    decimal alreadyPaid      = order.Payments.Sum(p => p.Amount);
    decimal amountRemaining  = order.TotalAmount - alreadyPaid;
    decimal totalAfterPayment = alreadyPaid + paymentDTO.Amount;

    if (paymentDTO.Amount > amountRemaining)
        return BadRequest(new
        {
            error            = $"Payment exceeds remaining balance.",
            AmountRemaining  = amountRemaining
        });

    order.IsPaid = totalAfterPayment >= order.TotalAmount;

    var payment = new Payment
    {
        Amount        = paymentDTO.Amount,
        Method        = method,
        ReferenceCode = paymentDTO.ReferenceCode,
        OrderId       = order.Id,
        PaidAt        = DateTime.UtcNow,
    };

    await _db.Payments.AddAsync(payment);
    await _db.SaveChangesAsync();

    return Ok(new
    {
        PaymentId        = payment.Id,
        OrderId          = order.Id,
        AmountPaid       = paymentDTO.Amount,
        AmountRemaining  = order.TotalAmount - totalAfterPayment,
        order.IsPaid
    });
}
```

**Score progression:** 4.7 → 6.7 → 8.3

---

### Task 03 — Bulk Stock Adjustment

**Level:** Hard
**Endpoint:** `POST /api/products/adjust-stock`

**Requirements:**
1. Request body: list of `{ productId, adjustment }` (adjustment can be positive or negative)
2. Load all referenced products in one query — not one query per product
3. Per item: reject if not found, reject if adjustment is zero, reject if stock would go below zero
4. Apply all valid adjustments — partial failures allowed
5. Save only if at least one adjustment succeeded
6. Return: `succeeded` list and `failed` list with reasons

**Key concepts practiced:**
- Batch loading with `Contains()` — one query for all Ids
- Guard clause pattern with `continue` — failures exit early, success at bottom
- EF change tracker — modify loaded entities directly, no `Update()` call needed
- Conditional `SaveChangesAsync` — skip if nothing succeeded
- Partial failure response shape

**Final submitted solution:**
```csharp
[HttpPost("adjust-stock")]
public async Task<IActionResult> AdjustProductStock(
    [FromBody] List<ProductAdjustment> adjustments)
{
    if (adjustments == null || adjustments.Count == 0)
        return BadRequest("No adjustments provided.");

    var productIds = adjustments.Select(a => a.ProductId);
    var products   = await _db.Products
        .Where(p => productIds.Contains(p.Id))
        .ToListAsync();

    var failed  = new List<FailersAdjustment>();
    var success = new List<SuccessAdjustmentDto>();

    foreach (var adjustment in adjustments)
    {
        var product = products.FirstOrDefault(p => p.Id == adjustment.ProductId);

        if (product is null)
        {
            failed.Add(new FailersAdjustment
            {
                productId = adjustment.ProductId,
                reason    = "Product not found."
            });
            continue;
        }

        if (adjustment.QuantityChange == 0)
        {
            failed.Add(new FailersAdjustment
            {
                productId = product.Id,
                reason    = "Adjustment cannot be zero."
            });
            continue;
        }

        if (product.Stock + adjustment.QuantityChange < 0)
        {
            failed.Add(new FailersAdjustment
            {
                productId = product.Id,
                reason    = $"Insufficient stock. Current: {product.Stock}, adjustment: {adjustment.QuantityChange}."
            });
            continue;
        }

        var oldStock   = product.Stock;
        product.Stock += adjustment.QuantityChange;

        success.Add(new SuccessAdjustmentDto
        {
            productId   = product.Id,
            productName = product.Name,
            OldStock    = oldStock,
            NewStock    = product.Stock
        });
    }

    if (success.Count > 0)
        await _db.SaveChangesAsync();

    return Ok(new
    {
        Success  = success,
        Failures = failed
    });
}
```

**Score progression:** 6.3 → 6.7 → 9.3

---

### Task 04 — Customer Sales Report

**Level:** Hardest
**Endpoint:** `GET /api/reports/customers`

**Requirements:**
1. Optional date range filter: `from`, `to`
2. One summary row per customer — only customers with at least one order
3. Per row: `customerId`, `customerName`, `customerEmail`, `totalOrders`, `totalSpent`, `averageOrderValue`, `lastOrderDate`, `fullyPaidOrders`
4. Sort by `totalSpent` descending
5. Pagination: `page` (default 1), `pageSize` (default 10, max 50)
6. Round `averageOrderValue` to 2 decimal places
7. All aggregates in a single database query

**Key concepts practiced:**
- Navigation aggregate pattern — `c.Orders.Count()`, `Sum()`, `Average()`, `Max()` inside `Select()`
- EF translates navigation aggregates into SQL aggregates (no in-memory grouping)
- `Where(c => c.Orders.Any())` — filters via SQL `EXISTS`
- Date filter applied consistently across every aggregate
- `CountAsync()` before `Skip/Take` for accurate pagination metadata
- `Math.Clamp()` to bound page size both ways
- `Where` vs `OrderBy` — filter removes rows, sort only reorders them

**Final submitted solution:**
```csharp
[HttpGet("customers")]
public async Task<IActionResult> CustomerSalesReport(
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] int pageSize   = 10,
    [FromQuery] int pageNumber = 1)
{
    pageSize = Math.Clamp(pageSize, 1, 50);

    var query = _context.Customers
        .Where(c => c.Orders.Any())
        .OrderByDescending(c => c.Orders.Sum(o => o.TotalAmount));

    var totalCount = await query.CountAsync();

    var items = await query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .Select(q => new
        {
            customerId    = q.Id,
            customerName  = q.Name,
            customerEmail = q.Email,

            totalOrders = q.Orders.Count(o =>
                (!from.HasValue || o.OrderDate >= from) &&
                (!to.HasValue   || o.OrderDate <= to)),

            totalSpend = q.Orders
                .Where(o => (!from.HasValue || o.OrderDate >= from) &&
                            (!to.HasValue   || o.OrderDate <= to))
                .Sum(o => o.TotalAmount),

            averageOrderValue = Math.Round(
                q.Orders
                    .Where(o => (!from.HasValue || o.OrderDate >= from) &&
                                (!to.HasValue   || o.OrderDate <= to))
                    .Average(o => o.TotalAmount), 2),

            lastOrderDate = q.Orders
                .Where(o => (!from.HasValue || o.OrderDate >= from) &&
                            (!to.HasValue   || o.OrderDate <= to))
                .Max(o => o.CreatedAt),

            fullyPaidOrders = q.Orders.Count(o =>
                o.IsPaid &&
                (!from.HasValue || o.OrderDate >= from) &&
                (!to.HasValue   || o.OrderDate <= to)),
        })
        .AsNoTracking()
        .ToListAsync();

    return Ok(new
    {
        totalCount,
        totalPages  = (int)Math.Ceiling(totalCount / (double)pageSize),
        currentPage = pageNumber,
        items,
    });
}
```

**Score progression:** 7.0 → 7.7 → 9.0

---
 
### Self-Directed — Orders Summary
 
**Level:** Self-directed (no requirements given)
**Endpoint:** `GET /api/orders/summary`
 
**What was built:**
Grouped orders by status and returned per-group counts, revenue, and unpaid counts — going further than the prompt asked by adding status breakdown instead of a flat total.
 
**Key concepts practiced:**
- `GroupBy` inside EF query translated to SQL `GROUP BY`
- `g.Count()` per group — not a pre-computed whole-table count
- Deriving overall totals from the grouped result in C# — no second DB call
- `g.Where(o => o.IsPaid).Sum()` for actual revenue only
 
**Final submitted solution:**
```csharp
[HttpGet("summary")]
public async Task<IActionResult> OrdersSummary()
{
    var summary = await _db.Orders
        .GroupBy(g => g.Status)
        .Select(g => new
        {
            Status           = g.Key.ToString(),
            TotalOrders      = g.Count(),
            TotalRevenue     = g.Where(o => o.IsPaid).Sum(o => o.TotalAmount),
            TotalCountUnPaid = g.Count(o => !o.IsPaid),
        })
        .AsNoTracking()
        .ToListAsync();
 
    return Ok(summary);
}
```
 
**Score:** 7.0 first attempt (self-directed, no requirements provided)
 
---
 
### Task 05 — Bulk Category Transfer
 
**Level:** Hardest
**Endpoint:** `POST /api/categories/{targetCategoryId}/transfer-products`
 
**Requirements:**
1. Request body: list of `productId` (Guid) to transfer into the target category
2. Reject entire request if target category does not exist — `404`
3. Reject entire request if product list is empty — `400`
4. Load all referenced products in one query
5. Per product: if not found → fail. If already in target category → fail. Otherwise transfer.
6. Save only if at least one transfer succeeded
7. Return: `targetCategory`, `succeeded` (productId, productName, previousCategory), `failed` (productId, reason)
 
**Key concepts practiced:**
- Whole-request validation before expensive DB work — category check first, product load second
- `Except()` to find missing Ids after batch load — no impossible loop condition
- `Include(p => p.Category)` to capture the old category name before mutation
- Same-category guard clause — prevents pointless self-transfer
- Names in responses, not Guids — display-facing fields are always human-readable
- `oldProductCategory` captured before `CategoryId` is changed — same pattern as `oldStock` in Task 03
 
**Final submitted solution:**
```csharp
[HttpPost("{targetCategoryId}/transfer-products")]
public async Task<IActionResult> TransferProducts(
    [FromRoute] Guid targetCategoryId,
    [FromBody]  List<Guid> productIds)
{
    var success = new List<ProductsSuccessTransferedDto>();
    var failed  = new List<FailedTransferedProductsToCategoryDto>();
 
    if (productIds == null || productIds.Count == 0)
        return BadRequest("No product Ids provided.");
 
    if (targetCategoryId == Guid.Empty)
        return BadRequest("Target category Id is not valid.");
 
    var targetCategory = await _db.Categories.FindAsync(targetCategoryId);
    if (targetCategory is null)
        return NotFound("Target category not found.");
 
    var products = await _db.Products
        .Where(p => productIds.Contains(p.Id))
        .Include(p => p.Category)
        .ToListAsync();
 
    var missingProductIds = productIds.Except(products.Select(p => p.Id)).ToList();
 
    foreach (var missingProductId in missingProductIds)
    {
        failed.Add(new FailedTransferedProductsToCategoryDto
        {
            ProductId = missingProductId,
            Reason    = "Product not found."
        });
    }
 
    foreach (var product in products)
    {
        if (product.CategoryId == targetCategoryId)
        {
            failed.Add(new FailedTransferedProductsToCategoryDto
            {
                ProductId = product.Id,
                Reason    = "Product is already in this category."
            });
            continue;
        }
 
        var oldProductCategory = product.Category.Name;
        product.CategoryId = targetCategoryId;
 
        success.Add(new ProductsSuccessTransferedDto
        {
            ProductId        = product.Id,
            ProductName      = product.Name,
            OldCategory      = oldProductCategory,
            NewCategory      = targetCategory.Name
        });
    }
 
    if (success.Count > 0)
        await _db.SaveChangesAsync();
 
    return Ok(new
    {
        TargetCategoryName = targetCategory.Name,
        Success            = success,
        Failed             = failed
    });
}
```
 
**Score progression:** 7.0 → 8.3 → 9.3
 
---
 
### Task 06 — Single Query vs Split Query
 
**Level:** Performance
**Endpoint:** `GET /api/orders/{id}/detail` (two versions)
 
**Requirements:**
1. Load one order with Customer, Items (with Product), and Payments
2. Write Version A using standard `Include().ThenInclude()`
3. Write Version B adding `AsSplitQuery()`
4. Capture and compare the SQL generated by each version
5. Explain the tradeoff in one sentence
 
**Key concepts practiced:**
- How two `Include()` collections on the same entity causes cartesian row multiplication
- `AsSplitQuery()` — sends one SELECT per collection, eliminates row duplication
- Reading generated SQL from the console log to understand what EF actually sends
- Decision rule: single query for small collections, split query when collections are large
- Split queries are not safe inside explicit transactions — consistent snapshot is not guaranteed
 
**The tradeoff:**
- Single query: 1 network round trip, but rows = Items × Payments (5 items × 3 payments = 15 rows)
- Split query: 3 round trips, but rows = 1 + 5 + 3 = 9 total — no multiplication
 
**Decision rule:**
 
| Situation | Use |
|---|---|
| 1 collection, any size | Single query |
| 2+ collections, rows small (<10 each) | Single query |
| 2+ collections, rows large | Split query |
| Inside a transaction | Single query always |
| Using Select() projection | Neither needed |
 
**Version A — Include() single query:**
```csharp
[HttpGet("{id}/details")]
public async Task<IActionResult> GetOrderDetailsV1([FromRoute] Guid id)
{
    var order = await _db.Orders
        .Include(o => o.Customer)
        .Include(o => o.Items)
            .ThenInclude(i => i.Product)
        .Include(o => o.Payments)
        .FirstOrDefaultAsync(o => o.Id == id);
 
    if (order == null) return NotFound($"Order with id {id} not found.");
 
    return Ok(new
    {
        order.Id,
        order.OrderNumber,
        order.OrderDate,
        order.TotalAmount,
        order.IsPaid,
        CustomerName = order.Customer.Name,
        Items = order.Items.Select(i => new
        {
            ProductName = i.Product.Name,
            i.Quantity,
            TotalPrice  = i.Quantity * i.UnitPrice
        }),
        Payments = order.Payments.Select(p => new
        {
            p.Id,
            p.Amount,
            p.Method,
            p.ReferenceCode,
            p.PaidAt
        })
    });
}
```
 
**Version B — AsSplitQuery():**
```csharp
[HttpGet("{id}/detailsV2")]
public async Task<IActionResult> GetOrderDetailsV2([FromRoute] Guid id)
{
    var order = await _db.Orders
        .Include(o => o.Customer)
        .Include(o => o.Items)
            .ThenInclude(i => i.Product)
        .Include(o => o.Payments)
        .AsSplitQuery()
        .FirstOrDefaultAsync(o => o.Id == id);
 
    if (order == null) return NotFound($"Order with id {id} not found.");
 
    return Ok(new
    {
        order.Id,
        order.OrderNumber,
        order.OrderDate,
        order.TotalAmount,
        order.IsPaid,
        CustomerName = order.Customer.Name,
        Items = order.Items.Select(i => new
        {
            ProductName = i.Product.Name,
            i.Quantity,
            TotalPrice  = i.Quantity * i.UnitPrice
        }),
        Payments = order.Payments.Select(p => new
        {
            p.Id,
            p.Amount,
            p.Method,
            p.ReferenceCode,
            p.PaidAt
        })
    });
}
```
 
**SQL observation:** Version A — 95ms, ~40 columns, one query. Version B — 7ms per split query, ~40 columns across 3 queries. Both load full entity columns regardless of what the response needs.
 
**Score:** 9/10 code + 10/10 SQL + 8/10 reasoning = strong first attempt
 
---
 
### Task 07 — Select() Projection vs Include()
 
**Level:** Performance
**Endpoint:** `GET /api/orders/{id}/detailsV3`
 
**Requirements:**
1. Rewrite the Task 06 endpoint using `Select()` instead of `Include()`
2. Remove all `Include()` and `ThenInclude()` calls
3. Navigate into related data directly inside the projection
4. Add `AsNoTracking()`
5. Paste the SQL and compare column count to Version A
 
**Key concepts practiced:**
- `Select()` generates the same JOIN structure as `Include()` — but only fetches named columns
- `Include()` is for write scenarios — load, modify, save. `Select()` is for read scenarios always.
- EF auto-generates JOINs for any navigation property referenced inside `Select()`
- Column count: Version A ~40 columns → Version C 14 columns — same data, 65% fewer bytes over the wire
- `AsNoTracking()` with `Select()` — no entity objects created, nothing to track
 
**The rule:**
- Writing data → `Include()` → modify entity → `SaveChangesAsync()`
- Reading data → `Select()` projection → `AsNoTracking()` → never `Include()`
 
**Final submitted solution:**
```csharp
[HttpGet("{id}/detailsV3")]
public async Task<IActionResult> GetOrderDetailsV3([FromRoute] Guid id)
{

    var order = await _db.Orders
        .Where(o => o.Id == id)
        .Select(o => new
        {
            o.Id,
            o.OrderNumber,
            o.OrderDate,
            o.TotalAmount,
            o.IsPaid,
            CustomerName = o.Customer.Name,
            Items = o.Items.Select(i => new
            {
                ProductName = i.Product.Name,
                i.Quantity,
                TotalPrice  = i.Quantity * i.UnitPrice
            }),
            Payments = o.Payments.Select(p => new
            {
                p.Id,
                p.Amount,
                p.Method,
                p.ReferenceCode,
                p.PaidAt
            })
        })
        .AsNoTracking()
        .FirstOrDefaultAsync();
 
    if (order is null) return NotFound($"Order {id} not found.");
    return Ok(order);
}
```
 
**SQL observation:** 14 columns vs ~40 in Version A. Same JOIN structure, 65% fewer bytes. EF computed `TotalPrice` as `CAST(Quantity AS decimal) * UnitPrice` directly in SQL — no C# math after the fact.
 
**Score:** 10/10 across all three dimensions
 ---
 
### Task 08 — Compiled Queries
 
**Level:** Performance
**Endpoints:** `GET /api/products/{id}`, `GET /api/products/by-category/{categoryId}`
 
**Requirements:**
1. Create a static `CompiledQueries` class in `Store.Infrastructure/Persistence/`
2. Write a compiled query returning a single product by Id
3. Write a compiled query returning all active products for a given categoryId
4. Use both in controller endpoints
5. Capture SQL and compare to normal query
6. Explain the difference between compiled query and EF's built-in query cache
 
**Key concepts practiced:**
- `EF.CompileAsyncQuery()` — pays LINQ-to-SQL translation cost once at startup
- `static readonly` field — one delegate instance shared across all requests, never recreated
- Named DTO required — anonymous types cannot be used as return types in compiled queries
- Sync terminators inside compiled queries — `FirstOrDefault()` not `FirstOrDefaultAsync()`
- Single entity → `Task<T?>`, collection → `IAsyncEnumerable<T>` streamed with `await foreach`
- SQL is identical to normal query — the difference is when translation happens, not what SQL is produced
- `!products.Any()` not `products is null` — a `new List<T>()` is never null
 
**The key distinction:**
- EF built-in cache: still runs expression tree parsing + hash lookup on every call even on a hit
- Compiled query: stores result as a .NET delegate — no parsing, no hashing, direct invocation
- Benefit is measurable only on hot-path endpoints called thousands of times per minute
 
**Final submitted solution:**
```csharp
public static class CompiledQueries
{
    public static readonly Func<StoreDbContext, Guid, Task<ProductDto?>>
        GetProductById = EF.CompileAsyncQuery(
            (StoreDbContext db, Guid id) =>
                db.Products
                  .Where(p => p.Id == id && p.IsActive)
                  .Select(p => new ProductDto
                  {
                      Id              = p.Id,
                      Name            = p.Name,
                      CategoryName    = p.Category.Name,
                      Price           = p.Price,
                      QuantityInStock = p.StockQuantity
                  })
                  .FirstOrDefault());
 
    public static readonly Func<StoreDbContext, Guid, IAsyncEnumerable<ProductDto>>
        GetProductsByCategoryId = EF.CompileAsyncQuery(
            (StoreDbContext db, Guid categoryId) =>
                db.Products
                  .Where(p => p.CategoryId == categoryId && p.IsActive)
                  .Select(p => new ProductDto
                  {
                      Id              = p.Id,
                      Name            = p.Name,
                      CategoryName    = p.Category.Name,
                      Price           = p.Price,
                      QuantityInStock = p.StockQuantity
                  }));
}
 
// Controller usage
[HttpGet("{id}/compiled")]
public async Task<IActionResult> GetById([FromRoute] Guid id)
{
    var product = await CompiledQueries.GetProductById(_db, id);
    if (product is null) return NotFound();
    return Ok(product);
}
 
[HttpGet("by-category")]
public async Task<IActionResult> GetByCategory([FromQuery] Guid categoryId)
{
    var products = new List<ProductDto>();
    await foreach (var p in CompiledQueries.GetProductsByCategoryId(_db, categoryId))
        products.Add(p);
 
    if (!products.Any()) return NotFound($"No products found for category {categoryId}.");
    return Ok(products);
}
```
 
**Score:** 9.3/10 first attempt
 
---
 
### Task 09 — Bulk Operations: ExecuteUpdateAsync and ExecuteDeleteAsync
 
**Level:** Performance + Danger
**Endpoints:** `PATCH /api/products/deactivate-by-category/{categoryId}`, `PATCH /api/products/apply-discount/{categoryId}`, `DELETE /api/products/hard-delete-inactive`
 
**Requirements:**
1. Deactivate all products in a category with one UPDATE statement
2. Apply a percentage price discount across a category — formula pushed into SQL
3. Hard-delete all inactive products permanently
4. Capture SQL for all three — verify single statement per operation
5. Explain why ExecuteDeleteAsync is dangerous
 
**Key concepts practiced:**
- `ExecuteUpdateAsync` — single UPDATE statement, no entities loaded, no SaveChanges needed
- `ExecuteDeleteAsync` — single DELETE statement, bypasses change tracker entirely
- SQL formula expressions — `p => p.Price * (1 - discount)` translates to `[Price] * @p` in SQL
- Pre-computed C# values generate per-row UPDATEs — column references generate one bulk UPDATE
- `IgnoreQueryFilters()` required for clean bulk operations — without it EF may add a SELECT first
- Cascade side effects — `ExecuteDeleteAsync` on Products also deleted OrderItems silently
- `ExecuteUpdate/Delete` bypass: global query filters, AuditInterceptor, soft delete, RowVersion
 
**The tradeoff:**
- Load + loop: per-row validation possible, interceptors fire, change tracker tracks, N+1 queries
- ExecuteUpdateAsync: one SQL statement, no validation per row, interceptors skipped, fastest possible
 
**Use ExecuteUpdateAsync/DeleteAsync when:**
- Operation is purely mechanical — set a flag, apply a formula
- No per-row business logic needed
- You have verified cascade side effects
 
**Avoid when:**
- Each row needs individual validation
- You need audit trail via interceptors
- Cascades could destroy related data unexpectedly
 
**Final submitted solutions:**
```csharp
// Deactivate all products in a category — single UPDATE
[HttpPatch("deactivate-by-category/{categoryId}")]
public async Task<IActionResult> DeactivateProductsByCategory([FromRoute] Guid categoryId)
{
    int affectedRows = await _db.Products
        .Where(p => p.CategoryId == categoryId)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
 
    return Ok(new { NumberOfRowsAffected = affectedRows });
}
 
// Apply discount — formula pushed into SQL, one UPDATE
[HttpPatch("apply-discount/{categoryId}/v2")]
public async Task<IActionResult> ApplyDiscountV2([FromRoute] Guid categoryId,
                                                 [FromBody] decimal percentage)
{
    if (percentage <= 0 || percentage >= 100)
        return BadRequest("Percentage must be between 0 and 100.");
 
    decimal discount = percentage / 100;
 
    int affectedRows = await _db.Products
        .Where(p => p.CategoryId == categoryId && p.IsActive)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, p => p.Price * (1 - discount)));
 
    return Ok(new { numberOfRowsAffected = affectedRows });
}
 
// Hard delete — explicit transaction required because of FK constraint on OrderItems
// OrderItems must be deleted first, then Products — both in one transaction
[HttpDelete("hard-delete-inactive")]
public async Task<IActionResult> HardDeleteInActiveProduct()
{
    await using var transaction = await _db.Database.BeginTransactionAsync();
    try
    {
        // Delete child rows first — FK constraint prevents deleting Products with existing OrderItems
        await _db.OrderItems
            .Where(oi => !oi.Product.IsActive)
            .ExecuteDeleteAsync();
 
        // Now safe to delete the parent rows
        int numberOfDeleteProducts = await _db.Products
            .Where(p => !p.IsActive)
            .ExecuteDeleteAsync();
 
        await transaction.CommitAsync();
        return Ok(new { TotalCountOfDeleteProducts = numberOfDeleteProducts });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return StatusCode(StatusCodes.Status500InternalServerError,
            $"An error occurred while deleting products: {ex.Message}");
    }
}
```
 
**Why the transaction is necessary here:**
If the OrderItems delete succeeds but the Products delete fails, without a transaction you end up with order history permanently destroyed but inactive products still in the table. The transaction guarantees both succeed or both roll back.
 
**SQL observations:**
- Deactivate: `UPDATE [p] SET [p].[IsActive] = @p FROM [Products] WHERE [CategoryId] = @categoryId` — one statement
- Discount V2: `UPDATE [p] SET [p].[Price] = [p].[Price] * @p FROM [Products] WHERE ...` — formula in SQL
- Hard delete v1 (naive): EF auto-generated two DELETEs — OrderItems first, then Products. Side effect was invisible from C#.
- Hard delete v2 (refactored): Explicit transaction wrapping two `ExecuteDeleteAsync` calls. OrderItems deleted first consciously, then Products. If either fails the whole operation rolls back.
 
**Key refactor lesson:**
Reading the SQL output from v1 revealed that EF silently deleted order history as a cascade side effect. The refactored version makes both deletions explicit and atomic — the developer controls the order, the transaction guarantees consistency.
 
**`await using` vs `using` for async transactions:**
Always use `await using var transaction = ...` not `using (var transaction = ...)` — the async `DisposeAsync()` must be awaited, not called synchronously.
 
**ExecuteDeleteAsync danger sentence:**
*"ExecuteDeleteAsync bypasses global query filters, soft delete, and all interceptors — rows are permanently removed with no audit trail, no undo, and cascade side effects that are invisible from the C# code."*
 
**Score:** 9.3/10 final (refactored from 8.7)
 
 ---
 
### Task 10 — Explicit Transactions with Savepoints
 
**Level:** Transactions
**Endpoint:** `POST /api/orders/{id}/confirm-with-stock`
 
**Requirements:**
1. Load order with items and products in one query
2. Validate: order must be Pending, all products must have sufficient stock — before opening any transaction
3. Inside one transaction: confirm order → reduce stock → create StockMovement audit records
4. Place a savepoint named `"StockReduced"` after stock reduction
5. If movement record creation fails, roll back to savepoint and commit — preserving the critical operations
6. Return order info, items processed, and movement records created
 
**Key concepts practiced:**
- `await using var tx = await _db.Database.BeginTransactionAsync()` — async disposal
- Three `SaveChangesAsync()` calls inside one transaction — all atomic until `CommitAsync()`
- `CreateSavepointAsync("name")` — named checkpoint inside a live transaction
- `RollbackToSavepointAsync("name")` — rewinds to checkpoint, transaction stays alive
- After `RollbackToSavepoint`, call `CommitAsync()` to keep the work before the checkpoint
- Validation always runs before opening the transaction — never open a transaction to validate
- No `Update()` calls needed — change tracker detects modifications on loaded entities
- `stockMovements` declared before `try` so it is accessible in the response outside the block
 
**The savepoint decision:**
The savepoint sits between stock reduction (critical) and movement record creation (audit trail). If the audit trail fails, the business-critical operations — order confirmed, stock reduced — are preserved by rolling back to the savepoint and committing. The audit trail failure is acceptable and recoverable. Losing the order confirmation is not.
 
**Final submitted solution:**
```csharp
[HttpPost("{id}/confirm-with-stock")]
public async Task<IActionResult> ConfirmOrderWithStockCheck([FromRoute] Guid id)
{
    var order = await _db.Orders
        .Include(o => o.Items)
            .ThenInclude(i => i.Product)
        .FirstOrDefaultAsync(o => o.Id == id);
 
    if (order is null) return NotFound($"Order with id {id} not found.");
    if (order.Status != OrderStatus.Pending)
        return BadRequest("Only pending orders can be confirmed.");
 
    var insufficientProducts = new List<InsufficientProductsDto>();
    foreach (var item in order.Items)
    {
        if (item.Product.StockQuantity < item.Quantity)
            insufficientProducts.Add(new InsufficientProductsDto
            {
                ProductId = item.ProductId,
                Reason    = $"Insufficient stock for {item.Product.Name}. " +
                            $"Available: {item.Product.StockQuantity}, Required: {item.Quantity}"
            });
    }
    if (insufficientProducts.Any())
        return BadRequest(new { InsufficientProducts = insufficientProducts });
 
    var stockMovements = new List<StockMovement>();
    await using var transaction = await _db.Database.BeginTransactionAsync();
    try
    {
        // Step 1 — confirm the order
        order.Status = OrderStatus.Confirmed;
        await _db.SaveChangesAsync();
 
        // Step 2 — reduce stock
        foreach (var item in order.Items)
            item.Product.StockQuantity -= item.Quantity;
        await _db.SaveChangesAsync();
 
        // Savepoint — order confirmed + stock reduced are safe beyond this point
        await transaction.CreateSavepointAsync("StockReduced");
 
        // Step 3 — create audit records
        stockMovements = order.Items.Select(i => new StockMovement
        {
            ProductId      = i.ProductId,
            OrderId        = order.Id,
            Order          = order,
            QuantityChange = -i.Quantity,
            Reason         = $"Order {order.OrderNumber} confirmed",
            MovementDate   = DateTime.UtcNow,
            Product        = i.Product
        }).ToList();
 
        await _db.StockMovements.AddRangeAsync(stockMovements);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch (Exception)
    {
        // Roll back to savepoint — order confirmed + stock reduced preserved
        // Only audit records lost — acceptable, can be recreated later
        await transaction.RollbackToSavepointAsync("StockReduced");
        await transaction.CommitAsync();
    }
 
    return Ok(new
    {
        OrderId                = order.Id,
        order.OrderNumber,
        Status                 = order.Status.ToString(),
        ItemsProcessed         = order.Items.Select(i => new
        {
            ProductName = i.Product.Name,
            i.Quantity
        }),
        MovementRecordsCreated = stockMovements.Select(s => new
        {
            s.Id,
            s.QuantityChange,
            s.Reason
        })
    });
}
```
 
**Score:** 7.3 first attempt → 9.3 final
 
---
 
## Patterns Learned
 
| Pattern | Description |
|---|---|
| Deferred execution | Build `IQueryable` fully, call `ToListAsync()` once at the end |
| Guard clauses | Check every failure first with `continue` — success path at the bottom |
| Batch loading | One `Contains()` query for all Ids — never query inside a loop |
| Navigation aggregates | `Count()`, `Sum()`, `Average()`, `Max()` inside `Select()` — translated to SQL |
| Consistent filtering | Date filters applied to every aggregate, not just one field |
| Change tracker | Modify loaded entities directly — no `Update()` call needed |
| Conditional save | `SaveChangesAsync()` only when there is something to write |
| Data integrity | Never mutate source values — derive calculated values from source |
| Except() for missing Ids | Find unmatched Ids after batch load — no impossible loop condition |
| Pre-mutation capture | Save old values before changing entity state — oldStock, oldCategory |
| Whole-request vs per-item validation | Check whole-request preconditions first, then validate per item in the loop |
| Names not Guids in responses | Display-facing fields are always human-readable strings |
| GroupBy aggregates | `g.Count()`, `g.Sum()` per group — not a pre-computed whole-table value |
| Compiled queries | `EF.CompileAsyncQuery` — translate once at startup, skip cache lookup on every call |
| IAsyncEnumerable streaming | `await foreach` — stream collection results without buffering all rows |
| List is never null | Use `!list.Any()` not `list is null` — a constructed List cannot be null |
| Select() over Include() for reads | Select() fetches only named columns — Include() loads full entities including unused columns |
| SQL logging | `LogTo` + `EnableSensitiveDataLogging` — always verify what EF actually sends |
| ExecuteUpdateAsync | Single UPDATE statement — no entities loaded, no SaveChanges, formula pushed into SQL |
| ExecuteDeleteAsync | Single DELETE — bypasses everything, permanent, cascade side effects invisible from C# |
| SQL formula expressions | Column reference in lambda → SQL arithmetic. Pre-computed C# value → per-row UPDATEs |
| IgnoreQueryFilters for bulk ops | Prevents EF adding a SELECT before UPDATE/DELETE when global filters are active |
| await using for transactions | Always `await using var tx = ...` not `using ()` — ensures DisposeAsync is awaited |
| Savepoints | `CreateSavepointAsync` + `RollbackToSavepointAsync` + `CommitAsync` — partial rollback without losing critical work |
| Validation before transaction | Never open a transaction to validate — validate first, open only when ready to write |
| Change tracker on loaded entities | No `Update()` needed — modifying a loaded entity is detected automatically |

---
 
## Concept Deep Dive — Expression Trees
 
> Added as a reference after a question about how EF translates `Where((expr1) && (expr2))` into SQL.
 
---
 
### The core question
 
*"When I use `Where(expression1 && expression2)` does EF translate it into two separate expression trees?"*
 
**Answer:** No — it is one tree. The `&&` operator becomes a single `BinaryExpression` node of type `AndAlso` with two child nodes. One tree produces one SQL `WHERE` clause with one `AND`.
 
---
 
### Func vs Expression — the foundation
 
These two lines look identical. They are not:
 
```csharp
// Func — a compiled delegate (machine code)
Func<Product, bool> fn = p => p.Price > 100;
// EF CANNOT translate this — it is compiled code, not inspectable data
 
// Expression — a data structure describing the lambda
Expression<Func<Product, bool>> ex = p => p.Price > 100;
// EF CAN translate this — it is an object graph EF can walk and convert to SQL
```
 
A **delegate** is a pointer to compiled IL — you run it but cannot inspect its structure. An **expression** is an object graph that describes what the code would do — EF walks it and translates each node to SQL.
 
---
 
### What a tree actually looks like in memory
 
For `p => p.Price > 100` the compiler builds:
 
```
LambdaExpression
├── Parameters: [ p (ParameterExpression, type: Product) ]
└── Body: BinaryExpression (NodeType: GreaterThan)
    ├── Left:  MemberExpression
    │          ├── Expression: p (ParameterExpression)
    │          └── Member: Price (PropertyInfo)
    └── Right: ConstantExpression
               └── Value: 100 (decimal)
```
 
Every node is a real C# object. EF walks it: `MemberExpression(Price)` → column name `Price`. `BinaryExpression(GreaterThan)` → SQL operator `>`. `ConstantExpression(100)` → SQL parameter `@p = 100`.
 
---
 
### What && looks like in the tree
 
For `.Where(p => p.CategoryId == categoryId && p.IsActive)`:
 
```
LambdaExpression
└── Body: BinaryExpression (NodeType: AndAlso)   ← this is the &&
    ├── Left:  BinaryExpression (NodeType: Equal)
    │          ├── Left:  MemberExpression → CategoryId
    │          └── Right: ConstantExpression → @categoryId
    └── Right: MemberExpression → IsActive
```
 
EF translates this to: `WHERE CategoryId = @p AND IsActive = CAST(1 AS bit)`
 
---
 
### Chained Where() calls
 
```csharp
_db.Products
    .Where(p => p.IsActive)
    .Where(p => p.Price > 100)
```
 
These are NOT two trees. Each `Where()` adds its expression as an additional AND clause. EF collapses them at execution time:
 
```sql
WHERE [p].[IsDeleted] = 0   -- global query filter
  AND [p].[IsActive] = 1    -- first Where()
  AND [p].[Price] > @p      -- second Where()
```
 
---
 
### Captured variables — why discount worked in Task 09
 
```csharp
decimal discount = percentage / 100;  // = 0.9 for 10% off
.SetProperty(p => p.Price, p => p.Price * (1 - discount))
```
 
The captured variable `discount` is stored as a field on a compiler-generated closure. EF sees `MemberExpression → closure.discount` and evaluates it immediately as a constant. The tree becomes:
 
```
BinaryExpression (Multiply)
├── Left:  MemberExpression → Price    ← column reference, stays in SQL
└── Right: BinaryExpression (Subtract)
           ├── ConstantExpression → 1
           └── ConstantExpression → 0.9  ← closure evaluated to constant
```
 
Result: `[Price] * @p` — one SQL formula, one bulk UPDATE.
 
When you pre-compute the value in C# first (`decimal newPrice = product.Price * 0.9`), EF sees a `ConstantExpression(119.99)` — a fixed number different per row — and generates one UPDATE per row instead.
 
---
 
### Why Func breaks EF
 
```csharp
// WRONG — Func variable, EF throws at runtime
Func<Product, bool> filter = p => p.Price > 100;
_db.Products.Where(filter);  // "The LINQ expression could not be translated"
 
// CORRECT — Expression variable, EF translates to SQL
Expression<Func<Product, bool>> filter = p => p.Price > 100;
_db.Products.Where(filter);  // WHERE Price > @p
```
 
When you write a lambda inline in `.Where(p => ...)`, the compiler automatically creates an `Expression<Func<>>` because `IQueryable.Where()` takes an `Expression` parameter. The problem only appears when you store the lambda in a `Func` variable first.
 
---
 
### EF translation pipeline — what happens between your C# and the database
 
| Step | What happens |
|---|---|
| 1 | You write LINQ — `_db.Products.Where(...).Select(...)` |
| 2 | C# compiler builds expression trees for each lambda |
| 3 | `IQueryable` holds a chain of `MethodCallExpression` nodes — nothing hits the DB |
| 4 | `ToListAsync()` called — execution triggered |
| 5 | EF visitor walks the tree — maps members to columns, operators to SQL, closures to parameters |
| 6 | SQL string assembled with parameter placeholders |
| 7 | Query hashed and stored in query cache |
| 8 | SQL sent to database, results mapped back to your type |
 
A **compiled query** (`EF.CompileAsyncQuery`) caches the result of steps 5–6 as a delegate. Every subsequent call skips those steps entirely and jumps straight to step 7.
 
---
 
### Quick error reference
 
| Error | Cause | Fix |
|---|---|---|
| `The LINQ expression could not be translated` | Lambda uses a `Func` variable or a C# method EF does not know | Change `Func<T,bool>` to `Expression<Func<T,bool>>` or rewrite using supported operators |
| Bulk UPDATE generates per-row UPDATEs | Pre-computed value passed instead of column expression | Reference the column inside the lambda: `p => p.Price * factor` not `newPrice` |
| Query loads all rows then filters | `Func` variable in `Where()`, or method call EF cannot translate | Inline the lambda or use an `Expression` variable |
 
---
 
## Starting Score Trend
 
| Task | First Attempt | Final |
|---|---|---|
| Task 01 — Search | 3.7 | 8.7 |
| Task 02 — Payment | 4.7 | 8.3 |
| Task 03 — Stock | 6.3 | 9.3 |
| Task 04 — Report | 7.0 | 9.0 |
| Self-directed — Summary | 7.0 | 7.0 |
| Task 05 — Transfer | 7.0 | 9.3 |
| Task 06 — Split Query | 9/10 code | 9/10 |
| Task 07 — Projection | 10/10 | 10/10 |
| Task 08 — Compiled Queries | 9.3/10 | 9.3/10 |
| Task 09 — Bulk Operations | 7.0 | 8.7 |
| Task 10 — Transactions | 7.3 | 9.3 |
 
 
First attempt scores: **3.7 → 4.7 → 6.3 → 7.0 → 7.0 → 7.0 → 9.0 → 10.0 → 9.3 → 7.0 → 7.3**
 
Ten tasks completed across two levels — business logic patterns (Tasks 01–05) and EF Core performance and internals (Tasks 06–10). Starting score trend went from 3.7 to consistently 7+ across all new tasks.

---
 
## Direction A — Application Layer
 
> Added after completing all 10 EF Core tasks. The goal: transform the store from controllers-calling-DbContext directly into a properly layered system that mirrors production architecture.
 
---
 
### Why the Application Layer
 
After Task 10, the store looked like this:
 
```
HTTP Request → Controller → StoreDbContext → Database
```
 
The controller was doing everything — validation, business logic, EF Core queries, response shaping. This works for learning but breaks down in production because:
 
- **Untestable** — you cannot unit test a controller that depends on a real database
- **No separation of concerns** — changing the database technology requires touching every controller
- **No reuse** — logic written in one controller cannot be called from another without duplication
- **No contract** — nothing enforces what a feature must do vs how it does it
 
After Direction A, the architecture looks like this:
 
```
HTTP Request → Controller → IService (interface) → ServiceImpl → StoreDbContext → Database
```
 
The controller only translates HTTP. The interface defines the contract. The service holds all logic. The infrastructure holds all EF Core code. Each layer can change independently.
 
---
 
### What changed and why — layer by layer
 
#### Store.Domain — unchanged
 
Pure C# entities, value objects, enums, interfaces. Zero dependencies on anything outside itself. This layer was already correct from Day 1.
 
#### Store.Application — new project
 
Defines **what** the system can do. No EF Core, no HTTP, no infrastructure concerns.
 
```
Store.Application/
├── Common/
│   └── Result.cs          ← Result<T> pattern — replaces exceptions for business failures
├── DTOs/
│   ├── Products/          ← typed request and response shapes
│   └── Orders/
├── Interfaces/
│   ├── IProductService.cs ← contracts — what each service must do
│   └── IOrderService.cs
└── DependencyInjection.cs ← AddApplication() extension method
```
 
**Dependency rule:** `Store.Application` references only `Store.Domain`. It never references EF Core, ASP.NET Core, or `Store.Infrastructure`.
 
#### Store.Infrastructure — expanded
 
Implements **how** the system does what Application defined. All EF Core lives here.
 
```
Store.Infrastructure/
├── Persistence/
│   └── StoreDbContext.cs
├── Configurations/        ← Fluent API
├── Interceptors/          ← AuditInterceptor
├── Services/              ← new: service implementations
│   ├── ProductService.cs  ← implements IProductService
│   └── OrderService.cs    ← implements IOrderService
└── DependencyInjection.cs ← AddInfrastructure() — registers DbContext + services
```
 
**Dependency rule:** `Store.Infrastructure` references `Store.Application` and `Store.Domain`. It never references `Store.API`.
 
#### Store.API — thinned
 
Only speaks HTTP. No business logic, no EF Core.
 
```
Store.API/
├── Controllers/           ← inject IService, call service, return result.ToActionResult(this)
├── Extensions/
│   └── ResultExtensions.cs ← maps Result<T> to IActionResult
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs ← catches all unhandled exceptions
└── Program.cs             ← builder.Services.AddInfrastructure().AddApplication()
```
 
**Dependency rule:** `Store.API` references `Store.Application` (for interfaces and DTOs) and `Store.Infrastructure` (via DI only — never directly).
 
---
 
### The dependency rule visualised
 
```
Store.API
    ↓ references
Store.Application  ←────────────  Store.Infrastructure
    ↓ references                       ↓ references
Store.Domain  ←────────────────── Store.Domain
```
 
Arrows point inward. Nothing points outward. `Store.Domain` knows about nothing. `Store.Infrastructure` knows about `Store.Domain` and `Store.Application`. `Store.API` knows about `Store.Application` but never calls `Store.Infrastructure` directly — only through DI.
 
---
 
### Task A1 — Result\<T\> Pattern
 
**What it solves:** Services need to communicate failures to controllers without throwing exceptions. Exceptions are expensive, swallowed silently by try/catch, and carry no HTTP semantics. `Result<T>` wraps either a success value or a failure with a status code — the controller maps it to HTTP in one line.
 
**Before:**
```csharp
// controller doing validation and returning HTTP directly
var product = await _db.Products.FindAsync(id);
if (product is null) return NotFound("not found");
return Ok(product);
```
 
**After:**
```csharp
// controller knows nothing — three lines total
var result = await _productService.GetByIdAsync(id);
return result.ToActionResult(this);
```
 
**Key design decisions:**
- Private constructor — only factory methods can create a Result. Impossible to create invalid state.
- `IsFailure` is computed from `IsSuccess` — single source of truth, never out of sync
- StatusCode travels with the Result — the controller extension maps it to HTTP without switch logic in every controller
- Two versions: `Result<T>` for operations returning data, `Result` for operations returning nothing
 
```csharp
// Store.Application/Common/Result.cs
public class Result<T>
{
    public bool   IsSuccess  { get; }
    public bool   IsFailure  => !IsSuccess;
    public T?     Value      { get; }
    public string Error      { get; } = string.Empty;
    public int    StatusCode { get; }
 
    private Result(bool isSuccess, T? value, string error, int statusCode)
    { IsSuccess = isSuccess; Value = value; Error = error; StatusCode = statusCode; }
 
    public static Result<T> Success(T value)       => new(true,  value,   string.Empty, 200);
    public static Result<T> NotFound(string error) => new(false, default, error,        404);
    public static Result<T> BadRequest(string error)=> new(false, default, error,       400);
    public static Result<T> Conflict(string error) => new(false, default, error,        409);
    public static Result<T> ServerError(string error)=>new(false, default, error,       500);
}
```
 
```csharp
// Store.API/Extensions/ResultExtensions.cs
public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
{
    if (result.IsSuccess) return controller.Ok(result.Value);
    return result.StatusCode switch
    {
        404 => controller.NotFound(new   { error = result.Error }),
        400 => controller.BadRequest(new { error = result.Error }),
        409 => controller.Conflict(new   { error = result.Error }),
        _   => controller.StatusCode(500, new { error = result.Error })
    };
}
```
 
---
 
### Task A2 — IProductService extraction
 
**What changed:** All product query logic moved from `ProductsController` into `ProductService` in `Store.Infrastructure/Services/`. The controller went from ~60 lines to ~15 lines.
 
**Key lessons:**
- Interface lives in `Store.Application/Interfaces/` — defines the contract
- Implementation lives in `Store.Infrastructure/Services/` — holds EF Core queries
- `Store.Application` never references EF Core directly — only the interface is defined there
- DTOs are records in `Store.Application/DTOs/Products/` — typed, immutable request/response shapes
- Registered in `Store.Infrastructure/DependencyInjection.cs` as `AddScoped<IProductService, ProductService>()`
- Extension method renamed `AddInfrastructure()` — never `AddApplication()` in the Infrastructure project
 
**Common mistakes caught:**
- `CountAsync()` must run after all filters are applied — not immediately after the base query
- `Skip()` uses `(Page - 1) * PageSize` — not `(PageSize - 1) * PageSize`
- `Math.Ceiling` for total pages — `Math.Round` loses the last page when count is not divisible
- Price filters are inclusive: `>=` and `<=`, not `>` and `<`
- `InStock == true` not `InStock.HasValue` — the latter applies the filter even when the user sends `false`
 
**Final controller shape:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    public ProductsController(IProductService productService)
        => _productService = productService;
 
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
        => (await _productService.GetByIdAsync(id)).ToActionResult(this);
 
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] ProductSearchRequest request)
        => (await _productService.SearchAsync(request)).ToActionResult(this);
}
```
 
---
 
 
---
 
### Task A3 — IOrderService extraction
 
**What changed:** All order logic — create, get by id, confirm with stock — moved from `OrdersController` into `OrderService` in `Store.Infrastructure/Services/`. The controller went from ~150 lines to ~15 lines.
 
**Key lessons:**
 
**N+1 in service methods is still N+1.** Moving logic from a controller to a service does not fix a query problem. The batch load pattern must follow the logic wherever it goes — load all products with one `Contains()` query before the loop, look up in memory inside the loop.
 
**Services return `Result<T>`, not `IActionResult`.** The service has no knowledge of HTTP. It returns `Result.NotFound(...)`, `Result.BadRequest(...)`, `Result.Success(...)`. The controller maps it to HTTP with one call to `ToActionResult(this)`.
 
**Transactions belong in the service, not the controller.** The full Task 10 transaction — three SaveChanges, savepoint, catch-rollback-commit — moved into `ConfirmWithStockAsync` unchanged. The controller never knew about it and still does not.
 
**Partial failures need structured responses.** `BadRequest` was overloaded to carry a value alongside the error string — `Result<T>.BadRequest(string error, T value)` — so insufficient product details travel back to the caller as structured data, not a message string.
 
**`IsMovementCreated` flag on the response.** When the catch fires (movement records fail), the transaction commits the order confirmation and stock reduction but skips the audit trail. The response carries `IsMovementCreated = moves.Any()` so the caller knows whether audit records were written without having to query again.
 
**Guard clause order matters.** Zero stock must be checked before insufficient stock — zero is a subset of insufficient, so checking insufficient first makes the zero case unreachable.
 
**Common mistakes caught:**
- `order.StockMovements.Any()` — navigation never loaded, always false. Use `moves.Any()` — the local list.
- `OrderByDescending(o => o.Id)` for order number — Guid ordering is not sequential. Use `CountAsync() + 1`.
- Customer existence not validated — FK violation at SaveChanges becomes a 500 instead of a clean 404.
- `if (product is not null &&` inside a block that already checked for null — redundant guard after `continue`.
 
**Final controller shape:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    public OrdersController(IOrderService orderService)
        => _orderService = orderService;
 
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderCreateRequest request)
        => (await _orderService.CreateAsync(request)).ToActionResult(this);
 
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
        => (await _orderService.GetByIdAsync(id)).ToActionResult(this);
 
    [HttpPost("{id}/confirm-with-stock")]
    public async Task<IActionResult> ConfirmWithStock([FromRoute] Guid id)
        => (await _orderService.ConfirmWithStockAsync(id)).ToActionResult(this);
}
```
 
**Score:** 7/10 first attempt → 9.3/10 final
