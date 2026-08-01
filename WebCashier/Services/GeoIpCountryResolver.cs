using System.Net;

namespace WebCashier.Services
{
    public class GeoIpCountryResolver : IGeoIpCountryResolver
    {
        private readonly ILogger<GeoIpCountryResolver> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public GeoIpCountryResolver(
            ILogger<GeoIpCountryResolver> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<string> ResolveCountryCodeAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            var fallback = NormalizeCountryCode(_configuration["Nuvei:default_country"]) ?? "US";

            // Prefer trusted edge headers when present.
            var headerCountry = NormalizeCountryCode(
                httpContext.Request.Headers["CF-IPCountry"].FirstOrDefault()
                ?? httpContext.Request.Headers["X-Country-Code"].FirstOrDefault());

            if (!string.IsNullOrWhiteSpace(headerCountry) && headerCountry != "XX")
            {
                _logger.LogInformation("GeoIP country resolved from header: {Country}", headerCountry);
                return headerCountry;
            }

            var ip = GetClientIp(httpContext);
            if (string.IsNullOrWhiteSpace(ip))
            {
                _logger.LogWarning("GeoIP country fallback used (missing client IP): {Country}", fallback);
                return fallback;
            }

            if (IsLocalOrPrivateIp(ip))
            {
                _logger.LogInformation("GeoIP country fallback used for local/private IP {IP}: {Country}", ip, fallback);
                return fallback;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(3);

                // ipapi returns plain two-letter country code for this endpoint.
                var url = $"https://ipapi.co/{Uri.EscapeDataString(ip)}/country/";
                var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GeoIP lookup failed for {IP}: HTTP {Status}", ip, (int)response.StatusCode);
                    return fallback;
                }

                var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                var resolved = NormalizeCountryCode(body);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    _logger.LogInformation("GeoIP country resolved from IP {IP}: {Country}", ip, resolved);
                    return resolved;
                }

                _logger.LogWarning("GeoIP lookup returned invalid country for {IP}: {Value}", ip, body);
                return fallback;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GeoIP lookup exception for IP {IP}; using fallback {Country}", ip, fallback);
                return fallback;
            }
        }

        private static string? NormalizeCountryCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var code = value.Trim().ToUpperInvariant();
            if (code.Length != 2) return null;
            return code;
        }

        private static string? GetClientIp(HttpContext httpContext)
        {
            var xff = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xff))
            {
                var first = xff.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }

            var xri = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xri)) return xri.Trim();

            return httpContext.Connection.RemoteIpAddress?.ToString();
        }

        private static bool IsLocalOrPrivateIp(string ip)
        {
            if (!IPAddress.TryParse(ip, out var address))
            {
                return true;
            }

            if (IPAddress.IsLoopback(address)) return true;

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                if (bytes[0] == 10) return true;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                if (bytes[0] == 169 && bytes[1] == 254) return true;
            }

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
            }

            return false;
        }
    }
}
