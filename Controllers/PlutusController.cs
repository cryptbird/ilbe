using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PlutusModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    [Route("dps-agorax/")]
    public class PlutusController : ControllerBase
    {
        private readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/PlutusOrderStatus")]
        [ApiAuthentication]
        public async Task<ActionResult> PlutusOrderStatus([FromHeader] string Authorization, [FromBody] PlutusOrderStatusReq plutusOrderStatusReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PlutusBAL plutusBAL = new PlutusBAL();
            FetchOrderStatusResp fetchOrderStatusResp = new FetchOrderStatusResp();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    fetchOrderStatusResp = await plutusBAL.PlutusOrderStatus(plutusOrderStatusReq, "REALTIME - PLUTUS ORDER STATUS");

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Plutus Order Status fetched successfully";
                    response.TraceId = plutusOrderStatusReq.trace_id;
                    response.Data = fetchOrderStatusResp;
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusController.cs - PlutusOrderStatus()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }
            return Ok(response);

        }


        [HttpPost("api/v1/pay/PlutusWebhook")]
        public async Task PlutusWebhook([FromBody] JsonElement plutusCallbackRequest)
        {
            PlutusBAL plutusBAL = new PlutusBAL();

            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusController.cs - PlutusWebhook() ",
                    Message: "Plutus Callback Request: " + plutusCallbackRequest,
                    TraceId: null), ApplicationConfig.PLUTUS_LOGS);

                await plutusBAL.ExtractPlutusOrderDataViaCallbackWebhook(plutusCallbackRequest, "REALTIME - PLUTUS WEBHOOK");
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusController.cs - PlutusWebhook()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }

            return;
        }
    }
  }
