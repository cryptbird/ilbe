using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Newtonsoft.Json;
using RestSharp;
using static DigitalPaymentSystem_ILTC.Models.DropLeadModels;
using static DigitalPaymentSystem_ILTC.Models.GAEventsModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class GAEventsBAL
    {
        private readonly CloudWatchLogs objLog = new CloudWatchLogs();
        
        public async Task<DPSStatusResponse> GAEvents(GAEventRequest gaEventRequest)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            GAEventsDAL gaEventsDAL = new GAEventsDAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            WebReqBody webReqBody = new WebReqBody();
            WebRespBody webRespBody = new WebRespBody();
            string decID = string.Empty;
            string transactionID = string.Empty;
            string traceID = string.Empty;
            string productValue = string.Empty;
            try
            {
                decID = encryptDecrypt.DecryptResponse(gaEventRequest.id);
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (string.IsNullOrEmpty(transactionID) || string.IsNullOrEmpty(traceID))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "GAEventsBAL.cs - GAEvents() ",
                            TraceId: traceID,
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID), ApplicationConfig.GA_EVENT_LOGS);

                        response.Data = null;
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = traceID;
                        return response;
                    }
                    webRespBody = await gaEventsDAL.GetProposalDetails(traceID, transactionID, gaEventRequest.product_value);
                    if (webRespBody == null)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "GAEventsBAL.cs - GAEvents() ",
                               Message: "No Txn details found for Transaction ID: " + transactionID,
                               TraceId: traceID), ApplicationConfig.GA_EVENT_LOGS);

                        response.Data = null;
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "No Txn details found";
                        response.TraceId = traceID;
                        return response;
                    }
                    else if (webRespBody.StatusCode == 200 && webRespBody.Success)
                    {
                        response.StatusCode = 1;
                        response.Message = "Success";
                        response.StatusMessage = "Txn details fetched successfully.";
                        response.TraceId = traceID;
                        response.Data = webRespBody;
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "GAEventsBAL.cs - GAEvents() ",
                               Message: "Failed to fetch txn details. Transaction ID: " + transactionID,
                               TraceId: traceID), ApplicationConfig.GA_EVENT_LOGS);

                        response.StatusCode = 0;
                        response.Message = "Failed";
                        response.StatusMessage = "Failed to fetch txn details.";
                        response.TraceId = traceID;
                        response.Data = webRespBody;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "GAEventsBAL.cs - GAEvents() ",
                        Message: "Invalid ID format. ID: " + gaEventRequest.id,
                        TraceId: traceID), ApplicationConfig.GA_EVENT_LOGS);

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
                    MethodName: "GAEventsBAL.cs - GAEvents()",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.GA_EVENT_LOGS);
            }
            return response;
        }
    }
}
