using System.ComponentModel.DataAnnotations;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class AuthenticateModel
    {

        public class AuthenticateToken
        {
            public string accessToken { get; set; }
            public string expiryTime { get; set; }
        }

        public class AuthenticateTokenData
        {
            public string username { get; set; }
            public string unique_name { get; set; }
            public string jti { get; set; }
            public string Role { get; set; }
            public string UserLogin { get; set; }
            public string Client { get; set; }
            public string CCId { get; set; }
            public string IsGuest { get; set; }
            public string SessionId { get; set; }
            public string SKey { get; set; }
            public string SP { get; set; }
        }

        public class RefreshTokenReq
        {
            [Required(ErrorMessage = "Id is required.")]
            public string id { get; set; }
        }

        public class TxnAuthDetails
        {
            public string username { get; set; }
            public string password { get; set; }
            public DateTime? txn_created_on { get; set; }
            public DateTime? payment_done_at {get; set; }
        }

        public class RefreshTokenResp
        {
            public AuthenticateToken authTokenData { get; set; }
            public bool isRefreshed { get; set; } = false;
        }
    }
}
