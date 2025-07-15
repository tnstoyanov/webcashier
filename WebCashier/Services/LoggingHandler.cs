using System.Text;

namespace WebCashier.Services
{
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHandler> _logger;

        public LoggingHandler(ILogger<LoggingHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Log the request
            await LogRequest(request);

            // Send the request and get the response
            var response = await base.SendAsync(request, cancellationToken);

            // Log the response
            await LogResponse(response);

            return response;
        }

        private async Task LogRequest(HttpRequestMessage request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== HTTP REQUEST ===");
            sb.AppendLine($"{request.Method} {request.RequestUri} HTTP/1.1");
            sb.AppendLine($"Host: {request.RequestUri?.Host}");

            // Log headers
            foreach (var header in request.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            // Log content headers and body
            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                sb.AppendLine($"Content-Length: {request.Content.Headers.ContentLength}");
                sb.AppendLine();

                var content = await request.Content.ReadAsStringAsync();
                sb.AppendLine(content);
            }

            sb.AppendLine("=== END REQUEST ===");
            _logger.LogInformation(sb.ToString());
        }

        private async Task LogResponse(HttpResponseMessage response)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== HTTP RESPONSE ===");
            sb.AppendLine($"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}");

            // Log headers
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            // Log content headers and body
            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                sb.AppendLine();

                var content = await response.Content.ReadAsStringAsync();
                sb.AppendLine(content);
            }

            sb.AppendLine("=== END RESPONSE ===");
            _logger.LogInformation(sb.ToString());
        }
    }
}
