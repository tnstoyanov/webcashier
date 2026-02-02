using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WebCashier.Services;

/// <summary>
/// Service for JM Financial Hosted Payment Page (HPP) API integration.
/// Handles payment session creation with hash calculation and API communication.
/// </summary>
public class JMFService : IJMFService
{
    private readonly IConfiguration _config;
    private readonly ILogger<JMFService> _logger;
    private readonly ICommLogService _commLog;
    private readonly IHttpClientFactory _httpClientFactory;

    public JMFService(
        IConfiguration config,
        ILogger<JMFService> logger,
        ICommLogService commLog,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _commLog = commLog;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JMFPaymentResponse?> CreatePaymentSessionAsync(
        decimal amount,
        string currency,
        string customerName,
        string customerEmail)
    {
        try
        {
            _logger.LogInformation("[JMF] Creating payment session for amount={Amount} {Currency}", amount, currency);

            await _commLog.LogAsync("jmf-create-session-inbound", new
            {
                provider = "JM Financial",
                amount,
                currency,
                customerName,
                customerEmail
            }, "jmf");

            // Get configuration
            var merchantKey = _config["JMF:MerchantKey"];
            var apiPassword = _config["JMF:ApiPassword"];
            var apiEndpoint = _config["JMF:ApiEndpoint"] ?? "https://checkout.jmfinancialkw.com/api/v1/session";

            if (string.IsNullOrWhiteSpace(merchantKey) || string.IsNullOrWhiteSpace(apiPassword))
            {
                _logger.LogError("[JMF] Missing MerchantKey or ApiPassword configuration");
                return new JMFPaymentResponse
                {
                    Status = 400,
                    Error = "Missing JMF configuration"
                };
            }

            // Generate order number
            var orderNumber = GenerateOrderNumber();

            // Create order object
            var order = new
            {
                number = orderNumber,
                amount = amount.ToString("F2"),
                currency,
                description = "Payment via WebCashier"
            };

            // Calculate hash
            var hash = CalculateHash(order, apiPassword);

            // Build payload
            var payload = new
            {
                merchant_key = merchantKey,
                operation = "purchase",
                order = new
                {
                    number = order.number,
                    amount = order.amount,
                    currency = order.currency,
                    description = order.description
                },
                session_expiry = "15",
                cancel_url = GetBaseUrl() + "/Payment?paymentMethod=jmf",
                success_url = GetBaseUrl() + "/JMF/Success",
                customer = new
                {
                    name = customerName,
                    email = customerEmail,
                    birth_date = "1980-01-01"
                },
                billing_address = new
                {
                    country = "AE",
                    city = "Dubai",
                    address = "Street",
                    zip = "00000",
                    phone = "+971501234567"
                },
                hash
            };

            _logger.LogInformation("[JMF] Sending request to {Endpoint}", apiEndpoint);

            var client = _httpClientFactory.CreateClient();
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(apiEndpoint, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("[JMF] API Response Status: {StatusCode}", response.StatusCode);

            // Log the response (without sensitive data)
            await _commLog.LogAsync("jmf-api-response", new
            {
                statusCode = (int)response.StatusCode,
                hasContent = !string.IsNullOrWhiteSpace(responseContent),
                contentLength = responseContent?.Length ?? 0
            }, "jmf");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[JMF] API request failed with status {StatusCode}: {Content}", response.StatusCode, responseContent);
                return new JMFPaymentResponse
                {
                    Status = (int)response.StatusCode,
                    Error = $"API request failed: {response.StatusCode}"
                };
            }

            try
            {
                var result = JsonSerializer.Deserialize<JMFPaymentResponse>(responseContent ?? "{}");
                
                if (result?.Response?.RedirectUrl != null)
                {
                    _logger.LogInformation("[JMF] Payment session created successfully. Order: {OrderNumber}", orderNumber);
                    return result;
                }
                else
                {
                    _logger.LogError("[JMF] Response missing redirect URL");
                    return result ?? new JMFPaymentResponse
                    {
                        Status = 400,
                        Error = "Invalid response format from JMF API"
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[JMF] Failed to parse API response: {Content}", responseContent);
                return new JMFPaymentResponse
                {
                    Status = 500,
                    Error = "Failed to parse API response"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JMF] Exception creating payment session");
            await _commLog.LogAsync("jmf-create-session-error", new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, "jmf");

            return new JMFPaymentResponse
            {
                Status = 500,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Calculates the hash required by JM Financial API.
    /// Hash = SHA1(MD5(uppercase(number + amount + currency + description + password)))
    /// </summary>
    private string CalculateHash(dynamic order, string password)
    {
        try
        {
            // Concatenate values
            var toHash = order.number + order.amount + order.currency + order.description + password;

            // Convert to uppercase and calculate MD5
            var md5Hash = MD5.HashData(Encoding.UTF8.GetBytes(toHash.ToUpper()));
            var md5String = Convert.ToHexString(md5Hash).ToLower();

            // Calculate SHA1 of MD5 and convert to lowercase
            var sha1Hash = SHA1.HashData(Encoding.UTF8.GetBytes(md5String));
            var finalHash = Convert.ToHexString(sha1Hash).ToLower();

            return finalHash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JMF] Error calculating hash");
            throw;
        }
    }

    private string GenerateOrderNumber()
    {
        return Random.Shared.Next(3000000, 3999999).ToString();
    }

    private string GetBaseUrl()
    {
        var baseUrl = _config["BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
            return baseUrl.TrimEnd('/');

        // Fallback to default
        return "https://webcashier.onrender.com";
    }
}
