using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Npgsql;
using Org.BouncyCastle.Asn1.Ocsp;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Transactions;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static Hangfire.Storage.JobStorageFeatures;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class OrderDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<string> GetLoaderReturnURL(int template_id)
        {
            string return_loader_url = string.Empty;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_TEMPLATE_LOADER_RETURN_URL;
                    return_loader_url = await connection.ExecuteScalarAsync<string>(query, new { template_id });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetReturnURL()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return return_loader_url;
        }

        public async Task<string> GetJuspayMerchantID(string application_name)
        {
            string juspayMerchantID = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_JUSPAY_MERCHANT_ID;
                    juspayMerchantID = await connection.ExecuteScalarAsync<string>(query, new { application_name });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetJuspayMerchantID()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return juspayMerchantID;
        }

        public async Task<string> GetNextDPSOrderSequence()
        {
            string dpsOrderStr = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_DPS_ORDER_SEQ;
                    long dpsOrderId = await connection.ExecuteScalarAsync<long>(query);

                    dpsOrderStr = "DPS_ORDER_" + dpsOrderId.ToString();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetNextDPSOrderSequence() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return dpsOrderStr;
        }

        public async Task<bool> InsertJuspayOrderDetails(string transaction_id, string trace_id, string amount, string order_id, string payment_mode, string dps_order_id, string? gateway_customer_id)
        {
            bool is_saved = false;

            try
            {
                using var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN);
                string insertJuspayOrder = DatabaseConfig.INSERT_ORDER_DETAILS;
                var parameters = new
                {
                    TRANSACTION_ID = transaction_id,
                    TRACE_ID = trace_id,
                    AMOUNT = amount,
                    ORDER_ID = order_id,
                    PAYMENT_MODE = payment_mode,
                    DPS_ORDER_ID = dps_order_id,
                    GATEWAY_CUSTOMER_ID = gateway_customer_id
                };

                int count = await connection.ExecuteAsync(insertJuspayOrder, parameters);
                if (count > 0)
                {
                    is_saved = true;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - SaveJuspayOrderAsync() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
            return is_saved;
        }

        public async Task<List<FetchOrderAndProposalDetails>> GetOrderAndProposalDetails(string transaction_id, string dps_order_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_ORDER_AND_PROPOSAL_DETAILS;
                    var fetchOrderAndProposalDetails = await connection.QueryAsync<FetchOrderAndProposalDetails>(query, new { TRANSACTION_ID = transaction_id, DPS_ORDER_ID = dps_order_id });
                    return fetchOrderAndProposalDetails.ToList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetOrderAndProposalDetails() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return null;
        }

        public async Task<string> GetNextIASSequence()
        {
            string iasIdStr = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_IAS_ID_SEQ;
                    long iasId = await connection.ExecuteScalarAsync<long>(query);

                    iasIdStr = "IAS_" + iasId.ToString();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetNextIASSequence()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return iasIdStr;
        }

        public async Task UpdateIASID(string iasId, string proposal_number)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_IAS_ID;

                    var parameters = new
                    {
                        IAS_ID = iasId,
                        PROPOSAL_NUMBER = proposal_number
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - UpdateIASID() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
        }

        public async Task<string> GetStatusReturnURL(int template_id)
        {
            string url = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_TEMPLATE_RETURN_STATUS_URL;
                    url = await connection.QueryFirstOrDefaultAsync<string>(query, new { template_id });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetStatusReturnURL()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return url;
        }

        public async Task<List<FetchPolicyDetailsByIDRespDetails>> GetPolicyDetailsByTransactionID(string transaction_id, string trace_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_POLICYDETAILS_BY_TRANSACTIONID;
                    var result = await connection.QueryAsync<FetchPolicyDetailsByIDRespDetails>(query, new { TRANSACTION_ID = transaction_id });
                    return result.ToList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetPolicyDetailsByTransactionID() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return null;
        }

        public async Task UpdateIsBreakInReq(string proposal_number)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_IS_BREAKIN_REQ;

                    var parameters = new
                    {
                        PROPOSAL_NUMBER = proposal_number
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - UpdateIASID() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
        }

        public async Task UpdateDpsOrderStatus(string dps_order_status, string transaction_id, string dps_order_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_DPS_ORDER_STATUS_FOR_TRANSACTION_ID;

                    var parameters = new
                    {
                        DPS_ORDER_STATUS = dps_order_status,
                        TRANSACTION_ID = transaction_id,
                        DPS_ORDER_ID = dps_order_id
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - UpdateDpsOrderStatus() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
        }

        public async Task<GetDpsOrderStatusAndIsMobileUserResp> GetDpsOrderStatusAndIsMobileUser(string transaction_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_DPS_ORDER_STATUS_AND_IS_MOBILE_USER_FROM_TRANSACTION_ID;

                    var result = await connection.QueryFirstOrDefaultAsync(
                        query,
                        new { TRANSACTION_ID = transaction_id }
                    );

                    if (result == null)
                    {
                        return new GetDpsOrderStatusAndIsMobileUserResp
                        {
                            dps_juspay_status = string.Empty,
                            is_mobile_user = false,
                            email = string.Empty
                        };
                    }

                    return new GetDpsOrderStatusAndIsMobileUserResp
                    {
                        dps_juspay_status = result.order_status ?? string.Empty,
                        is_mobile_user = result.is_mobile_user ?? false,
                        email = result.email ?? string.Empty
                    };
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "OrderDAL.cs - GetDpsOrderStatusAndIsMobileUser()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace
                    ),
                    ApplicationConfig.ORDER_LOGS
                );

                return new GetDpsOrderStatusAndIsMobileUserResp
                {
                    dps_juspay_status = string.Empty,
                    is_mobile_user = false
                };
            }
        }


        public async Task<FetchTxnDetails> GetJourneyTypeAndAmount(string transaction_id)
        {
            FetchTxnDetails fetchTxnDetails = new FetchTxnDetails();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_JOURNEYTYPE_AMOUNT_BY_TRANSACTIONID;
                    var TxnDetail = await connection.QueryFirstOrDefaultAsync(query, new { TRANSACTION_ID = transaction_id });
                    if (TxnDetail != null)
                    {
                        fetchTxnDetails.journey_type = TxnDetail.journey_type;
                        fetchTxnDetails.amount = TxnDetail.amount;
                        fetchTxnDetails.proposal_tenure = TxnDetail.proposal_tenure;
                        fetchTxnDetails.email = TxnDetail.email;
                        fetchTxnDetails.mobile_number = TxnDetail.mobile_number;
                        fetchTxnDetails.name = TxnDetail.name;
                        fetchTxnDetails.installment_amount = TxnDetail.installment_amount;
                        fetchTxnDetails.flightTracking = TxnDetail.flighttracking;
                        fetchTxnDetails.flightTrackingURL = TxnDetail.flighttrackingurl;
                        fetchTxnDetails.mandate_max_amount = TxnDetail.mandate_max_amount;
                        if (TxnDetail.payment_filters != null)
                        {
                            fetchTxnDetails.payment_filters = JsonConvert.DeserializeObject<PaymentFilters>(TxnDetail.payment_filters);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetJourneyTypeAndAmount()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return fetchTxnDetails;
        }

        public async Task<GatewayParams> GetGatewayParams(string transaction_id)
        {
            GatewayParams gatewayParams = new GatewayParams();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_GATEWAY_PARAMS_BY_TRANSACTIONID;
                    gatewayParams = await connection.QueryFirstOrDefaultAsync<GatewayParams>(query, new { TRANSACTION_ID = transaction_id });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetGatewayParams()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return gatewayParams;
        }

        public async Task<bool> IS_PAYMENT_DONE(string transactionId)
        {
            bool isCompleted = false;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    await connection.OpenAsync();

                    string query = DatabaseConfig.IS_PAYMENT_DONE;

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TRANSACTION_ID", transactionId);

                        var result = await command.ExecuteScalarAsync();
                        isCompleted = Convert.ToInt32(result) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - IS_PAYMENT_DONE()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return isCompleted;
        }

        public async Task<CaptureMandateReq> GetCaptureMandateReq(string transaction_id)
        {
            CaptureMandateReq captureMandateReq = new CaptureMandateReq();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_ILEMI_CAPTURE_MANDATE_DETAILS;
                    captureMandateReq = await connection.QueryFirstOrDefaultAsync<CaptureMandateReq>(query, new { TRANSACTION_ID = transaction_id });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetCaptureMandateReq()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return captureMandateReq;
        }

        public async Task<LatestEmiInstallment> GetLatestEmiInstallment(string proposal_number)
        {
            LatestEmiInstallment latestEmiInstallment = new LatestEmiInstallment();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_LATEST_EMI_INSTALLMENT;
                    latestEmiInstallment = await connection.QueryFirstOrDefaultAsync<LatestEmiInstallment>(query, new { PROPOSAL_NUMBER = proposal_number });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetLatestEmiInstallment()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
            return latestEmiInstallment;
        }

        public async Task<LatestEmiInstallment> GetRecurringInstallmentDetails(string recurring_id)
        {
            LatestEmiInstallment latestEmiInstallment = new LatestEmiInstallment();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_RECURRING_INSTALLMENT_DETAILS;
                    latestEmiInstallment = await connection.QueryFirstOrDefaultAsync<LatestEmiInstallment>(query, new { RECURRING_ID = recurring_id });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetLatestEmiInstallment()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
            return latestEmiInstallment;
        }

        public async Task<bool> upsertEmiInstallmentDetails(LatestEmiInstallment latestEmiInstallment)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPSERT_EMI_INSTALLMENT_DETAILS;

                    var parameters = new
                    {
                        RECURRING_ID = latestEmiInstallment.recurring_id,
                        TRANSACTION_ID = latestEmiInstallment.transaction_id,
                        TRACE_ID = latestEmiInstallment.trace_id,
                        PROPOSAL_NUMBER = latestEmiInstallment.proposal_number,
                        AMOUNT = latestEmiInstallment.amount,
                        INSTALLMENT_NUMBER = latestEmiInstallment.installment_number,
                        STATUS = latestEmiInstallment.status,
                        PAYMENT_ID = latestEmiInstallment.payment_id,
                        POLICY_NUMBER = latestEmiInstallment.policy_number,
                        MANDATE_ID = latestEmiInstallment.mandate_id,
                        DPS_ORDER_ID = latestEmiInstallment.dps_order_id,
                        ORDER_ID = latestEmiInstallment.order_id,
                        PAYMENT_MODE = latestEmiInstallment.payment_mode,
                        DUE_DATE = latestEmiInstallment.due_date,
                        EXECUTION_DATE = latestEmiInstallment.execution_date,
                        GRACE_END_DATE = latestEmiInstallment.grace_end_date,
                        REMARKS = latestEmiInstallment.remarks,
                        NO_OF_ATTEMPT = latestEmiInstallment.no_of_attempt,
                        CUSTOMER_ID = latestEmiInstallment.customer_id,
                        APPLICATION_NAME = latestEmiInstallment.application_name
                    };

                    int result = await connection.ExecuteAsync(query, parameters);
                    if (result > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - UpdateDpsOrderStatus() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
                return false;
            }
        }

        public async Task<string> GetNextRecurringId()
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string getRecurringIdSeq = DatabaseConfig.GET_RECURRING_ID_SEQ;
                    long recurring_id = await connection.ExecuteScalarAsync<long>(getRecurringIdSeq);
                    return ("DPS_REC_" + recurring_id.ToString());
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetNextRecurringId()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
                return null;
            }

        }

        public async Task<string> GetCustomerDetails(string mobile_number)
        {
            string gateway_customer_id = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_GATEWAY_CUSTOMER_ID_BY_MOBILE_NUMBER;
                    var custDetail = await connection.QueryFirstOrDefaultAsync(query, new { MOBILE_NUMBER = mobile_number });
                    if (custDetail != null && custDetail.gateway_customer_id != null)
                    {
                        gateway_customer_id = custDetail.gateway_customer_id;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - GetCustomerDetails()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return gateway_customer_id;
        }

        public async Task InsertGatewayCustomerDetails(string mobile_number, string gateway_customer_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.INSERT_GATEWAY_CUSTOMER_ID;
                    var parameters = new { MOBILE_NUMBER = mobile_number, GATEWAY_CUSTOMER_ID = gateway_customer_id };
                    int count = await connection.ExecuteAsync(query, parameters);

                    if (count == 0)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderDAL.cs - InsertGatewayCustomerDetails() ",
                            Message: "Failed to Insert Juspay Customer Details. Mobile_Number : " + mobile_number + " GATEWAY_CUSTOMER_ID : " + gateway_customer_id), ApplicationConfig.PAYMENT_LOGS);
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - InsertGatewayCustomerDetails()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
        }

        public async Task IncrementNoOfRecurringAttempt(string recurring_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.INCREMENT_NO_OF_RECURRING_ATTEMPT;
                    var parameters = new { RECURRING_ID = recurring_id };
                    int count = await connection.ExecuteAsync(query, parameters);
                    if (count == 0)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderDAL.cs - IncrementNoOfRecurringAttempt() ",
                            Message: "Failed to Increment No Of Recurring Attempt. RECURRING_ID : " + recurring_id), ApplicationConfig.PAYMENT_LOGS);
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - IncrementNoOfRecurringAttempt()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }
        }

        public async Task<bool> UpdatePaymentOrderStatus(string dps_order_status, string transaction_id, string dps_order_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_ORDER_STATUS_FOR_ORDER_ID;

                    var parameters = new
                    {
                        ORDER_STATUS = dps_order_status,
                        TRANSACTION_ID = transaction_id,
                        DPS_ORDER_ID = dps_order_id
                    };

                    int count = await connection.ExecuteAsync(query, parameters);
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderDAL.cs - UpdatePaymentOrderStatus() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return false;
        }
    }
}
