using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class PlutusModel
    {
        public class PlutusTokenReq
        {
            public string UserName { get; set; }
            public string SecretKey { get; set; }
        }

        public class PlutusTokenResp
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public DateTime timestamp { get; set; }
            public object errorMessage { get; set; }
            public bool isSuccess { get; set; }
            public TokenData data { get; set; }
        }

        public class TokenData
        {
            public string accessToken { get; set; }
            public string expiresIn { get; set; }
        }

        public class PlutusOrderStatusReq
        {
            public string trace_id { get; set; }
            public string order_id { get; set; }
            public string dps_order_id { get; set; }
            public string transaction_id { get; set; }
        }

        public class PlutusOrderStatusRequest
        {
            public string TransactionID { get; set; }
        }

        public class PlutusOrderStatusResponse
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public string timestamp { get; set; }
            public string errorMessage { get; set; }
            public bool isSuccess { get; set; }
            public PlutusOrderStatusData data { get; set; }
        }

        public class PlutusOrderStatusData
        {
            public int paymentStatus { get; set; }
            public string authCode { get; set; }
            public string paymentMessage { get; set; }
            public string pg_order_id { get; set; }
            public List<PlutusProposalData> proposalDetails { get; set; }
            public string payment_type { get; set; }
            public string payment_mode { get; set; }
            public PlutusMandateDetails mandateDetails { get; set; }
        }

        public class PlutusProposalData
        {
            public string proposalNo { get; set; }
            public string policyNo { get; set; }
            public string pid { get; set; }
            public string coverNoteNo { get; set; }
            public string authCode { get; set; }
        }

        public class PlutusMandateDetails
        {
            public string frequency { get; set; }
            public string mandateMaxLimit { get; set; }
            public string mandateExpiry { get; set; }
            public string mandateType { get; set; }
            public string plutusMandateId { get; set; }
            public string mandateStatus { get; set; }
        }

        public class GetPlutusOrderStatusData
        {
            public string pg_name { get; set; }
            public string order_id { get; set; }
            public string transaction_id { get; set; }
        }

        public static Dictionary<int, string> plutus_payment_status_map = new Dictionary<int, string>
        {
            { 0, "NEW" },
            { 1, "Pending" },
            { 2, "Success" },
            { 3, "Failed" }
        };


        public enum Payment_Action
        {
            Payment_Collection = 0,
            Payment_Entry = 1,
            Payment_Entry_Tagging = 2,
            Payment_Tagging = 4
        }

        public enum Core_Type
        {
            PF = 0,
            Artemis = 1
        }


        public class PlutusAddPaymentRequest
        {
            public string CorrelationId { get; set; }
            public string Email { get; set; }
            public string ContactNo { get; set; }
            public int Amount { get; set; }
            public string Name { get; set; }
            public int PaymentAction { get; set; }
            public bool IsRedirectRequired { get; set; }
            public string RedirectUrl { get; set; }
            public string SyncUrl { get; set; }
            public string PayerType { get; set; } = "C";
            public string IntermediaryCode { get; set; }
            public int CoreType { get; set; }
            public string AgentName { get; set; }
            public string ProductCode { get; set; }
            public string MarketingOfficer { get; set; }
            public string BusinessVertical { get; set; }
            public string SubVertical { get; set; }
            public string OfficeCode { get; set; }
            public Notes Notes { get; set; }
            public Paymentfilters PaymentFilters { get; set; }
            public List<Proposaldetail> ProposalDetails { get; set; }
            public object BimaPayInfo { get; set; }
            public bool IsDirectPayment { get; set; } = false;
            public bool IsAdvancePayment { get; set; } = false;
            public string AgentUid { get; set; }
            public bool isRecurringPayment { get; set; } = false;
            public PlutusMandateDetails mandateDetails { get; set; }
        }

        public class Notes
        {
            public string udf1 { get; set; }
            public string udf2 { get; set; }
            public string udf3 { get; set; }
            public string udf4 { get; set; }
            public string udf5 { get; set; }
            public string udf6 { get; set; }
            public object udf7 { get; set; }
            public string udf8 { get; set; }
            public string udf9 { get; set; }
            public string udf10 { get; set; }
            public string AgoraX_TxnID { get; set; }
            public string AgoraX_OrderID { get; set; }
        }

        public class Paymentfilters
        {
            public List<string> Online { get; set; }
            public List<string> Offline { get; set; }
            public bankFilter bankFilter { get; set; }
            public brandFilter brandFilter { get; set; }
            public emiFilter emiFilter { get; set; }
            public advPIDFilter AdvancePIDFilter { get; set; }
        }

        public class bankFilter
        {
            public List<string> NB { get; set; }
            public List<string> CARDS { get; set; }
        }

        public class brandFilter
        {
            public List<string> CARDS { get; set; }
        }

        public class emiFilter
        {
            public List<string> CREDIT_CARD_EMI_MODE { get; set; }
            public List<string> DEBIT_CARD_EMI_MODE { get; set; }
        }

        public class advPIDFilter
        {
            public List<string> Title { get; set; }
            public List<string> View { get; set; }
        }

        public class Proposaldetail
        {
            public string ProposalNo { get; set; }
            public string CustomerId { get; set; }
            public string DealID { get; set; }
            public int IsRenewal { get; set; }
            public object PreviousPolicyNo { get; set; }
            public object PolicyNo { get; set; }
            public object Pid { get; set; }
            public object CoverNoteNo { get; set; }
            public object AuthCode { get; set; }
        }



        public class PlutusAddPaymentResponse
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public DateTime timestamp { get; set; }
            public object errorMessage { get; set; }
            public bool isSuccess { get; set; }
            public Data data { get; set; }
        }

        public class Data
        {
            public string paymentURL { get; set; }
            public int transactionId { get; set; }
            public object proposal_no { get; set; }
            public object policy_no { get; set; }
            public object paymentID { get; set; }
        }

        public class PlutusMandateReq
        {
            public string plutusMandateId { get; set; }

            [Required(ErrorMessage = "Mandate action is required.")]
            [RegularExpression(@"^(?i)(STATUS|EXECUTE|REVOKE)$", ErrorMessage = "Mandate action must be 'STATUS', 'EXECUTE', 'REVOKE'.")]
            public string mandateAction { get; set; }

            public string amount { get; set; }
            public DateTime mandateExecutionTime { get; set; }
            public string customerId { get; set; }
            public string trace_id { get; set; }
        }

        public class PlutusMandateResp
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public string timestamp { get; set; }
            public string errorMessage { get; set; }
            public bool isSuccess { get; set; }
            public PlutusMandateData data { get; set; }
        }

        public class PlutusMandateData
        {
            public int mandateId { get; set; }
            public string plutusMandateId { get; set; }
            public string mandateStatus { get; set; }
            public string customerId { get; set; }
            public decimal amount { get; set; }
            public string executionTime { get; set; }
            public string transactionId { get; set; }
            public string authCode { get; set; }
            public string paymentStatus { get; set; }
            public DateTime? createdDate { get; set; }
            public DateTime? updatedDate { get; set; }
        }
    }
}
