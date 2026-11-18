using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using RestSharp;
using System.Diagnostics;
using static DigitalPaymentSystem_ILTC.Models.DropLeadModels;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.ReverseSyncTokenModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class DropLeadBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();
        private static int _rmTextCounter = -1;

        public async Task<DPSStatusResponse> SmeDropLeadDetails(DropLeadRequest dropLeadrequest, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            SMEDropLeadRequest SMEreqBody = new SMEDropLeadRequest();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            SMEDropLeadResponse smeDropLeadResponse = new SMEDropLeadResponse();
            DropLeadDAL dropLeadDAL = new DropLeadDAL();
            ReverseSyncToken reverseSyncToken = new ReverseSyncToken();
            string token = string.Empty;
            string traceID = string.Empty;
            string transactionID = string.Empty;
            string decID = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                decID = encryptDecrypt.DecryptResponse(dropLeadrequest.id);
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (string.IsNullOrEmpty(transactionID) || string.IsNullOrEmpty(traceID))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "DropLeadBAL.cs - SmeDropLeadDetails() ",
                            TraceId: traceID,
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID), ApplicationConfig.DROP_LEAD_LOGS);

                        response.Data = null;
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = traceID;
                        return response;
                    }

                    LeadDetailsResp LeadDetails = await dropLeadDAL.GetLeadDetails(transactionID, traceID);
                    if(LeadDetails == null)
                    {

                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "DropLeadBAL.cs - SmeDropLeadDetails() ",
                                Message: "No lead details found for Transaction ID: " + transactionID,
                                TraceId: traceID), ApplicationConfig.DROP_LEAD_LOGS);
                        response.Data = null;
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "No lead details found";
                        response.TraceId = traceID;
                        return response;
                    }
                    else
                    {
                        SMEreqBody.MobileNumber = LeadDetails.mobile_number;
                        if (LeadDetails.product == "SME_1")
                        {
                            SMEreqBody.ProductCode = ApplicationConfig.SME_1_CALLBACK_PRODUCT_NAME;
                        }
                    }

                     token = await reverseSyncToken.GetSMEAuthorizationToken(traceID, flow_type);


                    WebTokenEncryptedReq reqModel = ReverseSyncToken.TokenEncryptAndConcat(JsonConvert.SerializeObject(SMEreqBody));

                    if (reqModel != null)
                    {
                        WebEncrypted encrypted = new WebEncrypted();

                        if (string.IsNullOrEmpty(reqModel.payload))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                              MethodName: "DropLeadBAL.cs - SmeDropLeadDetails() ",
                              Message: "Exception reqModel.payload is Empty",
                              TraceId: traceID), ApplicationConfig.DROP_LEAD_LOGS);
                        }
                        else
                        {
                            encrypted.msg = reqModel.payload;

                            try
                            {
                                string URL = ApplicationConfig.DROP_LEAD_SME_URL;
                                var client = new RestClient();
                                var request = new RestRequest(URL, Method.Post);
                                request.AddHeader("version", reqModel.version);
                                request.AddHeader("kv", reqModel.kv);
                                request.AddHeader("Authorization", "Bearer " + token);
                                request.AddHeader("Content-Type", "application/json");
                                var body = JsonConvert.SerializeObject(encrypted);
                                request.AddStringBody(body, DataFormat.Json);

                                stopwatch = Stopwatch.StartNew();

                                RestResponse Restresponse = await client.ExecuteAsync(request);

                                stopwatch.Stop();
                                var responseTime = stopwatch.Elapsed.TotalSeconds;

                                await paymentDAL.LogIntegrationApi(traceID, "", JsonConvert.SerializeObject(SMEreqBody), Restresponse.Content, URL, "DROP LEAD SME API", JsonConvert.SerializeObject(Restresponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                                if (Restresponse.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(Restresponse.Content))
                                {
                                    smeDropLeadResponse = JsonConvert.DeserializeObject<SMEDropLeadResponse>(Restresponse.Content);

                                    if (smeDropLeadResponse.StatusCode == 200 && smeDropLeadResponse.Success)
                                    {
                                        response.StatusCode = 1;
                                        response.Message = "Success";
                                        response.StatusMessage = "Lead dropped successfully.";
                                        response.Data = smeDropLeadResponse;
                                    }
                                    else
                                    {
                                        response.StatusCode = 0;
                                        response.Message = "Failed";
                                        response.StatusMessage = "Lead drop failed.";
                                        response.Data = smeDropLeadResponse;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                stopwatch.Stop();

                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "DropLeadBAL.cs - SmeDropLeadDetails()",
                                    TraceId: traceID,
                                    Message: $"Exception occurred: {ex.Message}",
                                    StackTrace: ex.StackTrace), ApplicationConfig.DROP_LEAD_LOGS);
                            }
                        }
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "DropLeadBAL.cs - SmeDropLeadDetails() ",
                               Message: "Invalid ID format. ID: " + dropLeadrequest.id,
                               TraceId: traceID), ApplicationConfig.DROP_LEAD_LOGS);

                    response.Data = null;
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = traceID;
                }

                

            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DropLeadBAL.cs - SmeDropLeadDetails()",
                        TraceId: traceID,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.DROP_LEAD_LOGS);
            }
            return response;
        }

        public async Task<string> RMDetails(string trace_id, string product_name)
        {
            DropLeadDAL dropLeadDAL = new DropLeadDAL();
            string rm_details = string.Empty;
            string response = string.Empty;
            try
            {
                if (product_name == "SME_1")
                {
                    rm_details = ApplicationConfig.SME_1_RM_TEXT;
                }

                if (string.IsNullOrWhiteSpace(rm_details))
                {
                    return string.Empty;
                }
                var messages = rm_details
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (messages.Count == 0)
                {
                    return string.Empty;
                }

                int next = Interlocked.Increment(ref _rmTextCounter);
                int index = next % messages.Count;
                if (index < 0)
                {
                    index += messages.Count; // handle potential overflow
                }

                response = messages[index];
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DropLeadBAL.cs - RMDetails()",
                        TraceId: trace_id,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.DROP_LEAD_LOGS);
                return string.Empty;
            }
            return response;
        }
    }
}
