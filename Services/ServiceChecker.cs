using VONet_Stats.Configuration;
using VONet_Stats.Data;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.Mail;

namespace VONet_Stats.Services;

public interface IServiceChecker
{
    Task<CheckResult> CheckServiceAsync(ServiceConfiguration config);
}

public class ServiceChecker : IServiceChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceChecker> _logger;

    public ServiceChecker(HttpClient httpClient, ILogger<ServiceChecker> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CheckResult> CheckServiceAsync(ServiceConfiguration config)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            switch (config.Type.ToUpper())
            {
                case "HTTP":
                case "HTTPS":
                    return await CheckHttpServiceAsync(config, stopwatch);
                case "TCP":
                    return await CheckTcpServiceAsync(config, stopwatch);
                case "PING":
                    return await CheckPingServiceAsync(config, stopwatch);
                case "SMTP":
                    return await CheckSmtpServiceAsync(config, stopwatch);
                default:
                    return new CheckResult
                    {
                        IsHealthy = false,
                        ResponseTime = stopwatch.ElapsedMilliseconds,
                        ErrorMessage = $"Unsupported service type: {config.Type}"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service {ServiceId}", config.Id);
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<CheckResult> CheckHttpServiceAsync(ServiceConfiguration config, System.Diagnostics.Stopwatch stopwatch)
    {
        if (string.IsNullOrEmpty(config.Url))
        {
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = "URL is required for HTTP checks"
            };
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            var response = await _httpClient.GetAsync(config.Url, cts.Token);
            stopwatch.Stop();

            var isHealthy = (int)response.StatusCode == config.ExpectedStatusCode;
            return new CheckResult
            {
                IsHealthy = isHealthy,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = isHealthy ? null : $"Expected status {config.ExpectedStatusCode}, got {(int)response.StatusCode}"
            };
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = $"Request timeout after {config.TimeoutSeconds} seconds"
            };
        }
    }

    private async Task<CheckResult> CheckTcpServiceAsync(ServiceConfiguration config, System.Diagnostics.Stopwatch stopwatch)
    {
        if (string.IsNullOrEmpty(config.Host) || !config.Port.HasValue)
        {
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = "Host and Port are required for TCP checks"
            };
        }

        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(config.Host, config.Port.Value);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(config.TimeoutSeconds));
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            stopwatch.Stop();

            if (completedTask == timeoutTask)
            {
                return new CheckResult
                {
                    IsHealthy = false,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = $"Connection timeout after {config.TimeoutSeconds} seconds"
                };
            }

            if (connectTask.IsFaulted)
            {
                return new CheckResult
                {
                    IsHealthy = false,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = connectTask.Exception?.GetBaseException().Message ?? "Connection failed"
                };
            }

            return new CheckResult
            {
                IsHealthy = true,
                ResponseTime = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<CheckResult> CheckPingServiceAsync(ServiceConfiguration config, System.Diagnostics.Stopwatch stopwatch)
    {
        if (string.IsNullOrEmpty(config.Host))
        {
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = "Host is required for PING checks"
            };
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(config.Host, config.TimeoutSeconds * 1000);
            stopwatch.Stop();

            var isHealthy = reply.Status == IPStatus.Success;
            return new CheckResult
            {
                IsHealthy = isHealthy,
                ResponseTime = isHealthy ? reply.RoundtripTime : stopwatch.ElapsedMilliseconds,
                ErrorMessage = isHealthy ? null : reply.Status.ToString()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<CheckResult> CheckSmtpServiceAsync(ServiceConfiguration config, System.Diagnostics.Stopwatch stopwatch)
    {
        if (string.IsNullOrEmpty(config.Host) || !config.Port.HasValue)
        {
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = "Host and Port are required for SMTP checks"
            };
        }

        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(config.Host, config.Port.Value);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(config.TimeoutSeconds));
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            stopwatch.Stop();

            if (completedTask == timeoutTask)
            {
                return new CheckResult
                {
                    IsHealthy = false,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = $"SMTP connection timeout after {config.TimeoutSeconds} seconds"
                };
            }

            if (connectTask.IsFaulted)
            {
                return new CheckResult
                {
                    IsHealthy = false,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = connectTask.Exception?.GetBaseException().Message ?? "SMTP connection failed"
                };
            }

            return new CheckResult
            {
                IsHealthy = true,
                ResponseTime = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}

public class CheckResult
{
    public bool IsHealthy { get; set; }
    public double ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
}