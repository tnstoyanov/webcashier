using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;
using WebCashier.Models.PagTree;
using Microsoft.Extensions.Options;

namespace WebCashier.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PagTreeController : ControllerBase
    {
        private readonly IPagTreeService _pagTreeService;
        private readonly PagTreeConfig _config;
        private readonly ILogger<PagTreeController> _logger;

        public PagTreeController(
            IPagTreeService pagTreeService,
            IOptions<PagTreeConfig> config,
            ILogger<PagTreeController> logger)
        {
            _pagTreeService = pagTreeService;
            _config = config.Value;
            _logger = logger;
        }

        [HttpPost("create-payment")]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { success = false, error = "Invalid request" });
                }

                _logger.LogInformation("[PagTree] CreatePayment endpoint - Amount: {Amount}, CPF: {CPF}",
                    request.Amount, request.Cpf);

                // Generate idempotency key (same pattern as Postman: 4000000-4999999)
                var random = new Random();
                var idempotencyKey = (random.Next(1000000) + 4000000).ToString();

                // Normalize CPF (remove formatting)
                var normalizedCpf = request.Cpf?.Replace(".", "").Replace("-", "") ?? string.Empty;

                var paymentRequest = new PagTreePaymentRequest
                {
                    Amount = (long)(request.Amount * 100), // Convert to cents
                    Currency = "BRL",
                    Method = new PagTreeMethod { Type = "qr_code" },
                    ExpirationMinutes = 30,
                    IdempotencyKey = idempotencyKey,
                    Customer = new PagTreeCustomer
                    {
                        Id = request.CustomerId ?? "7654321",
                        Name = request.CustomerName ?? "Customer",
                        Document = normalizedCpf
                    },
                    Meta = new PagTreeMeta
                    {
                        TestBehavior = new PagTreeTestBehavior
                        {
                            FinalStatus = "verified" // For testing purposes
                        }
                    }
                };

                var (success, data, error) = await _pagTreeService.CreatePaymentAsync(paymentRequest);

                if (success && data != null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            paymentId = data.Id,
                            status = data.Status,
                            amount = data.Amount,
                            currency = data.Currency,
                            expiresAt = data.ExpiresAt,
                            qrCode = data.Method?.QrCode,
                            method = data.Method?.Type
                        }
                    });
                }
                else if (error != null)
                {
                    _logger.LogError("[PagTree] Payment creation failed: {ErrorCode} - {ErrorMessage}",
                        error.Code, error.Message);

                    // Format error message with field details if available
                    var errorMessage = error.Message;
                    if (error.Details != null && error.Details.Count > 0)
                    {
                        var firstDetail = error.Details[0];
                        errorMessage = $"{firstDetail.Field} | {firstDetail.Message}";
                    }

                    return BadRequest(new
                    {
                        success = false,
                        error = errorMessage,
                        code = error.Code
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Failed to create payment"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PagTree] Exception in CreatePayment");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        [HttpGet("payment-status/{paymentId}")]
        public async Task<IActionResult> GetPaymentStatus(string paymentId)
        {
            try
            {
                if (string.IsNullOrEmpty(paymentId))
                {
                    return BadRequest(new { success = false, error = "Payment ID is required" });
                }

                _logger.LogInformation("[PagTree] GetPaymentStatus endpoint - PaymentId: {PaymentId}", paymentId);

                var (success, data, error) = await _pagTreeService.GetPaymentStatusAsync(paymentId);

                if (success && data != null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            paymentId = data.Id,
                            status = data.Status,
                            amount = data.Amount,
                            currency = data.Currency,
                            paidAmount = data.PaidAmount,
                            expiresAt = data.ExpiresAt,
                            createdAt = data.CreatedAt,
                            updatedAt = data.UpdatedAt,
                            qrCode = data.Method?.QrCode,
                            txId = data.TxId,
                            walletId = data.WalletId
                        }
                    });
                }
                else if (error != null)
                {
                    _logger.LogError("[PagTree] Get payment status failed: {ErrorCode} - {ErrorMessage}",
                        error.Code, error.Message);

                    return BadRequest(new
                    {
                        success = false,
                        error = error.Message,
                        code = error.Code
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Failed to get payment status"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PagTree] Exception in GetPaymentStatus");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }

    public class CreatePaymentRequest
    {
        public decimal Amount { get; set; }
        public string? Cpf { get; set; }
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
    }
}
