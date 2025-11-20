namespace WebCashier.Models.Paysolutions
{
    public class PaysolutionsDepositRequest
    {
        public string MerchantID { get; set; } = string.Empty;
        public string ProductDetail { get; set; } = "PromptPay QR";
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
    }

    public class PaysolutionsDepositResponse
    {
        public string Status { get; set; } = string.Empty;
        public PaysolutionsData? Data { get; set; }
        public string? Message { get; set; }
    }

    public class PaysolutionsData
    {
        public string OrderNo { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Orderdatetime { get; set; } = string.Empty;
        public string Expiredate { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
    }

    public class PaysolutionsConfig
    {
        public string BaseUrl { get; set; } = "https://apis.paysolutions.asia";
        public string MerchantID { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
