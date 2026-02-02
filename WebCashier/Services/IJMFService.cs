using System.Text.Json.Serialization;

namespace WebCashier.Services;

/// <summary>
/// Service interface for JM Financial Hosted Payment Page (HPP) API integration.
/// </summary>
public interface IJMFService
{
    /// <summary>
    /// Creates a payment session with JM Financial API.
    /// </summary>
    Task<JMFPaymentResponse?> CreatePaymentSessionAsync(
        decimal amount, 
        string currency, 
        string customerName, 
        string customerEmail);
}

/// <summary>
/// Response from JM Financial API when creating a payment session.
/// </summary>
public class JMFPaymentResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("response")]
    public JMFResponse? Response { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("request")]
    public object? Request { get; set; }
}

public class JMFResponse
{
    [JsonPropertyName("redirect_url")]
    public string? RedirectUrl { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("order_number")]
    public string? OrderNumber { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
