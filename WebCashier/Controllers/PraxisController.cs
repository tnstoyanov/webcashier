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
                _logger.LogInformation("=== PRAXIS NOTIFICATION RECEIVED ===");
                _logger.LogInformation("Request Method: {Method}", Request.Method);
                _logger.LogInformation("Request Content-Type: {ContentType}", Request.ContentType);
                _logger.LogInformation("Request Headers: {Headers}", 
                    string.Join(", ", Request.Headers.Select(h => $"{h.Key}: {h.Value}")));

                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                
                _logger.LogInformation("Praxis notification payload: {RequestBody}", requestBody);

                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty notification payload received");
                    return Ok("Empty payload received");
                }

                try
                {
                    var callbackData = JsonSerializer.Deserialize<PraxisCallbackModel>(requestBody);
                    
                    if (callbackData?.session?.order_id != null)
                    {
                        var orderId = callbackData.session.order_id;
                        
                        _logger.LogInformation("Processing Praxis callback for OrderId: {OrderId}, Status: {Status}, TID: {TID}", 
                            orderId, callbackData.transaction?.transaction_status, callbackData.transaction?.tid);

                        // Update payment state with callback data
                        _paymentStateService.SetPaymentCompleted(orderId, callbackData);
                        
                        _logger.LogInformation("Payment state updated successfully for OrderId: {OrderId}", orderId);
                        
                        // Create proper Praxis callback response - same format for all transactions
                        var response = new PraxisCallbackResponse
                        {
                            status = 0,
                            description = "Ok",
                            version = "1.3",
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        };

                        _logger.LogInformation("Returning Praxis callback response: Status={Status}, Description={Description}, TransactionStatus={TransactionStatus}", 
                            response.status, response.description, callbackData.transaction?.transaction_status);
                        
                        return Ok(response);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid callback data structure - missing order_id");
                        _logger.LogWarning("Callback data: {CallbackData}", requestBody);
                        return Ok("Invalid callback data - missing order_id");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse Praxis callback JSON: {JsonBody}", requestBody);
                    return Ok("JSON parsing failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Praxis notification");
                return Ok("Error processing notification");
            }
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogInformation("Praxis API test endpoint called");
            return Ok(new { status = "Praxis API is working", timestamp = DateTime.UtcNow });
        }
    }
}
