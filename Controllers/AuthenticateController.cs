using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    [Route("dps-agorax/")]
    public class AuthenticateController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/CreateToken")]
        public async Task<ActionResult> CreateAuthToken([FromHeader] string Username, [FromHeader] string Password)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            AuthenticateDAL authenticateDAL = new AuthenticateDAL();
            string completeHeader = GetCompleteRequestHeaders();
            try
            {
                bool isValid = await authenticateDAL.IsValidAuth(Username, Password, completeHeader);
                if (isValid)
                {
                    response = authenticateBAL.CreateAuthToken(Username, Password);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateController.cs - CreateAuthToken() ", 
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        [HttpGet("api/v2/pay/CreateToken")]
        public async Task<ActionResult> CreateAuthTokenv2([FromHeader] string Username, [FromHeader] string Password)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            AuthenticateDAL authenticateDAL = new AuthenticateDAL();
            string completeHeader = GetCompleteRequestHeaders();

            try
            {
                bool isValid = await authenticateDAL.IsValidAuth(Username, Password, completeHeader);
                if (isValid)
                {
                    response = await authenticateBAL.CreateAuthTokenv2(Username, Password);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateController.cs - CreateAuthTokenv2() ",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }

        [HttpPost("api/v1/pay/RefreshToken")]
        [ApiAuthentication]
        public async Task<ActionResult> RefreshToken([FromHeader] string RefreshAuthorization, [FromBody] RefreshTokenReq request)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            try
            {
                response = await authenticateBAL.RefreshToken(RefreshAuthorization, request.id);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateController.cs - RefreshToken() ",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }


        private string GetCompleteRequestHeaders()
        {
            string clientIp =
                Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor)
                    ? forwardedFor.ToString().Split(',')[0].Trim()
                    : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            List<string> lines = new List<string>
    {
        $":method: {Request.Method}",
        $":scheme: {Request.Scheme}",
        $":path: {Request.Path}{Request.QueryString}",
        $":authority: {Request.Host.Value}",
        $"HTTP-Version: {Request.Protocol}",
        $"Client-IP: {clientIp}"
    };

            foreach (var header in Request.Headers)
            {
                lines.Add($"{header.Key}: {header.Value}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
