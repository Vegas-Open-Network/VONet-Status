namespace VONet_Stats.Configuration;

public class StatusConfiguration
{
    public int CheckIntervalMinutes { get; set; } = 5;
    public int HistoryRetentionDays { get; set; } = 90;
    public string? CronToken { get; set; }
    public List<ServiceConfiguration> Services { get; set; } = new();
    public NotificationConfiguration Notifications { get; set; } = new();
}

public class ServiceConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // HTTP, TCP, SMTP, PING
    public string? Url { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public int ExpectedStatusCode { get; set; } = 200;
    public int TimeoutSeconds { get; set; } = 30;
    public int CheckInterval { get; set; } = 5; // Minutes
    public bool Enabled { get; set; } = true;
}

public class NotificationConfiguration
{
    public EmailNotificationConfig Email { get; set; } = new();
    public WebhookNotificationConfig Webhook { get; set; } = new();
}

public class EmailNotificationConfig
{
    public bool Enabled { get; set; }
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> ToAddresses { get; set; } = new();
}

public class WebhookNotificationConfig
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
}