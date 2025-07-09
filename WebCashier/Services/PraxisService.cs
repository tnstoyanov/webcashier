using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebCashier.Models;
using WebCashier.Models.Praxis;

namespace WebCashier.Services
{
    public interface IPraxisService
    {
        Task<PraxisResponse> ProcessPaymentAsync(PaymentModel payment, string clientIp);
    }

    public class PraxisService : IPraxisService
    {
        private readonly HttpClient _httpClient;
        private readonly PraxisConfig _config;
        private readonly ILogger<PraxisService> _logger;

        public PraxisService(HttpClient httpClient, PraxisConfig config, ILogger<PraxisService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<PraxisResponse> ProcessPaymentAsync(PaymentModel payment, string clientIp)
        {
            try
            {
                // Generate dynamic values
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var cid = new Random().Next(1, int.MaxValue).ToString();
                var orderId = new Random().Next(1, int.MaxValue).ToString();

                // Prepare secret key and IV
                var secretKey = PadLeft(_config.MerchantSecret, 32, '0');
                var requestTimestamp = PadLeft(timestamp.ToString(), 16, '0');

                // Encrypt card data
                var encryptedCardNumber = AesEncrypt(payment.CardNumber, secretKey, requestTimestamp);
                var encryptedCardExp = AesEncrypt(payment.ExpirationDate, secretKey, requestTimestamp);
                var encryptedCvv = AesEncrypt(payment.CVV, secretKey, requestTimestamp);

                // Convert amount to cents
                var amountInCents = (int)(payment.Amount * 100);

                // Build request
                var request = new PraxisRequest
                {
                    merchant_id = _config.MerchantId,
                    application_key = _config.ApplicationKey,
                    transaction_type = _config.TransactionType,
                    currency = payment.Currency,
                    amount = amountInCents,
                    card_data = new PraxisCardData
                    {
                        card_number = encryptedCardNumber,
                        card_exp = encryptedCardExp,
                        cvv = encryptedCvv
                    },
                    device_data = new PraxisDeviceData
                    {
                        user_agent = "Mozilla/5.0 (WebCashier/1.0)",
                        accept_header = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
                        language = "en-us",
                        ip_address = clientIp,
                        timezone_offset = 0,
                        color_depth = "24",
                        pixel_depth = "24",
                        pixel_ratio = "1",
                        screen_height = 1080,
                        screen_width = 1920,
                        viewport_height = 800,
                        viewport_width = 1920,
                        java_enabled = 0,
                        javascript_enabled = 1
                    },
                    cid = cid,
                    locale = "en-US",
                    customer_data = new PraxisCustomerData
                    {
                        country = "US",
                        first_name = GetFirstName(payment.NameOnCard),
                        last_name = GetLastName(payment.NameOnCard),
                        dob = "01/01/1990",
                        email = "customer@example.com",
                        phone = "1234567890",
                        zip = "12345",
                        city = "New York",
                        address = "123 Main St",
                        profile = "0"
                    },
                    gateway = _config.Gateway,
                    notification_url = _config.NotificationUrl,
                    return_url = _config.ReturnUrl,
                    order_id = orderId,
                    version = _config.Version,
                    timestamp = timestamp
                };

                // Generate signature
                var signature = GenerateSignature(request, encryptedCardNumber);

                // Make API call
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("GT-Authentication", signature);

                _logger.LogInformation("Sending payment request to Praxis API");

                var response = await _httpClient.PostAsync(_config.Endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Received response from Praxis API: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var praxisResponse = JsonSerializer.Deserialize<PraxisResponse>(responseContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    return praxisResponse ?? new PraxisResponse { status = false, message = "Invalid response from payment gateway" };
                }
                else
                {
                    _logger.LogError("Praxis API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                    return new PraxisResponse { status = false, message = "Payment gateway error" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment with Praxis");
                return new PraxisResponse { status = false, message = "Payment processing failed" };
            }
        }

        private string AesEncrypt(string text, string key, string iv)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.Latin1.GetBytes(key);
            aes.IV = Encoding.Latin1.GetBytes(iv);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var textBytes = Encoding.UTF8.GetBytes(text);
            var encrypted = encryptor.TransformFinalBlock(textBytes, 0, textBytes.Length);
            return Convert.ToBase64String(encrypted);
        }

        private string GenerateSignature(PraxisRequest request, string encryptedCardNumber)
        {
            var inputString = $"{request.merchant_id}{request.application_key}{request.timestamp}{request.transaction_type}{request.cid}{request.order_id}{request.currency}{request.amount}{request.gateway}{request.notification_url}{request.return_url}{encryptedCardNumber}{_config.MerchantSecret}";
            
            using var sha384 = SHA384.Create();
            var hash = sha384.ComputeHash(Encoding.UTF8.GetBytes(inputString));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private string PadLeft(string str, int length, char paddingChar)
        {
            return str.PadLeft(length, paddingChar);
        }

        private string GetFirstName(string nameOnCard)
        {
            var parts = nameOnCard.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : "Customer";
        }

        private string GetLastName(string nameOnCard)
        {
            var parts = nameOnCard.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "Customer";
        }
    }
}
