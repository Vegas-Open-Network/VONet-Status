using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VONet_Stats.Services;

namespace VONet_Stats.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IStatusService _statusService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IStatusService statusService, ILogger<IndexModel> logger)
        {
            _statusService = statusService;
            _logger = logger;
        }

        public List<ServiceStatusViewModel> Services { get; set; } = new();
        public List<IncidentViewModel> RecentIncidents { get; set; } = new();
        public Services.SystemOverallStatus OverallStatus { get; set; } = new();
        public Dictionary<string, List<StatusHistoryPoint>> ServiceHistory { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                Services = await _statusService.GetCurrentStatusAsync();
                RecentIncidents = await _statusService.GetRecentIncidentsAsync();
                OverallStatus = await _statusService.GetOverallStatusAsync();

                // Load history for each service (last 30 days)
                foreach (var service in Services)
                {
                    var history = await _statusService.GetServiceHistoryAsync(service.ServiceId, 30);
                    ServiceHistory[service.ServiceId] = history;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading status page data");
                
                // Fallback to empty data with clear indication of the issue
                Services = new List<ServiceStatusViewModel>();
                RecentIncidents = new List<IncidentViewModel>();
                OverallStatus = new Services.SystemOverallStatus
                {
                    Status = "System Initializing - Please wait or run initial check",
                    StatusClass = "status-unknown",
                    Uptime = 0,
                    LastUpdated = DateTime.UtcNow
                };
                ServiceHistory = new Dictionary<string, List<StatusHistoryPoint>>();
            }
        }
    }

    public class ServiceStatus
    {
        public string Name { get; set; } = string.Empty;
        public ServiceStatusType Status { get; set; }
        public double Uptime { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public class OutageReport
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

    public enum ServiceStatusType
    {
        Operational,
        PartialOutage,
        MajorOutage,
        UnderMaintenance
    }

    public enum OutageStatusType
    {
        Investigating,
        Identified,
        Monitoring,
        Resolved
    }
}
