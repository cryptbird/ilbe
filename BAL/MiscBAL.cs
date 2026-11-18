using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Newtonsoft.Json;
using RestSharp;
using System.Diagnostics;
using System.Net;
using static DigitalPaymentSystem_ILTC.Models.BreakInModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class MiscBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();
        private static readonly HttpClient _wrapperHttpClient = new HttpClient();
        public async Task<HttpResponseMessage> GetFooterContent(string id, string flow_type)
        {
            Stopwatch stopwatch = new Stopwatch();
            ReverseSyncToken reverseSyncToken = new ReverseSyncToken();
            PaymentDAL paymentDAL = new PaymentDAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            string transactionID = string.Empty;
            string traceID = string.Empty;
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
                        string token = await reverseSyncToken.GetWebAuthorizationToken(traceID, flow_type);

                        string URL = ApplicationConfig.FOOTER_CONTENT_URL;
                        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, URL);
                        request.Headers.TryAddWithoutValidation("Authorization","Bearer "+ token);

                        stopwatch = Stopwatch.StartNew();
                        HttpResponseMessage response = await _wrapperHttpClient.SendAsync(request);
                        stopwatch.Stop();

                        string responseBody = await response.Content.ReadAsStringAsync();
                        var responseTime = stopwatch.Elapsed.TotalSeconds;
                        await paymentDAL.LogIntegrationApi(traceID, "", "", "", URL, "GET FOOTER", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                        return response;
                    }
                }
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("ID is not valid.")
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "MiscBAL.cs - GetFooterContent() ",
                   TraceId: traceID,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.MISC_LOGS);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Failed to fetch footer content.")
                };

            }

        }

    }
}
