# VONet Status IIS Deployment Script
# Run this script as Administrator

param(
    [string]$SiteName = "VONet-Status",
    [string]$AppPoolName = "VONet-Status",
    [string]$SitePath = "C:\inetpub\wwwroot\VONet-Status",
    [string]$DataPath = "C:\VONet-Data",
    [int]$Port = 80
)

Write-Host "?? VONet Status IIS Deployment Script" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Function to check if running as administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    Write-Error "? This script must be run as Administrator"
    exit 1
}

Write-Host "? Running with Administrator privileges" -ForegroundColor Green

# Step 1: Create data directory with proper permissions
Write-Host "?? Creating secure data directory..." -ForegroundColor Yellow
try {
    if (-not (Test-Path $DataPath)) {
        New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
        Write-Host "   Created: $DataPath" -ForegroundColor Green
    } else {
        Write-Host "   Directory already exists: $DataPath" -ForegroundColor Green
    }
    
    # Set permissions on data directory
    icacls $DataPath /grant "IIS_IUSRS:(OI)(CI)M" /T | Out-Null
    icacls $DataPath /grant "IIS AppPool\$AppPoolName:(OI)(CI)M" /T | Out-Null
    Write-Host "   ? Data directory permissions set" -ForegroundColor Green
} catch {
    Write-Error "? Failed to create data directory: $_"
    exit 1
}

# Step 2: Set application permissions (read-only)
Write-Host "?? Setting application permissions..." -ForegroundColor Yellow
try {
    if (Test-Path $SitePath) {
        # Remove inherited permissions and set explicit ones
        icacls $SitePath /inheritance:r | Out-Null
        icacls $SitePath /grant "Administrators:(OI)(CI)F" | Out-Null
        icacls $SitePath /grant "SYSTEM:(OI)(CI)F" | Out-Null
        icacls $SitePath /grant "IIS_IUSRS:(OI)(CI)RX" | Out-Null
        icacls $SitePath /grant "IIS AppPool\$AppPoolName:(OI)(CI)RX" | Out-Null
        Write-Host "   ? Application folder permissions set (read-only)" -ForegroundColor Green
    } else {
        Write-Host "   ??  Application folder not found: $SitePath" -ForegroundColor Yellow
        Write-Host "   Please copy your published application files to this location" -ForegroundColor Yellow
    }
} catch {
    Write-Error "? Failed to set application permissions: $_"
}

# Step 3: Create or update Application Pool
Write-Host "??  Configuring Application Pool..." -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction Stop
    
    if (Get-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue) {
        Write-Host "   Application Pool '$AppPoolName' already exists, updating..." -ForegroundColor Yellow
        Remove-WebAppPool -Name $AppPoolName -Confirm:$false
    }
    
    New-WebAppPool -Name $AppPoolName -Force | Out-Null
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "processModel.loadUserProfile" -Value $true
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "recycling.periodicRestart.time" -Value "00:00:00"
    
    Write-Host "   ? Application Pool '$AppPoolName' configured" -ForegroundColor Green
} catch {
    Write-Error "? Failed to configure Application Pool: $_"
    exit 1
}

# Step 4: Create or update Website
Write-Host "?? Configuring Website..." -ForegroundColor Yellow
try {
    if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
        Write-Host "   Website '$SiteName' already exists, updating..." -ForegroundColor Yellow
        Remove-Website -Name $SiteName -Confirm:$false
    }
    
    New-Website -Name $SiteName -Port $Port -PhysicalPath $SitePath -ApplicationPool $AppPoolName | Out-Null
    Write-Host "   ? Website '$SiteName' configured on port $Port" -ForegroundColor Green
} catch {
    Write-Error "? Failed to configure Website: $_"
    exit 1
}

# Step 5: Create web.config if it doesn't exist
Write-Host "?? Checking web.config..." -ForegroundColor Yellow
$webConfigPath = Join-Path $SitePath "web.config"
if (-not (Test-Path $webConfigPath) -and (Test-Path $SitePath)) {
    $webConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" 
                  arguments=".\VONet-Stats.dll" 
                  stdoutLogEnabled="true" 
                  stdoutLogFile=".\logs\stdout" 
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
      
      <!-- Security Headers -->
      <httpProtocol>
        <customHeaders>
          <add name="X-Content-Type-Options" value="nosniff" />
          <add name="X-Frame-Options" value="DENY" />
          <add name="X-XSS-Protection" value="1; mode=block" />
        </customHeaders>
      </httpProtocol>
    </system.webServer>
  </location>
</configuration>
"@
    
    try {
        $webConfigContent | Out-File -FilePath $webConfigPath -Encoding UTF8
        Write-Host "   ? Created web.config" -ForegroundColor Green
    } catch {
        Write-Host "   ??  Could not create web.config: $_" -ForegroundColor Yellow
    }
} elseif (Test-Path $webConfigPath) {
    Write-Host "   ? web.config already exists" -ForegroundColor Green
} else {
    Write-Host "   ??  Application folder not found, skipping web.config creation" -ForegroundColor Yellow
}

# Step 6: Create logs directory
Write-Host "?? Creating logs directory..." -ForegroundColor Yellow
try {
    $logsPath = Join-Path $SitePath "logs"
    if (Test-Path $SitePath) {
        if (-not (Test-Path $logsPath)) {
            New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
        }
        icacls $logsPath /grant "IIS AppPool\$AppPoolName:(OI)(CI)M" | Out-Null
        Write-Host "   ? Logs directory configured" -ForegroundColor Green
    }
} catch {
    Write-Host "   ??  Could not configure logs directory: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "?? Deployment Complete!" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "?? Site Name: $SiteName" -ForegroundColor Cyan
Write-Host "?? URL: http://localhost:$Port" -ForegroundColor Cyan
Write-Host "?? Application Path: $SitePath" -ForegroundColor Cyan
Write-Host "???  Data Path: $DataPath" -ForegroundColor Cyan
Write-Host "??  App Pool: $AppPoolName" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Next Steps:" -ForegroundColor Yellow
Write-Host "1. Copy your published application files to: $SitePath" -ForegroundColor White
Write-Host "2. Update appsettings.json with your production configuration" -ForegroundColor White
Write-Host "3. Set a secure CronToken in configuration" -ForegroundColor White
Write-Host "4. Configure your services in appsettings.json" -ForegroundColor White
Write-Host "5. Set up Task Scheduler for automated checks" -ForegroundColor White
Write-Host ""
Write-Host "?? Troubleshooting:" -ForegroundColor Yellow
Write-Host "- Check logs in: $SitePath\logs\" -ForegroundColor White
Write-Host "- Check Windows Event Viewer for errors" -ForegroundColor White
Write-Host "- Verify .NET 8 Hosting Bundle is installed" -ForegroundColor White
Write-Host ""
Write-Host "? Your VONet Status site is ready!" -ForegroundColor Green