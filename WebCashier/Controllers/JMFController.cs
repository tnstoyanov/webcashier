using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using WebCashier.Services;

namespace WebCashier.Controllers;

[Route("JMF")]
public class JMFController : Controller
{
    private readonly IJMFService _jmfService;
    private readonly ILogger<JMFController> _logger;
    private readonly ICommLogService _commLog;
    private readonly IHttpClientFactory _httpClientFactory;

    public JMFController(
        IJMFService jmfService,
        ILogger<JMFController> logger,
        ICommLogService commLog,
        IHttpClientFactory httpClientFactory)
    {
        _jmfService = jmfService;
        _logger = logger;
        _commLog = commLog;
        _httpClientFactory = httpClientFactory;
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

    /// <summary>
    /// Check payment status with JM Financial API.
    /// This endpoint is called from the Success/Cancel pages to verify transaction status.
    /// </summary>
    [HttpPost("Status")]
    public async Task<IActionResult> Status([FromBody] StatusCheckRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.order_id))
        {
            _logger.LogWarning("[JMF] Status check missing order_id");
            return Json(new { error = "Missing order_id" });
        }

        try
        {
            _logger.LogInformation("[JMF] Checking status for order: {OrderId}", request.order_id);

            var client = _httpClientFactory.CreateClient();
            var payload = JsonSerializer.Serialize(new
            {
                merchant_key = request.merchant_key,
                order_id = request.order_id,
                hash = request.hash
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://checkout.jmfinancialkw.com/api/v1/payment/status", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("[JMF] Status API Response: {StatusCode} - {Content}", response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[JMF] Status check failed: {StatusCode}", response.StatusCode);
                return Json(new { error = $"API returned status {response.StatusCode}" });
            }

            // Parse and forward the response from JM Financial
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JMF] Error checking payment status");
            return Json(new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for payment status checking
/// </summary>
public class StatusCheckRequest
{
    public string? merchant_key { get; set; }
    public string? order_id { get; set; }
    public string? hash { get; set; }
}
