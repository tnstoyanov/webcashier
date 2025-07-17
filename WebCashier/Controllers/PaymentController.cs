using Microsoft.AspNetCore.Mvc;
using WebCashier.Models;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ILogger<PaymentController> _logger;
        private readonly IPraxisService _praxisService;

        public PaymentController(ILogger<PaymentController> logger, IPraxisService praxisService)
        {
            _logger = logger;
            _praxisService = praxisService;
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
                // Get client IP address
                var clientIp = GetClientIpAddress();
                
                _logger.LogInformation("Processing payment for amount {Amount} using Praxis API", model.Amount);

                // Call Praxis API
                var praxisResponse = await _praxisService.ProcessPaymentAsync(model, clientIp);

                // Check if Praxis returned a redirect URL (for 3DS authentication)
                if (!string.IsNullOrEmpty(praxisResponse.redirect_url))
                {
                    _logger.LogInformation("Redirecting to 3DS authentication: {RedirectUrl}", praxisResponse.redirect_url);
                    return Redirect(praxisResponse.redirect_url);
                }

                // Handle direct response (no 3DS required)
                if (praxisResponse.IsSuccess && praxisResponse.transaction?.transaction_status == "approved")
                {
                    var result = new PaymentResult
                    {
                        Success = true,
                        Message = "Payment processed successfully!",
                        TransactionId = praxisResponse.transaction_id
                    };

                    _logger.LogInformation("Payment processed successfully: TransactionId={TransactionId}", result.TransactionId);
                    return View("Success", result);
                }
                else
                {
                    var result = new PaymentResult
                    {
                        Success = false,
                        Message = praxisResponse.description ?? "Payment processing failed",
                        TransactionId = praxisResponse.transaction_id
                    };

                    _logger.LogWarning("Payment failed: {Message}, Transaction Status: {TransactionStatus}", 
                        result.Message, praxisResponse.transaction?.transaction_status);
                    return View("Success", result);
                }
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

                return View("Success", result);
            }
        }

        [HttpGet]
        public IActionResult Return()
        {
            try
            {
                // Read the query parameters from the return URL
                var queryParams = Request.Query;
                
                _logger.LogInformation("Payment return received with query parameters: {QueryParams}", 
                    string.Join(", ", queryParams.Select(kv => $"{kv.Key}={kv.Value}")));

                // Parse the parameters that Praxis sends back
                var transactionStatus = queryParams["transaction_status"].ToString();
                var tid = queryParams["tid"].ToString();
                var paymentMethod = queryParams["payment_method"].ToString();
                var paymentProcessor = queryParams["payment_processor"].ToString();
                var currency = queryParams["currency"].ToString();
                var cardType = queryParams["card_type"].ToString();
                var cardNumber = queryParams["card_number"].ToString();
                var statusCode = queryParams["status_code"].ToString();
                var statusDetails = queryParams["status_details"].ToString();

                var model = new PaymentReturnModel
                {
                    IsSuccess = transactionStatus == "approved" || transactionStatus == "success" || transactionStatus == "completed",
                    TransactionId = tid,
                    PaymentMethod = paymentMethod,
                    PaymentProcessor = paymentProcessor,
                    Currency = currency,
                    CardType = cardType,
                    CardNumber = cardNumber,
                    StatusCode = statusCode,
                    StatusDetails = statusDetails,
                    TransactionStatus = transactionStatus
                };

                _logger.LogInformation("Payment return processed - Status: {Status}, TID: {TID}, Method: {Method}", 
                    transactionStatus, tid, paymentMethod);

                if (model.IsSuccess)
                {
                    return View("PaymentSuccess", model);
                }
                else
                {
                    return View("PaymentFailure", model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment return");
                
                var errorModel = new PaymentReturnModel
                {
                    IsSuccess = false,
                    StatusDetails = "An error occurred while processing the payment return"
                };
                
                return View("PaymentFailure", errorModel);
            }
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
