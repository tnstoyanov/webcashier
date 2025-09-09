using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    public class NuveiController : Controller
    {
        private readonly INuveiService _nuvei;
        private readonly ILogger<NuveiController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly ICommLogService _commLog;

        public NuveiController(INuveiService nuvei, ILogger<NuveiController> logger, IHttpContextAccessor http, ICommLogService commLog)
        {
            _nuvei = nuvei; _logger = logger; _http = http; _commLog = commLog;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] decimal amount, [FromForm] string currency)
        {
            try
            {
                await _commLog.LogAsync("inbound", new { provider = "Nuvei", action = "Create", amount, currency }, "nuvei");
                var baseUrl = GetBaseUrl();
                var form = _nuvei.BuildPaymentForm(new NuveiRequest(amount, currency, "12204834", "cashier"), baseUrl);
                var responseObj = new
                {
                    success = true,
                    formUrl = form.SubmitFormUrl,
                    fields = form.Fields.Select(f => new { f.Key, f.Value })
                };
                await _commLog.LogAsync("outbound", responseObj, "nuvei");
                return Json(responseObj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building Nuvei form");
                await _commLog.LogAsync("error", new { provider = "Nuvei", action = "Create", message = ex.Message }, "nuvei");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Callback()
        {
            _logger.LogInformation("Nuvei callback received");
            var form = Request.HasFormContentType ? Request.Form.ToDictionary(k => k.Key, v => string.Join(",", v.Value.ToArray())) : new Dictionary<string,string>();
            var query = Request.Query.ToDictionary(k => k.Key, v => string.Join(",", v.Value.ToArray()));
            await _commLog.LogAsync("callback", new { provider = "Nuvei", form, query }, "nuvei");
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
