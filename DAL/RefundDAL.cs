using Amazon.CloudWatchLogs;
using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Org.BouncyCastle.Crypto;
using static DigitalPaymentSystem_ILTC.Models.RefundModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;
namespace DigitalPaymentSystem_ILTC.DAL
{
    public class RefundDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<List<FetchRefundCase>> FetchRefundCases()
        {
            List<FetchRefundCase> fetchRefundCaseList = new List<FetchRefundCase>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_REFUND_CASES_VIEW;
                    var result = await connection.QueryAsync<FetchRefundCase>(query);
                    fetchRefundCaseList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - FetchRefundCases() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }

            return fetchRefundCaseList;
        }

        public async Task<FetchRefundDetails> FetchRefundDetails(string transaction_id, string proposal_number, string auth_code)
        {
            FetchRefundDetails fetchRefundDetails = new FetchRefundDetails();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.FETCH_REFUND_DETAILS;

                    var parameters = new
                    {
                        TRANSACTION_ID = transaction_id,
                        PROPOSAL_NUMBER = proposal_number,
                        AUTH_CODE = auth_code
                    };

                    fetchRefundDetails = await connection.QueryFirstOrDefaultAsync<FetchRefundDetails>(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - FetchRefundDetails() ",
                    TraceId: transaction_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }

            return fetchRefundDetails;
        }

        public async Task<FetchApplicationDetails> GetApplicationDetails(string application_name)
        {
            FetchApplicationDetails fetchApplicationDetails = new FetchApplicationDetails();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_APPLICATION_DETAILS;

                    fetchApplicationDetails =
                        await connection.QuerySingleOrDefaultAsync<FetchApplicationDetails>( query, new { application_name } );
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - GetApplicationDetails() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }

            return fetchApplicationDetails;
        }

        public async Task<FetchBreakInDetailsByProposalNo> GetBreakinDetailsByProposalNo(string proposal_number)
        {
            FetchBreakInDetailsByProposalNo fetchBreakInDetailsByProposalNo = new FetchBreakInDetailsByProposalNo();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_BREAKINDETAILS_BY_PROPOSALNO;
                    fetchBreakInDetailsByProposalNo = await connection.ExecuteScalarAsync<FetchBreakInDetailsByProposalNo>(query, new { PROPOSAL_NUMBER = proposal_number });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - GetBreakinDetailsByProposalNo() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }

            return fetchBreakInDetailsByProposalNo;
        }

        public async Task InsertRefundDetails(FetchRefundDetails fetchRefundDetails, RefundReq refundReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.INSERT_REFUND_DETAILS;
                    var parameters = new
                    {
                        PROPOSAL_NUMBER = refundReq.proposal_number,
                        POLICY_NUMBER = refundReq.policy_number,
                        CANC_PROPOSAL_NUMBER = refundReq.canc_proposal_number,
                        TRANSACTION_ID = refundReq.transaction_id,
                        PROPOSAL_AMOUNT = fetchRefundDetails.proposal_amount,
                        TRACE_ID = fetchRefundDetails.trace_id,
                        CORE = fetchRefundDetails.core,
                        APPLICATION_NAME = fetchRefundDetails.application_name,
                        PAYMENT_ID = fetchRefundDetails.payment_id,
                        DPS_ORDER_ID = fetchRefundDetails.dps_order_id,
                        AUTH_CODE = fetchRefundDetails.auth_code,
                        PRODUCT = fetchRefundDetails.product,
                        PROPOSAL_START_DATE = fetchRefundDetails.proposal_start_date,
                        VOUCHER_NUMBER = fetchRefundDetails.voucher_number,
                        REFUND_STATUS = fetchRefundDetails.refund_status,
                        CHANNEL_APPLICATION_DETAILS = fetchRefundDetails.channel_application_details,
                        PAYER_TYPE = fetchRefundDetails.payer_type,
                        CUSTOMER_ID = fetchRefundDetails.customer_id
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - InsertRefundDetails() ",
                    TraceId: fetchRefundDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }
        }

        public async Task UpdateRefundStatus(string trace_id, string proposal_number, string refund_status)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_REFUND_DETAILS;
                    var parameters = new
                    {
                        PROPOSAL_NUMBER = proposal_number,
                        REFUND_STATUS = refund_status,
                    };
                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - UpdateRefundStatus() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }
        }

        public async Task UpdateVoucherNumber(string trace_id, string proposal_number, string voucher_number)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_VOUCHER_NUMBER;
                    var parameters = new
                    {
                        PROPOSAL_NUMBER = proposal_number,
                        VOUCHER_NUMBER = voucher_number,
                    };
                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - UpdateVoucherNumber() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }
        }

        public async Task<bool> CheckRefundCaseExists(string proposal_number, FetchRefundDetails fetchRefundDetails)
        {
            bool refundCaseExists = false;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_REFUND_CASE;
                    var parameters = new
                    {
                        PROPOSAL_NUMBER = proposal_number,
                    };
                    var result = await connection.QueryAsync<FetchRefundCase>(query, parameters);

                    refundCaseExists = result.Count() > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - CheckRefundCaseExists() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
                refundCaseExists = false;
            }
            return refundCaseExists;
        }

        public async Task<string> GetIsRefundSyncProcessingFlag(string proposal_number)
        {
            string isRefundSyncProcessing = string.Empty;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_IS_REFUND_SYNC_PROCESSING;
                    var queryParams = new { PROPOSAL_NUMBER = proposal_number };
                    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(query, queryParams);

                    if (result != null)
                    {
                        isRefundSyncProcessing= result.is_refund_sync_processing;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - GetIsRefundSyncProcessingFlag()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }
            return isRefundSyncProcessing;
        }

        public async Task<bool> SetIsRefundSyncProcessingFlag(string isRefundSyncProcessing, string proposal_number)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.SET_IS_REFUND_SYNC_PROCESSING;
                    var queryParams = new { IS_REFUND_SYNC_PROCESSING = isRefundSyncProcessing, PROPOSAL_NUMBER = proposal_number };
                    var result = await connection.ExecuteAsync(query, queryParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - SetIsRefundSyncProcessingFlag()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);

                return false;
            }
        }

        public async Task<string> InitiateRefundTransaction(string proposal_number)
        {
            string refundIdStr = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string getTransactionIdSeq = DatabaseConfig.GET_REFUND_ID_SEQ;
                    long refundId = await connection.ExecuteScalarAsync<long>(getTransactionIdSeq);
                    refundIdStr = "DPS_REFUND_" + refundId.ToString();

                    string query = DatabaseConfig.UPDATE_REFUND_ID;

                    var parameters = new
                    {
                        REFUND_ID = refundIdStr,
                        PROPOSAL_NUMBER = proposal_number
                    };

                    int count = await connection.ExecuteAsync(query, parameters);
                    if (count > 0)
                    {
                        string insert_query = DatabaseConfig.INSERT_REFUND_MAPPING_DETAILS;
                        var insert_parameters = new
                        {
                            PROPOSAL_NUMBER = proposal_number,
                            REFUND_ID = refundIdStr,
                        };
                        int insert_count = await connection.ExecuteAsync(insert_query, insert_parameters);
                        if (insert_count > 0)
                        {
                            return refundIdStr;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundDAL.cs - SetIsRefundSyncProcessingFlag()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }

            return null;
        }
    }
}
