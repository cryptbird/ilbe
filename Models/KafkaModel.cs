namespace DigitalPaymentSystem_ILTC.Models
{

    public class AsbaWebhookReqModel
    {
        public string? P1 { get; set; }
        public string? P2 { get; set; }
        public string? IssuanceSystem { get; set; } 
        public double RetainAmount { get; set; }
        public double RefundAmount { get; set; }
        public string? PID { get; set; }
        public string? MandateID { get; set; }
        public string? Status { get; set; }
    }

    public class AsbaWebhookRespModel
    {
        public bool? Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ProposalDetails
    {
        public string TRACE_ID { get; set; }
        public string PROPOSAL_NUMBER { get; set; }
        public string? POLICY_NUMBER { get; set; }
        public string TRANSACTION_ID { get; set; }
        public string CUSTOMER_ID { get; set; }
        public string APPLICATION_NAME { get; set; }
        public string AUTH_CODE { get; set; }
        public string MANDATE_ID { get; set; }
        public string PAYMENT_ID { get; set; }
        public string ORDER_ID { get; set; }
        public string IS_ASBA_PROCESSED { get; set; }
    }

    public class PolicyNumberTopicReq
    {
        public string? ProposalNo { get; set; }
        public string? PolicyStatus { get; set; }
        public string? PolicySubStatus { get; set; }
        public string? PolicyNo { get; set; }
    }
    public class PolicyNumberTopicResp
    {
        public bool? Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class UpdatePIDReq
    {
        public string trace_id { get; set; }
        public string proposal_number { get; set; }
        public string receiptNumber { get; set; }
        public string mandateNumber { get; set; }
        public string instrumentAmount { get; set; }
        public string instrumentNumber { get; set; }
        public string instrumentDate { get; set; }
    }

    public class UpdatePIDResp 
    {
        public string TraceId { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public UpdatedPID Data { get; set; }
    }
     
    public class UpdatedPID
    {
        public string receiptNumber { get; set; }
        public string isProcessed { get; set; }
    }

}
