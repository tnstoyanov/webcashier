using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    [Route("Nuvei")] // Ensure base path /Nuvei
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

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] decimal amount, [FromForm] string currency)
        {
            try
            {
                await _commLog.LogAsync("nuvei-inbound", new { provider = "Nuvei", action = "Create", amount, currency }, "nuvei");
                var baseUrl = GetBaseUrl();
                var form = _nuvei.BuildPaymentForm(new NuveiRequest(amount, currency, "12204834", "cashier"), baseUrl);
                var responseObj = new
                {
                    success = true,
                    formUrl = form.SubmitFormUrl,
                    fields = form.Fields.Select(f => new { f.Key, f.Value })
                };
                // Mask sensitive values before logging outbound response (client side form build)
                var masked = new {
                    responseObj.success,
                    responseObj.formUrl,
                    fields = form.Fields.Select(f => new {
                        f.Key,
                        Value = f.Key.Equals("checksum", StringComparison.OrdinalIgnoreCase) && f.Value != null
                            ? (f.Value.Length > 12 ? f.Value.Substring(0,6) + "..." + f.Value[^4..] : f.Value)
                            : f.Key.Contains("secret", StringComparison.OrdinalIgnoreCase) ? "***" : f.Value
                    })
                };
                await _commLog.LogAsync("nuvei-outbound-response", masked, "nuvei");
                return Json(responseObj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building Nuvei form");
                await _commLog.LogAsync("nuvei-error", new { provider = "Nuvei", action = "Create", message = ex.Message }, "nuvei");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [IgnoreAntiforgeryToken]
        [HttpPost("Callback")]
        [HttpGet("Callback")] // Allow GET temporarily for diagnostics
        public async Task<IActionResult> Callback()
        {
            _logger.LogInformation("Nuvei callback received");
            var form = Request.HasFormContentType ? Request.Form.ToDictionary(k => k.Key, v => string.Join(",", v.Value.ToArray())) : new Dictionary<string,string>();
            var query = Request.Query.ToDictionary(k => k.Key, v => string.Join(",", v.Value.ToArray()));
            await _commLog.LogAsync("nuvei-callback", new { provider = "Nuvei", method = Request.Method, path = Request.Path.ToString(), form, query, headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()) }, "nuvei");
            return Ok();
        }

        [HttpGet("Success")] public IActionResult Success() => View();
        [HttpGet("Error")] public IActionResult Error() => View();
        [HttpGet("Pending")] public IActionResult Pending() => View();

    // Diagnostic: quick ping to verify base route reachable
    [HttpGet("")]
    public IActionResult Index() => Ok(new { status = "nuvei-controller-ok", time = DateTime.UtcNow });

        private string GetBaseUrl()
        {
            var request = _http.HttpContext!.Request;
            return $"{request.Scheme}://{request.Host}";
        }
    }
}
