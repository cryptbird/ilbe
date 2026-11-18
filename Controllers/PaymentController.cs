using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class PaymentController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/AddPaymentRequest")]
        [ApiAuthentication]
        public async Task<ActionResult> AddPaymentRequest([FromHeader] string Authorization, [FromBody] AddPaymentReq addPaymentReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - Request() ",
                    Message: "AddPaymentRequest Request: " + JsonConvert.SerializeObject(addPaymentReq) + "Token: " + Authorization,
                    TraceId: null), ApplicationConfig.PAYMENT_LOGS);

                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.AddPaymentRequest(authenticateTokenData.username, addPaymentReq, "REALTIME - ADD PAYMENT REQUEST");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - AddPaymentRequest() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v2/pay/AddPaymentRequest")]
        [ApiAuthentication]
        public async Task<ActionResult> AddPaymentRequestv2([FromHeader] string Authorization, [FromBody] AddPaymentReqv2 addPaymentReq, [FromHeader] string CorelationId)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - Request() ",
                    Message: "AddPaymentRequestv2 Request: " + JsonConvert.SerializeObject(addPaymentReq) + " Token: " + Authorization,
                    TraceId: null), ApplicationConfig.PAYMENT_LOGS);

                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.AddPaymentRequestv2(authenticateTokenData, addPaymentReq, "REALTIME - ADD PAYMENT REQUEST V2", CorelationId);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - AddPaymentRequestv2() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/FetchPaymentTransaction")]
        [ApiAuthentication]
        public async Task<ActionResult> FetchPaymentTransaction([FromHeader] string Authorization, [FromBody] FetchPaymentReq fetchPaymentReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.FetchPaymentTransaction(fetchPaymentReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(MethodName: "PaymentController.cs - FetchPaymentTransaction() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);

        }


        [HttpPost("api/v1/pay/TagProposal")]
        [ApiAuthentication]
        public async Task<ActionResult> TagProposal([FromHeader] string Authorization, [FromBody] TagProposalReq tagProposalReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.TagProposal(tagProposalReq, authenticateTokenData.username, "REALTIME - TAG PROPOSAL");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - TagProposal() ",
                    TraceId: tagProposalReq.TraceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        //[HttpPost("api/v1/pay/GetSendPaymentLinkData")]
        //public async Task<ActionResult> GetSendPaymentLinkData([FromBody] GetSendPaymentLinkDataReq getSendPaymentLinkDataReq)
        //{
        //    DPSStatusResponse response = new DPSStatusResponse();
        //    AuthenticateBAL authenticateBAL = new AuthenticateBAL();
        //    PaymentBAL paymentBAL = new PaymentBAL();
        //    string traceID = string.Empty;
        //    try
        //    {
        //        response = await paymentBAL.GetSendPaymentLinkData(getSendPaymentLinkDataReq);
        //    }
        //    catch (Exception ex)
        //    {
        //        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
        //            MethodName: "PaymentController.cs - getsendpaymentlinkdata() ",
        //            TraceId: traceID,
        //            Message: $"Exception occurred: {ex.Message}",
        //            StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

        //        response.StatusCode = 0;
        //        response.StatusMessage = "Failed";
        //        response.Message = null;
        //        response.TraceId = null;
        //        response.Data = null;
        //    }

        //    return Ok(response);
        //}


        [HttpPost("api/v1/pay/SendPaymentLink")]
        [ApiAuthentication]
        public async Task<ActionResult> SendPaymentLink([FromHeader] string Authorization, [FromBody] SendPaymentLinkReq sendPaymentLinkReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.SendPaymentLink(sendPaymentLinkReq, authenticateTokenData.username, "REALTIME - SEND PAYMENT LINK");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - SendPaymentLink() ",
                    TraceId: "",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v1/pay/GetPaymentLink")]
        public async Task<ActionResult> GetPaymentLink([FromBody] FetchPaymentReq fetchPaymentReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                response = await paymentBAL.GetPaymentLink(fetchPaymentReq);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - GetPaymentLink() ",
                    TraceId: "",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v2/pay/TagProposal")]
        [ApiAuthentication]
        public async Task<ActionResult> TagProposalv2([FromHeader] string Authorization, [FromBody] TagProposalReqv2 tagProposalReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.TagProposalv2(tagProposalReq, authenticateTokenData.username, "REALTIME - TAG PROPOSAL V2");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - TagProposalv2() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpPost("api/v3/pay/TagProposal")]
        [ApiAuthentication]
        public async Task<ActionResult> TagProposalv3([FromHeader] string Authorization, [FromBody] TagProposalReqv3 tagProposalReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.TagProposalv3(tagProposalReq, authenticateTokenData.username, "REALTIME - TAG PROPOSAL V3");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - TagProposalv3() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }

        [HttpPost("api/v1/pay/GetAgentDetails")]
        [ApiAuthentication]
        public async Task<ActionResult> GetAgentDetails([FromHeader] string Authorization, [FromBody] GetAgentDetailsRequest getAgentDetailsRequest)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await paymentBAL.GetAgentDetails(getAgentDetailsRequest.proposal_type, getAgentDetailsRequest.intermediaryCode, getAgentDetailsRequest.customer_id, getAgentDetailsRequest.payer_type, "REALTIME - GET AGENT DETAILS V1", "TESTING1234");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentController.cs - TagProposalv3() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

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
