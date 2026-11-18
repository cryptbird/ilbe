using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Fastenshtein;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PennyDropModel;
namespace DigitalPaymentSystem_ILTC.BAL
{
    public class PennyDropBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<DPSStatusResponse> AddPennyDropRequest(PennyDropReq pennyDropReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            PennyDropDAL pennyDropDAL = new PennyDropDAL();
            string traceId = string.Empty;
            string bank_account_holder_name = string.Empty; 
            string account_holder_name_at_bank = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                traceId = Guid.NewGuid().ToString();

                bank_account_holder_name = CleanNameSalutation(pennyDropReq.bank_account_holder_name).ToUpper();
                if (bank_account_holder_name == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PennyDropBAL.cs - AddPennyDropRequest() ",
                           TraceId: traceId,
                           Message: "Invalid Account Name. Request: " + pennyDropReq), ApplicationConfig.PENNY_DROP_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid Account Name";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                await pennyDropDAL.InsertPennyDropDetails(traceId, pennyDropReq);

                var juspayApiUrl = ApplicationConfig.JUSPAY_PENNY_DROP_API_URL + traceId + "/accounts";
                var client = new RestClient(juspayApiUrl);
                var request = new RestRequest(juspayApiUrl, Method.Post);

                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddHeader("Authorization", "Basic " + ApplicationConfig.JUSPAY_AUTH_TOKEN);

                var accountDetails = new
                {
                    bank_account = new[]
                    {
                        new
                        {
                            ifsc = pennyDropReq.bank_ifsc_code,
                            account = pennyDropReq.bank_account_number
                        }
                    }
                };

                request.AddParameter("account_details", JsonConvert.SerializeObject(accountDetails));
                request.AddParameter("customer_id", traceId);
                request.AddParameter("gateway_reference_id", ApplicationConfig.JUSPAY_GATEWAY_REFERENCE_ID);
                request.AddParameter("command", ApplicationConfig.JUSPAY_PENNY_DROP_API_COMMAND);

                stopwatch = Stopwatch.StartNew();

                var apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(accountDetails), apiResponse.Content, juspayApiUrl, "FETCH JUSPAY ACCOUNT DETAILS", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (apiResponse != null && apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                {
                    JsonElement jsonPennyDropResponse = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(apiResponse.Content);

                    if (jsonPennyDropResponse.TryGetProperty("account_details", out JsonElement accountDetailsElement) && accountDetailsElement.ValueKind == JsonValueKind.Object && accountDetailsElement.TryGetProperty("name", out JsonElement nameElement))
                    {
                        account_holder_name_at_bank = nameElement.GetString();
                    }

                    if (!string.IsNullOrEmpty(account_holder_name_at_bank))
                    {
                        account_holder_name_at_bank = CleanNameSalutation(account_holder_name_at_bank).ToUpper();
                        if (account_holder_name_at_bank == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PennyDropBAL.cs - AddPennyDropRequest() ",
                                TraceId: traceId,
                                Message: "Invalid Account Name at Bank. Request: " + pennyDropReq + " Penny Drop Response from Juspay: " + apiResponse.Content), ApplicationConfig.PENNY_DROP_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Invalid Account Name at Bank";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        var distance = Levenshtein.Distance(bank_account_holder_name, account_holder_name_at_bank);
                        int maxLength = Math.Max(pennyDropReq.bank_account_holder_name.Length, account_holder_name_at_bank.Length);
                        double fuzzy_logic_percentage_match = 1.0 - (double)distance / maxLength;

                        if (fuzzy_logic_percentage_match >= 0.75)
                        {
                            await pennyDropDAL.UpdatePennyDropDetails(traceId, 'Y', 'Y', account_holder_name_at_bank, fuzzy_logic_percentage_match);

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Account Verified Successfully";
                            response.TraceId = traceId;
                            response.Data = null;
                        }
                        else
                        {
                            await pennyDropDAL.UpdatePennyDropDetails(traceId, 'Y', 'N', account_holder_name_at_bank, fuzzy_logic_percentage_match);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Account Not Verified";
                            response.TraceId = traceId;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                          MethodName: "PennyDropBAL.cs - AddPennyDropRequest() ",
                          TraceId: traceId,
                          Message: "Could not fetch Account Name from Bank. Request: " + pennyDropReq + " Penny Drop Response from Juspay: " + apiResponse.Content), ApplicationConfig.PENNY_DROP_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Could not fetch Account Name from Bank";
                        response.TraceId = traceId;
                        response.Data = null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                         MethodName: "PennyDropBAL.cs - AddPennyDropRequest() ",
                         TraceId: traceId,
                         Message: "Could not fetch Account Details from Bank. Request: " + pennyDropReq + " Penny Drop Response from Juspay: " + apiResponse.Content), ApplicationConfig.PENNY_DROP_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Could not fetch Account Details from Bank";
                    response.TraceId = traceId;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "PennyDropBAL.cs - AddPennyDropRequest() ",
                   TraceId: traceId,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.PENNY_DROP_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public string CleanNameSalutation(string fullName)
        {
            try
            {
                if (string.IsNullOrEmpty(fullName))
                    return null;

                List<string> salutations = new List<string> { "MR", "MR.", "MRS", "MRS.", "MISS", "MISS.", "MS", "MS.", "DR", "DR.", "SHRI", "SHREE", "SHRIMATI", "SHREEMATI" };

                string name = fullName.Trim();

                string[] parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 1 && salutations.Contains(parts[0].ToUpper()))
                    name = parts[1];

                return System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "PennyDropBAL.cs - CleanNameSalutation() ",
                   Message: null,
                   StackTrace: ex.StackTrace), ApplicationConfig.PENNY_DROP_LOGS);

                return null;
            }
        }

    }
}
