using Microsoft.EntityFrameworkCore;
using VONet_Stats.Configuration;
using VONet_Stats.Data;
using VONet_Stats.Services;
using VONet_Stats.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Add configuration
builder.Services.Configure<StatusConfiguration>(
    builder.Configuration.GetSection("StatusConfiguration"));

// Add database
builder.Services.AddDbContext<StatusDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add HTTP client for service checking
builder.Services.AddHttpClient<IServiceChecker, ServiceChecker>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("User-Agent", "VONet-Status-Checker/1.0");
});

// Add custom services
builder.Services.AddScoped<IStatusService, StatusService>();

var app = builder.Build();

// Initialize database with proper error handling
await app.InitializeDatabaseAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
