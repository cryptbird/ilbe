using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Npgsql;
using static DigitalPaymentSystem_ILTC.Models.BreakInModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RefundModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class BreakInDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<int> InsertBreakInData(string proposal_number, string transaction_id, string trace_id, string inspection_type, string address, string pincode, string city, string state)
        {
            int rows = 0;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string checkQuery = DatabaseConfig.CHECK_IF_BREAKINDATA_EXISTS;

                    int count = await connection.ExecuteScalarAsync<int>(checkQuery, new
                    {
                        PROPOSAL_NUMBER = proposal_number
                    });

                    if (count > 0)
                    {
                        string updateQuery = DatabaseConfig.UPDATE_INITIAL_BREAKIN_DETAILS;

                        rows = await connection.ExecuteAsync(updateQuery, new
                        {
                            PROPOSAL_NUMBER = proposal_number,
                            INSPECTION_TYPE = inspection_type,
                            BREAKIN_ADDRESS = address,
                            BREAKIN_PINCODE = pincode,
                            BREAKIN_CITY = city,
                            BREAKIN_STATE = state
                        });
                    }
                    else
                    {
                        string query = DatabaseConfig.INSERT_BREAKIN_DETAILS;
                        var parameters = new
                        {
                            TRACE_ID = trace_id,
                            TRANSACTION_ID = transaction_id,
                            PROPOSAL_NUMBER = proposal_number,
                            INSPECTION_TYPE = inspection_type,
                            BREAKIN_ADDRESS = address,
                            BREAKIN_PINCODE = pincode,
                            BREAKIN_CITY = city,
                            BREAKIN_STATE = state
                        };
                        rows = await connection.ExecuteScalarAsync<int>(query, parameters);
                    }
                }

            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInDAL.cs - InsertBreakInData() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }

            return rows;
        }

        public async Task<string> GetProductFromProposalNo(string proposalNumber)
        {
            string product = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_PRODUCT_FROM_PROPOSALNO;
                    product = await connection.QuerySingleOrDefaultAsync<string>(query, new { PROPOSAL_NUMBER = proposalNumber });
                }
            }
            catch (Exception ex)
            {

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInDAL.cs - GetProductFromProposalNo()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }
            return product;
        }

        public async Task UpdateBreakInDetails(IlesbCreateBreakInResp ilesbCreateBreakInResp)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_BREAKIN_DETAILS;
                    var parameters = new
                    {
                        BREAKIN_ID = ilesbCreateBreakInResp.BrkId,
                        STATUS = ilesbCreateBreakInResp.Status,
                        REMARK = ilesbCreateBreakInResp.Remarks,
                        VRN = ilesbCreateBreakInResp.VRN,
                        PROPOSAL_NUMBER = ilesbCreateBreakInResp.ProposalNumber
                    };
                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInDAL.cs - UpdateBreakInDetails()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }
        }

        public async Task<int> UpdateBreakInStatus(string breakInId, string status, string inspection_date, string remarks)
        {
            int count = 0;
            string query = DatabaseConfig.UPDATE_BREAKIN_STATUS;

            try
            {
                DateTime inspectionDate = DateTime.Parse(inspection_date);

                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    count = await connection.ExecuteAsync(query, new { status, remarks, breakInId, inspection_date = inspectionDate });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "BreakInDAL.cs - UpdateBreakInStatus()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace),
                    ApplicationConfig.BREAKIN_LOGS);
            }

            return count;
        }

        public async Task<BreakInClearInspectionDetails> GetBreakInClearInspectionDetails(string breakinId)
        {
            BreakInClearInspectionDetails breakInClearInspectionDetails = null;

            string query = DatabaseConfig.GET_BREAKIN_CLEAR_INSPECTION_DETAILS;
            var parameters = new { BREAKIN_ID = breakinId };
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var TransactionData = await connection.QueryAsync(query, parameters);
                    if (TransactionData != null)
                    {
                        var firstRow = TransactionData.First();

                        breakInClearInspectionDetails = new BreakInClearInspectionDetails
                        {
                            ProposalNumber = firstRow.proposal_number,
                            ProposalStartDate = firstRow.proposal_start_date,
                            DealId = Convert.ToString(firstRow.deal_id),
                            traceId = firstRow.trace_id
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "BreakInDAL.cs - GetBreakInClearInspectionDetails()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace),
                    ApplicationConfig.BREAKIN_LOGS);
            }
            return breakInClearInspectionDetails;
        }

        public async Task<RetryResyncCase> GetTransactionDetailsByBreakInId(string breakinId, string traceId)
        {
            RetryResyncCase transactionDetail = null;
            string query = DatabaseConfig.GET_TRANSACTION_DETAILS_BY_BREAKIN_ID;
            var parameters = new { BREAKINID = breakinId };
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var TransactionData = await connection.QueryAsync(query, parameters);
                    if (TransactionData != null)
                    {
                        var firstRow = TransactionData.First();
                        transactionDetail = new RetryResyncCase
                        {
                            transaction_id = firstRow.transaction_id,
                            trace_id = firstRow.trace_id,
                            transaction_initiation_time = Convert.ToString(firstRow.created_on),
                            core = firstRow.core,
                            total_amount = firstRow.total_amount,
                            payer_type = firstRow.payer_type,
                            pf_mid = firstRow.pf_mid,
                            artemis_mid = firstRow.artemis_mid,
                            auth_code = firstRow.auth_code,
                            payment_id = firstRow.payment_id,
                            no_of_attempts = firstRow.no_of_attempts,
                            reverse_sync_url = firstRow.reverse_sync_url,
                            proposal_number = firstRow.proposal_number,
                            proposal_status = firstRow.proposal_status,
                            policy_number = firstRow.policy_number,
                            deal_id = firstRow.deal_id,
                            customer_id = firstRow.customer_id,
                            intermediary_code = firstRow.intermediary_code,
                            channel_application_details = firstRow.channel_application_details
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "BreakInDAL.cs - GetTransactionDetailsByBreakInId()",
                        Message: $"Exception occurred: {ex.Message}",
                        TraceId: traceId,
                        StackTrace: ex.StackTrace),
                    ApplicationConfig.BREAKIN_LOGS);
            }
            return transactionDetail;
        }

        public async Task<ILESBBreakInStatusReq> GetBreakInDetailsById(string breakInId)
        {
            ILESBBreakInStatusReq ilesbBreakInStatusReq = null;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_BREAKIN_DETAILS_BY_ID;
                    var dbRecord = await connection.QueryFirstOrDefaultAsync(query, new { BREAKIN_ID = breakInId });
                    if (dbRecord != null)
                    {
                        ilesbBreakInStatusReq = new ILESBBreakInStatusReq
                        {
                            CorrelationId = dbRecord.trace_id,
                            BreakinId = breakInId,
                            RegistrationNo = dbRecord.vrn
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInDAL.cs - GetBreakInByIdAsync()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }
            return ilesbBreakInStatusReq;
        }

        public async Task<DPSStatusResponse> FetchPiSublocation(string piInputSublocations)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PiSublocationResp piSublocationResp = new PiSublocationResp();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.FETCH_NYSA_BREAKIN_PI_SUBLOCATION;

                    var result = await connection.QueryAsync<string>(query, new { SUBLOCATION = "%" + piInputSublocations + "%" });
                    piSublocationResp.PiSublocations = result.ToList();

                    if (piSublocationResp.PiSublocations != null && piSublocationResp.PiSublocations.Count > 0)
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = piSublocationResp.PiSublocations.Count + " Record(s) Found";
                        response.TraceId = null;
                        response.Data = piSublocationResp;
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Data not Found";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInDAL.cs - FetchPiSublocation() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task UpdateBreakInClearInspectionStatus(string breakInId, string clear_inspection_status)
        {
            string query = DatabaseConfig.UPDATE_BREAKIN_CLEAR_INSPECTION_STATUS;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    await connection.ExecuteAsync(query, new { BREAKIN_CLEAR_INSPECTION_STATUS = clear_inspection_status, BREAKINID = breakInId });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    TraceId: null,
                    MethodName: "BreakInDAL.cs - UpdateBreakInClearInspectionStatus()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }
        }

        public async Task<List<breakInProposals>> GetBreakInProposalsByTransactionID(string transaction_id, string trace_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_BREAKIN_PROPOSALS_BY_TRANSACTION_ID;
                    var result = await connection.QueryAsync<breakInProposals>(query, new { TRANSACTION_ID = transaction_id });
                    return result.ToList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetBreakInProposalsByTransactionID() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }

            return null;
        }

        public async Task<FetchRefundCase> GetBreakinRefundDetails(string proposal_number)
        {
            FetchRefundCase fetchRefundCase = new FetchRefundCase();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_BREAKIN_REFUND_DETAILS;
                    var parameters = new { PROPOSAL_NUMBER = proposal_number };

                    var result = await connection.QueryFirstOrDefaultAsync(query, parameters);

                    if (result != null)
                    {
                        fetchRefundCase = new FetchRefundCase
                        {
                            proposal_number = proposal_number,
                            auth_code = result.auth_code,
                            policy_number = result.policy_number,
                            proposal_amount = result.proposal_amount,
                            transaction_id = result.transaction_id,
                            trace_id = result.trace_id,
                            core = result.core,
                            dps_order_id = result.dps_order_id,
                            refund_status = result.refund_status,
                            voucher_number = result.voucher_number,
                            channel_application_details = result.channel_application_details,
                            product = result.product,
                            application_name = result.application_name,
                            payment_id = result.payment_id,
                            proposal_start_date = result.proposal_start_date,
                            payer_type = result.payer_type,
                            customer_id = result.customer_id,
                            refund_id = result.refund_id
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInDAL.cs - GetBreakinRefundDetails() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }

            return fetchRefundCase;
        }
    }
}


