namespace WebCashier.Models.Praxis
{
    public class PraxisCallbackModel
    {
        public string merchant_id { get; set; }
        public string application_key { get; set; }
        public PraxisCustomerCallback customer { get; set; }
        public PraxisSessionCallback session { get; set; }
        public PraxisTransactionCallback transaction { get; set; }
        public string version { get; set; }
        public long timestamp { get; set; }
    }

    public class PraxisCustomerCallback
    {
        public string customer_token { get; set; }
        public string country { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public int avs_alert { get; set; }
        public int verification_alert { get; set; }
    }

    public class PraxisSessionCallback
    {
        public string auth_token { get; set; }
        public string intent { get; set; }
        public string session_status { get; set; }
        public string order_id { get; set; }
        public string currency { get; set; }
        public int amount { get; set; }
        public string conversion_rate { get; set; }
        public string processed_currency { get; set; }
        public int processed_amount { get; set; }
        public string payment_method { get; set; }
        public string gateway { get; set; }
        public string cid { get; set; }
        public string variable1 { get; set; }
        public string variable2 { get; set; }
        public string variable3 { get; set; }
    }

    public class PraxisTransactionCallback
    {
        public string transaction_type { get; set; }
        public string transaction_status { get; set; }
        public int tid { get; set; }
        public string transaction_id { get; set; }
        public string currency { get; set; }
        public int amount { get; set; }
        public string conversion_rate { get; set; }
        public string processed_currency { get; set; }
        public int? processed_amount { get; set; }
        public int fee { get; set; }
        public int fee_included { get; set; }
        public string fee_type { get; set; }
        public string payment_method { get; set; }
        public string payment_processor { get; set; }
        public string gateway { get; set; }
        public PraxisCardCallback card { get; set; }
        public object wallet { get; set; }
        public int is_async { get; set; }
        public int is_cascade { get; set; }
        public int cascade_level { get; set; }
        public string reference_id { get; set; }
        public string withdrawal_request_id { get; set; }
        public string created_by { get; set; }
        public string edited_by { get; set; }
        public string status_code { get; set; }
        public string status_details { get; set; }
    }

    public class PraxisCardCallback
    {
        public string card_token { get; set; }
        public string card_type { get; set; }
        public string card_number { get; set; }
        public string card_exp { get; set; }
        public string card_issuer_name { get; set; }
        public string card_issuer_country { get; set; }
    }
}
