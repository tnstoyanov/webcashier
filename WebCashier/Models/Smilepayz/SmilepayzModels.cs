using System.Text.Json.Serialization;

namespace WebCashier.Models.Smilepayz
{
    public class SmilepayzRequest
    {
        [JsonPropertyName("orderNo")] public string OrderNo { get; set; } = string.Empty;
        [JsonPropertyName("purpose")] public string Purpose { get; set; } = "Smilepayz deposit";
        [JsonPropertyName("merchant")] public SmilepayzMerchant Merchant { get; set; } = new();
        [JsonPropertyName("money")] public SmilepayzMoney Money { get; set; } = new();
        [JsonPropertyName("payer")] public SmilepayzPayer Payer { get; set; } = new();
        [JsonPropertyName("paymentMethod")] public string PaymentMethod { get; set; } = "BANK";
        [JsonPropertyName("redirectUrl")] public string RedirectUrl { get; set; } = string.Empty;
        [JsonPropertyName("callbackUrl")] public string CallbackUrl { get; set; } = string.Empty;
    }

    public class SmilepayzMerchant
    {
        [JsonPropertyName("merchantId")] public string MerchantId { get; set; } = string.Empty;
        [JsonPropertyName("merchantName")] public string MerchantName { get; set; } = string.Empty;
    }

    public class SmilepayzMoney
    {
        [JsonPropertyName("amount")] public string Amount { get; set; } = string.Empty;
        [JsonPropertyName("currency")] public string Currency { get; set; } = "THB";
    }

    public class SmilepayzPayer
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    public class SmilepayzResponse
    {
        [JsonPropertyName("tradeNo")] public string? TradeNo { get; set; }
        [JsonPropertyName("orderNo")] public string? OrderNo { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("merchant")] public SmilepayzMerchant? Merchant { get; set; }
        [JsonPropertyName("money")] public SmilepayzMoneyResp? Money { get; set; }
        [JsonPropertyName("channel")] public SmilepayzChannel? Channel { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("responseCode")] public string? ResponseCode { get; set; }
        [JsonPropertyName("responseMessage")] public string? ResponseMessage { get; set; }
    }

    public class SmilepayzMoneyResp
    {
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
    }

    public class SmilepayzChannel
    {
        [JsonPropertyName("paymentMethod")] public string? PaymentMethod { get; set; }
        [JsonPropertyName("vaNumber")] public string? VaNumber { get; set; }
        [JsonPropertyName("receiverBankName")] public string? ReceiverBankName { get; set; }
        [JsonPropertyName("qrString")] public string? QrString { get; set; }
        [JsonPropertyName("paymentUrl")] public string? PaymentUrl { get; set; }
        [JsonPropertyName("additionalInfo")] public string? AdditionalInfo { get; set; }
        [JsonPropertyName("cardData")] public string? CardData { get; set; }
    }
}
