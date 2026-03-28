using System.Text.Json.Serialization;

namespace WebCashier.Models.Brite
{
    public class BriteTransactionResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("merchant_reference")]
        public string? MerchantReference { get; set; }

        [JsonPropertyName("customer_reference")]
        public string? CustomerReference { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; } // pending, completed, failed, refunded, etc.

        [JsonPropertyName("amount")]
        public long? Amount { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("payment_method")]
        public string? PaymentMethod { get; set; }

        [JsonPropertyName("payment_url")]
        public string? PaymentUrl { get; set; } // Redirect URL for user authentication

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonPropertyName("error")]
        public BriteError? Error { get; set; }
    }

    public class BriteError
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }

    public class BriteRefundRequest
    {
        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("amount")]
        public long? Amount { get; set; } // Optional - full refund if not specified

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public class BriteWebhook
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; } // payment.completed, payment.failed, refund.completed

        [JsonPropertyName("transaction")]
        public BriteTransactionResponse? Transaction { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }
}
