using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
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
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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

    private void ApplyDefaultHeaders(HttpRequestMessage req, string? bearer = null, string? bodyForSigning = null)
    {
        var (apiId, clientId, clientSecret, clientRefId) = GetCreds();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        // Per Postman script for non-OAuth calls,
        // x-signature = HMAC_SHA256(client_id + timestamp + BODY, client_secret)
        // BODY rules: GET/HEAD -> '{}'; JSON POST -> minified JSON body
        var bodySigned = bodyForSigning ?? "{}";
        var sigPayload = string.Concat(clientId, ts, bodySigned);
        var signature = ComputeHmacSha256Hex(sigPayload, clientSecret);

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

        // Debug logging for signature calculation
        Console.WriteLine($"[SwiftGoldPay] Token signature calculation:");
        Console.WriteLine($"[SwiftGoldPay] - client_id: {clientId}");
        Console.WriteLine($"[SwiftGoldPay] - timestamp: {ts}");
        Console.WriteLine($"[SwiftGoldPay] - body_for_signing: '{bodyForSigning}'");
        Console.WriteLine($"[SwiftGoldPay] - data_to_sign: '{dataToSign}'");
        Console.WriteLine($"[SwiftGoldPay] - signature: {signature}");

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
        // Send empty body with Content-Type: application/json (like Postman)
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
        ApplyDefaultHeaders(req, bearerToken, "{}");
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
    var jsonBody = JsonSerializer.Serialize(body, _jsonOpts); // minified JSON
    // Sign with minified JSON body
    ApplyDefaultHeaders(req, bearerToken, jsonBody);
    // Send body as application/json WITHOUT charset to mirror Postman exactly
    var contentBytes = Encoding.UTF8.GetBytes(jsonBody);
    var content = new ByteArrayContent(contentBytes);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    req.Content = content;
    // Debug ts/sig and body size/preview
        var ts = req.Headers.TryGetValues("x-timestamp", out var tsVals) ? tsVals.FirstOrDefault() : null;
        var sig = req.Headers.TryGetValues("x-signature", out var sigVals) ? sigVals.FirstOrDefault() : null;
    await _log.LogAsync("SwiftGoldPay.Customer.Request", new { url = req.RequestUri!.ToString(), body, bodyLen = jsonBody.Length, bodyPreview = jsonBody.Length > 120 ? jsonBody.Substring(0, 120) : jsonBody, timestamp = ts, signature = sig });
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        await _log.LogAsync("SwiftGoldPay.Customer.Response", new { status = (int)resp.StatusCode, text });
        try
        {
            var json = JsonNode.Parse(text) as JsonObject;
            var statusCode = json?["status"]?["code"]?.GetValue<string>();
            var msg = json?["status"]?["message"]?.GetValue<string>();
            var customerId = json?["data"]?["customer_id"]?.GetValue<string>();
            // Accept S-2000 (new customer), S-4004 or P-4004 (customer already exists)
            var ok = resp.IsSuccessStatusCode && (statusCode == "S-2000" || statusCode == "S-4004" || statusCode == "P-4004");
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
    var body = new { customer_id = customerId, amount, ref_no = refNo };
    var jsonBody = JsonSerializer.Serialize(body, _jsonOpts); // minified JSON
    // Sign with minified JSON body
    ApplyDefaultHeaders(req, bearerToken, jsonBody);
    var contentBytes2 = Encoding.UTF8.GetBytes(jsonBody);
    var content2 = new ByteArrayContent(contentBytes2);
    content2.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    req.Content = content2;
    var ts = req.Headers.TryGetValues("x-timestamp", out var tsVals) ? tsVals.FirstOrDefault() : null;
    var sig = req.Headers.TryGetValues("x-signature", out var sigVals) ? sigVals.FirstOrDefault() : null;
    await _log.LogAsync("SwiftGoldPay.Deposit.Request", new { url = req.RequestUri!.ToString(), body, bodyLen = jsonBody.Length, bodyPreview = jsonBody.Length > 120 ? jsonBody.Substring(0, 120) : jsonBody, timestamp = ts, signature = sig });
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

    public async Task<(bool ok, JsonArray? items, string? error, object? raw)> GetTransactionStatusAsync(
        string refNo,
        string currency,
        string bearerToken,
        CancellationToken ct = default)
    {
        var path = $"/api/opay/v1.0/partner/transactions?ref_no={Uri.EscapeDataString(refNo)}&currency={Uri.EscapeDataString(currency)}";
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyDefaultHeaders(req, bearerToken, "{}");
        var empty = new ByteArrayContent(Array.Empty<byte>());
        empty.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Content = empty;
        var ts = req.Headers.TryGetValues("x-timestamp", out var tsVals) ? tsVals.FirstOrDefault() : null;
        var sig = req.Headers.TryGetValues("x-signature", out var sigVals) ? sigVals.FirstOrDefault() : null;
        await _log.LogAsync("SwiftGoldPay.TxStatus.Request", new { url = req.RequestUri!.ToString(), timestamp = ts, signature = sig });
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        await _log.LogAsync("SwiftGoldPay.TxStatus.Response", new { status = (int)resp.StatusCode, text });
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
            return (false, null, "Invalid JSON from transactions endpoint", text);
        }
    }
}
