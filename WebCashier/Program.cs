using WebCashier.Models.Praxis;
using WebCashier.Services;

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
builder.Services.AddHttpClient<IPraxisService, PraxisService>()
    .AddHttpMessageHandler<LoggingHandler>();

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
