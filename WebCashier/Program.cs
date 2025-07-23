using WebCashier.Models.Praxis;
using WebCashier.Services;
using System.Net;
using System.Security.Authentication;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Disable caching in development
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.ResponseCacheAttribute()
        {
            Duration = 0,
            Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None,
            NoStore = true
        });
    }
});

// Configure Praxis settings
builder.Services.Configure<PraxisConfig>(builder.Configuration.GetSection("Praxis"));
builder.Services.AddSingleton<PraxisConfig>(provider =>
{
    var config = new PraxisConfig();
    builder.Configuration.GetSection("Praxis").Bind(config);
    return config;
});

// Register HttpClient and PraxisService with logging
builder.Services.AddTransient<LoggingHandler>();
builder.Services.AddHttpClient<IPraxisService, PraxisService>(client =>
{
    // Configure HttpClient for HTTPS requests
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "WebCashier/1.0");
})
.AddHttpMessageHandler<LoggingHandler>()
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    
    // Force TLS 1.2 only for maximum compatibility
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
    handler.CheckCertificateRevocationList = false;
    handler.UseCookies = false;
    
    // In development, bypass SSL validation
    if (builder.Environment.IsDevelopment())
    {
        Console.WriteLine("Development mode: bypassing SSL certificate validation");
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    }
    
    return handler;
});

// Register HttpClient and LuxtakService
builder.Services.AddHttpClient<LuxtakService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "WebCashier/1.0");
})
.AddHttpMessageHandler<LoggingHandler>()
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
    handler.CheckCertificateRevocationList = false;
    handler.UseCookies = false;
    
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    }
    
    return handler;
});

// Register PaymentStateService as singleton to maintain state across requests
builder.Services.AddSingleton<IPaymentStateService, PaymentStateService>();

// Configure Kestrel differently for development vs production
if (builder.Environment.IsDevelopment())
{
    // Development configuration - HTTP only for simplicity
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenLocalhost(5182); // HTTP only
    });
}
else
{
    // Production configuration - bind to PORT environment variable (required for Render.com)
    var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(int.Parse(port));
    });
}

var app = builder.Build();

// Add startup logging for debugging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("WebCashier starting up...");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

if (app.Environment.IsProduction())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
    logger.LogInformation("Production mode - binding to port: {Port}", port);
}
else
{
    logger.LogInformation("Development mode - binding to localhost:5182");
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Configure static files with cache headers for development
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
    });
}
else
{
    app.UseStaticFiles();
}

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
