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
builder.Services.AddAntiforgery();
// Persistent runtime config (JSON file). For Render ephemeral FS, consider mounting a persistent disk.
builder.Services.AddSingleton<IRuntimeConfigStore>(_ =>
{
    Console.WriteLine("[Startup] Initializing RuntimeConfigStore...");
    if (string.Equals(Environment.GetEnvironmentVariable("RUNTIME_CONFIG_DISABLE"), "true", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("[Startup] Runtime config persistence DISABLED via RUNTIME_CONFIG_DISABLE");
        return new RuntimeConfigStore(); // Will skip load
    }
    try
    {
        var envPath = Environment.GetEnvironmentVariable("RUNTIME_CONFIG_PATH");
        string file;
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var dir = Path.GetDirectoryName(envPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            file = envPath;
        }
        else
        {
            var baseDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? AppContext.BaseDirectory;
            var path = Path.Combine(baseDir, "data");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            file = Path.Combine(path, "runtime-config.json");
        }
        Console.WriteLine($"[Startup] Runtime config file path: {file}");
        return new RuntimeConfigStore(file);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Fallback to in-memory runtime config due to exception: {ex.Message}");
        return new RuntimeConfigStore();
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
builder.Services.AddSingleton<ICommLogService, CommLogService>();
builder.Services.AddHttpClient("comm-logs")
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
        if (builder.Environment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
        }
        return handler;
    });

// Register HttpClient and SmilepayzService
builder.Services.AddHttpClient<ISmilepayzService, SmilepayzService>(client =>
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

// Nuvei service (no outbound HTTP needed yet; form generation only)
builder.Services.AddSingleton<INuveiService, NuveiService>();
builder.Services.AddHttpContextAccessor();

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

Console.WriteLine("[Startup] Building application host...");
var app = builder.Build();
Console.WriteLine("[Startup] Host built.");

// Add startup logging for debugging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("WebCashier starting up...");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
var runtimeStore = app.Services.GetRequiredService<IRuntimeConfigStore>() as RuntimeConfigStore;
if (runtimeStore != null)
{
    logger.LogInformation("Runtime config persistence file: {File}", runtimeStore.PersistPath);
}

var enableFwd = Environment.GetEnvironmentVariable("ENABLE_FWD_HEADERS");
if (string.Equals(enableFwd, "true", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost
        });
        logger.LogInformation("Forwarded headers middleware enabled");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed enabling forwarded headers middleware");
    }
}
else
{
    logger.LogInformation("Forwarded headers middleware disabled (set ENABLE_FWD_HEADERS=true to enable)");
}

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

// Simple health endpoint
app.MapGet("/healthz", () => Results.Ok(new {
    status = "ok",
    time = DateTime.UtcNow,
    env = app.Environment.EnvironmentName
}));

// Basic diagnostics (do NOT expose secrets in production; filter keys)
app.MapGet("/diag/env", () =>
{
    var whitelist = new[] { "ASPNETCORE_ENVIRONMENT", "RUNTIME_CONFIG_PATH", "RUNTIME_CONFIG_DISABLE", "ENABLE_FWD_HEADERS", "REQUIRE_HTTPS_URLS", "PORT", "DATA_DIR" };
    var dict = new Dictionary<string,string?>();
    foreach (var k in whitelist)
    {
        dict[k] = Environment.GetEnvironmentVariable(k);
    }
    return Results.Ok(new { vars = dict, time = DateTime.UtcNow });
});

app.Run();
