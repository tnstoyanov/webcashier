using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using WebCashier.Models.Luxtak;

namespace WebCashier.Controllers
{
    [ApiController]
    [Route("api/luxtak")]
    public class LuxtakApiController : ControllerBase
    {
        private readonly ILogger<LuxtakApiController> _logger;
        private readonly HttpClient _httpClient;
        
        // Render.com comm logs endpoint
        private const string RenderCommLogsUrl = "https://webcashier.onrender.com/api/comm-logs";

        public LuxtakApiController(ILogger<LuxtakApiController> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
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

        [HttpPost("notification")]
        public async Task<IActionResult> HandleNotification()
        {
            try
            {
                _logger.LogInformation("Luxtak notification received via {Method}", Request.Method);
                _logger.LogInformation("Content-Type: {ContentType}", Request.ContentType);
                _logger.LogInformation("Headers: {@Headers}", Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

                // Read the request body
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                _logger.LogInformation("Luxtak notification body: {Body}", body);
                await LogToRenderCommLogsAsync("luxtak-callback", body);

                if (string.IsNullOrEmpty(body))
                {
                    _logger.LogWarning("Luxtak notification received with empty body");
                    await LogToRenderCommLogsAsync("luxtak-callback-error", "Empty notification body");
                    return BadRequest("Empty notification body");
                }

                // Try to parse as JSON
                try
                {
                    var notification = JsonSerializer.Deserialize<LuxtakCallbackModel>(body, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                    _logger.LogInformation("Luxtak notification parsed: {@Notification}", notification);

                    if (notification != null)
                    {
                        _logger.LogInformation("Luxtak notification - TradeNo: {TradeNo}, OutTradeNo: {OutTradeNo}, Status: {Status}, Amount: {Amount}",
                            notification.TradeNo, notification.OutTradeNo, notification.TradeStatus, notification.OrderAmount);

                        await LogToRenderCommLogsAsync("luxtak-callback-success", notification);

                        // TODO: Update payment state in database based on notification
                        // For now, just log the notification

                        return Ok(new { status = "success", message = "Notification received and processed" });
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize Luxtak notification");
                        await LogToRenderCommLogsAsync("luxtak-callback-error", "Failed to deserialize notification");
                        return BadRequest("Invalid notification format");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse Luxtak notification JSON");
                    
                    // Log raw body for debugging
                    _logger.LogInformation("Raw notification body: {RawBody}", body);
                    await LogToRenderCommLogsAsync("luxtak-callback-json-error", ex);
                    
                    return BadRequest("Invalid JSON format");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Luxtak notification");
                await LogToRenderCommLogsAsync("luxtak-callback-general-error", ex);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new 
            { 
                status = "active", 
                timestamp = DateTime.UtcNow, 
                service = "Luxtak API Controller",
                endpoints = new
                {
                    notification = "/api/luxtak/notification"
                }
            });
        }
    }
}
