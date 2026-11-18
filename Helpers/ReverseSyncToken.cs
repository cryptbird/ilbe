using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;
using RestSharp;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.ReverseSyncTokenModel;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class ReverseSyncToken
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public static string GetAlphaNumericString(int length)
        {
            try
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new Random();
                return new string(Enumerable.Repeat(chars, length)
                  .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ReverseSyncToken.cs - GetAlphaNumericString()",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);

                return string.Empty;
            }
        }

        public static string EncryptRequest(string plainText, string keyValue, string ivValue)
        {
            try
            {
                var key = Encoding.UTF8.GetBytes(keyValue);
                var iv = Encoding.UTF8.GetBytes(ivValue);
                string retVal = string.Empty;

                // Check arguments.
                if (plainText == null || plainText.Length <= 0)
                {
                    return retVal;
                }
                // with the specified key and IV.
                using (var aesAlg = Aes.Create())
                {
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    aesAlg.FeedbackSize = 128;

                    aesAlg.Key = key;
                    aesAlg.IV = iv;

                    // Create a decrytor to perform the stream transform.
                    var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    // Create the streams used for encryption.
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (var swEncrypt = new StreamWriter(csEncrypt))
                            {
                                //Write all data to the stream.
                                swEncrypt.Write(plainText);
                            }
                        }
                        retVal = Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }

                // Return the encrypted bytes from the memory stream.
                return retVal;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ReverseSyncToken.cs - EncryptRequest()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);

                return string.Empty;
            }
        }

        public static string EncryptWithPublicKey(string message)
        {
            try
            {
                var bytesToEncrypt = Encoding.UTF8.GetBytes(message);
                var encryptEngine = new Pkcs1Encoding(new RsaEngine());

                string publicKey = ApplicationConfig.SME_REVERSE_SYNC_TOKEN_PUBLIC_KEY;

                using (var txtreader = new StringReader(publicKey))
                {
                    var keyParameter = (AsymmetricKeyParameter)new PemReader(txtreader).ReadObject();
                    encryptEngine.Init(true, keyParameter);
                }
                var encrypted = Convert.ToBase64String(encryptEngine.ProcessBlock(bytesToEncrypt, 0, bytesToEncrypt.Length));
                return encrypted;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ReverseSyncToken.cs - EncryptWithPublicKey()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);

                return string.Empty;
            }
        }

        public static WebTokenEncryptedReq TokenEncryptAndConcat(string plainText)
        {
            WebTokenEncryptedReq reqModel = new WebTokenEncryptedReq();

            try
            {
                string kv = GetAlphaNumericString(32);
                string encryptedpayload = EncryptRequest(plainText, kv.Substring(0, 16), kv.Substring(16, 16));
                string kvencrypt = EncryptWithPublicKey(kv);
                encryptedpayload = "e01" + encryptedpayload;

                reqModel.kv = kvencrypt;
                reqModel.version = ApplicationConfig.SME_REVERSE_SYNC_TOKEN_VERSION;
                reqModel.payload = encryptedpayload;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ReverseSyncToken.cs - TokenEncryptAndConcat()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return reqModel;
        }

        public async Task<string> GetSMEAuthorizationToken(string trace_id, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();

            WebTokenRequest smeTokenRequest = new WebTokenRequest();
            WebTokenResponse smeTokenResponse = new WebTokenResponse();
            string authToken = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                smeTokenRequest.Client = ApplicationConfig.SME_REVERSE_SYNC_TOKEN_CLIENT;
                smeTokenRequest.Scope = ApplicationConfig.SME_REVERSE_SYNC_TOKEN_SCOPE;

                WebTokenEncryptedReq reqModel = TokenEncryptAndConcat(JsonConvert.SerializeObject(smeTokenRequest));

                if (reqModel != null)
                {
                    WebEncrypted encrypted = new WebEncrypted();

                    if (string.IsNullOrEmpty(reqModel.payload))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                          MethodName: "ReverseSyncToken.cs - GetSMEAuthorizationToken() ",
                          Message: "Exception reqModel.payload is Empty",
                          TraceId: trace_id), ApplicationConfig.CALLBACK_LOGS);
                    }
                    else
                    {
                        encrypted.msg = reqModel.payload;

                        try
                        {
                            string URL = ApplicationConfig.SME_REVERSE_SYNC_TOKEN_API_URL;
                            var client = new RestClient();
                            var request = new RestRequest(URL, Method.Post);
                            request.AddHeader("version", reqModel.version);
                            request.AddHeader("kv", reqModel.kv);
                            request.AddHeader("Content-Type", "application/json");
                            var body = JsonConvert.SerializeObject(encrypted);
                            request.AddStringBody(body, DataFormat.Json);

                            stopwatch = Stopwatch.StartNew();

                            RestResponse response = await client.ExecuteAsync(request);

                            stopwatch.Stop();
                            var responseTime = stopwatch.Elapsed.TotalSeconds;

                            await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(smeTokenRequest), response.Content, URL, "SME REVERSE SYNC TOKEN API", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                            if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                            {
                                smeTokenResponse = JsonConvert.DeserializeObject<WebTokenResponse>(response.Content);

                                if (smeTokenResponse != null || !string.IsNullOrEmpty(smeTokenResponse.Passcode))
                                {
                                    authToken = smeTokenResponse.Passcode;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();

                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "ReverseSyncToken.cs - GetSMEAuthorizationToken()",
                                TraceId: trace_id,
                                Message: $"Exception occurred: {ex.Message}",
                                StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ReverseSyncToken.cs - GetSMEAuthorizationToken()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return authToken;
        }

        public async Task<string> GetILTCAuthorizationToken(string trace_id, string username, string password, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            ILTCTokenRequest iltcTokenRequest = new ILTCTokenRequest();
            ILTCTokenResponse iltcTokenResponse = new ILTCTokenResponse();

            string ILTCToken = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                iltcTokenRequest.Username = username;
                iltcTokenRequest.Password = password;
                var client = new RestClient();
                var request = new RestRequest(ApplicationConfig.ILTC_TOKEN_REVERSE_SYNC_API_URL, Method.Post);
                request.AddHeader("Content-Type", "application/json");

                request.AddParameter("application/json", JsonConvert.SerializeObject(iltcTokenRequest), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(iltcTokenRequest), response.Content, ApplicationConfig.ILTC_TOKEN_REVERSE_SYNC_API_URL, "ILTC REVERSE SYNC TOKEN API", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    iltcTokenResponse = JsonConvert.DeserializeObject<ILTCTokenResponse>(response.Content);
                    if (iltcTokenResponse != null || !string.IsNullOrEmpty(iltcTokenResponse.data.token))
                    {
                        ILTCToken = iltcTokenResponse.data.token;
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ReverseSyncToken.cs - GetILTCAuthorizationToken()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
            return ILTCToken;
        }

        public async Task<string> GetWebAuthorizationToken(string trace_id, string flow_type)
        {
            WebTokenRequest webTokenRequest = new WebTokenRequest();
            WebTokenResponse webTokenResponse = new WebTokenResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            string authToken = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                webTokenRequest.Client = ApplicationConfig.WEB_TOKEN_CLIENT;
                webTokenRequest.Scope = ApplicationConfig.WEB_TOKEN_SCOPE;

                WebTokenEncryptedReq reqModel = TokenEncryptAndConcat(JsonConvert.SerializeObject(webTokenRequest));

                if (reqModel != null)
                {
                    WebEncrypted encrypted = new WebEncrypted();

                    if (string.IsNullOrEmpty(reqModel.payload))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                          MethodName: "ReverseSyncToken.cs - GetWebAuthorizationToken() ",
                          Message: "Exception reqModel.payload is Empty",
                          TraceId: trace_id), ApplicationConfig.CALLBACK_LOGS);
                    }
                    else
                    {
                        encrypted.msg = reqModel.payload;

                        try
                        {
                            string URL = ApplicationConfig.WEBSITE_AUTH_TOKEN_URL;
                            var client = new RestClient();
                            var request = new RestRequest(URL, Method.Post);
                            request.AddHeader("version", reqModel.version);
                            request.AddHeader("kv", reqModel.kv);
                            request.AddHeader("Content-Type", "application/json");
                            var body = JsonConvert.SerializeObject(encrypted);
                            request.AddStringBody(body, DataFormat.Json);

                            stopwatch = Stopwatch.StartNew();

                            RestResponse response = await client.ExecuteAsync(request);

                            stopwatch.Stop();
                            var responseTime = stopwatch.Elapsed.TotalSeconds;

                            await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(webTokenRequest), response.Content, URL, "WEBSITE AUTH TOKEN API", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                            if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                            {
                                webTokenResponse = JsonConvert.DeserializeObject<WebTokenResponse>(response.Content);

                                if (webTokenResponse != null || !string.IsNullOrEmpty(webTokenResponse.Passcode))
                                {
                                    authToken = webTokenResponse.Passcode;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();

                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "ReverseSyncToken.cs - GetWebAuthorizationToken()",
                                  TraceId: trace_id,
                                  Message: $"Exception occurred: {ex.Message}",
                                  StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                     MethodName: "ReverseSyncToken.cs - GetWebAuthorizationToken()",
                     TraceId: trace_id,
                     Message: $"Exception occurred: {ex.Message}",
                     StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
            return authToken;
        }
    }
}
