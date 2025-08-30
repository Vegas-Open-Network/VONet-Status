# VONet Status Dashboard

A comprehensive service status monitoring dashboard built with ASP.NET Core Razor Pages, featuring real-time service health checks, historical uptime tracking, and incident management.

## Features

- ?? **Multi-Protocol Service Monitoring**: HTTP/HTTPS, TCP, SMTP, and PING checks
- ?? **Visual Status History**: 30-day history displayed as colored squares
- ?? **Responsive Design**: Works on desktop and mobile devices
- ??? **SQLite Database**: Self-contained data storage with no external dependencies
- ?? **Configurable Checks**: JSON-based configuration for easy service management
- ?? **Automated Monitoring**: Secure cron job compatible endpoint for scheduled checks
- ?? **Uptime Calculations**: Real-time uptime percentages and historical data
- ?? **Incident Tracking**: Built-in incident management system
- ?? **Security**: Token-based authentication and local access restrictions
- ?? **Dynamic Configuration**: Services automatically appear/disappear based on configuration changes

## Quick Start

1. **Clone and Run**:
   ```bash
   git clone https://github.com/Vegas-Open-Network/VONet-Status
   cd VONet-Status
   dotnet run
   ```

2. **Access the Dashboard**: Open `https://localhost:5001` in your browser

3. **First Run**: The application will automatically create the SQLite database on first startup

4. **Configure Security**: Set up a secure token in `appsettings.json`

5. **Run Initial Check**: Access `/check` locally to populate initial data, or wait for the system to initialize automatically

### First Run Experience

When you first start the application:

1. **Database Creation**: SQLite database is automatically created in the application directory
2. **Initial State**: The status page will show "System Initializing" until first checks are run
3. **Configuration**: Services are loaded from `appsettings.json`
4. **Auto-Population**: The system will begin monitoring configured services automatically

If you encounter any issues during first run, check the application logs for detailed error information.

## Configuration

### Dynamic Service Management

The system automatically manages services based on your configuration:

- **Adding Services**: New services in `appsettings.json` will appear on the status page after the next check
- **Removing Services**: Services removed from `appsettings.json` will disappear from the status page but retain their historical data
- **Modifying Services**: Changes to service names, descriptions, or settings are applied automatically
- **Re-adding Services**: Previously removed services can be re-added and will restore their historical data

### Service Configuration

Edit `appsettings.json` to configure monitored services:

```json
{
  "StatusConfiguration": {
    "CheckIntervalMinutes": 5,
    "HistoryRetentionDays": 90,
    "CronToken": "your-secure-random-token-32-chars-minimum",
    "Services": [
      {
        "Id": "unique-service-id",
        "Name": "Display Name",
        "Description": "Service description",
        "Type": "HTTP|TCP|SMTP|PING",
        "Url": "https://example.com",
        "ExpectedStatusCode": 200,
        "TimeoutSeconds": 30,
        "CheckInterval": 5,
        "Enabled": true
      }
    ]
  }
}
```

### Service Lifecycle Management

The system handles service configuration changes intelligently:

1. **Service Addition**: 
   - Add new service to `Services` array in `appsettings.json`
   - Service appears on status page after next check cycle
   - New database records are created automatically

2. **Service Removal**:
   - Remove service from `Services` array in `appsettings.json`
   - Service disappears from status page after next check cycle
   - Historical data is preserved in database (service marked as disabled)
   - No data loss occurs

3. **Service Modification**:
   - Update service properties in `appsettings.json`
   - Changes are applied during next check cycle
   - Name, description, and check parameters are updated

4. **Service Re-addition**:
   - Add previously removed service back to configuration
   - Service reappears on status page with historical data intact
   - Seamless restoration of monitoring

### Security Configuration

The `/check` endpoint is protected by multiple security layers:

1. **Local Access**: Automatically allowed from localhost/127.0.0.1
2. **Token Authentication**: Required for remote access
3. **Stealth Mode**: Returns 404 for unauthorized attempts
4. **Logging**: All access attempts are logged for monitoring

#### Setting Up Secure Token

Generate a secure random token (32+ characters):

```bash
# Using openssl
openssl rand -base64 32

# Using PowerShell
[System.Web.Security.Membership]::GeneratePassword(32, 0)

# Using Node.js
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

Add the token to your `appsettings.json`:

```json
{
  "StatusConfiguration": {
    "CronToken": "your-generated-secure-token-here"
  }
}
```

### Service Types and Required Fields

#### HTTP/HTTPS Services
```json
{
  "Type": "HTTP",
  "Url": "https://api.example.com/health",
  "ExpectedStatusCode": 200,
  "TimeoutSeconds": 30
}
```

#### TCP Services
```json
{
  "Type": "TCP", 
  "Host": "database.example.com",
  "Port": 5432,
  "TimeoutSeconds": 10
}
```

#### SMTP Services
```json
{
  "Type": "SMTP",
  "Host": "smtp.example.com", 
  "Port": 587,
  "TimeoutSeconds": 15
}
```

#### PING Services
```json
{
  "Type": "PING",
  "Host": "gateway.example.com",
  "TimeoutSeconds": 5
}
```

### Configuration Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier for the service (used for historical data) |
| `Name` | string | Yes | Display name shown on status page |
| `Description` | string | No | Additional service description |
| `Type` | string | Yes | Service type: HTTP, TCP, SMTP, or PING |
| `Url` | string | HTTP only | Full URL to check |
| `Host` | string | TCP/SMTP/PING | Hostname or IP address |
| `Port` | number | TCP/SMTP | Port number to check |
| `ExpectedStatusCode` | number | HTTP only | Expected HTTP status code (default: 200) |
| `TimeoutSeconds` | number | Yes | Timeout for the check in seconds |
| `CheckInterval` | number | Yes | Check interval in minutes |
| `Enabled` | boolean | Yes | Whether this service should be monitored |

### Global Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `CheckIntervalMinutes` | Default interval between checks | 5 |
| `HistoryRetentionDays` | How long to keep historical data | 90 |
| `CronToken` | Secure token for remote access to check endpoint | null |

## Service Management Best Practices

### Planning Service Changes

1. **Service IDs**: Use consistent, descriptive IDs that won't change over time
2. **Gradual Changes**: Add new services before removing old ones during migrations
3. **Testing**: Test configuration changes in development before production
4. **Backup**: Keep backups of working configurations

### Configuration Updates

```json
// Example: Adding a new service
{
  "StatusConfiguration": {
    "Services": [
      {
        "Id": "existing-service",
        "Name": "Existing Service",
        // ... existing configuration
      },
      {
        "Id": "new-api-service",
        "Name": "New API Service",
        "Description": "Recently deployed API endpoint",
        "Type": "HTTP",
        "Url": "https://api.example.com/v2/health",
        "ExpectedStatusCode": 200,
        "TimeoutSeconds": 15,
        "CheckInterval": 5,
        "Enabled": true
      }
    ]
  }
}
```

```json
// Example: Temporarily disabling a service
{
  "Id": "maintenance-service",
  "Name": "Service Under Maintenance",
  "Enabled": false  // Service won't appear on status page
}
```

## Automated Monitoring

### Setting Up Secure Cron Jobs

The `/check` endpoint is secured and designed for automated monitoring systems.

#### Local Cron Job (Recommended - No Token Required)
```bash
# Edit crontab
crontab -e

# Add this line to run checks every 5 minutes from localhost
*/5 * * * * curl -s "http://localhost:5000/check?format=json" > /dev/null
```

#### Remote Cron Job (Requires Token)
```bash
# Edit crontab  
crontab -e

# Add this line with your secure token
*/5 * * * * curl -s "https://your-domain.com/check?token=YOUR_SECURE_TOKEN&format=json" > /dev/null
```

#### Windows Task Scheduler (Local)
Create a new task with this action:
```powershell
powershell -Command "Invoke-WebRequest -Uri 'http://localhost:5000/check?format=json' -UseBasicParsing"
```

#### Windows Task Scheduler (Remote)
```powershell
powershell -Command "Invoke-WebRequest -Uri 'https://your-domain.com/check?token=YOUR_SECURE_TOKEN&format=json' -UseBasicParsing"
```

#### Docker/Kubernetes CronJob
```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: vonet-status-check
spec:
  schedule: "*/5 * * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: checker
            image: curlimages/curl
            env:
            - name: CHECK_TOKEN
              valueFrom:
                secretKeyRef:
                  name: status-check-secret
                  key: token
            command: ["curl", "-s", "https://your-domain.com/check?token=$(CHECK_TOKEN)&format=json"]
          restartPolicy: OnFailure
```

#### Using systemd (Linux)

1. Create service file `/etc/systemd/system/vonet-status-check.service`:
```ini
[Unit]
Description=VONet Status Check
Wants=vonet-status-check.timer

[Service]
Type=oneshot
ExecStart=/usr/bin/curl -s "http://localhost:5000/check?format=json"
User=vonet-status
Group=vonet-status

[Install]
WantedBy=multi-user.target
```

2. Create timer file `/etc/systemd/system/vonet-status-check.timer`:
```ini
[Unit]
Description=VONet Status Check Timer
Requires=vonet-status-check.service

[Timer]
Unit=vonet-status-check.service
OnCalendar=*:0/5

[Install]
WantedBy=timers.target
```

3. Enable and start:
```bash
sudo systemctl enable vonet-status-check.timer
sudo systemctl start vonet-status-check.timer
```

## Database

The application uses SQLite for data storage with the following tables:

- **ServiceStatuses**: Current status of each service
- **StatusHistory**: Historical check results
- **Incidents**: Incident reports and tracking

The database file (`status_history.db`) is created automatically in the application directory.

### Data Preservation

When services are removed from configuration:
- Historical data is preserved in the database
- Services are marked as disabled rather than deleted
- Re-adding services restores full historical context

## Status History Visualization

The status page displays the last 30 days of service history as colored squares:

- ?? **Green**: Operational (90-100% uptime)
- ?? **Yellow**: Partial outage (50-89% uptime)  
- ?? **Red**: Major outage (0-49% uptime)
- ?? **Blue**: Under maintenance
- ? **Gray**: No data/unknown

Each square represents one day, and hovering shows detailed uptime information.

## Security Features

### Check Endpoint Protection

The `/check` endpoint includes multiple security layers:

1. **Access Control**: Only accessible locally or with valid token
2. **Stealth Mode**: Returns 404 for unauthorized access attempts
3. **Request Logging**: All access attempts logged with IP and User-Agent
4. **Rate Limiting**: Inherent protection through cron job design

### Security Best Practices

1. **Use Strong Tokens**: Generate cryptographically secure random tokens (32+ characters)
2. **Local Deployment**: Deploy monitoring checks locally when possible
3. **Network Security**: Use HTTPS for remote token transmission
4. **Token Rotation**: Regularly rotate tokens and update cron jobs
5. **Log Monitoring**: Monitor logs for unauthorized access attempts
6. **Firewall Rules**: Restrict access to the check endpoint at network level

### Security Monitoring

Monitor application logs for:
- Repeated 404 responses to `/check` (potential scanning)
- Failed token authentication attempts
- Unusual access patterns or user agents
- Excessive request frequencies

## API Endpoints

### Check Endpoint
- **URL**: `/check`
- **Method**: GET
- **Authentication**: Local access or token parameter
- **Parameters**: 
  - `token` (string, required for remote access): Authentication token
  - `format=json` (optional): Returns JSON response instead of HTML
- **Description**: Triggers service checks (secured endpoint)

#### JSON Response Format
```json
{
  "success": true,
  "message": "Service checks completed successfully in 2.34 seconds",
  "executionTimeMs": 2340,
  "timestamp": "2025-01-08T12:00:00Z"
}
```

#### Security Response Codes
- **200 OK**: Check completed successfully
- **404 Not Found**: Unauthorized access attempt (no token or invalid token)
- **500 Internal Server Error**: Error during check execution

## Development

### Prerequisites
- .NET 8.0 SDK
- SQLite (included with .NET)

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Running in Development
```bash
dotnet run --environment Development
```

### Local Development Access
During development, the check endpoint is accessible at:
- `http://localhost:5000/check` (no token required for localhost)
- `https://localhost:5001/check` (no token required for localhost)

## Deployment

### Security Considerations for Production

1. **Token Management**: Store tokens securely (environment variables, key vaults)
2. **HTTPS**: Always use HTTPS in production for token transmission
3. **Firewall**: Restrict `/check` endpoint access at network level
4. **Monitoring**: Implement log monitoring and alerting
5. **Updates**: Keep dependencies updated for security patches

### Docker
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY . .
EXPOSE 80
ENV StatusConfiguration__CronToken=""
ENTRYPOINT ["dotnet", "VONet-Stats.dll"]
```

### Environment Variables
```bash
# Set token via environment variable
export StatusConfiguration__CronToken="your-secure-token"

# Or in Docker
docker run -e StatusConfiguration__CronToken="your-secure-token" vonet-status
```

### IIS/Windows
1. Publish: `dotnet publish -c Release`
2. Copy published files to IIS directory
3. Configure application pool for .NET 8
4. Ensure SQLite database has proper permissions
5. Configure secure token in web.config or environment

### Linux/Nginx
1. Publish: `dotnet publish -c Release`
2. Configure reverse proxy in Nginx
3. Set up systemd service for the application
4. Configure SSL certificates
5. Set environment variables for token

## Troubleshooting

### Common Issues

1. **Services not appearing**: Check if services are in `appsettings.json` and `Enabled: true`
2. **Services not disappearing**: Run a manual check cycle after removing from configuration
3. **500 error on startup**: Usually indicates database initialization failure - check logs and permissions
4. **Database initialization failure**: 
   - Ensure write permissions to application directory
   - Check SQLite is available (included with .NET 8)
   - Verify disk space is available
   - Try running as administrator/with elevated permissions
5. **"System Initializing" message persists**: 
   - Wait 30-60 seconds for automatic initialization
   - Check application logs for errors
   - Try accessing `/check` endpoint locally to force initialization
6. **404 on /check endpoint**: Check token configuration and ensure proper authentication
7. **Database file permissions**: Ensure the application has write access to the directory
8. **Network timeouts**: Adjust `TimeoutSeconds` for slow services
9. **SSL certificate errors**: Add certificate validation bypass for development
10. **Memory usage**: Adjust `HistoryRetentionDays` to control database size
11. **Token not working**: Verify token matches exactly (no extra spaces or characters)

### Database Troubleshooting

**Database File Location**: The SQLite database file (`status_history.db`) is created in the application root directory by default.

**Common Database Issues**:

1. **Permission Denied**:
   ```bash
   # Linux/macOS: Ensure write permissions
   chmod 755 /path/to/app/directory
   
   # Windows: Run as Administrator or check folder permissions
   ```

2. **Database Locked**:
   - Stop the application
   - Delete `.db-wal` and `.db-shm` files
   - Restart the application

3. **Corrupted Database**:
   ```bash
   # Backup and recreate
   mv status_history.db status_history.db.backup
   # Restart application to recreate
   ```

4. **Custom Database Location**:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=/custom/path/status_history.db"
     }
   }
   ```

**Database Recovery**:
If the database becomes corrupted or inaccessible:
1. Stop the application
2. Backup the existing database file (if possible)
3. Delete the `status_history.db` file
4. Restart the application (database will be recreated)
5. Run a manual check to begin collecting new data

### Configuration Troubleshooting

1. **Service changes not reflected**:
   - Check JSON syntax in `appsettings.json`
   - Restart application to reload configuration
   - Run manual check to trigger service discovery

2. **Historical data missing**:
   - Check if service ID changed (creates new service record)
   - Verify database file permissions and accessibility

### Security Troubleshooting

1. **Check endpoint returns 404**: 
   - Verify token in request matches configuration
   - Check if accessing from localhost (no token required)
   - Review application logs for authentication failures

2. **Logs show unauthorized access attempts**:
   - Normal for internet-facing deployments
   - Consider additional firewall restrictions
   - Monitor for unusual patterns

### Logging

Check application logs for detailed error information. Logging is configured in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "VONet_Stats": "Debug"
    }
  }
}
```

Security-related events are logged at Warning and Error levels.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.