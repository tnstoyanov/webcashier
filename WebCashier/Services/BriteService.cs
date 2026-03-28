using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebCashier.Models.Brite;

namespace WebCashier.Services
{
    public class BriteService : IBriteService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<BriteService> _logger;
        private readonly ICommLogService _commLog;
        private readonly IHttpClientFactory _httpClientFactory;
        private string? _apiUrl;
        private string? _publicKey;
        private string? _secret;

        public BriteService(
            IConfiguration config,
            ILogger<BriteService> logger,
            ICommLogService commLog,
            IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _commLog = commLog;
            _httpClientFactory = httpClientFactory;

            _apiUrl = _config["Brite:ApiUrl"] ?? "https://sandbox.britepaymentgroup.com";
            _publicKey = _config["Brite:PublicKey"];
            _secret = _config["Brite:Secret"];

            if (string.IsNullOrWhiteSpace(_publicKey) || string.IsNullOrWhiteSpace(_secret))
            {
                _logger.LogWarning("[Brite] Missing PublicKey or Secret configuration");
            }
        }

        /// <summary>
        /// Step 2: Get bearer token from Brite
        /// </summary>
        public async Task<BriteAuthResponse?> AuthorizeAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_publicKey) || string.IsNullOrWhiteSpace(_secret))
                {
                    _logger.LogError("[Brite] Missing API credentials");
                    await _commLog.LogAsync("brite-auth-error", new { error = "Missing credentials" }, "brite");
                    return null;
                }

                var authRequest = new BriteAuthRequest
                {
                    PublicKey = _publicKey,
                    Secret = _secret
                };

                var client = _httpClientFactory.CreateClient();
                var endpoint = $"{_apiUrl}/api/merchant.authorize";

                var jsonContent = JsonSerializer.Serialize(authRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("[Brite] Authorizing merchant");

                var response = await client.PostAsync(endpoint, httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                await _commLog.LogAsync("brite-authorize", new
                {
                    endpoint,
                    statusCode = (int)response.StatusCode
                }, "brite");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[Brite] Authorization failed: {StatusCode} - {Error}", response.StatusCode, responseContent);
                    await _commLog.LogAsync("brite-auth-error", new
                    {
                        statusCode = (int)response.StatusCode,
                        error = responseContent
                    }, "brite");
                    return null;
                }

                var authResponse = JsonSerializer.Deserialize<BriteAuthResponse>(responseContent);

                if (authResponse?.AccessToken == null)
                {
                    _logger.LogError("[Brite] Authorization response missing access token");
                    await _commLog.LogAsync("brite-auth-error", new { error = "Missing access token in response" }, "brite");
                    return null;
                }

                _logger.LogInformation("[Brite] Authorization successful");
                return authResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brite] Error in AuthorizeAsync");
                await _commLog.LogAsync("brite-auth-exception", new { error = ex.Message }, "brite");
                return null;
            }
        }

        /// <summary>
        /// Step 3: Create deposit session
        /// </summary>
        public async Task<BriteSessionResponse?> CreateDepositSessionAsync(
            string bearerToken,
            string paymentMethod,
            string countryId,
            decimal amount,
            string customerReference,
            string merchantReference,
            string customerEmail,
            string? customerFirstname = null,
            string? customerLastname = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(bearerToken))
                {
                    _logger.LogError("[Brite] Missing bearer token");
                    return null;
                }

                // Determine endpoint based on payment method
                var sessionEndpoint = paymentMethod == BritePaymentMethod.SwishPayment 
                    ? "session.create_swish_payment" 
                    : "session.create_deposit";

                // Send amount as-is (not multiplied by 100) - Brite expects the unit amount
                var amountValue = (long)amount;

                var sessionRequest = new BriteDepositSessionRequest
                {
                    CustomerEmail = customerEmail,
                    DeeplinkRedirect = _config["Brite:DeeplinkRedirect"] ?? _config["Brite:ReturnUrl"],
                    CountryId = countryId.ToLower(),
                    CustomerFirstname = customerFirstname ?? "Customer",
                    CustomerLastname = customerLastname ?? "User",
                    CustomerReference = customerReference,
                    MerchantReference = merchantReference,
                    CustomerDob = "1950-01-01", // Placeholder
                    Amount = amountValue,
                    ApprovalRequired = false,
                    CustomerAddress = new BriteCustomerAddress
                    {
                        City = "Anyplace",
                        Address = "123 Main St",
                        PostalCode = "00000",
                        CountryId = countryId.ToLower()
                    },
                    TransactionCallbackUrl = _config["Brite:WebhookUrl"],
                    SessionCallbackUrl = _config["Brite:WebhookUrl"]
                };

                var client = _httpClientFactory.CreateClient();
                var endpoint = $"{_apiUrl}/api/{sessionEndpoint}";

                var jsonContent = JsonSerializer.Serialize(sessionRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("[Brite] Creating {SessionType} session for {CountryId}", sessionEndpoint, countryId);

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = httpContent
                };
                request.Headers.Add("Authorization", $"Bearer {bearerToken}");

                var response = await client.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                await _commLog.LogAsync("brite-create-session", new
                {
                    endpoint,
                    paymentMethod,
                    countryId,
                    amount,
                    statusCode = (int)response.StatusCode
                }, "brite");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[Brite] Session creation failed: {StatusCode} - {Error}", response.StatusCode, responseContent);
                    await _commLog.LogAsync("brite-session-error", new
                    {
                        statusCode = (int)response.StatusCode,
                        error = responseContent
                    }, "brite");
                    
                    // Try to parse error response
                    var errorResponse = JsonSerializer.Deserialize<BriteSessionResponse>(responseContent);
                    if (errorResponse != null)
                    {
                        return errorResponse;
                    }
                    
                    return new BriteSessionResponse 
                    { 
                        ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}",
                        State = "APPLICATION_ERROR"
                    };
                }

                var sessionResponse = JsonSerializer.Deserialize<BriteSessionResponse>(responseContent);

                if (sessionResponse?.Id == null || sessionResponse?.Token == null)
                {
                    _logger.LogError("[Brite] Session creation returned invalid response");
                    return new BriteSessionResponse 
                    { 
                        ErrorMessage = "Invalid session response from Brite",
                        State = "APPLICATION_ERROR"
                    };
                }

                _logger.LogInformation("[Brite] Session created successfully: {SessionId}", sessionResponse.Id);
                return sessionResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brite] Error in CreateDepositSessionAsync");
                await _commLog.LogAsync("brite-session-exception", new { error = ex.Message, stackTrace = ex.StackTrace }, "brite");
                return new BriteSessionResponse 
                { 
                    ErrorMessage = $"Exception: {ex.Message}",
                    State = "APPLICATION_ERROR"
                };
            }
        }

        /// <summary>
        /// Step 5: Get session details after completion
        /// </summary>
        public async Task<BriteSessionDetails?> GetSessionDetailsAsync(string bearerToken, string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(bearerToken) || string.IsNullOrWhiteSpace(sessionId))
                {
                    _logger.LogError("[Brite] Missing bearer token or session ID");
                    return null;
                }

                var client = _httpClientFactory.CreateClient();
                var endpoint = $"{_apiUrl}/api/session.get";

                var requestBody = new { id = sessionId };
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("[Brite] Getting session details for {SessionId}", sessionId);

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = httpContent
                };
                request.Headers.Add("Authorization", $"Bearer {bearerToken}");

                var response = await client.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[Brite] Get session failed: {StatusCode}", response.StatusCode);
                    await _commLog.LogAsync("brite-get-session-error", new
                    {
                        sessionId,
                        statusCode = (int)response.StatusCode
                    }, "brite");
                    return null;
                }

                var sessionDetails = JsonSerializer.Deserialize<BriteSessionDetails>(responseContent);

                if (sessionDetails?.Id == null)
                {
                    _logger.LogError("[Brite] Session details response invalid");
                    return null;
                }

                _logger.LogInformation("[Brite] Session details retrieved: State={State}, Amount={Amount}", sessionDetails.State, sessionDetails.Amount);
                return sessionDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brite] Error in GetSessionDetailsAsync");
                await _commLog.LogAsync("brite-get-session-exception", new { error = ex.Message }, "brite");
                return null;
            }
        }

        public async Task<BriteTransactionResponse?> CreatePaymentAsync(
            decimal amount,
            string currency,
            string paymentMethod,
            string countryId,
            string description,
            string merchantReference,
            string? customerReference = null)
        {
            // This is kept for backwards compatibility but not used in the new flow
            return null;
        }

        public async Task<BriteTransactionResponse?> GetTransactionAsync(string transactionId)
        {
            // Not implemented for Brite
            return null;
        }

        public async Task<BriteTransactionResponse?> RefundTransactionAsync(string transactionId, decimal? amount = null)
        {
            // Not implemented for Brite in this version
            return null;
        }

        public async Task<BriteTransactionResponse?> CaptureTransactionAsync(string transactionId)
        {
            // Not implemented for Brite
            return null;
        }

        public bool VerifyWebhookSignature(string payload, string signature)
        {
            // Brite uses HMAC-SHA256, but specific implementation depends on their webhook format
            // For now, return true - implement based on actual webhook requirements
            return true;
        }
    }
}
