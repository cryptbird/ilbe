using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Npgsql;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PennyDropModel;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class PennyDropDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task InsertPennyDropDetails(string traceId, PennyDropReq pennyDropReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string insertQuery = DatabaseConfig.INSERT_PENNY_DROP_DETAILS;

                    var parameters = new
                    {
                        TRACE_ID = traceId,
                        BANK_ACCOUNT_NUMBER = pennyDropReq.bank_account_number,
                        BANK_ACCOUNT_HOLDER_NAME = pennyDropReq.bank_account_holder_name,
                        BANK_IFSC_CODE = pennyDropReq.bank_ifsc_code,
                    };

                    await connection.ExecuteAsync(insertQuery, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PennyDropDAL.cs - InsertPennyDropDetails() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PENNY_DROP_LOGS);
            }
        }

        public async Task UpdatePennyDropDetails(string traceId, char is_penny_drop_success, char is_account_verified, string account_holder_name_at_bank, double fuzzy_logic_percentage_match)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string updateQuery = DatabaseConfig.UPDATE_PENNY_DROP_DETAILS;

                    var parameters = new
                    {
                        TRACE_ID = traceId,
                        IS_PENNY_DROP_SUCCESS = is_penny_drop_success,
                        IS_ACCOUNT_VERIFIED = is_account_verified,
                        ACCOUNT_HOLDER_NAME_AT_BANK = account_holder_name_at_bank,
                        FUZZY_LOGIC_PERCENTAGE_MATCH = fuzzy_logic_percentage_match
                    };

                    await connection.ExecuteAsync(updateQuery, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PennyDropDAL.cs - UpdatePennyDropDetails() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PENNY_DROP_LOGS);
            }
        }
    }
}
