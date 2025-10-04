using System.Text.Json.Nodes;

namespace WebCashier.Services;

public interface ISwiftGoldPayService
{
    Task<(bool ok, string? token, string? error, object? raw)> GetTokenAsync(CancellationToken ct = default);
    Task<(bool ok, JsonArray? banks, string? error, object? raw)> GetBanksAsync(string country, string bearerToken, CancellationToken ct = default);
    Task<(bool ok, string? customerId, string? code, string? message, object? raw)> CreateOrGetCustomerAsync(
        string currency,
        string nameEn,
        string nameTh,
        string email,
        string bankCode,
        string bankAccountNameEn,
        string bankAccountNameTh,
        string bankAccountNumber,
        string customerRef,
        string bearerToken,
        CancellationToken ct = default);
    Task<(bool ok, string? depositId, object? raw, string? code, string? message)> CreateDepositAsync(
        string customerId,
        decimal amount,
        string refNo,
        string bearerToken,
        CancellationToken ct = default);
}
