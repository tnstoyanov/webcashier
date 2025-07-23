using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebCashier.Models.Luxtak;

namespace WebCashier.Controllers
{
    [ApiController]
    [Route("api/luxtak")]
    public class LuxtakApiController : ControllerBase
    {
        private readonly ILogger<LuxtakApiController> _logger;

        public LuxtakApiController(ILogger<LuxtakApiController> logger)
        {
            _logger = logger;
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

                if (string.IsNullOrEmpty(body))
                {
                    _logger.LogWarning("Luxtak notification received with empty body");
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

                        // TODO: Update payment state in database based on notification
                        // For now, just log the notification

                        return Ok(new { status = "success", message = "Notification received and processed" });
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize Luxtak notification");
                        return BadRequest("Invalid notification format");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse Luxtak notification JSON");
                    
                    // Log raw body for debugging
                    _logger.LogInformation("Raw notification body: {RawBody}", body);
                    
                    return BadRequest("Invalid JSON format");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Luxtak notification");
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
