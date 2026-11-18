using DigitalPaymentSystem_ILTC.Constants;
using Newtonsoft.Json;
using RestSharp;
using System.Security.Cryptography;
using System.Text;
using static DigitalPaymentSystem_ILTC.Models.EncryptDecryptModel;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class EncryptDecrypt
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public string EncryptRequest(string inputData)
        {
            string kv = string.Empty;
            string retVal = string.Empty;

            try
            {
                kv = ApplicationConfig.ENCRYPT_DECRYPT_KV;

                var key = Encoding.UTF8.GetBytes(kv.Substring(0, 16));
                var iv = Encoding.UTF8.GetBytes(kv.Substring(16, 16));

                using(var aesAlg = Aes.Create())
                {
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    aesAlg.Key = key;
                    aesAlg.IV = iv;

                    var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    string plainText = inputData is string ? (string)inputData : JsonConvert.SerializeObject(inputData);

                    if (string.IsNullOrEmpty(plainText))
                    {
                        return retVal;
                    }

                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (var swEncrypt = new StreamWriter(csEncrypt))
                            {
                                swEncrypt.Write(plainText);
                            }
                        }
                        retVal = Convert.ToBase64String(msEncrypt.ToArray())
                        .Replace('+', '-')
                        .Replace('/', '_');
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "EncryptDecrypt.cs - EncryptRequest() ",
                    TraceId: inputData,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.ENCRYPT_DECRYPT_LOGS);
            }

            return retVal;
        }

        public string DecryptResponse(string cipherText)
        {
            string kv = string.Empty;
            string retVal = string.Empty;

            try
            {
                kv = ApplicationConfig.ENCRYPT_DECRYPT_KV;

                var key = Encoding.UTF8.GetBytes(kv.Substring(0, 16));
                var iv = Encoding.UTF8.GetBytes(kv.Substring(16, 16));

                if (string.IsNullOrEmpty(cipherText))
                {
                    return retVal;
                }

                string base64 = cipherText
                    .Replace('-', '+')
                    .Replace('_', '/');

                using (var aesAlg = Aes.Create())
                {
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    aesAlg.Key = key;
                    aesAlg.IV = iv;

                    var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    var cipherBytes = Convert.FromBase64String(base64);

                    using (var msDecrypt = new MemoryStream(cipherBytes))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                retVal = srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "EncryptDecrypt.cs - DecryptResponse() ",
                    TraceId: cipherText,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.ENCRYPT_DECRYPT_LOGS);
            }

            return retVal;
        }
    }
}
