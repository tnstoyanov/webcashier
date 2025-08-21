using System.Text.Json.Serialization;

namespace WebCashier.Models.Luxtak
{
    public class LuxtakPaymentRequest
    {
        [JsonPropertyName("charset")]
        public string Charset { get; set; } = "UTF-8";

        [JsonPropertyName("app_id")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; } = string.Empty;

        [JsonPropertyName("order_currency")]
        public string OrderCurrency { get; set; } = string.Empty;

        [JsonPropertyName("order_amount")]
        public string OrderAmount { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "Luxtak Deposit";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "LATAM operations";

        [JsonPropertyName("trade_type")]
        public string TradeType { get; set; } = "WEB";

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("notify_url")]
        public string NotifyUrl { get; set; } = string.Empty;

        [JsonPropertyName("return_url")]
        public string ReturnUrl { get; set; } = "https://tnstoyanov.wixsite.com/payment-response/return";

        [JsonPropertyName("buyer_id")]
        public string BuyerId { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0";

        [JsonPropertyName("customer")]
        public LuxtakCustomer Customer { get; set; } = new();

        [JsonPropertyName("regions")]
        public string[] Regions { get; set; } = new[] { "BRA" };

        [JsonPropertyName("address")]
        public LuxtakAddress Address { get; set; } = new();
    }

    public class LuxtakCustomer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = "73984401850";

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    public class LuxtakIdentify
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "CPF";

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;
    }

    public class LuxtakAddress
    {
        [JsonPropertyName("zip_code")]
        public string ZipCode { get; set; } = "38082365";
    }
}
