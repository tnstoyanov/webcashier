var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Kestrel differently for development vs production
if (builder.Environment.IsDevelopment())
{
    // Development configuration with custom certificate
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenLocalhost(5182); // HTTP
        serverOptions.ListenLocalhost(7068, listenOptions =>
        {
            listenOptions.UseHttps("localhost.pfx", "testpassword"); // HTTPS with custom cert
        });
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
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
