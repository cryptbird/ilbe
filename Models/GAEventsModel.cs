namespace DigitalPaymentSystem_ILTC.Models
{
    public class GAEventsModel
    {
        public class GAEventRequest
        {
            public string id { get; set; }
            public string product_value { get; set; }
        }

        public class  GAEventResponse
        {
            public WebRespBody TxnDetails { get; set; }
            public AgoraDBDetails AgoraDBDetails { get; set; }
        }

        public class WebRespBody 
        {
            public string CustomerName { get; set; }
            public string TransactionType { get; set; }
            public string TransactionAffiliation { get; set; }
            public string UserType { get; set; }
            public Policy Policy { get; set; }
            public double Amount { get; set; }
            public bool Success { get; set; }
            public int StatusCode { get; set; }
            public object TechnicalError { get; set; }
            public object DisplayMessage { get; set; }
            public string CorelationId { get; set; }
            public string TripType { get; set; }
        }

        public class Policy
        {
            public string ProposalId { get; set; }
            public string EmailId { get; set; }
            public string ProposalNo { get; set; }
            public string QuoteId { get; set; }
            public string PolicyStartDate { get; set; }
            public string PolicyEndDate { get; set; }
            public int PolicyTenure { get; set; }
            public string PolicyType { get; set; }
            public double BasicPremium { get; set; }
            public double Gst { get; set; }
            public string DealId { get; set; }
            public double Premium { get; set; }
            public string ProductDescription { get; set; }
            public string ProductSku { get; set; }
            public string mobile_number { get; set; }
            public string payment_mode { get; set; }
        }

        public class AgoraDBDetails
        {
            public string juspay_udf10 { get; set; }
            public string mobile_number { get; set; }
            public string payment_mode { get; set; }
        }

        public class WebReqBody
        {
            public string TransactionId { get; set; }
        }

        public class GAProposalDetails
        {
            public string NAME { get; set; }
            public string MOBILE_NUMBER { get; set; }
            public string PROPOSAL_ID { get; set; }
            public string PROPOSAL_TYPE { get; set; }
            public DateTime? PROPOSAL_START_DATE { get; set; }
            public DateTime? PROPOSAL_END_DATE { get; set; }
            public int PROPOSAL_TENURE { get; set; }
            public string BASIC_PREMIUM { get; set; }
            public string GST { get; set; }
            public string TOTAL_AMOUNT { get; set; }
            public string DEAL_ID { get; set; }
            public string PAYMENT_MODE { get; set; }
            public string PROPOSAL_NUMBER { get; set; }
            public string JOURNEY_TYPE { get; set; }
            public string PRODUCT { get; set; }
            public string EMAIL { get; set; }
            public string TRIP_TYPE { get; set; }
            public int SUM_INSURED { get; set; }
            public bool PED { get; set; }
        }

    }
}

