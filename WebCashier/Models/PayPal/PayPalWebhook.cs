using System.Text.Json.Serialization;

namespace WebCashier.Models.PayPal
{
    public class PayPalWebhook
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("event_version")]
        public string? EventVersion { get; set; }

        [JsonPropertyName("create_time")]
        public string? CreateTime { get; set; }

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        [JsonPropertyName("resource_type")]
        public string? ResourceType { get; set; }

        [JsonPropertyName("resource")]
        public dynamic? Resource { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}
