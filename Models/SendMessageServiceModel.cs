using Newtonsoft.Json;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class SendMessageServiceModel
    {
        public class ILSancharRequest
        {
            public string dcode { get; set; }
            public string subuid { get; set; }
            public string pwd { get; set; }
            public string to { get; set; }
            public string msgtxt { get; set; }
            public string msgtype { get; set; }
            public string from { get; set; }
            public string OTPFlag { get; set; }
            public string ctype { get; set; }
        }

        public class ILSancharResponse
        {
            public string messageid { get; set; }
            public string responseid { get; set; }
            public int errCode { get; set; }
            public string errDescritption { get; set; }
            public bool success { get; set; }
        }

        public class WhatsAppDynamicParams
        {
            public string customerName { get; set; }
            public string amount { get; set; }
            public string paymentLink { get; set; }
            public string whatsappDeadline { get; set; } = "12 am";
            public string whatsappAttempts { get; set; } = "3";
            public string supportPhone { get; set; }
            public string supportEmail { get; set; }
            public string productName { get; set; }
            public string proposalList { get; set; }
            public string policyStartDate { get; set; }
            public string policyEndDate { get; set; }

            
        }

        public class WhatsappReqModel
        {
            public Userdetails userDetails { get; set; }
            public Notification notification { get; set; }
        }

        public class Userdetails
        {
            public string number { get; set; }
        }

        public class Notification
        {
            public string type { get; set; }
            public string sender { get; set; }
            public string templateId { get; set; }
            public Params @params { get; set; }

        }

        public class Params
        {
            [JsonProperty("1")]
            public string _1 { get; set; }
            [JsonProperty("2")]
            public string _2 { get; set; }
            [JsonProperty("3")]
            public string _3 { get; set; }
            [JsonProperty("4")]
            public string _4 { get; set; }
            [JsonProperty("5")]
            public string _5 { get; set; }
            [JsonProperty("6")]
            public string _6 { get; set; }
            [JsonProperty("7")]
            public string _7 { get; set; }
            [JsonProperty("8")]
            public string _8 { get; set; }
            [JsonProperty("9")]
            public string _9 { get; set; }
            [JsonProperty("10")]
            public string _10 { get; set; }
        }

        public class WhatsAppMedia
        {
            public string mediaBase64 { get; set; }
            public string mediaBase64Type { get; set; }
            public string title { get; set; }
        }

        public class WhatsappResModel
        {
            public string msgId { get; set; }
            public string message { get; set; }
        }
    }
}
