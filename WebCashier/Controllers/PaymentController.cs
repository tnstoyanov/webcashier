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
        [HttpPost]
        public async Task<IActionResult> Return()
        {
            try
            {
                _logger.LogInformation("Payment return endpoint called via {Method}", Request.Method);
                
                // Check if we have transaction data from the callback
                var tid = Request.Query["tid"].ToString();
                if (!string.IsNullOrEmpty(tid))
                {
                    var transactionDataJson = TempData[$"transaction_{tid}"]?.ToString();
                    if (!string.IsNullOrEmpty(transactionDataJson))
                    {
                        _logger.LogInformation("Found transaction data from callback for TID: {TID}", tid);
                        
                        try
                        {
                            var transactionData = JsonSerializer.Deserialize<PraxisTransaction>(transactionDataJson);
                            
                            if (transactionData != null)
                            {
                                var model = new PaymentReturnModel
                                {
                                    IsSuccess = transactionData.transaction_status == "approved",
                                    TransactionId = transactionData.tid.ToString(),
                                    PaymentMethod = transactionData.payment_method ?? "",
                                    PaymentProcessor = transactionData.payment_processor ?? "",
                                    Currency = transactionData.currency ?? "",
                                    CardType = transactionData.card?.card_type ?? "",
                                    CardNumber = transactionData.card?.card_number ?? "",
                                    StatusCode = transactionData.status_code ?? "",
                                    StatusDetails = transactionData.status_details ?? "",
                                    TransactionStatus = transactionData.transaction_status ?? "",
                                    Amount = transactionData.amount.ToString()
                                };

                                _logger.LogInformation("Using callback data - Status: {Status}, TID: {TID}", 
                                    transactionData.transaction_status, transactionData.tid);

                                if (model.IsSuccess)
                                {
                                    return View("PaymentSuccess", model);
                                }
                                else
                                {
                                    return View("PaymentFailure", model);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to deserialize transaction data from callback");
                        }
                    }
                }

                // First, try to handle JSON callback from Praxis (fallback)
                if (Request.Method == "POST" && Request.ContentType?.Contains("application/json") == true)
                {
                    Request.EnableBuffering();
                    using var reader = new StreamReader(Request.Body);
                    var jsonBody = await reader.ReadToEndAsync();
                    
                    _logger.LogInformation("Praxis JSON callback received at Return endpoint: {JsonBody}", jsonBody);
                    
                    try
                    {
                        var callbackData = JsonSerializer.Deserialize<PraxisCallbackModel>(jsonBody, new JsonSerializerOptions 
                        { 
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                        });
                        
                        if (callbackData?.transaction != null)
                        {
                            var model = new PaymentReturnModel
                            {
                                IsSuccess = callbackData.transaction.transaction_status == "approved",
                                TransactionId = callbackData.transaction.tid.ToString(),
                                PaymentMethod = callbackData.transaction.payment_method ?? "",
                                PaymentProcessor = callbackData.transaction.payment_processor ?? "",
                                Currency = callbackData.transaction.currency ?? "",
                                CardType = callbackData.transaction.card?.card_type ?? "",
                                CardNumber = callbackData.transaction.card?.card_number ?? "",
                                StatusCode = callbackData.transaction.status_code ?? "",
                                StatusDetails = callbackData.transaction.status_details ?? "",
                                TransactionStatus = callbackData.transaction.transaction_status ?? "",
                                Amount = callbackData.transaction.amount.ToString()
                            };

                            _logger.LogInformation("Praxis callback processed - Status: {Status}, TID: {TID}", 
                                callbackData.transaction.transaction_status, callbackData.transaction.tid);

                            if (model.IsSuccess)
                            {
                                return View("PaymentSuccess", model);
                            }
                            else
                            {
                                return View("PaymentFailure", model);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse Praxis callback JSON");
                    }
                }

                // Fall back to query parameter handling
                var queryParams = Request.Query;
                
                _logger.LogInformation("Payment return received with query parameters: {QueryParams}", 
                    string.Join(", ", queryParams.Select(kv => $"{kv.Key}={kv.Value}")));

                // Parse the parameters that Praxis sends back
                var transactionStatus = queryParams["transaction_status"].ToString();
                var queryTid = queryParams["tid"].ToString();
                var paymentMethod = queryParams["payment_method"].ToString();
                var paymentProcessor = queryParams["payment_processor"].ToString();
                var currency = queryParams["currency"].ToString();
                var cardType = queryParams["card_type"].ToString();
                var cardNumber = queryParams["card_number"].ToString();
                var statusCode = queryParams["status_code"].ToString();
                var statusDetails = queryParams["status_details"].ToString();

                var fallbackModel = new PaymentReturnModel
                {
                    IsSuccess = transactionStatus == "approved" || transactionStatus == "success" || transactionStatus == "completed",
                    TransactionId = queryTid,
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
                    transactionStatus, queryTid, paymentMethod);

                if (fallbackModel.IsSuccess)
                {
                    return View("PaymentSuccess", fallbackModel);
                }
                else
                {
                    return View("PaymentFailure", fallbackModel);
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

        [HttpPost]
        [Route("Payment/Callback")]
        public async Task<IActionResult> Callback()
        {
            try
            {
                _logger.LogInformation("Praxis notification callback received");
                
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body);
                var jsonBody = await reader.ReadToEndAsync();
                
                _logger.LogInformation("Praxis callback payload: {JsonBody}", jsonBody);
                
                if (string.IsNullOrEmpty(jsonBody))
                {
                    _logger.LogWarning("Empty callback payload received");
                    return Ok("Empty payload");
                }

                try
                {
                    var callbackData = JsonSerializer.Deserialize<PraxisCallbackModel>(jsonBody, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    });
                    
                    if (callbackData?.transaction != null)
                    {
                        _logger.LogInformation("Praxis callback processed - Status: {Status}, TID: {TID}, Amount: {Amount}", 
                            callbackData.transaction.transaction_status, 
                            callbackData.transaction.tid,
                            callbackData.transaction.amount);

                        // Store transaction details for later retrieval
                        // In a real application, you would save this to a database
                        TempData[$"transaction_{callbackData.transaction.tid}"] = JsonSerializer.Serialize(callbackData.transaction);
                        
                        return Ok("Callback processed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Invalid callback data structure");
                        return Ok("Invalid callback data");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse Praxis callback JSON");
                    return Ok("JSON parsing failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Praxis callback");
                return Ok("Error processing callback");
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
