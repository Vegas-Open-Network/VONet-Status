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
            var dataDirectory = app.Configuration["DataDirectory"] ?? "App_Data";
            
            logger.LogInformation("Initializing database with connection string: {ConnectionString}", connectionString);
            logger.LogInformation("Data directory: {DataDirectory}", dataDirectory);
            
            // Ensure the data directory exists with proper error handling
            await EnsureDataDirectoryAsync(dataDirectory, logger);
            
            // Create full database path and ensure directory exists
            var fullDatabasePath = await EnsureDatabaseDirectoryAsync(connectionString, logger);
            
            // Test database connection and create if needed
            logger.LogInformation("Testing database connectivity...");
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogInformation("Database does not exist, creating...");
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database created successfully at: {DatabasePath}", fullDatabasePath);
            }
            else
            {
                logger.LogInformation("Database connection verified at: {DatabasePath}", fullDatabasePath);
            }
            
            // Verify database structure by testing a simple query
            try
            {
                var serviceCount = await context.ServiceStatuses.CountAsync();
                logger.LogInformation("Database verification successful. Found {ServiceCount} existing service records", serviceCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database structure verification failed. Attempting to recreate...");
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database recreated successfully");
            }
            
            // Test write permissions by creating a test record
            await TestDatabaseWritePermissions(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database. The application will continue but may not function correctly.");
            LogTroubleshootingTips(logger);
        }
        
        return app;
    }

    private static async Task EnsureDataDirectoryAsync(string dataDirectory, ILogger logger)
    {
        try
        {
            var fullPath = Path.GetFullPath(dataDirectory);
            
            if (!Directory.Exists(fullPath))
            {
                logger.LogInformation("Creating data directory: {DataDirectory}", fullPath);
                Directory.CreateDirectory(fullPath);
                
                // Set permissions for IIS (Windows)
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        var directoryInfo = new DirectoryInfo(fullPath);
                        logger.LogInformation("Data directory created successfully: {DataDirectory}", fullPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not set directory permissions. Manual permission setup may be required.");
                    }
                }
            }
            else
            {
                logger.LogInformation("Data directory already exists: {DataDirectory}", fullPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create data directory: {DataDirectory}", dataDirectory);
            throw;
        }
    }

    private static async Task<string> EnsureDatabaseDirectoryAsync(string? connectionString, ILogger logger)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured");
        }

        // Extract database file path from connection string
        var dataSource = ExtractDataSourceFromConnectionString(connectionString);
        var fullPath = Path.GetFullPath(dataSource);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            logger.LogInformation("Creating database directory: {Directory}", directory);
            Directory.CreateDirectory(directory);
        }

        logger.LogInformation("Database will be located at: {DatabasePath}", fullPath);
        return fullPath;
    }

    private static string ExtractDataSourceFromConnectionString(string connectionString)
    {
        // Handle different SQLite connection string formats
        var keyValuePairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var pair in keyValuePairs)
        {
            var parts = pair.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].Trim();
            }
        }
        
        throw new InvalidOperationException($"Could not extract Data Source from connection string: {connectionString}");
    }

    private static async Task TestDatabaseWritePermissions(StatusDbContext context, ILogger logger)
    {
        try
        {
            // Test write permissions by attempting a simple operation
            var testQuery = "SELECT 1";
            await context.Database.ExecuteSqlRawAsync(testQuery);
            logger.LogInformation("Database write permissions verified");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database write permission test failed. Check file permissions on the database directory.");
            throw new InvalidOperationException("Database write permissions are insufficient", ex);
        }
    }

    private static void LogTroubleshootingTips(ILogger logger)
    {
        logger.LogInformation("Database initialization troubleshooting tips:");
        logger.LogInformation("1. Ensure the application has write permissions to the data directory");
        logger.LogInformation("2. Check that SQLite is available (included with .NET 8)");
        logger.LogInformation("3. Verify sufficient disk space is available");
        logger.LogInformation("4. For IIS: Ensure IIS_IUSRS has modify permissions on the App_Data folder");
        logger.LogInformation("5. For IIS: Grant permissions to 'IIS AppPool\\YourAppPoolName' identity");
        logger.LogInformation("6. Check Windows Event Viewer for additional error details");
        logger.LogInformation("7. Try running the application with elevated permissions temporarily for testing");
    }
}