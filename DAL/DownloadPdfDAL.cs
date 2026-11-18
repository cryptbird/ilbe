using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Npgsql;
using static DigitalPaymentSystem_ILTC.Models.DownloadPdfModel;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class DownloadPdfDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<FetchPolicyDetailsFromDB> GetPolicyDetailsByProposalNo(string proposal_number, string transaction_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_POLICYDETAILS_FROM_PROPOSAL_NO;
                    var result = await connection.QueryFirstOrDefaultAsync<FetchPolicyDetailsFromDB>(query, new { PROPOSAL_NUMBER = proposal_number, TRANSACTION_ID = transaction_id });
                    return result;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetPolicyDetailsByProposalNo() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DOWNLOAD_POLICY_PDF_LOGS);
            }

            return null;
        }
    }
}
