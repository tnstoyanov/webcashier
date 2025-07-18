using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebCashier.Models.Praxis;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PraxisController : ControllerBase
    {
        private readonly ILogger<PraxisController> _logger;
        private readonly IPaymentStateService _paymentStateService;

        public PraxisController(ILogger<PraxisController> logger, IPaymentStateService paymentStateService)
        {
            _logger = logger;
            _paymentStateService = paymentStateService;
        }

        [HttpPost("notification")]
        public async Task<IActionResult> HandleNotification()
        {
            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                
                _logger.LogInformation("Received Praxis notification: {RequestBody}", requestBody);

                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty notification payload received");
                    return Ok("Empty payload");
                }

                try
                {
                    var callbackData = JsonSerializer.Deserialize<PraxisCallbackModel>(requestBody);
                    
                    if (callbackData?.session?.order_id != null)
                    {
                        var orderId = callbackData.session.order_id;
                        
                        _logger.LogInformation("Processing Praxis callback for OrderId: {OrderId}, Status: {Status}", 
                            orderId, callbackData.transaction?.transaction_status);

                        // Update payment state with callback data
                        _paymentStateService.SetPaymentCompleted(orderId, callbackData);
                        
                        return Ok(new { status = "processed", order_id = orderId });
                    }
                    else
                    {
                        _logger.LogWarning("Invalid callback data structure - missing order_id");
                        return Ok("Invalid callback data");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse Praxis callback JSON");
                    return Ok("JSON parsing failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Praxis notification");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
