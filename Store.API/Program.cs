using Microsoft.EntityFrameworkCore;
using Store.Application;
using Store.Infrastructure;
using Store.Infrastructure.Interceptors;
using Store.Infrastructure.Presistence;

namespace Store.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            // Register the AuditInterceptor as a scoped service, which means a new instance will be created for each HTTP request.
            builder.Services.AddScoped<AuditInterceptor>();

            builder.Services.AddInfracstructure();

            // Configure the database context to use SQL Server with the connection string from appsettings.json
            // The MigrationsAssembly option specifies the assembly where the EF Core migrations are located, which is "Store.Infrastructure" in this case.

            builder.Services.AddDbContext<ApplicationDbContext>((ServiceProvider,option) =>
            {
                option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                    sql => sql.MigrationsAssembly("Store.Infrastructure"));
                // Add the AuditInterceptor to the DbContext options, which will allow it to intercept database operations for auditing purposes.
                option.AddInterceptors(
                    ServiceProvider.GetRequiredService<AuditInterceptor>());
                // Configure logging for the DbContext to log SQL queries and other information to
                // the console with a log level of Information. This can be useful for debugging and monitoring database interactions.
                option.LogTo(Console.WriteLine,LogLevel.Information)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors();
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();


            app.MapControllers();
            using (var scope = app.Services.CreateScope())
            {
                // Get the ApplicationDbContext instance from the service provider
                // apply any pending migrations to the database
                // and seed the database with initial data using the Seeder class.
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
                await Seeder.SeedAsync(db);
            }

                app.Run();
        }
    }
}

