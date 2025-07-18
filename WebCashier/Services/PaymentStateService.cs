using System.Collections.Concurrent;
using WebCashier.Models.Praxis;

namespace WebCashier.Services
{
    public interface IPaymentStateService
    {
        void SetPaymentPending(string orderId, string transactionId);
        void SetPaymentCompleted(string orderId, PraxisCallbackModel callbackData);
        PaymentState? GetPaymentState(string orderId);
        void SetPaymentFailed(string orderId, string reason);
    }

    public class PaymentStateService : IPaymentStateService
    {
        private readonly ConcurrentDictionary<string, PaymentState> _paymentStates = new();
        private readonly ILogger<PaymentStateService> _logger;

        public PaymentStateService(ILogger<PaymentStateService> logger)
        {
            _logger = logger;
        }

        public void SetPaymentPending(string orderId, string transactionId)
        {
            var state = new PaymentState
            {
                OrderId = orderId,
                TransactionId = transactionId,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _paymentStates.AddOrUpdate(orderId, state, (key, existing) => state);
            _logger.LogInformation("Payment set to pending: OrderId={OrderId}, TransactionId={TransactionId}", orderId, transactionId);
        }

        public void SetPaymentCompleted(string orderId, PraxisCallbackModel callbackData)
        {
            _paymentStates.AddOrUpdate(orderId, 
                new PaymentState
                {
                    OrderId = orderId,
                    Status = PaymentStatus.Completed,
                    CallbackData = callbackData,
                    CompletedAt = DateTime.UtcNow
                },
                (key, existing) => 
                {
                    existing.Status = PaymentStatus.Completed;
                    existing.CallbackData = callbackData;
                    existing.CompletedAt = DateTime.UtcNow;
                    return existing;
                });

            _logger.LogInformation("Payment completed: OrderId={OrderId}, Status={Status}", 
                orderId, callbackData.transaction?.transaction_status);
        }

        public void SetPaymentFailed(string orderId, string reason)
        {
            _paymentStates.AddOrUpdate(orderId,
                new PaymentState
                {
                    OrderId = orderId,
                    Status = PaymentStatus.Failed,
                    ErrorMessage = reason,
                    CompletedAt = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.Status = PaymentStatus.Failed;
                    existing.ErrorMessage = reason;
                    existing.CompletedAt = DateTime.UtcNow;
                    return existing;
                });

            _logger.LogInformation("Payment failed: OrderId={OrderId}, Reason={Reason}", orderId, reason);
        }

        public PaymentState? GetPaymentState(string orderId)
        {
            _paymentStates.TryGetValue(orderId, out var state);
            return state;
        }
    }

    public class PaymentState
    {
        public string OrderId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; }
        public PraxisCallbackModel? CallbackData { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Timeout
    }
}
