using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using WebCashier.Services;

namespace WebCashier.Controllers;

[Route("Zota")]
public class ZotaController : Controller
{
    private readonly IZotaService _zota;
    private readonly ICommLogService _comm;
    private readonly ILogger<ZotaController> _logger;

    public ZotaController(IZotaService zota, ICommLogService comm, ILogger<ZotaController> logger)
    {
        _zota = zota;
        _comm = comm;
        _logger = logger;
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] ZotaCreateRequest req, CancellationToken ct)
    {
    await _comm.LogAsync("zota-inbound", new { path = "/Zota/Create", req }, "zota");
        if (req is null || req.Amount <= 0 || string.IsNullOrWhiteSpace(req.Currency))
            return BadRequest(new { success = false, message = "Invalid amount/currency" });

        var result = await _zota.CreateDepositAsync(req.Amount, req.Currency!, HttpContext, ct);
    await _comm.LogAsync("zota-outbound-response", new { result }, "zota");

        return Json(new { success = result.Success, depositUrl = result.DepositUrl, orderId = result.OrderId, merchantOrderId = result.MerchantOrderId });
    }

    [HttpGet("Return")]
    public IActionResult Return()
    {
        var q = Request.Query;
        var vm = new ZotaReturnViewModel
        {
            ErrorMessage = q["errorMessage"].FirstOrDefault() ?? string.Empty,
            MerchantOrderID = q["merchantOrderID"].FirstOrDefault() ?? string.Empty,
            OrderID = q["orderID"].FirstOrDefault() ?? string.Empty,
            Status = q["status"].FirstOrDefault() ?? string.Empty
        };
        return View(vm);
    }

    [HttpPost("Callback")]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    public async Task<IActionResult> Callback()
    {
        var form = Request.HasFormContentType ? Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString()) : new Dictionary<string, string>();
        object? parsedBody = null;
        string? rawBody = null;
        string contentType = Request.ContentType ?? string.Empty;

        if (!Request.HasFormContentType)
        {
            using var reader = new StreamReader(Request.Body);
            rawBody = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                // Try parse as-is first
                if (!TryParseJson(rawBody, out parsedBody))
                {
                    // Some providers send bodies with \u0022 instead of quotes. Normalize and try again.
                    var normalized = UnescapeUnicodeEscapes(rawBody);
                    if (TryParseJson(normalized, out var reparsed))
                    {
                        parsedBody = reparsed;
                        rawBody = normalized; // keep normalized copy for fallback logging
                    }
                }
            }
        }

        var logData = new
        {
            contentType,
            form,
            body = parsedBody ?? (object?)rawBody ?? string.Empty
        };

        await _comm.LogAsync("zota-callback", logData, "zota");
        return Ok();
    }

    private static bool TryParseJson(string text, out object? result)
    {
        try
        {
            result = System.Text.Json.JsonSerializer.Deserialize<object>(text);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    private static string UnescapeUnicodeEscapes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return Regex.Replace(input, @"\\u(?<Value>[0-9a-fA-F]{4})", m =>
        {
            var code = Convert.ToInt32(m.Groups["Value"].Value, 16);
            return char.ConvertFromUtf32(code);
        });
    }
}

public class ZotaCreateRequest
{
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
}

public class ZotaReturnViewModel
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string MerchantOrderID { get; set; } = string.Empty;
    public string OrderID { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
