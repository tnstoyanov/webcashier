using Microsoft.AspNetCore.Mvc;
using WebCashier.Models;
using WebCashier.Services;
using WebCashier.Models.Praxis;
using WebCashier.Models.Luxtak;
using System.Text.Json;

namespace WebCashier.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ILogger<PaymentController> _logger;
        private readonly IPraxisService _praxisService;
        private readonly LuxtakService _luxtakService;
        private readonly IPaymentStateService _paymentStateService;
        private readonly ISmilepayzService _smilepayzService;
        private readonly IPayPalService _paypalService;
        private readonly ICommLogService _commLog;

        public PaymentController(ILogger<PaymentController> logger, IPraxisService praxisService, LuxtakService luxtakService, IPaymentStateService paymentStateService, ISmilepayzService smilepayzService, IPayPalService paypalService, ICommLogService commLog)
        {
            _logger = logger;
            _praxisService = praxisService;
            _luxtakService = luxtakService;
            _paymentStateService = paymentStateService;
            _smilepayzService = smilepayzService;
            _paypalService = paypalService;
            _commLog = commLog;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index()
        {
            var model = new PaymentModel();
            return View(model);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken] // AJAX requests use X-Requested-With header which is CSRF-safe
        public async Task<IActionResult> Index(string paymentMethod, decimal amount, string currency)
        {
            _logger.LogInformation("=== AJAX PAYMENT REQUEST START ===");
            _logger.LogInformation("Payment Method: {PaymentMethod}, Amount: {Amount}, Currency: {Currency}", paymentMethod, amount, currency);

            try
            {
                if (paymentMethod?.ToLower() == "luxtak")
                {
                    _logger.LogInformation("Processing Luxtak payment via AJAX");

                    var response = await _luxtakService.CreateTradeAsync(amount, currency, "tony.stoyanov@tiebreak.dev", "Tony Stoyanov");

                    if (response != null && !string.IsNullOrEmpty(response.WebUrl))
                    {
                        _logger.LogInformation("Luxtak payment successful, web_url: {WebUrl}", response.WebUrl);
                        
                        return Json(new { 
                            success = true, 
                            paymentUrl = response.WebUrl,
                            tradeNo = response.TradeNo 
                        });
                    }
                    else
                    {
                        _logger.LogError("Luxtak payment failed - no web_url in response: {@Response}", response);
                        
                        return Json(new { 
                            success = false, 
                            error = "Payment initialization failed. Please try again." 
                        });
                    }
                }
                else if (paymentMethod?.ToLower() == "smilepayz-th")
                {
                    _logger.LogInformation("Processing Smilepayz TH payment via AJAX");

                    var response = await _smilepayzService.CreatePayInAsync(amount, currency, "Tony Stoyanov");

            if (response != null && response.Channel?.PaymentUrl != null)
                    {
                        _logger.LogInformation("Smilepayz payment initialized, paymentUrl: {Url}, status: {Status}", response.Channel.PaymentUrl, response.Status);
                        return Json(new {
                            success = true,
                            paymentUrl = response.Channel.PaymentUrl,
                status = response.Status,
                orderNo = response.OrderNo,
                tradeNo = response.TradeNo,
                            amount = response.Money?.Amount,
                            currency = response.Money?.Currency
                        });
                    }
                    else
                    {
                        _logger.LogError("Smilepayz payment init failed: {@Response}", response);
                        return Json(new { success = false, error = response?.Message ?? "Payment initialization failed" });
                    }
                }
                else if (paymentMethod?.ToLower() == "paypal")
                {
                    _logger.LogInformation("Processing PayPal payment via AJAX");

                    var referenceId = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Random.Shared.Next(10000, 99999);
                    var description = $"Payment {referenceId}";

                    var paypalOrder = await _paypalService.CreateOrderAsync(amount, currency, description, referenceId);

                    if (paypalOrder?.Id != null)
                    {
                        _logger.LogInformation("PayPal order object received - Id: {OrderId}, Status: {Status}, LinksCount: {LinksCount}", 
                            paypalOrder.Id, paypalOrder.Status, paypalOrder.Links?.Count ?? 0);
                        
                        // PayPal uses "payer-action" rel, not "approve"
                        var approvalLink = paypalOrder.Links?.FirstOrDefault(l => l.Rel == "payer-action")?.Href 
                                        ?? paypalOrder.Links?.FirstOrDefault(l => l.Rel == "approve")?.Href;

                        if (!string.IsNullOrWhiteSpace(approvalLink))
                        {
                            _logger.LogInformation("PayPal approval link found: {ApprovalLink}", approvalLink);
                            _logger.LogInformation("PayPal order created successfully: {OrderId}", paypalOrder.Id);
                            await _commLog.LogAsync("payment-paypal-order-created", new
                            {
                                orderId = paypalOrder.Id,
                                amount,
                                currency,
                                referenceId,
                                approvalLink
                            }, "payment-flow");

                            return Json(new
                            {
                                success = true,
                                paymentUrl = approvalLink,
                                orderId = paypalOrder.Id,
                                status = paypalOrder.Status
                            });
                        }
                        else
                        {
                            _logger.LogError("PayPal approval link is null/empty. Order: {OrderId}, Status: {Status}, Links: {@Links}", 
                                paypalOrder.Id, paypalOrder.Status, paypalOrder.Links);
                        }
                    }
                    else
                    {
                        _logger.LogError("PayPal order is null or missing Id. Order: {@PayPalOrder}", paypalOrder);
                    }

                    _logger.LogError("PayPal payment failed - unable to create order or get approval URL");
                    await _commLog.LogAsync("payment-paypal-error", new
                    {
                        amount,
                        currency,
                        error = "Order creation failed"
                    }, "payment-flow");

                    return Json(new
                    {
                        success = false,
                        error = "Failed to initialize PayPal payment. Please try again."
                    });
                }
                else
                {
                    _logger.LogWarning("Unsupported payment method: {PaymentMethod}", paymentMethod);
                    
                    return Json(new { 
                        success = false, 
                        error = "Unsupported payment method" 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AJAX payment request");
                await _commLog.LogAsync("payment-paypal-exception", new { error = ex.Message }, "payment-flow");
                
                return Json(new { 
                    success = false, 
                    error = "An error occurred while processing your payment. Please try again." 
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentModel model)
        {
            _logger.LogInformation("=== PAYMENT PROCESSING START ===");
            _logger.LogInformation("Received payment request: {@Model}", model);
            _logger.LogInformation("ModelState IsValid: {IsValid}", ModelState.IsValid);
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid, returning to Index view");
                foreach (var error in ModelState)
                {
                    foreach (var subError in error.Value.Errors)
                    {
                        _logger.LogWarning("ModelState Error - Key: {Key}, Error: {Error}", error.Key, subError.ErrorMessage);
                    }
                }
                return View("Index", model);
            }

            try
            {
                // Generate a unique order ID
                var orderId = Guid.NewGuid().ToString("N")[..16];
                
                // Get client IP address
                var clientIp = GetClientIpAddress();
                
                _logger.LogInformation("Generated OrderId: {OrderId}, Client IP: {ClientIp}", orderId, clientIp);
                _logger.LogInformation("Processing {PaymentMethod} payment for amount {Amount} {Currency} with OrderId {OrderId}", 
                    model.PaymentMethod, model.Amount, model.Currency, orderId);

                // Set payment as pending
                _paymentStateService.SetPaymentPending(orderId, "");

                // Handle different payment methods
                switch (model.PaymentMethod?.ToLower())
                {
                    case "luxtak":
                        _logger.LogInformation("Routing to Luxtak payment processing");
                        return await ProcessLuxtakPayment(model, orderId);
                    
                    case "card":
                    default:
                        _logger.LogInformation("Routing to Praxis (card) payment processing");
                        return await ProcessPraxisPayment(model, orderId, clientIp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== PAYMENT PROCESSING EXCEPTION ===");
                _logger.LogError("Unexpected error processing payment: {Message}", ex.Message);
                _logger.LogError("Exception Type: {Type}", ex.GetType().Name);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
                
                var result = new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while processing your payment. Please try again.",
                    TransactionId = ""
                };

                return View("PaymentFailure", result);
            }
        }

        private async Task<IActionResult> ProcessPraxisPayment(PaymentModel model, string orderId, string clientIp)
        {
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

        private async Task<IActionResult> ProcessLuxtakPayment(PaymentModel model, string orderId)
        {
            _logger.LogInformation("=== PROCESSING LUXTAK PAYMENT START ===");
            _logger.LogInformation("Processing Luxtak payment for OrderId: {OrderId}, Amount: {Amount}, Currency: {Currency}", 
                orderId, model.Amount, model.Currency);

            // Extract amount and currency from form data
            var amount = model.Amount;
            var currency = model.Currency ?? "USD";

            _logger.LogInformation("Final payment parameters - Amount: {Amount}, Currency: {Currency}", amount, currency);
            _logger.LogInformation("Request Headers: {@Headers}", Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            _logger.LogInformation("Is AJAX Request: {IsAjax}", Request.Headers["X-Requested-With"] == "XMLHttpRequest");

            // Call Luxtak API
            _logger.LogInformation("Calling LuxtakService.CreateTradeAsync...");
            var luxtakResponse = await _luxtakService.CreateTradeAsync(amount, currency);

            _logger.LogInformation("LuxtakService returned response: {@Response}", luxtakResponse);

            // Check if request was for AJAX (from the Luxtak form)
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                _logger.LogInformation("Processing AJAX request for Luxtak payment");
                
                // Return JSON response for AJAX handling
                if (luxtakResponse.Code == "10000" && !string.IsNullOrEmpty(luxtakResponse.WebUrl))
                {
                    _logger.LogInformation("=== LUXTAK PAYMENT SUCCESS ===");
                    _logger.LogInformation("Success case with web_url: {WebUrl}", luxtakResponse.WebUrl);
                    
                    // Success case with web_url
                    _paymentStateService.SetPaymentPending(orderId, luxtakResponse.TradeNo ?? orderId);
                    
                    var successResult = new { 
                        success = true, 
                        webUrl = luxtakResponse.WebUrl,
                        tradeNo = luxtakResponse.TradeNo 
                    };
                    
                    _logger.LogInformation("Returning success JSON: {@Result}", successResult);
                    _logger.LogInformation("=== PROCESSING LUXTAK PAYMENT END - AJAX SUCCESS ===");
                    
                    return Json(successResult);
                }
                else
                {
                    _logger.LogWarning("=== LUXTAK PAYMENT ERROR ===");
                    _logger.LogWarning("Error case - Code: {Code}, Message: {Message}, SubCode: {SubCode}, SubMessage: {SubMessage}", 
                        luxtakResponse.Code, luxtakResponse.Message, luxtakResponse.SubCode, luxtakResponse.SubMessage);
                    
                    // Error case - format error message
                    var errorMessage = $"Code: {luxtakResponse.Code}";
                    if (!string.IsNullOrEmpty(luxtakResponse.Message))
                        errorMessage += $". {luxtakResponse.Message}";
                    if (!string.IsNullOrEmpty(luxtakResponse.SubCode))
                        errorMessage += $" {luxtakResponse.SubCode}";
                    if (!string.IsNullOrEmpty(luxtakResponse.SubMessage))
                        errorMessage += $" {luxtakResponse.SubMessage}";

                    var errorResult = new { 
                        success = false, 
                        error = errorMessage 
                    };
                    
                    _logger.LogWarning("Returning error JSON: {@Result}", errorResult);
                    _logger.LogWarning("=== PROCESSING LUXTAK PAYMENT END - AJAX ERROR ===");
                    
                    return Json(errorResult);
                }
            }

            _logger.LogInformation("Processing non-AJAX request for Luxtak payment (legacy handling)");
            
            // Legacy handling for non-AJAX requests
            // Check if Luxtak API call was successful
            if (luxtakResponse.Code == "10000" && !string.IsNullOrEmpty(luxtakResponse.WebUrl))
            {
                _logger.LogInformation("=== LUXTAK PAYMENT SUCCESS (LEGACY) ===");
                
                // Update payment state with Luxtak transaction ID
                _paymentStateService.SetPaymentPending(orderId, luxtakResponse.TradeNo ?? orderId);

                // Redirect to web_url
                _logger.LogInformation("Redirecting to Luxtak web_url: {WebUrl}", luxtakResponse.WebUrl);
                _logger.LogInformation("=== PROCESSING LUXTAK PAYMENT END - LEGACY REDIRECT ===");
                
                return Redirect(luxtakResponse.WebUrl);
            }
            else
            {
                _logger.LogError("=== LUXTAK PAYMENT FAILURE (LEGACY) ===");
                _logger.LogError("Luxtak API error - Code: {Code}, Message: {Message}", luxtakResponse.Code, luxtakResponse.Message);
                
                var result = new PaymentResult
                {
                    Success = false,
                    Message = $"Luxtak payment failed: {luxtakResponse.Message}",
                    TransactionId = orderId
                };

                _logger.LogError("Showing PaymentFailure view with result: {@Result}", result);
                _logger.LogError("=== PROCESSING LUXTAK PAYMENT END - LEGACY FAILURE ===");
                
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
                        IsSuccess = transaction?.transaction_status == "approved",
                        TransactionId = transaction?.transaction_id,
                        PaymentMethod = transaction?.payment_method,
                        PaymentProcessor = transaction?.payment_processor,
                        Currency = transaction?.currency,
                        Amount = transaction != null ? (transaction.amount / 100.0m).ToString("F2") : "0.00",
                        CardType = transaction?.card?.card_type,
                        CardNumber = transaction?.card?.card_number,
                        StatusCode = transaction?.status_code,
                        StatusDetails = transaction?.status_details,
                        TransactionStatus = transaction?.transaction_status,
                        OrderId = session?.order_id
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
        [HttpPost]
        [Route("Payment/Return")]
        public async Task<IActionResult> Return()
        {
            try
            {
                _logger.LogInformation("Payment Return endpoint called via {Method}", Request.Method);
                _logger.LogInformation("Return URL query parameters: {QueryParams}", 
                    string.Join(", ", Request.Query.Select(kv => $"{kv.Key}={kv.Value}")));
                
                // Log additional information to help identify Luxtak returns
                var referer = Request.Headers["Referer"].ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();
                _logger.LogInformation("Referer: {Referer}", referer);
                _logger.LogInformation("User-Agent: {UserAgent}", userAgent);

                // Check if this is a Luxtak return based on query parameters
                var queryParams = Request.Query;
                if (queryParams.ContainsKey("trade_status") || queryParams.ContainsKey("trade_no"))
                {
                    return HandleLuxtakReturn(queryParams);
                }

                // Smilepayz redirect returns usually go back to redirectUrl; we don't have explicit params here.
                // We rely on explicit status pages after callback polling in a real app. For demo, show generic pending.
                if (!string.IsNullOrEmpty(referer) && referer.Contains("smilepayz", StringComparison.OrdinalIgnoreCase))
                {
                    var model = new PaymentReturnModel
                    {
                        IsSuccess = false,
                        PaymentMethod = "Smilepayz TH",
                        PaymentProcessor = "Smilepayz",
                        TransactionStatus = "PROCESSING",
                        StatusDetails = "Payment is being processed via Smilepayz"
                    };
                    return View("LuxtakPending", model);
                }

                // If this is a POST request with JSON (callback), handle it as a Praxis callback
                if (Request.Method == "POST" && Request.ContentType?.Contains("application/json") == true)
                {
                    return await HandlePraxisCallback();
                }

                // Check if this might be a Luxtak return without parameters
                // This can happen when there's no callback or incomplete redirect
                var isLikelyLuxtakReturn = false;
                
                // Check if referer contains luxtak domain
                if (!string.IsNullOrEmpty(referer) && (referer.Contains("luxtak.com") || referer.Contains("gateway.luxtak")))
                {
                    isLikelyLuxtakReturn = true;
                    _logger.LogInformation("Detected Luxtak return based on referer: {Referer}", referer);
                }
                
                // Check for recent Luxtak payment in progress
                var recentLuxtakPayment = _paymentStateService.GetMostRecentPendingPayment();
                if (recentLuxtakPayment != null && recentLuxtakPayment.CreatedAt != DateTime.MinValue && 
                    DateTime.UtcNow - recentLuxtakPayment.CreatedAt < TimeSpan.FromMinutes(30))
                {
                    isLikelyLuxtakReturn = true;
                    _logger.LogInformation("Found recent pending payment, likely Luxtak return without callback for OrderId: {OrderId}", recentLuxtakPayment.OrderId);
                }

                // If this looks like a Luxtak return without proper callback, show pending page
                if (isLikelyLuxtakReturn)
                {
                    var model = new PaymentReturnModel
                    {
                        TransactionId = recentLuxtakPayment?.TransactionId ?? "Unknown",
                        OrderId = recentLuxtakPayment?.OrderId ?? "Unknown",
                        Amount = "0.00",
                        Currency = "USD",
                        PaymentMethod = "Luxtak",
                        PaymentProcessor = "Luxtak",
                        TransactionStatus = "PROCESSING",
                        IsSuccess = false,
                        StatusDetails = "Payment is being processed via Luxtak"
                    };
                    
                    _logger.LogInformation("Showing Luxtak pending page for return without callback");
                    return View("LuxtakPending", model);
                }

                // Handle GET request with query parameters or fallback
                var orderId = queryParams["order_id"].ToString();
                
                if (!string.IsNullOrEmpty(orderId))
                {
                    return RedirectToAction("Result", new { orderId });
                }

                // Check for most recent completed payment (within last 5 minutes)
                var recentPayment = _paymentStateService.GetMostRecentCompletedPayment();
                if (recentPayment != null && recentPayment.CompletedAt.HasValue && 
                    DateTime.UtcNow - recentPayment.CompletedAt.Value < TimeSpan.FromMinutes(5))
                {
                    _logger.LogInformation("Found recent completed payment for OrderId {OrderId}, redirecting to result", recentPayment.OrderId);
                    return RedirectToAction("Result", new { orderId = recentPayment.OrderId });
                }

                // Fallback - show error
                return View("PaymentFailure", new PaymentReturnModel
                {
                    IsSuccess = false,
                    StatusDetails = "No callback received from payment processor!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment return");
                return View("PaymentFailure", new PaymentReturnModel
                {
                    IsSuccess = false,
                    StatusDetails = "An error occurred while processing the payment return"
                });
            }
        }

        private IActionResult HandleLuxtakReturn(IQueryCollection queryParams)
        {
            _logger.LogInformation("Handling Luxtak return with parameters: {@Params}", queryParams.ToDictionary(k => k.Key, v => v.Value.ToString()));

            var tradeStatus = queryParams["trade_status"].ToString();
            var tradeNo = queryParams["trade_no"].ToString();
            var outTradeNo = queryParams["out_trade_no"].ToString();
            var orderAmount = queryParams["order_amount"].ToString();
            var orderCurrency = queryParams["order_currency"].ToString();

            var model = new PaymentReturnModel
            {
                TransactionId = tradeNo,
                OrderId = outTradeNo,
                Amount = orderAmount,
                Currency = orderCurrency,
                PaymentMethod = "Luxtak",
                PaymentProcessor = "Luxtak",
                TransactionStatus = tradeStatus
            };

            switch (tradeStatus?.ToUpper())
            {
                case "SUCCESS":
                    model.IsSuccess = true;
                    model.StatusDetails = "Payment completed successfully via Luxtak";
                    _logger.LogInformation("Luxtak payment SUCCESS for OrderId: {OrderId}, TradeNo: {TradeNo}", outTradeNo, tradeNo);
                    return View("LuxtakSuccess", model);

                case "PROCESSING":
                    model.IsSuccess = false;
                    model.StatusDetails = "Payment is being processed via Luxtak";
                    _logger.LogInformation("Luxtak payment PROCESSING for OrderId: {OrderId}, TradeNo: {TradeNo}", outTradeNo, tradeNo);
                    return View("LuxtakPending", model);

                case "CANCEL":
                case "CANCELLED":
                case "EXPIRED":
                    model.IsSuccess = false;
                    model.StatusDetails = tradeStatus?.ToUpper() == "EXPIRED" ? "Payment expired via Luxtak" : "Payment was cancelled via Luxtak";
                    _logger.LogInformation("Luxtak payment {Status} for OrderId: {OrderId}, TradeNo: {TradeNo}", tradeStatus?.ToUpper(), outTradeNo, tradeNo);
                    return View("LuxtakCancelled", model);

                default:
                    model.IsSuccess = false;
                    model.StatusDetails = $"Unknown payment status: {tradeStatus}";
                    _logger.LogWarning("Unknown Luxtak payment status '{Status}' for OrderId: {OrderId}, TradeNo: {TradeNo}", tradeStatus, outTradeNo, tradeNo);
                    return View("PaymentFailure", model);
            }
        }

        private async Task<IActionResult> HandlePraxisCallback()
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body);
            var jsonBody = await reader.ReadToEndAsync();
            
            _logger.LogInformation("Praxis callback received at Return endpoint: {JsonBody}", jsonBody);
            
            if (!string.IsNullOrEmpty(jsonBody))
            {
                try
                {
                    var callbackData = JsonSerializer.Deserialize<PraxisCallbackModel>(jsonBody);
                    
                    if (callbackData?.session?.order_id != null)
                    {
                        var callbackOrderId = callbackData.session.order_id;
                        
                        _logger.LogInformation("Processing Praxis callback for OrderId: {OrderId}", callbackOrderId);
                        
                        // Update payment state
                        _paymentStateService.SetPaymentCompleted(callbackOrderId, callbackData);
                        
                        // Create result model
                        var transaction = callbackData.transaction;
                        var model = new PaymentReturnModel
                        {
                            IsSuccess = transaction?.transaction_status == "approved",
                            TransactionId = transaction?.transaction_id,
                            PaymentMethod = transaction?.payment_method,
                            PaymentProcessor = transaction?.payment_processor,
                            Currency = transaction?.currency,
                            Amount = transaction != null ? (transaction.amount / 100.0m).ToString("F2") : "0.00",
                            CardType = transaction?.card?.card_type,
                            CardNumber = transaction?.card?.card_number,
                            StatusCode = transaction?.status_code,
                            StatusDetails = transaction?.status_details,
                            TransactionStatus = transaction?.transaction_status,
                            OrderId = callbackOrderId
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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Praxis callback JSON at Return endpoint");
                }
            }

            return View("PaymentFailure", new PaymentReturnModel
            {
                IsSuccess = false,
                StatusDetails = "Invalid callback data received"
            });
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

        [HttpGet]
        public IActionResult PaymentSuccess(string provider, string orderId)
        {
            _logger.LogInformation("PaymentSuccess called: provider={Provider}, orderId={OrderId}", provider, orderId);
            
            var model = new PaymentReturnModel
            {
                TransactionId = orderId,
                OrderId = orderId,
                PaymentMethod = provider ?? "Unknown",
                PaymentProcessor = provider ?? "Unknown",
                TransactionStatus = "SUCCESS",
                IsSuccess = true,
                StatusDetails = $"Payment completed successfully via {provider}"
            };

            // Try to get additional details from session if available
            if (!string.IsNullOrWhiteSpace(provider) && provider.Equals("PayPal", StringComparison.OrdinalIgnoreCase))
            {
                var sessionStatus = HttpContext.Session.GetString("PayPalOrderStatus");
                var sessionOrderId = HttpContext.Session.GetString("PayPalOrderId");
                
                if (!string.IsNullOrWhiteSpace(sessionStatus))
                {
                    model.TransactionStatus = sessionStatus;
                }
                if (!string.IsNullOrWhiteSpace(sessionOrderId))
                {
                    model.TransactionId = sessionOrderId;
                }
            }

            return View("PaymentSuccess", model);
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

        [HttpGet]
        public IActionResult SmilepayzStatus(string status, string? orderNo, string? tradeNo, string? amount, string? currency)
        {
            var model = new PaymentReturnModel
            {
                TransactionId = tradeNo,
                OrderId = orderNo,
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(currency) ? "THB" : currency,
                PaymentMethod = "Smilepayz TH",
                PaymentProcessor = "Smilepayz",
                TransactionStatus = status?.ToUpper()
            };

            switch (status?.ToUpper())
            {
                case "SUCCESS":
                    model.IsSuccess = true;
                    model.StatusDetails = "Payment completed successfully via Smilepayz";
                    return View("PaymentSuccess", model);
                case "PROCESSING":
                    model.IsSuccess = false;
                    model.StatusDetails = "Payment is being processed via Smilepayz";
                    return View("LuxtakPending", model);
                case "FAILED":
                    model.IsSuccess = false;
                    model.StatusDetails = "Payment failed via Smilepayz";
                    return View("PaymentFailure", model);
                default:
                    model.IsSuccess = false;
                    model.StatusDetails = $"Unknown payment status: {status}";
                    return View("PaymentFailure", model);
            }
        }
    }
}
