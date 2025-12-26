using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    [Route("PayPal")]
    public class PayPalController : Controller
    {
        private readonly IPayPalService _paypalService;
        private readonly ILogger<PayPalController> _logger;
        private readonly ICommLogService _commLog;

        public PayPalController(
            IPayPalService paypalService,
            ILogger<PayPalController> logger,
            ICommLogService commLog)
        {
            _paypalService = paypalService;
            _logger = logger;
            _commLog = commLog;
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] decimal amount, [FromForm] string currency, [FromForm] string description = "Payment via WebCashier")
        {
            try
            {
                await _commLog.LogAsync("paypal-inbound", new
                {
                    provider = "PayPal",
                    action = "Create",
                    amount,
                    currency,
                    description
                }, "paypal");

                var referenceId = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Random.Shared.Next(10000, 99999);
                
                var order = await _paypalService.CreateOrderAsync(amount, currency, description, referenceId);
                
                if (order?.Id == null)
                {
                    _logger.LogError("[PayPal] Order creation returned null or missing ID");
                    return Json(new { success = false, error = "Failed to create order" });
                }

                // Find the approval link
                var approvalLink = order.Links?.FirstOrDefault(l => l.Rel == "approve")?.Href;
                
                if (string.IsNullOrWhiteSpace(approvalLink))
                {
                    _logger.LogError("[PayPal] Order created but approval link not found");
                    return Json(new { success = false, error = "No approval link in order response" });
                }

                _logger.LogInformation("[PayPal] Order created successfully: {OrderId}", order.Id);
                
                return Json(new
                {
                    success = true,
                    orderId = order.Id,
                    approvalUrl = approvalLink,
                    status = order.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error in Create action");
                await _commLog.LogAsync("paypal-create-exception", new { error = ex.Message }, "paypal");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("Capture")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Capture([FromForm] string orderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(orderId))
                {
                    return Json(new { success = false, error = "Missing orderId" });
                }

                await _commLog.LogAsync("paypal-inbound", new
                {
                    provider = "PayPal",
                    action = "Capture",
                    orderId
                }, "paypal");

                var captureResult = await _paypalService.CaptureOrderAsync(orderId);
                
                if (captureResult?.Id == null)
                {
                    _logger.LogError("[PayPal] Order capture failed for {OrderId}", orderId);
                    return Json(new { success = false, error = "Capture failed" });
                }

                _logger.LogInformation("[PayPal] Order captured: {OrderId}, Status: {Status}", captureResult.Id, captureResult.Status);

                return Json(new
                {
                    success = true,
                    orderId = captureResult.Id,
                    status = captureResult.Status,
                    captures = captureResult.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?
                        .Select(c => new { c.Id, c.Status, Amount = c.Amount?.Value })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error in Capture action");
                await _commLog.LogAsync("paypal-capture-exception", new { error = ex.Message }, "paypal");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("Return")]
        public async Task<IActionResult> Return([FromQuery] string token)
        {
            try
            {
                await _commLog.LogAsync("paypal-inbound", new
                {
                    provider = "PayPal",
                    action = "Return",
                    orderId = token
                }, "paypal");

                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("[PayPal] Return callback received without token");
                    return RedirectToAction("PaymentFailure", "Payment");
                }

                var order = await _paypalService.GetOrderAsync(token);
                
                if (order?.Id == null)
                {
                    _logger.LogError("[PayPal] Failed to retrieve order {OrderId}", token);
                    return RedirectToAction("PaymentFailure", "Payment");
                }

                _logger.LogInformation("[PayPal] Return callback: OrderId={OrderId}, Status={Status}", order.Id, order.Status);

                // Check if order is approved before attempting capture
                if (order.Status != "APPROVED")
                {
                    _logger.LogError("[PayPal] Order {OrderId} is not in APPROVED state: {Status}", order.Id, order.Status);
                    await _commLog.LogAsync("paypal-return-not-approved", new
                    {
                        orderId = order.Id,
                        status = order.Status
                    }, "paypal");
                    return RedirectToAction("PaymentFailure", "Payment");
                }

                // Capture the order
                _logger.LogInformation("[PayPal] Attempting to capture order {OrderId}", order.Id);
                var capturedOrder = await _paypalService.CaptureOrderAsync(order.Id);

                if (capturedOrder?.Id == null || capturedOrder.Status != "COMPLETED")
                {
                    _logger.LogError("[PayPal] Failed to capture order {OrderId}. Response status: {Status}", 
                        order.Id, capturedOrder?.Status ?? "null");
                    await _commLog.LogAsync("paypal-capture-failed", new
                    {
                        orderId = order.Id,
                        capturedStatus = capturedOrder?.Status ?? "null"
                    }, "paypal");
                    return RedirectToAction("PaymentFailure", "Payment");
                }

                _logger.LogInformation("[PayPal] Order captured successfully: {OrderId}", capturedOrder.Id);
                
                // Store order details in session for display
                HttpContext.Session.SetString("PayPalOrderId", capturedOrder.Id);
                HttpContext.Session.SetString("PayPalOrderStatus", capturedOrder.Status ?? "unknown");

                await _commLog.LogAsync("paypal-capture-completed", new
                {
                    orderId = capturedOrder.Id,
                    status = capturedOrder.Status
                }, "paypal");

                // Redirect to success page
                return RedirectToAction("PaymentSuccess", "Payment", new { provider = "PayPal", orderId = capturedOrder.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error in Return action");
                await _commLog.LogAsync("paypal-return-exception", new { error = ex.Message }, "paypal");
                return RedirectToAction("PaymentFailure", "Payment");
            }
        }

        [HttpGet("Cancel")]
        public async Task<IActionResult> Cancel([FromQuery] string token)
        {
            try
            {
                await _commLog.LogAsync("paypal-inbound", new
                {
                    provider = "PayPal",
                    action = "Cancel",
                    orderId = token
                }, "paypal");

                _logger.LogWarning("[PayPal] Payment cancelled by user: OrderId={OrderId}", token);
                return RedirectToAction("Index", "Payment");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error in Cancel action");
                await _commLog.LogAsync("paypal-cancel-exception", new { error = ex.Message }, "paypal");
                return RedirectToAction("Index", "Payment");
            }
        }

        [HttpPost("Notification")]
        public async Task<IActionResult> Notification()
        {
            try
            {
                var body = await new StreamReader(Request.Body).ReadToEndAsync();
                
                await _commLog.LogAsync("paypal-webhook", new
                {
                    provider = "PayPal",
                    eventType = Request.Headers["X-Event-Type"].ToString(),
                    body = body
                }, "paypal");

                var eventType = Request.Headers["X-Event-Type"].ToString();
                _logger.LogInformation("[PayPal] Webhook received: {EventType}", eventType);

                // Return 200 OK to acknowledge receipt
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error processing webhook notification");
                await _commLog.LogAsync("paypal-webhook-exception", new { error = ex.Message }, "paypal");
                return StatusCode(500);
            }
        }
    }
}
