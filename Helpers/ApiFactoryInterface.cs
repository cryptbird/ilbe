using Amazon.Runtime.Telemetry.Tracing;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Models;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using RestSharp;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.EncryptDecryptModel;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class ApiFactoryInterface
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<string> GetApiFactoryToken(string traceId, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            Stopwatch stopwatch = new Stopwatch();

            HttpClient httpClient = new HttpClient();
            string grant_type = string.Empty;
            string scope = string.Empty;
            string clientId = string.Empty;
            string clientSecret = string.Empty;

            try
            {
                grant_type = ApplicationConfig.APIFACTORY_GRANT_TYPE;
                scope = ApplicationConfig.APIFACTORY_SCOPE;
                clientId = ApplicationConfig.APIFACTORY_CLIENT_ID;
                clientSecret = ApplicationConfig.APIFACTORY_CLIENT_SECRET;

                Uri tokenEndpoint = new Uri(ApplicationConfig.APIFACTORY_TOKEN_URL);

                // Prepare request parameters
                var requestData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", grant_type),
                    new KeyValuePair<string, string>("scope", scope)
                });

                // Encode client credentials for Basic Auth header
                var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));

                // Add Basic Auth header
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);

                stopwatch = Stopwatch.StartNew();

                // Make request to obtain access token synchronously
                var response = await httpClient.PostAsync(tokenEndpoint, requestData);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                var requestBody = await requestData.ReadAsStringAsync();
                var responseBody = await response.Content.ReadAsStringAsync();

                await paymentDAL.LogIntegrationApi(traceId, "", requestBody, responseBody, ApplicationConfig.APIFACTORY_TOKEN_URL, "APIFACTORY TOKEN", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                // Check if request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Extract access token from response
                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic jsonResponse = JsonConvert.DeserializeObject<ApiFactoryAccessToken>(responseContent)!;
                    string accessToken = jsonResponse.access_token;
                    return accessToken;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ApiFactoryInterface.cs - GetApiFactoryToken() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);
            }

            return null;
        }

        public async Task<RestResponse> GenerateArtemisPolicyData(string traceId, ArtemisPolicyCreationReq requestBody, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string apiUrl = ApplicationConfig.APIFACTORY_PAYMENT_ENTRY_TAGGING_URL;
                var client = new RestClient(apiUrl);
                var request = new RestRequest(apiUrl, Method.Post);

                string token = await GetApiFactoryToken(traceId, flow_type);

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddParameter("application/json", JsonConvert.SerializeObject(requestBody), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(requestBody), apiResponse.Content, apiUrl, "APIFACTORY PAYMENT ENTRY AND TAGGING", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                return apiResponse;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ApiFactoryInterface.cs - GenerateArtemisPolicyData() ",
                   TraceId: traceId,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);
            }

            return null;
        }

        public async Task<ArtemisPolicyTaggingOnlyResp> GenerateArtemisTaggingOnlyData(string traceId, ArtemisPolicyTaggingOnlyReq requestBody, string flow_type)
        {
            ArtemisPolicyTaggingOnlyResp artemisPolicyTaggingOnlyResp = new ArtemisPolicyTaggingOnlyResp();
            PaymentDAL paymentDAL = new PaymentDAL();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string apiUrl = ApplicationConfig.APIFACTORY_PAYMENT_TAGGING_URL;
                var client = new RestClient(apiUrl);
                var request = new RestRequest(apiUrl, Method.Post);

                string token = await apiFactoryInterface.GetApiFactoryToken(traceId, flow_type);

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddParameter("application/json", JsonConvert.SerializeObject(requestBody), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(requestBody), apiResponse.Content, apiUrl, "APIFACTORY PAYMENT TAGGING", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                {
                    artemisPolicyTaggingOnlyResp = JsonConvert.DeserializeObject<ArtemisPolicyTaggingOnlyResp>(apiResponse.Content);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ApiFactoryInterface.cs - GenerateArtemisTaggingOnlyData() ",
                   TraceId: traceId,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);
            }

            return artemisPolicyTaggingOnlyResp;
        }

        public async Task<string> ExecuteRequestEncypted(string url, string token, string publicKey, object req, string aesKey)
        {
            try
            {


                var client = new RestClient();
                string requestbody = EncryptUsingAesKeyOrion(JsonConvert.SerializeObject(req), aesKey);
                OrionPolicyDetailsRequestEncrypted orionPolicyDetailsRequestEncrypted = new OrionPolicyDetailsRequestEncrypted();
                orionPolicyDetailsRequestEncrypted.data = requestbody;

                requestbody = JsonConvert.SerializeObject(orionPolicyDetailsRequestEncrypted);

                //string url = iconfig.GetSection("ABHA_PROFILE").Value;
                var request = new RestRequest(url, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddHeader("x-crypto-key", EncryptUsingRSA_Oaep256(aesKey, publicKey));
                //var reqBody = JsonConvert.SerializeObject(orionGetPolicyDetailsRequest);
                request.AddParameter("application/json", requestbody, ParameterType.RequestBody);

                //SmeRenewalApplicationLog objLog = new SmeRenewalApplicationLog();
                //objLog.WriteLogToAWSOrion("OrionHttpUtility.cs->() Request:" + url + " " + JsonConvert.SerializeObject(request));
                RestResponse restResponse = await client.ExecuteAsync(request);
                var encryptedContent = JsonConvert.DeserializeObject<OrionPolicyDetailsRequestEncrypted>(restResponse.Content);
                var decryptedContent = DecryptUsingAesKey(encryptedContent.data, aesKey);
                // objLog.WriteLogToAWSOrion("OrionHttpUtility.cs->() decryptedContent:" + url + " " + decryptedContent);

                return decryptedContent;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ApiFactoryInterface.cs - ExecuteRequestEncypted() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);

                return null;
            }

        }

        public string EncryptUsingAesKeyOrion(string requestBody, string aesKey)
        {
            try
            {
                using Aes aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(aesKey);
                aes.IV = Encoding.UTF8.GetBytes(aesKey.Substring(0, 16)); // IV from first 16 bytes of AES key
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                ICryptoTransform transform = aes.CreateEncryptor(aes.Key, aes.IV);
                using MemoryStream memoryStream = new MemoryStream();
                using CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
                using (StreamWriter writer = new StreamWriter(cryptoStream))
                {
                    writer.Write(requestBody);
                }
                return Convert.ToBase64String(memoryStream.ToArray()); // Return Base64 encoded cipher text
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ApiFactoryInterface.cs - EncryptUsingAesKeyOrion() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);
            }

            return null;
        }

        public static string EncryptUsingRSA_Oaep256(string aesKey, string publicKey)
        {
            try
            {
                byte[] aesKeyBytes = Encoding.UTF8.GetBytes(aesKey);
                RSAParameters? rsaParams = ConvertFromX509PublicKey(publicKey);

                if (rsaParams == null)
                {
                    return null;
                }

                using RSA rsa = RSA.Create(2048);
                rsa.ImportParameters(rsaParams.Value);

                byte[] encryptedKey = rsa.Encrypt(aesKeyBytes, RSAEncryptionPadding.OaepSHA256);
                return Convert.ToBase64String(encryptedKey); // Return Base64 encoded RSA-encrypted AES key
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ApiFactoryInterface.cs - EncryptUsingRSA_Oaep256() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);

                return null;
            }
        }

        private static RSAParameters? ConvertFromX509PublicKey(string publicKeyString)
        {
            try
            {
                publicKeyString = publicKeyString.Replace("-----BEGIN PUBLIC KEY-----", "").Replace("-----END PUBLIC KEY-----", "");
                byte[] array = Convert.FromBase64String(publicKeyString);
                RSA rSA = RSA.Create(2048);
                rSA.ImportSubjectPublicKeyInfo(array, out var _);
                return rSA.ExportParameters(includePrivateParameters: false);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ApiFactoryInterface.cs - ConvertFromX509PublicKey() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);

                return null;
            }
        }

        public static string DecryptUsingAesKey(string cipherText, string aesKey)
        {
            try
            {
                if (!string.IsNullOrEmpty(cipherText))
                {
                    byte[] cipherBytes = Convert.FromBase64String(cipherText.Replace(' ', '+'));

                    using Aes aes = Aes.Create();
                    aes.Key = Encoding.UTF8.GetBytes(aesKey);
                    aes.IV = Encoding.UTF8.GetBytes(aesKey.Substring(0, 16)); // IV from first 16 bytes of AES key
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    using MemoryStream memoryStream = new MemoryStream(cipherBytes);
                    using CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    using StreamReader reader = new StreamReader(cryptoStream);

                    return reader.ReadToEnd(); // Return the decrypted data as string
                }
                return null;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ApiFactoryInterface.cs - DecryptUsingAesKey() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);

                return null;
            }
        }

        public async Task<FetchArtemisProposalDetails> FetchArtemisProposalDetails(string proposal_number, string trace_id, string product, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            JsonElement fetchArtemisIsNSTPFlagResp = new JsonElement();

            string apiUrl = string.Empty;
            string response = string.Empty;

            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string token = await GetApiFactoryToken(trace_id, flow_type);
                var aesKey = Guid.NewGuid().ToString("N");

                if (product.Contains("HEALTH"))
                {
                    apiUrl = ApplicationConfig.ARTEMIS_HEALTH_PROPOSAL_DETAILS_URL;

                    var request = new
                    {
                        proposalNumber = proposal_number,
                    };

                    stopwatch = Stopwatch.StartNew();

                    response = await ExecuteRequestEncypted(apiUrl, token, ApplicationConfig.ARTEMIS_HEALTH_PUBLIC_KEY, request, aesKey);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(trace_id, proposal_number, JsonConvert.SerializeObject(request), response, apiUrl, "APIFACTORY FETCH HEALTH PROPOSAL DETAILS", "", flow_type, JsonConvert.SerializeObject(responseTime));

                    if (!string.IsNullOrEmpty(response))
                    {
                        fetchArtemisIsNSTPFlagResp = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);
                        if (fetchArtemisIsNSTPFlagResp.TryGetProperty("response", out JsonElement responseElement))
                        {
                            if (responseElement.ValueKind != JsonValueKind.Null)
                            {
                                if (responseElement.TryGetProperty("isNSTPFlag", out JsonElement isNSTPFlagElement))
                                {
                                    fetchArtemisProposalDetails.is_nstp_flow = isNSTPFlagElement.GetString();
                                }

                                if (responseElement.TryGetProperty("policyNumber", out JsonElement policyNumberElement))
                                {
                                    fetchArtemisProposalDetails.policy_status = policyNumberElement.GetString();
                                }

                                if (responseElement.TryGetProperty("policyStatus", out JsonElement policyStatusElement))
                                {
                                    fetchArtemisProposalDetails.policy_status = policyStatusElement.GetString();
                                }

                                if (responseElement.TryGetProperty("policySubStatus", out JsonElement policySubStatusElement))
                                {
                                    fetchArtemisProposalDetails.policy_sub_status = policySubStatusElement.GetString();
                                }

                                if (responseElement.TryGetProperty("intermediary", out JsonElement intermediaryElement))
                                {
                                    if (intermediaryElement.TryGetProperty("primaryVerticalCode", out JsonElement primaryVerticalCodeElement))
                                    {
                                        fetchArtemisProposalDetails.business_vertical = primaryVerticalCodeElement.GetString();
                                    }

                                    if (intermediaryElement.TryGetProperty("secondaryVerticalCode", out JsonElement secondaryVerticalCodeElement))
                                    {
                                        fetchArtemisProposalDetails.sub_vertical = secondaryVerticalCodeElement.GetString();
                                    }

                                    if (intermediaryElement.TryGetProperty("salesManagerCode", out JsonElement salesManagerCodeElement))
                                    {
                                        fetchArtemisProposalDetails.marketing_officer = salesManagerCodeElement.GetString();
                                    }

                                    if (intermediaryElement.TryGetProperty("intermediaryCode", out JsonElement intermediaryCodeElement))
                                    {
                                        fetchArtemisProposalDetails.intermediary_code = intermediaryCodeElement.GetString();
                                    }
                                }

                                if (responseElement.TryGetProperty("proposerDetails", out JsonElement proposerDetailsElement))
                                {
                                    if (proposerDetailsElement.TryGetProperty("proposerCode", out JsonElement proposerCodeElement))
                                    {
                                        fetchArtemisProposalDetails.proposer_code = proposerCodeElement.GetString();
                                    }
                                }
                            }
                        }
                    }
                }
                else if (product.Contains("SME"))
                {
                    apiUrl = ApplicationConfig.ARTEMIS_COMMERCIAL_PROPOSAL_DETAILS_URL;

                    var request = new
                    {
                        proposal_number = proposal_number,
                    };

                    stopwatch = Stopwatch.StartNew();

                    response = await ExecuteRequestEncypted(apiUrl, token, ApplicationConfig.ARTEMIS_HEALTH_PUBLIC_KEY, request, aesKey);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(trace_id, proposal_number, JsonConvert.SerializeObject(request), response, apiUrl, "APIFACTORY FETCH COMMERCIAL PROPOSAL DETAILS", "", flow_type, JsonConvert.SerializeObject(responseTime));

                    if (!string.IsNullOrEmpty(response))
                    {
                        fetchArtemisIsNSTPFlagResp = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);
                        if (fetchArtemisIsNSTPFlagResp.TryGetProperty("data", out JsonElement responseElement))
                        {
                            if (responseElement.ValueKind != JsonValueKind.Null)
                            {
                                if (responseElement.TryGetProperty("nstp_flag", out JsonElement isNSTPFlagElement))
                                {
                                    fetchArtemisProposalDetails.is_nstp_flow = isNSTPFlagElement.GetString();
                                }

                                if (responseElement.TryGetProperty("policy_number", out JsonElement policyNumberElement))
                                {
                                    fetchArtemisProposalDetails.policy_status = policyNumberElement.GetString();
                                }

                                if (responseElement.TryGetProperty("status", out JsonElement policyStatusElement))
                                {
                                    fetchArtemisProposalDetails.policy_status = policyStatusElement.GetString();
                                }

                                if (responseElement.TryGetProperty("sub_status", out JsonElement policySubStatusElement))
                                {
                                    fetchArtemisProposalDetails.policy_sub_status = policySubStatusElement.GetString();
                                }

                                if (responseElement.TryGetProperty("intermediary_details", out JsonElement intermediaryElement))
                                {
                                    JsonElement selectedIntermediary = default;

                                    if (intermediaryElement.ValueKind == JsonValueKind.Object)
                                    {
                                        selectedIntermediary = intermediaryElement;
                                    }
                                    else if (intermediaryElement.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (JsonElement item in intermediaryElement.EnumerateArray())
                                        {
                                            //if (item.ValueKind == JsonValueKind.Object &&
                                            //    item.TryGetProperty("primary_intermediary", out JsonElement p) &&
                                            //    string.Equals(p.GetString(), "Y", StringComparison.OrdinalIgnoreCase))
                                            //{
                                            //    selectedIntermediary = item;
                                            //    break;
                                            //}

                                            if (selectedIntermediary.ValueKind == JsonValueKind.Undefined && item.ValueKind == JsonValueKind.Object)
                                            {
                                                selectedIntermediary = item; // fallback: first object
                                            }
                                        }
                                    }

                                    if (selectedIntermediary.ValueKind == JsonValueKind.Object)
                                    {
                                        if (selectedIntermediary.TryGetProperty("primary_vertical_Code", out JsonElement pv))
                                            fetchArtemisProposalDetails.business_vertical = pv.GetString();

                                        if (selectedIntermediary.TryGetProperty("secondary_vertical_Code", out JsonElement sv)) 
                                            fetchArtemisProposalDetails.sub_vertical = sv.GetString();

                                        if (selectedIntermediary.TryGetProperty("primary_sales_manager_id", out JsonElement sm))
                                            fetchArtemisProposalDetails.marketing_officer = sm.GetString();

                                        if (selectedIntermediary.TryGetProperty("intermediary_code", out JsonElement ic))
                                            fetchArtemisProposalDetails.intermediary_code = ic.GetString();
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ApiFactoryInterface.cs - FetchArtemisProposalDetails() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);
            }
            return fetchArtemisProposalDetails;
        }

        public async Task<UpdatePIDResp> ArtemisUpdatePaymentID(UpdatePIDReq UpdatePIDreq, string flow_type)
        {
            ApiFactoryInterface apiFactory = new ApiFactoryInterface();
            PaymentDAL paymentDAL = new PaymentDAL();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string url = ApplicationConfig.UPDATEASBAAUTHCODEPID_API_URL;
                string token = await apiFactory.GetApiFactoryToken(UpdatePIDreq.trace_id, flow_type);
                var orionRequest = new UpdatePIDReq
                {
                    receiptNumber = UpdatePIDreq.receiptNumber,
                    mandateNumber = UpdatePIDreq.mandateNumber,
                    instrumentAmount = UpdatePIDreq.instrumentAmount,
                    instrumentDate = UpdatePIDreq.instrumentDate,
                    instrumentNumber = UpdatePIDreq.instrumentNumber
                };
                var clientURL = new RestClient(url);
                var request = new RestRequest(url, Method.Post);

                request.AddHeader("Authorization", "Bearer " + token);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(orionRequest), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await clientURL.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(UpdatePIDreq.trace_id, UpdatePIDreq.proposal_number, JsonConvert.SerializeObject(orionRequest), response.Content, url, "ASBA UPDATE PID", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));
                
                if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    UpdatePIDResp objResponse = JsonConvert.DeserializeObject<UpdatePIDResp>(response.Content);
                    if (objResponse != null && objResponse.StatusCode == 200 && objResponse.Data != null)
                    {
                        return objResponse;

                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AsbaWebhookBAL.cs - UpdatePaymentID() ",
                    TraceId: UpdatePIDreq.trace_id,
                    Message: "Proposal No:- " + UpdatePIDreq.proposal_number + " Exception:- " + $"Exception occurred: {ex.Message}"), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);

                return null;
            }
            return null;
        }

        public async Task<ArtemisEMISummaryDetailsResp> GetArtemisEMISummaryDetails(string traceId, ArtemisEmiDetailsReqModel requestBody, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string apiUrl = ApplicationConfig.ARTEMIS_EMI_SUMMARY_DETAILS_URL;
                string token = await GetApiFactoryToken(traceId, flow_type);

                var client = new RestClient(apiUrl);
                var request = new RestRequest(apiUrl, Method.Post);

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddParameter("application/json", JsonConvert.SerializeObject(requestBody), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId,requestBody.proposalNumber,JsonConvert.SerializeObject(requestBody),apiResponse.Content,apiUrl,"ARTEMIS EMI SUMMARY DETAILS",JsonConvert.SerializeObject(apiResponse.StatusCode),flow_type,JsonConvert.SerializeObject(responseTime));

                if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                {
                    return JsonConvert.DeserializeObject<ArtemisEMISummaryDetailsResp>(apiResponse.Content);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ApiFactoryInterface.cs - GetArtemisEMISummaryDetails() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);
            }
            return null;
        }

        public async Task<ArtemisAgentDetailsResp> GetAgentDetailsByIntermediaryCode(string traceId, string intermediaryCode, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string token = await GetApiFactoryToken(traceId, flow_type);
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                string apiUrl = $"{ApplicationConfig.APIFACTORY_AGENT_DETAILS_URL}{Uri.EscapeDataString(intermediaryCode)}";

                var client = new RestClient(apiUrl);
                var request = new RestRequest(apiUrl, Method.Get);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Bearer " + token);

                stopwatch = Stopwatch.StartNew();

                RestResponse apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, "",  "", apiResponse.Content, apiUrl, "ARTEMIS AGENT DETAILS",  JsonConvert.SerializeObject(apiResponse.StatusCode),  flow_type,  JsonConvert.SerializeObject(responseTime));

                if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                {
                    return JsonConvert.DeserializeObject<ArtemisAgentDetailsResp>(apiResponse.Content);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ApiFactoryInterface.cs - GetAgentDetailsByAgentCode() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.APIFACTORY_INTERFACE_LOGS);
            }

            return null;
        }
    }
}
