using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
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
    private readonly IRuntimeConfigStore _runtime;
    private readonly IConfiguration _configuration;

        public NuveiController(INuveiService nuvei, ILogger<NuveiController> logger, IHttpContextAccessor http, ICommLogService commLog, IRuntimeConfigStore runtime, IConfiguration configuration)
        {
            _nuvei = nuvei; _logger = logger; _http = http; _commLog = commLog; _runtime = runtime; _configuration = configuration;
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
        [HttpGet("Callback")] // GET remains for diagnostics; Nuvei should POST
        public async Task<IActionResult> Callback()
        {
            _logger.LogInformation("Nuvei callback hit: method={Method} contentType={CT}", Request.Method, Request.ContentType);

            Dictionary<string,string> formData = new();
            if (HttpMethods.IsPost(Request.Method) && Request.HasFormContentType)
            {
                var posted = await Request.ReadFormAsync();
                foreach (var kv in posted)
                {
                    formData[kv.Key] = string.Join(",", kv.Value.ToArray());
                }
            }
            else if (Request.HasFormContentType) // fallback (shouldn't normally hit because of ReadFormAsync above)
            {
                foreach (var f in Request.Form)
                    formData[f.Key] = string.Join(",", f.Value.ToArray());
            }

            var query = Request.Query.ToDictionary(k => k.Key, v => string.Join(",", v.Value.ToArray()));

            // Redaction rules
            string Mask(string? v) => string.IsNullOrEmpty(v) ? v ?? string.Empty : v.Length <= 6 ? new string('*', v.Length) : v.Substring(0,3) + new string('*', v.Length-7) + v[^4..];
            bool ShouldMask(string key) => key.Contains("card", StringComparison.OrdinalIgnoreCase) ||
                                           key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                                           key.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                           key.Equals("uniqueCC", StringComparison.OrdinalIgnoreCase) ||
                                           key.Equals("PAR", StringComparison.OrdinalIgnoreCase);
            bool ShouldPartial(string key) => key.EndsWith("checksum", StringComparison.OrdinalIgnoreCase);

            var redacted = formData.ToDictionary(
                k => k.Key,
                v => ShouldMask(v.Key) ? Mask(v.Value) : ShouldPartial(v.Key) && v.Value.Length > 12 ? v.Value.Substring(0,6)+"..."+v.Value[^4..] : v.Value
            );

            // Attempt simplistic checksum verification if secret key present
            string? secretKey = _runtime.Get("Nuvei:secret_key") ?? _configuration["Nuvei:secret_key"]; // same naming as request side
            string? responseChecksum = formData.TryGetValue("responsechecksum", out var rc) ? rc : null;
            bool? checksumValid = null;
            if (!string.IsNullOrEmpty(secretKey) && !string.IsNullOrEmpty(responseChecksum))
            {
                try
                {
                    // Heuristic: concatenate all form values except any checksum fields
                    var sb = new StringBuilder();
                    foreach (var kv in formData.Where(kv => !kv.Key.EndsWith("checksum", StringComparison.OrdinalIgnoreCase)))
                        sb.Append(kv.Value);
                    var candidate = Sha256Hex(secretKey + sb.ToString());
                    checksumValid = string.Equals(candidate, responseChecksum, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nuvei response checksum verification failed internally");
                }
            }

            await _commLog.LogAsync("nuvei-callback", new {
                provider = "Nuvei",
                method = Request.Method,
                path = Request.Path.ToString(),
                formCount = formData.Count,
                checksumPresent = responseChecksum != null,
                checksumValid,
                form = redacted,
                query,
                headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
            }, "nuvei");
            return Ok(new { status = "received" });
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
            var scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && !string.IsNullOrEmpty(proto) ? proto.ToString() : request.Scheme;
            var forceHttps = Environment.GetEnvironmentVariable("REQUIRE_HTTPS_URLS");
            if (string.Equals(forceHttps, "true", StringComparison.OrdinalIgnoreCase) && !string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
                scheme = "https";
            return $"{scheme}://{request.Host}";
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }
    }
}
