namespace WebCashier.Models.Praxis
{
    public class PraxisConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string MerchantId { get; set; } = string.Empty;
        public string ApplicationKey { get; set; } = string.Empty;
        public string MerchantSecret { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "sale";
        public string Gateway { get; set; } = string.Empty;
        public string NotificationUrl { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string Version { get; set; } = "1.3";
    }

    public class PraxisRequest
    {
        public string merchant_id { get; set; } = string.Empty;
        public string application_key { get; set; } = string.Empty;
        public string transaction_type { get; set; } = string.Empty;
        public string currency { get; set; } = string.Empty;
        public int amount { get; set; }
        public PraxisCardData card_data { get; set; } = new();
        public PraxisDeviceData device_data { get; set; } = new();
        public string cid { get; set; } = string.Empty;
        public string locale { get; set; } = string.Empty;
        public PraxisCustomerData customer_data { get; set; } = new();
        public string gateway { get; set; } = string.Empty;
        public string notification_url { get; set; } = string.Empty;
        public string return_url { get; set; } = string.Empty;
        public string order_id { get; set; } = string.Empty;
        public string version { get; set; } = string.Empty;
        public long timestamp { get; set; }
    }

    public class PraxisCardData
    {
        public string card_number { get; set; } = string.Empty;
        public string card_exp { get; set; } = string.Empty;
        public string cvv { get; set; } = string.Empty;
    }

    public class PraxisDeviceData
    {
        public string user_agent { get; set; } = string.Empty;
        public string accept_header { get; set; } = string.Empty;
        public string language { get; set; } = string.Empty;
        public string ip_address { get; set; } = string.Empty;
        public int timezone_offset { get; set; }
        public string color_depth { get; set; } = string.Empty;
        public string pixel_depth { get; set; } = string.Empty;
        public string pixel_ratio { get; set; } = string.Empty;
        public int screen_height { get; set; }
        public int screen_width { get; set; }
        public int viewport_height { get; set; }
        public int viewport_width { get; set; }
        public int java_enabled { get; set; }
        public int javascript_enabled { get; set; }
    }

    public class PraxisCustomerData
    {
        public string country { get; set; } = string.Empty;
        public string first_name { get; set; } = string.Empty;
        public string last_name { get; set; } = string.Empty;
        public string dob { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string phone { get; set; } = string.Empty;
        public string zip { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public string profile { get; set; } = "0";
    }

    public class PraxisResponse
    {
        public bool status { get; set; }
        public string message { get; set; } = string.Empty;
        public string transaction_id { get; set; } = string.Empty;
        public string order_id { get; set; } = string.Empty;
        public string reference_id { get; set; } = string.Empty;
        public decimal amount { get; set; }
        public string currency { get; set; } = string.Empty;
        public string gateway_response { get; set; } = string.Empty;
    }
}
