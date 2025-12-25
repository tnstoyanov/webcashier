using System.Text.Json.Serialization;

namespace WebCashier.Models.PayPal
{
    public class PayPalOrderResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("links")]
        public List<Link>? Links { get; set; }

        [JsonPropertyName("purchase_units")]
        public List<PurchaseUnitResponse>? PurchaseUnits { get; set; }

        [JsonPropertyName("payer")]
        public Payer? Payer { get; set; }

        [JsonPropertyName("create_time")]
        public string? CreateTime { get; set; }

        [JsonPropertyName("update_time")]
        public string? UpdateTime { get; set; }
    }

    public class Link
    {
        [JsonPropertyName("rel")]
        public string? Rel { get; set; }

        [JsonPropertyName("href")]
        public string? Href { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }
    }

    public class PurchaseUnitResponse
    {
        [JsonPropertyName("reference_id")]
        public string? ReferenceId { get; set; }

        [JsonPropertyName("amount")]
        public Amount? Amount { get; set; }

        [JsonPropertyName("payee")]
        public Payee? Payee { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("payments")]
        public Payments? Payments { get; set; }
    }

    public class Payments
    {
        [JsonPropertyName("captures")]
        public List<Capture>? Captures { get; set; }

        [JsonPropertyName("authorizations")]
        public List<Authorization>? Authorizations { get; set; }
    }

    public class Capture
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("amount")]
        public Amount? Amount { get; set; }

        [JsonPropertyName("create_time")]
        public string? CreateTime { get; set; }

        [JsonPropertyName("update_time")]
        public string? UpdateTime { get; set; }
    }

    public class Authorization
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("amount")]
        public Amount? Amount { get; set; }

        [JsonPropertyName("create_time")]
        public string? CreateTime { get; set; }
    }

    public class Payer
    {
        [JsonPropertyName("name")]
        public Name? Name { get; set; }

        [JsonPropertyName("email_address")]
        public string? EmailAddress { get; set; }

        [JsonPropertyName("payer_id")]
        public string? PayerId { get; set; }

        [JsonPropertyName("phone")]
        public Phone? Phone { get; set; }

        [JsonPropertyName("address")]
        public Address? Address { get; set; }
    }

    public class Name
    {
        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("surname")]
        public string? Surname { get; set; }
    }

    public class Phone
    {
        [JsonPropertyName("phone_number")]
        public PhoneNumber? PhoneNumber { get; set; }

        [JsonPropertyName("phone_type")]
        public string? PhoneType { get; set; }
    }

    public class PhoneNumber
    {
        [JsonPropertyName("national_number")]
        public string? NationalNumber { get; set; }
    }

    public class Address
    {
        [JsonPropertyName("address_line_1")]
        public string? AddressLine1 { get; set; }

        [JsonPropertyName("address_line_2")]
        public string? AddressLine2 { get; set; }

        [JsonPropertyName("admin_area_2")]
        public string? AdminArea2 { get; set; }

        [JsonPropertyName("admin_area_1")]
        public string? AdminArea1 { get; set; }

        [JsonPropertyName("postal_code")]
        public string? PostalCode { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
    }
}
