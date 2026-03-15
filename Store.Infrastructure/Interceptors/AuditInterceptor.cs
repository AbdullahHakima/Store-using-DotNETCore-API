using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Store.Domain.Entities;
namespace Store.Infrastructure.Interceptors;

public class AuditInterceptor:SaveChangesInterceptor
{
    // This method is called after the changes have been saved to the database
    // You can use this method to perform any additional actions after the save operation, such as logging or auditing
    // The eventData parameter contains information about the save operation, such as the context and the entities that were changed
    // The result parameter contains the number of state entries written to the database 
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stemp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
    // This method is the asynchronous version of the SavedChanges method
    // It is called after the changes have been saved to the database in an asynchronous manner
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stemp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Stemp(DbContext? context)
    {
        if (context == null) return;

        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if(entry.State== EntityState.Added)
            {
                // For new entities, set the CreatedAt property to the current date and time
                // and ensure that the Id is set to a new Guid if it is not already assigned
                // This ensures that every new entity has a unique identifier and a timestamp for when it was created
                entry.Entity.Id = entry.Entity.Id==Guid.Empty?Guid.NewGuid():entry.Entity.Id;
                entry.Entity.CreatedAt = now;

            }
            if(entry.State== EntityState.Modified)
            {
                // For modified entities, set the UpdatedAt property to the current date and time
                // This allows you to track when an entity was last updated, which can be useful for auditing and debugging purposes
                
                entry.Entity.UpdatedAt = now;
                // Ensure that the CreatedAt property is not modified when updating an entity
                //  

                entry.Property(e=>e.CreatedAt).IsModified = false;

            }
        }
    }
}
