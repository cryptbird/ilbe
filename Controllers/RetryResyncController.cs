using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class RetryResyncController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();


        [HttpPost("api/v1/pay/STPRetryResync")]
        public async Task<DPSStatusResponse> STPRetryResync()
        {
            DPSStatusResponse response = new DPSStatusResponse();
            RetryResyncBAL retryResyncBAL = new RetryResyncBAL();

            try
            {
                response = await retryResyncBAL.STPRetryResync("REALTIME - STP RETRY RESYNC");
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncController.cs - STPRetryResync()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        [HttpPost("api/v1/pay/NSTPRetryResync")]
        public async Task<DPSStatusResponse> NSTPRetryResync()
        {
            DPSStatusResponse response = new DPSStatusResponse();
            RetryResyncBAL retryResyncBAL = new RetryResyncBAL();

            try
            {
                response = await retryResyncBAL.NSTPRetryResync("REALTIME - NSTP RETRY RESYNC");
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncController.cs - NSTPRetryResync()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }


        [HttpPost("api/v1/pay/PortingStatusUpdate")]
        public async Task<ActionResult> GetPortingUpdate(RetryResyncGetPortingUpdateReq retryResyncGetPortingUpdateReq)
        {
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();
            string is_porting_done = string.Empty;

            try
            {
                 is_porting_done = await retryResyncDAL.GetPortingUpdate(retryResyncGetPortingUpdateReq.trace_id, retryResyncGetPortingUpdateReq.proposal_number);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncController.cs - RetryResyncGetPortingUpdate()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return Ok(is_porting_done);
        }
    }
}