using System.Text;
using System.Text.Json;
using WebCashier.Models.Luxtak;

namespace WebCashier.Services
{
    public class LuxtakService
    {
        // Render.com comm logs endpoint (replace with your actual endpoint if needed)
        private const string RenderCommLogsUrl = "https://webcashier.onrender.com/api/comm-logs";

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

        private async Task LogToRenderCommLogsAsync(string type, object data)
        {
            try
            {
                var payload = new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    type,
                    data
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(RenderCommLogsUrl, content);
                _logger.LogInformation("Logged to Render.com comm logs: {Type}", type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log to Render.com comm logs");
            }
        }

        public async Task<LuxtakPaymentResponse> CreateTradeAsync(decimal amount, string currency, string userEmail = "tony.stoyanov@tiebreak.dev", string userName = "Tony Stoyanov")
        {
            try
            {
                var request = CreatePaymentRequest(amount, currency, userEmail, userName);
                _logger.LogInformation("=== LUXTAK API CALL START ===");
                _logger.LogInformation("Creating Luxtak trade request: {@Request}", request);
                await LogToRenderCommLogsAsync("luxtak-request", request);

                // Ensure all required fields match sample
                request.Subject = "Luxtak Deposit";
                request.Content = "LATAM operations";
                request.TradeType = "WEB";
                request.Version = "2.0";
                request.Regions = new[] { "BRA" };
                request.Address = new LuxtakAddress { ZipCode = "38082365" };

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    WriteIndented = false
                });

                _logger.LogInformation("Luxtak JSON Request Body: {Json}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Set Authorization header as Basic <base64>
                var authToken = _configuration[AuthTokenKey];
                if (!string.IsNullOrEmpty(authToken))
                {
                    // If not already base64, encode it
                    if (!authToken.StartsWith("MT")) // crude check for sample
                    {
                        var bytes = Encoding.UTF8.GetBytes(authToken);
                        authToken = Convert.ToBase64String(bytes);
                    }
                    _httpClient.DefaultRequestHeaders.Remove("Authorization");
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
                    _logger.LogInformation("Authorization header set with Basic auth token");
                }
                else
                {
                    _logger.LogWarning("No authorization token configured for Luxtak");
                }

                var endpoint = _configuration[EndpointKey] ?? "https://gateway.luxtak.com/trade/create";
                _logger.LogInformation("Sending POST request to Luxtak endpoint: {Endpoint}", endpoint);
                _logger.LogInformation("Request Content-Type: application/json");
                _logger.LogInformation("Request Content-Length: {Length} bytes", json.Length);

                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    _logger.LogInformation("Request Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsync(endpoint, content);
                stopwatch.Stop();
                var responseContent = await response.Content.ReadAsStringAsync();

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

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("HTTP request successful, attempting to deserialize JSON response");
                    var luxtakResponse = JsonSerializer.Deserialize<LuxtakPaymentResponse>(responseContent ?? "{}");
                    _logger.LogInformation("Luxtak response deserialized successfully: {@Response}", luxtakResponse);
                    _logger.LogInformation("=== LUXTAK API CALL END - SUCCESS ===");
                    await LogToRenderCommLogsAsync("luxtak-success", luxtakResponse ?? new object());
                    return luxtakResponse ?? new LuxtakPaymentResponse { Code = "ERROR", Message = "Failed to deserialize response" };
                }
                else
                {
                    _logger.LogError("=== LUXTAK API CALL END - HTTP ERROR ===");
                    _logger.LogError("Luxtak API HTTP error - Status: {StatusCode}, Reason: {ReasonPhrase}, Content: {Content}", 
                        response.StatusCode, response.ReasonPhrase, responseContent);
                    await LogToRenderCommLogsAsync("luxtak-error", responseContent ?? "Empty response");
                    return new LuxtakPaymentResponse 
                    { 
                        Code = "HTTP_ERROR", 
                        Message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseContent}" 
                    };
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "=== LUXTAK API CALL END - HTTP EXCEPTION ===");
                _logger.LogError("HTTP exception calling Luxtak API: {Message}", httpEx.Message);
                await LogToRenderCommLogsAsync("luxtak-http-exception", httpEx);
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
                await LogToRenderCommLogsAsync("luxtak-timeout", tcEx);
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
                await LogToRenderCommLogsAsync("luxtak-json-error", jsonEx);
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
                await LogToRenderCommLogsAsync("luxtak-general-exception", ex);
                
                return new LuxtakPaymentResponse 
                { 
                    Code = "GENERAL_ERROR", 
                    Message = $"Exception: {ex.Message}" 
                };
            }
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
                    Name = userName,
                    Phone = "73984401850",
                    Email = userEmail
                },
                Address = new LuxtakAddress
                {
                    ZipCode = "38082365"
                }
            };
        }
    }
}
