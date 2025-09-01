using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebCashier.Models.Smilepayz;

namespace WebCashier.Services
{
    public interface ISmilepayzService
    {
        Task<SmilepayzResponse> CreatePayInAsync(decimal amount, string currency, string payerName);
    }

    public class SmilepayzService : ISmilepayzService
    {
        private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmilepayzService> _logger;
    private readonly ICommLogService _comm;

        public SmilepayzService(HttpClient httpClient, IConfiguration configuration, ILogger<SmilepayzService> logger, ICommLogService comm)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _comm = comm;
        }

        public async Task<SmilepayzResponse> CreatePayInAsync(decimal amount, string currency, string payerName)
        {
            var endpoint = _configuration["Smilepayz:Endpoint"] ?? "https://sandbox-gateway.smilepayz.com/v2.0/transaction/pay-in";
            var partnerId = _configuration["Smilepayz:PartnerId"] ?? string.Empty;
            var merchantSecret = _configuration["Smilepayz:MerchantSecret"] ?? string.Empty;
            var redirectUrl = _configuration["Smilepayz:RedirectUrl"] ?? string.Empty;
            var callbackUrl = _configuration["Smilepayz:CallbackUrl"] ?? string.Empty;
            var merchantName = _configuration["Smilepayz:MerchantName"] ?? "Tiebreak";
            var paymentMethod = _configuration["Smilepayz:PaymentMethod"] ?? "BANK";

            // Build request body
            var rnd = new Random();
            var orderNo = rnd.Next(3_000_000, 4_000_000).ToString();
            var request = new SmilepayzRequest
            {
                OrderNo = orderNo,
                Purpose = "Smilepayz deposit",
                Merchant = new SmilepayzMerchant { MerchantId = partnerId, MerchantName = merchantName },
                Money = new SmilepayzMoney { Amount = ((int)Math.Round(amount, 0)).ToString(), Currency = string.IsNullOrWhiteSpace(currency) ? "THB" : currency },
                Payer = new SmilepayzPayer { Name = string.IsNullOrWhiteSpace(payerName) ? "Customer" : payerName },
                PaymentMethod = paymentMethod,
                RedirectUrl = redirectUrl,
                CallbackUrl = callbackUrl
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = null });
            await _comm.LogAsync("smilepayz-request", request, "smilepayz");

            // Create ISO8601 with offset timestamp
            var timestamp = CreateIso8601WithOffset(DateTimeOffset.Now);

            // String to sign: timestamp|merchantSecret|minifiedPayload
            var stringToSign = $"{timestamp}|{merchantSecret}|{json}";

            // Sign with RSA private key SHA256
            var privateKeyPem = _configuration["Smilepayz:RSAPrivateKey"] ?? string.Empty;
            var signature = SignWithRsa(privateKeyPem, stringToSign);
            await _comm.LogAsync("smilepayz-headers", new { partnerId, timestamp, signatureLength = signature?.Length }, "smilepayz");

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Content = content;
            requestMessage.Headers.TryAddWithoutValidation("X-PARTNER-ID", partnerId);
            requestMessage.Headers.TryAddWithoutValidation("X-TIMESTAMP", timestamp);
            requestMessage.Headers.TryAddWithoutValidation("X-SIGNATURE", signature);

            _logger.LogInformation("Smilepayz request: {Json}", json);
            _logger.LogInformation("Smilepayz headers - X-PARTNER-ID: {Pid}, X-TIMESTAMP: {Ts}, X-SIGNATURE len: {Len}", partnerId, timestamp, signature?.Length);

            var resp = await _httpClient.SendAsync(requestMessage);
            var respContent = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("Smilepayz response ({Status}): {Content}", (int)resp.StatusCode, respContent);
            await _comm.LogAsync("smilepayz-response", new { status = (int)resp.StatusCode, content = respContent }, "smilepayz");

            if (!resp.IsSuccessStatusCode)
            {
                return new SmilepayzResponse { Code = ((int)resp.StatusCode).ToString(), Message = respContent };
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<SmilepayzResponse>(respContent);
                return parsed ?? new SmilepayzResponse { Code = "PARSE_ERROR", Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Smilepayz response");
                return new SmilepayzResponse { Code = "PARSE_EXCEPTION", Message = ex.Message };
            }
        }

        private static string CreateIso8601WithOffset(DateTimeOffset date)
        {
            // Example: 2025-09-01T22:58:39+03:00
            return date.ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        }

        private static string SignWithRsa(string privateKeyPem, string data)
        {
            // Load RSA private key from PEM and sign data with SHA256
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem.ToCharArray());
            var bytes = Encoding.UTF8.GetBytes(data);
            var sigBytes = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sigBytes);
        }
    }
}
