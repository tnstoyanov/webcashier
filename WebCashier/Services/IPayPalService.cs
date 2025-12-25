using WebCashier.Models.PayPal;

namespace WebCashier.Services
{
    public interface IPayPalService
    {
        Task<string?> GetAccessTokenAsync();
        Task<PayPalOrderResponse?> CreateOrderAsync(decimal amount, string currency, string description, string referenceId);
        Task<PayPalOrderResponse?> CaptureOrderAsync(string orderId);
        Task<PayPalOrderResponse?> GetOrderAsync(string orderId);
    }
}
