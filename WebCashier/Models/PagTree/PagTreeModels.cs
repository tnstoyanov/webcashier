using System.Text.Json.Serialization;

namespace WebCashier.Models.PagTree
{
    public class PagTreeConfig
    {
        public string BaseUrl { get; set; } = "https://apiv2.pagtree.com";
        public string SecretKey { get; set; } = string.Empty;
    }

    // Request Models
    public class PagTreePaymentRequest
    {
        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "BRL";

        [JsonPropertyName("method")]
        public PagTreeMethod Method { get; set; } = new();

        [JsonPropertyName("expirationMinutes")]
        public int ExpirationMinutes { get; set; } = 30;

        [JsonPropertyName("idempotencyKey")]
        public string IdempotencyKey { get; set; } = string.Empty;

        [JsonPropertyName("customer")]
        public PagTreeCustomer Customer { get; set; } = new();

        [JsonPropertyName("meta")]
        public PagTreeMeta? Meta { get; set; }
    }

    public class PagTreeMethod
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "qr_code";
    }

    public class PagTreeCustomer
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("document")]
        public string Document { get; set; } = string.Empty; // CPF or CNPJ
    }

    public class PagTreeMeta
    {
        [JsonPropertyName("testBehavior")]
        public PagTreeTestBehavior? TestBehavior { get; set; }
    }

    public class PagTreeTestBehavior
    {
        [JsonPropertyName("finalStatus")]
        public string? FinalStatus { get; set; }
    }

    // Response Models
    public class PagTreePaymentResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("paidAmount")]
        public long? PaidAmount { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [JsonPropertyName("method")]
        public PagTreeResponseMethod? Method { get; set; }

        [JsonPropertyName("customer")]
        public PagTreeCustomer? Customer { get; set; }

        [JsonPropertyName("idempotencyKey")]
        public string? IdempotencyKey { get; set; }

        [JsonPropertyName("txId")]
        public string? TxId { get; set; }

        [JsonPropertyName("walletId")]
        public string? WalletId { get; set; }

        [JsonPropertyName("meta")]
        public PagTreeMeta? Meta { get; set; }

        [JsonPropertyName("walletTransactions")]
        public List<PagTreeWalletTransaction>? WalletTransactions { get; set; }
    }

    public class PagTreeResponseMethod
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("qrCode")]
        public string? QrCode { get; set; }
    }

    public class PagTreeWalletTransaction
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("resourceType")]
        public string ResourceType { get; set; } = string.Empty;

        [JsonPropertyName("grossAmount")]
        public long GrossAmount { get; set; }

        [JsonPropertyName("feesAmount")]
        public long FeesAmount { get; set; }

        [JsonPropertyName("deltaAmount")]
        public long DeltaAmount { get; set; }

        [JsonPropertyName("balanceAfter")]
        public long BalanceAfter { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }
    }

    public class PagTreeErrorResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        public List<PagTreeErrorDetail>? Details { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }
    }

    public class PagTreeErrorDetail
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
