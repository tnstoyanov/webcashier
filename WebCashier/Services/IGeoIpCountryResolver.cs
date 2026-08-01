namespace WebCashier.Services
{
    public interface IGeoIpCountryResolver
    {
        Task<string> ResolveCountryCodeAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
    }
}
