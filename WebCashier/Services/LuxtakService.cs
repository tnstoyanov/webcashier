using System.Text;
using System.Text.Json;
using WebCashier.Models.Luxtak;

namespace WebCashier.Services
{
    public class LuxtakService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LuxtakService> _logger;

        // Configuration keys
        private const string EndpointKey = "Luxtak:Endpoint";
        private const string AppIdKey = "Luxtak:AppId";
        private const string AuthTokenKey = "Luxtak:AuthToken";
        private const string NotifyUrlKey = "Luxtak:NotifyUrl";

        public LuxtakService(HttpClient httpClient, IConfiguration configuration, ILogger<LuxtakService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<LuxtakPaymentResponse> CreateTradeAsync(decimal amount, string currency, string userEmail = "tony.stoyanov@tiebreak.dev", string userName = "Tony Stoyanov")
        {
            try
            {
                var request = CreatePaymentRequest(amount, currency, userEmail, userName);
                
                _logger.LogInformation("Creating Luxtak trade request: {@Request}", request);

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true
                });

                _logger.LogInformation("Luxtak JSON Request: {Json}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Add authorization header
                var authToken = _configuration[AuthTokenKey];
                if (!string.IsNullOrEmpty(authToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
                }

                var endpoint = _configuration[EndpointKey] ?? "https://gateway.luxtak.com/trade/create";
                
                _logger.LogInformation("Sending POST request to Luxtak endpoint: {Endpoint}", endpoint);

                var response = await _httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Luxtak API Response - Status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var luxtakResponse = JsonSerializer.Deserialize<LuxtakPaymentResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                    _logger.LogInformation("Luxtak response deserialized: {@Response}", luxtakResponse);
                    return luxtakResponse ?? new LuxtakPaymentResponse { Code = "ERROR", Message = "Failed to deserialize response" };
                }
                else
                {
                    _logger.LogError("Luxtak API error - Status: {StatusCode}, Content: {Content}", 
                        response.StatusCode, responseContent);
                    
                    return new LuxtakPaymentResponse 
                    { 
                        Code = "ERROR", 
                        Message = $"HTTP {response.StatusCode}: {responseContent}" 
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Luxtak API");
                return new LuxtakPaymentResponse 
                { 
                    Code = "ERROR", 
                    Message = $"Exception: {ex.Message}" 
                };
            }
        }

        private LuxtakPaymentRequest CreatePaymentRequest(decimal amount, string currency, string userEmail, string userName)
        {
            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Generate random transaction ID: R-3000000 ~ R-3999999
            var random = new Random();
            var outTradeNo = $"R-{random.Next(3000000, 4000000)}";
            
            // Generate buyer ID: buyer_7000000 ~ buyer_7999999
            var buyerId = $"buyer_{random.Next(7000000, 8000000)}";
            
            var appId = _configuration[AppIdKey] ?? "17529157991280801";
            var notifyUrl = _configuration[NotifyUrlKey] ?? "https://webcashier.onrender.com/api/luxtak/notification";

            return new LuxtakPaymentRequest
            {
                AppId = appId,
                OutTradeNo = outTradeNo,
                OrderCurrency = currency.ToUpper(),
                OrderAmount = amount.ToString("F2"),
                Timestamp = timestamp,
                NotifyUrl = notifyUrl,
                BuyerId = buyerId,
                Customer = new LuxtakCustomer
                {
                    Identify = new LuxtakIdentify
                    {
                        Type = "CPF",
                        Number = "50284414727" // Default test CPF
                    },
                    Name = userName,
                    Email = userEmail
                }
            };
        }
    }
}
