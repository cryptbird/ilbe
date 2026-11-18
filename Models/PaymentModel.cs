using Amazon.Util.Internal.PlatformServices;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class PaymentModel
    {
        public class DPSStatusResponse
        {
            public int StatusCode { get; set; }
            public string StatusMessage { get; set; }
            public string Message { get; set; }
            public string TraceId { get; set; }
            public object Data { get; set; }
        }

        public class AddPaymentReq
        {
            [Required(ErrorMessage = "Name is required.")]
            [NoHtmlTags(ErrorMessage = "Name contains invalid characters.")]
            public string Name { get; set; }

            [Required(ErrorMessage = "Email address is required.")]
            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
            [NoHtmlTags(ErrorMessage = "Email contains invalid characters.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Mobile Number is required.")]
            [RegularExpression(@"^\d{1,19}$", ErrorMessage = "Mobile number is in the wrong format.")]
            [NoHtmlTags(ErrorMessage = "Mobile Number contains invalid characters.")]
            public string MobileNumber { get; set; }

            [Required(ErrorMessage = "Total Amount is required.")]
            [RegularExpression(@"^\d{1,19}(\.\d{1,2})?$", ErrorMessage = "Total Amount is out of range.")]
            [NoHtmlTags(ErrorMessage = "Total Amount contains invalid characters.")]
            public string TotalAmount { get; set; }

            [RegularExpression(@"^(?i)(I|C)$", ErrorMessage = "PayerType must be either 'I' or 'C'.")]
            [NoHtmlTags(ErrorMessage = "PayerType contains invalid characters.")]
            public string PayerType { get; set; } = "C";

            [Required(ErrorMessage = "Product is required.")]
            [RegularExpression(@"^(?i)(HEALTH|HOME|MOTOR|TRAVEL|SME)_(?:[1-9]|1[0-9]|20)$", ErrorMessage = "Invalid Product Format.")]
            [NoHtmlTags(ErrorMessage = "Product contains invalid characters.")]
            public string Product { get; set; }

            [RegularExpression(@"^(?i)(POLICY|ENDORSEMENT|ADVANCE|NC_EMI|CL_EMI|IL_EMI|ONLY_EMI|ASBA|AUTOMANDATE)$", ErrorMessage = "JourneyType must be either 'POLICY', 'ENDORSEMENT' or 'ADVANCE' or 'NC_EMI' or 'CL_EMI' or 'IL_EMI' or 'ONLY_EMI' or 'ASBA' or 'AUTOMANDATE'.")]
            [NoHtmlTags(ErrorMessage = "JourneyType contains invalid characters.")]
            public string JourneyType { get; set; } = "POLICY";

            [Required(ErrorMessage = "Core is required.")]
            [RegularExpression(@"^(?i)(PF|ARTEMIS)$", ErrorMessage = "Core must be either 'PF' or 'ARTEMIS'.")]
            [NoHtmlTags(ErrorMessage = "Core contains invalid characters.")]
            public string Core { get; set; }

            [RegularExpression(@"^[A-Za-z|]+$", ErrorMessage = "SendPaymentLink contains invalid characters.")]
            public string SendPaymentLink { get; set; }

            [Required(ErrorMessage = "Template ID is required.")]
            [NoHtmlTags(ErrorMessage = "TemplateID contains invalid characters.")]
            public int TemplateID { get; set; }

            public List<ProposalList> ProposalList { get; set; }

            [NoHtmlTags(ErrorMessage = "ProposalStartDate contains invalid characters.")]
            public string ProposalStartDate { get; set; }
            public bool is_mobile_user { get; set; } = false;

            public JsonElement channel_application_details { get; set; }

            public GatewayParams? gateway_params { get; set; }
        }

        public class ProposalList
        {
            [NoHtmlTags(ErrorMessage = "DealId contains invalid characters.")]
            public string DealId { get; set; }

            [NoHtmlTags(ErrorMessage = "CustomerId contains invalid characters.")]
            public string CustomerId { get; set; }

            [NoHtmlTags(ErrorMessage = "IntermediaryCode contains invalid characters.")]
            public string IntermediaryCode { get; set; }

            [RegularExpression(@"^[a-zA-Z0-9-]{7,25}$", ErrorMessage = "ProposalNumber is in wrong format.")]
            [NoHtmlTags(ErrorMessage = "ProposalNumber contains invalid characters.")]
            public string ProposalNumber { get; set; }

            [RegularExpression(@"^\d{1,19}(\.\d{1,2})?$", ErrorMessage = "ProposalAmount is out of range.")]
            [NoHtmlTags(ErrorMessage = "ProposalAmount contains invalid characters.")]
            public string ProposalAmount { get; set; }

            [NoHtmlTags(ErrorMessage = "ProposalStatus contains invalid characters.")]
            public string ProposalStatus { get; set; }

            [NoHtmlTags(ErrorMessage = "UserID contains invalid characters.")]
            public int UserID { get; set; }

            public string is_nstp_flow { get; set; }
            public string is_breakin_req { get; set; }
            public string business_vertical { get; set; }
            public string sub_vertical { get; set; }
            public string marketing_officer { get; set; }
            public string proposer_code { get; set; }
        }

        public class GatewayParams
        {
            public string juspay_udf1 { get; set; }
            public string juspay_udf2 { get; set; }
            public string juspay_udf3 { get; set; }
            public string juspay_udf4 { get; set; }
            public string juspay_udf5 { get; set; }
            public string juspay_udf6 { get; set; }
            public string juspay_udf7 { get; set; }
            public string juspay_udf8 { get; set; }
            public string juspay_udf9 { get; set; }
            public string juspay_udf10 { get; set; }
        }

        public class AddPaymentReqv2
        {
            [Required(ErrorMessage = "Total Amount is required.")]
            [RegularExpression(@"^\d{1,19}(\.\d{1,2})?$", ErrorMessage = "Total Amount is out of range.")]
            [NoHtmlTags(ErrorMessage = "Total Amount contains invalid characters.")]
            public string totalAmount { get; set; }

            [RegularExpression(@"^(?i)(I|C)$", ErrorMessage = "PayerType must be either 'I' or 'C'.")]
            [NoHtmlTags(ErrorMessage = "PayerType contains invalid characters.")]
            public string payerType { get; set; } = "C";

            //[Required(ErrorMessage = "Product is required.")]
            [RegularExpression(@"^(?i)(?:(?:HEALTH|HOME|MOTOR|TRAVEL|SME|MISC)_(?:[1-9]|[12][0-9]|30))$", ErrorMessage = "Invalid Product Format.")]
            [NoHtmlTags(ErrorMessage = "Product contains invalid characters.")]
            public string product { get; set; }

            [RegularExpression(@"^(?i)(FRESH|RENEWAL|ENDORSEMENT|ADVANCE)$", ErrorMessage = "JourneyType must be either 'FRESH', 'RENEWAL', 'ENDORSEMENT' or 'ADVANCE'.")]
            [NoHtmlTags(ErrorMessage = "JourneyType contains invalid characters.")]
            public string journeyType { get; set; } = "FRESH";

            public string prePaymentReturnUrl { get; set; }

            public bool isMobileUser { get; set; } = false;

            public string rid { get; set; }

            public JsonElement channelApplicationDetails { get; set; }

            [Required(ErrorMessage = "PaymentFilters is required.")]
            public PaymentFilters paymentFilters { get; set; }

            public emiDetails emiDetails { get; set; }

            public List<ProposalListv2> proposalList { get; set; }

            [AllowedValues("WEB", "ANDROID", "IOS", ErrorMessage = "platform must be either 'WEB', 'ANDROID' or 'IOS'.")]
            [NoHtmlTags(ErrorMessage = "platform contains invalid characters.")]
            public string platform { get; set; } = "WEB";

            public bool isHeaderReq { get; set; } = ApplicationConfig.IS_FOOTER_REQUIRED == "Y";

            public bool isFooterReq { get; set; } = ApplicationConfig.IS_FOOTER_REQUIRED == "Y";

            public string intermediaryCode { get; set; }
        }

        public class emiDetails
        {
            public string? mandate_max_amount { get; set; }
            public string? installment_amount { get; set; }
            public string? installment_number { get; set; }
            public string? prev_transaction_id { get; set; }
        }

        public class prePaymentDetails
        {
            public string? returnUrl { get; set; }
            public string? customerId { get; set; }
            public string? dealId { get; set; }
            public int? proposalsCount { get; set; }
        }

        public class PaymentFilters
        {
            public bool UPI { get; set; }
            public bool NetBanking { get; set; }
            public bool CreditCard { get; set; }
            public bool DebitCard { get; set; }
            public bool Wallet { get; set; }
            public bool NoCostEMI { get; set; }
            public bool StandardEMI { get; set; }
            public bool CardlessEMI { get; set; }
            public bool BimaAsba { get; set; }
            public bool ILEMI { get; set; }
            public bool Autopay { get; set; }
        }

        public class ProposalListv2
        {
            [RegularExpression(@"^[a-zA-Z0-9-]{7,25}$", ErrorMessage = "ProposalNumber is in wrong format.")]
            [NoHtmlTags(ErrorMessage = "ProposalNumber contains invalid characters.")]
            public string ProposalNumber { get; set; }

            [RegularExpression(@"^\d{1,19}(\.\d{1,2})?$", ErrorMessage = "ProposalAmount is out of range.")]
            [NoHtmlTags(ErrorMessage = "ProposalAmount contains invalid characters.")]
            public string ProposalAmount { get; set; }

            [NoHtmlTags(ErrorMessage = "ProposalId contains invalid characters.")]
            public string ProposalId { get; set; }

            [NoHtmlTags(ErrorMessage = "ProposalType contains invalid characters.")]
            public string ProposalType { get; set; }

            [RegularExpression(@"^(?i)(PF|ARTEMIS)$", ErrorMessage = "Core must be either 'PF' or 'ARTEMIS'.")]
            [NoHtmlTags(ErrorMessage = "Core contains invalid characters.")]
            public string core { get; set; }

            [NoHtmlTags(ErrorMessage = "DealId contains invalid characters.")]
            public string DealId { get; set; }

            [NoHtmlTags(ErrorMessage = "CustomerId contains invalid characters.")]
            public string CustomerId { get; set; }

            [NoHtmlTags(ErrorMessage = "IntermediaryCode contains invalid characters.")]
            public string IntermediaryCode { get; set; }
        }

        public class AddPaymentResp
        {
            public string URL { get; set; }
            public string TransactionID { get; set; }
            public string pg_name { get; set; }
        }

        public class AddPaymentRespv2
        {
            public string URL { get; set; }
            public string TransactionID { get; set; }
            public string pg_name { get; set; }
        }

        public class SendPaymentLink
        {
            public bool EmailSent { get; set; } = false;
            public bool SmsSent { get; set; } = false;
            public bool WhatsAppSent { get; set; } = false;
            public string TransactionID { get; set; }
        }

        public class FetchPaymentReq
        {
            [Required(ErrorMessage = "ID is required.")]
            public string ID { get; set; }

            public string is_landing_page { get; set; } = "N";

        }

        public class FetchPaymentResp
        {
            public string TransactionId { get; set; }
            public string ApplicationName { get; set; }
            public string TraceId { get; set; }
            public string Amount { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string MobileNumber { get; set; }
            public string Product { get; set; }
            public string ProductValue { get; set; }
            public string Core { get; set; }
            public int TemplateID { get; set; }
            public string PayerType { get; set; }
            public string ProposalStartDate { get; set; }
            public string is_first_txn_visit { get; set; }
            public string link_expiry_time { get; set; }
            public string journey_type { get; set; }
            public string TemplateDetails { get; set; }
            public List<ProposalList> ProposalList { get; set; }
            //public AuthenticateToken authTokenData { get; set; }
            public bool is_transaction_done { get; set; } = false;
            public string is_callback_reqd { get; set; } = "N";
            public string rm_text { get; set; }
            public string payment_filters { get; set; }
            public string adf0 { get; set; } = ApplicationConfig.ADF0;
            public string adf1 { get; set; } = ApplicationConfig.ADF1;
            public string vrn { get; set; }
            public string pg_id { get; set; }
            public string address { get; set; }
            public string state { get; set; }
            public string city { get; set; }
            public string pincode { get; set; }
            public string basic_premium { get; set; }
            public string gst { get; set; }
            public DateTime? created_on { get; set; }
            public string tripType { get; set; }
            public string platform { get; set; }
            public string installment_number { get; set; }
            public string installment_amount { get; set; }
            public bool isHeaderReq { get; set; }
            public bool isFooterReq { get; set; }
            public bool isILEMI { get; set; }
        }

        public class TemplateDetails
        {
            public int upi { get; set; }
            public int credit { get; set; }
            public int debit { get; set; }
            public int netbanking { get; set; }
            public int wallet { get; set; }
            public int bima_asba { get; set; }
            public int emi { get; set; }
        }

        public class FetchProposalStatusFromArtemisReq
        {
            public string proposal_no { get; set; }
        }

        public class FetchProposalStatusFromArtemisResp
        {
            public string proposalStatus { get; set; }
            public string policyNumber { get; set; }
            public string message { get; set; }
            public bool status { get; set; }
            public string statusMessage { get; set; }
            public string correlationId { get; set; }
        }

        public class TagProposalReq
        {
            [Required(ErrorMessage = "TraceID is required.")]
            [NoHtmlTags(ErrorMessage = "TraceID contains invalid characters.")]
            public string TraceID { get; set; }

            [Required(ErrorMessage = "TransactionID is required.")]
            [NoHtmlTags(ErrorMessage = "TransactionID contains invalid characters.")]
            public string TransactionID { get; set; }

            [RegularExpression(@"^(?i)(I|C)$", ErrorMessage = "PayerType must be either 'I' or 'C'.")]
            [NoHtmlTags(ErrorMessage = "PayerType contains invalid characters.")]
            public string PayerType { get; set; } = "C";

            public ProposalList ProposalList { get; set; }
        }

        public class TagProposalResp
        {
            public string PolicyNumber { get; set; }
        }

        public class FetchProposalDetailsReq
        {
            public string ProposalNumber { get; set; }
            public string ProductType { get; set; }
            public string IssuanceSystem { get; set; }
        }

        public class FetchProposalDetailsResp
        {
            public string PolicyNumber { get; set; }
            public string ProposalStatus { get; set; }
            public string ProposalSubStatus { get; set; }
            public string ProposalStartDate { get; set; }
            public string ProposalEndDate { get; set; }
            public int? ProposalTenure { get; set; }
            public long? SumInsured { get; set; }

            public ProposalPremiumDetails PremiumDetails { get; set; }

            public string DealId { get; set; }
            public string IntermediaryCode { get; set; }
            public string pfProductCode { get; set; }
            public string irdaProductCode { get; set; }
            public bool? IsNSTPFlow { get; set; }
            public string IsNSTPFlowStr { get; set; }

            public string BusinessVertical { get; set; }
            public string SubVertical { get; set; }
            public string MarketingOfficer { get; set; }
            public int StatusCode { get; set; }

            public CustomerDetails CustomerDetails { get; set; }
            public MotorAdditionalInfo MotorAdditionalInfo { get; set; }
            public TravelAdditionalInfo TravelAdditionalInfo { get; set; }
            public string CorelationId { get; set; }
            public string TechnicalError { get; set; }
            public string DisplayMessage { get; set; }
            public bool Success { get; set; }

            public string proposalNumber { get; set; }
            public string proposalAmount { get; set; }
            public string proposalId { get; set; }
            public string proposalType { get; set; }
            public string core { get; set; }
        }

        public class TravelAdditionalInfo
        {
            public string tripType { get; set; }
            public bool flightTracking { get; set; }
            public string flightTrackingURL { get; set; }
            public long? pedSumInsured { get; set; }
            public List<TravellerDetails> travellerDetails { get; set; }
        }

        public class TravellerDetails
        {
            public string name { get; set; }
            public int age { get; set; }
            public bool isPed { get; set; }
        }

        public class ProposalPremiumDetails
        {
            public double? BasicPremium { get; set; }
            public double? StampDuty { get; set; }
            public double? GST { get; set; }
            public double? TotalPremium { get; set; }
        }

        public class CustomerDetails
        {
            public string CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string CustomerEmail { get; set; }
            public string CustomerMobile { get; set; }
            public string CustomerAddress { get; set; }
            public string State { get; set; }
            public string City { get; set; }
            public string Pincode { get; set; }
        }

        public class MotorAdditionalInfo
        {
            public string TypeVehicle { get; set; }
            public string VehicleMake { get; set; }
            public string VehicleModel { get; set; }
            public string RegistrationNo { get; set; }
            public string EngineNo { get; set; }
            public string ChassisNo { get; set; }
            public string ManufactureYear { get; set; }
            public string SubLocation { get; set; }
            public int? BreakInDays { get; set; }
            public string SalesRemarks { get; set; }
            public string BreakInType { get; set; }
            public bool IsPOSDealId { get; set; } = false;
        }

        public class GetSendPaymentLinkDataReq
        {
            public string ID { get; set; }
        }

        public class GetSendPaymentLinkDataResp
        {
            public string amount { get; set; }
            public string email { get; set; }
            public string mobile_number { get; set; }
            public string product_value { get; set; }
            public string net_premium { get; set; }
            public string gst { get; set; }
            public string token { get; set; }
            public List<ProposalList> proposal_list { get; set; }
            public string product { get; set; }
            public string tokenExpiryTime { get; set; }
            public string send_payment_link { get; set; }
        }

        public class SendPaymentLinkReq
        {
            [Required(ErrorMessage = "ID is required.")]
            public string ID { get; set; }

            [RegularExpression(@"^[SWE](\|[SWE])*$", ErrorMessage = "SendPaymentLink contains invalid characters.")]
            public string paymentMethod { get; set; }

            [RegularExpression(@"^\d{1,19}$", ErrorMessage = "Mobile number is in the wrong format.")]
            [NoHtmlTags(ErrorMessage = "WhatsApp Mobile Number contains invalid characters.")]
            public string whatsappMobileNumber { get; set; }

            [RegularExpression(@"^\d{1,19}$", ErrorMessage = "Mobile number is in the wrong format.")]
            [NoHtmlTags(ErrorMessage = "Mobile Number contains invalid characters.")]
            public string mobileNumber { get; set; }

            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
            [NoHtmlTags(ErrorMessage = "Email ID contains invalid characters.")]
            public string emailId { get; set; }

            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
            [NoHtmlTags(ErrorMessage = "Alternate Email ID contains invalid characters.")]
            public string alternateEmailId { get; set; }
            public string product { get; set; }
        }

        public class GetPaymentLinkDetails
        {
            public string send_link_landing_url { get; set; }
            public string product { get; set; }
            public string total_amount { get; set; }
            public string name { get; set; }
            public string proposal_number { get; set; }
            public string proposal_amount { get; set; }
            public string proposal_status { get; set; }
            public string proposal_start_date { get; set; }
            public string proposal_end_date { get; set; }
        }

        public class SendPaymentLinkResp
        {
            public bool EmailSent { get; set; } = false;
            public bool SmsSent { get; set; } = false;
            public bool WhatsAppSent { get; set; } = false;
            public string TransactionID { get; set; }
        }

        public class CheckProposalV2OrderData
        {
            public string proposal_number { get; set; }
            public string transaction_id { get; set; }
            public string trace_id { get; set; }
            public string payment_url { get; set; }
            public string send_payment_link { get; set; }
            public string product { get; set; }
            public string application_name { get; set; }
            public string pg_name { get; set; }
            public string order_id { get; set; }
            public string dps_order_id { get; set; }
            public DateTime? created_on { get; set; }
        }

        public class CheckProposalV2Resp
        {
            public AddPaymentRespv2 addPaymentResp { get; set; } = new AddPaymentRespv2();
            public string trace_id { get; set; }
            public bool is_error { get; set; } = false;
            public string error_message { get; set; }
        }

        public class ProposalFWEXWApiResponse
        {
            public string DealId { get; set; }
            public long? CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string CustomerEmail { get; set; }
            public string CustomerAddress { get; set; }
            public string State { get; set; }
            public string City { get; set; }
            public string Pincode { get; set; }
            public string TelephoneNumber { get; set; }
            public string MobileNumber { get; set; }
            public string TypeVehicle { get; set; }
            public string VehicleMake { get; set; }
            public string VehicleModel { get; set; }
            public string RegistrationNo { get; set; }
            public string EngineNo { get; set; }
            public string ChassisNo { get; set; }
            public string ManufactureYear { get; set; }
            public int? BreakInDays { get; set; }
            public string SalesRemarks { get; set; }
            public string BreakInType { get; set; }
            public bool IsPOSDealId { get; set; }
            public string ProposalStatus { get; set; }
            public string ProposalStartDate { get; set; }
            public string ProposalEndDate { get; set; }
            public int? ProposalTenure { get; set; }
            public double? SumInsured { get; set; }
            public double? BasicPremium { get; set; }
            public double? StampDuty { get; set; }
            public double? GST { get; set; }
            public double? TotalPremium { get; set; }
        }

        public class GetMessageByProduct
        {
            public string sms_text { get; set; }
            public string mail_text { get; set; }
            public string whatsapp_text { get; set; }
        }

        public class GetPaymentDetails
        {
            public string payment_url { get; set; }
            public string pg_name { get; set; }
            public string application_name { get; set; }
            public string application_id { get; set; }
            public DateTime? txn_created_on { get; set; }
            public DateTime? payment_done_at { get; set; }
        }
        public class GetPaymentLinkResp
        {
            public string payment_url { get; set; }
            public string pg_name { get; set; }
            public AuthenticateToken authTokenData { get; set; }
        }

        public class TagProposalReqv2
        {
            [Required(ErrorMessage = "TransactionID is required.")]
            [NoHtmlTags(ErrorMessage = "TransactionID contains invalid characters.")]
            public string TransactionID { get; set; }

            public ProposalListv2 ProposalList { get; set; }
        }

        public class TagProposalRespv2
        {
            public string PolicyNumber { get; set; }
        }

        public class TagProposalReqv3
        {
            [Required(ErrorMessage = "TransactionID is required.")]
            [NoHtmlTags(ErrorMessage = "TransactionID contains invalid characters.")]
            public string TransactionID { get; set; }

            public List<ProposalListv2> ProposalList { get; set; }
        }

        public class TagProposalRespv3
        {
            public string redirectUrl { get; set; }

            public List<TagProposalListv3> ProposalList { get; set; }
        }

        public class TagProposalListv3
        {
            public string proposalNumber { get; set; }
            public string policyNumber { get; set; }
        }

        public class UpdateAdvancePaymentDetails
        {
            public string transaction_id { get; set; }
            public string trace_id { get; set; }
            public string proposal_number { get; set; }
            public string proposal_id { get; set; }
            public string proposal_amount { get; set; }
            public string proposal_type { get; set; }
            public string core { get; set; }
            public string proposal_status { get; set; }
            public string is_nstp_flow { get; set; }

            public string proposal_start_date { get; set; }
            public string proposal_end_date { get; set; }
            public int? proposal_tenure { get; set; }

            public string name { get; set; }
            public string email { get; set; }
            public string mobile_number { get; set; }
            public string address { get; set; }
            public string state { get; set; }
            public string city { get; set; }
            public string pincode { get; set; }
            public double? basic_premium { get; set; }
            public double? gst { get; set; }
            public string vrn { get; set; }                      
        }

        public class GetAgentDetailsRequest
        {
            public string proposal_type { get; set; }
            public string intermediaryCode { get; set; }
            public string customer_id { get; set; }
            public string payer_type { get; set; }
        }

        public class ArtemisAgentDetailsForProductCode
        {
            public string business_vertical_code { get; set; }
            public string sub_vertical_code { get; set; }
            public string marketing_officer { get; set; }
            public string proposer_code { get; set; }
        }

        public class EMIModifyProposalReq
        {
            public string proposalid { get; set; }
        }

        public class EMIModifyProposalResp
        {
            public string? transactionId { get; set; }
            public string? requestId { get; set; }
            public decimal totalPremium { get; set; }
            public string? proposalId { get; set; }
            public bool success { get; set; }
            public string? errorMessage { get; set; }
            public string? corelationId { get; set; } 
            public int errorCode { get; set; }
            public string? proposalNo { get; set; }
        }
    }
}
