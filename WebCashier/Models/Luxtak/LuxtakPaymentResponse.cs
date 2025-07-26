using System.Text.Json.Serialization;

namespace WebCashier.Models.Luxtak
{
    public class LuxtakPaymentResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("sub_code")]
        public string? SubCode { get; set; }

        [JsonPropertyName("sub_msg")]
        public string? SubMessage { get; set; }

        [JsonPropertyName("out_trade_no")]
        public string? OutTradeNo { get; set; }

        [JsonPropertyName("trade_no")]
        public string? TradeNo { get; set; }

        [JsonPropertyName("web_url")]
        public string? WebUrl { get; set; }

        [JsonPropertyName("prepay_id")]
        public string? PrepayId { get; set; }

        [JsonPropertyName("data")]
        public LuxtakPaymentData? Data { get; set; }
    }

    public class LuxtakPaymentData
    {
        [JsonPropertyName("trade_no")]
        public string TradeNo { get; set; } = string.Empty;

        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; } = string.Empty;

        [JsonPropertyName("order_amount")]
        public string OrderAmount { get; set; } = string.Empty;

        [JsonPropertyName("order_currency")]
        public string OrderCurrency { get; set; } = string.Empty;

        [JsonPropertyName("trade_status")]
        public string TradeStatus { get; set; } = string.Empty;

        [JsonPropertyName("payment_url")]
        public string PaymentUrl { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }
}
