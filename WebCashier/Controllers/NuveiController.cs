using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    public class NuveiController : Controller
    {
        private readonly INuveiService _nuvei;
        private readonly ILogger<NuveiController> _logger;
        private readonly IHttpContextAccessor _http;

        public NuveiController(INuveiService nuvei, ILogger<NuveiController> logger, IHttpContextAccessor http)
        {
            _nuvei = nuvei; _logger = logger; _http = http;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([FromForm] decimal amount, [FromForm] string currency)
        {
            try
            {
                var baseUrl = GetBaseUrl();
                var form = _nuvei.BuildPaymentForm(new NuveiRequest(amount, currency, "12204834", "cashier"), baseUrl);
                return Json(new
                {
                    success = true,
                    formUrl = form.SubmitFormUrl,
                    fields = form.Fields.Select(f => new { f.Key, f.Value })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building Nuvei form");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Callback()
        {
            _logger.LogInformation("Nuvei callback received");
            return Ok();
        }

        public IActionResult Success() => View();
        public IActionResult Error() => View();
        public IActionResult Pending() => View();

        private string GetBaseUrl()
        {
            var request = _http.HttpContext!.Request;
            return $"{request.Scheme}://{request.Host}";
        }
    }
}
