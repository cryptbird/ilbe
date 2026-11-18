using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.DropLeadModels;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{

    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class DropLeadController: ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/SmeDropLead")]
        [ApiAuthentication]

        public async Task<ActionResult> SmeDropLead([FromHeader] string Authorization, [FromBody] DropLeadRequest request)
        {
            DropLeadBAL dropLeadBAL = new DropLeadBAL();
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await dropLeadBAL.SmeDropLeadDetails(request, "REALTIME - SME SROP LEAD");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DropLeadController.cs - SmeDropLead()",
                       TraceId: null,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.DROP_LEAD_LOGS);

            }
            return Ok(response);
        }

    }
}
