using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.MiscModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class MiscController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/GetFooterContent")]
        [ApiAuthentication]
        public async Task<ActionResult> GetFooterContent([FromHeader] string Authorization, [FromBody] FooterContentRequest request)
        {
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            MiscBAL miscBAL = new MiscBAL();
            try
            {
                if (request == null || string.IsNullOrEmpty(request.id))
                {
                    return BadRequest("id is required");
                }

                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    using HttpResponseMessage response = await miscBAL.GetFooterContent(request.id, "REALTIME - GET FOOTER CONTENT");
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, responseBody);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
                
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "MiscController.cs - GetFooterContent() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                return StatusCode(500, ex.Message);
            }
        }
    }
}
