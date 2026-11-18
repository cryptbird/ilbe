using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class DashboardModel
    {
        public class DashboardLoginReq
        {
            [Required(ErrorMessage = "Email is required.")]
            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
            public string email { get; set; }
        }

        public class DashboardLoginVerifyReq
        {
            [Required(ErrorMessage = "Email is required.")]
            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
            public string email { get; set; }

            [Required(ErrorMessage = "OTP is required.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Invalid OTP")]
            public string otp { get; set; }
        }

        public class TransactionSummaryReq()
        {
            [Required(ErrorMessage = "Input Key is required.")]
            [RegularExpression(@"^(?i)(TRANSACTION_ID|PROPOSAL_NUMBER)$", ErrorMessage = "Input Key must be either 'TRANSACTION_ID' or 'PROPOSAL_NUMBER'.")]
            public string input_key { get; set; }

            [Required(ErrorMessage = "Input Value is required.")]
            public string input_value { get; set; }

            [Required(ErrorMessage = "Email is required.")]
            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
            public string email { get; set; }
        }

        public class TransactionSummaryResp
        {
            public string transaction_id { get; set; }
            public string trace_id { get; set; }
            public string total_amount { get; set; }
            public string application_name { get; set; }
            public string product { get; set; }
            public string core { get; set; }
            public string proposal_number { get; set; }
            public decimal proposal_amount { get; set; }
            public string transaction_initiation_time { get; set; }
            public string proposal_status { get; set; }
            public string dps_order_id { get; set; }
            public string order_id { get; set; }
            public string auth_code { get; set; }
            public string payment_id { get; set; }
            public string policy_number { get; set; }
            public int no_of_attempts { get; set; }
            public string payment_success_time { get; set; }
            public string payment_mode { get; set; }
            public string resync_status { get; set; }
            public string proposal_start_date { get; set; }
            public string deal_id { get; set; }
            public string intermediary_code { get; set; }
            public string customer_id { get; set; }
            public string name { get; set; }
            public string email { get; set; }
            public string mobile_number { get; set; }
            public string payer_type { get; set; }
            public string breakin_id { get; set; }
            public string breakin_status { get; set; }
            public string breakin_remark { get; set; }
            public string breakin_inspection_type { get; set; }
            public string breakin_inspection_date { get; set; }
            public string breakin_address { get; set; }
            public string breakin_pincode { get; set; }
            public string breakin_city { get; set; }
            public string breakin_state { get; set; }
            public string breakin_creation_time { get; set; }
            public string breakin_clear_inspection_status { get; set; }
            public string vrn { get; set; }
            public string ias_id { get; set; }
            public string refund_status { get; set; }
            public string refund_proposal_number { get; set; }
            public string pf_mid { get; set; }
            public string artemis_mid { get; set; }
            public string reverse_sync_url { get; set; }
            public string is_nstp_flow { get; set; }
            public string channel_application_details { get; set; }
            public string journey_type { get; set; }
            public DashboardTimeline dashboardTimeline { get; set; }
        }

        public class TransactionSummaryRespForNonAdmin
        {
            public string transaction_id { get; set; }
            public string trace_id { get; set; }
            public string total_amount { get; set; }
            public string application_name { get; set; }
            public string product { get; set; }
            public string core { get; set; }
            public string proposal_number { get; set; }
            public decimal proposal_amount { get; set; }
            public string transaction_initiation_time { get; set; }
            public string proposal_status { get; set; }
            public string dps_order_id { get; set; }
            public string order_id { get; set; }
            public string auth_code { get; set; }
            public string payment_id { get; set; }
            public string policy_number { get; set; }
            public int no_of_attempts { get; set; }
            public string payment_success_time { get; set; }
            public string payment_mode { get; set; }
            public string resync_status { get; set; }
            public string proposal_start_date { get; set; }
            public string deal_id { get; set; }
            public string intermediary_code { get; set; }
            public string customer_id { get; set; }
            public string name { get; set; }
            public string email { get; set; }
            public string mobile_number { get; set; }
            public string payer_type { get; set; }
            public string breakin_id { get; set; }
            public string breakin_status { get; set; }
            public string breakin_remark { get; set; }
            public string breakin_inspection_type { get; set; }
            public string breakin_inspection_date { get; set; }
            public string breakin_address { get; set; }
            public string breakin_pincode { get; set; }
            public string breakin_city { get; set; }
            public string breakin_state { get; set; }
            public string breakin_creation_time { get; set; }
            public string breakin_clear_inspection_status { get; set; }
            public string vrn { get; set; }
            public string ias_id { get; set; }
            public string refund_status { get; set; }
            public DashboardTimeline dashboardTimeline { get; set; }
        }

        public class DashboardTimeline
        {
            public bool retryButton { get; set; } = true;
            public DashboardTimelineEvents dashboardTimelineEvents { get; set; }

        }

        public class DashboardTimelineEvents
        {
            public string payment_initiation { get; set; }
            public string payment_status { get; set; }
            public string payment_id_generation { get; set; }
            public string ias_clearance { get; set; }
            public string proposal_status_1 { get; set; }
            public string breakin_id_creation { get; set; }
            public string breakin_clearance { get; set; }
            public string proposal_status_2 { get; set; }
            public string porting_status { get; set; }
            public string policy_number_generation { get; set; }
            public string reverse_sync { get; set; }
            public string refund_initiation_status { get; set; }
        }

        public class DashboardCheckProposalStatusReq
        {
            public string trace_id { get; set; }
            public string proposal_number { get; set; }
            public string core { get; set; }
            public string deal_id { get; set; }
            public string intermediary_code { get; set; }
        }

        public class DashboardCheckProposalStatusResp
        {
            public string proposal_number { get; set; }
            public string proposal_status { get; set; }
        }

        public class DashboardEncryptDataResp
        {
            public string encryptedData { get; set; }
        }

        public class DashboardDecryptDataReq
        {
            public string encryptedData { get; set; }
        }

        public class DashboardDecryptDataResp
        {
            public string decryptedData { get; set; }
        }

        public class GetTableColumnsReq
        {
            [Required(ErrorMessage = "Table name is required.")]
            public string table_name { get; set; }
        }

        public class DashboardOnboardUserReq
        {
            [Required(ErrorMessage = "Application name is required.")]
            public string application_name { get; set; }

            [Required(ErrorMessage = "Application ID is required.")]
            public string application_id { get; set; }

            [Required(ErrorMessage = "PF MID is required.")]
            public string pf_mid { get; set; }

            [Required(ErrorMessage = "Artemis MID is required.")]
            public string artemis_mid { get; set; }

            [Required(ErrorMessage = "Juspay MID is required.")]
            public string juspay_mid { get; set; }

            [Required(ErrorMessage = "Reverse sync URL is required.")]
            public string reverse_sync_url { get; set; }
        }

        public class DashboardOnboardUserResp
        {
            public string application_name { get; set; }
            public string message { get; set; }
        }

        public class GetTemplateIdsReq
        {
            [Required(ErrorMessage = "Application name is required.")]
            public string application_name { get; set; }
        }

        public class TemplateIdResp
        {
            public int template_id { get; set; }
            public string product { get; set; }
            public int is_active { get; set; }
        }

        public class ToggleTemplateStatusReq
        {
            [Required(ErrorMessage = "Template ID is required.")]
            [Range(1, int.MaxValue, ErrorMessage = "Template ID must be greater than 0")]
            public int templateId { get; set; }

            [Required(ErrorMessage = "IsActive flag is required.")]
            public bool isActive { get; set; }
        }

        public class ToggleTemplateStatusResp
        {
            public int template_id { get; set; }
            public int is_active { get; set; }
            public string message { get; set; }
        }

        public class ProductListResp
        {
            public string product_key { get; set; }
        }

        public class CreateTemplateReq
        {
            [Required(ErrorMessage = "Application name is required.")]
            public string application_name { get; set; }

            [Required(ErrorMessage = "Product is required.")]
            public string product { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "UPI order must be 0 or greater")]
            public int upi { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "Credit order must be 0 or greater")]
            public int credit { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "Debit order must be 0 or greater")]
            public int debit { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "Netbanking order must be 0 or greater")]
            public int netbanking { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "Wallet order must be 0 or greater")]
            public int wallet { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "Bima ASBA order must be 0 or greater")]
            public int bima_asba { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "EMI order must be 0 or greater")]
            public int emi { get; set; }

            [Required(ErrorMessage = "IsActive is required.")]
            [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
            public int is_active { get; set; }
        }

        public class CreateTemplateResp
        {
            public int template_id { get; set; }
            public string product { get; set; }
            public string application_name { get; set; }
            public string return_loader_url { get; set; }
            public string return_status_url { get; set; }
            public string message { get; set; }
        }

        public class ExecuteRefreshReq
        {
            [Required(ErrorMessage = "Email is required.")]
            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email address is in the wrong format")]
            public string email { get; set; }

            [Required(ErrorMessage = "Query is required.")]
            public string cache { get; set; }
        }
        public class ExecuteRefreshResp
        {
            public string cache { get; set; }
            public List<Dictionary<string, object>> rows { get; set; }
            public string count { get; set; }
        }
    }
}