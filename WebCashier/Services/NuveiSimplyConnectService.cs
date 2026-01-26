using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WebCashier.Services
{
    /// <summary>
    /// Handles Nuvei Simply Connect (SafeCharge) payment processing.
    /// Simply Connect uses the /openOrder API to initiate a session and the checkout() method for payment UI.
    /// </summary>
    public class NuveiSimplyConnectService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NuveiSimplyConnectService> _logger;
        private readonly ICommLogService _commLog;
        private readonly IRuntimeConfigStore _runtime;
        private const string OpenOrderEndpoint = "https://ppp-test.nuvei.com/ppp/api/v1/openOrder.do";
        private const string OpenOrderProdEndpoint = "https://ppp.nuvei.com/ppp/api/v1/openOrder.do";
        private const string GetPaymentStatusEndpoint = "https://ppp-test.nuvei.com/ppp/api/v1/getPaymentStatus.do";
        private const string GetPaymentStatusProdEndpoint = "https://ppp.nuvei.com/ppp/api/v1/getPaymentStatus.do";

        public NuveiSimplyConnectService(
            IConfiguration config,
            ILogger<NuveiSimplyConnectService> logger,
            ICommLogService commLog,
            IRuntimeConfigStore runtime)
        {
            _config = config;
            _logger = logger;
            _commLog = commLog;
            _runtime = runtime;
        }

        /// <summary>
        /// Initiates a session by calling the /openOrder API.
        /// This must be done on the backend to keep the secret key safe.
        /// </summary>
        public async Task<OpenOrderResponse?> InitiateSessionAsync(decimal amount, string currency, string clientUniqueId)
        {
            try
            {
                var merchantId = Get("Nuvei:merchant_id");
                var merchantSiteId = Get("Nuvei:merchant_site_id");
                var secretKey = Get("Nuvei:secret_key");
                var environment = Get("Nuvei:environment") ?? "test"; // test or prod

                if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(merchantSiteId) || string.IsNullOrWhiteSpace(secretKey))
                {
                    _logger.LogError("Nuvei Simply Connect configuration incomplete");
                    throw new InvalidOperationException("Nuvei configuration incomplete");
                }

                var endpoint = environment.Equals("prod", StringComparison.OrdinalIgnoreCase) 
                    ? OpenOrderProdEndpoint 
                    : OpenOrderEndpoint;

                var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var amountStr = amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                // Build the request payload
                var request = new
                {
                    merchantId = merchantId,
                    merchantSiteId = merchantSiteId,
                    clientUniqueId = clientUniqueId,
                    currency = currency,
                    amount = amountStr,
                    timeStamp = timeStamp
                };

                // Calculate checksum: SHA256(merchantId + merchantSiteId + amount + currency + timeStamp + secretKey)
                var toHash = merchantId + merchantSiteId + amountStr + currency + timeStamp + secretKey;
                var checksum = Sha256Hex(toHash);

                // Create final request with checksum
                var requestWithChecksum = new
                {
                    merchantId = merchantId,
                    merchantSiteId = merchantSiteId,
                    clientUniqueId = clientUniqueId,
                    currency = currency,
                    amount = amountStr,
                    timeStamp = timeStamp,
                    checksum = checksum
                };

                // Log outbound request
                await _commLog.LogAsync("nuvei-simply-connect-outbound", new
                {
                    provider = "Nuvei Simply Connect",
                    action = "openOrder",
                    endpoint = endpoint,
                    merchantId = merchantId,
                    merchantSiteId = merchantSiteId,
                    clientUniqueId = clientUniqueId,
                    currency = currency,
                    amount = amountStr,
                    timeStamp = timeStamp,
                    checksum = checksum
                }, "nuvei");

                // Send request to Nuvei
                using var httpClient = new HttpClient();
                var jsonContent = JsonSerializer.Serialize(requestWithChecksum);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Log the complete request payload
                _logger.LogInformation("Nuvei Simply Connect openOrder request payload: {Payload}", jsonContent);
                _logger.LogInformation("Sending Nuvei Simply Connect openOrder request to {Endpoint}", endpoint);
                
                var response = await httpClient.PostAsync(endpoint, content);
                
                // Log response status
                _logger.LogInformation("Nuvei Simply Connect openOrder response status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Nuvei Simply Connect openOrder failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                    await _commLog.LogAsync("nuvei-simply-connect-error", new
                    {
                        provider = "Nuvei Simply Connect",
                        action = "openOrder",
                        statusCode = response.StatusCode,
                        errorBody = errorBody
                    }, "nuvei");
                    return null;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

                // Log successful response
                var logResponse = new
                {
                    provider = "Nuvei Simply Connect",
                    action = "openOrder",
                    status = jsonResponse.TryGetProperty("status", out var status) ? status.GetString() : null,
                    sessionToken = jsonResponse.TryGetProperty("sessionToken", out var token) ? "***[masked]***" : null,
                    orderId = jsonResponse.TryGetProperty("orderId", out var orderId) ? orderId.GetInt64() : (long?)null
                };
                await _commLog.LogAsync("nuvei-simply-connect-response", logResponse, "nuvei");

                // Parse response
                if (jsonResponse.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "SUCCESS")
                {
                    return new OpenOrderResponse
                    {
                        Status = statusProp.GetString(),
                        SessionToken = jsonResponse.TryGetProperty("sessionToken", out var sessionToken) ? sessionToken.GetString() : null,
                        OrderId = jsonResponse.TryGetProperty("orderId", out var orderIdProp) ? orderIdProp.GetInt64() : 0,
                        ClientUniqueId = clientUniqueId,
                        MerchantId = merchantId,
                        MerchantSiteId = merchantSiteId
                    };
                }

                _logger.LogWarning("Nuvei Simply Connect openOrder returned non-SUCCESS status: {Status}", 
                    jsonResponse.TryGetProperty("status", out var failStatus) ? failStatus.GetString() : "unknown");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nuvei Simply Connect openOrder API call failed");
                await _commLog.LogAsync("nuvei-simply-connect-exception", new
                {
                    provider = "Nuvei Simply Connect",
                    action = "openOrder",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                }, "nuvei");
                return null;
            }
        }

        private string? Get(string key) => _runtime.Get(key) ?? _config[key];

        /// <summary>
        /// Gets the payment status for a transaction using clientUniqueId
        /// </summary>
        public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(string clientUniqueId)
        {
            try
            {
                var merchantId = Get("Nuvei:merchant_id");
                var merchantSiteId = Get("Nuvei:merchant_site_id");
                var secretKey = Get("Nuvei:secret_key");
                var environment = Get("Nuvei:environment") ?? "test";

                if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(merchantSiteId) || string.IsNullOrWhiteSpace(secretKey))
                {
                    _logger.LogError("Nuvei Simply Connect configuration incomplete for getPaymentStatus");
                    throw new InvalidOperationException("Nuvei configuration incomplete");
                }

                var endpoint = environment.Equals("prod", StringComparison.OrdinalIgnoreCase) 
                    ? GetPaymentStatusProdEndpoint 
                    : GetPaymentStatusEndpoint;

                var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

                // Build the request payload
                var request = new
                {
                    merchantId = merchantId,
                    merchantSiteId = merchantSiteId,
                    clientUniqueId = clientUniqueId,
                    timeStamp = timeStamp
                };

                // Calculate checksum: SHA256(merchantId + merchantSiteId + clientUniqueId + timeStamp + secretKey)
                var toHash = merchantId + merchantSiteId + clientUniqueId + timeStamp + secretKey;
                var checksum = Sha256Hex(toHash);

                // Create final request with checksum
                var requestWithChecksum = new
                {
                    merchantId = merchantId,
                    merchantSiteId = merchantSiteId,
                    clientUniqueId = clientUniqueId,
                    timeStamp = timeStamp,
                    checksum = checksum
                };

                // Log outbound request
                _logger.LogInformation("Calling Nuvei getPaymentStatus for clientUniqueId: {ClientUniqueId}", clientUniqueId);
                await _commLog.LogAsync("nuvei-get-payment-status-outbound", new
                {
                    provider = "Nuvei Simply Connect",
                    action = "getPaymentStatus",
                    endpoint = endpoint,
                    merchantId = merchantId,
                    merchantSiteId = merchantSiteId,
                    clientUniqueId = clientUniqueId,
                    timeStamp = timeStamp,
                    checksum = checksum
                }, "nuvei");

                // Send request to Nuvei
                using var httpClient = new HttpClient();
                var jsonContent = JsonSerializer.Serialize(requestWithChecksum);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Nuvei getPaymentStatus request payload: {Payload}", jsonContent);
                
                var response = await httpClient.PostAsync(endpoint, content);
                
                _logger.LogInformation("Nuvei getPaymentStatus response status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Nuvei getPaymentStatus failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                    await _commLog.LogAsync("nuvei-get-payment-status-error", new
                    {
                        provider = "Nuvei Simply Connect",
                        action = "getPaymentStatus",
                        statusCode = response.StatusCode,
                        errorBody = errorBody
                    }, "nuvei");
                    return null;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Nuvei getPaymentStatus response body: {ResponseBody}", responseBody);
                
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

                // Log successful response
                await _commLog.LogAsync("nuvei-get-payment-status-response", new
                {
                    provider = "Nuvei Simply Connect",
                    action = "getPaymentStatus",
                    transactionStatus = jsonResponse.TryGetProperty("transactionStatus", out var status) ? status.GetString() : null,
                    transactionId = jsonResponse.TryGetProperty("transactionId", out var txId) ? txId.GetString() : null,
                    errorCode = jsonResponse.TryGetProperty("errorCode", out var errCode) ? errCode.GetString() : null
                }, "nuvei");

                // Parse and return the full response
                var statusResponse = new PaymentStatusResponse();
                
                if (jsonResponse.TryGetProperty("transactionStatus", out var statusProp))
                    statusResponse.TransactionStatus = statusProp.GetString();
                if (jsonResponse.TryGetProperty("gwExtendedErrorCode", out var gwErr))
                    statusResponse.GwExtendedErrorCode = gwErr.GetInt32();
                if (jsonResponse.TryGetProperty("errorCode", out var errCodeProp))
                    statusResponse.ErrorCode = errCodeProp.GetInt32();
                if (jsonResponse.TryGetProperty("reason", out var reasonProp))
                    statusResponse.Reason = reasonProp.GetString();
                if (jsonResponse.TryGetProperty("authCode", out var authProp))
                    statusResponse.AuthCode = authProp.GetString();
                if (jsonResponse.TryGetProperty("clientRequestId", out var clientReqProp))
                    statusResponse.ClientRequestId = clientReqProp.GetString();
                if (jsonResponse.TryGetProperty("internalRequestId", out var internalReqProp))
                    statusResponse.InternalRequestId = internalReqProp.GetInt64();
                if (jsonResponse.TryGetProperty("version", out var verProp))
                    statusResponse.Version = verProp.GetString();
                if (jsonResponse.TryGetProperty("transactionId", out var txIdProp))
                    statusResponse.TransactionId = txIdProp.GetString();
                if (jsonResponse.TryGetProperty("amount", out var amountProp))
                    statusResponse.Amount = amountProp.GetString();
                if (jsonResponse.TryGetProperty("currency", out var currencyProp))
                    statusResponse.Currency = currencyProp.GetString();
                if (jsonResponse.TryGetProperty("merchantId", out var merIdProp))
                    statusResponse.MerchantId = merIdProp.GetString();
                if (jsonResponse.TryGetProperty("merchantSiteId", out var merSiteIdProp))
                    statusResponse.MerchantSiteId = merSiteIdProp.GetString();
                if (jsonResponse.TryGetProperty("transactionType", out var txTypeProp))
                    statusResponse.TransactionType = txTypeProp.GetString();
                if (jsonResponse.TryGetProperty("clientUniqueId", out var clientUniqueProp))
                    statusResponse.ClientUniqueId = clientUniqueProp.GetString();
                if (jsonResponse.TryGetProperty("errCode", out var errCodeIntProp))
                    statusResponse.ErrCode = errCodeIntProp.GetInt32();
                if (jsonResponse.TryGetProperty("status", out var statusStrProp))
                    statusResponse.Status = statusStrProp.GetString();

                return statusResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nuvei getPaymentStatus API call failed");
                await _commLog.LogAsync("nuvei-get-payment-status-exception", new
                {
                    provider = "Nuvei Simply Connect",
                    action = "getPaymentStatus",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                }, "nuvei");
                return null;
            }
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }
    }

    public class OpenOrderResponse
    {
        public string? Status { get; set; }
        public string? SessionToken { get; set; }
        public long OrderId { get; set; }
        public string? ClientUniqueId { get; set; }
        public string? MerchantId { get; set; }
        public string? MerchantSiteId { get; set; }
    }

    public class PaymentStatusResponse
    {
        public string? TransactionStatus { get; set; }
        public int GwExtendedErrorCode { get; set; }
        public int ErrorCode { get; set; }
        public string? Reason { get; set; }
        public string? AuthCode { get; set; }
        public string? ClientRequestId { get; set; }
        public long InternalRequestId { get; set; }
        public string? Version { get; set; }
        public string? TransactionId { get; set; }
        public string? Amount { get; set; }
        public string? Currency { get; set; }
        public string? MerchantId { get; set; }
        public string? MerchantSiteId { get; set; }
        public string? TransactionType { get; set; }
        public string? ClientUniqueId { get; set; }
        public int ErrCode { get; set; }
        public string? Status { get; set; }
    }
}
