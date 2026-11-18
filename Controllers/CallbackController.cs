using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class CallbackController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/CallbackWebhook")]
        public async Task CallbackWebhook([FromBody] JsonElement callbackRequest)
        {
            CallbackBAL callbackBAL = new CallbackBAL();

            try
            {               
                await callbackBAL.ExtractOrderDataViaCallbackWebhook(callbackRequest, "REALTIME - CALLBACK WEBHOOK");

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackController.cs - CallbackWebhook() ",
                    Message: "Callback Request: " + callbackRequest,
                    TraceId: null), ApplicationConfig.CALLBACK_LOGS);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackController.cs - CallbackWebhook()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return;
        }


        [HttpPost("api/v1/pay/TestingWebhook")]
        public async Task<string> CallbackWebhook2([FromBody] TestingWebhookReq abc)
        {
            CallbackBAL callbackBAL = new CallbackBAL();

            try
            {
                //await callbackBAL.ExtractOrderDataViaCallbackWebhook(callbackRequest, "REALTIME - CALLBACK WEBHOOK");

                await Task.Delay(25000);

                return "Testing Success";
            }
            catch (Exception ex)
            {
                //objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                //    MethodName: "CallbackController.cs - CallbackWebhook()",
                //    TraceId: null,
                //    Message: $"Exception occurred: {ex.Message}",
                //    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return "Testing Failed";
        }


        [HttpPost("api/v1/pay/WebhookFallback")]
        public async Task WebhookFallback()
        {
            CallbackBAL callbackBAL = new CallbackBAL();

            try
            {
                await callbackBAL.ExtractOrderDataWebhookFallback("REALTIME - WEBHOOK FALLBACK");
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackController.cs - WebhookFallback()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return;
        }


        [HttpPost("api/v1/pay/ChannelApplicationOrderDetails")]
        [ApiAuthentication]
        public async Task<DPSStatusResponse> ChannelApplicationOrderDetails([FromBody] ChannelApplicationOrderDetailsReq channelApplicationOrderDetailsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            CallbackBAL callbackBAL = new CallbackBAL();

            try
            {
                response = await callbackBAL.ChannelApplicationOrderDetails(channelApplicationOrderDetailsReq, "REALTIME - CHANNEL APPLICATION ORDER DETAILS");
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackController.cs - ChannelApplicationOrderDetails()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }
    }
}
