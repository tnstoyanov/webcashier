using System.Text.Json.Serialization;

namespace WebCashier.Models.Brite
{
    // Step 2: Authorization request
    public class BriteAuthRequest
    {
        [JsonPropertyName("public_key")]
        public string? PublicKey { get; set; }

        [JsonPropertyName("secret")]
        public string? Secret { get; set; }
    }

    // Step 2: Authorization response
    public class BriteAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires")]
        public double? Expires { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("error_name")]
        public string? ErrorName { get; set; }
    }

    // Step 3: Deposit session request
    public class BriteDepositSessionRequest
    {
        [JsonPropertyName("customer_email")]
        public string? CustomerEmail { get; set; }

        [JsonPropertyName("deeplink_redirect")]
        public string? DeeplinkRedirect { get; set; }

        [JsonPropertyName("country_id")]
        public string? CountryId { get; set; }

        [JsonPropertyName("customer_firstname")]
        public string? CustomerFirstname { get; set; }

        [JsonPropertyName("customer_lastname")]
        public string? CustomerLastname { get; set; }

        [JsonPropertyName("customer_reference")]
        public string? CustomerReference { get; set; }

        [JsonPropertyName("merchant_reference")]
        public string? MerchantReference { get; set; }

        [JsonPropertyName("customer_dob")]
        public string? CustomerDob { get; set; }

        [JsonPropertyName("amount")]
        public long? Amount { get; set; }

        [JsonPropertyName("approval_required")]
        public bool? ApprovalRequired { get; set; }

        [JsonPropertyName("customer_address")]
        public BriteCustomerAddress? CustomerAddress { get; set; }

        [JsonPropertyName("transaction_callback_url")]
        public string? TransactionCallbackUrl { get; set; }

        [JsonPropertyName("session_callback_url")]
        public string? SessionCallbackUrl { get; set; }
    }

    public class BriteCustomerAddress
    {
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("postal_code")]
        public string? PostalCode { get; set; }

        [JsonPropertyName("country_id")]
        public string? CountryId { get; set; }
    }

    public class BriteCallback
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("event")]
        public string? Event { get; set; }
    }

    // Step 3: Deposit session response
    public class BriteSessionResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("error_name")]
        public string? ErrorName { get; set; }
    }

    // Step 5: Get session response
    public class BriteSessionDetails
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("created")]
        public double? Created { get; set; }

        [JsonPropertyName("completed")]
        public double? Completed { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("state")]
        public int? State { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("merchant_reference")]
        public string? MerchantReference { get; set; }

        [JsonPropertyName("customer_reference")]
        public string? CustomerReference { get; set; }

        [JsonPropertyName("currency_id")]
        public string? CurrencyId { get; set; }

        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("bank")]
        public BriteBank? Bank { get; set; }

        [JsonPropertyName("bank_account")]
        public BriteBankAccount? BankAccount { get; set; }

        [JsonPropertyName("bank_integration_id")]
        public string? BankIntegrationId { get; set; }
    }

    public class BriteBank
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class BriteBankAccount
    {
        [JsonPropertyName("bban")]
        public string? Bban { get; set; }

        [JsonPropertyName("holder")]
        public string? Holder { get; set; }
    }

    public class BritePaymentMethod
    {
        public const string Swish = "swish";
        public const string Ideal = "ideal";
        public const string OpenBanking = "open_banking";
        public const string Card = "card";
        public const string Deposit = "session.create_deposit";
        public const string SwishPayment = "session.create_swish_payment";
    }
}
