using System;
using System.Collections.Generic;
using System.Text.Json;
using Newtonsoft.Json;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class RefundModel
    {
        public class RefundReq
        {
            public string proposal_number { get; set; }
            public string auth_code { get; set; }
            public string policy_number { get; set; }
            public string proposal_amount { get; set; }
            public string canc_proposal_number { get; set; }
            public string transaction_id { get; set; }
        }

        public class FetchRefundDetails
        {
            public string trace_id { get; set; }
            public string core { get; set; }
            public string dps_order_id { get; set; }
            public string channel_application_details { get; set; }
            public string auth_code { get; set; }
            public string proposal_amount { get; set; }
            public string product { get; set; }
            public string application_name { get; set; }
            public string payment_id { get; set; }
            public string proposal_start_date { get; set; }
            public string payer_type { get; set; }
            public string customer_id { get; set; }
            public string refund_proposal_number { get; set; }
            public string voucher_number { get; set; }
            public string refund_status { get; set; }
            public string policy_number { get; set; }
            public string refund_id { get; set; }

            public bool IsValid()
            {
                return !new[]
                {
            trace_id, core, dps_order_id,
            auth_code, proposal_amount, product, application_name,
            payment_id, proposal_start_date
        }.Any(string.IsNullOrEmpty);
            }

        }

        public class FetchRefundCase
        {
            public string proposal_number { get; set; }
            public string auth_code { get; set; }
            public string policy_number { get; set; }
            public string proposal_amount { get; set; }
            public string canc_proposal_number { get; set; }
            public string transaction_id { get; set; }
            public string trace_id { get; set; }
            public string core { get; set; }
            public string dps_order_id { get; set; }
            public string channel_application_details { get; set; }
            public string product { get; set; }
            public string application_name { get; set; }
            public string payment_id { get; set; }
            public string proposal_start_date { get; set; }
            public string payer_type { get; set; }
            public string customer_id { get; set; }
            public string voucher_number { get; set; }
            public string refund_status { get; set; }
            public string refund_id { get; set; }
        }

        public class FetchApplicationDetails
        {
            public string reverse_sync_url { get; set; }
            public string juspay_mid { get; set; }
        }

        public class FetchBreakInDetailsByProposalNo
        {
            public string breakin_id { get; set; }
            public string status { get; set; }
            public string remark { get; set; }
        }

        public class JuspayRefundRequest
        {
            public string unique_request_id { get; set; }
            public string amount { get; set; }
            public string order_id { get; set; }
        }

        public class RefundResp
        {
            public string refund_status { get; set; }
        }

    }
}
