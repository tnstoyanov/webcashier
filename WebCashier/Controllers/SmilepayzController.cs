using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    [ApiController]
    [Route("api/smilepayz")] 
    public class SmilepayzController : ControllerBase
    {
        private readonly ILogger<SmilepayzController> _logger;
        private readonly ICommLogService _comm;

        public SmilepayzController(ILogger<SmilepayzController> logger, ICommLogService comm)
        {
            _logger = logger;
            _comm = comm;
        }

        [HttpPost("notification")]
        public async Task<IActionResult> Notification()
        {
            try
            {
                _logger.LogInformation("Smilepayz notification received");
                var body = await new StreamReader(Request.Body).ReadToEndAsync();
                await _comm.LogAsync("smilepayz-callback", new { headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()), body }, "smilepayz");

                // Try parse just for logging
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    _logger.LogInformation("Smilepayz callback JSON parsed OK");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Smilepayz callback JSON parse error");
                }

                return Ok(new { status = "received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Smilepayz notification");
                await _comm.LogAsync("smilepayz-callback-error", ex, "smilepayz");
                return StatusCode(500);
            }
        }
    }
}
