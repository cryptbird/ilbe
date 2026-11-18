namespace DigitalPaymentSystem_ILTC.Models
{
    public class ReverseSyncTokenModel
    {
        public class WebTokenRequest
        {
            public string Client { get; set; }
            public string Scope { get; set; }
        }

        public class WebTokenResponse
        {
            public string Passcode { get; set; }
            public DateTime Expiry { get; set; }
            public bool Success { get; set; }
            public int StatusCode { get; set; }
            public object TechnicalError { get; set; }
            public object DisplayMessage { get; set; }
            public string CorelationId { get; set; }
        }

        public class WebTokenEncryptedReq
        {
            public string kv { get; set; }
            public string payload { get; set; }
            public string version { get; set; }
        }

        public class WebEncrypted
        {
            public string msg { get; set; }
        }

        public class ILTCTokenRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class ILTCTokenResponse
        {
            public int statusCode { get; set; }
            public string statusMessage { get; set; }
            public Data data { get; set; }
        }

        public class Data
        {
            public string token { get; set; }
            public DateTime expiry { get; set; }
        }
    }
}
