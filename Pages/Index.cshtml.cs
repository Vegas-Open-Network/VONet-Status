using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace VONet_Stats.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IList<ServiceStatus> Services { get; private set; } = new List<ServiceStatus>();

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            Services = new List<ServiceStatus>
            {
                new ServiceStatus { Name = "API Gateway", Status = "Operational" },
                new ServiceStatus { Name = "Auth Service", Status = "Degraded" },
                new ServiceStatus { Name = "Database", Status = "Down" }
            };
        }

        public string StatusClass(string status) => status switch
        {
            "Operational" => "status-up",
            "Degraded" => "status-degraded",
            _ => "status-down"
        };
    }

    public class ServiceStatus
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
