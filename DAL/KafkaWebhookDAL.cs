using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class KafkaWebhookDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<bool> UpdateMandateDetails(string AUTH_CODE, string Mandate_status, string proposal_number, string asba_retain_amount)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_ASBA_DETAILS;
                    var parameter = new
                    {
                        AUTH_CODE = AUTH_CODE,
                        MANDATE_STATUS = Mandate_status,
                        PROPOSAL_NUMBER = proposal_number,
                        ASBA_RETAIN_AMOUNT = asba_retain_amount
                    };
                    int result = await connection.ExecuteAsync(query, parameter);
                    if (result > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookDAL.cs - UpdateMandateDetails()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);
                return false;
            }
            return false;
        }

        public async Task<ProposalDetails> CheckProposalExists(string proposal_number)
        {
            ProposalDetails result = new ProposalDetails();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.CHECK_PROPOSAL_EXISTS;
                    var parameter = new
                    {
                        PROPOSAL_NUMBER = proposal_number
                    };
                    result = await connection.QueryFirstOrDefaultAsync<ProposalDetails>(query, parameter);

                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookDAL.cs - CheckProposalExists()",
                    TraceId: result.TRACE_ID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);
                return null;
            }
            return result;
        }

        public async Task<bool> UpdatePolicyNumberDetails(string proposal_number, string policy_number, string trace_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_POLICY_NUMBER_DETAILS;
                    var parameter = new
                    {
                        PROPOSAL_NUMBER = proposal_number,
                        POLICY_NUMBER = policy_number
                    };
                    int result = await connection.ExecuteAsync(query, parameter);
                    if (result > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookDAL.cs - UpdatePolicyNumberDetails()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);
                return false;
            }
            return false;
        }

        public async Task<bool> UpdateAsbaProcessedFlag(string proposal_number, string is_asba_processed, string trace_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_ASBA_PROCESSED_FLAG;
                    var parameter = new
                    {
                        PROPOSAL_NUMBER = proposal_number,
                        IS_ASBA_PROCESSED = is_asba_processed
                    };
                    int result = await connection.ExecuteAsync(query, parameter);
                    if (result > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookDAL.cs - UpdateAsbaProcessedFlag()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);
                return false;
            }
            return false;
        }
    }
}
