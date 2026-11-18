using System.Text.Json.Nodes;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class AlertLogsModel
    {
        public class AlertConfig
        {
            public int alert_id { get; set; }
            public string alert_event { get; set; }
            public string alert_webhook { get; set; }
        }

        public class AlertLogsReq
        {
            public string alert_event { get; set; }
            public JsonArray alert_attachment { get; set; }
            public string alert_type { get; set; }
        }
    }
}
