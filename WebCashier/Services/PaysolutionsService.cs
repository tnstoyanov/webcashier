using System.Text.Json;
using WebCashier.Models.Paysolutions;
using Microsoft.Extensions.Options;

namespace WebCashier.Services
{
    public interface IPaysolutionsService
    {
        Task<PaysolutionsDepositResponse> CreateDepositAsync(PaysolutionsDepositRequest request);
    }

    public class PaysolutionsService : IPaysolutionsService
    {
        private readonly HttpClient _httpClient;
        private readonly PaysolutionsConfig _config;
        private readonly ILogger<PaysolutionsService> _logger;
        private readonly ICommLogService _commLog;

        public PaysolutionsService(
            HttpClient httpClient,
            IOptions<PaysolutionsConfig> config,
            ILogger<PaysolutionsService> logger,
            ICommLogService commLog)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            _commLog = commLog;
        }

        public async Task<PaysolutionsDepositResponse> CreateDepositAsync(PaysolutionsDepositRequest request)
        {
            try
            {
                // Build query string
                var queryParams = new Dictionary<string, string>
                {
                    ["merchantID"] = request.MerchantID,
                    ["productDetail"] = request.ProductDetail,
                    ["customerEmail"] = request.CustomerEmail,
                    ["customerName"] = request.CustomerName,
                    ["total"] = request.Total.ToString("0.00"),
                    ["referenceNo"] = request.ReferenceNo
                };

                var queryString = string.Join("&", queryParams.Select(kvp => 
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

                var url = $"{_config.BaseUrl}/tep/api/v2/promptpaynew?{queryString}";

                _logger.LogInformation("[Paysolutions] Creating deposit - ReferenceNo: {ReferenceNo}, Total: {Total}", 
                    request.ReferenceNo, request.Total);

                // Log request
                await _commLog.LogAsync("Paysolutions.Deposit.Request", new
                {
                    url = url,
                    merchantID = request.MerchantID,
                    referenceNo = request.ReferenceNo,
                    total = request.Total,
                    customerEmail = request.CustomerEmail,
                    customerName = request.CustomerName
                });

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Headers.Add("Authorization", $"Bearer {_config.AuthToken}");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("[Paysolutions] Response status: {StatusCode}", response.StatusCode);

                // Log response
                await _commLog.LogAsync("Paysolutions.Deposit.Response", new
                {
                    statusCode = (int)response.StatusCode,
                    response = responseContent
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[Paysolutions] API error: {StatusCode} - {Content}", 
                        response.StatusCode, responseContent);

                    // Try to parse error message
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<PaysolutionsDepositResponse>(responseContent, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        return errorResponse ?? new PaysolutionsDepositResponse
                        {
                            Status = "error",
                            Message = $"HTTP {response.StatusCode}: {responseContent}"
                        };
                    }
                    catch
                    {
                        return new PaysolutionsDepositResponse
                        {
                            Status = "error",
                            Message = $"HTTP {response.StatusCode}: {responseContent}"
                        };
                    }
                }

                var result = JsonSerializer.Deserialize<PaysolutionsDepositResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                {
                    _logger.LogError("[Paysolutions] Failed to deserialize response");
                    return new PaysolutionsDepositResponse
                    {
                        Status = "error",
                        Message = "Failed to parse response"
                    };
                }

                _logger.LogInformation("[Paysolutions] Deposit created successfully - OrderNo: {OrderNo}", 
                    result.Data?.OrderNo);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Paysolutions] Exception creating deposit");
                await _commLog.LogAsync("Paysolutions.Deposit.Exception", new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });

                return new PaysolutionsDepositResponse
                {
                    Status = "error",
                    Message = ex.Message
                };
            }
        }
    }
}
