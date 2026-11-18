using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class FetchPaymentDetails
    {
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
        public string created_on { get; set; }
        public string core { get; set; }
        public string total_amount { get; set; }
        public string payer_type { get; set; }
        public List<FetchPaymentProposalList> fetchPaymentProposalList { get; set; }
        public string pf_mid { get; set; }
        public string artemis_mid { get; set; }
        public string auth_code { get; set; }
        public string reverse_sync_url { get; set; }
        public string payment_mode { get; set; }
        public string product { get; set; }
        public string journey_type { get; set; }
        public string application_name { get; set; }
        public string mandate_id { get; set; }
        public string channel_application_details { get; set; }
        public string dps_order_id { get; set; }
        public string installment_number { get; set; }
        public string installment_amount { get; set; }
        public string gateway_customer_id { get; set; } = "";
        public string payment_id { get; set; }
        public string proposal_id { get; set; }
        public bool is_ilemi_proposal_mod_reqd { get; set; }
    }

    public class FetchPaymentProposalList
    {
        public string proposal_number { get; set; }
        public string proposal_id { get; set; }
        public string proposal_amount { get; set; }
        public string proposal_type { get; set; }
        public string core { get; set; }
        public string proposal_status { get; set; }
        public string deal_id { get; set; }
        public string customer_id { get; set; }
        public string intermediary_code { get; set; }
        public string is_nstp_flow { get; set; }
        public string business_vertical { get; set; }
        public string sub_vertical { get; set; }
        public string marketing_officer { get; set; }
        public string proposer_code { get; set; }
        public string policy_number { get; set; }
    }

    public class SyncProposalToSource
    {
        [Required]
        public string application_name { get; set; }
        public string policy_number { get; set; }
        public string payment_id { get; set; }
        public string proposal_number { get; set; }
        public string? installment_number { get; set; }
        public string? installment_amount { get; set; }
        public string reverse_sync_url { get; set; }
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
        public string refund_amount { get; set; }
        public string breakin_id { get; set; }
        public string breakin_status { get; set; }
        public string breakin_remarks { get; set; }
        public string is_advance_payment_success { get; set; }
        public string channel_application_details { get; set; }
        public string auth_code { get; set; }
        public string sync_event { get; set; }
    }

    public class FetchWebhookFallbackCases
    {
        public string dps_order_id { get; set; }
        public string trace_id { get; set; }
        public string application_name { get; set; }
        public int webhook_fallback_attempt_count { get; set; }
    }

    public class TransactionDetails
    {
        public string transaction_id { get; set; }
        public string order_status { get; set; }
        public string auth_code { get; set; }
        public string mandate_id { get; set; }
    }

    public class ChannelApplicationOrderDetailsReq
    {
        [Required(ErrorMessage = "transaction_id is required.")]
        public string transaction_id { get; set; }
    }

    public class ChannelApplicationOrderDetailsFlatRow
    {
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
        public string application_name { get; set; }
        public string dps_order_id { get; set; }
        public string auth_code { get; set; }
        public string total_amount { get; set; }
        public string payment_mode { get; set; }
        public string channel_application_details { get; set; }
        public string proposal_number { get; set; }
        public string payment_id { get; set; }
        public string policy_number { get; set; }
        public string breakin_id { get; set; }
        public string breakin_status { get; set; }
        public string breakin_remarks { get; set; }
        public string refund_amount { get; set; }
        public string refund_id { get; set; }
        public string refund_status { get; set; }
        public string core { get; set; }
        public string proposal_start_date { get; set; }
        public string proposal_end_date { get; set; }
        public int? proposal_tenure { get; set; }
        public long? sum_insured { get; set; }
        public string trip_type { get; set; }
        public bool flight_tracking { get; set; }
        public string flight_tracking_url { get; set; }
        public string ped { get; set; }
        public string product { get; set; }
    }

    public class ChannelApplicationOrderDetailsResp
    {
        public string pg_status { get; set; }
        public string payment_status { get; set; }
        public string payment_received_on { get; set; }
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
        public string application_name { get; set; }
        public string dps_order_id { get; set; }
        public string auth_code { get; set; }
        public string total_amount { get; set; }
        public string payment_mode { get; set; }
        public string payment_id { get; set; }
        public string channel_application_details { get; set; }
        public List<ChannelApplicationProposalDetailsResp> ChannelApplicationProposalDetailsResp { get; set; }
        public List<ChannelApplicationRefundDetails> refunds { get; set; }
    }

    public class ChannelApplicationProposalDetailsResp
    {
        public string proposal_number { get; set; }
        public string policy_number { get; set; }
        public string proposal_start_date { get; set; }
        public string proposal_end_date { get; set; }
        public long? sum_insured { get; set; }
        public string download_policy_copy_url { get; set; }
        public MotorAdditionalInfo motor_additional_info { get; set; }
        public ProposalTravelAdditionalInfo travel_additional_info { get; set; }
    }

    public class ChannelApplicationRefundDetails
    {
        public string proposal_number { get; set; }
        public string refund_id { get; set; }
        public string refund_status { get; set; }
        public string refund_amount { get; set; }
        public string core { get; set; }
    }

    public class PortingStatusUpdateReq
    {
        public string transaction_id { get; set; }
        public string trace_id { get; set; }
        public string proposal_number { get; set; }
        public string amount { get; set; }
        public string status { get; set; }
    }

    public class TestingWebhookReq
    {
        public string abc { get; set; }
    }

    public class MotorAdditionalInfo
    {
        public string breakin_id { get; set; }
        public string breakin_status { get; set; }
        public string breakin_remarks { get; set; }
    }
    public class ProposalTravelAdditionalInfo
    {
        public string trip_type { get; set; }
        public bool flight_tracking { get; set; }
        public string flight_tracking_url { get; set; }
        public List<ProposalTravellerDetails> traveller_details { get; set; }
    }

    public class ProposalTravellerDetails
    {
        public string name { get; set; }
        public bool isPed { get; set; }
    }
}
