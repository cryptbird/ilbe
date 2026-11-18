using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using DigitalPaymentSystem_ILTC.Constants;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    [Route("dps-agorax/")]
    public class ILTCController : ControllerBase
    {
        #region Config Values (Fill Later)

        string WIZ_TOKEN_URL = ApplicationConfig.ILTC_WIZ_TOKEN_URL;
        string WIZ_CLIENT_KEY = ApplicationConfig.ILTC_WIZ_CLIENT_KEY;
        string WIZ_CLIENT_ID = ApplicationConfig.ILTC_WIZ_CLIENT_ID;
        string WIZ_EXECUTE_BASE_URL = ApplicationConfig.ILTC_WIZ_EXECUTE_BASE_URL;
        string WIZ_TASK_ID = ApplicationConfig.ILTC_WIZ_TASK_ID;

        #endregion

        #region Request Model

        public class TriggerWizRequest
        {
            public string policyno { get; set; }
            public string SRTName { get; set; }
            public string SubSRTName { get; set; }
            public string Source_System { get; set; }
        }

        #endregion

        [HttpPost("api/Policy/GetReqDocforPolicyCancellation")]
        public async Task<IActionResult> ILTCWizRequest([FromBody] TriggerWizRequest request)
        {
            string correlationId = Guid.NewGuid().ToString();

            try
            {
                #region Request Validation

                if (request == null || string.IsNullOrWhiteSpace(request.policyno))
                {
                    return BadRequest(new
                    {
                        Success = "Failed",
                        Correlation_id = correlationId,
                        Message = "Invalid request"
                    });
                }

                #endregion

                using HttpClient client = new HttpClient();

                #region Step 1 - Get Token

                var tokenRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    WIZ_TOKEN_URL);

                tokenRequest.Headers.Add(
                    "X_WIZ_CLIENT_KEY",
                    WIZ_CLIENT_KEY);

                var tokenBody = new
                {
                    client_id = WIZ_CLIENT_ID
                };

                tokenRequest.Content = new StringContent(
                    JsonConvert.SerializeObject(tokenBody),
                    Encoding.UTF8,
                    "application/json");

                var tokenResponse = await client.SendAsync(tokenRequest);

                var tokenResponseContent =
                    await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Token API Failed : {tokenResponseContent}");
                }

                dynamic tokenResult =
                    JsonConvert.DeserializeObject(tokenResponseContent);

                string token =
                    tokenResult?.token ??
                    tokenResult?.access_token ??
                    tokenResult?.auth_token;

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new Exception("Token not found in response.");
                }

                #endregion

                #region Step 2 - Execute WIZ API

                string executeUrl =
                    $"{WIZ_EXECUTE_BASE_URL}{WIZ_TASK_ID}";

                var executeRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    executeUrl);

                executeRequest.Headers.Add(
                    "XWIZAUTHTOKEN",
                    token);

                var executeBody = new
                {
                    policyno = request.policyno,
                    SRTName = request.SRTName,
                    SubSRTName = request.SubSRTName,
                    Source_System = request.Source_System
                };

                executeRequest.Content = new StringContent(
                    JsonConvert.SerializeObject(executeBody),
                    Encoding.UTF8,
                    "application/json");

                var executeResponse =
                    await client.SendAsync(executeRequest);

                var executeResponseContent =
                    await executeResponse.Content.ReadAsStringAsync();

                if (!executeResponse.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Execute API Failed : {executeResponseContent}");
                }

                #endregion

                #region Step 3 - Extract Documents

                string rawDocuments = string.Empty;

                var parsedResponse =
                    JsonConvert.DeserializeObject
                    <Dictionary<string, object>>
                    (executeResponseContent);

                if (parsedResponse != null &&
                    parsedResponse.ContainsKey("Documents Required") &&
                    parsedResponse["Documents Required"] != null)
                {
                    rawDocuments =
                        parsedResponse["Documents Required"].ToString();
                }

                #endregion

                #region Step 4 - Format Documents

                List<string> cleanDocumentsList =
                    new List<string>();

                if (!string.IsNullOrWhiteSpace(rawDocuments))
                {
                    string[] rawChunks =
                        rawDocuments.Split(
                            new[] { '|' },
                            StringSplitOptions.RemoveEmptyEntries);

                    foreach (string chunk in rawChunks)
                    {
                        string trimmedChunk = chunk.Trim();

                        if (trimmedChunk.Contains(
                            "Cancelled Cheque",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            cleanDocumentsList.Add(
                                "Cancelled Cheque");
                        }

                        if (trimmedChunk.Contains(
                            "Valid ID",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            cleanDocumentsList.Add(
                                "Valid ID (Pan Card/Driving License/Passport/Masked Aadhar)");
                        }

                        if (trimmedChunk.Contains(
                            "Alternate Policy Copy",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            cleanDocumentsList.Add(
                                "Alternate Policy Copy");
                        }
                    }

                    cleanDocumentsList =
                        cleanDocumentsList
                        .Distinct()
                        .ToList();
                }

                #endregion

                #region Success Response

                return Ok(new
                {
                    Success = "Success",
                    Correlation_id = correlationId,
                    Icon1 = "https://api-iltc.insurancearticlez.com/iltc-assets/images/Service_Request/sr_tracking_file_upload.png",
                    Icon2 = "https://api-iltc.insurancearticlez.com/iltc-assets/images/Service_Request/sr_tracking_upload_documents.png",
                    Text = "To proceed with your policy cancellation verification, please upload the following documents",
                    InfoText = "You can upload documents in PDF or JPG format. The maximum file size is 10 MB per file.",
                    Documents_Required = cleanDocumentsList
                });

                #endregion
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = "Failed",
                    Correlation_id = correlationId,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }
    }
}