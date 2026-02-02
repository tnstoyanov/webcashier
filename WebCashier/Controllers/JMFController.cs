using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers;

[Route("JMF")]
public class JMFController : Controller
{
    private readonly IJMFService _jmfService;
    private readonly ILogger<JMFController> _logger;
    private readonly ICommLogService _commLog;

    public JMFController(
        IJMFService jmfService,
        ILogger<JMFController> logger,
        ICommLogService commLog)
    {
        _jmfService = jmfService;
        _logger = logger;
        _commLog = commLog;
    }

    /// <summary>
    /// Creates a payment session with JM Financial API.
    /// Returns redirect URL for the hosted payment page.
    /// </summary>
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] decimal amount,
        [FromForm] string currency,
        [FromForm] string customerName = "",
        [FromForm] string customerEmail = "")
    {
        try
        {
            await _commLog.LogAsync("jmf-create-inbound", new
            {
                provider = "JM Financial",
                action = "Create",
                amount,
                currency,
                customerName,
                customerEmail
            }, "jmf");

            if (amount <= 0)
            {
                _logger.LogWarning("[JMF] Invalid amount: {Amount}", amount);
                return Json(new { success = false, error = "Invalid amount" });
            }

            if (string.IsNullOrWhiteSpace(currency))
            {
                _logger.LogWarning("[JMF] Currency not specified");
                return Json(new { success = false, error = "Currency is required" });
            }

            _logger.LogInformation("[JMF] Creating payment session for {Amount} {Currency}", amount, currency);

            var result = await _jmfService.CreatePaymentSessionAsync(amount, currency, customerName, customerEmail);

            if (result == null)
            {
                _logger.LogError("[JMF] Service returned null response");
                await _commLog.LogAsync("jmf-create-error", new
                {
                    provider = "JM Financial",
                    action = "Create",
                    error = "Service returned null"
                }, "jmf");
                return Json(new { success = false, error = "Failed to create payment session" });
            }

            // Check if response has error
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                _logger.LogError("[JMF] API error: {Error}", result.Error);
                await _commLog.LogAsync("jmf-create-error", new
                {
                    provider = "JM Financial",
                    action = "Create",
                    error = result.Error
                }, "jmf");
                return Json(new { success = false, error = result.Error });
            }

            // Check if response has redirect URL
            if (string.IsNullOrWhiteSpace(result.Response?.RedirectUrl))
            {
                _logger.LogError("[JMF] No redirect URL in response");
                await _commLog.LogAsync("jmf-create-error", new
                {
                    provider = "JM Financial",
                    action = "Create",
                    error = "No redirect URL in response"
                }, "jmf");
                return Json(new { success = false, error = "No payment URL generated" });
            }

            _logger.LogInformation("[JMF] Payment session created successfully");

            await _commLog.LogAsync("jmf-create-success", new
            {
                provider = "JM Financial",
                action = "Create",
                amount,
                currency,
                orderNumber = result.Response.OrderNumber
            }, "jmf");

            return Json(new
            {
                success = true,
                paymentUrl = result.Response.RedirectUrl,
                orderNumber = result.Response.OrderNumber,
                sessionId = result.Response.SessionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JMF] Error in Create action");
            await _commLog.LogAsync("jmf-create-exception", new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, "jmf");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Success redirect page after payment completion.
    /// This is where the user is redirected after successful payment on JMF's hosted page.
    /// </summary>
    [HttpGet("Success")]
    public async Task<IActionResult> Success()
    {
        _logger.LogInformation("[JMF] Success page accessed");
        await _commLog.LogAsync("jmf-success", new
        {
            provider = "JM Financial",
            message = "User redirected to success page"
        }, "jmf");
        
        return View();
    }

    /// <summary>
    /// Cancel/Error redirect page after payment cancellation or failure.
    /// This is where the user is redirected if payment is cancelled or fails.
    /// </summary>
    [HttpGet("Cancel")]
    public async Task<IActionResult> Cancel()
    {
        _logger.LogInformation("[JMF] Cancel page accessed");
        await _commLog.LogAsync("jmf-cancel", new
        {
            provider = "JM Financial",
            message = "User redirected to cancel page"
        }, "jmf");
        
        return View();
    }
}
