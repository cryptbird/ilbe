using System.Text.Json;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class RetryResyncModel
    {
        public class RetryResyncCase
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
            public string business_vertical { get; set; }
            public string sub_vertical { get; set; }
            public string marketing_officer { get; set; }
            public string proposer_code { get; set; }
            public string channel_application_details { get; set; }
            public string journey_type { get; set; }
            public string proposal_id { get; set; }
            public string proposal_type { get; set; }
            public string pg_name { get; set; }
            public bool is_ilemi_proposal_modified { get; set; }
            public bool is_ilemi_proposal_mod_reqd { get; set; }
        }

        public class RetryResyncCaseResp
        {
            public string proposal_number { get; set; }
            public string status { get; set; }
        }

        public class RetryResyncGetPortingUpdateReq
        {
            public string trace_id { get; set; }
            public string proposal_number { get; set; }
        }
    }
}


