using System.Text.Json.Serialization;

namespace WebCashier.Models.PayPal
{
    public class PayPalOrderRequest
    {
        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "CAPTURE";

        [JsonPropertyName("purchase_units")]
        public List<PurchaseUnit> PurchaseUnits { get; set; } = new();

        [JsonPropertyName("payment_source")]
        public PaymentSource? PaymentSource { get; set; }
    }

    public class PurchaseUnit
    {
        [JsonPropertyName("reference_id")]
        public string? ReferenceId { get; set; }

        [JsonPropertyName("amount")]
        public Amount? Amount { get; set; }

        [JsonPropertyName("payee")]
        public Payee? Payee { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class Amount
    {
        [JsonPropertyName("currency_code")]
        public string? CurrencyCode { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    public class Payee
    {
        [JsonPropertyName("merchant_id")]
        public string? MerchantId { get; set; }

        [JsonPropertyName("email_address")]
        public string? EmailAddress { get; set; }
    }

    public class PaymentSource
    {
        [JsonPropertyName("paypal")]
        public PayPalPaymentSource? PayPal { get; set; }
    }

    public class PayPalPaymentSource
    {
        [JsonPropertyName("experience_context")]
        public ExperienceContext? ExperienceContext { get; set; }
    }

    public class ExperienceContext
    {
        [JsonPropertyName("brand_name")]
        public string? BrandName { get; set; }

        [JsonPropertyName("user_action")]
        public string? UserAction { get; set; }

        [JsonPropertyName("shipping_preference")]
        public string? ShippingPreference { get; set; }

        [JsonPropertyName("locale")]
        public string? Locale { get; set; }

        [JsonPropertyName("return_url")]
        public string? ReturnUrl { get; set; }

        [JsonPropertyName("cancel_url")]
        public string? CancelUrl { get; set; }
    }
}
