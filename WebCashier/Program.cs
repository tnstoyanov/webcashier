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
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<WebCashier.Services.ZotaService>();
builder.Services.AddScoped<WebCashier.Services.IZotaService, WebCashier.Services.ZotaService>();
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

// SwiftGoldPay service with mTLS and optional pinning
builder.Services.AddHttpClient<ISwiftGoldPayService, SwiftGoldPayService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(40);
    client.DefaultRequestHeaders.Add("User-Agent", "WebCashier/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
    handler.CheckCertificateRevocationList = false;
    handler.UseCookies = false;

    try
    {
        var searchDirs = new List<string?>
        {
            Environment.GetEnvironmentVariable("CERT_DIR"),
            Path.Combine(builder.Environment.ContentRootPath, "cert"),
            Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "cert")),
            "/cert"
        };
        string? foundDir = null;

        // Prefer PFX if provided (more portable)
        string? pfxPath = Environment.GetEnvironmentVariable("CERT_PFX_PATH");
        if (string.IsNullOrWhiteSpace(pfxPath))
        {
            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var pfxTry = Path.Combine(dir, "client.pfx");
                if (File.Exists(pfxTry)) { pfxPath = pfxTry; foundDir = dir; break; }
            }
        }
        if (!string.IsNullOrWhiteSpace(pfxPath) && File.Exists(pfxPath))
        {
            try
            {
                var pwd = Environment.GetEnvironmentVariable("CERT_PFX_PASSWORD");
                #pragma warning disable SYSLIB0057
                X509Certificate2 cert = string.IsNullOrEmpty(pwd)
                    ? new X509Certificate2(pfxPath)
                    : new X509Certificate2(pfxPath, pwd, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
                #pragma warning restore SYSLIB0057
                handler.ClientCertificates.Add(cert);
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                Console.WriteLine($"[SwiftGoldPay] Loaded client PFX certificate from: {pfxPath}");
                Console.WriteLine($"[SwiftGoldPay] Client cert subject: {cert.Subject}");
                Console.WriteLine($"[SwiftGoldPay] Client cert valid until (UTC): {cert.NotAfter.ToUniversalTime():u}");
                foundDir ??= Path.GetDirectoryName(pfxPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SwiftGoldPay] Failed to load PFX: {ex.Message}. Falling back to PEM if available.");
            }
        }

        // Fallback: PEM + private key
        if (handler.ClientCertificates.Count == 0)
        {
            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var pemPathTry = Path.Combine(dir, "certificate.pem");
                var keyPathTry = Path.Combine(dir, "private.key");
                if (File.Exists(pemPathTry) && File.Exists(keyPathTry))
                {
                    // Load from PEM files and re-wrap as PKCS#12 for compatibility
                    var cert = X509Certificate2.CreateFromPemFile(pemPathTry, keyPathTry);
                    var pfxBytes = cert.Export(X509ContentType.Pkcs12);
                    #pragma warning disable SYSLIB0057
                    cert = new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
                    #pragma warning restore SYSLIB0057
                    handler.ClientCertificates.Add(cert);
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                    Console.WriteLine($"[SwiftGoldPay] Loaded client certificate from: {pemPathTry}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert subject: {cert.Subject}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert valid until (UTC): {cert.NotAfter.ToUniversalTime():u}");
                    foundDir = dir;
                    break;
                }
            }
        }
        if (foundDir == null)
        {
            Console.WriteLine("[SwiftGoldPay] Client certificate not found. Place certificate.pem and private.key under /cert or set CERT_DIR.");
        }

        if (!builder.Environment.IsDevelopment())
        {
            var insecure = string.Equals(Environment.GetEnvironmentVariable("SGP_INSECURE_SKIP_VERIFY"), "true", StringComparison.OrdinalIgnoreCase);
            // Optional server cert pinning to work around UntrustedRoot in sandbox
            string? pinnedPath = null;
            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var p = Path.Combine(dir, "server.crt");
                if (File.Exists(p)) { pinnedPath = p; break; }
            }
            X509Certificate2? pinned = null;
            if (!string.IsNullOrEmpty(pinnedPath))
            {
                try
                {
                    // Try DER/CER first, then PEM text content
                    try
                    {
                        #pragma warning disable SYSLIB0057
                        pinned = new X509Certificate2(pinnedPath);
                        #pragma warning restore SYSLIB0057
                    }
                    catch
                    {
                        try
                        {
                            var pemText = File.ReadAllText(pinnedPath);
                            pinned = X509Certificate2.CreateFromPem(pemText);
                        }
                        catch
                        {
                            pinned = null;
                        }
                    }
                    Console.WriteLine($"[SwiftGoldPay] Loaded pinned server certificate from: {pinnedPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SwiftGoldPay] Failed to load pinned server certificate: {ex.Message}");
                }
            }

            handler.ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
            {
                try
                {
                    var host = req?.RequestUri?.Host ?? string.Empty;
                    if (host.Equals("sandbox-partner.swiftgoldpay.com", StringComparison.OrdinalIgnoreCase))
                    {
                        if (errors == SslPolicyErrors.None)
                        {
                            return true;
                        }
                        if (insecure)
                        {
                            Console.WriteLine("[SwiftGoldPay] Insecure: bypassing server certificate validation for sandbox-partner.swiftgoldpay.com due to SGP_INSECURE_SKIP_VERIFY=true");
                            return true;
                        }
                        if (pinned != null && cert != null)
                        {
                            var presented = cert.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256).Replace(":", string.Empty, StringComparison.OrdinalIgnoreCase);
                            var expected = pinned.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256).Replace(":", string.Empty, StringComparison.OrdinalIgnoreCase);
                            if (string.Equals(presented, expected, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("[SwiftGoldPay] Accepted server certificate via pinning (SHA256 thumbprint match).");
                                return true;
                            }
                        }
                        Console.WriteLine($"[SwiftGoldPay] Server certificate validation failed for {host}: {errors}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SwiftGoldPay] Server cert validation exception: {ex.Message}");
                }
                return errors == SslPolicyErrors.None;
            };
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("[SwiftGoldPay] Error configuring HttpClient handler: " + ex.Message);
    }

    if (builder.Environment.IsDevelopment())
    {
        // Trust any server cert in dev only; does not affect presenting our client cert
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    }

    return handler;
});

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
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
    // Avoid enforcing HTTPS redirection unless proxy headers are enabled; otherwise could 503/loop on Render.
    var enableFwdHeaders = Environment.GetEnvironmentVariable("ENABLE_FWD_HEADERS");
    if (string.Equals(enableFwdHeaders, "true", StringComparison.OrdinalIgnoreCase))
    {
        app.UseHttpsRedirection();
    }
    else
    {
        var l = app.Services.GetRequiredService<ILogger<Program>>();
        l.LogWarning("Skipping UseHttpsRedirection because ENABLE_FWD_HEADERS!=true (set it to true when behind HTTPS proxy)");
    }
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

// SwiftGoldPay client certificate diagnostics (no secrets). Returns cert metadata if found.
app.MapGet("/diag/sgp-cert", () =>
{
    try
    {
        var searchDirs = new List<string?>
        {
            Environment.GetEnvironmentVariable("CERT_DIR"),
            Path.Combine(app.Environment.ContentRootPath, "cert"),
            Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "cert")),
            "/cert"
        };
        string? dirFound = null;
        string? pemPath = null;
        string? keyPath = null;
        foreach (var dir in searchDirs)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var pemTry = Path.Combine(dir, "certificate.pem");
            var keyTry = Path.Combine(dir, "private.key");
            if (File.Exists(pemTry) && File.Exists(keyTry))
            {
                dirFound = dir;
                pemPath = pemTry;
                keyPath = keyTry;
                break;
            }
        }
        if (pemPath == null || keyPath == null)
        {
            return Results.Ok(new { found = false, message = "certificate.pem/private.key not found in search paths", searchDirs });
        }
    var cert = X509Certificate2.CreateFromPemFile(pemPath, keyPath);
    var pfx = cert.Export(X509ContentType.Pkcs12);
    #pragma warning disable SYSLIB0057
    cert = new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
    #pragma warning restore SYSLIB0057
        var info = new
        {
            found = true,
            dir = dirFound,
            subject = cert.Subject,
            issuer = cert.Issuer,
            notBefore = cert.NotBefore,
            notAfter = cert.NotAfter,
            serialNumber = cert.SerialNumber,
            thumbprintSHA1 = cert.Thumbprint,
            thumbprintSHA256 = cert.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256),
            hasPrivateKey = cert.HasPrivateKey
        };
        return Results.Ok(info);
    }
    catch (Exception ex)
    {
        return Results.Ok(new { found = false, error = ex.Message });
    }
});

app.Run();
