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
| AsSplitQuery() | Use when loading 2+ large collections — eliminates cartesian row multiplication |
| Select() over Include() for reads | Select() fetches only named columns — Include() loads full entities including unused columns |
| SQL logging | `LogTo` + `EnableSensitiveDataLogging` — always verify what EF actually sends |
 
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
 
First attempt scores: **3.7 → 4.7 → 6.3 → 7.0 → 7.0 → 7.0 → 9.0 → 10.0**
