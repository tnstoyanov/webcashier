using System.Text;
using System.Text.Json;
using WebCashier.Models.PayPal;

namespace WebCashier.Services
{
    public class PayPalService : IPayPalService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PayPalService> _logger;
        private readonly ICommLogService _commLog;
        private readonly IHttpClientFactory _httpClientFactory;
        private string? _cachedAccessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public PayPalService(
            IConfiguration config,
            ILogger<PayPalService> logger,
            ICommLogService commLog,
            IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _commLog = commLog;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            // Return cached token if still valid
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
            {
                _logger.LogInformation("[PayPal] Using cached access token");
                return _cachedAccessToken;
            }

            var apiUrl = _config["PayPal:ApiUrl"] ?? "https://api.sandbox.paypal.com";
            var clientId = _config["PayPal:ClientId"];
            var clientSecret = _config["PayPal:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogError("[PayPal] Missing ClientId or ClientSecret configuration");
                await _commLog.LogAsync("paypal-oauth-error", new { error = "Missing configuration" }, "paypal");
                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                var tokenUrl = $"{apiUrl}/v1/oauth2/token";
                _logger.LogInformation("[PayPal] Requesting access token from {Url}", tokenUrl);

                var response = await client.PostAsync(tokenUrl, content);

                await _commLog.LogAsync("paypal-oauth-request", new
                {
                    endpoint = tokenUrl,
                    statusCode = (int)response.StatusCode
                }, "paypal");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[PayPal] OAuth token request failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    await _commLog.LogAsync("paypal-oauth-error", new
                    {
                        statusCode = (int)response.StatusCode,
                        error = errorContent
                    }, "paypal");
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var oauthResponse = JsonSerializer.Deserialize<PayPalOAuthResponse>(jsonContent);

                if (oauthResponse?.AccessToken == null)
                {
                    _logger.LogError("[PayPal] OAuth response missing access token");
                    await _commLog.LogAsync("paypal-oauth-error", new { error = "Missing access token in response" }, "paypal");
                    return null;
                }

                _cachedAccessToken = oauthResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(oauthResponse.ExpiresIn);

                _logger.LogInformation("[PayPal] Access token obtained successfully, expires in {ExpiresIn}s", oauthResponse.ExpiresIn);
                await _commLog.LogAsync("paypal-oauth-success", new
                {
                    tokenType = oauthResponse.TokenType,
                    expiresIn = oauthResponse.ExpiresIn
                }, "paypal");

                return _cachedAccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error obtaining access token");
                await _commLog.LogAsync("paypal-oauth-exception", new { error = ex.Message }, "paypal");
                return null;
            }
        }

        public async Task<PayPalOrderResponse?> CreateOrderAsync(decimal amount, string currency, string description, string referenceId)
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("[PayPal] Cannot create order: no access token");
                return null;
            }

            var apiUrl = _config["PayPal:ApiUrl"] ?? "https://api.sandbox.paypal.com";
            var merchantId = _config["PayPal:MerchantId"];
            var brandName = _config["PayPal:BrandName"] ?? "Finansero";
            var returnUrl = _config["PayPal:ReturnUrl"] ?? "https://webcashier.onrender.com/PayPal/Return";
            var cancelUrl = _config["PayPal:CancelUrl"] ?? "https://webcashier.onrender.com/PayPal/Cancel";

            try
            {
                var orderRequest = new PayPalOrderRequest
                {
                    Intent = "CAPTURE",
                    PurchaseUnits = new List<PurchaseUnit>
                    {
                        new PurchaseUnit
                        {
                            ReferenceId = referenceId,
                            Amount = new Amount
                            {
                                CurrencyCode = currency.ToUpper(),
                                Value = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                            },
                            Payee = new Payee { MerchantId = merchantId },
                            Description = description
                        }
                    },
                    PaymentSource = new PaymentSource
                    {
                        PayPal = new PayPalPaymentSource
                        {
                            ExperienceContext = new ExperienceContext
                            {
                                BrandName = brandName,
                                UserAction = "PAY_NOW",
                                ShippingPreference = "NO_SHIPPING",
                                Locale = "en-US",
                                ReturnUrl = returnUrl,
                                CancelUrl = cancelUrl
                            }
                        }
                    }
                };

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                client.DefaultRequestHeaders.Add("Prefer", "return=representation");
                client.DefaultRequestHeaders.Add("PayPal-Request-Id", Guid.NewGuid().ToString());

                var jsonContent = JsonSerializer.Serialize(orderRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var ordersUrl = $"{apiUrl}/v2/checkout/orders";
                _logger.LogInformation("[PayPal] Creating order: {Amount} {Currency}", amount, currency);

                var response = await client.PostAsync(ordersUrl, content);

                await _commLog.LogAsync("paypal-order-create", new
                {
                    endpoint = ordersUrl,
                    statusCode = (int)response.StatusCode,
                    amount,
                    currency,
                    referenceId
                }, "paypal");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[PayPal] Order creation failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    await _commLog.LogAsync("paypal-order-error", new
                    {
                        statusCode = (int)response.StatusCode,
                        error = errorContent
                    }, "paypal");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var orderResponse = JsonSerializer.Deserialize<PayPalOrderResponse>(responseContent);

                if (orderResponse?.Id != null)
                {
                    _logger.LogInformation("[PayPal] Order created successfully: {OrderId}", orderResponse.Id);
                    await _commLog.LogAsync("paypal-order-created", new
                    {
                        orderId = orderResponse.Id,
                        status = orderResponse.Status,
                        referenceId
                    }, "paypal");
                }

                return orderResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error creating order");
                await _commLog.LogAsync("paypal-order-exception", new { error = ex.Message }, "paypal");
                return null;
            }
        }

        public async Task<PayPalOrderResponse?> CaptureOrderAsync(string orderId)
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("[PayPal] Cannot capture order: no access token");
                return null;
            }

            var apiUrl = _config["PayPal:ApiUrl"] ?? "https://api.sandbox.paypal.com";

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                client.DefaultRequestHeaders.Add("Prefer", "return=representation");
                client.DefaultRequestHeaders.Add("PayPal-Request-Id", Guid.NewGuid().ToString());

                var captureUrl = $"{apiUrl}/v2/checkout/orders/{orderId}/capture";
                _logger.LogInformation("[PayPal] Capturing order: {OrderId}", orderId);

                var content = new StringContent("{}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync(captureUrl, content);

                await _commLog.LogAsync("paypal-order-capture", new
                {
                    endpoint = captureUrl,
                    orderId,
                    statusCode = (int)response.StatusCode
                }, "paypal");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[PayPal] Order capture failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    await _commLog.LogAsync("paypal-capture-error", new
                    {
                        orderId,
                        statusCode = (int)response.StatusCode,
                        error = errorContent
                    }, "paypal");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var captureResponse = JsonSerializer.Deserialize<PayPalOrderResponse>(responseContent);

                if (captureResponse?.Id != null)
                {
                    _logger.LogInformation("[PayPal] Order captured successfully: {OrderId}, Status: {Status}", 
                        captureResponse.Id, captureResponse.Status);
                    await _commLog.LogAsync("paypal-captured", new
                    {
                        orderId = captureResponse.Id,
                        status = captureResponse.Status
                    }, "paypal");
                }

                return captureResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error capturing order");
                await _commLog.LogAsync("paypal-capture-exception", new { orderId, error = ex.Message }, "paypal");
                return null;
            }
        }

        public async Task<PayPalOrderResponse?> GetOrderAsync(string orderId)
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("[PayPal] Cannot get order: no access token");
                return null;
            }

            var apiUrl = _config["PayPal:ApiUrl"] ?? "https://api.sandbox.paypal.com";

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var getUrl = $"{apiUrl}/v2/checkout/orders/{orderId}";
                _logger.LogInformation("[PayPal] Retrieving order: {OrderId}", orderId);

                var response = await client.GetAsync(getUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[PayPal] Get order failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    await _commLog.LogAsync("paypal-get-error", new
                    {
                        orderId,
                        statusCode = (int)response.StatusCode,
                        error = errorContent
                    }, "paypal");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var orderResponse = JsonSerializer.Deserialize<PayPalOrderResponse>(responseContent);

                _logger.LogInformation("[PayPal] Order retrieved: {OrderId}, Status: {Status}", orderId, orderResponse?.Status);
                await _commLog.LogAsync("paypal-order-retrieved", new
                {
                    orderId,
                    status = orderResponse?.Status
                }, "paypal");

                return orderResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayPal] Error retrieving order");
                await _commLog.LogAsync("paypal-get-exception", new { orderId, error = ex.Message }, "paypal");
                return null;
            }
        }
    }
}
