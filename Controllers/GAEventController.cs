using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.GAEventsModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]

    public class GAEventController : Controller
    {

        private readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/GAEvents")]
        [ApiAuthentication]
        public async Task<ActionResult> GAEvents([FromHeader] string Authorization, [FromBody] GAEventRequest request)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            GAEventsBAL gAEventsBAL = new GAEventsBAL();
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await gAEventsBAL.GAEvents(request);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }

            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "GAEventController.cs - GAEvents()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.GA_EVENT_LOGS);
            }
            return Ok(response);
        }
    }
}
