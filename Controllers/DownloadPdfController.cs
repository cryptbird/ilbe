using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.DownloadPdfModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    //[EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class DownloadPdfController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/DownloadPolicyCertificate")]
        //[ApiAuthentication]
        public async Task<ActionResult> DownloadPolicyCertificate([FromHeader] string AuthToken, [FromHeader] string Authorization, [FromBody] FetchPolicyDetails request)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DownloadPdfBAL downloadPdfBAL = new DownloadPdfBAL();

            try
            {
                //AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                //if (!string.IsNullOrEmpty(authenticateTokenData.username))
                //{
                var pdfStream = await downloadPdfBAL.DownloadPolicyCertificate(request.proposalNumber, "REALTIME - DOWNLOAD POLICY CERTIFICATE", request.id);

                if (pdfStream != null)
                {
                    byte[] pdfBytes;

                    // ensure we are at beginning
                    if (pdfStream.CanSeek)
                    {
                        pdfStream.Position = 0;
                    }

                    using (var ms = new MemoryStream())
                    {
                        await pdfStream.CopyToAsync(ms);
                        pdfBytes = ms.ToArray();
                    }

                    var base64Pdf = Convert.ToBase64String(pdfBytes);

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = null;
                    response.TraceId = null;
                    response.Data = base64Pdf;
                    //}
                    //else
                    //{
                    //    return BadRequest("Invalid User Credentials");
                    //}
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                  MethodName: "DownloadPdfController.cs - DownloadPolicyCertificate()",
                  TraceId: null,
                  Message: $"Exception occurred: {ex.Message}",
                  StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }

        [HttpPost("api/v1/pay/SendPolicyMail")]
        [ApiAuthentication]
        public async Task<ActionResult> SendPolicyMail([FromHeader] string Authorization, [FromBody] SendPolicyMailReq sendPolicyMailReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DownloadPdfBAL downloadPdfBAL = new DownloadPdfBAL();

            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                if (!string.IsNullOrEmpty(authenticateTokenData.username))
                {
                    response = await downloadPdfBAL.SendPolicyMail(sendPolicyMailReq, "REALTIME - SEND POLICY MAIL");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                  MethodName: "DownloadPdfController.cs - SendPolicyMail()",
                  TraceId: null,
                  Message: $"Exception occurred: {ex.Message}",
                  StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                response.TraceId = null;
                response.Data = null;
            }
            return Ok(response);
        }


        [HttpGet("api/v2/pay/DownloadPolicyCertificate")]
        public async Task<ActionResult> DownloadPolicyCertificatev2([FromQuery] FetchPolicyDetails request)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DownloadPdfBAL downloadPdfBAL = new DownloadPdfBAL();

            try
            {
                var pdfStream = await downloadPdfBAL.DownloadPolicyCertificate(request.proposalNumber, "REALTIME - DOWNLOAD POLICY CERTIFICATE", request.id);

                if (pdfStream != null)
                {
                    byte[] pdfBytes;

                    if (pdfStream.CanSeek)
                    {
                        pdfStream.Position = 0;
                    }

                    using (var ms = new MemoryStream())
                    {
                        await pdfStream.CopyToAsync(ms);
                        pdfBytes = ms.ToArray();
                    }

                    // ✅ Returns PDF inline (opens in browser, not downloaded)
                    return File(pdfBytes, "application/pdf");
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                  MethodName: "DownloadPdfController.cs - DownloadPolicyCertificatev2()",
                  TraceId: null,
                  Message: $"Exception occurred: {ex.Message}",
                  StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

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
