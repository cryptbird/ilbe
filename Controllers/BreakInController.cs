using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.BreakInModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{

    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class BreakInController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/CreateBreakIn")]
        [ApiAuthentication]
        public async Task<ActionResult> CreateBreakIn([FromHeader] string Authorization, [FromBody] CreateBreakInReq createBreakInReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            BreakInBAL breakInBAL = new BreakInBAL();
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await breakInBAL.BreakInGeneration(createBreakInReq, "REALTIME - CREATE BREAKIN");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "BreakInController.cs - CreateBreakIn()",    
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/BreakInWebhook")]
        public async Task<ActionResult> BreakInWebhook([FromBody] BreakInWebhookReq breakInWebhookReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            BreakInBAL breakInBAL = new BreakInBAL();

            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "BreakInController.cs - Request() ",
                   Message: "BreakIn Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                   TraceId: null), ApplicationConfig.BREAKIN_LOGS);

                response = await breakInBAL.BreakInWebhook(breakInWebhookReq, "REALTIME - BREAKIN WEBHOOK");
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInController.cs - BreakInWebhook()",
                    TraceId:breakInWebhookReq.CorelationId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/CheckBreakInStatus")]
        [ApiAuthentication]
        public async Task<ActionResult> CheckBreakInStatus([FromHeader] string Authorization, [FromBody] BreakInStatusReq breakInStatusReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            BreakInBAL breakInBAL = new BreakInBAL();
            try
            {

                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await breakInBAL.CheckBreakInStatus(breakInStatusReq, "REALTIME - CHECK BREAKIN STATUS");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInController.cs - CheckBreakInStatus()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/FetchPiSublocation")]
        [ApiAuthentication]
        public async Task<ActionResult> FetchPiSublocation([FromHeader] string Authorization, [FromBody] PiSublocationReq piSublocationReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            BreakInDAL breakInDAL = new BreakInDAL();
           
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await breakInDAL.FetchPiSublocation(piSublocationReq.Sublocation.Trim());
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInController.cs - FetchPiSublocation() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/FetchPincodeDetails")]
        [ApiAuthentication]
        public async Task<ActionResult> FetchPincodeDetails([FromHeader] string Authorization, [FromBody] FetchPincodeDetailsReq fetchPincodeDetailsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            BreakInBAL breakInBAL = new BreakInBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await breakInBAL.FetchPincodeDetails(fetchPincodeDetailsReq, "REALTIME - FETCH PINCODE DETAILS");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInController.cs - FetchPincodeDetails() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

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
