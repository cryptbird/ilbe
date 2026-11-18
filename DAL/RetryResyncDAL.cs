using Dapper;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;

namespace DigitalPaymentSystem_ILTC.DAL
{

    public class RetryResyncDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<List<RetryResyncCase>> GetSTPRetryResyncCases()
        {
            List<RetryResyncCase> retryResyncCasesList = new List<RetryResyncCase>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var result = await connection.QueryAsync<RetryResyncCase>(DatabaseConfig.GET_STP_RETRY_RESYNC_TRANSACTION_CASES_VIEW);
                    retryResyncCasesList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncDAL.cs - GetSTPRetryResyncCases()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return retryResyncCasesList;
        }

        public async Task<List<RetryResyncCase>> GetNSTPRetryResyncCases()
        {
            List<RetryResyncCase> retryResyncCasesList = new List<RetryResyncCase>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var result = await connection.QueryAsync<RetryResyncCase>(DatabaseConfig.GET_NSTP_RETRY_RESYNC_TRANSACTION_CASES_VIEW);
                    retryResyncCasesList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncDAL.cs - GetNSTPRetryResyncCases()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return retryResyncCasesList;
        }

        public async Task UpdatePaymentIDForProposalNo(string payment_id, string proposal_number)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string query = DatabaseConfig.UPDATE_PAYMENT_ID_FOR_PROPOSAL_NO;
                    var updateParameters = new
                    {
                        PAYMENT_ID = payment_id,
                        PROPOSAL_NUMBER = proposal_number,
                    };
                    await connection.ExecuteAsync(query, updateParameters);

                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - updatePaymentIDForProposalNo()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
                }
            }
        }

        public async Task UpdatePolicyNoForProposalNo(string policy_number, string proposal_number)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string query = DatabaseConfig.UPDATE_POLICY_NO_FOR_PROPOSAL_NO;
                    var updateParameters = new
                    {
                        POLICY_NUMBER = policy_number,
                        PROPOSAL_NUMBER = proposal_number
                    };
                    await connection.ExecuteAsync(query, updateParameters);

                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - updatePolicyNoForProposalNo()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
                }
            }
        }

        public async Task UpdateNoOfAttemptsCount(int no_of_attempts, string proposal_number)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string query = DatabaseConfig.UPDATE_NO_OF_ATTEMPTS_FOR_PROPOSAL_NO;
                    var updateParameters = new
                    {
                        NO_OF_ATTEMPTS = no_of_attempts,
                        PROPOSAL_NUMBER = proposal_number,
                    };
                    await connection.ExecuteAsync(query, updateParameters);

                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - updatePolicyNoForProposalNo()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
                }
            }
        }

        public async Task<List<string>> GetIsProcessingFlag(string proposal_number)
        {
            var resultList = new List<string>();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_IS_DASHBOARD_RETRYRESYNC_PROCESSING;
                    var queryParams = new { PROPOSAL_NUMBER = proposal_number };
                    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(query, queryParams);

                    if (result != null)
                    {
                        resultList.Add(result.is_retryresync_processing?.ToString());
                        resultList.Add(result.is_dashboard_processing?.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetIsProcessingFlag()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }
            return resultList;
        }

        public async Task<bool> SetIsDashboardProcessing(string isDashboardProcessing, string proposal_number)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.SET_IS_DASHBOARD_PROCESSING;
                    var queryParams = new { IS_DASHBOARD_PROCESSING = isDashboardProcessing, PROPOSAL_NUMBER = proposal_number };
                    var result = await connection.ExecuteAsync(query, queryParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - SetIsDashboardProcessing()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);

                return false;
            }
        }

        public async Task<bool> SetIsRetryResyncProcessing(string isRetryResyncProcessing, string proposal_number)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.SET_IS_RETRYRESYNC_PROCESSING;
                    var queryParams = new { IS_RETRYRESYNC_PROCESSING = isRetryResyncProcessing, PROPOSAL_NUMBER = proposal_number };
                    var result = await connection.ExecuteAsync(query, queryParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - SetIsRetryResyncProcessing()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);

                return false;
            }
        }

        public async Task<string> GetPaymentIDFromDb(string transaction_id)
        {
            string paymentId = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_PAYMENT_ID_FROM_DB;
                    var queryParams = new { TRANSACTION_ID = transaction_id };
                    paymentId = await connection.QueryFirstOrDefaultAsync<string>(query, queryParams);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetPaymentIDFromDb()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }
            return paymentId;
        }

        public async Task<string> GetPortingUpdate(string trace_id, string proposal_number)
        {
            string IsApproved = string.Empty;

            try
            {
                using (var connection = new OracleConnection(await SecretManager.GetEchannelOracleSecretValue()))
                {
                    string query = DatabaseConfig.GET_PORTING_STATUS;
                    IsApproved = await connection.QueryFirstOrDefaultAsync<string>(query, new { PROPOSAL_NUMBER = proposal_number });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RetryResyncBAL.cs - GetPortingUpdate() ",
                       TraceId: trace_id,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return IsApproved;
        }

        public async Task<List<UpdatePIDReq>> GetUpdateAsbaPaymentIdCases()
        {
            List<UpdatePIDReq> updatePIDReqCases = new List<UpdatePIDReq>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var result = await connection.QueryAsync<UpdatePIDReq>(DatabaseConfig.GET_UPDATE_ASBA_PAYMENTID_CASES);
                    updatePIDReqCases = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncDAL.cs - GetUpdateAsbaPaymentIdCases()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return updatePIDReqCases;
        }
    }
}