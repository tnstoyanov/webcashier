using System.Text.Json.Serialization;

namespace WebCashier.Models.Luxtak
{
    public class LuxtakPaymentResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;

<<<<<<< HEAD
=======
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

>>>>>>> 2844ecadaec88cc3e03b7e14ca37b489d4d37aff
        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; } = string.Empty;

        [JsonPropertyName("trade_no")]
        public string TradeNo { get; set; } = string.Empty;

        [JsonPropertyName("web_url")]
        public string WebUrl { get; set; } = string.Empty;

        [JsonPropertyName("prepay_id")]
        public string PrepayId { get; set; } = string.Empty;
    }
}
