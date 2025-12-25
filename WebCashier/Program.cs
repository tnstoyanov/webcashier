using WebCashier.Models.Praxis;
using WebCashier.Services;
using System.Net;
using System.Security.Authentication;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using System.Text;

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

// Configure antiforgery for Render.com (reverse proxy environment)
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    // Use SameAsRequest for production since Render.com forwards HTTP internally
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax; // More permissive for payment redirects
    options.SuppressXFrameOptionsHeader = false;
});

// Consolidated DataProtection configuration
try
{
    // Prefer explicit key ring directory if provided
    var keysRoot = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_DIRECTORY");
    if (string.IsNullOrWhiteSpace(keysRoot))
    {
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/app";
        keysRoot = Path.Combine(dataDir, "data-protection-keys");
    }
    var keysPath = keysRoot;
    if (!Directory.Exists(keysPath)) Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("WebCashier")
        .SetDefaultKeyLifetime(TimeSpan.FromDays(30)); // Longer lifetime to reduce key rotation issues
    Console.WriteLine($"[Startup] DataProtection keys path: {keysPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Failed to configure DataProtection persistence: {ex.Message}");
}

// Remove the old data protection configuration below
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

// Helper: attach client certificate chain (intermediates) if present
static void TryAttachClientChain(HttpClientHandler handler, X509Certificate2 primary, string? foundDir, List<string?> searchDirs, string? overridePath, string? overridePem)
{
    try
    {
        IEnumerable<string> LoadPemBlocks(string pem)
        {
            foreach (var block in ExtractPemCertificates(pem))
            {
                yield return block;
            }
        }

        bool AttachBlocks(IEnumerable<string> blocks, string source)
        {
            var addedLocal = 0;
            foreach (var block in blocks)
            {
                try
                {
                    var x = X509Certificate2.CreateFromPem(block);
                    if (!string.Equals(x.Thumbprint, primary.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        handler.ClientCertificates.Add(x);
                        addedLocal++;
                    }
                }
                catch { /* ignore individual block errors */ }
            }
            if (addedLocal > 0)
            {
                Console.WriteLine($"[SwiftGoldPay] Attached {addedLocal} intermediate certificate(s) from: {source}");
                return true;
            }
            return false;
        }

        if (!string.IsNullOrWhiteSpace(overridePem) && AttachBlocks(LoadPemBlocks(overridePem), "SGP_CLIENT_CHAIN_PEM/BASE64"))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            var pemText = File.ReadAllText(overridePath);
            if (AttachBlocks(LoadPemBlocks(pemText), overridePath)) return;
        }

        var candidates = new[] { "client-chain.pem", "client_chain.pem", "chain.pem", "ca-chain.pem", "ca_bundle.pem", "ca-bundle.crt", "client_chain.crt" };
        if (!string.IsNullOrWhiteSpace(foundDir))
        {
            foreach (var name in candidates)
            {
                var path = Path.Combine(foundDir, name);
                if (File.Exists(path))
                {
                    var pemText = File.ReadAllText(path);
                    if (AttachBlocks(LoadPemBlocks(pemText), path)) return;
                }
            }
        }

        foreach (var dir in searchDirs)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            foreach (var name in candidates)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    var pemText = File.ReadAllText(path);
                    if (AttachBlocks(LoadPemBlocks(pemText), path)) return;
                }
            }
        }

        Console.WriteLine("[SwiftGoldPay] No client certificate chain file found (checked env vars and common filenames). Skipping chain attachment.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SwiftGoldPay] Failed attaching client chain: {ex.Message}");
    }
}

// Helper: extract all certificate PEM blocks from concatenated text
static IEnumerable<string> ExtractPemCertificates(string pemText)
{
    const string begin = "-----BEGIN CERTIFICATE-----";
    const string end = "-----END CERTIFICATE-----";
    var list = new List<string>();
    int start = 0;
    while (true)
    {
        var i = pemText.IndexOf(begin, start, StringComparison.Ordinal);
        if (i < 0) break;
        var j = pemText.IndexOf(end, i, StringComparison.Ordinal);
        if (j < 0) break;
        j += end.Length;
        var block = pemText.Substring(i, j - i);
        list.Add(block);
        start = j;
    }
    return list;
}

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

// Register PayPal service
builder.Services.AddHttpClient<IPayPalService, PayPalService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "WebCashier/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
    handler.CheckCertificateRevocationList = false;
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

// Paysolutions service (PromptPay QR)
builder.Services.Configure<WebCashier.Models.Paysolutions.PaysolutionsConfig>(builder.Configuration.GetSection("Paysolutions"));
builder.Services.AddHttpClient<WebCashier.Services.IPaysolutionsService, WebCashier.Services.PaysolutionsService>(client =>
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
            Path.Combine(builder.Environment.ContentRootPath, "cert"), // repo-root/cert (Render native runtime)
            Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "cert")),
            Path.Combine(AppContext.BaseDirectory ?? string.Empty, "cert"), // publish/cert (where the dll lives)
            "/cert"
        };
        string? foundDir = null;
        var chainOverridePath = Environment.GetEnvironmentVariable("SGP_CLIENT_CHAIN_PATH");
        string? chainOverridePem = null;
        var chainPem = Environment.GetEnvironmentVariable("SGP_CLIENT_CHAIN_PEM");
        if (!string.IsNullOrWhiteSpace(chainPem))
        {
            chainOverridePem = chainPem;
            Console.WriteLine("[SwiftGoldPay] Using client chain from SGP_CLIENT_CHAIN_PEM env var");
        }
        else
        {
            var chainB64 = Environment.GetEnvironmentVariable("SGP_CLIENT_CHAIN_BASE64");
            if (!string.IsNullOrWhiteSpace(chainB64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(chainB64);
                    chainOverridePem = Encoding.UTF8.GetString(bytes);
                    Console.WriteLine("[SwiftGoldPay] Loaded client chain from SGP_CLIENT_CHAIN_BASE64 env var");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SwiftGoldPay] Failed decoding SGP_CLIENT_CHAIN_BASE64: {ex.Message}");
                }
            }
        }

        // Option A: Client certificate from environment (PEM or Base64 PFX)
        try
        {
            var envCertPem = Environment.GetEnvironmentVariable("SGP_CLIENT_CERT_PEM");
            var envKeyPem  = Environment.GetEnvironmentVariable("SGP_CLIENT_KEY_PEM");
            if (handler.ClientCertificates.Count == 0 &&
                !string.IsNullOrWhiteSpace(envCertPem) && !string.IsNullOrWhiteSpace(envKeyPem))
            {
                var cert = X509Certificate2.CreateFromPem(envCertPem, envKeyPem);
                var pfxBytesEnv = cert.Export(X509ContentType.Pkcs12);
                #pragma warning disable SYSLIB0057
                cert = new X509Certificate2(pfxBytesEnv, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
                #pragma warning restore SYSLIB0057
                handler.ClientCertificates.Add(cert);
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                Console.WriteLine("[SwiftGoldPay] Loaded client certificate from env (SGP_CLIENT_CERT_PEM/SGP_CLIENT_KEY_PEM)");
                Console.WriteLine($"[SwiftGoldPay] Client cert subject: {cert.Subject}");
                Console.WriteLine($"[SwiftGoldPay] Client cert valid until (UTC): {cert.NotAfter.ToUniversalTime():u}");
            }

            var pfxB64 = Environment.GetEnvironmentVariable("SGP_CLIENT_PFX_BASE64");
            if (handler.ClientCertificates.Count == 0 && !string.IsNullOrWhiteSpace(pfxB64))
            {
                var pfxBytes = Convert.FromBase64String(pfxB64);
                var pwd = Environment.GetEnvironmentVariable("SGP_CLIENT_PFX_PASSWORD");
                #pragma warning disable SYSLIB0057
                var cert = string.IsNullOrEmpty(pwd)
                    ? new X509Certificate2(pfxBytes)
                    : new X509Certificate2(pfxBytes, pwd, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
                #pragma warning restore SYSLIB0057
                handler.ClientCertificates.Add(cert);
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                Console.WriteLine("[SwiftGoldPay] Loaded client PFX from env (SGP_CLIENT_PFX_BASE64)");
                Console.WriteLine($"[SwiftGoldPay] Client cert subject: {cert.Subject}");
                Console.WriteLine($"[SwiftGoldPay] Client cert valid until (UTC): {cert.NotAfter.ToUniversalTime():u}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftGoldPay] Failed loading client cert from environment: {ex.Message}");
        }

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
        if (!string.IsNullOrWhiteSpace(pfxPath) && File.Exists(pfxPath) && handler.ClientCertificates.Count == 0)
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

                // Optionally attach intermediate chain if present alongside PFX
                TryAttachClientChain(handler, cert, foundDir, searchDirs, chainOverridePath, chainOverridePem);
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
                    // Load from PEM files - use different approach based on platform
                    X509Certificate2 cert;
                    if (OperatingSystem.IsLinux())
                    {
                        // On Linux, we need to use PFX for proper key association
                        var tempCert = X509Certificate2.CreateFromPemFile(pemPathTry, keyPathTry);
                        var pfxBytes = tempCert.Export(X509ContentType.Pkcs12);
                        #pragma warning disable SYSLIB0057
                        cert = new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.Exportable);
                        #pragma warning restore SYSLIB0057
                    }
                    else
                    {
                        // On macOS/Windows, direct PEM loading works fine
                        cert = X509Certificate2.CreateFromPemFile(pemPathTry, keyPathTry);
                    }
                    handler.ClientCertificates.Add(cert);
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                    Console.WriteLine($"[SwiftGoldPay] Loaded client certificate from: {pemPathTry}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert subject: {cert.Subject}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert issuer: {cert.Issuer}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert serial: {cert.SerialNumber}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert thumbprint: {cert.Thumbprint}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert valid until (UTC): {cert.NotAfter.ToUniversalTime():u}");
                    Console.WriteLine($"[SwiftGoldPay] Client cert has private key: {cert.HasPrivateKey}");
                    foundDir = dir;

                    // Optionally attach intermediate chain if present
                    TryAttachClientChain(handler, cert, foundDir, searchDirs, chainOverridePath, chainOverridePem);
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
            // First, allow pinned server cert via environment variable (PEM)
            X509Certificate2? pinned = null;
            var serverPem = Environment.GetEnvironmentVariable("SGP_SERVER_CERT_PEM");
            if (!string.IsNullOrWhiteSpace(serverPem))
            {
                try
                {
                    pinned = X509Certificate2.CreateFromPem(serverPem);
                    Console.WriteLine("[SwiftGoldPay] Loaded pinned server certificate from env SGP_SERVER_CERT_PEM");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SwiftGoldPay] Failed to load pinned server certificate from env: {ex.Message}");
                }
            }

            string? pinnedPath = null;
            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var p = Path.Combine(dir, "server.crt");
                if (File.Exists(p)) { pinnedPath = p; break; }
            }
            if (!string.IsNullOrEmpty(pinnedPath))
            {
                try
                {
                    // Try DER/CER first, then PEM text content
                    try
                    {
                        #pragma warning disable SYSLIB0057
                        pinned = pinned ?? new X509Certificate2(pinnedPath);
                        #pragma warning restore SYSLIB0057
                    }
                    catch
                    {
                        try
                        {
                            var pemText = File.ReadAllText(pinnedPath);
                            pinned = pinned ?? X509Certificate2.CreateFromPem(pemText);
                        }
                        catch
                        {
                            pinned = pinned ?? null;
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
                    if (host.Equals("sandbox-partner.swiftgoldpay.com", StringComparison.OrdinalIgnoreCase) ||
                        host.Equals("api-partner.swiftgoldpay.com", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[SwiftGoldPay] Server cert validation for {host}:");
                        Console.WriteLine($"[SwiftGoldPay] - SSL errors: {errors}");
                        Console.WriteLine($"[SwiftGoldPay] - Cert subject: {cert?.Subject ?? "null"}");
                        
                        if (errors == SslPolicyErrors.None)
                        {
                            Console.WriteLine("[SwiftGoldPay] Certificate validation passed - no errors");
                            return true;
                        }
                        if (insecure)
                        {
                            Console.WriteLine($"[SwiftGoldPay] Insecure: bypassing server certificate validation for {host} due to SGP_INSECURE_SKIP_VERIFY=true");
                            return true;
                        }
                        if (pinned != null && cert != null)
                        {
                            var presented = cert.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256).Replace(":", string.Empty, StringComparison.OrdinalIgnoreCase);
                            var expected = pinned.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256).Replace(":", string.Empty, StringComparison.OrdinalIgnoreCase);
                            Console.WriteLine($"[SwiftGoldPay] - Presented cert SHA256: {presented}");
                            Console.WriteLine($"[SwiftGoldPay] - Expected cert SHA256: {expected}");
                            if (string.Equals(presented, expected, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("[SwiftGoldPay] Accepted server certificate via pinning (SHA256 thumbprint match).");
                                return true;
                            }
                            else
                            {
                                Console.WriteLine("[SwiftGoldPay] Certificate mismatch - SHA256 thumbprints don't match");
                            }
                        }
                        // TEMPORARY: Allow production connections despite certificate issues for debugging
                        if (host.Equals("api-partner.swiftgoldpay.com", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[SwiftGoldPay] TEMPORARILY allowing production server certificate errors for {host} to debug API response");
                            return true;
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

    // Observe outbound public IP (useful for provider IP whitelist checks)
    app.MapGet("/diag/outbound-ip", async (IHttpClientFactory factory) =>
    {
        try
        {
            var client = factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var ip = await client.GetStringAsync("https://api.ipify.org");
            return Results.Ok(new { ip = ip.Trim(), time = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return Results.Problem(title: "Failed to fetch outbound IP", detail: ex.Message);
        }
    });

    // (moved helper functions earlier)

app.Run();
