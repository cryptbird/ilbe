using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Hangfire;
using Newtonsoft.Json;
using RestSharp;
using System.Net;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class ILTCBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();
        public void ScheduleAutoClaimRaised()
        {
            try
            {
                BackgroundJob.Enqueue(() => AutoClaimRaised());
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILTCBAL.cs - ScheduleAutoClaimRaised() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ILTC_LOGS);
            }
        }

        public async Task AutoClaimRaised()
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            string apiUrl = "https://api-iltc.insurancearticlez.com/api/v1/TripSecure/AutoClaimRaised";

            try
            {
                var client = new RestClient(apiUrl);
                var request = new RestRequest(apiUrl, Method.Post);

                request.AddHeader("Content-Type", "application/json");

                RestResponse response = await client.ExecuteAsync(request);

                await paymentDAL.LogIntegrationApi(
                    traceId: null,
                    proposalNumber: "",
                    request: "Auto Claim Raised Request",
                    response: response.Content + " Status Code: " + response.StatusCode,
                    apiUrl: apiUrl,
                    module: "AUTO CLAIM RAISED",
                    "", "", ""
                );

                if (response != null && response.StatusCode == HttpStatusCode.OK)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "ILTCBAL.cs - AutoClaimRaised()",
                        TraceId: null,
                        Message: "Auto Claim Raised API called successfully. Response: " + response.Content),
                        ApplicationConfig.ILTC_LOGS);
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "ILTCBAL.cs - AutoClaimRaised()",
                        TraceId: null,
                        Message: "Auto Claim Raised API call failed. Status Code: " + response?.StatusCode + " Response: " + response?.Content),
                        ApplicationConfig.ILTC_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILTCBAL.cs - AutoClaimRaised()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace),
                    ApplicationConfig.ILTC_LOGS);
            }
        }
    }
}