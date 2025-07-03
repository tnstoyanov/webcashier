using Microsoft.AspNetCore.Mvc;
using WebCashier.Models;

namespace WebCashier.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(ILogger<PaymentController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var model = new PaymentModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessPayment(PaymentModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            // Simulate payment processing
            var result = new PaymentResult
            {
                Success = true,
                Message = "Payment processed successfully!",
                TransactionId = Guid.NewGuid().ToString("N")[..8].ToUpper()
            };

            _logger.LogInformation("Payment processed: Amount={Amount}, Method={PaymentMethod}, TransactionId={TransactionId}", 
                model.Amount, model.PaymentMethod, result.TransactionId);

            return View("Success", result);
        }

        public IActionResult Success()
        {
            return View();
        }
    }
}
