using Microsoft.AspNetCore.Mvc.Filters;

namespace WebCashier.Filters
{
    /// <summary>
    /// Disables antiforgery token validation for AJAX requests (identified by X-Requested-With header).
    /// This is needed for Render.com where DataProtection key ring is ephemeral and can't persist
    /// antiforgery tokens across application restarts.
    /// </summary>
    public class SkipAntiforgeryForAjaxFilter : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if this is an AJAX request
            var isAjax = context.HttpContext.Request.Headers.ContainsKey("X-Requested-With") &&
                         context.HttpContext.Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

            if (isAjax)
            {
                // Mark this so IAntiforgery can skip validation
                context.HttpContext.Items["X-Requested-With"] = "XMLHttpRequest";
            }

            await Task.CompletedTask;
        }
    }
}
