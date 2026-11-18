using Dapper;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Hangfire.Storage.Monitoring;
using Newtonsoft.Json;
using Npgsql;
using RestSharp;
using static DigitalPaymentSystem_ILTC.Models.DashboardModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;
using static Hangfire.Storage.JobStorageFeatures;
using MotorAdditionalInfo = DigitalPaymentSystem_ILTC.Models.MotorAdditionalInfo;


namespace DigitalPaymentSystem_ILTC.DAL
{
    public class CallbackDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<FetchPaymentDetails> LogPaymentDetails(string order_id, string auth_code, string transaction_id, string trace_id, string dps_order_id, string status, string mandate_id, string payment_mode)
        {
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();
            OrderDAL orderDAL = new OrderDAL();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_PAYMENT_DETAILS;
                    var parameters = new { ORDER_ID = order_id };
                    var paymentDataList = await connection.QueryAsync(query, parameters);

                    if (paymentDataList != null && paymentDataList.Count() > 0)
                    {
                        var firstRow = paymentDataList.First();
                        fetchPaymentDetails = new FetchPaymentDetails
                        {
                            transaction_id = firstRow.transaction_id,
                            trace_id = firstRow.trace_id,
                            created_on = Convert.ToString(DateTime.Now),
                            core = firstRow.core,
                            total_amount = firstRow.total_amount,
                            payer_type = firstRow.payer_type,
                            pf_mid = firstRow.pf_mid,
                            artemis_mid = firstRow.artemis_mid,
                            reverse_sync_url = firstRow.reverse_sync_url,
                            payment_mode = !string.IsNullOrEmpty(firstRow.payment_mode) ? firstRow.payment_mode : payment_mode,
                            product = firstRow.product,
                            journey_type = firstRow.journey_type,
                            application_name = firstRow.application_name,
                            channel_application_details = firstRow.channel_application_details,
                            installment_number = firstRow.installment_number,
                            installment_amount = firstRow.installment_amount,
                            gateway_customer_id = firstRow.gateway_customer_id,
                            proposal_id = firstRow.proposal_id,
                            is_ilemi_proposal_mod_reqd = firstRow.is_ilemi_proposal_mod_reqd
                        };

                        List<FetchPaymentProposalList> fetchPaymentProposalLists = new List<FetchPaymentProposalList>();
                        foreach (var row in paymentDataList)
                        {
                            fetchPaymentProposalLists.Add(new FetchPaymentProposalList
                            {
                                proposal_number = row.proposal_number,
                                proposal_amount = row.proposal_amount,
                                proposal_type = row.proposal_type,
                                core = row.core,
                                proposal_status = row.proposal_status,
                                deal_id = row.deal_id,
                                customer_id = row.customer_id,
                                intermediary_code = row.intermediary_code,
                                is_nstp_flow = row.is_nstp_flow,
                                business_vertical = row.business_vertical,
                                sub_vertical = row.sub_vertical,
                                marketing_officer = row.marketing_officer,
                                proposer_code = row.proposer_code
                            });
                        }
                        fetchPaymentDetails.fetchPaymentProposalList = fetchPaymentProposalLists;


                        string checkQuery = DatabaseConfig.CHECK_TRANSACTION_EXISTS_IN_PAYMENT;
                        var checkParams = new
                        {
                            TRANSACTION_ID = transaction_id
                        };
                        TransactionDetails transactionDetails = await connection.QueryFirstOrDefaultAsync<TransactionDetails>(checkQuery, checkParams);

                        if (transactionDetails != null)
                        {
                            if (string.IsNullOrEmpty(transactionDetails.auth_code))
                            {
                                foreach (var paymentDetails in paymentDataList)
                                {
                                    string updateQuery = DatabaseConfig.UPDATE_PAYMENT_DETAILS;
                                    var updateParameters = new
                                    {
                                        AUTH_CODE = auth_code,
                                        ORDER_STATUS = status,
                                        TRANSACTION_ID = transaction_id
                                    };
                                    await connection.ExecuteAsync(updateQuery, updateParameters);
                                }
                            }

                            if (string.IsNullOrEmpty(transactionDetails.mandate_id))
                            {
                                foreach (var paymentDetails in paymentDataList)
                                {
                                    string updateQuery = DatabaseConfig.UPDATE_MANDATE_PAYMENT_DETAILS;
                                    var updateParameters = new
                                    {
                                        ORDER_STATUS = status,
                                        TRANSACTION_ID = transaction_id,
                                        MANDATE_ID = mandate_id
                                    };
                                    await connection.ExecuteAsync(updateQuery, updateParameters);
                                }
                            }
                        }
                        else
                        {
                            foreach (var paymentDetails in paymentDataList)
                            {
                                string insertQuery = DatabaseConfig.INSERT_PAYMENT_DETAILS;
                                var insertParameters = new
                                {
                                    PROPOSAL_NUMBER = paymentDetails.proposal_number,
                                    PROPOSAL_AMOUNT = paymentDetails.proposal_amount,
                                    PROPOSAL_TYPE = paymentDetails.proposal_type,
                                    CORE = paymentDetails.core,
                                    TRACE_ID = trace_id,
                                    TRANSACTION_ID = transaction_id,
                                    AMOUNT = paymentDetails.total_amount,
                                    ORDER_ID = order_id,
                                    AUTH_CODE = auth_code,
                                    PAYMENT_MODE = payment_mode,
                                    DPS_ORDER_ID = dps_order_id,
                                    ORDER_STATUS = status,
                                    MANDATE_ID = mandate_id
                                };
                                await connection.ExecuteAsync(insertQuery, insertParameters);
                            }
                        }
                    }
                }
                await orderDAL.UpdateDpsOrderStatus(status, transaction_id, dps_order_id);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackDAL.cs - LogPaymentDetails()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return fetchPaymentDetails;
        }

        public async Task updatePaymentEntryTaggingDetails(string proposal_number, string policy_number)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string updateQuery = DatabaseConfig.UPDATE_CORE_POLICY_NUMBER;

                    var updateParameters = new
                    {
                        POLICY_NUMBER = policy_number,
                        PROPOSAL_NUMBER = proposal_number
                    };
                    await connection.ExecuteAsync(updateQuery, updateParameters);
                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - updatePaymentEntryTaggingDetails()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                }
            }
        }

        public async Task UpdateReverseSyncFlag(SyncProposalToSource syncProposalToSource)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string updateToSource = DatabaseConfig.UPDATE_RESYNC_STATUS;
                    var parameters = new { PROPOSAL_NUMBER = syncProposalToSource.proposal_number };
                    await connection.ExecuteAsync(updateToSource, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "CallbackDAL.cs - UpdateReverseSyncFlag()",
                       TraceId: syncProposalToSource.trace_id,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
        }

        public async Task<List<FetchWebhookFallbackCases>> GetWebhookFallbackRetryCases()
        {
            List<FetchWebhookFallbackCases> webhookFallbackCasesList = new List<FetchWebhookFallbackCases>();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var result = await connection.QueryAsync<FetchWebhookFallbackCases>(DatabaseConfig.GET_WEBHOOK_FALLBACK_RETRY_CASES);
                    webhookFallbackCasesList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackDAL.cs - GetWebhookFallbackRetryCases()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
            return webhookFallbackCasesList;
        }

        public async Task UpdateWebhookFallbackAttemptCount(int webhook_fallback_attempt_count, string dps_order_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_FALLBACKATTEMPT_COUNT;
                    var parameters = new { WEBHOOK_FALLBACK_ATTEMPT_COUNT = webhook_fallback_attempt_count, DPS_ORDER_ID = dps_order_id };
                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "CallbackDAL.cs - UpdateWebhookFallbackAttemptCount()",
                       TraceId: null,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
        }

        public async Task UpdateNoOfAttemptsCountByTransactionID(string transaction_id)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string query = DatabaseConfig.UPDATE_NO_OF_ATTEMPTS_FOR_TRANSACTION_ID;
                    var updateParameters = new
                    {
                        TRANSACTION_ID = transaction_id
                    };
                    await connection.ExecuteAsync(query, updateParameters);

                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - UpdateNoOfAttemptsCountByTransactionID()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                }
            }
        }

        public async Task UpdateCorePaymentID(string payment_id, string transaction_id)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string updateQuery = DatabaseConfig.UPDATE_CORE_PAYMENT_ID;

                    var updateParameters = new
                    {
                        PAYMENT_ID = payment_id,
                        TRANSACTION_ID = transaction_id
                    };
                    await connection.ExecuteAsync(updateQuery, updateParameters);
                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - UpdateCorePaymentID()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                }
            }
        }

        public async Task<string> GetIsWebhookFallbackProcessingFlag(string dps_order_id)
        {
            string isWebhookeFallbackProcessing = string.Empty;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_IS_WEBHOOK_FALLBACK_PROCESSING;
                    var queryParams = new { DPS_ORDER_ID = dps_order_id };
                    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(query, queryParams);

                    if (result != null)
                    {
                        isWebhookeFallbackProcessing = result.is_webhook_fallback_processing;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackDAL.cs - GetIsWebhookFallbackProcessingFlag()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
            return isWebhookeFallbackProcessing;
        }

        public async Task<bool> SetIsWebhookFallbackProcessingFlag(string isWebhookeFallbackProcessing, string dps_order_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.SET_IS_WEBHOOK_FALLBACK_PROCESSING;
                    var queryParams = new { IS_WEBHOOK_FALLBACK_PROCESSING = isWebhookeFallbackProcessing, DPS_ORDER_ID = dps_order_id };
                    var result = await connection.ExecuteAsync(query, queryParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackDAL.cs - SetIsWebhookFallbackProcessingFlag()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);

                return false;
            }
        } 

        public async Task<ChannelApplicationOrderDetailsResp> ChannelApplicationOrderDetails(ChannelApplicationOrderDetailsReq channelApplicationOrderDetailsReq)
        {
            List<ChannelApplicationOrderDetailsFlatRow> channelApplicationOrderDetailsFlatRowList = new List<ChannelApplicationOrderDetailsFlatRow>();
            ChannelApplicationOrderDetailsResp channelApplicationOrderDetailsResp = new ChannelApplicationOrderDetailsResp();
            string encryptedId = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_CHANNEL_APPLICATION_ORDER_DETAILS;

                    var queryParams = new { TRANSACTION_ID = channelApplicationOrderDetailsReq.transaction_id };
                    var result = await connection.QueryAsync<ChannelApplicationOrderDetailsFlatRow>(query, queryParams);
                    channelApplicationOrderDetailsFlatRowList = result.AsList();

                    if (channelApplicationOrderDetailsFlatRowList.Count > 0)
                    {
                        var first = channelApplicationOrderDetailsFlatRowList[0];
                        string inputData = first.transaction_id + "|"+ first.trace_id;
                        encryptedId = new EncryptDecrypt().EncryptRequest(inputData);


                        channelApplicationOrderDetailsResp = new ChannelApplicationOrderDetailsResp
                        {
                            transaction_id = first.transaction_id,
                            trace_id = first.trace_id,
                            application_name = first.application_name,
                            dps_order_id = first.dps_order_id,
                            auth_code = first.auth_code,
                            total_amount = first.total_amount,
                            payment_mode = first.payment_mode,
                            channel_application_details = first.channel_application_details,
                            payment_id = first.payment_id,
                            ChannelApplicationProposalDetailsResp = channelApplicationOrderDetailsFlatRowList
                            .Select(x => new ChannelApplicationProposalDetailsResp
                            {
                                proposal_number = x.proposal_number,
                                policy_number = x.policy_number,
                                proposal_start_date = x.proposal_start_date,
                                proposal_end_date = x.proposal_end_date,
                                sum_insured = x.sum_insured,
                                download_policy_copy_url = ApplicationConfig.DOWNLOAD_POLICY_CERTIFICATE + $"?proposalNumber={x.proposal_number}&id={encryptedId}",
                                motor_additional_info = (x.product.Contains("MOTOR") ? new MotorAdditionalInfo
                                {
                                    breakin_id = x.breakin_id,
                                    breakin_status = x.breakin_status,
                                    breakin_remarks = x.breakin_remarks
                                } : null),
                                travel_additional_info = (x.product.Contains("TRAVEL") ? new ProposalTravelAdditionalInfo
                                {
                                    trip_type = x.trip_type,
                                    flight_tracking = x.flight_tracking,
                                    flight_tracking_url = x.flight_tracking_url,
                                    traveller_details = string.IsNullOrWhiteSpace(x.ped)
                                                    ? new List<ProposalTravellerDetails>()
                                                    : JsonConvert.DeserializeObject<List<ProposalTravellerDetails>>(x.ped)
                                } : null)
                            }).ToList(),
                            refunds = channelApplicationOrderDetailsFlatRowList
                                .Where(x => !string.IsNullOrEmpty(x.refund_status) && (x.refund_status == "SUCCESS" || x.refund_status == "PENDING" || x.refund_status == "FAILED" || x.refund_status == "MANUAL_REV"))
                                .Select(x => new ChannelApplicationRefundDetails
                                {
                                    proposal_number = x.proposal_number,
                                    refund_id = x.refund_id,
                                    refund_status = x.refund_status,
                                    refund_amount = x.refund_amount,
                                    core = x.core
                                }).ToList(),
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackDAL.cs - ChannelApplicationOrderDetails() ",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return channelApplicationOrderDetailsResp;
        }

        public async Task UpdateOrderDetailsInPayment(string auth_code, string status, string transaction_id)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string updateQuery = DatabaseConfig.UPDATE_PAYMENT_DETAILS;
                    var updateParameters = new
                    {
                        AUTH_CODE = auth_code,
                        ORDER_STATUS = status,
                        TRANSACTION_ID = transaction_id
                    };
                    await connection.ExecuteAsync(updateQuery, updateParameters);
                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - UpdateOrderDetailsInPayment()",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                }
            }
        }

        //public async Task updateILEMIPaymentEntryTaggingDetails(string proposal_number, string policy_number,string recurring_id)
        //{
        //    using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
        //    {
        //        try
        //        {
        //            string updateQuery = DatabaseConfig.UPDATE_ILEMI_SUBSEQUENT_TRANSACTION;

        //            var updateParameters = new
        //            {
        //                POLICY_NUMBER = policy_number,
        //                PROPOSAL_NUMBER = proposal_number,
        //                RECURRING_ID = recurring_id
        //            };
        //            await connection.ExecuteAsync(updateQuery, updateParameters);
        //        }
        //        catch (Exception ex)
        //        {
        //            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
        //                MethodName: "CallbackDAL.cs - updatePaymentEntryTaggingDetails()",
        //                TraceId: null,
        //                Message: $"Exception occurred: {ex.Message}",
        //                StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
        //        }
        //    }
        //}

        public async Task UpdateILEMIProposalModifyStatus(bool isModified, string trace_id, string proposal_id, string transaction_id)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                try
                {
                    string updateQuery = DatabaseConfig.UPDATE_ILEMI_PROPOSAL_MODIFIED_STATUS;
                    var updateParameters = new
                    {
                        IS_ILEMI_PROPOSAL_MODIFIED = isModified,
                        PROPOSAL_ID = proposal_id,
                        TRANSACTION_ID = transaction_id
                    };
                    await connection.ExecuteAsync(updateQuery, updateParameters);
                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackDAL.cs - UpdateILEMIProposalModifyStatus()",
                        TraceId: trace_id,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                }
            }
        }
    }
}