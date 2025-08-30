# VONet Status - Simple IIS Deployment

## Quick Deployment Steps

1. **Publish Application**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Copy Files**
   - Copy all files from `./publish/` to your existing IIS site folder
   - That's it! No configuration needed.

3. **Visit Your Site**
   - The application will automatically create the `App_Data` folder
   - If permissions are insufficient, you'll see clear error messages

## If You See Permission Errors

The application will display a red error message with this fix:

```powershell
icacls "C:\path\to\your\site\App_Data" /grant "IIS_IUSRS:(OI)(CI)M"
```

Replace `C:\path\to\your\site` with your actual site path.

## Prerequisites

- .NET 8 Hosting Bundle installed on server
- Existing IIS site with .NET 8 app pool (No Managed Code)

## That's It!

The application is completely self-contained and handles all setup automatically.