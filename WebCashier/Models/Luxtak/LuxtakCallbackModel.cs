using System.Text.Json.Serialization;

namespace WebCashier.Models.Luxtak
{
    public class LuxtakCallbackModel
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

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("app_id")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("buyer_id")]
        public string BuyerId { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("sign")]
        public string Sign { get; set; } = string.Empty;
    }

    public class LuxtakReturnModel
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

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("app_id")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("buyer_id")]
        public string BuyerId { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("sign")]
        public string Sign { get; set; } = string.Empty;
    }
}
