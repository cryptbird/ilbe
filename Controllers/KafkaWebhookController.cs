using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.BreakInModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class KafkaWebhookController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/AsbaPaymentStatusWebhook")]
        [ApiAuthentication]
        public async Task<AsbaWebhookRespModel> AsbaPaymentStatusWebhook([FromHeader] string Authorization, [FromBody] AsbaWebhookReqModel asbaWebhookReq)
        {
            AsbaWebhookRespModel response = new AsbaWebhookRespModel();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            KafkaWebhookBAL kafkaWebhookBAL = new KafkaWebhookBAL();

            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookController.cs - Request() ",
                    Message: "AsbaPaymentStatusWebhook Request: " + JsonConvert.SerializeObject(asbaWebhookReq),
                    TraceId: null), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await kafkaWebhookBAL.AsbaPaymentStatusWebhook(asbaWebhookReq, authenticateTokenData.username, "REALTIME - ASBA PAYMENT STATUS WEBHOOK");
                }
                else
                {
                    response.Success = false;
                }
                return response;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookController.cs - AsbaPaymentStatusWebhook() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                response.Success = false;
                return response;
            }
        }

        [HttpPost("api/v1/pay/PolicyNumberStatusWebhook")]
        [ApiAuthentication]
        public async Task<PolicyNumberTopicResp> PolicyNumberStatusWebhook([FromHeader] string Authorization, [FromBody] PolicyNumberTopicReq policyNumberTopicReq)
        {
            PolicyNumberTopicResp response = new PolicyNumberTopicResp();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            KafkaWebhookBAL kafkaWebhookBAL = new KafkaWebhookBAL();
            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookController.cs - Request() ",
                    Message: "PolicyNumberStatusWebhook Request: " + JsonConvert.SerializeObject(policyNumberTopicReq),
                    TraceId: null), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await kafkaWebhookBAL.PolicyNumberStatusWebhook(policyNumberTopicReq, "REALTIME - POLICY NUMBER STATUS WEBHOOK");
                }else
                {
                    response.Success = false;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookController.cs - PolicyNumberStatusWebhook() ",
                    TraceId: null,
                    Message: "Proposal No:- " + policyNumberTopicReq.ProposalNo + " Exception:- " + $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                response.Success = false;
                return response;
            }

            return response;
        }
    }
}
