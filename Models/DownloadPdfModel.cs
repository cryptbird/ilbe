namespace DigitalPaymentSystem_ILTC.Models
{
    public class DownloadPdfModel
    {
        public class FetchPolicyDetails
        {
            public string proposalNumber { get; set; }
            public string id { get; set; }
            //public string PolicyNumber { get; set; }
        }

        public class UploadStreamRequest
        {
            public String AppName { get; set; }
            public String FolderIndex { get; set; }
            public String DocName { get; set; }
            public Byte[] DocData { get; set; }
            public string docstring { get; set; }
        }

        public class ResponseUrlPathModel
        {
            public string Urlpath { get; set; }
            public string traceid { get; set; }

        }

        public class Orion_SendCommunication_ResponseModel
        {
            public string traceid { get; set; }
            public int statuscode { get; set; }
            public List<string> messages { get; set; }
            public Orion_SendComm_Data data { get; set; }

        }

        public class Orion_SendComm_Data
        {
            public string ilRequestNo { get; set; }
            public string ilTemplateId { get; set; }
            public string referenceNo { get; set; }
            public string documentRequired { get; set; }
            public string communicationRequired { get; set; }
            public string document { get; set; }
            public string communication { get; set; }
            public string documentType { get; set; }
            public string base64ByteArray { get; set; }
        }

        public class Orion_SendCommunication
        {
            public string ilRequestNo { get; set; }
            public string ilTemplateId { get; set; }
            public string referenceNo { get; set; }
            public string documentRequired { get; set; }
            public string communicationRequired { get; set; }
            public string additionalinfo1 { get; set; }
            public string additionalinfo2 { get; set; }
            public string additionalinfo3 { get; set; }
            public string additionalinfo4 { get; set; }
            public string additionalinfo5 { get; set; }
            public string ilTemplateName { get; set; }
        }

        public class HttpCallModel
        {
            public string url { get; set; }
            public string Method { get; set; }
            public object postData { get; set; }
            public string CorrelationID { get; set; }
            public string servicename { get; set; }
            public bool useproxy { get; set; }
            public IDictionary<string, string> customheaders { get; set; }
            public string Mobilenumber { get; set; }
        }

        public class FetchPolicyDetailsFromDB
        {
            public string policy_number { get; set; }
            public string customer_id { get; set; }
            public string trace_id { get; set; }
            public string core { get; set; }
            public DateTime? payment_created_on { get; set; }
        }

        public class SendPolicyMailReq
        {
            public string email { get; set; }
            public string id { get; set; }
        }
    }
}
