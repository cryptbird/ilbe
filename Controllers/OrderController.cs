using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class OrderController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/Session")]
        [ApiAuthentication]
        public async Task<ActionResult> Session([FromHeader] string Authorization, [FromBody] SessionReq sessionReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await orderBAL.Session(sessionReq, authenticateTokenData.username, "REALTIME - SESSION");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderController.cs - Session() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/FetchOrderStatus")]
        [ApiAuthentication]
        public async Task<ActionResult> GetOrderStatus([FromHeader] string Authorization, [FromBody] FetchOrderStatusReq fetchOrderStatusReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await orderBAL.GetOrderStatus(fetchOrderStatusReq, authenticateTokenData.username, "REALTIME - FETCH ORDER STATUS");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "OrderController.cs - GetOrderStatus() ",
                   TraceId: fetchOrderStatusReq.trace_id,
                   Message:$"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            return Ok(response);
        }


        [HttpPost("api/v1/pay/FetchRedirectionUrl")]
        [ApiAuthentication]
        public async Task<ActionResult> FetchRedirectionUrl([FromHeader] string Authorization, [FromBody] FetchRedirectionUrlReq fetchRedirectionUrlReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await orderBAL.FetchRedirectionUrl(fetchRedirectionUrlReq, "REALTIME - FETCH REDIRECTION URL");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "OrderController.cs - FetchRedirectionUrl() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/FetchPolicyDetailsByID")]
        [ApiAuthentication]
        public async Task<ActionResult> FetchPolicyDetailsByID([FromHeader] string Authorization, [FromBody] FetchPolicyDetailsByIDReq fetchPolicyDetailsByIDReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await orderBAL.FetchPolicyDetailsByID(fetchPolicyDetailsByIDReq, "REALTIME - FETCH POLICY DETAILS BY ID");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderController.cs - FetchPolicyDetailsByID() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/CaptureMandate")]
        [ApiAuthentication]
        public async Task<ActionResult> CaptureMandate([FromHeader] string Authorization, [FromBody] CaptureMandateReq captureMandateReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            MandateExecutionResp mandateExecutionResp = new MandateExecutionResp();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    mandateExecutionResp = await orderBAL.CaptureMandate(captureMandateReq, "REALTIME - CAPTURE MANDATE");

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "";
                    response.TraceId = captureMandateReq.trace_id;
                    response.Data = mandateExecutionResp;
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "OrderController.cs - CaptureMandate() ",
                   TraceId: captureMandateReq.trace_id,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/RevokeMandate")]
        [ApiAuthentication]
        public async Task<ActionResult> RevokeMandate([FromHeader] string Authorization, [FromBody] RevokeMandateReq revokeMandateReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            MandateExecutionResp mandateExecutionResp = new MandateExecutionResp();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    mandateExecutionResp = await orderBAL.RevokeMandate(revokeMandateReq, "REALTIME - REVOKE MANDATE");

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "";
                    response.TraceId = revokeMandateReq.trace_id;
                    response.Data = mandateExecutionResp;
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "OrderController.cs - RevokeMandate() ",
                   TraceId: revokeMandateReq.trace_id,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/FetchCrossSellingDetails")]
        [ApiAuthentication]
        public ActionResult FetchCrossSellingDetails([FromHeader] string Authorization, [FromBody] FetchCrossSellingDetailsReq fetchCrossSellingDetailsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = orderBAL.FetchCrossSellingDetails(fetchCrossSellingDetailsReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderController.cs - FetchCrossSellingDetails() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/ILEmiExecution")]
        [ApiAuthentication]
        public async Task<ActionResult> ILEmiExecution([FromHeader] string Authorization, [FromBody] ILEMIValidateInstallmentReq iLEMISubsequentApiReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            OrderBAL orderBAL = new OrderBAL();
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await orderBAL.ILEmiExecution(iLEMISubsequentApiReq, "ILEmi Execution Api - OrderBAL()");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderController.cs - ILEmiExecution() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

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