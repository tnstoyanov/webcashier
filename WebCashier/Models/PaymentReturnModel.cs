namespace WebCashier.Models
{
    public class PaymentReturnModel
    {
        public bool IsSuccess { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentProcessor { get; set; }
        public string? Currency { get; set; }
        public string? CardType { get; set; }
        public string? CardNumber { get; set; }
        public string? StatusCode { get; set; }
        public string? StatusDetails { get; set; }
        public string? TransactionStatus { get; set; }
        public string? OrderId { get; set; }
        public string? Amount { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AuthorizationCode { get; set; }
        public string? Reference { get; set; }
    }
}
