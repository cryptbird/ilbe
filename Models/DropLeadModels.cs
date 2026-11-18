namespace DigitalPaymentSystem_ILTC.Models
{
    public class DropLeadModels
    {
        public class DropLeadRequest
        {
            public string id { get; set; }
        }
        public class DropLeadDetailsResp
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        public class SMEDropLeadRequest
        {
            public string MobileNumber { get; set; }
            public string ProductCode { get; set; }
        }


        public class SMEDropLeadResponse
        {
            public int LeadId { get; set; }
            public string ReferenceNo { get; set; }
            public int ErrorId { get; set; }
            public bool Success { get; set; }
            public int StatusCode { get; set; }
            public object TechnicalError { get; set; }
            public object DisplayMessage { get; set; }
            public object CorelationId { get; set; }
        }

        public class LeadDetailsResp 
        { 
            public string product { get; set; }
            public string mobile_number { get; set; }
        }

        public class RmDetails
        {
            public string RM_TEXT { get; set; }
            public string CALLBACK_PRODUCT_NAME { get; set; }
            public string IS_CALLBACK_REQD { get; set; }
        }

    }
}
