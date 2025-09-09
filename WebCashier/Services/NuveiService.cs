using System.Security.Cryptography;
using System.Text;

namespace WebCashier.Services
{
    public class NuveiService : INuveiService
    {
        private readonly IRuntimeConfigStore _runtime;
        private readonly IConfiguration _config;
        private readonly ILogger<NuveiService> _logger;
        private readonly ICommLogService _commLog;
        private const string DefaultPppUrl = "https://ppp-test.safecharge.com/ppp/purchase.do";

        public NuveiService(IRuntimeConfigStore runtime, IConfiguration config, ILogger<NuveiService> logger, ICommLogService commLog)
        {
            _runtime = runtime; _config = config; _logger = logger; _commLog = commLog;
        }

        public NuveiFormResponse BuildPaymentForm(NuveiRequest req, string baseUrl)
        {
            var merchantId = Get("Nuvei:merchant_id");
            var merchantSiteId = Get("Nuvei:merchant_site_id");
            var secretKey = Get("Nuvei:secret_key");
            var endpoint = Get("Nuvei:endpoint") ?? DefaultPppUrl;

            if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(merchantSiteId) || string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("Nuvei configuration incomplete");

            var transactionRef = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Random.Shared.Next(1000,9999);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd.HH:mm:ss");
            var userToken = req.UserTokenId;
            var amountStr = req.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            // URLs
            // Enforce https externally
            string ForceHttps(string url) => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "https://" + url.Substring("http://".Length) : url;
            string notifyUrl = ForceHttps(Combine(baseUrl, "/Nuvei/Callback"));
            string successUrl = ForceHttps(Combine(baseUrl, "/Nuvei/Success"));
            string errorUrl = ForceHttps(Combine(baseUrl, "/Nuvei/Error"));
            string pendingUrl = ForceHttps(Combine(baseUrl, "/Nuvei/Pending"));
            string backUrl = ForceHttps(Combine(baseUrl, "/Payment?paymentMethod=gpay"));

            var fields = new List<NuveiFormField>
            {
                F("merchant_id", merchantId!),
                F("merchant_site_id", merchantSiteId!),
                F("time_stamp", timestamp.Replace("-", "-")),
                F("currency", req.Currency),
                F("merchantLocale", "en_US"),
                F("userid", userToken),
                F("merchant_unique_id", transactionRef),
                F("item_name_1", req.ItemName),
                F("item_number_1", "1"),
                F("item_amount_1", amountStr),
                F("item_quantity_1", "1"),
                F("total_amount", amountStr),
                F("user_token_id", userToken),
                F("version", "4.0.0"),
                F("encoding", "UTF-8"),
                F("payment_method", "ppp_GooglePay"),
                F("payment_method_mode", "filter"),
                F("notify_url", notifyUrl),
                F("success_url", successUrl),
                F("error_url", errorUrl),
                F("pending_url", pendingUrl),
                F("back_url", backUrl)
            };

            // Checksum: concatenate values (no spaces) in exact order, prepend secret key, sha256 hex lowercase
            var concatValues = new StringBuilder();
            foreach (var f in fields)
            {
                concatValues.Append(f.Value ?? string.Empty);
            }
            var toHash = secretKey + concatValues.ToString();
            var checksum = Sha256Hex(toHash);
            fields.Add(F("checksum", checksum));

            _logger.LogInformation("Nuvei form built with transactionRef {Ref} checksum {Checksum}", transactionRef, checksum);

            // Enhanced redaction rules
            bool NeedsRedaction(string key) => key.Contains("secret", StringComparison.OrdinalIgnoreCase);
            bool NeedsMask(string key) => key.Equals("userid", StringComparison.OrdinalIgnoreCase) ||
                                          key.Equals("user_token_id", StringComparison.OrdinalIgnoreCase) ||
                                          key.Equals("merchant_unique_id", StringComparison.OrdinalIgnoreCase);
            string Mask(string? value)
            {
                if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
                if (value.Length <= 6) return new string('*', value.Length);
                return value.Substring(0, 3) + new string('*', value.Length - 7) + value[^4..];
            }

            var loggedFields = fields.Select(f => new {
                f.Key,
                Value = NeedsRedaction(f.Key) ? "***" : NeedsMask(f.Key) ? Mask(f.Value) : f.Key == "checksum" && f.Value != null ? (f.Value.Length > 12 ? f.Value.Substring(0,6) + "..." + f.Value[^4..] : f.Value) : f.Value
            });

            _ = _commLog.LogAsync("nuvei-outbound", new {
                provider = "Nuvei",
                transactionRef,
                endpoint,
                fields = loggedFields
            }, "nuvei");
            return new NuveiFormResponse(endpoint, fields);
        }

        private static NuveiFormField F(string k, string v) => new(k, v);
        private string? Get(string key) => _runtime.Get(key) ?? _config[key];
        private static string Combine(string baseUrl, string path) => baseUrl.TrimEnd('/') + path;
        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }
    }
}
