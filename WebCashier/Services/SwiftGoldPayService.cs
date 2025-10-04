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

    private void ApplyDefaultHeaders(HttpRequestMessage req, string? bearer = null)
    {
        var (apiId, clientId, clientSecret, clientRefId) = GetCreds();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        // Signature rule: not specified in full; infer pattern: SHA256(client_id+client_secret+client_ref_id+x-apigw-api-id+x-timestamp+method+path)
        // We'll use method and path only as per examples; if spec differs, adjust easily.
        var path = req.RequestUri!.PathAndQuery;
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

    public async Task<(bool ok, string? token, string? error, object? raw)> GetTokenAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/oauth/v1.0/partner/token");
        ApplyDefaultHeaders(req);
        req.Content = new StringContent("", Encoding.UTF8, "application/json");
    await _log.LogAsync("SwiftGoldPay.Token.Request", new { url = req.RequestUri!.ToString(), method = req.Method.Method });
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
        await _log.LogAsync("SwiftGoldPay.Banks.Request", new { url = req.RequestUri!.ToString() });
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
