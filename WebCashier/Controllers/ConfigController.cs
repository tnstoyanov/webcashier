using Microsoft.AspNetCore.Mvc;
using WebCashier.Services;

namespace WebCashier.Controllers
{
    public class ConfigController : Controller
    {
        private readonly IRuntimeConfigStore _runtime;
        private readonly IConfiguration _configuration;

        public ConfigController(IRuntimeConfigStore runtime, IConfiguration configuration)
        {
            _runtime = runtime;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Nuvei
        [HttpGet]
        public IActionResult Nuvei()
        {
            var model = Prefill(new[] { "Nuvei:merchant_id","Nuvei:merchant_site_id","Nuvei:secret_key","Nuvei:endpoint" });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveNuvei([FromForm] Dictionary<string,string?> form)
        {
            var allowed = new[] { "Nuvei:merchant_id","Nuvei:merchant_site_id","Nuvei:secret_key","Nuvei:endpoint" };
            var before = allowed.ToDictionary(k => k, k => _runtime.Get(k));
            SaveAllowed(form, allowed);
            var after = allowed.ToDictionary(k => k, k => _runtime.Get(k));
            // Simple diff logging to console for diagnostics (could integrate with comm log later)
            Console.WriteLine("[Nuvei Config Save] Incoming form values:");
            foreach (var kv in form) Console.WriteLine($"  {kv.Key}={(string.IsNullOrWhiteSpace(kv.Value)?"<empty>":kv.Value)}");
            Console.WriteLine("[Nuvei Config Save] Before -> After runtime values:");
            foreach (var k in allowed)
            {
                Console.WriteLine($"  {k}: '{before[k] ?? "<null>"}' -> '{after[k] ?? "<null>"}'");
            }

            // Attempt to persist into appsettings.json (best-effort; may fail in read-only container)
            try
            {
                var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (System.IO.File.Exists(appSettingsPath))
                {
                    var jsonText = System.IO.File.ReadAllText(appSettingsPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                    var root = doc.RootElement.Clone();
                    // Build mutable dictionary representation
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText) ?? new();
                    // Nuvei typically not present; create/update section
                    var nuveiSection = new Dictionary<string,string?>
                    {
                        { "merchant_id", after["Nuvei:merchant_id"] },
                        { "merchant_site_id", after["Nuvei:merchant_site_id"] },
                        { "secret_key", after["Nuvei:secret_key"] },
                        { "endpoint", after["Nuvei:endpoint"] }
                    };
                    dict["Nuvei"] = nuveiSection;
                    var newJson = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    var backupPath = appSettingsPath + ".bak";
                    try { System.IO.File.Copy(appSettingsPath, backupPath, true); } catch { }
                    System.IO.File.WriteAllText(appSettingsPath, newJson);
                    Console.WriteLine("[Nuvei Config Save] appsettings.json updated with Nuvei section.");
                }
                else
                {
                    Console.WriteLine("[Nuvei Config Save] appsettings.json not found for persistence");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Nuvei Config Save] Failed to persist to appsettings.json: " + ex.Message);
            }
            TempData["Saved"] = true;
            return RedirectToAction(nameof(Nuvei));
        }

        // Smilepayz
        [HttpGet]
        public IActionResult Smilepayz()
        {
            var model = Prefill(new[]
            {
                "Smilepayz:Endpoint","Smilepayz:PartnerId","Smilepayz:MerchantSecret",
                "Smilepayz:RSAPublicKey","Smilepayz:RSAPrivateKey","Smilepayz:RedirectUrl",
                "Smilepayz:CallbackUrl","Smilepayz:PaymentMethod","Smilepayz:Currency","Smilepayz:MerchantName"
            });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveSmilepayz([FromForm] Dictionary<string, string?> form)
        {
            var allowed = new[]
            {
                "Smilepayz:Endpoint","Smilepayz:PartnerId","Smilepayz:MerchantSecret",
                "Smilepayz:RSAPublicKey","Smilepayz:RSAPrivateKey","Smilepayz:RedirectUrl",
                "Smilepayz:CallbackUrl","Smilepayz:PaymentMethod","Smilepayz:Currency","Smilepayz:MerchantName"
            };
            SaveAllowed(form, allowed);
            TempData["Saved"] = true;
            return RedirectToAction(nameof(Smilepayz));
        }

        // Praxis
        [HttpGet]
        public IActionResult Praxis()
        {
            var model = Prefill(new[]
            {
                "Praxis:Endpoint","Praxis:MerchantId","Praxis:ApplicationKey","Praxis:MerchantSecret",
                "Praxis:TransactionType","Praxis:Gateway","Praxis:NotificationUrl","Praxis:ReturnUrl","Praxis:Version"
            });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePraxis([FromForm] Dictionary<string, string?> form)
        {
            var allowed = new[]
            {
                "Praxis:Endpoint","Praxis:MerchantId","Praxis:ApplicationKey","Praxis:MerchantSecret",
                "Praxis:TransactionType","Praxis:Gateway","Praxis:NotificationUrl","Praxis:ReturnUrl","Praxis:Version"
            };
            SaveAllowed(form, allowed);
            TempData["Saved"] = true;
            return RedirectToAction(nameof(Praxis));
        }

        // Luxtak
        [HttpGet]
        public IActionResult Luxtak()
        {
            var model = Prefill(new[]
            {
                "Luxtak:Endpoint","Luxtak:AppId","Luxtak:AuthToken","Luxtak:NotifyUrl","Luxtak:ReturnUrl"
            });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveLuxtak([FromForm] Dictionary<string, string?> form)
        {
            var allowed = new[]
            {
                "Luxtak:Endpoint","Luxtak:AppId","Luxtak:AuthToken","Luxtak:NotifyUrl","Luxtak:ReturnUrl"
            };
            SaveAllowed(form, allowed);
            TempData["Saved"] = true;
            return RedirectToAction(nameof(Luxtak));
        }

        private IDictionary<string, string?> Prefill(IEnumerable<string> keys)
        {
            var dict = new Dictionary<string, string?>();
            foreach (var k in keys)
            {
                dict[k] = _runtime.Get(k) ?? _configuration[k];
            }
            return dict;
        }

        private void SaveAllowed(Dictionary<string, string?> form, IEnumerable<string> allowed)
        {
            var toSave = new Dictionary<string, string?>();
            var removed = new List<string>();
            foreach (var k in allowed)
            {
                if (form.TryGetValue(k, out var v))
                {
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        if (_runtime.Remove(k)) removed.Add(k);
                    }
                    else
                    {
                        toSave[k] = v;
                    }
                }
            }
            if (toSave.Count > 0) _runtime.SetRange(toSave);
            if (removed.Count > 0)
            {
                TempData["Cleared"] = string.Join(",", removed);
            }
        }
    }
}
