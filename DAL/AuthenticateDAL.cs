using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Npgsql;
using System.Diagnostics;
using System.Text;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
namespace DigitalPaymentSystem_ILTC.DAL
{
    public class AuthenticateDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<bool> IsValidAuth(string Username, string Password , string? completeHeader)
        {
            bool validFlag = false;

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.IS_VALID_AUTH;


                    int count = await connection.ExecuteScalarAsync<int>(
                        query,
                        new { Username, Password });

                    validFlag = count > 0;                  
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "AuthenticateDAL.cs - IsValidAuth() ",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace),
                    ApplicationConfig.AUTHENTICATION_LOGS);
            }
            finally
            {
                stopwatch.Stop();
                double responseTime = stopwatch.Elapsed.TotalSeconds;

                // Call Power Automate API
                SendNotificationAsync(responseTime, Username, completeHeader);
            }

            return validFlag;
        }

        private void SendNotificationAsync(double responseTime, string Username, string? completeHeader)
        {
            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "SendNotificationAsync",
                    Message: "Response Time: " + responseTime + " Current Date: " + DateTime.Now + " Username: " + Username + " completeHeader: "+ completeHeader,
                    TraceId: null), ApplicationConfig.AUTHENTICATION_LOGS);
            }
            catch (Exception)
            {
            }
        }

        public async Task<AuthenticateTokenData> GetAuthTokenDetails(string transactionID, string traceID)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_AUTHTOKEN_DETAILS;
                    var parameters = new { TRANSACTION_ID = transactionID };

                    var result = await connection.QueryFirstOrDefaultAsync<AuthenticateTokenData>(query, parameters);

                    return result;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateDAL.cs - GetAuthTokenDetails() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                return null;
            }
        }

        public async Task<TxnAuthDetails> GetTxnAuthDetails(string transactionID, string traceID)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_TXN_AUTH_DETAILS;

                    var result = await connection.QueryFirstOrDefaultAsync<TxnAuthDetails>(query,  new { TRANSACTION_ID = transactionID, TRACE_ID = traceID });

                    return result;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "AuthenticateDAL.cs - GetTxnAuthDetails() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.AUTHENTICATION_LOGS);

                return null;
            }
        }
    }
}