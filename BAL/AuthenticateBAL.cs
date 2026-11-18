using Amazon.Auth.AccessControlPolicy;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PlutusModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class AuthenticateBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public DPSStatusResponse CreateAuthToken(string username, string password)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateToken authenticateToken = new AuthenticateToken();

            int expiresIn = Convert.ToInt32(ApplicationConfig.TOKEN_EXPIRY_TIME);

            try
            {
                var claims = new[] {
                new Claim("username", username)
               };

                string secretKey = ApplicationConfig.JWT_SECRET_KEY;
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

                var token = new JwtSecurityToken(
                    issuer: ApplicationConfig.JWT_ISSUER,
                    audience: ApplicationConfig.JWT_AUDIENCE,
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(expiresIn),
                    signingCredentials: creds
                 );

                authenticateToken.accessToken = new JwtSecurityTokenHandler().WriteToken(token);
                authenticateToken.expiryTime = Convert.ToString(DateTime.Now.AddMinutes(expiresIn));

                response.StatusCode = 1;
                response.StatusMessage = "Success";
                response.Message = null;
                response.TraceId = null;
                response.Data = authenticateToken;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateBAL.cs - CreateAuthToken() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public AuthenticateTokenData GetDetailsFromAuthToken(string Authorization)
        {
            AuthenticateTokenData authenticateTokenData = new AuthenticateTokenData();

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var bearerToken = Authorization?.Substring("Bearer ".Length).Trim();
                var jwtToken = handler.ReadToken(bearerToken) as JwtSecurityToken;

                if (jwtToken != null)
                {
                    authenticateTokenData.username = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "username")?.Value ?? string.Empty;
                    authenticateTokenData.unique_name = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "unique_name")?.Value ?? string.Empty;
                    authenticateTokenData.jti = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "jti")?.Value ?? string.Empty;
                    authenticateTokenData.Role = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "Role")?.Value ?? string.Empty;
                    authenticateTokenData.UserLogin = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "UserLogin")?.Value ?? string.Empty;
                    authenticateTokenData.Client = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "Client")?.Value ?? string.Empty;
                    authenticateTokenData.CCId = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "CCId")?.Value ?? string.Empty;
                    authenticateTokenData.IsGuest = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "IsGuest")?.Value ?? string.Empty;
                    authenticateTokenData.SessionId = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "SessionId")?.Value ?? string.Empty;
                    authenticateTokenData.SKey = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "SKey")?.Value ?? string.Empty;
                    authenticateTokenData.SP = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "SP")?.Value ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateBAL.cs - GetDetailsFromAuthToken() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);
            }

            return authenticateTokenData;
        }

        public async Task<DPSStatusResponse> CreateAuthTokenv2(string username, string password)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateDAL authenticateDAL = new AuthenticateDAL();

            AuthenticateToken authenticateToken = new AuthenticateToken();

            int expiresIn = Convert.ToInt32(ApplicationConfig.TOKEN_EXPIRY_TIME);

            try
            {
                var claims = new[] {
                new Claim("username", username)
               };

                string secretKey = ApplicationConfig.JWT_SECRET_KEY;
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

                var token = new JwtSecurityToken(
                    issuer: ApplicationConfig.JWT_ISSUER,
                    audience: ApplicationConfig.JWT_AUDIENCE,
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(expiresIn),
                    signingCredentials: creds
                 );

                authenticateToken.accessToken = new JwtSecurityTokenHandler().WriteToken(token);
                authenticateToken.expiryTime = Convert.ToString(DateTime.Now.AddMinutes(expiresIn));

                response.StatusCode = 1;
                response.StatusMessage = "Success";
                response.Message = null;
                response.TraceId = null;
                response.Data = authenticateToken;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateBAL.cs - CreateAuthTokenv2() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> RefreshToken(string authorization, string encryptedId)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            RefreshTokenResp refreshTokenResp = new RefreshTokenResp();
            AuthenticateDAL authenticateDAL = new AuthenticateDAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            SecurityToken validatedToken;
            string transactionID = string.Empty;
            string traceID = string.Empty;
            DateTime? transactionTime = null;
            bool isExpired = false;
            string usernameFromToken = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                    TraceId: traceID,
                    Message: "Invalid Token for ID: " + encryptedId), ApplicationConfig.AUTHENTICATION_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid Token";
                    response.TraceId = null;
                    response.Data = null;

                    return response;
                }

                string decryptedId = encryptDecrypt.DecryptResponse(encryptedId);
                
                if (string.IsNullOrEmpty(decryptedId) || !decryptedId.Contains("|"))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                    TraceId: traceID,
                    Message: "Invalid ID format. Decrypted ID: " + decryptedId), ApplicationConfig.AUTHENTICATION_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = null;
                    response.Data = null;

                    return response;
                }

                string[] idParts = decryptedId.Split("|");

                transactionID = idParts[0];
                traceID = idParts[1];

                TxnAuthDetails txnAuthDetails = await authenticateDAL.GetTxnAuthDetails(transactionID, traceID);
                if (txnAuthDetails != null && !string.IsNullOrEmpty(txnAuthDetails.username))
                {
                    transactionTime = txnAuthDetails.payment_done_at ?? txnAuthDetails.txn_created_on;

                    if (transactionTime.HasValue)
                    {
                        if (!(DateTime.Now >= transactionTime.Value.AddHours(12)))
                        {
                            var tokenHandler = new JwtSecurityTokenHandler();
                            var bearerToken = authorization?.Substring("Bearer ".Length).Trim();
                            var jwtToken = tokenHandler.ReadToken(bearerToken) as JwtSecurityToken;

                            usernameFromToken = jwtToken.Claims.FirstOrDefault(c => c.Type == "username")?.Value ?? string.Empty;

                            if (!usernameFromToken.Equals(txnAuthDetails.username, StringComparison.OrdinalIgnoreCase))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                                TraceId: traceID,
                                Message: "Invalid Token -> Token username does not match transaction username. Decrypted ID: " + decryptedId), ApplicationConfig.AUTHENTICATION_LOGS);


                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Invalid Token";
                                response.TraceId = traceID;
                                response.Data = null;

                                return response;
                            }

                            DateTime expiryUtc = jwtToken.ValidTo;
                            DateTime expiryIst = TimeZoneInfo.ConvertTimeFromUtc(expiryUtc, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));

                            TimeSpan remaining = expiryIst - DateTime.Now;


                            if (remaining <= TimeSpan.FromMinutes(2))
                            {
                                // create new token
                                DPSStatusResponse newTokenResp = await CreateAuthTokenv2(usernameFromToken, txnAuthDetails.password);
                                if (newTokenResp.StatusCode == 1 && newTokenResp.Data != null)
                                {
                                    AuthenticateToken tokenData = JsonConvert.DeserializeObject<AuthenticateToken>(JsonConvert.SerializeObject(newTokenResp.Data));

                                    refreshTokenResp.authTokenData = new AuthenticateModel.AuthenticateToken
                                    {
                                        expiryTime = tokenData.expiryTime,
                                        accessToken = tokenData.accessToken
                                    };
                                    refreshTokenResp.isRefreshed = true;

                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                                    TraceId: traceID,
                                    Message: "Token Refreshed Successfully Decrypted ID: " + decryptedId + " Response:- " + JsonConvert.SerializeObject(refreshTokenResp)), ApplicationConfig.AUTHENTICATION_LOGS);


                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = "Token Refreshed Successfully";
                                    response.TraceId = traceID;
                                    response.Data = refreshTokenResp;
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                                    TraceId: traceID,
                                    Message: "Failed to create auth token. Decrypted ID: " + decryptedId), ApplicationConfig.AUTHENTICATION_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Token Refresh Failed";
                                    response.TraceId = traceID;
                                    response.Data = null;
                                }
                            }
                            else
                            {
                                refreshTokenResp.authTokenData = new AuthenticateModel.AuthenticateToken
                                {
                                    expiryTime = expiryIst.ToString("yyyy-MM-dd HH:mm:ss"),
                                    accessToken = bearerToken
                                };
                                refreshTokenResp.isRefreshed = false;

                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                                TraceId: traceID,
                                Message: "Token still valid Decrypted ID: " + decryptedId + " Response:- " + JsonConvert.SerializeObject(refreshTokenResp)), ApplicationConfig.AUTHENTICATION_LOGS);


                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = "Token is still valid.";
                                response.TraceId = traceID;
                                response.Data = refreshTokenResp;
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                            TraceId: traceID,
                            Message: "Failed to create auth token. Decrypted ID: " + decryptedId), ApplicationConfig.AUTHENTICATION_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Payment Session Expired";
                            response.TraceId = traceID;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                        TraceId: traceID,
                        Message: "No transaction details found. Decrypted ID: " + decryptedId), ApplicationConfig.AUTHENTICATION_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid Transaction";
                        response.TraceId = traceID;
                        response.Data = null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                    TraceId: traceID,
                    Message: "Transaction details not found for this ID. Decrypted ID: " + decryptedId), ApplicationConfig.AUTHENTICATION_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "No Transaction Details Found";
                    response.TraceId = traceID;
                    response.Data = null;
                }              
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateBAL.cs - RefreshToken() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = traceID;
                response.Data = null;
            }

            return response;
        }
    }
}
