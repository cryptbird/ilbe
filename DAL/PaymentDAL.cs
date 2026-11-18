using Amazon.CloudWatchLogs;
using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Pipelines.Sockets.Unofficial.Arenas;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static Hangfire.Storage.JobStorageFeatures;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class PaymentDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<bool> CheckValidTemplate(string username, AddPaymentReq addPaymentReq, string traceId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string checkValidTemplate = DatabaseConfig.CHECK_VALID_TEMPLATE;

                    var parameters = new
                    {
                        TEMPLATE_ID = addPaymentReq.TemplateID,
                        APPLICATION_NAME = username,
                        PRODUCT = addPaymentReq.Product
                    };

                    int is_active = await connection.ExecuteScalarAsync<int>(checkValidTemplate, parameters);
                    if (is_active > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - CheckValidTemplate() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
            return false;
        }

        public async Task<AddPaymentResp> LogDbTransaction(string username, AddPaymentReq addPaymentReq, string traceId, decimal totalAmount, string rm_text)
        {
            int totalRowsInserted = 0;
            int expectedInsertCount = 0;
            string transactionIdStr = string.Empty;
            string channel_application_details = string.Empty;

            try
            {
                if (addPaymentReq.channel_application_details.ValueKind == JsonValueKind.Object)
                {
                    channel_application_details =
                        System.Text.Json.JsonSerializer.Serialize(addPaymentReq.channel_application_details);
                }
                else if (addPaymentReq.channel_application_details.ValueKind != JsonValueKind.Undefined &&
                         addPaymentReq.channel_application_details.ValueKind != JsonValueKind.Null)
                {
                    // ❌ Present but NOT an object → invalid format
                    objLog.LogAWSMessageAsync(
                        objLog.GenericAWSException_CWL(
                            MethodName: "PaymentDAL.cs - LogDbTransaction()",
                            Message: "Channel Application Details format is invalid. Request: " +
                                     JsonConvert.SerializeObject(addPaymentReq),
                            TraceId: traceId),
                        ApplicationConfig.PAYMENT_LOGS
                    );
                }


                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    if (addPaymentReq.JourneyType != "ADVANCE")
                    {
                        string getPGname = DatabaseConfig.GET_ACTIVE_PG_NAME;
                        var getPgNameParameter = new { APPLICATION_NAME = username };
                        string pgName = await connection.ExecuteScalarAsync<string>(getPGname, getPgNameParameter);
                        pgName = !string.IsNullOrEmpty(pgName) ? pgName : "JUSPAY";

                        string getTransactionIdSeq = DatabaseConfig.GET_TRANSACTION_ID_SEQ;
                        long transactionId = await connection.ExecuteScalarAsync<long>(getTransactionIdSeq);
                        transactionIdStr = "PG_TXN_" + transactionId.ToString();
                        string insertTransactionDetails = DatabaseConfig.INSERT_TRANSACTION_DETAILS;

                        expectedInsertCount = addPaymentReq.ProposalList.Count;

                        foreach (ProposalList proposal in addPaymentReq.ProposalList)
                        {
                            var parameters = new
                            {
                                TRANSACTION_ID = transactionIdStr,
                                APPLICATION_NAME = username,
                                TRACE_ID = traceId,
                                TOTAL_AMOUNT = totalAmount.ToString("F2"),
                                PROPOSAL_NUMBER = proposal.ProposalNumber,
                                PROPOSAL_AMOUNT = proposal.ProposalAmount,
                                PROPOSAL_STATUS = proposal.ProposalStatus,
                                PROPOSAL_START_DATE = addPaymentReq.ProposalStartDate,
                                DEAL_ID = proposal.DealId,
                                CUSTOMER_ID = proposal.CustomerId,
                                INTERMEDIARY_CODE = proposal.IntermediaryCode,
                                NAME = addPaymentReq.Name,
                                EMAIL = addPaymentReq.Email,
                                MOBILE_NUMBER = addPaymentReq.MobileNumber,
                                PRODUCT = addPaymentReq.Product,
                                JOURNEY_TYPE = addPaymentReq.JourneyType.ToUpper(),
                                CORE = addPaymentReq.Core,
                                TEMPLATE_ID = addPaymentReq.TemplateID,
                                PAYER_TYPE = addPaymentReq.PayerType,
                                SEND_PAYMENT_LINK = addPaymentReq.SendPaymentLink,
                                USER_ID = proposal.UserID,
                                IS_NSTP_FLOW = proposal.is_nstp_flow,
                                BUSINESS_VERTICAL = proposal.business_vertical,
                                SUB_VERTICAL = proposal.sub_vertical,
                                MARKETING_OFFICER = proposal.marketing_officer,
                                PROPOSER_CODE = proposal.proposer_code,
                                IS_MOBILE_USER = addPaymentReq.is_mobile_user,
                                CHANNEL_APPLICATION_DETAILS = channel_application_details,
                                JUSPAY_UDF1 = addPaymentReq.gateway_params?.juspay_udf1,
                                JUSPAY_UDF2 = addPaymentReq.gateway_params?.juspay_udf2,
                                JUSPAY_UDF3 = addPaymentReq.gateway_params?.juspay_udf3,
                                JUSPAY_UDF4 = addPaymentReq.gateway_params?.juspay_udf4,
                                JUSPAY_UDF5 = addPaymentReq.gateway_params?.juspay_udf5,
                                JUSPAY_UDF6 = addPaymentReq.gateway_params?.juspay_udf6,
                                JUSPAY_UDF7 = addPaymentReq.gateway_params?.juspay_udf7,
                                JUSPAY_UDF8 = addPaymentReq.gateway_params?.juspay_udf8,
                                JUSPAY_UDF9 = addPaymentReq.gateway_params?.juspay_udf9,
                                JUSPAY_UDF10 = addPaymentReq.gateway_params?.juspay_udf10,
                                RM_TEXT = rm_text,
                                PG_NAME = pgName
                            };

                            int rows = await connection.ExecuteAsync(insertTransactionDetails, parameters);
                            totalRowsInserted += rows;
                        }

                        if (totalRowsInserted == expectedInsertCount)
                        {
                            AddPaymentResp addPaymentResp = new AddPaymentResp
                            {
                                URL = null,
                                TransactionID = transactionIdStr,
                                pg_name = pgName
                            };
                            return addPaymentResp;
                        }
                    }
                    else
                    {
                        string getPGname = DatabaseConfig.GET_ACTIVE_PG_NAME;
                        var getPgNameParameter = new { APPLICATION_NAME = username };
                        string pgName = await connection.ExecuteScalarAsync<string>(getPGname, getPgNameParameter);
                        pgName = !string.IsNullOrEmpty(pgName) ? pgName : "JUSPAY";

                        string getTransactionIdSeq = DatabaseConfig.GET_TRANSACTION_ID_SEQ;
                        long transactionId = await connection.ExecuteScalarAsync<long>(getTransactionIdSeq);
                        transactionIdStr = "PG_TXN_" + transactionId.ToString();
                        string insertTransactionDetails = DatabaseConfig.INSERT_ADVANCE_TRANSACTION_DETAILS;

                        var parameters = new
                        {
                            TRANSACTION_ID = transactionIdStr,
                            APPLICATION_NAME = username,
                            TRACE_ID = traceId,
                            TOTAL_AMOUNT = totalAmount.ToString("F2"),
                            PROPOSAL_START_DATE = addPaymentReq.ProposalStartDate,
                            NAME = addPaymentReq.Name,
                            EMAIL = addPaymentReq.Email,
                            MOBILE_NUMBER = addPaymentReq.MobileNumber,
                            PRODUCT = addPaymentReq.Product,
                            JOURNEY_TYPE = addPaymentReq.JourneyType.ToUpper(),
                            CORE = addPaymentReq.Core,
                            TEMPLATE_ID = addPaymentReq.TemplateID,
                            PAYER_TYPE = addPaymentReq.PayerType,
                            SEND_PAYMENT_LINK = addPaymentReq.SendPaymentLink,
                            CHANNEL_APPLICATION_DETAILS = channel_application_details,
                            JUSPAY_UDF1 = addPaymentReq.gateway_params?.juspay_udf1,
                            JUSPAY_UDF2 = addPaymentReq.gateway_params?.juspay_udf2,
                            JUSPAY_UDF3 = addPaymentReq.gateway_params?.juspay_udf3,
                            JUSPAY_UDF4 = addPaymentReq.gateway_params?.juspay_udf4,
                            JUSPAY_UDF5 = addPaymentReq.gateway_params?.juspay_udf5,
                            JUSPAY_UDF6 = addPaymentReq.gateway_params?.juspay_udf6,
                            JUSPAY_UDF7 = addPaymentReq.gateway_params?.juspay_udf7,
                            JUSPAY_UDF8 = addPaymentReq.gateway_params?.juspay_udf8,
                            JUSPAY_UDF9 = addPaymentReq.gateway_params?.juspay_udf9,
                            JUSPAY_UDF10 = addPaymentReq.gateway_params?.juspay_udf10,
                            PG_NAME = pgName
                        };

                        int rows = await connection.ExecuteAsync(insertTransactionDetails, parameters);
                        if (rows == 1)
                        {
                            AddPaymentResp addPaymentResp = new AddPaymentResp
                            {
                                URL = null,
                                TransactionID = transactionIdStr,
                                pg_name = pgName
                            };
                            return addPaymentResp;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - LogDbTransaction() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
            return null;
        }

        public async Task updatePaymentLink(string traceId, string transactionId, string paymentLink)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string updatePaymentLink = DatabaseConfig.UPDATE_PAYMENT_LINK;

                    var parameters = new
                    {
                        TRANSACTION_ID = transactionId,
                        PAYMENT_URL = paymentLink
                    };

                    await connection.ExecuteAsync(updatePaymentLink, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - updatePaymentLink() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task LogIntegrationApi(string traceId, string proposalNumber, string request, string response, string apiUrl, string module, string status_code, string flow_type, string response_time)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string logIntegrationApiDetails = DatabaseConfig.LOG_INTEGRATION_API;

                    var parameters = new
                    {
                        TRACE_ID = traceId,
                        REQUEST = request,
                        RESPONSE = response,
                        API_URL = apiUrl,
                        MODULE = module,
                        PROPOSAL_NUMBER = proposalNumber,
                        STATUS_CODE = status_code,
                        FLOW_TYPE = flow_type,
                        RESPONSE_TIME = response_time
                    };

                    int count = await connection.ExecuteAsync(logIntegrationApiDetails, parameters);

                    if (count == 0)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(MethodName: "PaymentDAL.cs - LogIntegrationApi() ", Message: " Proposal Number : " + proposalNumber + " Request : " + request + " Response : " + response + " API URL : " + apiUrl + " Module : " + module, TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(MethodName: "PaymentDAL.cs - LogIntegrationApi() ", TraceId: traceId, Message: $"Exception occurred: {ex.Message}", StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<FetchPaymentResp> FetchPaymentTransaction(string transactionID, string traceID)
        {
            //JObject fetchTransactionData = new JObject();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            string application_id = string.Empty;
            //string rm_details = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var parameters = new
                    {
                        TRANSACTION_ID = transactionID,
                        TRACE_ID = traceID
                    };
                    // Extracting Proposal List details
                    var data = await connection.QueryAsync(DatabaseConfig.GET_TRANSACTION_DETAILS, parameters);

                    if (data != null && data.Count() > 0)
                    {
                        var firstRow = data.First();
                        FetchPaymentResp transactionData = new FetchPaymentResp
                        {
                            TransactionId = transactionID,
                            TraceId = traceID,
                            ApplicationName = firstRow.application_name,
                            Name = firstRow.name,
                            Email = firstRow.email,
                            MobileNumber = firstRow.mobile_number,
                            Amount = firstRow.total_amount,
                            Product = firstRow.product,
                            PayerType = firstRow.payer_type,
                            Core = firstRow.core,
                            TemplateID = firstRow.template_id == null ? 0 : firstRow.template_id,
                            ProposalStartDate = firstRow.proposal_start_date,
                            is_first_txn_visit = firstRow.is_first_txn_visit,
                            link_expiry_time = firstRow.link_expiry_time,
                            journey_type = firstRow.journey_type,
                            is_callback_reqd = firstRow.product == "SME_1" ? ApplicationConfig.SME_1_CALLBACK_REQD : "N",
                            rm_text = firstRow.rm_text,
                            payment_filters = firstRow.payment_filters,
                            vrn = firstRow.vrn,
                            pg_id = firstRow.dps_order_id,
                            address = firstRow.address,
                            state = firstRow.state,
                            city = firstRow.city,
                            pincode = firstRow.pincode,
                            basic_premium = firstRow.basic_premium,
                            gst = firstRow.gst,
                            created_on = firstRow.payment_created_on != null ? firstRow.payment_created_on : firstRow.created_on,
                            tripType = firstRow.trip_type,
                            platform = firstRow.platform,
                            installment_amount = firstRow.installment_amount,
                            installment_number = firstRow.installment_number,
                            isHeaderReq = firstRow.is_header_req,
                            isFooterReq = firstRow.is_footer_req,
                            isILEMI = !string.IsNullOrEmpty(firstRow.installment_number)
                        };
                        //application_id = firstRow.application_id;

                        //transactionData.authTokenData = new AuthenticateModel.AuthenticateToken
                        //{
                        //    accessToken = firstRow.authorization_token,
                        //    expiryTime = firstRow.link_expiry_time
                        //};

                        var proposalList = new List<ProposalList>();
                        foreach (var row in data)
                        {
                            proposalList.Add(new ProposalList
                            {
                                DealId = row.deal_id,
                                CustomerId = row.customer_id,
                                IntermediaryCode = row.intermediary_code,
                                ProposalNumber = row.proposal_number,
                                ProposalAmount = row.proposal_amount,
                                ProposalStatus = row.proposal_status,
                                is_nstp_flow = row.is_nstp_flow,
                                is_breakin_req = row.is_breakin_req
                            });
                        }
                        transactionData.ProposalList = proposalList;

                        if (transactionData.TemplateID != 0)
                        {
                            if (transactionData.journey_type == "IL_EMI")
                            {
                                transactionData.TemplateDetails = "120000";
                            }
                            else
                            {
                                var templateDetails = await connection.QueryAsync(DatabaseConfig.GET_TEMPLATE_DETAILS, new { TEMPLATE_ID = transactionData.TemplateID });
                                if (templateDetails != null && templateDetails.Count() > 0)
                                {
                                    var templateRow = templateDetails.First();
                                    int upi = templateRow.upi;
                                    int credit = templateRow.credit;
                                    int debit = templateRow.debit;
                                    int netbanking = templateRow.netbanking;
                                    int wallet = templateRow.wallet;
                                    int bima_asba = 0;
                                    int emi = templateRow.emi;

                                    if (transactionData.Product.Contains("HEALTH") && transactionData.ProposalList.Count == 1 && transactionData.ProposalList[0].is_nstp_flow == "Y" && transactionData.journey_type.ToUpper() == "ASBA")
                                    {
                                        bima_asba = templateRow.upi;
                                        upi = 0;
                                    }

                                    string sequence =
                                           $"{upi}{credit}{netbanking}{wallet}{bima_asba}{emi}{debit}";

                                    transactionData.TemplateDetails = sequence;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(transactionData.payment_filters))
                        {
                            var filters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(transactionData.payment_filters);

                            var order = new[]
                            {
                                "UPI",
                                "CreditCard",
                                "NetBanking",
                                "Wallet",
                                "BimaAsba",
                                "StandardEMI", // modify if EMI logic changes
                                "DebitCard"
                            };

                            int seq = 1;

                            string sequence = string.Concat(order.Select(k =>
                                filters != null && filters.TryGetValue(k, out bool val) && val
                                    ? (seq++).ToString()
                                    : "0"
                            ));


                            transactionData.TemplateDetails = sequence;
                        }

                        if (string.IsNullOrEmpty(transactionData.TemplateDetails))
                        {
                            transactionData.TemplateDetails = "1245003";
                        }

                        //if (string.IsNullOrEmpty(application_id))
                        //{
                        //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        //        MethodName: "PaymentDAL.cs - FetchPaymentTransaction() ",
                        //        TraceId: traceID,
                        //        Message: "Application Id Not Found. Transaction ID: " + transactionID + " Trace ID: " + traceID), ApplicationConfig.PAYMENT_LOGS);

                        //    return null;
                        //}


                        //fetchTransactionData["application_id"] = application_id;
                        //fetchTransactionData["rm_details"] = rm_details;
                        //fetchTransactionData["transaction_data"] = JsonConvert.SerializeObject(transactionData);

                        return transactionData;
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentDAL.cs - FetchPaymentTransaction() ",
                                TraceId: traceID,
                                Message: "Transaction Data Not Found. Transaction ID: " + transactionID + " Trace ID: " + traceID), ApplicationConfig.PAYMENT_LOGS);

                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - FetchPaymentTransaction() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                return null;
            }
        }

        public async Task updateIsFirstTxnVisit(string traceId, string transactionId, string link_expiry_time, string authorization_token)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_IS_FIRST_TXN_VISIT;

                    var parameters = new
                    {
                        TRANSACTION_ID = transactionId,
                        LINK_EXPIRY_TIME = link_expiry_time,
                        AUTHORIZATION_TOKEN = authorization_token
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - updateIsFirstTxnVisit() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task UpdateAdvanceDBTransaction(TagProposalReq tagProposalReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_ADVANCE_TRANSACTION_DETAILS;

                    var parameters = new
                    {
                        PROPOSAL_NUMBER = tagProposalReq.ProposalList.ProposalNumber,
                        PROPOSAL_AMOUNT = tagProposalReq.ProposalList.ProposalAmount,
                        PROPOSAL_STATUS = tagProposalReq.ProposalList.ProposalStatus,
                        DEAL_ID = tagProposalReq.ProposalList.DealId,
                        CUSTOMER_ID = tagProposalReq.ProposalList.CustomerId,
                        INTERMEDIARY_CODE = tagProposalReq.ProposalList.IntermediaryCode,
                        USER_ID = tagProposalReq.ProposalList.UserID,
                        TRANSACTION_ID = tagProposalReq.TransactionID
                    };

                    await connection.ExecuteAsync(query, parameters);


                    string payment_query = DatabaseConfig.UPDATE_ADVANCE_PAYMENT_DETAILS;

                    var payment_parameters = new
                    {
                        PROPOSAL_NUMBER = tagProposalReq.ProposalList.ProposalNumber,
                        TRANSACTION_ID = tagProposalReq.TransactionID
                    };

                    await connection.ExecuteAsync(payment_query, payment_parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - UpdateAdvanceDBTransaction() ",
                    TraceId: tagProposalReq.TraceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<FetchPaymentDetails> FetchAdvancePaymentDetails(string transactionID)
        {
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.FETCH_ADVANCE_PAYMENT_DETAILS;
                    var parameters = new { TRANSACTION_ID = transactionID };
                    var paymentDataList = await connection.QueryAsync(query, parameters);

                    if (paymentDataList != null && paymentDataList.Count() > 0)
                    {
                        var firstRow = paymentDataList.First();
                        fetchPaymentDetails = new FetchPaymentDetails
                        {
                            transaction_id = firstRow.transaction_id,
                            trace_id = firstRow.trace_id,
                            created_on = Convert.ToString(firstRow.created_on),
                            core = firstRow.core,
                            total_amount = firstRow.total_amount,
                            payer_type = firstRow.payer_type,
                            pf_mid = firstRow.pf_mid,
                            artemis_mid = firstRow.artemis_mid,
                            reverse_sync_url = firstRow.reverse_sync_url,
                            payment_mode = firstRow.payment_mode,
                            product = firstRow.product,
                            journey_type = firstRow.journey_type,
                            auth_code = firstRow.auth_code,
                            channel_application_details = firstRow.channel_application_details
                        };

                        List<FetchPaymentProposalList> fetchPaymentProposalLists = new List<FetchPaymentProposalList>();
                        foreach (var row in paymentDataList)
                        {
                            fetchPaymentProposalLists.Add(new FetchPaymentProposalList
                            {
                                proposal_number = row.proposal_number,
                                proposal_status = row.proposal_status,
                                deal_id = row.deal_id,
                                customer_id = row.customer_id,
                                intermediary_code = row.intermediary_code,
                                is_nstp_flow = row.is_nstp_flow
                            });
                        }
                        fetchPaymentDetails.fetchPaymentProposalList = fetchPaymentProposalLists;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - FetchAdvancePaymentDetails()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return fetchPaymentDetails;
        }

        public async Task UpdateAdvanceProposalDetails(string proposal_number, string proposal_status, string is_ntsp_flow, string business_vertical, string sub_vertical, string marketing_officer, string intermediary_code, string proposer_code, string trace_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_ADVANCE_PROPOSAL_DETAILS;

                    var parameters = new
                    {
                        PROPOSAL_NUMBER = proposal_number,
                        PROPOSAL_STATUS = proposal_status,
                        IS_NSTP_FLOW = is_ntsp_flow,
                        BUSINESS_VERTICAL = business_vertical,
                        SUB_VERTICAL = sub_vertical,
                        MARKETING_OFFICER = marketing_officer,
                        INTERMEDIARY_CODE = intermediary_code,
                        PROPOSER_CODE = proposer_code
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - UpdateAdvanceProposalDetails() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<bool> IsTransactionDone(string transactionId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.IS_TRANSACTION_DONE;
                    var parameters = new { TRANSACTION_ID = transactionId };
                    int count = await connection.ExecuteScalarAsync<int>(query, parameters);
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - IsTransactionDone() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
                return false;
            }
        }

        public async Task<AddPaymentResp> checkIfProposalExists(AddPaymentReq addPaymentReq)
        {
            AddPaymentResp addPaymentResp = new AddPaymentResp();
            string transactionIdStr = string.Empty;
            string channel_application_details = string.Empty;

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var proposalNumbers = addPaymentReq.ProposalList.Select(p => p.ProposalNumber).ToArray();

                    string checkIfProposalExists = DatabaseConfig.CHECK_IF_PROPOSAL_EXISTS;
                    var existingRows = await connection.QueryAsync(checkIfProposalExists, new { PROPOSAL_LIST = proposalNumbers });

                    if (existingRows != null && existingRows.Count() != 0)
                    {
                        var paymentUrls = existingRows.Select(r => (string)r.payment_url).Where(u => !string.IsNullOrEmpty(u)).ToList();

                        if (paymentUrls.Count > 0 && paymentUrls.Count == addPaymentReq.ProposalList.Count && paymentUrls.Distinct().Count() == 1)
                        {
                            addPaymentResp = new AddPaymentResp
                            {
                                URL = existingRows.First().payment_url,
                                TransactionID = existingRows.First().transaction_id,
                                pg_name = existingRows.First().pg_name
                            };

                            foreach (var row in existingRows)
                            {
                                string updateIsFirstTxnVisitToY = DatabaseConfig.UPDATE_IS_FIRST_TXN_VISIT_TO_Y;
                                await connection.ExecuteAsync(updateIsFirstTxnVisitToY, new { PROPOSAL_NUMBER = row.proposal_number });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - checkIfProposalExists() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return addPaymentResp;
        }

        public async Task<AddPaymentRespv2> LogDbTransactionv2(AuthenticateTokenData authenticateTokenData, AddPaymentReqv2 addPaymentReq, string traceId, decimal totalAmount, string rm_text, List<FetchProposalDetailsResp> fetchProposalDetailsRespList)
        {
            int totalRowsInserted = 0;
            int expectedInsertCount = 0;
            string transactionIdStr = string.Empty;
            string channel_application_details = string.Empty;
            string proposal_status = string.Empty;
            long sum_insured = 0;

            try
            {
                if (addPaymentReq.channelApplicationDetails.ValueKind == JsonValueKind.Object)
                {
                    channel_application_details =
                        System.Text.Json.JsonSerializer.Serialize(addPaymentReq.channelApplicationDetails);
                }
                else if (addPaymentReq.channelApplicationDetails.ValueKind != JsonValueKind.Undefined &&
                         addPaymentReq.channelApplicationDetails.ValueKind != JsonValueKind.Null)
                {
                    // ❌ Present but NOT an object → invalid format
                    objLog.LogAWSMessageAsync(
                        objLog.GenericAWSException_CWL(
                            MethodName: "PaymentDAL.cs - LogDbTransactionv2()",
                            Message: "Channel Application Details format is invalid. Request: " +
                                     JsonConvert.SerializeObject(addPaymentReq),
                            TraceId: traceId),
                        ApplicationConfig.PAYMENT_LOGS
                    );
                }


                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    if (addPaymentReq.journeyType != "ADVANCE")
                    {
                        string getPGname = DatabaseConfig.GET_ACTIVE_PG_NAME;
                        var getPgNameParameter = new { APPLICATION_NAME = authenticateTokenData.username };
                        string pgName = await connection.ExecuteScalarAsync<string>(getPGname, getPgNameParameter);
                        pgName = !string.IsNullOrEmpty(pgName) ? pgName : "JUSPAY";

                        string getTransactionIdSeq = DatabaseConfig.GET_TRANSACTION_ID_SEQ;
                        long transactionId = await connection.ExecuteScalarAsync<long>(getTransactionIdSeq);
                        transactionIdStr = "PG_TXN_" + transactionId.ToString();
                        string insertTransactionDetails = DatabaseConfig.INSERT_TRANSACTION_DETAILS_V2;

                        expectedInsertCount = addPaymentReq.proposalList.Count;

                        foreach (FetchProposalDetailsResp proposalDetailsResp in fetchProposalDetailsRespList)
                        {
                            if (string.IsNullOrEmpty(proposalDetailsResp.ProposalSubStatus) && !string.IsNullOrEmpty(proposalDetailsResp.ProposalStatus))
                            {
                                proposal_status = proposalDetailsResp.ProposalStatus;
                            }
                            else if (!string.IsNullOrEmpty(proposalDetailsResp.ProposalSubStatus))
                            {
                                proposal_status = proposalDetailsResp.ProposalSubStatus;
                            }

                            string travellerDetailsJson = null;
                            bool? isSeniorCitizen = null;

                            if (proposalDetailsResp.TravelAdditionalInfo?.travellerDetails?.Any() == true)
                            {
                                var travellers = proposalDetailsResp.TravelAdditionalInfo.travellerDetails;

                                var travellerPayload = travellers
                                    .Select(t => new
                                    {
                                        name = t.name,
                                        isPed = t.isPed
                                    })
                                    .ToList();

                                travellerDetailsJson = JsonConvert.SerializeObject(travellerPayload);
                                isSeniorCitizen = travellers.Any(t => t.age > 70);
                            }
                            if (addPaymentReq.product.ToUpper().Contains("TRAVEL"))
                            {
                                sum_insured = proposalDetailsResp.TravelAdditionalInfo?.pedSumInsured ?? 0;
                            }
                            else
                            {
                                sum_insured = proposalDetailsResp.SumInsured ?? 0;
                            }
                            var parameters = new
                            {
                                TRANSACTION_ID = transactionIdStr,
                                APPLICATION_NAME = authenticateTokenData.username,
                                TRACE_ID = traceId,
                                TOTAL_AMOUNT = totalAmount.ToString("F2"),
                                PROPOSAL_NUMBER = proposalDetailsResp.proposalNumber,
                                PROPOSAL_AMOUNT = Convert.ToDecimal(proposalDetailsResp.proposalAmount).ToString("F2"),
                                PROPOSAL_STATUS = proposal_status,
                                PROPOSAL_START_DATE = proposalDetailsResp.ProposalStartDate,
                                NAME = proposalDetailsResp.CustomerDetails?.CustomerName,
                                EMAIL = proposalDetailsResp.CustomerDetails?.CustomerEmail,
                                MOBILE_NUMBER = proposalDetailsResp.CustomerDetails?.CustomerMobile,
                                PRODUCT = addPaymentReq.product,
                                JOURNEY_TYPE = addPaymentReq.journeyType.ToUpper(),
                                CORE = proposalDetailsResp.core,
                                PAYER_TYPE = addPaymentReq.payerType,
                                IS_MOBILE_USER = addPaymentReq.isMobileUser,
                                CHANNEL_APPLICATION_DETAILS = channel_application_details,
                                RM_TEXT = rm_text,
                                PG_NAME = pgName,
                                PAYMENT_FILTERS = JsonConvert.SerializeObject(addPaymentReq.paymentFilters),
                                DEAL_ID = proposalDetailsResp.DealId,
                                CUSTOMER_ID = proposalDetailsResp.CustomerDetails?.CustomerId,
                                INTERMEDIARY_CODE = !string.IsNullOrEmpty(proposalDetailsResp.IntermediaryCode) ? proposalDetailsResp.IntermediaryCode : addPaymentReq.intermediaryCode,
                                IS_NSTP_FLOW = proposalDetailsResp.IsNSTPFlowStr,
                                BUSINESS_VERTICAL = proposalDetailsResp.BusinessVertical,
                                SUB_VERTICAL = proposalDetailsResp.SubVertical,
                                MARKETING_OFFICER = proposalDetailsResp.MarketingOfficer,
                                PROPOSER_CODE = addPaymentReq.payerType == "C" ? proposalDetailsResp.CustomerDetails?.CustomerId : proposalDetailsResp.IntermediaryCode,
                                PROPOSAL_ID = proposalDetailsResp.proposalId,
                                PROPOSAL_TYPE = proposalDetailsResp.proposalType,
                                PROPOSAL_END_DATE = proposalDetailsResp.ProposalEndDate,
                                PROPOSAL_TENURE = proposalDetailsResp.ProposalTenure,
                                BASIC_PREMIUM = proposalDetailsResp.PremiumDetails?.BasicPremium,
                                GST = proposalDetailsResp.PremiumDetails?.GST,
                                VRN = proposalDetailsResp.MotorAdditionalInfo?.RegistrationNo,
                                ADDRESS = proposalDetailsResp.CustomerDetails?.CustomerAddress,
                                STATE = proposalDetailsResp.CustomerDetails?.State,
                                CITY = proposalDetailsResp.CustomerDetails?.City,
                                PINCODE = proposalDetailsResp.CustomerDetails?.Pincode,
                                TRIP_TYPE = proposalDetailsResp.TravelAdditionalInfo?.tripType,
                                SUM_INSURED = sum_insured,
                                PED = travellerDetailsJson,
                                IS_SENIORCITIZEN = isSeniorCitizen,
                                flightTracking = proposalDetailsResp.TravelAdditionalInfo?.flightTracking,
                                flightTrackingURL = proposalDetailsResp.TravelAdditionalInfo?.flightTrackingURL,
                                JUSPAY_UDF1 = "-",
                                JUSPAY_UDF2 = "Website",
                                JUSPAY_UDF3 = proposalDetailsResp.core == "PF" ? proposalDetailsResp?.pfProductCode : proposalDetailsResp?.irdaProductCode,
                                JUSPAY_UDF4 = addPaymentReq.journeyType == "RENEWAL" ? "W" : "R",
                                JUSPAY_UDF5 = addPaymentReq.proposalList[0].ProposalNumber,
                                JUSPAY_UDF6 = "-",
                                JUSPAY_UDF7 = "-",
                                JUSPAY_UDF8 = "Digital",
                                JUSPAY_UDF9 = "-",
                                JUSPAY_UDF10 = transactionIdStr,
                                PLATFORM = addPaymentReq.platform,
                                IS_HEADER_REQ = addPaymentReq.isHeaderReq,
                                IS_FOOTER_REQ = addPaymentReq.isFooterReq,
                                INSTALLMENT_AMOUNT = addPaymentReq.emiDetails?.installment_amount,
                                INSTALLMENT_NUMBER = addPaymentReq.emiDetails?.installment_number,
                                PRODUCT_CODE = proposalDetailsResp.core == "PF" ? proposalDetailsResp?.pfProductCode : proposalDetailsResp?.irdaProductCode,
                                IS_ILEMI_PROPOSAL_MOD_REQD = (addPaymentReq.paymentFilters.ILEMI && addPaymentReq.emiDetails?.installment_number == "1") ? true : false,
                                MANDATE_MAX_AMOUNT = addPaymentReq.emiDetails?.mandate_max_amount,
                            };

                            int rows = await connection.ExecuteAsync(insertTransactionDetails, parameters);
                            totalRowsInserted += rows;
                        }

                        if (totalRowsInserted == expectedInsertCount)
                        {
                            AddPaymentRespv2 addPaymentResp = new AddPaymentRespv2
                            {
                                URL = null,
                                TransactionID = transactionIdStr,
                                pg_name = pgName
                            };
                            return addPaymentResp;
                        }
                    }
                    else
                    {
                        string getPGname = DatabaseConfig.GET_ACTIVE_PG_NAME;
                        var getPgNameParameter = new { APPLICATION_NAME = authenticateTokenData.username };
                        string pgName = await connection.ExecuteScalarAsync<string>(getPGname, getPgNameParameter);
                        pgName = !string.IsNullOrEmpty(pgName) ? pgName : "JUSPAY";

                        string getTransactionIdSeq = DatabaseConfig.GET_TRANSACTION_ID_SEQ;
                        long transactionId = await connection.ExecuteScalarAsync<long>(getTransactionIdSeq);
                        transactionIdStr = "PG_TXN_" + transactionId.ToString();
                        string insertTransactionDetails = DatabaseConfig.INSERT_ADVANCE_TRANSACTION_DETAILS_V3;

                        foreach (ProposalListv2 proposal in addPaymentReq.proposalList)
                        {
                            var parameters = new
                            {
                                TRANSACTION_ID = transactionIdStr,
                                APPLICATION_NAME = authenticateTokenData.username,
                                TRACE_ID = traceId,
                                TOTAL_AMOUNT = totalAmount.ToString("F2"),
                                PRODUCT = addPaymentReq.product,
                                JOURNEY_TYPE = addPaymentReq.journeyType.ToUpper(),
                                PAYER_TYPE = addPaymentReq.payerType,
                                IS_MOBILE_USER = addPaymentReq.isMobileUser,
                                CHANNEL_APPLICATION_DETAILS = channel_application_details,
                                PG_NAME = pgName,
                                PAYMENT_FILTERS = JsonConvert.SerializeObject(addPaymentReq.paymentFilters),
                                JUSPAY_UDF1 = "-",
                                JUSPAY_UDF2 = "Website",
                                JUSPAY_UDF3 = "-",
                                JUSPAY_UDF4 = addPaymentReq.journeyType == "RENEWAL" ? "W" : "R",
                                JUSPAY_UDF5 = "-",
                                JUSPAY_UDF6 = "-",
                                JUSPAY_UDF7 = "-",
                                JUSPAY_UDF8 = "Digital",
                                JUSPAY_UDF9 = "-",
                                JUSPAY_UDF10 = transactionIdStr,
                                PLATFORM = addPaymentReq.platform,
                                IS_HEADER_REQ = addPaymentReq.isHeaderReq,
                                IS_FOOTER_REQ = addPaymentReq.isFooterReq,
                                PRE_PAYMENT_RETURN_URL = addPaymentReq.prePaymentReturnUrl.Contains("?") ? addPaymentReq.prePaymentReturnUrl += "&txnId=" + transactionIdStr : addPaymentReq.prePaymentReturnUrl += "?txnId=" + transactionIdStr,
                                DEAL_ID = proposal.DealId,
                                CUSTOMER_ID = proposal.CustomerId,
                                INTERMEDIARY_CODE = proposal.IntermediaryCode,
                                PROPOSAL_AMOUNT = Convert.ToDecimal(proposal.ProposalAmount).ToString("F2"),
                                PROPOSAL_TYPE = proposal.ProposalType,
                                CORE = proposal.core
                            };

                            int rows = await connection.ExecuteAsync(insertTransactionDetails, parameters);
                            totalRowsInserted += rows;
                        }

                        if (totalRowsInserted == addPaymentReq.proposalList.Count)
                        {
                            AddPaymentRespv2 addPaymentResp = new AddPaymentRespv2
                            {
                                URL = null,
                                TransactionID = transactionIdStr,
                                pg_name = pgName
                            };
                            return addPaymentResp;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - LogDbTransactionv2() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
            return null;
        }

        public async Task LogAuthTokenData(string traceId, string transactionId, AuthenticateTokenData authenticateTokenData)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.LOG_AUTH_TOKEN_DATA;

                    var parameters = new
                    {
                        TRANSACTION_ID = transactionId,
                        TRACE_ID = traceId,
                        APPLICATION_NAME = authenticateTokenData.username,
                        UNIQUE_NAME = authenticateTokenData.unique_name,
                        JTI = authenticateTokenData.jti,
                        ROLE = authenticateTokenData.Role,
                        USERLOGIN = authenticateTokenData.UserLogin,
                        CLIENT = authenticateTokenData.Client,
                        CC_ID = authenticateTokenData.CCId,
                        ISGUEST = authenticateTokenData.IsGuest,
                        SESSIONID = authenticateTokenData.SessionId,
                        SKEY = authenticateTokenData.SKey,
                        SP = authenticateTokenData.SP
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - LogAuthTokenData() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<List<GetPaymentLinkDetails>> GetPaymentLinkByTransactionID(string transactionID, string traceID)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_PAYMENT_LINK_BY_TRANSACTION_ID;
                    var parameters = new { TRANSACTION_ID = transactionID };

                    var result = await connection.QueryAsync<GetPaymentLinkDetails>(query, parameters);

                    return result.ToList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - GetPaymentLinkByTransactionID()",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                return new List<GetPaymentLinkDetails>();
            }
        }

        public async Task<List<CheckProposalV2OrderData>> checkproposalv2(AddPaymentReqv2 addPaymentReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var proposalNumbers = addPaymentReq.proposalList
                        .Where(x => !string.IsNullOrEmpty(x.ProposalNumber))
                        .Select(x => x.ProposalNumber)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (proposalNumbers.Length == 0)
                    {
                        return new List<CheckProposalV2OrderData>();
                    }

                    var result = await connection.QueryAsync<CheckProposalV2OrderData>(
                        DatabaseConfig.GET_PROPOSAL_TXN_ORDER_HISTORY_V2,
                        new { PROPOSAL_LIST = proposalNumbers });

                    return result?.ToList() ?? new List<CheckProposalV2OrderData>();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - checkproposalv2() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                return new List<CheckProposalV2OrderData>();
            }
        }

        public async Task UpdateTransactionDetails(string transaction_id, string customerAddress, string state, string city, string pinCode, string trace_id)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_TRANSACTION_DETAILS;

                    var parameters = new
                    {
                        ADDRESS = customerAddress,
                        STATE = state,
                        CITY = city,
                        PINCODE = pinCode,
                        TRANSACTION_ID = transaction_id
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - UpdateTransactionDetails() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<GetMessageByProduct> getMessageByProduct(string product)
        {
            GetMessageByProduct getMessageByProduct = new GetMessageByProduct();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_MESSAGE_BY_PRODUCT;
                    var parameters = new { PRODUCT = product };
                    var result = await connection.QueryFirstOrDefaultAsync<GetMessageByProduct>(query, parameters);
                    return result;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - getMessageByProduct() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
            return null;
        }

        public async Task updateProposalStatus(string traceId, string proposal_number, string proposal_status)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_PROPOSAL_STATUS;

                    var parameters = new
                    {
                        PROPOSAL_STATUS = proposal_status,
                        PROPOSAL_NUMBER = proposal_number,
                        TRACE_ID = traceId
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - updateProposalStatus() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<GetPaymentDetails> GetPaymentLink(string transactionId, string traceId)
        {
            GetPaymentDetails getPaymentDetails = new GetPaymentDetails();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_PAYMENT_LINK;
                    var parameters = new { TRANSACTION_ID = transactionId };
                    var result = await connection.QueryFirstOrDefaultAsync<GetPaymentDetails>(query, parameters);
                    return result;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - GetPaymentLink() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
            return null;
        }

        public async Task<FetchPaymentDetails> FetchAdvancePaymentDetailsv2(string transactionID)
        {
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.FETCH_ADVANCE_PAYMENT_DETAILS_V2;
                    var parameters = new { TRANSACTION_ID = transactionID };
                    var paymentDataList = await connection.QueryAsync(query, parameters);

                    if (paymentDataList != null && paymentDataList.Count() > 0)
                    {
                        var firstRow = paymentDataList.First();
                        fetchPaymentDetails = new FetchPaymentDetails
                        {
                            transaction_id = firstRow.transaction_id,
                            trace_id = firstRow.trace_id,
                            created_on = Convert.ToString(firstRow.created_on),
                            core = firstRow.core,
                            total_amount = firstRow.total_amount,
                            payer_type = firstRow.payer_type,
                            pf_mid = firstRow.pf_mid,
                            artemis_mid = firstRow.artemis_mid,
                            reverse_sync_url = firstRow.reverse_sync_url,
                            payment_mode = firstRow.payment_mode,
                            product = firstRow.product,
                            journey_type = firstRow.journey_type,
                            auth_code = firstRow.auth_code,
                            channel_application_details = firstRow.channel_application_details,
                            dps_order_id = firstRow.dps_order_id,
                            application_name = firstRow.application_name,
                            fetchPaymentProposalList = new List<FetchPaymentProposalList>
                            {
                                new FetchPaymentProposalList
                                {
                                    proposal_number = firstRow.proposal_number,
                                    proposal_status = firstRow.proposal_status,
                                    deal_id = firstRow.deal_id,
                                    customer_id = firstRow.customer_id,
                                    intermediary_code = firstRow.intermediary_code,
                                    is_nstp_flow = firstRow.is_nstp_flow
                                }
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - FetchAdvancePaymentDetailsv2()",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return fetchPaymentDetails;
        }

        public async Task UpdateAdvanceDBTransactionv2(TagProposalReqv2 tagProposalReq, FetchPaymentDetails fetchPaymentDetails, FetchProposalDetailsResp fetchProposalDetailsResp)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_ADVANCE_TRANSACTION_DETAILS_V2;

                    var parameters = new
                    {
                        PROPOSAL_NUMBER = tagProposalReq.ProposalList.ProposalNumber,
                        PROPOSAL_AMOUNT = tagProposalReq.ProposalList.ProposalAmount,
                        PROPOSAL_ID = tagProposalReq.ProposalList.ProposalId,
                        PROPOSAL_TYPE = tagProposalReq.ProposalList.ProposalType,
                        CORE = tagProposalReq.ProposalList.core,
                        PROPOSAL_STATUS = fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status,
                        DEAL_ID = fetchPaymentDetails.fetchPaymentProposalList[0].deal_id,
                        CUSTOMER_ID = fetchPaymentDetails.fetchPaymentProposalList[0].customer_id,
                        INTERMEDIARY_CODE = fetchPaymentDetails.fetchPaymentProposalList[0].intermediary_code,
                        IS_NSTP_FLOW = fetchPaymentDetails.fetchPaymentProposalList[0].is_nstp_flow,
                        BUSINESS_VERTICAL = fetchPaymentDetails.fetchPaymentProposalList[0].business_vertical,
                        SUB_VERTICAL = fetchPaymentDetails.fetchPaymentProposalList[0].sub_vertical,
                        MARKETING_OFFICER = fetchPaymentDetails.fetchPaymentProposalList[0].marketing_officer,
                        PROPOSER_CODE = fetchPaymentDetails.fetchPaymentProposalList[0].proposer_code,
                        PROPOSAL_START_DATE = fetchProposalDetailsResp.ProposalStartDate,
                        NAME = fetchProposalDetailsResp.CustomerDetails?.CustomerName,
                        EMAIL = fetchProposalDetailsResp.CustomerDetails?.CustomerEmail,
                        MOBILE_NUMBER = fetchProposalDetailsResp.CustomerDetails?.CustomerMobile,
                        PROPOSAL_END_DATE = fetchProposalDetailsResp.ProposalEndDate,
                        PROPOSAL_TENURE = fetchProposalDetailsResp.ProposalTenure,
                        BASIC_PREMIUM = fetchProposalDetailsResp.PremiumDetails?.BasicPremium,
                        GST = fetchProposalDetailsResp.PremiumDetails?.GST,
                        VRN = fetchProposalDetailsResp.MotorAdditionalInfo?.RegistrationNo,
                        ADDRESS = fetchProposalDetailsResp.CustomerDetails?.CustomerAddress,
                        STATE = fetchProposalDetailsResp.CustomerDetails?.State,
                        CITY = fetchProposalDetailsResp.CustomerDetails?.City,
                        PINCODE = fetchProposalDetailsResp.CustomerDetails?.Pincode,
                        TRANSACTION_ID = tagProposalReq.TransactionID
                    };

                    await connection.ExecuteAsync(query, parameters);


                    string payment_query = DatabaseConfig.UPDATE_ADVANCE_PAYMENT_DETAILS;

                    var payment_parameters = new
                    {
                        PROPOSAL_NUMBER = tagProposalReq.ProposalList.ProposalNumber,
                        TRANSACTION_ID = tagProposalReq.TransactionID
                    };

                    await connection.ExecuteAsync(payment_query, payment_parameters);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - UpdateAdvanceDBTransactionv2() ",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<FetchPaymentDetails> FetchAdvanceTxnDetailsv3(string transactionID)
        {
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.FETCH_ADVANCE_TXN_DETAILS_V3;
                    var parameters = new { TRANSACTION_ID = transactionID };
                    var result = await connection.QueryAsync(query, parameters);

                    if (result != null && result.Count() > 0)
                    {
                        var firstRow = result.First();
                        fetchPaymentDetails = new FetchPaymentDetails
                        {
                            transaction_id = firstRow.transaction_id,
                            trace_id = firstRow.trace_id,
                            created_on = firstRow.created_on.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            core = firstRow.core,
                            total_amount = firstRow.total_amount,
                            payer_type = firstRow.payer_type,
                            pf_mid = firstRow.pf_mid,
                            artemis_mid = firstRow.artemis_mid,
                            auth_code = firstRow.auth_code,
                            reverse_sync_url = firstRow.reverse_sync_url,
                            payment_mode = firstRow.payment_mode,
                            product = firstRow.product,
                            journey_type = firstRow.journey_type,
                            application_name = firstRow.application_name,
                            channel_application_details = firstRow.channel_application_details,
                            dps_order_id = firstRow.dps_order_id,
                            payment_id = firstRow.payment_id,
                            fetchPaymentProposalList = result.Select(row => new FetchPaymentProposalList
                            {
                                proposal_number = row.proposal_number,
                                proposal_amount = row.proposal_amount,
                                proposal_type = row.proposal_type,
                                core = row.core,
                                proposal_status = row.proposal_status,
                                is_nstp_flow = row.is_nstp_flow,
                                deal_id = row.deal_id,
                                customer_id = row.customer_id,
                                intermediary_code = row.intermediary_code,
                                business_vertical = row.business_vertical,
                                sub_vertical = row.sub_vertical,
                                marketing_officer = row.marketing_officer,
                                proposer_code = row.proposer_code,
                                policy_number = row.policy_number
                            }).ToList()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - FetchAdvanceTxnDetailsv3()",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return fetchPaymentDetails;
        }

        public async Task<FetchPaymentProposalList> FetchAdvancePaymentDetailsv3(string transaction_id, string proposal_amount, string proposal_type, string core, string trace_id)
        {
            FetchPaymentProposalList fetchPaymentProposal = new FetchPaymentProposalList();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.FETCH_ADVANCE_PAYMENT_DETAILS_V3;
                    var parameters = new { TRANSACTION_ID = transaction_id, PROPOSAL_AMOUNT = proposal_amount, PROPOSAL_TYPE = proposal_type, CORE = core };
                    var paymentData = await connection.QueryFirstOrDefaultAsync(query, parameters);

                    if (paymentData != null)
                    {
                        fetchPaymentProposal = new FetchPaymentProposalList
                        {
                            deal_id = paymentData.deal_id,
                            customer_id = paymentData.customer_id,
                            intermediary_code = paymentData.intermediary_code,
                            business_vertical = paymentData.business_vertical,
                            sub_vertical = paymentData.sub_vertical,
                            marketing_officer = paymentData.marketing_officer,
                            proposer_code = paymentData.proposer_code
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - FetchAdvancePaymentDetailsv3()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return fetchPaymentProposal;
        }

        public async Task UpdateAdvanceDBTransactionv3(List<UpdateAdvancePaymentDetails> updateAdvancePaymentDetailsList, string transactionId, string traceId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    foreach (UpdateAdvancePaymentDetails updateAdvancePaymentDetails in updateAdvancePaymentDetailsList)
                    {
                        string query = DatabaseConfig.UPDATE_ADVANCE_TRANSACTION_DETAILS_V3;

                        var parameters = new
                        {
                            PROPOSAL_NUMBER = updateAdvancePaymentDetails.proposal_number,
                            PROPOSAL_ID = updateAdvancePaymentDetails.proposal_id,
                            PROPOSAL_STATUS = updateAdvancePaymentDetails.proposal_status,
                            IS_NSTP_FLOW = updateAdvancePaymentDetails.is_nstp_flow,
                            PROPOSAL_START_DATE = updateAdvancePaymentDetails.proposal_start_date,
                            NAME = updateAdvancePaymentDetails.name,
                            EMAIL = updateAdvancePaymentDetails.email,
                            MOBILE_NUMBER = updateAdvancePaymentDetails.mobile_number,
                            PROPOSAL_END_DATE = updateAdvancePaymentDetails.proposal_end_date,
                            PROPOSAL_TENURE = updateAdvancePaymentDetails.proposal_tenure,
                            BASIC_PREMIUM = updateAdvancePaymentDetails?.basic_premium,
                            GST = updateAdvancePaymentDetails?.gst,
                            VRN = updateAdvancePaymentDetails.vrn,
                            ADDRESS = updateAdvancePaymentDetails.address,
                            STATE = updateAdvancePaymentDetails.state,
                            CITY = updateAdvancePaymentDetails.city,
                            PINCODE = updateAdvancePaymentDetails.pincode,
                            PROPOSAL_AMOUNT = updateAdvancePaymentDetails.proposal_amount,
                            PROPOSAL_TYPE = updateAdvancePaymentDetails.proposal_type,
                            CORE = updateAdvancePaymentDetails.core,
                            TRANSACTION_ID = transactionId
                        };

                        int count = await connection.ExecuteAsync(query, parameters);


                        string payment_query = DatabaseConfig.UPDATE_ADVANCE_PAYMENT_DETAILS_V3;

                        var payment_parameters = new
                        {
                            PROPOSAL_NUMBER = updateAdvancePaymentDetails.proposal_number,
                            TRANSACTION_ID = transactionId
                        };

                        int payment_count = await connection.ExecuteAsync(payment_query, payment_parameters);
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - UpdateAdvanceDBTransactionv3() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }
        }

        public async Task<string> GetProductCodeFromProposalType(string traceId, string proposal_type)
        {
            string productCode = string.Empty;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_PRODUCT_CODE_FROM_PROPOSAL_TYPE;
                    var parameters = new { PROPOSAL_TYPE = proposal_type };
                    productCode = await connection.ExecuteScalarAsync<string>(query, parameters);
                }

            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - FetchAgentDetailsFromApiFactory() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return productCode;
        }

        public async Task<List<CheckProposalV2OrderData>> checkproposal(AddPaymentReq addPaymentReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    var proposalNumbers = addPaymentReq.ProposalList
                        .Where(x => !string.IsNullOrEmpty(x.ProposalNumber))
                        .Select(x => x.ProposalNumber)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (proposalNumbers.Length == 0)
                    {
                        return new List<CheckProposalV2OrderData>();
                    }

                    var result = await connection.QueryAsync<CheckProposalV2OrderData>(
                        DatabaseConfig.GET_PROPOSAL_TXN_ORDER_HISTORY_V2,
                        new { PROPOSAL_LIST = proposalNumbers });

                    return result?.ToList() ?? new List<CheckProposalV2OrderData>();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentDAL.cs - checkproposal() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                return new List<CheckProposalV2OrderData>();
            }
        }
    }
}
