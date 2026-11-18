using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Npgsql;
using static DigitalPaymentSystem_ILTC.Models.DropLeadModels;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class DropLeadDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();
        public async Task<LeadDetailsResp> GetLeadDetails (string transactionID, string traceID)
        {
            LeadDetailsResp leadDetailsResp = new LeadDetailsResp();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_LEAD_DETAILS;
                    var parameters = new
                    {
                        TRANSACTION_ID = transactionID
                    };
                    leadDetailsResp = await connection.QueryFirstOrDefaultAsync<LeadDetailsResp>(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DropLeadDAL.cs - GetLeadDetails()",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DROP_LEAD_LOGS);
                return null;
            }
            return leadDetailsResp;
        }
    }
}
