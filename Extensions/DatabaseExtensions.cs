using Microsoft.EntityFrameworkCore;
using VONet_Stats.Data;

namespace VONet_Stats.Extensions;

public static class DatabaseExtensions
{
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<StatusDbContext>();
            var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
            
            logger.LogInformation("Initializing database with connection string: {ConnectionString}", connectionString);
            
            // Ensure the database file directory exists
            if (connectionString?.Contains("Data Source=") == true)
            {
                var dataSource = connectionString.Split("Data Source=")[1].Split(";")[0];
                var fullPath = Path.GetFullPath(dataSource);
                var directory = Path.GetDirectoryName(fullPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    logger.LogInformation("Created database directory: {Directory}", directory);
                }
                
                logger.LogInformation("Database file will be created at: {DatabasePath}", fullPath);
            }
            
            // Test database connection and create if needed
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogInformation("Database does not exist, creating...");
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database created successfully");
            }
            else
            {
                logger.LogInformation("Database connection verified");
            }
            
            // Verify tables exist by checking one of our main tables
            try
            {
                var serviceCount = await context.ServiceStatuses.CountAsync();
                logger.LogInformation("Database initialized successfully. Found {ServiceCount} existing service records", serviceCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not query service statuses table. Database may need initialization.");
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database re-created after table verification failed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database. The application will continue but may not function correctly until database issues are resolved.");
            logger.LogInformation("Troubleshooting tips:");
            logger.LogInformation("1. Ensure the application has write permissions to the database directory");
            logger.LogInformation("2. Check that SQLite is properly installed");
            logger.LogInformation("3. Verify the connection string in appsettings.json");
            logger.LogInformation("4. Try running the application as administrator (Windows) or with appropriate permissions (Linux)");
        }
        
        return app;
    }
}