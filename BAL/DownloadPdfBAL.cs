using Amazon.Runtime.Telemetry.Tracing;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using ILTakeCare.DAL;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using RestSharp;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using static DigitalPaymentSystem_ILTC.Models.DownloadPdfModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class DownloadPdfBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<Stream> DownloadPolicyCertificate(string proposalNumber, string flow_type, string id)
        {
            ILESBInterface ilesbInterface = new ILESBInterface();
            DownloadPdfDAL downloadPdfDAL = new DownloadPdfDAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            FetchPolicyDetailsFromDB fetchPolicyDetailsFromDB = new FetchPolicyDetailsFromDB();

            Stream _stream = null;

            Stopwatch stopwatch = new Stopwatch();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DPSStatusResponse response = new DPSStatusResponse();
            string transactionID = string.Empty;
            string traceID = string.Empty;
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            try
            {
                string decID = encryptDecrypt.DecryptResponse(id);
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        fetchPolicyDetailsFromDB = await downloadPdfDAL.GetPolicyDetailsByProposalNo(proposalNumber, transactionID);
                        if (fetchPolicyDetailsFromDB != null)
                        {
                            if (fetchPolicyDetailsFromDB.payment_created_on.HasValue && DateTime.Now > fetchPolicyDetailsFromDB.payment_created_on.Value.AddHours(Convert.ToInt32(ApplicationConfig.PAYMENT_URL_LIFE_HOURS)))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "DownloadPdfBAL.cs - DownloadPolicyCertificate() ",
                                   Message: "Session Expired. Fetch Policy Details From DB Response: " + JsonConvert.SerializeObject(fetchPolicyDetailsFromDB),
                                   TraceId: traceID), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                                return null;
                            }

                            if (fetchPolicyDetailsFromDB.core.ToUpper() == "PF")
                            {
                                string token = await ilesbInterface.GetILESBToken(ApplicationConfig.ILESB_DOWNLOAD_POLICY_SCOPE, fetchPolicyDetailsFromDB.trace_id, flow_type);

                                string URL = ApplicationConfig.PF_POLICY_COPY_DOWNLOAD_URL;
                                var clientURL = new RestClient(URL);
                                var request = new RestRequest(URL, Method.Post);
                                var objRequestURL = new
                                {
                                    customerId = fetchPolicyDetailsFromDB.customer_id,
                                    policyNo = fetchPolicyDetailsFromDB.policy_number,
                                    correlationId = fetchPolicyDetailsFromDB.trace_id
                                };

                                request.AddHeader("Authorization", "Bearer " + token);
                                request.AddHeader("Content-Type", "application/json");
                                request.AddParameter("application/json", JsonConvert.SerializeObject(objRequestURL), ParameterType.RequestBody);

                                stopwatch = Stopwatch.StartNew();

                                RestResponse pdfResponse = await clientURL.ExecuteAsync(request);

                                stopwatch.Stop();
                                var responseTime = stopwatch.Elapsed.TotalSeconds;

                                string responseToLog = BuildPfResponseForDb(pdfResponse);

                                await paymentDAL.LogIntegrationApi(fetchPolicyDetailsFromDB.trace_id, "", JsonConvert.SerializeObject(objRequestURL), responseToLog, URL, "ILESB Policy Download", JsonConvert.SerializeObject(pdfResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                                if (pdfResponse.StatusCode == HttpStatusCode.OK && pdfResponse.RawBytes != null)
                                {
                                    _stream = new MemoryStream(pdfResponse.RawBytes);
                                }
                            }
                            else if (fetchPolicyDetailsFromDB.core.ToUpper() == "ARTEMIS")
                            {
                                string token = await new ApiFactoryInterface().GetApiFactoryToken(fetchPolicyDetailsFromDB.trace_id, flow_type);
                                //string correlationId = Guid.NewGuid().ToString();

                                string URL = ApplicationConfig.APIFACTORY_POLICY_COPY_DOWNLOAD_URL;
                                var clientURL = new RestClient(URL);
                                var request = new RestRequest(URL, Method.Post);

                                var orionRequest = new Orion_SendCommunication
                                {
                                    ilRequestNo = fetchPolicyDetailsFromDB.trace_id,
                                    ilTemplateId = "",
                                    ilTemplateName = ApplicationConfig.APIFACTORY_POLICY_DOWNLOAD_TEMPLATE_NAME,
                                    referenceNo = fetchPolicyDetailsFromDB.policy_number,
                                    documentRequired = ApplicationConfig.APIFACTORY_DOWNLOAD_POLICY_ISDOCUMENT_REQD,
                                    communicationRequired = ApplicationConfig.APIFACTORY_DOWNLOAD_POLICY_COMMUNICATION_REQD,
                                    additionalinfo1 = "",
                                    additionalinfo2 = "",
                                    additionalinfo3 = "",
                                    additionalinfo4 = "",
                                    additionalinfo5 = ""
                                };

                                request.AddHeader("Authorization", "Bearer " + token);
                                request.AddHeader("Content-Type", "application/json");
                                request.AddParameter("application/json", JsonConvert.SerializeObject(orionRequest), ParameterType.RequestBody);

                                stopwatch = Stopwatch.StartNew();

                                RestResponse pdfResponse = await clientURL.ExecuteAsync(request);

                                stopwatch.Stop();
                                var responseTime = stopwatch.Elapsed.TotalSeconds;

                                await paymentDAL.LogIntegrationApi(fetchPolicyDetailsFromDB.trace_id, "", JsonConvert.SerializeObject(orionRequest), pdfResponse.Content, URL, "ApiFactory Policy Download", JsonConvert.SerializeObject(pdfResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                                if (pdfResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(pdfResponse.Content))
                                {
                                    Orion_SendCommunication_ResponseModel objResponse = JsonConvert.DeserializeObject<Orion_SendCommunication_ResponseModel>(pdfResponse.Content);

                                    if (objResponse != null && objResponse.statuscode == 200 && objResponse.data != null)
                                    {
                                        if (!string.IsNullOrEmpty(objResponse.data.base64ByteArray))
                                        {
                                            byte[] buffer = Convert.FromBase64String(objResponse.data.base64ByteArray);
                                            _stream = new MemoryStream(buffer);
                                            return _stream;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "DownloadPdfBAL.cs - DownloadPolicyCertificate() ",
                                    TraceId: fetchPolicyDetailsFromDB.trace_id,
                                    Message: "Invalid Core fetched from DB. DownloadPolicyCertificate Request: " + fetchPolicyDetailsFromDB.policy_number + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPolicyDetailsFromDB)), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "DownloadPdfBAL.cs - DownloadPolicyCertificate() ",
                                   Message: "Could not fetch details from DB. Proposal Number: " + proposalNumber + " Transaction ID: " + transactionID,
                                   TraceId: traceID), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "DownloadPdfBAL.cs - DownloadPolicyCertificate() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
                            TraceId: traceID), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "DownloadPdfBAL.cs - DownloadPolicyCertificate() ",
                      Message: "Invalid ID format. ID: " + id,
                      TraceId: traceID), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                  MethodName: "DownloadPdfBAL.cs - DownloadPolicyCertificate()",
                  TraceId: fetchPolicyDetailsFromDB.trace_id,
                  Message: $"Exception occurred: {ex.Message}",
                  StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);
            }
            return _stream;
        }

        public async Task<DPSStatusResponse> SendPolicyMail(SendPolicyMailReq sendPolicyMailReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            OrderBAL orderBAL = new OrderBAL();
            SendMessageService sendMessageService = new SendMessageService();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            string transactionID = string.Empty;
            string traceID = string.Empty;
            int mailsSentCount = 0;
            int totalPolicies = 0;

            try
            {
                // Decrypt ID to get transaction ID and trace ID
                string decID = encryptDecrypt.DecryptResponse(sendPolicyMailReq.id);

                if (!decID.Contains("|"))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                        TraceId: null,
                        Message: "Invalid ID format. ID: " + sendPolicyMailReq.id), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                string[] IDParts = decID.Split("|");
                transactionID = IDParts[0];
                traceID = IDParts[1];

                if (string.IsNullOrEmpty(transactionID) || string.IsNullOrEmpty(traceID))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                        TraceId: traceID,
                        Message: "Empty Transaction ID or Trace ID. Decrypted ID: " + decID), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = traceID;
                    response.Data = null;
                    return response;
                }

                // Prepare request for fetching policy details
                FetchPolicyDetailsByIDReq fetchPolicyDetailsByIDReq = new FetchPolicyDetailsByIDReq
                {
                    ID = sendPolicyMailReq.id
                };

                // Fetch policy details using OrderBAL
                DPSStatusResponse policyDetailsResponse = await orderBAL.FetchPolicyDetailsByID(fetchPolicyDetailsByIDReq, flow_type);

                if (policyDetailsResponse.StatusCode != 1 || policyDetailsResponse.Data == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                        TraceId: traceID,
                        Message: "Failed to fetch policy details. Transaction ID: " + transactionID), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = traceID;
                    response.Data = null;
                    return response;
                }

                // Parse the policy details response
                FetchPolicyDetailsByIDResp policyDetailsData = null;

                try
                {
                    // First serialize to JSON, then deserialize to the correct type
                    string jsonData = JsonConvert.SerializeObject(policyDetailsResponse.Data);
                    policyDetailsData = JsonConvert.DeserializeObject<FetchPolicyDetailsByIDResp>(jsonData);
                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                        TraceId: traceID,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = traceID;
                    response.Data = null;
                    return response;
                }

                if (policyDetailsData == null || policyDetailsData.proposalDetailsList == null || policyDetailsData.proposalDetailsList.Count == 0)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                        TraceId: traceID,
                        Message: "No policy details found. Transaction ID: " + transactionID), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = traceID;
                    response.Data = null;
                    return response;
                }

                totalPolicies = policyDetailsData.proposalDetailsList.Count;

                // Email content
                string emailSubject = "Your Policy Certificate - ICICI Lombard General Insurance Company Limited";
                string emailBody = "Dear Customer,<br><br>Thank you for choosing ICICI Lombard.<br><br>Please find attached the copy of your insurance policy. We recommend that you review the policy document carefully and keep it for your records.<br><br>In case you have any questions regarding your policy or require assistance, please feel free to reach out to us through our customer support channels mentioned on our website.<br><br>We appreciate your trust in ICICI Lombard.<br><br>Warm regards,<br>ICICI Lombard General Insurance Company Limited";

                // Loop through each policy and send mail with attachment
                foreach (var policyDetail in policyDetailsData.proposalDetailsList)
                {
                    if (!string.IsNullOrEmpty(policyDetail.policy_number))
                    {
                        try
                        {
                            // Download PDF for the policy
                            Stream pdfStream = await DownloadPolicyCertificate(policyDetail.proposal_number, flow_type, sendPolicyMailReq.id);

                            if (pdfStream != null && pdfStream.Length > 0)
                            {
                                // Convert stream to base64 for email attachment
                                byte[] pdfBytes;
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    pdfStream.Position = 0;
                                    await pdfStream.CopyToAsync(ms);
                                    pdfBytes = ms.ToArray();
                                }

                                string base64Pdf = Convert.ToBase64String(pdfBytes);
                                string attachmentName = $"Policy_{policyDetail.policy_number}.pdf";

                                // Send mail with PDF attachment
                                bool mailSent = sendMessageService.SendMail(
                                    content: emailBody,
                                    emailId: sendPolicyMailReq.email,
                                    subject: emailSubject,
                                    attachmentPath: null,
                                    attachmentName: attachmentName,
                                    isHighPriority: false,
                                    base64Attachment: base64Pdf
                                );

                                if (mailSent)
                                {
                                    mailsSentCount++;
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                                        TraceId: traceID,
                                        Message: $"Failed to send mail for Policy Number: {policyDetail.policy_number}. Email: {sendPolicyMailReq.email}"), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                                    TraceId: traceID,
                                    Message: $"PDF not found or empty for Policy Number: {policyDetail.policy_number}"), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);
                            }
                        }
                        catch (Exception ex)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                                TraceId: traceID,
                                Message: $"Error processing Policy Number: {policyDetail.policy_number}. Error: {$"Exception occurred: {ex.Message}"}",
                                StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);
                        }
                    }
                }

                // Prepare response
                if (mailsSentCount == totalPolicies)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = $"All {mailsSentCount} mails sent successfully";
                    response.TraceId = traceID;
                    response.Data = new { total_policies = totalPolicies, mails_sent = mailsSentCount };
                }
                else if (mailsSentCount > 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = $"{mailsSentCount} out of {totalPolicies} mails sent successfully";
                    response.TraceId = traceID;
                    response.Data = new { total_policies = totalPolicies, mails_sent = mailsSentCount };
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                    response.TraceId = traceID;
                    response.Data = new { total_policies = totalPolicies, mails_sent = mailsSentCount };
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DownloadPdfBAL.cs - SendPolicyMail()",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = ApplicationConfig.REQUEST_FAILED_TEXT;
                response.TraceId = traceID;
                response.Data = null;
            }

            return response;
        }

        private static string BuildPfResponseForDb(RestResponse response)
        {
            // If valid PDF bytes, store compact metadata (not raw binary)
            if (response?.RawBytes != null && response.RawBytes.Length >= 5)
            {
                bool isPdfHeaderValid =
                    response.RawBytes[0] == 0x25 && // %
                    response.RawBytes[1] == 0x50 && // P
                    response.RawBytes[2] == 0x44 && // D
                    response.RawBytes[3] == 0x46 && // F
                    response.RawBytes[4] == 0x2D;   // -

                if (isPdfHeaderValid)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        isPdf = true,
                        byteLength = response.RawBytes.Length,
                        contentType = response.ContentType,
                        sha256 = Convert.ToHexString(SHA256.HashData(response.RawBytes)).ToLowerInvariant()
                    });
                }
            }

            // Non-PDF: store exact API response body
            if (!string.IsNullOrEmpty(response?.Content))
            {
                return response.Content;
            }

            // Fallback for transport-level failures
            return response?.ErrorMessage ?? "No response content";
        }
    }
}
