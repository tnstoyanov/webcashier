using System.Text.Json;
using WebCashier.Models.PagTree;
using Microsoft.Extensions.Options;

namespace WebCashier.Services
{
    public interface IPagTreeService
    {
        Task<(bool Success, PagTreePaymentResponse? Data, PagTreeErrorResponse? Error)> CreatePaymentAsync(PagTreePaymentRequest request);
        Task<(bool Success, PagTreePaymentResponse? Data, PagTreeErrorResponse? Error)> GetPaymentStatusAsync(string paymentId);
    }

    public class PagTreeService : IPagTreeService
    {
        private readonly HttpClient _httpClient;
        private readonly PagTreeConfig _config;
        private readonly ILogger<PagTreeService> _logger;
        private readonly ICommLogService _commLog;

        public PagTreeService(
            HttpClient httpClient,
            IOptions<PagTreeConfig> config,
            ILogger<PagTreeService> logger,
            ICommLogService commLog)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            _commLog = commLog;
        }

        public async Task<(bool Success, PagTreePaymentResponse? Data, PagTreeErrorResponse? Error)> CreatePaymentAsync(PagTreePaymentRequest request)
        {
            try
            {
                var url = $"{_config.BaseUrl}/payments";

                _logger.LogInformation("[PagTree] Creating payment - IdempotencyKey: {IdempotencyKey}, Amount: {Amount}", 
                    request.IdempotencyKey, request.Amount);

                // Log request
                await _commLog.LogAsync("PagTree.CreatePayment.Request", new
                {
                    url = url,
                    amount = request.Amount,
                    currency = request.Currency,
                    idempotencyKey = request.IdempotencyKey,
                    customerDocument = request.Customer.Document
                });

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
                httpRequest.Headers.Add("Authorization", $"Bearer {_config.SecretKey}");
                httpRequest.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("[PagTree] Response status: {StatusCode}", response.StatusCode);

                // Log response
                await _commLog.LogAsync("PagTree.CreatePayment.Response", new
                {
                    statusCode = (int)response.StatusCode,
                    response = responseContent
                });

                if (response.IsSuccessStatusCode)
                {
                    var paymentResponse = JsonSerializer.Deserialize<PagTreePaymentResponse>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    return (true, paymentResponse, null);
                }
                else
                {
                    _logger.LogError("[PagTree] API error: {StatusCode} - {Content}", 
                        response.StatusCode, responseContent);

                    // Try to parse error response
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<PagTreeErrorResponse>(responseContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        return (false, null, errorResponse);
                    }
                    catch
                    {
                        var errorResponse = new PagTreeErrorResponse
                        {
                            Code = response.StatusCode.ToString(),
                            Message = $"HTTP {response.StatusCode}: {responseContent}"
                        };
                        return (false, null, errorResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PagTree] Exception during CreatePaymentAsync");
                var errorResponse = new PagTreeErrorResponse
                {
                    Code = "EXCEPTION",
                    Message = ex.Message
                };
                return (false, null, errorResponse);
            }
        }

        public async Task<(bool Success, PagTreePaymentResponse? Data, PagTreeErrorResponse? Error)> GetPaymentStatusAsync(string paymentId)
        {
            try
            {
                var url = $"{_config.BaseUrl}/payments/{paymentId}";

                _logger.LogInformation("[PagTree] Getting payment status - PaymentId: {PaymentId}", paymentId);

                // Log request
                await _commLog.LogAsync("PagTree.GetPayment.Request", new
                {
                    url = url,
                    paymentId = paymentId
                });

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Add("Authorization", $"Bearer {_config.SecretKey}");
                httpRequest.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("[PagTree] Response status: {StatusCode}", response.StatusCode);

                // Log response
                await _commLog.LogAsync("PagTree.GetPayment.Response", new
                {
                    statusCode = (int)response.StatusCode,
                    response = responseContent
                });

                if (response.IsSuccessStatusCode)
                {
                    var paymentResponse = JsonSerializer.Deserialize<PagTreePaymentResponse>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    return (true, paymentResponse, null);
                }
                else
                {
                    _logger.LogError("[PagTree] API error: {StatusCode} - {Content}", 
                        response.StatusCode, responseContent);

                    // Try to parse error response
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<PagTreeErrorResponse>(responseContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        return (false, null, errorResponse);
                    }
                    catch
                    {
                        var errorResponse = new PagTreeErrorResponse
                        {
                            Code = response.StatusCode.ToString(),
                            Message = $"HTTP {response.StatusCode}: {responseContent}"
                        };
                        return (false, null, errorResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PagTree] Exception during GetPaymentStatusAsync");
                var errorResponse = new PagTreeErrorResponse
                {
                    Code = "EXCEPTION",
                    Message = ex.Message
                };
                return (false, null, errorResponse);
            }
        }
    }
}
