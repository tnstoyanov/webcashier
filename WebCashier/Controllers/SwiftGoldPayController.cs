using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers;

[Route("SwiftGoldPay")]
public class SwiftGoldPayController : Controller
{
    private readonly ISwiftGoldPayService _svc;
    private readonly IConfiguration _cfg;
    private readonly ICommLogService _log;

    public SwiftGoldPayController(ISwiftGoldPayService svc, IConfiguration cfg, ICommLogService log)
    {
        _svc = svc;
        _cfg = cfg;
        _log = log;
    }

    [HttpGet("Init")]
    public async Task<IActionResult> Init()
    {
        var tokenRes = await _svc.GetTokenAsync(HttpContext.RequestAborted);
        if (!tokenRes.ok || string.IsNullOrEmpty(tokenRes.token))
        {
            return Json(new { success = false, error = $"Failed to get the bearer token! {(tokenRes.error ?? "").Trim()}" });
        }

        var country = _cfg["SwiftGoldPay:DefaultCountry"] ?? "THAI";
        var banksRes = await _svc.GetBanksAsync(country, tokenRes.token!, HttpContext.RequestAborted);
        if (!banksRes.ok || banksRes.banks == null)
        {
            return Json(new { success = false, error = $"Failed to get the list of banks! {(banksRes.error ?? "").Trim()}" });
        }

        return Json(new { success = true, token = tokenRes.token, banks = banksRes.banks });
    }

    public record CreateCustomerRequest(string token, string bankCode, string bankAccountNumber);

    [HttpPost("CreateCustomer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.token) || string.IsNullOrWhiteSpace(req.bankCode) || string.IsNullOrWhiteSpace(req.bankAccountNumber))
        {
            return BadRequest(new { success = false, error = "Missing required fields" });
        }
        var currency = _cfg["SwiftGoldPay:DefaultCurrency"] ?? "THB";
        var nameEn = _cfg["SwiftGoldPay:NameEn"] ?? "John Doe";
        var nameTh = _cfg["SwiftGoldPay:NameTh"] ?? "";
        var email = _cfg["SwiftGoldPay:Email"] ?? "";
        var customerRef = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var res = await _svc.CreateOrGetCustomerAsync(
            currency,
            nameEn,
            nameTh,
            email,
            req.bankCode,
            nameEn,
            nameTh,
            req.bankAccountNumber,
            customerRef,
            req.token,
            HttpContext.RequestAborted);
        if (!res.ok)
        {
            var code = res.code ?? ""; var msg = res.message ?? "";
            return Json(new { success = false, error = $"Failed to create or retrieve the customer! {code} {msg}" });
        }
        return Json(new { success = true, customerId = res.customerId, code = res.code, message = res.message });
    }

    public record CreateDepositRequest(string token, string customerId, decimal amount);

    [HttpPost("CreateDeposit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.token) || string.IsNullOrWhiteSpace(req.customerId) || req.amount <= 0)
        {
            return BadRequest(new { success = false, error = "Missing required fields" });
        }
        var refNo = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + new Random().Next(100000, 999999).ToString();
        var res = await _svc.CreateDepositAsync(req.customerId, req.amount, refNo, req.token, HttpContext.RequestAborted);
        if (!res.ok)
        {
            var code = res.code ?? ""; var msg = res.message ?? "";
            return Json(new { success = false, error = $"Deposit failed! {code} {msg}" });
        }
        // Response includes instructions; the user expects new tab to continue flow - here we just return raw data
        return Json(new { success = true, data = res.raw });
    }
}
