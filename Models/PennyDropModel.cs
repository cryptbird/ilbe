namespace DigitalPaymentSystem_ILTC.Models
{
    public class PennyDropModel
    {
        public class PennyDropReq
        {
            public string bank_account_number { get; set; }
            public string bank_ifsc_code { get; set; }
            public string bank_account_holder_name { get; set; }
        }
    }
}
