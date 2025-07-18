using Microsoft.AspNetCore.Mvc;
using WebCashier.Models;
using WebCashier.Services;
using WebCashier.Models.Praxis;
using System.Text.Json;

namespace WebCashier.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ILogger<PaymentController> _logger;
        private readonly IPraxisService _praxisService;
        private readonly IPaymentStateService _paymentStateService;

        public PaymentController(ILogger<PaymentController> logger, IPraxisService praxisService, IPaymentStateService paymentStateService)
        {
            _logger = logger;
            _praxisService = praxisService;
            _paymentStateService = paymentStateService;
        }

        public IActionResult Index()
        {
            var model = new PaymentModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            try
            {
                // Generate a unique order ID
                var orderId = Guid.NewGuid().ToString("N")[..16];
                
                // Get client IP address
                var clientIp = GetClientIpAddress();
                
                _logger.LogInformation("Processing payment for amount {Amount} with OrderId {OrderId}", model.Amount, orderId);

                // Set payment as pending
                _paymentStateService.SetPaymentPending(orderId, "");

                // Call Praxis API
                var praxisResponse = await _praxisService.ProcessPaymentAsync(model, clientIp, orderId);

                // Update with transaction ID from Praxis response
                _paymentStateService.SetPaymentPending(orderId, praxisResponse.transaction_id ?? "");

                // Check if Praxis returned a redirect URL (for 3DS authentication)
                if (!string.IsNullOrEmpty(praxisResponse.redirect_url))
                {
                    _logger.LogInformation("Redirecting to 3DS authentication: {RedirectUrl}", praxisResponse.redirect_url);
                    return Redirect(praxisResponse.redirect_url);
                }

                // Always redirect to processing page to wait for callback
                ViewBag.OrderId = orderId;
                return View("Processing", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment");
                
                var result = new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while processing your payment. Please try again.",
                    TransactionId = ""
                };

                return View("PaymentFailure", result);
            }
        }

        [HttpGet]
        public IActionResult CheckStatus(string orderId)
        {
            try
            {
                var state = _paymentStateService.GetPaymentState(orderId);
                
                if (state == null)
                {
                    return Json(new { status = "not_found" });
                }

                switch (state.Status)
                {
                    case PaymentStatus.Pending:
                        return Json(new { status = "pending" });
                    case PaymentStatus.Completed:
                    case PaymentStatus.Failed:
                        return Json(new { status = "completed" });
                    case PaymentStatus.Timeout:
                        return Json(new { status = "timeout" });
                    default:
                        return Json(new { status = "unknown" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking payment status for OrderId {OrderId}", orderId);
                return Json(new { status = "error" });
            }
        }

        [HttpGet]
        public IActionResult Result(string orderId)
        {
            try
            {
                var state = _paymentStateService.GetPaymentState(orderId);
                
                if (state == null)
                {
                    return View("PaymentFailure", new PaymentReturnModel
                    {
                        IsSuccess = false,
                        StatusDetails = "No callback received from Praxis!"
                    });
                }

                if (state.Status == PaymentStatus.Completed && state.CallbackData != null)
                {
                    var transaction = state.CallbackData.transaction;
                    var session = state.CallbackData.session;
                    
                    var model = new PaymentReturnModel
                    {
                        IsSuccess = transaction.transaction_status == "approved",
                        TransactionId = transaction.transaction_id,
                        PaymentMethod = transaction.payment_method,
                        PaymentProcessor = transaction.payment_processor,
                        Currency = transaction.currency,
                        Amount = (transaction.amount / 100.0m).ToString("F2"),
                        CardType = transaction.card?.card_type,
                        CardNumber = transaction.card?.card_number,
                        StatusCode = transaction.status_code,
                        StatusDetails = transaction.status_details,
                        TransactionStatus = transaction.transaction_status,
                        OrderId = session.order_id
                    };

                    if (model.IsSuccess)
                    {
                        return View("PaymentSuccess", model);
                    }
                    else
                    {
                        return View("PaymentFailure", model);
                    }
                }
                else
                {
                    return View("PaymentFailure", new PaymentReturnModel
                    {
                        IsSuccess = false,
                        StatusDetails = state.ErrorMessage ?? "No callback received from Praxis!"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment result for OrderId {OrderId}", orderId);
                return View("PaymentFailure", new PaymentReturnModel
                {
                    IsSuccess = false,
                    StatusDetails = "An error occurred while processing the payment result"
                });
            }
        }

        [HttpGet]
        public IActionResult Timeout(string orderId)
        {
            _logger.LogWarning("Payment timeout for OrderId {OrderId}", orderId);
            
            return View("PaymentFailure", new PaymentReturnModel
            {
                IsSuccess = false,
                StatusDetails = "Payment processing timed out. No callback received from Praxis!"
            });
        }

        [HttpGet]
        public IActionResult Error(string orderId)
        {
            _logger.LogError("Payment error for OrderId {OrderId}", orderId);
            
            return View("PaymentFailure", new PaymentReturnModel
            {
                IsSuccess = false,
                StatusDetails = "An error occurred during payment processing"
            });
        }

        public IActionResult Success()
        {
            return View();
        }

        private string GetClientIpAddress()
        {
            // Try to get the real IP address from various headers
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            
            // Check for forwarded headers (common in reverse proxy scenarios)
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
            }
            else if (Request.Headers.ContainsKey("X-Real-IP"))
            {
                ipAddress = Request.Headers["X-Real-IP"].FirstOrDefault();
            }

            return ipAddress ?? "127.0.0.1";
        }
    }
}
