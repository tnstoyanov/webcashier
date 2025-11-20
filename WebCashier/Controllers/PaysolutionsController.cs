using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;
using WebCashier.Models.Paysolutions;
using Microsoft.Extensions.Options;

namespace WebCashier.Controllers
{
    public class PaysolutionsController : Controller
    {
        private readonly IPaysolutionsService _paysolutionsService;
        private readonly PaysolutionsConfig _config;
        private readonly ILogger<PaysolutionsController> _logger;

        public PaysolutionsController(
            IPaysolutionsService paysolutionsService,
            IOptions<PaysolutionsConfig> config,
            ILogger<PaysolutionsController> logger)
        {
            _paysolutionsService = paysolutionsService;
            _config = config.Value;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateDeposit(decimal amount, string? customerName, string? customerEmail)
        {
            try
            {
                // Generate reference number (3000000 - 4999999 range as per Postman script)
                var random = new Random();
                var referenceNo = (random.Next(2000000) + 3000000).ToString();

                var request = new PaysolutionsDepositRequest
                {
                    MerchantID = _config.MerchantID,
                    ProductDetail = "PromptPay QR",
                    CustomerEmail = customerEmail ?? "customer@example.com",
                    CustomerName = customerName ?? "Customer",
                    Total = amount,
                    ReferenceNo = referenceNo
                };

                var response = await _paysolutionsService.CreateDepositAsync(request);

                if (response.Status == "success" && response.Data != null)
                {
                    // Check if this is an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        // Return the QR data directly in JSON for AJAX requests
                        return Json(new { 
                            success = true, 
                            data = new {
                                image = response.Data.Image,
                                orderNo = response.Data.OrderNo,
                                referenceNo = response.Data.ReferenceNo,
                                total = response.Data.Total,
                                orderdatetime = response.Data.Orderdatetime,
                                expiredate = response.Data.Expiredate
                            }
                        });
                    }
                    else
                    {
                        // For non-AJAX requests, store in TempData and redirect
                        TempData["PaysolutionsQRData"] = System.Text.Json.JsonSerializer.Serialize(response.Data);
                        return RedirectToAction("ShowQR");
                    }
                }
                else
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, error = response.Message ?? "Failed to create deposit" });
                    }
                    else
                    {
                        TempData["ErrorMessage"] = response.Message ?? "Failed to create deposit";
                        return RedirectToAction("Index", "Payment");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Paysolutions] Error in CreateDeposit");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = ex.Message });
                }
                else
                {
                    TempData["ErrorMessage"] = ex.Message;
                    return RedirectToAction("Index", "Payment");
                }
            }
        }

        public IActionResult ShowQR()
        {
            // Retrieve QR data from TempData
            var qrDataJson = TempData["PaysolutionsQRData"] as string;
            if (string.IsNullOrEmpty(qrDataJson))
            {
                TempData["ErrorMessage"] = "QR data not found";
                return RedirectToAction("Index", "Payment");
            }

            var qrData = System.Text.Json.JsonSerializer.Deserialize<PaysolutionsData>(qrDataJson);
            if (qrData == null)
            {
                TempData["ErrorMessage"] = "Failed to load QR data";
                return RedirectToAction("Index", "Payment");
            }

            return View(qrData);
        }
    }
}
