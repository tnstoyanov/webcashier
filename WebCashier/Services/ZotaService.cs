using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebCashier.Services;

public interface IZotaService
{
    Task<ZotaCreateDepositResult> CreateDepositAsync(decimal amount, string currency, HttpContext httpContext, CancellationToken ct = default);
}

public class ZotaService : IZotaService
{
    private readonly HttpClient _http;
    private readonly ILogger<ZotaService> _logger;
    private readonly IConfiguration _config;
    private readonly ICommLogService _comm;

    public ZotaService(HttpClient http, ILogger<ZotaService> logger, IConfiguration config, ICommLogService comm)
    {
        _http = http;
        _logger = logger;
        _config = config;
        _comm = comm;
    }

    public async Task<ZotaCreateDepositResult> CreateDepositAsync(decimal amount, string currency, HttpContext httpContext, CancellationToken ct = default)
    {
        var zota = _config.GetSection("Zota");
        var endpointId = zota["EndpointID"] ?? "";
        var secretKey = zota["MerchantSecretKey"] ?? "";
        var orderCurrency = string.IsNullOrWhiteSpace(currency) ? (zota["orderCurrency"] ?? "EUR") : currency;
        var redirectUrl = zota["RedirectUrl"] ?? "https://webcashier.onrender.com/Zota/Return";
        var callbackUrl = zota["CallbackUrl"] ?? "https://webcashier.onrender.com/Zota/Callback";
        var checkoutUrl = zota["CheckoutUrl"] ?? "https://webcashier.onrender.com/Payment";
        var customerEmail = zota["CustomerEmail"] ?? "demo@example.com";

        var merchantOrderID = $"R-{Random.Shared.NextInt64(3_000_000, 3_999_999)}";
        var orderAmount = amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        var xff = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var clientIp = (xff?.Split(',').FirstOrDefault()?.Trim()) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "134.201.250.130";

        var toSign = $"{endpointId}{merchantOrderID}{orderAmount}{customerEmail}{secretKey}";
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(toSign))).ToLowerInvariant();

        var body = new
        {
            merchantOrderID,
            merchantOrderDesc = "Sandbox test",
            orderAmount,
            orderCurrency,
            customerEmail,
            customerFirstName = "Tony",
            customerLastName = "Stoyanov",
            customerAddress = "11 Sun Street",
            customerCountryCode = "TH",
            customerCity = "Bangkok",
            customerState = "",
            customerZipCode = "10010",
            customerPhone = "+359888123456",
            customerBankCode = "",
            customerIP = clientIp,
            redirectUrl,
            callbackUrl,
            customParam = "{\"UserId\": \"9a145b02\"}",
            checkoutUrl,
            signature
        };

        var url = $"https://api.zotapay-sandbox.com/api/v1/deposit/request/{endpointId}";

    await _comm.LogAsync("zota-outbound", new { url, body }, "zota");

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(body);
        var res = await _http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

    await _comm.LogAsync("zota-response", new { status = (int)res.StatusCode, body = text }, "zota");

        string? depositUrl = null;
        string? orderId = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("depositUrl", out var du)) depositUrl = du.GetString();
            else if (root.TryGetProperty("data", out var data) && data.TryGetProperty("depositUrl", out var du2)) depositUrl = du2.GetString();
            if (root.TryGetProperty("orderID", out var oid)) orderId = oid.GetString();
            else if (root.TryGetProperty("data", out var data2) && data2.TryGetProperty("orderID", out var oid2)) orderId = oid2.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Zota response JSON");
        }

        return new ZotaCreateDepositResult
        {
            Success = res.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(depositUrl),
            DepositUrl = depositUrl ?? string.Empty,
            OrderId = orderId ?? string.Empty,
            MerchantOrderId = merchantOrderID
        };
    }
}

public class ZotaCreateDepositResult
{
    public bool Success { get; set; }
    public string DepositUrl { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string MerchantOrderId { get; set; } = string.Empty;
}
