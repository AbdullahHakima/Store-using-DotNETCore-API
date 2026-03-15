# Store API

A robust ASP.NET Core Web API for an e-commerce platform, built with **.NET 10** and **Entity Framework Core**. This project demonstrates enterprise-level patterns, Clean Architecture principles, and advanced EF Core features to guarantee data integrity and high performance.

## 🏗 Architecture

The project is divided into distinct layers to separate concerns:

*   **Store.Domain:** Contains the enterprise models (`Entities`), enums, and core business rules. Models inherit from a `BaseEntity`.
*   **Store.Infrastructure:** Contains the database context (`ApplicationDbContext`), EF Core migrations, entity configurations, interceptors, and data seeding logic.
*   **Store.API:** The presentation layer representing the HTTP endpoints (Controllers), dependency injection configuration, Swagger setup, and application bootstrapping.

## ✨ Key Features

*   **Domain-Driven Design (DDD) Patterns:** Entities encapsulate their own behavior. For instance, `Order` manages its `OrderItems` internally (Aggregate Root pattern) and handles total calculation and status transitions itself (`ConfirmOrder()`, `CancelOrder()`).
*   **Automated Auditing:** Uses a custom EF Core `SaveChangesInterceptor` (`AuditInterceptor`) to automatically stamp entities with `CreatedAt` and `UpdatedAt` timestamps during database saves.
*   **Soft Deletion:** Implements Global Query Filters in EF Core. Entities marked as `IsDeleted` are automatically filtered out from all standard `SELECT` queries across the application.
*   **Concurrency Control:** All entities contain a `RowVersion` property configured as a concurrency token to prevent lost updates in highly concurrent scenarios.
*   **Optimized Data Access:** Read operations in the API (like `ProductController`) extensively utilize `.AsNoTracking()` and explicit projections (`.Select()`) to fetch exactly what is needed without change-tracking overhead.
*   **Automated Data Seeding:** A built-in `Seeder` class populates the database with initial categories, tags, products, customers, and orders on application startup if the database is empty.
*   **Circular Reference Handling:** API responses are configured to handle model object cycles safely (e.g., `ReferenceHandler.IgnoreCycles`).

## 📦 Domain Models

*   **`Product`**: Represents items available for purchase. Belongs to a `Category` and can have many `Tags`.
*   **`Category`**: Groups related products (e.g., Electronics, Clothing).
*   **`Tag`**: Allows many-to-many descriptive labeling of products.
*   **`Customer`**: Holds user information, including a complex value type `Address`.
*   **`Order`**: The aggregate root for purchases. Holds `OrderItems`, links to a `Customer`, and tracks `OrderStatus`.
*   **`OrderItem`**: Represents line items tied to an `Order` and a `Product`.
*   **`Payment`**: Tracks financial transactions tied to an `Order`.

## 🚀 Getting Started

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

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guidelines](./CONTRIBUTING.md) for details on submitting issues, proposing features, and code guidelines.

## 📜 License

This project is licensed under the [MIT License](./LICENSE.txt).