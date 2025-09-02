using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebCashier.Models;
using WebCashier.Services;

namespace WebCashier.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRuntimeConfigStore _runtimeConfig;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IRuntimeConfigStore runtimeConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _runtimeConfig = runtimeConfig;
    }

    public IActionResult Index()
    {
        // Prepare a simple configuration model (dictionary-like) for Smilepayz keys
        var model = new Dictionary<string, string?>
        {
            { "Smilepayz:PartnerId", _runtimeConfig.Get("Smilepayz:PartnerId") ?? _configuration["Smilepayz:PartnerId"] },
            { "Smilepayz:MerchantSecret", _runtimeConfig.Get("Smilepayz:MerchantSecret") ?? _configuration["Smilepayz:MerchantSecret"] },
            { "Smilepayz:RSAPublicKey", _runtimeConfig.Get("Smilepayz:RSAPublicKey") ?? _configuration["Smilepayz:RSAPublicKey"] },
            { "Smilepayz:RSAPrivateKey", _runtimeConfig.Get("Smilepayz:RSAPrivateKey") ?? _configuration["Smilepayz:RSAPrivateKey"] },
            { "Smilepayz:Endpoint", _runtimeConfig.Get("Smilepayz:Endpoint") ?? _configuration["Smilepayz:Endpoint"] },
            { "Smilepayz:RedirectUrl", _runtimeConfig.Get("Smilepayz:RedirectUrl") ?? _configuration["Smilepayz:RedirectUrl"] },
            { "Smilepayz:CallbackUrl", _runtimeConfig.Get("Smilepayz:CallbackUrl") ?? _configuration["Smilepayz:CallbackUrl"] },
            { "Smilepayz:PaymentMethod", _runtimeConfig.Get("Smilepayz:PaymentMethod") ?? _configuration["Smilepayz:PaymentMethod"] },
            { "Smilepayz:Currency", _runtimeConfig.Get("Smilepayz:Currency") ?? _configuration["Smilepayz:Currency"] }
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveConfig([FromForm] Dictionary<string, string?> form)
    {
        // Only allow known keys for safety
        var allowedKeys = new[]
        {
            "Smilepayz:PartnerId",
            "Smilepayz:MerchantSecret",
            "Smilepayz:RSAPublicKey",
            "Smilepayz:RSAPrivateKey",
            "Smilepayz:Endpoint",
            "Smilepayz:RedirectUrl",
            "Smilepayz:CallbackUrl",
            "Smilepayz:PaymentMethod",
            "Smilepayz:Currency"
        };

        var toSet = new Dictionary<string, string?>();
        foreach (var key in allowedKeys)
        {
            if (form.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                toSet[key] = val;
            }
        }

        _runtimeConfig.SetRange(toSet);
        TempData["Saved"] = true;
        return RedirectToAction("Index");
    }
}
