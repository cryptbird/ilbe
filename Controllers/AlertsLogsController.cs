using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.AlertLogsModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class AlertsLogsController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/CallAlert")]
        public async Task<ActionResult> CallAlert([FromHeader] string Authorization, [FromBody] AlertLogsReq alertLogsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AlertLogsBAL alertLogsBAL = new AlertLogsBAL();

            try
            {
                response = await alertLogsBAL.CallAlert(alertLogsReq.alert_event, alertLogsReq.alert_attachment, alertLogsReq.alert_type);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AlertLogsController.cs - CallAlert() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ALERT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }
    }
}
