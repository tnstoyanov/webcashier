using WebCashier.Models.Brite;

namespace WebCashier.Services
{
    public interface IBriteService
    {
        /// <summary>
        /// Step 2: Authorize with Brite to get bearer token
        /// </summary>
        Task<BriteAuthResponse?> AuthorizeAsync();

        /// <summary>
        /// Step 3: Create a deposit or swish payment session
        /// </summary>
        Task<BriteSessionResponse?> CreateDepositSessionAsync(
            string bearerToken,
            string paymentMethod,
            string countryId,
            decimal amount,
            string customerReference,
            string merchantReference,
            string customerEmail,
            string? customerFirstname = null,
            string? customerLastname = null);

        /// <summary>
        /// Step 5: Get session details after user completes authentication
        /// </summary>
        Task<BriteSessionDetails?> GetSessionDetailsAsync(string bearerToken, string sessionId);

        // Kept for interface compatibility
        Task<BriteTransactionResponse?> CreatePaymentAsync(
            decimal amount,
            string currency,
            string paymentMethod,
            string countryId,
            string description,
            string merchantReference,
            string? customerReference = null);

        Task<BriteTransactionResponse?> GetTransactionAsync(string transactionId);

        Task<BriteTransactionResponse?> RefundTransactionAsync(string transactionId, decimal? amount = null);

        Task<BriteTransactionResponse?> CaptureTransactionAsync(string transactionId);

        bool VerifyWebhookSignature(string payload, string signature);
    }
}
