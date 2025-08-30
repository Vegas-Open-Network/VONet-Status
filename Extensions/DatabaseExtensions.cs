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
            
            // Get the correct application root path
            var webHostEnvironment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var contentRoot = webHostEnvironment.ContentRootPath;
            
            logger.LogInformation("?? VONet Status - Database Initialization");
            logger.LogInformation("?? Application root: {ContentRoot}", contentRoot);
            logger.LogInformation("??? Data directory: {DataDirectory}", dataDirectory);
            
            // Create data folder and test permissions
            var dataFolderPath = await CreateDataFolderAsync(dataDirectory, contentRoot, logger);
            
            // Initialize database
            await InitializeDatabaseAsync(context, connectionString, contentRoot, logger);
            
            logger.LogInformation("? Database initialization completed successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError("? PERMISSION ERROR: {Message}", ex.Message);
            logger.LogError("?? SOLUTION: Grant write permissions to the application folder or App_Data subfolder");
            logger.LogError("?? For IIS: icacls \"{Path}\\App_Data\" /grant \"IIS_IUSRS:(OI)(CI)M\"", 
                scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>().ContentRootPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogError("? FOLDER ERROR: {Message}", ex.Message);
            logger.LogError("?? SOLUTION: Ensure the application has access to create folders");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? DATABASE INITIALIZATION FAILED");
            logger.LogError("?? Troubleshooting steps:");
            logger.LogError("   1. Check folder permissions (write access required)");
            logger.LogError("   2. Ensure .NET 8 Hosting Bundle is installed");
            logger.LogError("   3. Verify sufficient disk space");
            logger.LogError("   4. Check Windows Event Viewer for additional details");
        }
        
        return app;
    }

    private static async Task<string> CreateDataFolderAsync(string dataDirectory, string contentRoot, ILogger logger)
    {
        try
        {
            // Resolve full path
            string dataFolderPath;
            if (Path.IsPathRooted(dataDirectory))
            {
                dataFolderPath = dataDirectory;
            }
            else
            {
                dataFolderPath = Path.Combine(contentRoot, dataDirectory);
            }
            
            dataFolderPath = Path.GetFullPath(dataFolderPath);
            logger.LogInformation("?? Data folder path: {DataFolderPath}", dataFolderPath);
            
            // Create folder if it doesn't exist
            if (!Directory.Exists(dataFolderPath))
            {
                logger.LogInformation("?? Creating data folder...");
                Directory.CreateDirectory(dataFolderPath);
                logger.LogInformation("? Data folder created successfully");
            }
            else
            {
                logger.LogInformation("?? Data folder already exists");
            }
            
            // Test write permissions immediately
            await TestWritePermissionsAsync(dataFolderPath, logger);
            
            return dataFolderPath;
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException($"Cannot create or access data folder '{dataDirectory}'. Check folder permissions.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new DirectoryNotFoundException($"Cannot access parent directory for '{dataDirectory}'. Check path and permissions.");
        }
    }

    private static async Task TestWritePermissionsAsync(string folderPath, ILogger logger)
    {
        try
        {
            var testFile = Path.Combine(folderPath, "permission_test.tmp");
            await File.WriteAllTextAsync(testFile, "Permission test");
            File.Delete(testFile);
            logger.LogInformation("? Write permissions verified");
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException($"Cannot write to data folder '{folderPath}'. The application needs write permissions to this folder.");
        }
    }

    private static async Task InitializeDatabaseAsync(StatusDbContext context, string? connectionString, string contentRoot, ILogger logger)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured in appsettings.json");
        }

        try
        {
            // Extract and resolve database path
            var dataSource = ExtractDataSourceFromConnectionString(connectionString);
            string databasePath;
            
            if (Path.IsPathRooted(dataSource))
            {
                databasePath = dataSource;
            }
            else
            {
                databasePath = Path.Combine(contentRoot, dataSource);
            }
            
            databasePath = Path.GetFullPath(databasePath);
            logger.LogInformation("??? Database file: {DatabasePath}", databasePath);
            
            // Ensure database directory exists
            var databaseDir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(databaseDir) && !Directory.Exists(databaseDir))
            {
                Directory.CreateDirectory(databaseDir);
                logger.LogInformation("?? Created database directory");
            }
            
            // Test database connectivity
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogInformation("?? Creating database...");
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("? Database created successfully");
            }
            else
            {
                logger.LogInformation("? Database connection verified");
            }
            
            // Verify database structure
            var serviceCount = await context.ServiceStatuses.CountAsync();
            logger.LogInformation("?? Found {ServiceCount} existing service records", serviceCount);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Database initialization failed: {ex.Message}", ex);
        }
    }

    private static string ExtractDataSourceFromConnectionString(string connectionString)
    {
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
}