using Microsoft.Extensions.Options;
using VONet_Stats.Configuration;
using VONet_Stats.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace VONet_Stats.Services;

public interface IStatusService
{
    Task<List<ServiceStatusViewModel>> GetCurrentStatusAsync();
    Task<List<IncidentViewModel>> GetRecentIncidentsAsync();
    Task<SystemOverallStatus> GetOverallStatusAsync();
    Task<List<StatusHistoryPoint>> GetServiceHistoryAsync(string serviceId, int days = 30);
    Task CheckAllServicesAsync();
    Task<bool> IsDatabaseHealthyAsync();
}

public class StatusService : IStatusService
{
    private readonly StatusDbContext _context;
    private readonly IServiceChecker _serviceChecker;
    private readonly IOptions<StatusConfiguration> _config;
    private readonly ILogger<StatusService> _logger;

    public StatusService(
        StatusDbContext context,
        IServiceChecker serviceChecker,
        IOptions<StatusConfiguration> config,
        ILogger<StatusService> logger)
    {
        _context = context;
        _serviceChecker = serviceChecker;
        _config = config;
        _logger = logger;
    }

    public async Task<List<ServiceStatusViewModel>> GetCurrentStatusAsync()
    {
        try
        {
            // Get configured service IDs from appsettings
            var configuredServiceIds = _config.Value.Services
                .Where(s => s.Enabled)
                .Select(s => s.Id)
                .ToHashSet();

            // Only get services that are both in database AND in current configuration
            var statuses = await _context.ServiceStatuses
                .Where(s => s.IsEnabled && configuredServiceIds.Contains(s.ServiceId))
                .OrderBy(s => s.Name)
                .ToListAsync();

            var result = new List<ServiceStatusViewModel>();

            foreach (var status in statuses)
            {
                var uptime = await CalculateUptimeAsync(status.ServiceId, 30);
                result.Add(new ServiceStatusViewModel
                {
                    ServiceId = status.ServiceId,
                    Name = status.Name,
                    Description = status.Description,
                    Status = status.Status,
                    Uptime = uptime,
                    LastChecked = status.LastChecked,
                    ResponseTime = status.ResponseTimeMs
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current service status");
            
            // Return empty list if database is not available
            // This allows the page to load even if there are database issues
            return new List<ServiceStatusViewModel>();
        }
    }

    public async Task<List<IncidentViewModel>> GetRecentIncidentsAsync()
    {
        try
        {
            var incidents = await _context.Incidents
                .Where(i => i.StartTime >= DateTime.UtcNow.AddDays(-30))
                .OrderByDescending(i => i.StartTime)
                .Take(10)
                .ToListAsync();

            return incidents.Select(i => new IncidentViewModel
            {
                Title = i.Title,
                Description = i.Description,
                StartTime = i.StartTime,
                EndTime = i.EndTime,
                Status = (OutageStatusType)(int)i.Status,
                AffectedServices = JsonSerializer.Deserialize<string[]>(i.AffectedServices) ?? Array.Empty<string>()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent incidents");
            
            // Return empty list if database is not available
            return new List<IncidentViewModel>();
        }
    }

    public async Task<SystemOverallStatus> GetOverallStatusAsync()
    {
        try
        {
            // Get configured service IDs from appsettings
            var configuredServiceIds = _config.Value.Services
                .Where(s => s.Enabled)
                .Select(s => s.Id)
                .ToHashSet();

            // Only get services that are both in database AND in current configuration
            var statuses = await _context.ServiceStatuses
                .Where(s => s.IsEnabled && configuredServiceIds.Contains(s.ServiceId))
                .ToListAsync();

            if (!statuses.Any())
            {
                return new SystemOverallStatus
                {
                    Status = "No Services Configured",
                    StatusClass = "status-unknown",
                    Uptime = 0,
                    LastUpdated = DateTime.UtcNow
                };
            }

            var operationalCount = statuses.Count(s => s.Status == ServiceStatusType.Operational);
            var totalServices = statuses.Count;
            
            string status;
            string statusClass;

            if (operationalCount == totalServices)
            {
                status = "All Systems Operational";
                statusClass = "status-operational";
            }
            else if (operationalCount >= totalServices * 0.8)
            {
                status = "Partial System Outage";
                statusClass = "status-partial";
            }
            else
            {
                status = "Major System Outage";
                statusClass = "status-major";
            }

            var overallUptime = 0.0;
            foreach (var serviceStatus in statuses)
            {
                var uptime = await CalculateUptimeAsync(serviceStatus.ServiceId, 30);
                overallUptime += uptime;
            }
            overallUptime /= statuses.Count;

            return new SystemOverallStatus
            {
                Status = status,
                StatusClass = statusClass,
                Uptime = overallUptime,
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overall system status");
            
            // Return a fallback status if database is not available
            return new SystemOverallStatus
            {
                Status = "System Initializing",
                StatusClass = "status-unknown",
                Uptime = 0,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    public async Task<List<StatusHistoryPoint>> GetServiceHistoryAsync(string serviceId, int days = 30)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            
            var history = await _context.StatusHistory
                .Where(h => h.ServiceId == serviceId && h.Timestamp >= startDate)
                .OrderBy(h => h.Timestamp)
                .ToListAsync();

            var result = new List<StatusHistoryPoint>();
            var currentDate = startDate.Date;
            var endDate = DateTime.UtcNow.Date;

            while (currentDate <= endDate)
            {
                var dayHistory = history.Where(h => h.Timestamp.Date == currentDate).ToList();
                
                if (dayHistory.Any())
                {
                    var operationalCount = dayHistory.Count(h => h.Status == ServiceStatusType.Operational);
                    var uptimePercentage = (double)operationalCount / dayHistory.Count * 100;
                    
                    result.Add(new StatusHistoryPoint
                    {
                        Date = currentDate,
                        UptimePercentage = uptimePercentage,
                        Status = GetDominantStatus(dayHistory)
                    });
                }
                else
                {
                    // No data for this day, assume unknown
                    result.Add(new StatusHistoryPoint
                    {
                        Date = currentDate,
                        UptimePercentage = 0,
                        Status = ServiceStatusType.MajorOutage
                    });
                }

                currentDate = currentDate.AddDays(1);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service history for {ServiceId}", serviceId);
            
            // Return empty list if database is not available
            return new List<StatusHistoryPoint>();
        }
    }

    public async Task CheckAllServicesAsync()
    {
        _logger.LogInformation("Starting service checks");

        try
        {
            var serviceConfigs = _config.Value.Services.Where(s => s.Enabled).ToList();

            // First, mark services as disabled if they're no longer in configuration
            await DisableRemovedServicesAsync();

            foreach (var serviceConfig in serviceConfigs)
            {
                try
                {
                    var result = await _serviceChecker.CheckServiceAsync(serviceConfig);
                    await UpdateServiceStatusAsync(serviceConfig, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking service {ServiceId}", serviceConfig.Id);
                }
            }

            // Clean up old history
            await CleanupOldHistoryAsync();

            _logger.LogInformation("Completed service checks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during service checks - database may not be available");
            throw; // Re-throw for the calling code to handle
        }
    }

    private async Task DisableRemovedServicesAsync()
    {
        try
        {
            // Get configured service IDs
            var configuredServiceIds = _config.Value.Services
                .Select(s => s.Id)
                .ToHashSet();

            // Find services in database that are no longer in configuration
            var removedServices = await _context.ServiceStatuses
                .Where(s => !configuredServiceIds.Contains(s.ServiceId))
                .ToListAsync();

            if (removedServices.Any())
            {
                _logger.LogInformation("Disabling {Count} services no longer in configuration: {ServiceIds}", 
                    removedServices.Count, 
                    string.Join(", ", removedServices.Select(s => s.ServiceId)));

                // Mark them as disabled instead of deleting (preserves history)
                foreach (var service in removedServices)
                {
                    service.IsEnabled = false;
                    _logger.LogWarning("Service {ServiceId} disabled - no longer in configuration", service.ServiceId);
                }

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling removed services");
            // Don't re-throw - this is not critical for service checks to continue
        }
    }

    private async Task UpdateServiceStatusAsync(ServiceConfiguration config, CheckResult result)
    {
        try
        {
            var now = DateTime.UtcNow;
            var status = result.IsHealthy ? ServiceStatusType.Operational : ServiceStatusType.MajorOutage;

            // Get or create service status
            var serviceStatus = await _context.ServiceStatuses
                .FirstOrDefaultAsync(s => s.ServiceId == config.Id);

            if (serviceStatus == null)
            {
                serviceStatus = new ServiceStatus
                {
                    ServiceId = config.Id,
                    Name = config.Name,
                    Description = config.Description,
                    Status = status,
                    ResponseTimeMs = result.ResponseTime,
                    LastChecked = now,
                    LastStatusChange = now,
                    ErrorMessage = result.ErrorMessage,
                    IsEnabled = config.Enabled
                };
                _context.ServiceStatuses.Add(serviceStatus);
                _logger.LogInformation("Created new service status for {ServiceId}", config.Id);
            }
            else
            {
                var statusChanged = serviceStatus.Status != status;
                var wasDisabled = !serviceStatus.IsEnabled;
                
                // Update all fields from configuration (in case they changed)
                serviceStatus.Name = config.Name;
                serviceStatus.Description = config.Description;
                serviceStatus.Status = status;
                serviceStatus.ResponseTimeMs = result.ResponseTime;
                serviceStatus.LastChecked = now;
                serviceStatus.ErrorMessage = result.ErrorMessage;
                serviceStatus.IsEnabled = config.Enabled; // Re-enable if it was disabled
                
                if (statusChanged)
                {
                    serviceStatus.LastStatusChange = now;
                    _logger.LogWarning("Service {ServiceId} status changed to {Status}", config.Id, status);
                }

                if (wasDisabled && config.Enabled)
                {
                    _logger.LogInformation("Service {ServiceId} re-enabled in configuration", config.Id);
                }
            }

            // Add to history
            var historyEntry = new StatusHistory
            {
                ServiceId = config.Id,
                Status = status,
                ResponseTimeMs = result.ResponseTime,
                Timestamp = now,
                ErrorMessage = result.ErrorMessage
            };
            _context.StatusHistory.Add(historyEntry);

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service status for {ServiceId}", config.Id);
            // Don't re-throw - log the error but continue with other services
        }
    }

    public async Task<bool> IsDatabaseHealthyAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return false;
        }
    }

    private async Task<double> CalculateUptimeAsync(string serviceId, int days)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        
        var totalChecks = await _context.StatusHistory
            .CountAsync(h => h.ServiceId == serviceId && h.Timestamp >= startDate);

        if (totalChecks == 0) return 100.0;

        var operationalChecks = await _context.StatusHistory
            .CountAsync(h => h.ServiceId == serviceId && 
                           h.Timestamp >= startDate && 
                           h.Status == ServiceStatusType.Operational);

        return (double)operationalChecks / totalChecks * 100.0;
    }

    private ServiceStatusType GetDominantStatus(List<StatusHistory> dayHistory)
    {
        var statusCounts = dayHistory.GroupBy(h => h.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        return statusCounts.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    private async Task CleanupOldHistoryAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-_config.Value.HistoryRetentionDays);
        
        var oldHistory = _context.StatusHistory.Where(h => h.Timestamp < cutoffDate);
        _context.StatusHistory.RemoveRange(oldHistory);
        
        await _context.SaveChangesAsync();
    }
}

public class ServiceStatusViewModel
{
    public string ServiceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceStatusType Status { get; set; }
    public double Uptime { get; set; }
    public DateTime LastChecked { get; set; }
    public double ResponseTime { get; set; }
}

public class IncidentViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public OutageStatusType Status { get; set; }
    public string[] AffectedServices { get; set; } = Array.Empty<string>();
}

public class SystemOverallStatus
{
    public string Status { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public double Uptime { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class StatusHistoryPoint
{
    public DateTime Date { get; set; }
    public double UptimePercentage { get; set; }
    public ServiceStatusType Status { get; set; }
}

public enum OutageStatusType
{
    Investigating = 0,
    Identified = 1,
    Monitoring = 2,
    Resolved = 3
}