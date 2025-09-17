using Microsoft.AspNetCore.Mvc;
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
        string body = string.Empty;
        if (!Request.HasFormContentType)
        {
            using var reader = new StreamReader(Request.Body);
            body = await reader.ReadToEndAsync();
        }
    await _comm.LogAsync("zota-callback", new { form, body }, "zota");
        return Ok();
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
