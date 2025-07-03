using System.ComponentModel.DataAnnotations;

namespace WebCashier.Models
{
    public class PaymentModel
    {
        [Required]
        [Display(Name = "Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Currency")]
        public string Currency { get; set; } = "USD";

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "card";

        [Required]
        [Display(Name = "Name on Card")]
        public string NameOnCard { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Card Number")]
        [CreditCard(ErrorMessage = "Invalid card number")]
        public string CardNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Expiration Date")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Please enter expiration date in MM/YY format")]
        public string ExpirationDate { get; set; } = string.Empty;

        [Required]
        [Display(Name = "CVV")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits")]
        public string CVV { get; set; } = string.Empty;

        [Display(Name = "Promotion Code")]
        public string? PromotionCode { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }
}
