using WebCashier.Models.Praxis;
using WebCashier.Services;
using System.Net;
using System.Security.Authentication;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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
    // Production configuration - let Azure handle ports and HTTPS
    // Azure App Service will automatically configure ports and SSL
}

var app = builder.Build();

// Test SSL connectivity on startup
Console.WriteLine("Testing SSL connection to Praxis API...");
await WebCashier.SSLTest.TestSSLConnection();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
