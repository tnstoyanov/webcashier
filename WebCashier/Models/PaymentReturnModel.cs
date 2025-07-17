namespace WebCashier.Models
{
    public class PaymentReturnModel
    {
        public bool IsSuccess { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentProcessor { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string StatusDetails { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string AuthorizationCode { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
    }
}
