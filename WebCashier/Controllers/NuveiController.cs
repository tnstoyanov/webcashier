using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    [Route("Nuvei")] // Ensure base path /Nuvei
    public class NuveiController : Controller
    {
        private readonly INuveiService _nuvei;
        private readonly ILogger<NuveiController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly ICommLogService _commLog;
    private readonly IRuntimeConfigStore _runtime;
    private readonly IConfiguration _configuration;

        public NuveiController(INuveiService nuvei, ILogger<NuveiController> logger, IHttpContextAccessor http, ICommLogService commLog, IRuntimeConfigStore runtime, IConfiguration configuration)
        {
            _nuvei = nuvei; _logger = logger; _http = http; _commLog = commLog; _runtime = runtime; _configuration = configuration;
        }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] decimal amount, [FromForm] string currency, [FromForm] string paymentMethod = "ppp_GooglePay")
        {
            try
            {
                await _commLog.LogAsync("nuvei-inbound", new { provider = "Nuvei", action = "Create", amount, currency, paymentMethod }, "nuvei");
                var baseUrl = GetBaseUrl();
                var form = _nuvei.BuildPaymentForm(new NuveiRequest(amount, currency, "12204834", "cashier", paymentMethod), baseUrl);
                var formUrl = form.SubmitFormUrl;
                if (string.IsNullOrWhiteSpace(formUrl))
                {
                    // Fallback to default PPP endpoint if missing (misconfiguration or blank runtime value)
                    formUrl = "https://ppp-test.safecharge.com/ppp/purchase.do";
                    _logger.LogWarning("Nuvei form SubmitFormUrl was blank. Using default PPP URL fallback.");
                }
                var responseObj = new
                {
                    success = true,
                    formUrl,
                    fields = form.Fields.Select(f => new { f.Key, f.Value })
                };
                // Mask sensitive values before logging outbound response (client side form build)
                var masked = new {
                    responseObj.success,
                    responseObj.formUrl,
                    fields = form.Fields.Select(f => new {
                        f.Key,
                        Value = f.Key.Equals("checksum", StringComparison.OrdinalIgnoreCase) && f.Value != null
                            ? (f.Value.Length > 12 ? f.Value.Substring(0,6) + "..." + f.Value[^4..] : f.Value)
                            : f.Key.Contains("secret", StringComparison.OrdinalIgnoreCase) ? "***" : f.Value
                    })
                };
                await _commLog.LogAsync("nuvei-outbound-response", masked, "nuvei");
                return Json(responseObj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building Nuvei form");
                await _commLog.LogAsync("nuvei-error", new { provider = "Nuvei", action = "Create", message = ex.Message }, "nuvei");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Generates the Apple Pay IFrame payment URL with all parameters.
        /// Returns a complete GET URL that can be loaded in an iframe.
        /// </summary>
        [HttpPost("ApplePay/GetIFrameUrl")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetApplePayIFrameUrl([FromForm] decimal amount, [FromForm] string currency)
        {
            try
            {
                _logger.LogInformation("Generating Apple Pay IFrame URL for amount={Amount} currency={Currency}", amount, currency);

                await _commLog.LogAsync("nuvei-applepay-iframe-inbound", new
                {
                    provider = "Nuvei",
                    action = "GetApplePayIFrameUrl",
                    amount,
                    currency
                }, "nuvei");

                var baseUrl = GetBaseUrl();
                var form = _nuvei.BuildPaymentForm(new NuveiRequest(amount, currency, "12204834", "cashier", "ppp_ApplePay"), baseUrl);
                
                var formUrl = form.SubmitFormUrl;
                if (string.IsNullOrWhiteSpace(formUrl))
                {
                    formUrl = "https://ppp-test.safecharge.com/ppp/purchase.do";
                    _logger.LogWarning("Nuvei form SubmitFormUrl was blank for Apple Pay IFrame. Using default PPP URL fallback.");
                }

                // Build GET URL with query parameters
                var uriBuilder = new UriBuilder(formUrl);
                var queryParams = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                
                // Add all fields from the form to query parameters
                foreach (var field in form.Fields)
                {
                    queryParams[field.Key] = field.Value;
                }

                // Add parent_url parameter for Apple Pay IFrame support
                queryParams["parent_url"] = baseUrl;

                uriBuilder.Query = queryParams.ToString();
                var iframeUrl = uriBuilder.ToString();

                var responseObj = new
                {
                    success = true,
                    iframeUrl,
                    formUrl,
                    parentUrl = baseUrl,
                    amount,
                    currency
                };

                var masked = new
                {
                    responseObj.success,
                    iframeUrl = iframeUrl.Contains("checksum") ? "***" : iframeUrl,
                    responseObj.formUrl,
                    responseObj.parentUrl,
                    responseObj.amount,
                    responseObj.currency
                };

                await _commLog.LogAsync("nuvei-applepay-iframe-outbound", masked, "nuvei");

                return Json(responseObj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Apple Pay IFrame URL");
                await _commLog.LogAsync("nuvei-applepay-iframe-error", new
                {
                    provider = "Nuvei",
                    action = "GetApplePayIFrameUrl",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                }, "nuvei");
                return Json(new { success = false, error = ex.Message });
            }
        }

    [IgnoreAntiforgeryToken]
    [HttpPost("Callback")]
        [HttpGet("Callback")] // GET remains for diagnostics; Nuvei should POST
        public async Task<IActionResult> Callback()
        {
            _logger.LogInformation("Nuvei callback hit: method={Method} contentType={CT}", Request.Method, Request.ContentType);

            Dictionary<string,string> formData = new();
            if (HttpMethods.IsPost(Request.Method) && Request.HasFormContentType)
            {
                var posted = await Request.ReadFormAsync();
                foreach (var kv in posted)
                {
                    formData[kv.Key] = string.Join(",", kv.Value.ToArray());
                }
            }
            else if (Request.HasFormContentType) // fallback (shouldn't normally hit because of ReadFormAsync above)
            {
                foreach (var f in Request.Form)
                    formData[f.Key] = string.Join(",", f.Value.ToArray());
            }

            var query = Request.Query.ToDictionary(k => k.Key, v => string.Join(",", v.Value.ToArray()));

            // Redaction rules
            string Mask(string? v) => string.IsNullOrEmpty(v) ? v ?? string.Empty : v.Length <= 6 ? new string('*', v.Length) : v.Substring(0,3) + new string('*', v.Length-7) + v[^4..];
            bool ShouldMask(string key) => key.Contains("card", StringComparison.OrdinalIgnoreCase) ||
                                           key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                                           key.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                           key.Equals("uniqueCC", StringComparison.OrdinalIgnoreCase) ||
                                           key.Equals("PAR", StringComparison.OrdinalIgnoreCase);
            bool ShouldPartial(string key) => key.EndsWith("checksum", StringComparison.OrdinalIgnoreCase);

            var redacted = formData.ToDictionary(
                k => k.Key,
                v => ShouldMask(v.Key) ? Mask(v.Value) : ShouldPartial(v.Key) && v.Value.Length > 12 ? v.Value.Substring(0,6)+"..."+v.Value[^4..] : v.Value
            );

            // Attempt simplistic checksum verification if secret key present
            string? secretKey = _runtime.Get("Nuvei:secret_key") ?? _configuration["Nuvei:secret_key"]; // same naming as request side
            string? responseChecksum = formData.TryGetValue("responsechecksum", out var rc) ? rc : null;
            bool? checksumValid = null;
            if (!string.IsNullOrEmpty(secretKey) && !string.IsNullOrEmpty(responseChecksum))
            {
                try
                {
                    // Heuristic: concatenate all form values except any checksum fields
                    var sb = new StringBuilder();
                    foreach (var kv in formData.Where(kv => !kv.Key.EndsWith("checksum", StringComparison.OrdinalIgnoreCase)))
                        sb.Append(kv.Value);
                    var candidate = Sha256Hex(secretKey + sb.ToString());
                    checksumValid = string.Equals(candidate, responseChecksum, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nuvei response checksum verification failed internally");
                }
            }

            await _commLog.LogAsync("nuvei-callback", new {
                provider = "Nuvei",
                method = Request.Method,
                path = Request.Path.ToString(),
                formCount = formData.Count,
                checksumPresent = responseChecksum != null,
                checksumValid,
                form = redacted,
                query,
                headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
            }, "nuvei");
            // Extract Status and decide redirect
            formData.TryGetValue("Status", out var statusValue);
            string status = statusValue ?? string.Empty;

            // Helper to safely get value
            string GV(string k) => formData.TryGetValue(k, out var v) ? v : string.Empty;

            // Collect parameter sets
            // Expanded sets: include user requested fields for Success/Error view.
            // NOTE: Only values coming from callback formData are used (never from any Nuvei outbound response object).
            var successKeys = new [] {
                "ppp_status","LifeCycleId","currency","merchant_unique_id","merchant_site_id","merchant_id","merchantLocale","requestVersion",
                "PPP_TransactionID","productId","userid","customData","payment_method","responseTimeStamp","message","Error","userPaymentOptionId",
                "externalToken_cardExpiration","externalToken_cardMask","externalToken_extendedCardType","externalToken_Indication","externalToken_tokenValue",
                "Status","ClientUniqueID","ExErrCode","ErrCode","AuthCode","ReasonCode","Token","tokenId","responsechecksum","advanceResponseChecksum",
                "totalAmount","TransactionID","dynamicDescriptor","uniqueCC","eci","orderTransactionId",
                // existing display extras
                "cardBrand","issuerName"
            };
            var errorKeys = new [] {
                "Status","merchant_unique_id","errApmCode","errScCode","errApmDescription","errScDescription","Reason","ReasonCode","customData",
                "total_amount","currency","TransactionID","cardBrand","issuerName","ppp_status","LifeCycleId","merchant_site_id","merchant_id","PPP_TransactionID",
                "payment_method","responseTimeStamp","message","ExErrCode","ErrCode","AuthCode","Token","tokenId","responsechecksum","advanceResponseChecksum"
            };

            bool isSuccess = string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);
            bool isFailure = string.Equals(status, "DECLINED", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase);

            if (isSuccess || isFailure)
            {
                var keys = isSuccess ? successKeys : errorKeys;
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                foreach (var k in keys)
                {
                    var val = GV(k);
                    if (!string.IsNullOrEmpty(val)) q[k] = val;
                }
                string baseDest = isSuccess ? "/Nuvei/Success" : "/Nuvei/Error";
                var redirectUrl = baseDest + "?" + q.ToString();
                _logger.LogInformation("Nuvei redirecting callback to {Url} for status {Status}", redirectUrl, status);
                return Redirect(redirectUrl);
            }

            return Ok(new { status = "received", note = "No redirect performed (status=" + status + ")" });
        }

        [HttpGet("Success")] public IActionResult Success() => View();
        [HttpGet("Error")] public IActionResult Error() => View();
        [HttpGet("Pending")] public IActionResult Pending() => View();

    // Diagnostic: quick ping to verify base route reachable
    /// <summary>
    /// Initiates a Nuvei Simply Connect session by calling the /openOrder API.
    /// This endpoint prepares the payment session and returns a sessionToken for the frontend.
    /// </summary>
    [HttpPost("SimplyConnect/OpenOrder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenOrder([FromForm] decimal amount, [FromForm] string currency)
    {
        try
        {
            await _commLog.LogAsync("nuvei-simply-connect-inbound", new { 
                provider = "Nuvei Simply Connect", 
                action = "OpenOrder", 
                amount, 
                currency 
            }, "nuvei");

            // Generate a unique client ID for this transaction
            var clientUniqueId = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Random.Shared.Next(10000, 99999);

            // Call the Simply Connect service
            var simplyConnectService = HttpContext.RequestServices.GetRequiredService<NuveiSimplyConnectService>();
            var sessionResponse = await simplyConnectService.InitiateSessionAsync(amount, currency, clientUniqueId);

            if (sessionResponse == null || string.IsNullOrEmpty(sessionResponse.SessionToken))
            {
                _logger.LogError("Failed to initiate Nuvei Simply Connect session");
                return Json(new { success = false, error = "Failed to initiate payment session" });
            }

            // Store the sessionToken in the HTTP session for later use in GetPaymentStatus
            HttpContext.Session.SetString("Nuvei_SessionToken", sessionResponse.SessionToken);
            HttpContext.Session.SetString("Nuvei_ClientUniqueId", sessionResponse.ClientUniqueId);

            await _commLog.LogAsync("nuvei-simply-connect-session-created", new {
                provider = "Nuvei Simply Connect",
                clientUniqueId = sessionResponse.ClientUniqueId,
                orderId = sessionResponse.OrderId,
                amount,
                currency
            }, "nuvei");

            return Json(new
            {
                success = true,
                sessionToken = sessionResponse.SessionToken,
                orderId = sessionResponse.OrderId,
                clientUniqueId = sessionResponse.ClientUniqueId,
                merchantId = sessionResponse.MerchantId,
                merchantSiteId = sessionResponse.MerchantSiteId,
                amount = amount,
                currency = currency
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Nuvei Simply Connect session");
            await _commLog.LogAsync("nuvei-simply-connect-error", new {
                provider = "Nuvei Simply Connect",
                action = "OpenOrder",
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, "nuvei");
            return Json(new { success = false, error = "An error occurred while initiating the payment session" });
        }
    }

    /// <summary>
    /// Retrieves the payment status using the sessionToken stored in the HTTP session.
    /// Should be called after the Nuvei checkout UI closes/completes payment processing.
    /// </summary>
    [HttpPost("SimplyConnect/GetPaymentStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetPaymentStatus()
    {
        try
        {
            // Retrieve the sessionToken from the HTTP session
            var sessionToken = HttpContext.Session.GetString("Nuvei_SessionToken");
            var clientUniqueId = HttpContext.Session.GetString("Nuvei_ClientUniqueId");

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                _logger.LogWarning("GetPaymentStatus called but no sessionToken in session");
                return Json(new { success = false, error = "No active payment session found" });
            }

            _logger.LogInformation("GetPaymentStatus called, retrieving status for session");

            await _commLog.LogAsync("nuvei-get-payment-status-inbound", new { 
                provider = "Nuvei Simply Connect", 
                action = "GetPaymentStatus",
                clientUniqueId = clientUniqueId
            }, "nuvei");

            var simplyConnectService = HttpContext.RequestServices.GetRequiredService<NuveiSimplyConnectService>();
            var statusResponse = await simplyConnectService.GetPaymentStatusAsync(sessionToken);

            if (statusResponse == null)
            {
                _logger.LogError("Failed to get payment status from Nuvei");
                return Json(new { success = false, error = "Failed to retrieve payment status" });
            }

            _logger.LogInformation("Payment status retrieved: {TransactionStatus}", 
                statusResponse.TransactionStatus);

            await _commLog.LogAsync("nuvei-get-payment-status-retrieved", new {
                provider = "Nuvei Simply Connect",
                transactionStatus = statusResponse.TransactionStatus,
                transactionId = statusResponse.TransactionId,
                amount = statusResponse.Amount,
                currency = statusResponse.Currency,
                paymentMethod = statusResponse.PaymentMethod,
                transactionType = statusResponse.TransactionType
            }, "nuvei");

            // Clear the session tokens after successful retrieval
            HttpContext.Session.Remove("Nuvei_SessionToken");
            HttpContext.Session.Remove("Nuvei_ClientUniqueId");

            return Json(new
            {
                success = true,
                transactionStatus = statusResponse.TransactionStatus,
                transactionId = statusResponse.TransactionId,
                amount = statusResponse.Amount,
                currency = statusResponse.Currency,
                transactionType = statusResponse.TransactionType,
                paymentMethod = statusResponse.PaymentMethod,
                gwErrorCode = statusResponse.GwErrorCode,
                gwExtendedErrorCode = statusResponse.GwExtendedErrorCode,
                gwErrorReason = statusResponse.GwErrorReason,
                errCode = statusResponse.ErrCode,
                reason = statusResponse.Reason,
                clientUniqueId = statusResponse.ClientUniqueId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status");
            await _commLog.LogAsync("nuvei-get-payment-status-error", new {
                provider = "Nuvei Simply Connect",
                action = "GetPaymentStatus",
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, "nuvei");
            return Json(new { success = false, error = "An error occurred while retrieving payment status" });
        }
    }

    [HttpGet("")]
    public IActionResult Index() => Ok(new { status = "nuvei-controller-ok", time = DateTime.UtcNow });

        private string GetBaseUrl()
        {
            var request = _http.HttpContext!.Request;
            var scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && !string.IsNullOrEmpty(proto) ? proto.ToString() : request.Scheme;
            var forceHttps = Environment.GetEnvironmentVariable("REQUIRE_HTTPS_URLS");
            if (string.Equals(forceHttps, "true", StringComparison.OrdinalIgnoreCase) && !string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
                scheme = "https";
            return $"{scheme}://{request.Host}";
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }
    }
}
