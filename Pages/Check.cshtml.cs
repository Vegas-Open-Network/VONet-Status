using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VONet_Stats.Services;
using Microsoft.Extensions.Options;
using VONet_Stats.Configuration;

namespace VONet_Stats.Pages;

public class CheckModel : PageModel
{
    private readonly IStatusService _statusService;
    private readonly ILogger<CheckModel> _logger;
    private readonly IConfiguration _configuration;

    public CheckModel(IStatusService statusService, ILogger<CheckModel> logger, IConfiguration configuration)
    {
        _statusService = statusService;
        _logger = logger;
        _configuration = configuration;
    }

    public string Message { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public bool IsAuthorized { get; set; } = false;

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        // Security check - require token or local access
        var expectedToken = _configuration["StatusConfiguration:CronToken"];
        var isLocal = IsLocalRequest();
        var hasValidToken = !string.IsNullOrEmpty(expectedToken) && !string.IsNullOrEmpty(token) && token == expectedToken;
        
        // Allow access if local OR if token is configured and matches
        // If no token is configured, allow local access only
        IsAuthorized = isLocal || (hasValidToken && !string.IsNullOrEmpty(expectedToken));

        // Log access attempts
        var clientInfo = $"{HttpContext.Connection.RemoteIpAddress} - User-Agent: {Request.Headers.UserAgent}";
        
        if (!IsAuthorized)
        {
            _logger.LogWarning("Unauthorized access attempt to check endpoint from {ClientInfo}. IsLocal: {IsLocal}, HasToken: {HasToken}, TokenConfigured: {TokenConfigured}", 
                clientInfo, isLocal, !string.IsNullOrEmpty(token), !string.IsNullOrEmpty(expectedToken));
            
            // Return 404 to hide the existence of this endpoint
            return NotFound();
        }

        _logger.LogInformation("Authorized service check triggered from {ClientInfo}", clientInfo);

        var startTime = DateTime.UtcNow;
        
        try
        {
            await _statusService.CheckAllServicesAsync();
            
            ExecutionTime = DateTime.UtcNow - startTime;
            Success = true;
            Message = $"Service checks completed successfully in {ExecutionTime.TotalSeconds:F2} seconds";
            
            _logger.LogInformation("Service check completed in {ExecutionTime}ms", ExecutionTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            ExecutionTime = DateTime.UtcNow - startTime;
            Success = false;
            Message = $"Error during service checks: {ex.Message}";
            
            _logger.LogError(ex, "Error during service check");
        }

        // Return JSON for automated tools, HTML for local/browser access
        if (Request.Headers.Accept.ToString().Contains("application/json") || 
            Request.Query.ContainsKey("format") && Request.Query["format"] == "json")
        {
            return new JsonResult(new
            {
                success = Success,
                message = Message,
                executionTimeMs = ExecutionTime.TotalMilliseconds,
                timestamp = DateTime.UtcNow
            });
        }

        return Page();
    }

    private bool IsLocalRequest()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp == null) return false;

        // Check if it's localhost/loopback
        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        return remoteIp.Equals(System.Net.IPAddress.Loopback) || 
               remoteIp.Equals(System.Net.IPAddress.IPv6Loopback) ||
               remoteIp.ToString() == "127.0.0.1" ||
               remoteIp.ToString() == "::1";
    }
}