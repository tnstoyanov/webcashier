using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WebCashier.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PraxisController : ControllerBase
    {
        private readonly ILogger<PraxisController> _logger;

        public PraxisController(ILogger<PraxisController> logger)
        {
            _logger = logger;
        }

        [HttpPost("notification")]
        public async Task<IActionResult> HandleNotification()
        {
            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                
                _logger.LogInformation("Received Praxis notification: {RequestBody}", requestBody);

                // Parse the notification
                var notification = JsonSerializer.Deserialize<JsonDocument>(requestBody);
                
                // Process the notification based on your business logic
                // For now, just log it and return OK
                
                return Ok(new { status = "received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Praxis notification");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
