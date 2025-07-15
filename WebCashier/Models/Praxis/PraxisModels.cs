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
        public int status { get; set; }
        public string description { get; set; } = string.Empty;
        public string redirect_url { get; set; } = string.Empty;
        public PraxisCustomer customer { get; set; } = new();
        public PraxisSession session { get; set; } = new();
        public PraxisTransaction transaction { get; set; } = new();
        public long timestamp { get; set; }
        public string version { get; set; } = string.Empty;
        
        // Legacy properties for backward compatibility
        public bool IsSuccess => status == 0;
        public string message => description;
        public string transaction_id => transaction?.transaction_id ?? string.Empty;
        public string order_id => session?.order_id ?? string.Empty;
    }

    public class PraxisCustomer
    {
        public string customer_token { get; set; } = string.Empty;
        public string country { get; set; } = string.Empty;
        public string first_name { get; set; } = string.Empty;
        public string last_name { get; set; } = string.Empty;
        public int avs_alert { get; set; }
        public int verification_alert { get; set; }
    }

    public class PraxisSession
    {
        public string auth_token { get; set; } = string.Empty;
        public string intent { get; set; } = string.Empty;
        public string session_status { get; set; } = string.Empty;
        public string order_id { get; set; } = string.Empty;
        public string currency { get; set; } = string.Empty;
        public int amount { get; set; }
        public string conversion_rate { get; set; } = string.Empty;
        public string processed_currency { get; set; } = string.Empty;
        public int processed_amount { get; set; }
        public string payment_method { get; set; } = string.Empty;
        public string gateway { get; set; } = string.Empty;
        public string cid { get; set; } = string.Empty;
        public string? variable1 { get; set; }
        public string? variable2 { get; set; }
        public string? variable3 { get; set; }
    }

    public class PraxisTransaction
    {
        public string transaction_type { get; set; } = string.Empty;
        public string transaction_status { get; set; } = string.Empty;
        public int tid { get; set; }
        public string transaction_id { get; set; } = string.Empty;
        public string currency { get; set; } = string.Empty;
        public int amount { get; set; }
        public string conversion_rate { get; set; } = string.Empty;
        public string? processed_currency { get; set; }
        public int? processed_amount { get; set; }
        public int fee { get; set; }
        public int fee_included { get; set; }
        public string fee_type { get; set; } = string.Empty;
        public string payment_method { get; set; } = string.Empty;
        public string payment_processor { get; set; } = string.Empty;
        public string gateway { get; set; } = string.Empty;
        public PraxisCard card { get; set; } = new();
        public object? wallet { get; set; }
        public int is_async { get; set; }
        public int is_cascade { get; set; }
        public string? cascade_level { get; set; }
        public string? reference_id { get; set; }
        public string? withdrawal_request_id { get; set; }
        public string created_by { get; set; } = string.Empty;
        public string? edited_by { get; set; }
        public string status_code { get; set; } = string.Empty;
        public string status_details { get; set; } = string.Empty;
        public string redirect_url { get; set; } = string.Empty;
    }

    public class PraxisCard
    {
        public string? card_token { get; set; }
        public string card_type { get; set; } = string.Empty;
        public string card_number { get; set; } = string.Empty;
        public string card_exp { get; set; } = string.Empty;
        public string card_issuer_name { get; set; } = string.Empty;
        public string card_issuer_country { get; set; } = string.Empty;
    }
}
