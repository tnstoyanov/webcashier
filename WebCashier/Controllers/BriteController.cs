using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    [Route("Brite")]
    [ApiController]
    public class BriteController : Controller
    {
        private readonly IBriteService _briteService;
        private readonly ILogger<BriteController> _logger;
        private readonly ICommLogService _commLog;

        public BriteController(
            IBriteService briteService,
            ILogger<BriteController> logger,
            ICommLogService commLog)
        {
            _briteService = briteService;
            _logger = logger;
            _commLog = commLog;
        }

        /// <summary>
        /// Step 2: Get authorization token from Brite
        /// GET /Brite/Authorize
        /// </summary>
        [HttpGet("Authorize")]
        public async Task<IActionResult> Authorize()
        {
            try
            {
                await _commLog.LogAsync("brite-inbound", new
                {
                    provider = "Brite",
                    action = "Authorize"
                }, "brite");

                var authResponse = await _briteService.AuthorizeAsync();

                if (authResponse == null)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Authorization failed"
                    });
                }

                if (!string.IsNullOrWhiteSpace(authResponse.ErrorMessage))
                {
                    return Json(new
                    {
                        success = false,
                        error = authResponse.ErrorMessage,
                        errorName = authResponse.ErrorName
                    });
                }

                if (string.IsNullOrWhiteSpace(authResponse.AccessToken))
                {
                    return Json(new
                    {
                        success = false,
                        error = "No access token in response"
                    });
                }

                _logger.LogInformation("[Brite] Authorization successful");

                return Json(new
                {
                    success = true,
                    accessToken = authResponse.AccessToken,
                    expires = authResponse.Expires,
                    refreshToken = authResponse.RefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brite] Error in Authorize action");
                await _commLog.LogAsync("brite-authorize-exception", new { error = ex.Message }, "brite");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Step 3: Create deposit session
        /// POST /Brite/CreateSession
        /// </summary>
        [HttpPost("CreateSession")]
        public async Task<IActionResult> CreateSession(
            [FromForm] string bearerToken,
            [FromForm] decimal amount,
            [FromForm] string countryId,
            [FromForm] string paymentMethod = "session.create_deposit",
            [FromForm] string? customerEmail = null,
            [FromForm] string? customerFirstname = null,
            [FromForm] string? customerLastname = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(bearerToken) || string.IsNullOrWhiteSpace(countryId))
                {
                    return Json(new { success = false, error = "Missing required parameters" });
                }

                var customerReference = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + Random.Shared.Next(1000000, 9999999);
                var merchantReference = customerReference;

                await _commLog.LogAsync("brite-inbound", new
                {
                    provider = "Brite",
                    action = "CreateSession",
                    amount,
                    countryId,
                    paymentMethod,
                    customerReference
                }, "brite");

                var sessionResponse = await _briteService.CreateDepositSessionAsync(
                    bearerToken,
                    paymentMethod,
                    countryId,
                    amount,
                    customerReference,
                    merchantReference,
                    customerEmail ?? "customer@example.com",
                    customerFirstname,
                    customerLastname);

                if (sessionResponse == null)
                {
                    return Json(new { success = false, error = "Failed to create session - null response" });
                }

                if (!string.IsNullOrWhiteSpace(sessionResponse.ErrorMessage))
                {
                    _logger.LogWarning("[Brite] Session creation returned error: {ErrorMessage}", sessionResponse.ErrorMessage);
                    return Json(new
                    {
                        success = false,
                        error = sessionResponse.ErrorMessage,
                        errorName = sessionResponse.ErrorName,
                        state = sessionResponse.State
                    });
                }

                if (string.IsNullOrWhiteSpace(sessionResponse.Id) || string.IsNullOrWhiteSpace(sessionResponse.Token))
                {
                    _logger.LogError("[Brite] Session response missing required fields");
                    return Json(new { 
                        success = false, 
                        error = "Invalid session response - missing id or token",
                        receivedId = sessionResponse.Id,
                        receivedToken = sessionResponse.Token
                    });
                }

                _logger.LogInformation("[Brite] Session created: {SessionId}", sessionResponse.Id);

                return Json(new
                {
                    success = true,
                    sessionId = sessionResponse.Id,
                    token = sessionResponse.Token,
                    customerReference,
                    merchantReference,
                    amount,
                    countryId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brite] Error in CreateSession action");
                await _commLog.LogAsync("brite-create-session-exception", new { error = ex.Message }, "brite");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Step 5: Get session details (called after client completes)
        /// POST /Brite/SessionDetails
        /// </summary>
        [HttpPost("SessionDetails")]
        public async Task<IActionResult> SessionDetails(
            [FromForm] string bearerToken,
            [FromForm] string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(bearerToken) || string.IsNullOrWhiteSpace(sessionId))
                {
                    return Json(new { success = false, error = "Missing parameters" });
                }

                await _commLog.LogAsync("brite-inbound", new
                {
                    provider = "Brite",
                    action = "SessionDetails",
                    sessionId
                }, "brite");

                var details = await _briteService.GetSessionDetailsAsync(bearerToken, sessionId);

                if (details == null)
                {
                    return Json(new { success = false, error = "Failed to get session details" });
                }

                if (details.State == null)
                {
                    return Json(new { success = false, error = "Invalid session state" });
                }

                _logger.LogInformation("[Brite] Session details retrieved: State={State}", details.State);

                return Json(new
                {
                    success = true,
                    sessionId = details.Id,
                    state = details.State,
                    amount = details.Amount,
                    currency = details.CurrencyId,
                    merchantReference = details.MerchantReference,
                    customerReference = details.CustomerReference,
                    transactionId = details.TransactionId,
                    bankId = details.Bank?.Id,
                    bankName = details.Bank?.Name,
                    bankAccountBban = details.BankAccount?.Bban,
                    bankAccountHolder = details.BankAccount?.Holder,
                    created = details.Created,
                    completed = details.Completed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brite] Error in SessionDetails action");
                await _commLog.LogAsync("brite-session-details-exception", new { error = ex.Message }, "brite");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Webhook endpoint for transaction notifications
        /// POST /Brite/Webhook
        /// </summary>
        [HttpPost("Webhook")]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                using (var reader = new StreamReader(Request.Body))
                {
                    var payload = await reader.ReadToEndAsync();

                    await _commLog.LogAsync("brite-webhook-received", new
                    {
                        payload = payload.Length > 500 ? payload.Substring(0, 500) : payload,
                        contentType = Request.ContentType
                    }, "brite");

                    _logger.LogInformation("[Brite] Webhook received: {PayloadSize} bytes", payload.Length);

                    // TODO: Parse and process webhook events (transaction.completed, transaction.failed, etc.)
                    // For now, just acknowledge receipt
                    return Ok(new { success = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brite] Error processing webhook");
                await _commLog.LogAsync("brite-webhook-exception", new { error = ex.Message }, "brite");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
