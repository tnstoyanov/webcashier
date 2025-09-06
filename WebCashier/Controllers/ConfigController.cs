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
            SaveAllowed(form, allowed);
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
            foreach (var k in allowed)
            {
                if (form.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    toSave[k] = v;
            }
            _runtime.SetRange(toSave);
        }
    }
}
