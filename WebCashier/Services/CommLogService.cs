using System.Text;
using System.Text.Json;

namespace WebCashier.Services
{
    public interface ICommLogService
    {
        Task LogAsync(string type, object data, string? category = null);
    }

    public class CommLogService : ICommLogService
    {
        private readonly ILogger<CommLogService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public CommLogService(ILogger<CommLogService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task LogAsync(string type, object data, string? category = null)
        {
            var payload = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                type,
                category = category ?? "general",
                data
            };

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(payload, jsonOptions);
            _logger.LogInformation("[CommLog] {Type}/{Category}: {Json}", type, category ?? "general", json);

            var endpoint = _configuration["CommLogs:Endpoint"] ?? "https://webcashier.onrender.com/api/comm-logs";
            var enabled = string.Equals(_configuration["CommLogs:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            try
            {
                if (enabled)
                {
                    var client = _httpClientFactory.CreateClient("comm-logs");
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(endpoint, content);
                    _logger.LogInformation("[CommLog] POST {Endpoint} -> {Status}", endpoint, (int)response.StatusCode);
                }
                else
                {
                    _logger.LogDebug("[CommLog] Remote posting disabled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CommLog] Failed posting to {Endpoint}", endpoint);
            }
        }
    }
}
