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
<<<<<<< HEAD
=======
                
                _logger.LogInformation("=== LUXTAK API CALL START ===");
                _logger.LogInformation("Creating Luxtak trade request: {@Request}", request);

>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true
                });

<<<<<<< HEAD
                // Log to Render.com comm logs
                await LogToRenderCom("Luxtak Request", json);
=======
                _logger.LogInformation("Luxtak JSON Request Body: {Json}", json);
>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add required headers
                var authToken = _configuration[AuthTokenKey];
                if (!string.IsNullOrEmpty(authToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
                    _logger.LogInformation("Authorization header set with Basic auth token");
                }
                else
                {
                    _logger.LogWarning("No authorization token configured for Luxtak");
                }
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WebCashier/1.0");
                _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

                var endpoint = _configuration[EndpointKey] ?? "https://gateway.luxtak.com/trade/create";
<<<<<<< HEAD
=======
                
                _logger.LogInformation("Sending POST request to Luxtak endpoint: {Endpoint}", endpoint);
                _logger.LogInformation("Request Content-Type: application/json");
                _logger.LogInformation("Request Content-Length: {Length} bytes", json.Length);
>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff

                // Log all request headers
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    _logger.LogInformation("Request Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsync(endpoint, content);
                stopwatch.Stop();
                
                var responseContent = await response.Content.ReadAsStringAsync();

<<<<<<< HEAD
                // Log response to Render.com comm logs
                await LogToRenderCom("Luxtak Response", responseContent);
=======
                _logger.LogInformation("=== LUXTAK API RESPONSE ===");
                _logger.LogInformation("Response received in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("Response Status Code: {StatusCode} ({StatusCodeInt})", response.StatusCode, (int)response.StatusCode);
                _logger.LogInformation("Response Reason Phrase: {ReasonPhrase}", response.ReasonPhrase);
                
                // Log all response headers
                foreach (var header in response.Headers)
                {
                    _logger.LogInformation("Response Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
                }
                
                // Log content headers
                foreach (var header in response.Content.Headers)
                {
                    _logger.LogInformation("Content Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
                }

                _logger.LogInformation("Response Content Length: {Length} bytes", responseContent?.Length ?? 0);
                _logger.LogInformation("Response Content: {Content}", responseContent);
>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("HTTP request successful, attempting to deserialize JSON response");
                    
                    var luxtakResponse = JsonSerializer.Deserialize<LuxtakPaymentResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });
<<<<<<< HEAD
=======

                    _logger.LogInformation("Luxtak response deserialized successfully: {@Response}", luxtakResponse);
                    _logger.LogInformation("=== LUXTAK API CALL END - SUCCESS ===");
                    
>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff
                    return luxtakResponse ?? new LuxtakPaymentResponse { Code = "ERROR", Message = "Failed to deserialize response" };
                }
                else
                {
<<<<<<< HEAD
                    return new LuxtakPaymentResponse
                    {
                        Code = "ERROR",
                        Message = $"HTTP {response.StatusCode}: {responseContent}"
=======
                    _logger.LogError("=== LUXTAK API CALL END - HTTP ERROR ===");
                    _logger.LogError("Luxtak API HTTP error - Status: {StatusCode}, Reason: {ReasonPhrase}, Content: {Content}", 
                        response.StatusCode, response.ReasonPhrase, responseContent);
                    
                    return new LuxtakPaymentResponse 
                    { 
                        Code = "HTTP_ERROR", 
                        Message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseContent}" 
>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff
                    };
                }
            }
            catch (HttpRequestException httpEx)
            {
<<<<<<< HEAD
                await LogToRenderCom("Luxtak Exception", ex.ToString());
                return new LuxtakPaymentResponse
                {
                    Code = "ERROR",
                    Message = $"Exception: {ex.Message}"
=======
                _logger.LogError(httpEx, "=== LUXTAK API CALL END - HTTP EXCEPTION ===");
                _logger.LogError("HTTP exception calling Luxtak API: {Message}", httpEx.Message);
                return new LuxtakPaymentResponse 
                { 
                    Code = "HTTP_EXCEPTION", 
                    Message = $"HTTP Exception: {httpEx.Message}" 
                };
            }
            catch (TaskCanceledException tcEx)
            {
                _logger.LogError(tcEx, "=== LUXTAK API CALL END - TIMEOUT ===");
                _logger.LogError("Timeout calling Luxtak API: {Message}", tcEx.Message);
                return new LuxtakPaymentResponse 
                { 
                    Code = "TIMEOUT", 
                    Message = $"Request timeout: {tcEx.Message}" 
                };
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "=== LUXTAK API CALL END - JSON ERROR ===");
                _logger.LogError("JSON deserialization error: {Message}", jsonEx.Message);
                return new LuxtakPaymentResponse 
                { 
                    Code = "JSON_ERROR", 
                    Message = $"JSON Error: {jsonEx.Message}" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== LUXTAK API CALL END - GENERAL EXCEPTION ===");
                _logger.LogError("Unexpected error calling Luxtak API: {Message}", ex.Message);
                _logger.LogError("Exception Type: {Type}", ex.GetType().Name);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
                
                return new LuxtakPaymentResponse 
                { 
                    Code = "GENERAL_ERROR", 
                    Message = $"Exception: {ex.Message}" 
>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff
                };
            }
        }

        // Log to Render.com comm logs (simple HTTP POST)
        private async Task LogToRenderCom(string type, string message)
        {
            try
            {
                var logEndpoint = "https://webcashier.onrender.com/api/comm-logs";
                var logObj = new
                {
                    type,
                    message,
                    timestamp = DateTime.UtcNow.ToString("o")
                };
                var logJson = JsonSerializer.Serialize(logObj);
                var logContent = new StringContent(logJson, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(logEndpoint, logContent);
            }
            catch { /* ignore logging errors */ }
        }

        private LuxtakPaymentRequest CreatePaymentRequest(decimal amount, string currency, string userEmail, string userName)
        {
            _logger.LogInformation("=== CREATING LUXTAK PAYMENT REQUEST ===");
            
            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Generate random transaction ID: R-3000000 ~ R-3999999
            var random = new Random();
            var outTradeNo = $"R-{random.Next(3000000, 4000000)}";
            
            // Generate buyer ID: buyer_7000000 ~ buyer_7999999
            var buyerId = $"buyer_{random.Next(7000000, 8000000)}";
            
            var appId = _configuration[AppIdKey] ?? "17529157991280801";
            var notifyUrl = _configuration[NotifyUrlKey] ?? "https://webcashier.onrender.com/api/luxtak/notification";
            var endpoint = _configuration[EndpointKey] ?? "https://gateway.luxtak.com/trade/create";
            var hasAuthToken = !string.IsNullOrEmpty(_configuration[AuthTokenKey]);

            _logger.LogInformation("Configuration values:");
            _logger.LogInformation("- Endpoint: {Endpoint}", endpoint);
            _logger.LogInformation("- AppId: {AppId}", appId);
            _logger.LogInformation("- NotifyUrl: {NotifyUrl}", notifyUrl);
            _logger.LogInformation("- HasAuthToken: {HasAuthToken}", hasAuthToken);
            
            _logger.LogInformation("Generated values:");
            _logger.LogInformation("- OutTradeNo: {OutTradeNo}", outTradeNo);
            _logger.LogInformation("- BuyerId: {BuyerId}", buyerId);
            _logger.LogInformation("- Timestamp: {Timestamp}", timestamp);
            
            _logger.LogInformation("Request parameters:");
            _logger.LogInformation("- Amount: {Amount}", amount);
            _logger.LogInformation("- Currency: {Currency}", currency);
            _logger.LogInformation("- UserEmail: {UserEmail}", userEmail);
            _logger.LogInformation("- UserName: {UserName}", userName);

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
