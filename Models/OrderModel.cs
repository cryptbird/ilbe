using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Cms;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class SessionReq
    {
        [Required(ErrorMessage = "ID is required.")]
        public string ID { get; set; }

        [Required(ErrorMessage = "Payment Mode is required.")]
        public string payment_mode { get; set; }
    }

    public class FetchTxnDetails
    {
        public string journey_type { get; set; }
        public string amount { get; set; }
        public bool? flightTracking { get; set; }
        public string? flightTrackingURL { get; set; }
        public PaymentModel.PaymentFilters? payment_filters { get; set; }
        public int proposal_tenure { get; set; }
        public string name { get; set; }
        public string mobile_number { get; set; }
        public string email { get; set; }
        public string? installment_amount { get; set; }
        public string? mandate_max_amount { get; set; }
    }

    public class JuspaySessionReq
    {
        public string order_id { get; set; }
        public string amount { get; set; }
        public string payment_page_client_id { get; set; }
        public string action { get; set; }
        public string return_url { get; set; }

        [JsonProperty("metadata.JUSPAY:gateway_reference_id")]
        public string metadata_juspay_gateway_reference_id { get; set; }

        //[JsonProperty("metadata.webhook_url")]
        //public string metadata_webhook_url { get; set; }
        public string udf1 { get; set; }
        public string udf2 { get; set; }
        public string udf3 { get; set; }
        public string udf4 { get; set; }
        public string udf5 { get; set; }
        public string udf6 { get; set; }
        public string udf7 { get; set; }
        public string udf8 { get; set; }
        public string udf9 { get; set; }
        public string udf10 { get; set; }

        [JsonProperty("metadata.multiWebhookUrlConfigs")]
        public List<WebhookConfig> metadata_multiWebhookUrlConfigs { get; set; }

        public PaymentFilter payment_filter { get; set; }

        // Mandate Tags
        [JsonProperty("options.create_mandate")]
        public string options_create_mandate { get; set; }

        [JsonProperty("options.mandate_amount")]
        public string options_mandate_amount { get; set; }

        [JsonProperty("mandate.start_date")]
        public string mandate_start_date { get; set; }

        [JsonProperty("mandate.end_date")]
        public string mandate_end_date { get; set; }

        [JsonProperty("mandate.frequency")]
        public string mandate_frequency { get; set; }

        [JsonProperty("mandate.block_funds")]
        public bool mandate_block_funds { get; set; }

        [JsonProperty("metadata.txns.auto_capture")]
        public string metadata_txns_auto_capture { get; set; }

        [JsonProperty("mandate.revokable_by_customer")]
        public bool mandate_revokable_by_customer { get; set; }

        [JsonProperty("mandate.max_amount")]
        public string mandate_max_amount { get; set; }

        public string customer_id { get; set; }
        public string customer_phone { get; set; }
        public string customer_email { get; set; }
    }
    public class WebhookConfig
    {
        public string webhookUrl { get; set; }
        public List<string> enabledEvents { get; set; }
        public Dictionary<string, string> customHeaders { get; set; }
    }

    public class PaymentFilter
    {
        public bool allowDefaultOptions { get; set; }
        public List<Option> options { get; set; }
        public JuspayEmiPaymentOptions? emiOptions { get; set; }
    }

    public class Option
    {
        public string paymentMethodType { get; set; }
        public bool enable { get; set; }
        public List<UpiFilter> upiFilters { get; set; }
        public List<JuspayCardPaymentFilters> cardFilters { get; set; }
    }

    public class UpiFilter
    {
        public string upiType { get; set; }
        public bool enable { get; set; }
        public List<string> upiMethods { get; set; }
    }

    public class JuspayCardPaymentFilters
    {
        public bool enable { get; set; }
        public List<string>? cardBrands { get; set; }
        public List<string>? cardTypes { get; set; }
    }

    public class JuspayEmiPaymentOptions

    {
        public bool showOnlyEmi { get; set; }
        public JuspayPaymentEmi? standardEmi { get; set; }
        public JuspayPaymentEmi? lowCostEmi { get; set; }
        public JuspayPaymentEmi? noCostEmi { get; set; }
    }
    public class JuspayPaymentEmi
    {
        public bool enable { get; set; }
        public JuspayPaymentCardFilter? cardless { get; set; }
        public JuspayPaymentCardFilter? credit { get; set; }
    }
    public class JuspayPaymentCardFilter
    {
        public bool enable { get; set; }
        public List<JuspayPaymentCardFilters>? filters { get; set; }
    }
    public class JuspayPaymentCardFilters
    {
        public bool enable { get; set; }
        public JuspayPaymentEmiFilterIssuer? issuerFilter { get; set; }
        public string? paymentMethodType { get; set; }
        public string? paymentMethod { get; set; }
    }
    public class JuspayPaymentEmiFilterIssuer
    {
        public bool enable { get; set; }
        public List<string>? issuers { get; set; }
        public List<string>? tenures { get; set; }
    }

    public class FetchOrderStatusReq
    {
        [Required(ErrorMessage = "DPS Order ID is required.")]
        public string dps_order_id { get; set; }

        [Required(ErrorMessage = "Trace ID is required.")]
        public string trace_id { get; set; }
    }

    public class FetchOrderStatusResp
    {
        public string order_id { get; set; }
        public string auth_code { get; set; }
        public string status { get; set; }
        public string mandate_id { get; set; }
        public string payment_status { get; set; }
        public string payment_received_on { get; set; }
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
        public string dps_order_id { get; set; }
        public int amount { get; set; }
        public int amount_refunded { get; set; }
        public string payment_mode { get; set; }
        public List<RefundDetail> refunds { get; set; } = new List<RefundDetail>();
    }

    public class FetchRedirectionUrlReq
    {
        [Required(ErrorMessage = "ID is required.")]
        public string ID { get; set; }

        [Required(ErrorMessage = "pg_id is required.")]
        public string pg_id { get; set; }
    }

    public class FetchOrderAndProposalDetails
    {
        public string proposal_status { get; set; }
        public string proposal_number { get; set; }
        public string application_name { get; set; }
        public int template_id { get; set; }
        public string product { get; set; }
        public string core { get; set; }
        public string deal_id { get; set; }
        public string intermediary_code { get; set; }
        public string dps_order_id { get; set; }
        public string journey_type { get; set; }
        public string pg_name { get; set; }
        public string pre_payment_return_url { get; set; }
    }

    public class FetchRedirectionUrlResp
    {
        public string URL { get; set; }
        public string STATUS { get; set; }
    }

    public class FetchPolicyDetailsByIDReq
    {
        [Required(ErrorMessage = "ID is required.")]
        public string ID { get; set; }
    }

    public class FetchPolicyDetailsByIDResp
    {
        public string textMessage { get; set; }
        public string dps_juspay_status { get; set; }
        public string journey_type { get; set; }
        public bool is_mobile_user { get; set; }
        public string email { get; set; }
        public bool? flightTracking { get; set; }
        public string? flightTrackingURL { get; set; }
        public List<FetchPolicyDetailsByIDRespDetails> proposalDetailsList { get; set; }
    }

    public class FetchPolicyDetailsByIDRespDetails
    {
        public string proposal_number { get; set; }
        public string policy_number { get; set; }
        public string is_breakin_req { get; set; }
        public string journey_type { get; set; }
        public string breakin_id { get; set; }
        public string inspection_type { get; set; }
        public string proposal_start_date { get; set; }
        public string proposal_end_date { get; set; }
        public string proposal_amount { get; set; }
        public long sum_insured { get; set; }
        public string ped { get; set; }
        public bool is_seniorcitizen { get; set; }
        public string proposal_type { get; set; }
    }

    public class FetchBreakInProposalDetailsByIDReq
    {
        [Required(ErrorMessage = "ID is required.")]
        public string ID { get; set; }
    }

    public class FetchBreakInProposalDetailsByIDResp
    {
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
        public List<string> breakInProposals { get; set; }
    }

    public class CaptureMandateReq
    {

        public string order_id { get; set; }
        public string mandate_id { get; set; }
        public string amount { get; set; }
        public string customer_id { get; set; }
        public long mandate_execution_date { get; set; }
        public string application_name { get; set; }
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
    }

    public class MandateExecutionResp
    {
        public string mandate_id { get; set; }
        public string auth_code { get; set; }
        public bool isSuccess { get; set; } = false;
        public string status { get; set; }
    }

    public class JuspayCaptureMandateResp
    {
        public string txn_uuid { get; set; }
        public string txn_id { get; set; }
        public string status { get; set; }
        public string order_id { get; set; }
        public JuspayPayment payment { get; set; }
    }

    public class JuspayPayment
    {
        public JuspayAuthentication authentication { get; set; }
    }

    public class JuspayAuthentication
    {
        public string url { get; set; }
        public string method { get; set; }
    }

    public class RevokeMandateReq
    {
        public string mandate_id { get; set; }
        public string application_name { get; set; }
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
    }

    public class JuspayRevokeMandateResp
    {
        public string mandate_status { get; set; }
        public string mandate_id { get; set; }
        public string gateway_response_code { get; set; }
        public string gateway_response_message { get; set; }
    }

    public class FetchCrossSellingDetailsReq
    {
        [Required(ErrorMessage = "ID is required.")]
        public string ID { get; set; }

        [Required(ErrorMessage = "Product is required.")]
        public string Product { get; set; }
    }

    public class FetchCrossSellingDetailsResp
    {
        public List<CrossSellingProduct> products_list { get; set; }
    }

    public class CrossSellingProduct
    {
        public string product { get; set; }
    }

    public class GetDpsOrderStatusAndIsMobileUserResp
    {
        public string dps_juspay_status { get; set; }
        public bool is_mobile_user { get; set; }
        public string email { get; set; }
    }

    public class RefundDetail
    {
        public int amount { get; set; }
        public string unique_request_id { get; set; }
        public string ref_id { get; set; }
        public string created { get; set; }
        public string last_updated { get; set; }
        public string status { get; set; }
        public string error_message { get; set; }
        public bool sent_to_gateway { get; set; }
        public string expected_refund_credit_time { get; set; }
    }

    public class ILEMIValidateInstallmentReq
    {
        public string transaction_id { get; set; }
        public string? trace_id { get; set; }
        public string proposal_number { get; set; }
        public string? policy_number { get; set; }

        [RegularExpression("^(1[0-2]|[1-9])$", ErrorMessage = "Installment number must be between 1 and 12.")]
        public string installment_number { get; set; }
        public string? application_name { get; set; }
        public string amount { get; set; }
    }

    public class LatestEmiInstallment
    {
        public string recurring_id { get; set; }
        public string transaction_id { get; set;}
        public string trace_id { get; set;}
        public string proposal_number { get; set;}
        public string amount { get; set;}
        public string installment_number { get; set;}
        public string status { get; set;}
        public string payment_id { get; set;}
        public string? policy_number { get; set;}
        public string mandate_id { get; set;}
        public string dps_order_id { get; set;}
        public string order_id { get; set;}
        public string payment_mode { get; set;}
        public DateTime? due_date  { get; set;}
        public DateTime? execution_date  { get; set;}
        public DateTime? grace_end_date  { get; set;}
        public string remarks { get; set;}
        public string? no_of_attempt { get; set; }
        public string? customer_id { get; set; }
        public string application_name { get; set; }
    }

    public class CreateJuspayCustomerReq
    {
        [Required(ErrorMessage = "Object Reference ID is required.")]
        [NoHtmlTags(ErrorMessage = "Object Reference ID contains invalid characters.")]
        public string object_reference_id { get; set; }

        [Required(ErrorMessage = "Mobile Number is required.")]
        [NoHtmlTags(ErrorMessage = "Mobile Number contains invalid characters.")]
        public string mobile_number { get; set; }

        [Required(ErrorMessage = "Email Address is required.")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
        [NoHtmlTags(ErrorMessage = "Email Address contains invalid characters.")]
        public string email_address { get; set; }

        [Required(ErrorMessage = "First Name is required.")]
        [NoHtmlTags(ErrorMessage = "First Name contains invalid characters.")]
        public string first_name { get; set; }

        //[NoHtmlTags(ErrorMessage = "Last Name contains invalid characters.")]
        //public string last_name { get; set; }

        //[NoHtmlTags(ErrorMessage = "Mobile Country Code contains invalid characters.")]
        //public string mobile_country_code { get; set; }
        public bool options_get_client_auth_token { get; set; } = false;
    }

    public class JuspayCustomerResponse
    {
        public string id { get; set; }

        [JsonPropertyName("object")]
        public string object_type { get; set; }

        public string object_reference_id { get; set; }

        public string mobile_country_code { get; set; }

        public string mobile_number { get; set; }

        public string email_address { get; set; }

        public string first_name { get; set; }

        public string last_name { get; set; }

        public string date_created { get; set; }

        public string last_updated { get; set; }
    }

}