using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace WebCashier.Services;

public class SwiftGoldPayService : ISwiftGoldPayService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ICommLogService _log;
    private readonly JsonSerializerOptions _jsonOpts;

    public SwiftGoldPayService(HttpClient http, IConfiguration cfg, ICommLogService log)
    {
        _http = http;
        _cfg = cfg;
        _log = log;
        _jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var baseUrl = _cfg["SwiftGoldPay:BaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl))
        {
            _http.BaseAddress = new Uri(baseUrl!);
        }
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(40);
    }

    private (string apiId, string clientId, string clientSecret, string clientRefId) GetCreds()
    {
        return (
            _cfg["SwiftGoldPay:ApiId"] ?? string.Empty,
            _cfg["SwiftGoldPay:ClientId"] ?? string.Empty,
            _cfg["SwiftGoldPay:ClientSecret"] ?? string.Empty,
            _cfg["SwiftGoldPay:ClientRefId"] ?? string.Empty
        );
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string ComputeHmacSha256Hex(string message, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var sb = new StringBuilder(sig.Length * 2);
        foreach (var b in sig) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private void ApplyDefaultHeaders(HttpRequestMessage req, string? bearer = null)
    {
        var (apiId, clientId, clientSecret, clientRefId) = GetCreds();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        // Signature rule: not specified in full; infer pattern: SHA256(client_id+client_secret+client_ref_id+x-apigw-api-id+x-timestamp+method+path)
        // We'll use method and path only as per examples; if spec differs, adjust easily.
        // Handle relative URIs safely (PathAndQuery throws for relative URIs)
        Uri? fullUri = null;
        if (req.RequestUri != null)
        {
            if (req.RequestUri.IsAbsoluteUri)
            {
                fullUri = req.RequestUri;
            }
            else if (_http.BaseAddress != null)
            {
                fullUri = new Uri(_http.BaseAddress, req.RequestUri.ToString());
            }
        }
            string path; 
            if (req.RequestUri is null)
            {
                path = "/";
            }
            else if (req.RequestUri.IsAbsoluteUri)
            {
                path = req.RequestUri.PathAndQuery;
            }
            else
            {
                var s = req.RequestUri.OriginalString;
                if (!s.StartsWith("/")) s = "/" + s;
                path = s;
            }
        if (fullUri != null && fullUri.IsAbsoluteUri)
        {
            // Use AbsolutePath + Query to avoid PathAndQuery on any potential relative URI
            path = fullUri.AbsolutePath + fullUri.Query;
        }
        else
        {
            var raw = req.RequestUri?.ToString() ?? string.Empty;
            // Ensure it starts with '/'
            path = string.IsNullOrEmpty(raw) ? "/" : (raw.StartsWith('/') ? raw : "/" + raw);
        }
        var method = req.Method.Method.ToUpperInvariant();
        var sigPayload = string.Concat(clientId, clientSecret, clientRefId, apiId, ts, method, path);
        var signature = ComputeSha256Hex(sigPayload);

        req.Headers.Remove("client_id");
        req.Headers.Remove("client_secret");
        req.Headers.Remove("client_ref_id");
        req.Headers.Remove("x-apigw-api-id");
        req.Headers.Remove("x-timestamp");
        req.Headers.Remove("x-signature");
        req.Headers.Add("client_id", clientId);
        // Include client_secret on all calls (token and subsequent endpoints)
        req.Headers.Add("client_secret", clientSecret);
        req.Headers.Add("client_ref_id", clientRefId);
        req.Headers.Add("x-apigw-api-id", apiId);
        req.Headers.Add("x-timestamp", ts);
        req.Headers.Add("x-signature", signature);
        if (!string.IsNullOrEmpty(bearer))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
    }

    // Token endpoint signature per Postman script:
    // x-signature = HMAC_SHA256(client_id + timestamp + JSON.stringify({}), client_secret)
    private void ApplyTokenHeaders(HttpRequestMessage req)
    {
        var (apiId, clientId, clientSecret, clientRefId) = GetCreds();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        const string bodyForSigning = "{}"; // per spec, request body used for signing is an empty JSON object
        var dataToSign = string.Concat(clientId, ts, bodyForSigning);
        var signature = ComputeHmacSha256Hex(dataToSign, clientSecret);

        req.Headers.Remove("client_id");
        req.Headers.Remove("client_secret");
        req.Headers.Remove("client_ref_id");
        req.Headers.Remove("x-apigw-api-id");
        req.Headers.Remove("x-timestamp");
        req.Headers.Remove("x-signature");

        req.Headers.Add("client_id", clientId);
        req.Headers.Add("client_secret", clientSecret);
        req.Headers.Add("client_ref_id", clientRefId);
        req.Headers.Add("x-apigw-api-id", apiId);
        req.Headers.Add("x-timestamp", ts);
        req.Headers.Add("x-signature", signature);
        // Content-Type is set on the HttpContent below
    }

    public async Task<(bool ok, string? token, string? error, object? raw)> GetTokenAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/oauth/v1.0/partner/token");
        ApplyTokenHeaders(req);
        // Intentionally send a zero-length body with Content-Type: application/json (no charset)
        var empty = new ByteArrayContent(Array.Empty<byte>());
        empty.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Content = empty;
        // For troubleshooting, surface timestamp and signature (no secrets)
        var ts = req.Headers.TryGetValues("x-timestamp", out var tsVals) ? tsVals.FirstOrDefault() : null;
        var sig = req.Headers.TryGetValues("x-signature", out var sigVals) ? sigVals.FirstOrDefault() : null;
        await _log.LogAsync("SwiftGoldPay.Token.Request", new { url = req.RequestUri!.ToString(), method = req.Method.Method, timestamp = ts, signature = sig });
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        await _log.LogAsync("SwiftGoldPay.Token.Response", new { status = (int)resp.StatusCode, text });
        try
        {
            var json = JsonNode.Parse(text) as JsonObject;
            var statusCode = json?["status"]?["code"]?.GetValue<string>();
            var token = json?["data"]?["token"]?.GetValue<string>();
            var msg = json?["status"]?["message"]?.GetValue<string>();
            var ok = resp.IsSuccessStatusCode && statusCode == "S-2000" && !string.IsNullOrEmpty(token);
            return (ok, token, ok ? null : $"{statusCode} {msg}", json);
        }
        catch
        {
            return (false, null, "Invalid JSON from token endpoint", text);
        }
    }

    public async Task<(bool ok, JsonArray? banks, string? error, object? raw)> GetBanksAsync(string country, string bearerToken, CancellationToken ct = default)
    {
        var path = $"/api/opay/v1.0/partner/bank?country={Uri.EscapeDataString(country)}";
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyDefaultHeaders(req, bearerToken);
        // Ensure Content-Type header like Postman; attach a zero-length body to avoid proxies stripping headers
        var empty = new ByteArrayContent(Array.Empty<byte>());
        empty.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Content = empty;
        // Debug log timestamp and signature
        var ts = req.Headers.TryGetValues("x-timestamp", out var tsVals) ? tsVals.FirstOrDefault() : null;
        var sig = req.Headers.TryGetValues("x-signature", out var sigVals) ? sigVals.FirstOrDefault() : null;
        await _log.LogAsync("SwiftGoldPay.Banks.Request", new { url = req.RequestUri!.ToString(), timestamp = ts, signature = sig });
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        await _log.LogAsync("SwiftGoldPay.Banks.Response", new { status = (int)resp.StatusCode, text });
        try
        {
            var json = JsonNode.Parse(text) as JsonObject;
            var statusCode = json?["status"]?["code"]?.GetValue<string>();
            var msg = json?["status"]?["message"]?.GetValue<string>();
            var data = json?["data"] as JsonArray;
            var ok = resp.IsSuccessStatusCode && statusCode == "S-2000" && data != null;
            return (ok, data, ok ? null : $"{statusCode} {msg}", json);
        }
        catch
        {
            return (false, null, "Invalid JSON from banks endpoint", text);
        }
    }

    public async Task<(bool ok, string? customerId, string? code, string? message, object? raw)> CreateOrGetCustomerAsync(
        string currency,
        string nameEn,
        string nameTh,
        string email,
        string bankCode,
        string bankAccountNameEn,
        string bankAccountNameTh,
        string bankAccountNumber,
        string customerRef,
        string bearerToken,
        CancellationToken ct = default)
    {
        var path = "/api/opay/v1.0/partner/customers";
        var req = new HttpRequestMessage(HttpMethod.Post, path);
        ApplyDefaultHeaders(req, bearerToken);
        var body = new
        {
            currency,
            name_th = nameTh,
            name_en = nameEn,
            email,
            bank_code = bankCode,
            bank_account_name_th = bankAccountNameTh,
            bank_account_name_en = bankAccountNameEn,
            bank_account_number = bankAccountNumber,
            customer_ref = customerRef
        };
        var jsonBody = JsonSerializer.Serialize(body, _jsonOpts);
        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        await _log.LogAsync("SwiftGoldPay.Customer.Request", new { url = req.RequestUri!.ToString(), body });
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        await _log.LogAsync("SwiftGoldPay.Customer.Response", new { status = (int)resp.StatusCode, text });
        try
        {
            var json = JsonNode.Parse(text) as JsonObject;
            var statusCode = json?["status"]?["code"]?.GetValue<string>();
            var msg = json?["status"]?["message"]?.GetValue<string>();
            var customerId = json?["data"]?["customer_id"]?.GetValue<string>();
            var ok = resp.IsSuccessStatusCode && (statusCode == "S-2000" || statusCode == "S-4004");
            return (ok, customerId, statusCode, msg, json);
        }
        catch
        {
            return (false, null, null, "Invalid JSON from customers endpoint", text);
        }
    }

    public async Task<(bool ok, string? depositId, object? raw, string? code, string? message)> CreateDepositAsync(
        string customerId,
        decimal amount,
        string refNo,
        string bearerToken,
        CancellationToken ct = default)
    {
        var path = "/api/opay/v1.0/partner/deposits";
        var req = new HttpRequestMessage(HttpMethod.Post, path);
        ApplyDefaultHeaders(req, bearerToken);
        var body = new { customer_id = customerId, amount, ref_no = refNo };
        var jsonBody = JsonSerializer.Serialize(body, _jsonOpts);
        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        await _log.LogAsync("SwiftGoldPay.Deposit.Request", new { url = req.RequestUri!.ToString(), body });
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        await _log.LogAsync("SwiftGoldPay.Deposit.Response", new { status = (int)resp.StatusCode, text });
        try
        {
            var json = JsonNode.Parse(text) as JsonObject;
            var statusCode = json?["status"]?["code"]?.GetValue<string>();
            var msg = json?["status"]?["message"]?.GetValue<string>();
            var depositId = json?["data"]?["deposit_id"]?.GetValue<string>();
            var ok = resp.IsSuccessStatusCode && statusCode == "S-2000";
            return (ok, depositId, json, statusCode, msg);
        }
        catch
        {
            return (false, null, text, null, "Invalid JSON from deposits endpoint");
        }
    }
}
