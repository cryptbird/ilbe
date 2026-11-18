using Amazon.CloudWatchLogs;
using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Hangfire;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.DashboardModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RefundModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;
namespace DigitalPaymentSystem_ILTC.BAL
{
    public class CallbackBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task ExtractOrderDataViaCallbackWebhook(JsonElement callbackRequest, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            ILEMIValidateInstallmentReq iLEMISubsequentApiReq = new ILEMIValidateInstallmentReq();
            LatestEmiInstallment recurringInstallmentDetails = new LatestEmiInstallment();
            MandateExecutionResp mandateExecutionResp = new MandateExecutionResp();
            DPSStatusResponse iLEMISubsequentApiResp = new DPSStatusResponse();
            OrderDAL orderDAL = new OrderDAL();
            OrderBAL orderBAL = new OrderBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            // Webhook Details
            string order_id = string.Empty;
            string auth_code = string.Empty;
            string udf9 = string.Empty;
            string trace_id = string.Empty;
            string transaction_id = string.Empty;
            string dps_order_id = string.Empty;
            string status = string.Empty;
            string mandate_id = string.Empty;
            string event_name = string.Empty;
            string payment_mode = string.Empty;
            string payment_type = string.Empty;

            try
            {
                if (callbackRequest.TryGetProperty("content", out JsonElement contentElement) && contentElement.ValueKind == JsonValueKind.Object && contentElement.TryGetProperty("order", out JsonElement orderElement))
                {
                    if (orderElement.TryGetProperty("id", out JsonElement idElement))
                    {
                        order_id = idElement.GetString();
                    }
                    if (orderElement.TryGetProperty("payment_gateway_response", out JsonElement paymentGatewayResponseElement) && paymentGatewayResponseElement.ValueKind == JsonValueKind.Object)
                    {
                        if (paymentGatewayResponseElement.TryGetProperty("epg_txn_id", out JsonElement epg_txn_idElement))
                        {
                            auth_code = epg_txn_idElement.GetString();
                        }
                        else if (paymentGatewayResponseElement.TryGetProperty("rrn", out JsonElement rrnElement))
                        {
                            auth_code = rrnElement.GetString();
                        }
                        else
                        {
                            auth_code = order_id;
                        }
                    }
                    if (orderElement.TryGetProperty("udf9", out JsonElement udf9Element))
                    {
                        udf9 = udf9Element.GetString();

                        if (udf9.Contains("|"))
                        {
                            string[] udf9Parts = udf9.Split("|");

                            trace_id = udf9Parts[1];
                            transaction_id = udf9Parts[2];
                            dps_order_id = udf9Parts[3];

                            if (udf9Parts.Length > 4)
                            {
                                payment_type = udf9Parts[4];
                            }

                        }
                    }
                    if (orderElement.TryGetProperty("payment_method_type", out JsonElement payment_method_typeElement))
                    {
                        payment_mode = payment_method_typeElement.GetString();
                    }

                    if (orderElement.TryGetProperty("txn_detail", out JsonElement txnDetailElement) && txnDetailElement.ValueKind == JsonValueKind.Object && txnDetailElement.TryGetProperty("status", out JsonElement statusElement))
                    {
                        status = statusElement.GetString();

                        if (ApplicationConfig.DPS_JUSPAY_STATUS_LIST.ContainsKey(status))
                        {
                            status = ApplicationConfig.DPS_JUSPAY_STATUS_LIST[status];
                            status = status.ToUpper();
                        }
                    }
                    if (orderElement.TryGetProperty("mandate", out JsonElement contentParameter) && contentParameter.ValueKind == JsonValueKind.Object && contentParameter.TryGetProperty("mandate_id", out JsonElement mandateidElement))
                    {
                        mandate_id = mandateidElement.GetString();
                    }
                }
                if (callbackRequest.TryGetProperty("event_name", out JsonElement event_nameElement))
                {
                    event_name = event_nameElement.GetString();
                }

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(callbackRequest), "", "", "CALLBACK WEBHOOK", "", flow_type, "");

                if (string.IsNullOrEmpty(order_id) || string.IsNullOrEmpty(trace_id) || string.IsNullOrEmpty(transaction_id) || string.IsNullOrEmpty(status) || string.IsNullOrEmpty(event_name))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CallbackBAL.cs - ExtractOrderDataViaCallbackWebhook() ",
                        TraceId: trace_id,
                        Message: "Invalid webhook details. Callback Request: " + callbackRequest), ApplicationConfig.CALLBACK_LOGS);

                    return;
                }
                if (status.ToUpper() == "NOTIFICATION_FAILED")
                {
                    recurringInstallmentDetails = await orderDAL.GetRecurringInstallmentDetails(order_id);
                    iLEMISubsequentApiReq.amount = recurringInstallmentDetails.amount;
                    iLEMISubsequentApiReq.proposal_number = recurringInstallmentDetails.proposal_number;
                    iLEMISubsequentApiReq.installment_number = recurringInstallmentDetails.installment_number;
                    iLEMISubsequentApiReq.transaction_id = recurringInstallmentDetails.transaction_id;
                    iLEMISubsequentApiReq.trace_id = recurringInstallmentDetails.trace_id;
                    iLEMISubsequentApiReq.application_name = recurringInstallmentDetails.application_name;

                    iLEMISubsequentApiResp = await orderBAL.ILEmiExecution(iLEMISubsequentApiReq, flow_type);
                    if(iLEMISubsequentApiResp == null || iLEMISubsequentApiResp.StatusCode != 200)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "CallbackBAL.cs - ExtractOrderDataViaCallbackWebhook() ",
                            TraceId: trace_id,
                            Message: "ILEMI Subsequent API call failed. Callback Request: " + callbackRequest + " ILEMI Subsequent API Request: " + JsonConvert.SerializeObject(iLEMISubsequentApiReq) + " ILEMI Subsequent API Response: " + JsonConvert.SerializeObject(iLEMISubsequentApiResp)), ApplicationConfig.CALLBACK_LOGS);
                        return;
                    }
                    else{
                        if (iLEMISubsequentApiResp.Data != null)
                        {
                            mandateExecutionResp = JsonConvert.DeserializeObject<MandateExecutionResp>(iLEMISubsequentApiResp.Data.ToString());
                            if (mandateExecutionResp == null || !mandateExecutionResp.isSuccess)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "CallbackBAL.cs - ExtractOrderDataViaCallbackWebhook() ",
                                    TraceId: trace_id,
                                    Message: "ILEMI Subsequent API call failed. Callback Request: " + callbackRequest + " ILEMI Subsequent API Request: " + JsonConvert.SerializeObject(iLEMISubsequentApiReq) + " ILEMI Subsequent API Response: " + JsonConvert.SerializeObject(iLEMISubsequentApiResp)), ApplicationConfig.CALLBACK_LOGS);
                                return;
                            }
                        }
                    }
                }

                if (event_name == "ORDER_FAILED")
                {
                    await orderDAL.UpdateDpsOrderStatus("FAILED", transaction_id, dps_order_id);
                    bool isUpdated = await orderDAL.UpdatePaymentOrderStatus("FAILED", transaction_id, dps_order_id);
                    if (isUpdated)
                    {
                        //if (fetchPaymentDetails.journey_type == "ADVANCE")
                        //{
                        //    SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                        //    {
                        //        reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                        //        trace_id = fetchPaymentDetails.trace_id,
                        //        transaction_id = fetchPaymentDetails.transaction_id,
                        //        is_advance_payment_success = "N",
                        //        application_name = fetchPaymentDetails.application_name,
                        //        channel_application_details = fetchPaymentDetails.channel_application_details,
                        //        auth_code = auth_code,
                        //        sync_event = "PAYMENT_FAILED"
                        //    };
                        //    await SyncProposalToSource(syncProposalToSource, flow_type, fetchPaymentDetails.journey_type);
                        //}
                        //else
                        //{
                        //    SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                        //    {
                        //        reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                        //        trace_id = fetchPaymentDetails.trace_id,
                        //        transaction_id = fetchPaymentDetails.transaction_id,
                        //        is_advance_payment_success = null,
                        //        application_name = fetchPaymentDetails.application_name,
                        //        channel_application_details = fetchPaymentDetails.channel_application_details,
                        //        auth_code = auth_code,
                        //        sync_event = "PAYMENT_FAILED"
                        //    };
                        //    await SyncProposalToSource(syncProposalToSource, flow_type, fetchPaymentDetails.journey_type);
                        //}
                    }
                }

                if (status.ToUpper() != "SUCCESS" || event_name.ToUpper() != "ORDER_SUCCEEDED")
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "CallbackBAL.cs - ExtractOrderDataViaCallbackWebhook() ",
                       TraceId: trace_id,
                       Message: "Transaction not successful (Status is not SUCCESS). Callback Request: " + callbackRequest), ApplicationConfig.CALLBACK_LOGS);

                    return;
                }

                await CreatePolicyFromCallbackWebhook(callbackRequest, order_id, auth_code, transaction_id, trace_id, dps_order_id, status, mandate_id, payment_mode, flow_type, payment_type);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackBAL.cs - CallbackWebhook()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return;
        }

        public async Task CreatePolicyFromCallbackWebhook(JsonElement callbackRequest, string order_id, string auth_code, string transaction_id, string trace_id, string dps_order_id, string status, string mandate_id, string payment_mode, string flow_type, string payment_type)
        {
            PaymentBAL paymentBAL = new PaymentBAL();
            OrderBAL orderBAL = new OrderBAL();
            OrderDAL orderDAL = new OrderDAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();

            HashSet<string> uniqueIsNSTPFlagList = new HashSet<string>();

            // PF 
            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            PFPolicyCreationResp pfPolicyCreationResp = new PFPolicyCreationResp();
            ILESBInterface ilesbInterface = new ILESBInterface();

            string pfPaymentId = string.Empty;

            // Artemis
            ArtemisPolicyCreationReq artemisPolicyCreationReq = new ArtemisPolicyCreationReq();
            ArtemisPolicyCreationResp artemisPolicyCreationResp = new ArtemisPolicyCreationResp();
            ArtemisPolicyCreationRespTimeout artemisPolicyCreationRespTimeout = new ArtemisPolicyCreationRespTimeout();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();

            string receiptNumber = string.Empty;
            PlutusDAL plutusDAL = new PlutusDAL();

            List<SyncProposalToSource> syncProposalToSourceList = new List<SyncProposalToSource>();

            //ModifyProposal
            bool isEMIProposalModified = false;

            try
            {
                fetchPaymentDetails = await callbackDAL.LogPaymentDetails(order_id, auth_code, transaction_id, trace_id, dps_order_id, status, mandate_id, payment_mode);
                if (fetchPaymentDetails == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                      Message: "Order does not exist or Transaction already exists in Payment. Callback Request: " + callbackRequest,
                      TraceId: trace_id), ApplicationConfig.CALLBACK_LOGS);

                }
                else
                {
                    if (!string.IsNullOrEmpty(payment_type) && payment_type == "ILEMI")
                    {
                        foreach (var row in fetchPaymentDetails.fetchPaymentProposalList)
                        {
                            LatestEmiInstallment latestEmiInstallmentDetails = await orderDAL.GetLatestEmiInstallment(row.proposal_number);
                            if (latestEmiInstallmentDetails == null)
                            {
                                LatestEmiInstallment latestEMI = new LatestEmiInstallment
                                {
                                    recurring_id = dps_order_id,
                                    transaction_id = transaction_id,
                                    trace_id = trace_id,
                                    proposal_number = row.proposal_number,
                                    //policy_number = "",
                                    installment_number = fetchPaymentDetails.installment_number,
                                    amount = fetchPaymentDetails.installment_amount,
                                    application_name = fetchPaymentDetails.application_name,
                                    status = "RAISED",
                                    //payment_id = "",
                                    mandate_id = mandate_id,
                                    dps_order_id = dps_order_id,
                                    customer_id = fetchPaymentDetails.gateway_customer_id,
                                    order_id = order_id,
                                    payment_mode = payment_mode,
                                    due_date = DateTime.Now,
                                    execution_date = DateTime.Now,
                                    grace_end_date = DateTime.Now.AddMonths(4),
                                    remarks = "ONLINE PROCESSED"
                                };

                                // Insert into database
                                await orderDAL.upsertEmiInstallmentDetails(latestEMI);
                            }
                        }
                    }

                    if (fetchPaymentDetails.journey_type == "ADVANCE")
                    {
                        SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                        {
                            reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                            trace_id = fetchPaymentDetails.trace_id,
                            transaction_id = fetchPaymentDetails.transaction_id,
                            installment_amount = fetchPaymentDetails.installment_amount,
                            installment_number = fetchPaymentDetails.installment_number,
                            is_advance_payment_success = "Y",
                            application_name = fetchPaymentDetails.application_name,
                            channel_application_details = fetchPaymentDetails.channel_application_details,
                            auth_code = auth_code,
                            sync_event = "PAYMENT_CONFIRMATION"
                        };

                        await SyncProposalToSource(syncProposalToSource, flow_type, fetchPaymentDetails.journey_type);
                    }
                    else
                    {
                        fetchPaymentDetails.auth_code = auth_code;
                        fetchPaymentDetails.mandate_id = mandate_id;

                        foreach (FetchPaymentProposalList fetchPaymentProposal in fetchPaymentDetails.fetchPaymentProposalList)
                        {
                            uniqueIsNSTPFlagList.Add(fetchPaymentProposal.is_nstp_flow);
                        }

                        if (fetchPaymentDetails.core.ToUpper() == "PF")
                        {
                            // PF
                            // Create Payment Entry and Tagging Request Body (Check if any of the proposal statuses are NCCN or RCCN. If Yes, then do both Entry and Tagging, otherwise only Entry)
                            if (fetchPaymentDetails.journey_type.ToUpper().Contains("PORTABILITY"))
                            {
                                pfPolicyCreationReq = CreatePFWebhookEntryTaggingReqBody(true, false, fetchPaymentDetails);
                            }
                            else
                            {
                                if (uniqueIsNSTPFlagList.Contains("N"))
                                {
                                    pfPolicyCreationReq = CreatePFWebhookEntryTaggingReqBody(true, true, fetchPaymentDetails);
                                }
                                else
                                {
                                    pfPolicyCreationReq = CreatePFWebhookEntryTaggingReqBody(true, false, fetchPaymentDetails);
                                }
                            }

                            if (pfPolicyCreationReq == null)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                                    TraceId: trace_id,
                                    Message: "Error while creating request body for PF Policy Creation. Callback Request: " + callbackRequest + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.CALLBACK_LOGS);
                            }
                            else
                            {
                                // ILESB Payment Entry and Tagging API Call
                                pfPolicyCreationResp = await ilesbInterface.GeneratePFPolicyData(pfPolicyCreationReq, flow_type);
                                if (pfPolicyCreationResp == null)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                       MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                                       TraceId: trace_id,
                                       Message: "Error while calling Policy Creation API. Callback Request: " + callbackRequest + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq)), ApplicationConfig.CALLBACK_LOGS);
                                }

                                if (pfPolicyCreationResp != null && pfPolicyCreationResp.StatusMessage.ToUpper() == "SUCCESS")
                                {
                                    if (pfPolicyCreationResp.paymentEntryResponse != null && pfPolicyCreationResp.paymentEntryResponse.Status.ToUpper() == "SUCCESS")
                                    {
                                        pfPaymentId = pfPolicyCreationResp.paymentEntryResponse.PaymentID;
                                        await callbackDAL.UpdateCorePaymentID(pfPaymentId, fetchPaymentDetails.transaction_id);
                                    }
                                    if (pfPolicyCreationResp.paymentTagResponse != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList.Count > 0)
                                    {
                                        foreach (var item in pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList)
                                        {
                                            if (item.Status.ToUpper() == "SUCCESS" && !string.IsNullOrEmpty(item.PolicyNo) && item.PolicyNo != "0")
                                            {
                                                SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                                {
                                                    policy_number = item.PolicyNo,
                                                    payment_id = pfPaymentId,
                                                    proposal_number = item.ProposalNo,
                                                    installment_amount = fetchPaymentDetails.installment_amount,
                                                    installment_number = fetchPaymentDetails.installment_number,
                                                    reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                                                    trace_id = fetchPaymentDetails.trace_id,
                                                    transaction_id = fetchPaymentDetails.transaction_id,
                                                    application_name = fetchPaymentDetails.application_name,
                                                    channel_application_details = fetchPaymentDetails.channel_application_details,
                                                    auth_code = fetchPaymentDetails.auth_code,
                                                    sync_event = "POLICY_CONFIRMATION"
                                                };
                                                syncProposalToSourceList.Add(syncProposalToSource);
                                            }

                                            await callbackDAL.updatePaymentEntryTaggingDetails(item.ProposalNo, item.PolicyNo);
                                        }
                                    }
                                }
                            }
                        }
                        else if (fetchPaymentDetails.core.ToUpper() == "ARTEMIS")
                        {
                            if (fetchPaymentDetails.is_ilemi_proposal_mod_reqd && !string.IsNullOrEmpty(payment_type) && payment_type == "ILEMI")
                            {
                                isEMIProposalModified = await paymentBAL.EMIModifyProposal(trace_id,fetchPaymentDetails.proposal_id, flow_type);
                                await callbackDAL.UpdateILEMIProposalModifyStatus(isEMIProposalModified,trace_id, fetchPaymentDetails.proposal_id, transaction_id);

                                fetchPaymentDetails.total_amount = fetchPaymentDetails.installment_amount;

                                if (!isEMIProposalModified)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                                        TraceId: trace_id,
                                        Message: "EMIModifyProposal API failed to modify the proposal. Callback Request: " + callbackRequest + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.CALLBACK_LOGS);

                                    artemisPolicyCreationReq = null;
                                }
                                else
                                {
                                    // ARTEMIS
                                    // Create Payment Entry and Tagging Request Body (Check if any of the proposals are STP. If Yes, then do both Entry and Tagging, otherwise only Entry)
                                    if (fetchPaymentDetails.journey_type.ToUpper().Contains("PORTABILITY"))
                                    {
                                        artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                                    }
                                    else
                                    {
                                        if (uniqueIsNSTPFlagList.Contains("N"))
                                        {
                                            artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                                        }
                                        else
                                        {
                                            if (fetchPaymentDetails.payment_mode.ToUpper() == "ASBA")
                                            {
                                                artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                                            }
                                            else
                                            {
                                                artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // ARTEMIS
                                // Create Payment Entry and Tagging Request Body (Check if any of the proposals are STP. If Yes, then do both Entry and Tagging, otherwise only Entry)
                                if (fetchPaymentDetails.journey_type.ToUpper().Contains("PORTABILITY"))
                                {
                                    artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                                }
                                else
                                {
                                    if (uniqueIsNSTPFlagList.Contains("N"))
                                    {
                                        artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                                    }
                                    else
                                    {
                                        if (fetchPaymentDetails.payment_mode.ToUpper() == "ASBA")
                                        {
                                            artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                                        }
                                        else
                                        {
                                            artemisPolicyCreationReq = CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                                        }
                                    }
                                }
                            }

                            if (artemisPolicyCreationReq == null)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                                    TraceId: trace_id,
                                    Message: "Error while creating request body for Artemis Policy Creation. Callback Request: " + callbackRequest + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.CALLBACK_LOGS);
                            }
                            else
                            {
                                // API FACTORY Payment Entry and Tagging API Call
                                RestResponse apiResponse = await apiFactoryInterface.GenerateArtemisPolicyData(trace_id, artemisPolicyCreationReq, flow_type);

                                if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                                {
                                    artemisPolicyCreationResp = JsonConvert.DeserializeObject<ArtemisPolicyCreationResp>(apiResponse.Content);
                                }
                                else if (apiResponse.StatusCode == HttpStatusCode.GatewayTimeout && !string.IsNullOrEmpty(apiResponse.Content))
                                {
                                    artemisPolicyCreationRespTimeout = JsonConvert.DeserializeObject<ArtemisPolicyCreationRespTimeout>(apiResponse.Content);
                                }

                                if (artemisPolicyCreationResp == null && artemisPolicyCreationRespTimeout == null)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                       MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                                       TraceId: trace_id,
                                       Message: "Error while calling Artemis Policy Creation API. Callback Request: " + callbackRequest + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.CALLBACK_LOGS);
                                }
                                else
                                {
                                    if (artemisPolicyCreationResp != null && artemisPolicyCreationResp.data != null && artemisPolicyCreationResp.statusCode == 200)
                                    {
                                        if (artemisPolicyCreationResp.data.receiptStatus.ToUpper() == "SUCCESSFUL")
                                        {
                                            receiptNumber = artemisPolicyCreationResp.data.receiptNumber;
                                            await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);
                                        }
                                        if (artemisPolicyCreationResp.data.tagging != null && artemisPolicyCreationResp.data.tagging.Count > 0)
                                        {
                                            foreach (var item in artemisPolicyCreationResp.data.tagging)
                                            {
                                                if (!string.IsNullOrEmpty(item.policyNumber))
                                                {
                                                    SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                                    {
                                                        policy_number = item.policyNumber,
                                                        payment_id = receiptNumber,
                                                        proposal_number = item.proposalNumber,
                                                        installment_amount = fetchPaymentDetails.installment_amount,
                                                        installment_number = fetchPaymentDetails.installment_number,
                                                        reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                                                        trace_id = fetchPaymentDetails.trace_id,
                                                        transaction_id = fetchPaymentDetails.transaction_id,
                                                        application_name = fetchPaymentDetails.application_name,
                                                        channel_application_details = fetchPaymentDetails.channel_application_details,
                                                        auth_code = fetchPaymentDetails.auth_code,
                                                        sync_event = "POLICY_CONFIRMATION"
                                                    };
                                                    syncProposalToSourceList.Add(syncProposalToSource);
                                                }

                                                if (!string.IsNullOrEmpty(payment_type) && payment_type == "ILEMI")
                                                {
                                                    LatestEmiInstallment latestEmiInstallmentDetails = await orderDAL.GetLatestEmiInstallment(item.proposalNumber);
                                                    if (latestEmiInstallmentDetails != null && latestEmiInstallmentDetails.installment_number == fetchPaymentDetails.installment_number)
                                                    {
                                                        LatestEmiInstallment latestEMI = new LatestEmiInstallment
                                                        {
                                                            recurring_id = dps_order_id,
                                                            transaction_id = fetchPaymentDetails.transaction_id,
                                                            trace_id = fetchPaymentDetails.trace_id,
                                                            proposal_number = item.proposalNumber,
                                                            policy_number = item.policyNumber,
                                                            installment_number = fetchPaymentDetails.installment_number,
                                                            amount = fetchPaymentDetails.installment_amount,
                                                            status = "RAISED AND PAID",
                                                            payment_id = receiptNumber,
                                                            mandate_id = latestEmiInstallmentDetails.mandate_id,
                                                            dps_order_id = dps_order_id,
                                                            order_id = order_id,
                                                            //customer_id = fetchPaymentDetails.gateway_customer_id,
                                                            //application_name = latestEmiInstallmentDetails.application_name,
                                                            payment_mode = fetchPaymentDetails.payment_mode,
                                                            //due_date = latestEmiInstallmentDetails.due_date,
                                                            execution_date = latestEmiInstallmentDetails.execution_date,
                                                            //grace_end_date = ,
                                                            remarks = "ONLINE PROCESSED"
                                                        };

                                                        // Insert into database
                                                        await orderDAL.upsertEmiInstallmentDetails(latestEMI);
                                                    }

                                                    string recurring_id = await orderDAL.GetNextRecurringId();
                                                    int nextInstallmentNumber = Convert.ToInt32(fetchPaymentDetails.installment_number) + 1;

                                                    LatestEmiInstallment scheduleNextEMI =  new LatestEmiInstallment
                                                    {
                                                        recurring_id = recurring_id,
                                                        transaction_id = fetchPaymentDetails.transaction_id,
                                                        trace_id = fetchPaymentDetails.trace_id,
                                                        proposal_number = latestEmiInstallmentDetails.proposal_number,
                                                        //policy_number = "", // ITEM.POLICY_NUMBER
                                                        installment_number = nextInstallmentNumber.ToString(),
                                                        amount = fetchPaymentDetails.installment_amount,
                                                        status = "TO BE RAISED",
                                                        //payment_id = receiptNumber,
                                                        application_name = latestEmiInstallmentDetails.application_name,
                                                        mandate_id = latestEmiInstallmentDetails.mandate_id,
                                                        dps_order_id = recurring_id,
                                                        //order_id = order_id,
                                                        payment_mode = fetchPaymentDetails.payment_mode,
                                                        due_date = latestEmiInstallmentDetails.due_date.HasValue ? latestEmiInstallmentDetails.due_date.Value.AddMonths(1) : DateTime.Now.AddMonths(1),
                                                        customer_id = fetchPaymentDetails.gateway_customer_id,
                                                        //execution_date = DateTime.Now,
                                                        grace_end_date = latestEmiInstallmentDetails.due_date.HasValue ? latestEmiInstallmentDetails.due_date.Value.AddMonths(5) : DateTime.Now.AddMonths(5),
                                                        //remarks = "ONLINE PROCESSED"
                                                    };

                                                    // Insert into database
                                                    await orderDAL.upsertEmiInstallmentDetails(scheduleNextEMI);

                                                }

                                                await callbackDAL.updatePaymentEntryTaggingDetails(item.proposalNumber, item.policyNumber);
                                            }
                                        }
                                    }

                                    if (artemisPolicyCreationRespTimeout != null && artemisPolicyCreationRespTimeout.data != null && artemisPolicyCreationRespTimeout.statusCode == 504)
                                    {
                                        if (artemisPolicyCreationRespTimeout.data.taggingDetails != null && artemisPolicyCreationRespTimeout.data.taggingDetails.Count > 0)
                                        {
                                            foreach (var item in artemisPolicyCreationRespTimeout.data.taggingDetails)
                                            {
                                                if (!string.IsNullOrEmpty(item.taggedPolicyNumber) && !string.IsNullOrEmpty(item.taggedReceiptNumber))
                                                {
                                                    SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                                    {
                                                        policy_number = item.taggedPolicyNumber,
                                                        payment_id = item.taggedReceiptNumber,
                                                        proposal_number = item.taggedProposalNumber,
                                                        installment_amount = fetchPaymentDetails.installment_amount,
                                                        installment_number = fetchPaymentDetails.installment_number,
                                                        reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                                                        trace_id = fetchPaymentDetails.trace_id,
                                                        transaction_id = fetchPaymentDetails.transaction_id,
                                                        application_name = fetchPaymentDetails.application_name,
                                                        channel_application_details = fetchPaymentDetails.channel_application_details,
                                                        auth_code = fetchPaymentDetails.auth_code,
                                                        sync_event = "POLICY_CONFIRMATION"
                                                    };
                                                    syncProposalToSourceList.Add(syncProposalToSource);
                                                }

                                                await callbackDAL.UpdateCorePaymentID(item.taggedReceiptNumber, fetchPaymentDetails.transaction_id);
                                                await callbackDAL.updatePaymentEntryTaggingDetails(item.taggedProposalNumber, item.taggedPolicyNumber);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                                TraceId: trace_id,
                                Message: "Invalid Core fetched from DB. Callback Request: " + callbackRequest + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.CALLBACK_LOGS);
                            return;
                        }

                        await callbackDAL.UpdateNoOfAttemptsCountByTransactionID(transaction_id);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                foreach (var reverse_sync_proposal in syncProposalToSourceList)
                                {
                                    await SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                                }
                            }
                            catch (Exception ex)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() - Background SyncProposalToSource Policy Status",
                                    TraceId: fetchPaymentDetails.trace_id,
                                    Message: $"Exception occurred: {ex.Message}",
                                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackBAL.cs - CreatePolicyFromCallbackWebhook() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
        }

        public PFPolicyCreationReq CreatePFWebhookEntryTaggingReqBody(bool isEntryNeeded, bool isTaggingNeeded, FetchPaymentDetails fetchPaymentDetails)
        {
            PFPolicyCreationReq request = new PFPolicyCreationReq();
            DateFormatter dateFormatter = new DateFormatter();
            string productCode = string.Empty;
            DateTime createdDate;

            try
            {
                switch (fetchPaymentDetails.product)
                {
                    case "SME_1":
                        productCode = ApplicationConfig.MSME_PRODUCT_CODE;
                        break;
                    case "SME_2":
                        productCode = ApplicationConfig.CARI_PRODUCT_CODE;
                        break;
                    case "SME_3":
                        productCode = ApplicationConfig.PICA_PRODUCT_CODE;
                        break;
                    case "SME_4":
                        productCode = ApplicationConfig.PIDR_PRODUCT_CODE;
                        break;
                    case "SME_5":
                        productCode = ApplicationConfig.WC_PRODUCT_CODE;
                        break;
                    case "SME_6":
                        productCode = ApplicationConfig.MARINEINLO_PRODUCT_CODE;
                        break;
                    case "SME_7":
                        productCode = ApplicationConfig.MARINEIMPO_PRODUCT_CODE;
                        break;
                    case "SME_8":
                        productCode = ApplicationConfig.MARINEEXPO_PRODUCT_CODE;
                        break;
                    case "SME_9":
                        productCode = ApplicationConfig.MARINEINLS_PRODUCT_CODE;
                        break;
                    case "SME_10":
                        productCode = ApplicationConfig.MARINEIMPS_PRODUCT_CODE;
                        break;
                    case "SME_11":
                        productCode = ApplicationConfig.MARINEEXPS_PRODUCT_CODE;
                        break;
                    default:
                        break;
                }

                fetchPaymentDetails.auth_code = fetchPaymentDetails.auth_code.Replace("pay_", "");

                string created_on = fetchPaymentDetails.created_on;

                bool isValidParsedDate = dateFormatter.TryNormalizeDate(created_on, out DateTime parsedDate);
                if (!isValidParsedDate)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "CallbackBAL.cs - CreatePFWebhookEntryTaggingReqBody() ",
                       Message: "Invalid Created on. Created On: " + JsonConvert.SerializeObject(created_on),
                       TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.CALLBACK_LOGS);

                    return null;
                }
                createdDate = parsedDate;

                string formattedDate = createdDate.ToString("yyyy-MM-dd");

                request.CorrelationId = fetchPaymentDetails.trace_id;

                if (isEntryNeeded)
                {
                    request.PaymentEntry = new PFPaymentEntryReq
                    {
                        onlineDAEntry = new PFOnlineDAEntryReq
                        {
                            CustomerID = fetchPaymentDetails.fetchPaymentProposalList[0].customer_id,
                            AuthCode = fetchPaymentDetails.auth_code,
                            MerchantID = fetchPaymentDetails.pf_mid,
                            TransactionID = fetchPaymentDetails.trace_id,
                            PaymentAmount = fetchPaymentDetails.total_amount,
                            InstrumentDate = formattedDate,
                            ReceiptDate = formattedDate,
                            PayerType = fetchPaymentDetails.payer_type
                        },
                        ProductCode = productCode
                    };
                }
                else
                {
                    request.PaymentEntry = null;
                }

                if (!string.IsNullOrEmpty(fetchPaymentDetails.fetchPaymentProposalList[0].deal_id))
                {
                    request.DealID = fetchPaymentDetails.fetchPaymentProposalList[0].deal_id;
                    request.IntermediaryID = null;
                }
                else
                {
                    request.DealID = null;
                    request.IntermediaryID = fetchPaymentDetails.fetchPaymentProposalList[0].intermediary_code;
                }
                request.isMappingRequired = false;
                request.isTaggingRequired = isTaggingNeeded;

                if (isTaggingNeeded)
                {
                    List<PFCustomerProposalReq> customerProposal = new List<PFCustomerProposalReq>();
                    foreach (FetchPaymentProposalList fetchPaymentProposalList in fetchPaymentDetails.fetchPaymentProposalList)
                    {
                        if (fetchPaymentProposalList.is_nstp_flow == "N")
                        {
                            customerProposal.Add(new PFCustomerProposalReq
                            {
                                ProposalNo = fetchPaymentProposalList.proposal_number,
                                CustomerID = fetchPaymentProposalList.customer_id,
                            });
                        }
                    }
                    request.PaymentTagging = new PFPaymentMapTagReq
                    {
                        customerProposal = customerProposal,
                        PaymentID = isEntryNeeded ? null : fetchPaymentDetails.payment_id
                    };
                }
                else
                {
                    request.PaymentTagging = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackBAL.cs - CreatePFWebhookEntryTaggingReqBody() ",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return request;
        }

        public ArtemisPolicyCreationReq CreateArtemisWebhookEntryTaggingReqBody(bool isTaggingNeeded, FetchPaymentDetails fetchPaymentDetails)
        {
            ArtemisPolicyCreationReq request = new ArtemisPolicyCreationReq();
            CallbackDAL callbackDAL = new CallbackDAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            DateFormatter dateFormatter = new DateFormatter();

            DateTime createdDate;

            try
            {
                if (fetchPaymentDetails.payment_mode.ToUpper() == "ASBA")
                {
                    fetchPaymentDetails.auth_code = fetchPaymentDetails.mandate_id;
                }
                else
                {
                    fetchPaymentDetails.auth_code = fetchPaymentDetails.auth_code.Replace("pay_", "");
                }

                string created_on = fetchPaymentDetails.created_on;

                bool isValidParsedDate = dateFormatter.TryNormalizeDate(created_on, out DateTime parsedDate);
                if (!isValidParsedDate)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "CallbackBAL.cs - CreateArtemisWebhookEntryTaggingReqBody() ",
                       Message: "Invalid Created on. Created On: " + JsonConvert.SerializeObject(created_on),
                       TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.CALLBACK_LOGS);

                    return null;
                }
                createdDate = parsedDate;

                string formattedDate = createdDate.ToString("yyyy-MM-dd");

                request.mid = fetchPaymentDetails.artemis_mid;
                request.imDetails = new ArtemisImDetails
                {
                    imdCode = fetchPaymentDetails.fetchPaymentProposalList[0].intermediary_code,
                    marketingOfficer = fetchPaymentDetails.fetchPaymentProposalList[0].marketing_officer,
                    businessVertical = fetchPaymentDetails.fetchPaymentProposalList[0].business_vertical,
                    subVertical = fetchPaymentDetails.fetchPaymentProposalList[0].sub_vertical
                };

                request.receiptDetails = new ArtemisReceiptDetails
                {
                    subCode = ApplicationConfig.APIFACTORY_PAYMENT_SUB_CODE,
                    mode = ApplicationConfig.APIFACTORY_PAYMENT_MODE,
                    instrumentAmount = Convert.ToDecimal(fetchPaymentDetails.total_amount),
                    instrumentNumber = fetchPaymentDetails.auth_code,
                    instrumentDate = formattedDate,
                    officeCode = ApplicationConfig.APIFACTORY_PAYMENT_OFFICE_CODE,
                    onlineDetails = fetchPaymentDetails.payment_mode.ToUpper() == "ASBA" || fetchPaymentDetails.payment_mode.ToUpper() == "PAL" ? new onlineDetails
                    {
                        subModeOnline = fetchPaymentDetails.payment_mode.ToUpper() == "ASBA"
                            ? ApplicationConfig.APIFACTORY_PAYMENT_MODE_ASBA
                            : fetchPaymentDetails.payment_mode
                    } : null
                };

                if (fetchPaymentDetails.payer_type.ToUpper() == "I")
                {
                    request.billingPartyStake = ApplicationConfig.APIFACTORY_PAYMENT_PAYERTYPE_I;
                    request.receiptDetails.payerPayeeCode = fetchPaymentDetails.fetchPaymentProposalList[0].intermediary_code;
                }
                else
                {
                    request.billingPartyStake = ApplicationConfig.APIFACTORY_PAYMENT_PAYERTYPE_C;
                    request.receiptDetails.payerPayeeCode = fetchPaymentDetails.fetchPaymentProposalList[0].proposer_code;
                }

                if (isTaggingNeeded)
                {
                    List<ArtemisTaggingReq> artemisTagging = new List<ArtemisTaggingReq>();
                    foreach (FetchPaymentProposalList fetchPaymentProposalList in fetchPaymentDetails.fetchPaymentProposalList)
                    {
                        if (fetchPaymentProposalList.is_nstp_flow == "N" || fetchPaymentDetails.payment_mode.ToUpper() == "ASBA")
                        {
                            artemisTagging.Add(new ArtemisTaggingReq
                            {
                                proposalNumber = fetchPaymentProposalList.proposal_number,
                            });
                        }
                    }
                    request.tagging = artemisTagging;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackBAL.cs - CreateArtemisWebhookEntryTaggingReqBody() ",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return request;
        }

        public ArtemisPolicyTaggingOnlyReq CreateArtemisWebhookTaggingOnlyReqBody(FetchPaymentDetails fetchPaymentDetails)
        {
            ArtemisPolicyTaggingOnlyReq artemisPolicyTaggingOnlyReq = new ArtemisPolicyTaggingOnlyReq();

            try
            {
                foreach (FetchPaymentProposalList proposal in fetchPaymentDetails.fetchPaymentProposalList)
                {
                    artemisPolicyTaggingOnlyReq.tagging.Add(new ArtemisTaggingOnlyReq
                    {
                        receiptNumber = fetchPaymentDetails.payment_id,
                        proposalNumber = proposal.proposal_number
                    });
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackBAL.cs - CreateArtemisWebhookTaggingOnlyReqBody() ",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return artemisPolicyTaggingOnlyReq;
        }

        public async Task<string> SyncProposalToSource(SyncProposalToSource syncProposalToSource, string flow_type, string journey_type)
        {
            CallbackDAL callbackDAL = new CallbackDAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusDAL plutusDAL = new PlutusDAL();

            string channel_application_id = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            string is_payment_resync_sent = string.Empty;
            bool isSuccess = true;

            try
            {
                if (string.IsNullOrEmpty(syncProposalToSource.trace_id) || string.IsNullOrEmpty(syncProposalToSource.transaction_id) || string.IsNullOrEmpty(syncProposalToSource.reverse_sync_url) || string.IsNullOrEmpty(syncProposalToSource.application_name))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                          MethodName: "CallbackBAL.cs - SyncProposalToSource() ",
                          TraceId: syncProposalToSource.trace_id,
                          Message: "Could not reverse sync the data as trace id, transaction id, reverse sync url and application_name cannot be empty. Request: " + JsonConvert.SerializeObject(syncProposalToSource)), ApplicationConfig.CALLBACK_LOGS);

                    return "N";
                }

                is_payment_resync_sent = await plutusDAL.GetIsPaymentResyncSent(syncProposalToSource.trace_id);

                if (!string.IsNullOrEmpty(is_payment_resync_sent) && is_payment_resync_sent.ToUpper() != "Y" && journey_type != "ADVANCE")
                {
                    SyncProposalToSource payment_syncProposalToSource = new SyncProposalToSource()
                    {
                        reverse_sync_url = syncProposalToSource.reverse_sync_url,
                        trace_id = syncProposalToSource.trace_id,
                        transaction_id = syncProposalToSource.transaction_id,
                        application_name = syncProposalToSource.application_name,
                        channel_application_details = syncProposalToSource.channel_application_details,
                        auth_code = syncProposalToSource.auth_code,
                        sync_event = "PAYMENT_CONFIRMATION"
                    };

                    string payment_url = payment_syncProposalToSource.reverse_sync_url;
                    switch (payment_syncProposalToSource.application_name)
                    {
                        case "MSME":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "ILTCMotorEndorsement":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "ILTakeCare":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "MOTOR_CV_RENEWAL":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "DIGITAL_WEBSITE":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "HEALTH":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "HOME":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "TRAVEL":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "MISC":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        //case "SME":
                        //    url = + url;
                        //    break;
                        case "MotorILTC":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "MotorWebsite":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                    }

                    var payment_client = new RestClient(payment_url);
                    var payment_request = new RestRequest
                    {
                        Method = Method.Post
                    };

                    string payment_token = await FetchSyncProposalToSourceToken(payment_syncProposalToSource.application_name, payment_syncProposalToSource.trace_id, flow_type);
                    if (!string.IsNullOrEmpty(payment_token))
                    {
                        switch (payment_syncProposalToSource.application_name)
                        {
                            case "MSME":
                                payment_request.AddHeader("Authorization", "Bearer " + payment_token);
                                break;
                            case "ILTCMotorEndorsement":
                                payment_request.AddHeader("CommonRedirectionAuthorization", "Bearer " + payment_token);
                                break;
                            case "ILTakeCare":
                                payment_request.AddHeader("CommonRedirectionAuthorization", "Bearer " + payment_token);
                                break;
                            case "MOTOR_CV_RENEWAL":
                                payment_request.AddHeader("CommonRedirectionAuthorization", "Bearer " + payment_token);
                                break;
                            case "DIGITAL_WEBSITE":
                                break;
                            //case "HEALTH":
                            //    url = + url;
                            //    break;
                            case "HOME":
                                break;
                            case "TRAVEL":
                                break;
                            case "MISC":
                                break;
                            //case "SME":
                            //    url = + url;
                            //    break;
                            case "MotorILTC":
                                break;
                            case "MotorWebsite":
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(payment_syncProposalToSource.channel_application_details))
                    {
                        JsonElement parsedContent = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(payment_syncProposalToSource.channel_application_details);
                        if (parsedContent.TryGetProperty("ID", out JsonElement idElement))
                        {
                            channel_application_id = idElement.GetString();
                        }
                        payment_request.AddHeader("ID", channel_application_id);
                    }

                    payment_request.AddHeader("Content-Type", "application/json");
                    payment_request.AddParameter("application/json", JsonConvert.SerializeObject(payment_syncProposalToSource), ParameterType.RequestBody);

                    stopwatch = Stopwatch.StartNew();

                    var payment_apiResponse = await payment_client.ExecuteAsync(payment_request);

                    stopwatch.Stop();
                    var payment_responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(payment_syncProposalToSource.trace_id, payment_syncProposalToSource.proposal_number, JsonConvert.SerializeObject(payment_syncProposalToSource), payment_apiResponse.Content, payment_url, "SYNC TO SOURCE", JsonConvert.SerializeObject(payment_apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(payment_responseTime));

                    if (payment_apiResponse.StatusCode == HttpStatusCode.OK)
                    {
                        await plutusDAL.SetIsPaymentResyncSent(payment_syncProposalToSource.trace_id, "Y");
                    }
                    else
                    {
                        isSuccess = false;
                    }
                }


                if (isSuccess)
                {
                    string url = syncProposalToSource.reverse_sync_url;
                    switch (syncProposalToSource.application_name)
                    {
                        case "MSME":
                            url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + url;
                            break;
                        case "ILTCMotorEndorsement":
                            url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + url;
                            break;
                        case "ILTakeCare":
                            url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + url;
                            break;
                        case "MOTOR_CV_RENEWAL":
                            url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + url;
                            break;
                        case "DIGITAL_WEBSITE":
                            url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + url;
                            break;
                        case "HEALTH":
                            url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + url;
                            break;
                        case "HOME":
                            url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + url;
                            break;
                        case "TRAVEL":
                            url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + url;
                            break;
                        case "MISC":
                            url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + url;
                            break;
                        //case "SME":
                        //    url = + url;
                        //    break;
                        case "MotorILTC":
                            url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + url;
                            break;
                        case "MotorWebsite":
                            url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + url;
                            break;
                    }

                    var client = new RestClient(url);
                    var request = new RestRequest
                    {
                        Method = Method.Post
                    };

                    string token = await FetchSyncProposalToSourceToken(syncProposalToSource.application_name, syncProposalToSource.trace_id, flow_type);
                    if (!string.IsNullOrEmpty(token))
                    {
                        switch (syncProposalToSource.application_name)
                        {
                            case "MSME":
                                request.AddHeader("Authorization", "Bearer " + token);
                                break;
                            case "ILTCMotorEndorsement":
                                request.AddHeader("CommonRedirectionAuthorization", "Bearer " + token);
                                break;
                            case "ILTakeCare":
                                request.AddHeader("CommonRedirectionAuthorization", "Bearer " + token);
                                break;
                            case "MOTOR_CV_RENEWAL":
                                request.AddHeader("CommonRedirectionAuthorization", "Bearer " + token);
                                break;
                            case "DIGITAL_WEBSITE":
                                break;
                            //case "HEALTH":
                            //    url = + url;
                            //    break;
                            case "HOME":
                                break;
                            case "TRAVEL":
                                break;
                            case "MISC":
                                break;
                            //case "SME":
                            //    url = + url;
                            //    break;
                            case "MotorILTC":
                                break;
                            case "MotorWebsite":
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(syncProposalToSource.channel_application_details))
                    {
                        JsonElement parsedContent = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(syncProposalToSource.channel_application_details);
                        if (parsedContent.TryGetProperty("ID", out JsonElement idElement))
                        {
                            channel_application_id = idElement.GetString();
                        }
                        request.AddHeader("ID", channel_application_id);
                    }

                    request.AddHeader("Content-Type", "application/json");
                    request.AddParameter("application/json", JsonConvert.SerializeObject(syncProposalToSource), ParameterType.RequestBody);

                    stopwatch = Stopwatch.StartNew();

                    var apiResponse = await client.ExecuteAsync(request);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(syncProposalToSource.trace_id, syncProposalToSource.proposal_number, JsonConvert.SerializeObject(syncProposalToSource), apiResponse.Content, url, "SYNC TO SOURCE", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                    if (apiResponse.StatusCode == HttpStatusCode.OK)
                    {
                        if (syncProposalToSource.sync_event.ToUpper() == "PAYMENT_CONFIRMATION" || syncProposalToSource.sync_event.ToUpper() == "PAYMENT_FAILED")
                        {
                            await plutusDAL.SetIsPaymentResyncSent(syncProposalToSource.trace_id, "Y");
                        }
                        else if (syncProposalToSource.sync_event.ToUpper() == "POLICY_CONFIRMATION")
                        {
                            await callbackDAL.UpdateReverseSyncFlag(syncProposalToSource);
                        }
                        return "Y";
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                  MethodName: "CallbackBAL.cs - SyncProposalToSource() ",
                  TraceId: syncProposalToSource.trace_id,
                  Message: $"Exception occurred: {ex.Message}",
                  StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return "N";
        }

        public async Task<string> PaymentSyncProposalToSource(SyncProposalToSource syncProposalToSource, string flow_type, string journey_type)
        {
            CallbackDAL callbackDAL = new CallbackDAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusDAL plutusDAL = new PlutusDAL();

            string channel_application_id = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            string is_payment_resync_sent = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(syncProposalToSource.trace_id) || string.IsNullOrEmpty(syncProposalToSource.transaction_id) || string.IsNullOrEmpty(syncProposalToSource.reverse_sync_url) || string.IsNullOrEmpty(syncProposalToSource.application_name))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                          MethodName: "CallbackBAL.cs - SyncProposalToSource() ",
                          TraceId: syncProposalToSource.trace_id,
                          Message: "Could not reverse sync the data as trace id, transaction id, reverse sync url and application_name cannot be empty. Request: " + JsonConvert.SerializeObject(syncProposalToSource)), ApplicationConfig.CALLBACK_LOGS);

                    return "N";
                }

                is_payment_resync_sent = await plutusDAL.GetIsPaymentResyncSent(syncProposalToSource.trace_id);

                if (!string.IsNullOrEmpty(is_payment_resync_sent) && is_payment_resync_sent.ToUpper() != "Y" && journey_type != "ADVANCE")
                {
                    SyncProposalToSource payment_syncProposalToSource = new SyncProposalToSource()
                    {
                        reverse_sync_url = syncProposalToSource.reverse_sync_url,
                        trace_id = syncProposalToSource.trace_id,
                        transaction_id = syncProposalToSource.transaction_id,
                        application_name = syncProposalToSource.application_name,
                        channel_application_details = syncProposalToSource.channel_application_details,
                        auth_code = syncProposalToSource.auth_code,
                        sync_event = "PAYMENT_CONFIRMATION"
                    };

                    string payment_url = payment_syncProposalToSource.reverse_sync_url;
                    switch (payment_syncProposalToSource.application_name)
                    {
                        case "MSME":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "ILTCMotorEndorsement":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "ILTakeCare":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "MOTOR_CV_RENEWAL":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "DIGITAL_WEBSITE":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "HEALTH":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "HOME":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "TRAVEL":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        case "MISC":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                        //case "SME":
                        //    url = + url;
                        //    break;
                        case "MotorILTC":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("ILTCDomain") + payment_url;
                            break;
                        case "MotorWebsite":
                            payment_url = AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + payment_url;
                            break;
                    }

                    var payment_client = new RestClient(payment_url);
                    var payment_request = new RestRequest
                    {
                        Method = Method.Post
                    };

                    string payment_token = await FetchSyncProposalToSourceToken(payment_syncProposalToSource.application_name, payment_syncProposalToSource.trace_id, flow_type);
                    if (!string.IsNullOrEmpty(payment_token))
                    {
                        switch (payment_syncProposalToSource.application_name)
                        {
                            case "MSME":
                                payment_request.AddHeader("Authorization", "Bearer " + payment_token);
                                break;
                            case "ILTCMotorEndorsement":
                                payment_request.AddHeader("CommonRedirectionAuthorization", "Bearer " + payment_token);
                                break;
                            case "ILTakeCare":
                                payment_request.AddHeader("CommonRedirectionAuthorization", "Bearer " + payment_token);
                                break;
                            case "MOTOR_CV_RENEWAL":
                                payment_request.AddHeader("CommonRedirectionAuthorization", "Bearer " + payment_token);
                                break;
                            case "DIGITAL_WEBSITE":
                                break;
                            //case "HEALTH":
                            //    url = + url;
                            //    break;
                            case "HOME":
                                break;
                            case "TRAVEL":
                                break;
                            case "MISC":
                                break;
                            //case "SME":
                            //    url = + url;
                            //    break;
                            case "MotorILTC":
                                break;
                            case "MotorWebsite":
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(payment_syncProposalToSource.channel_application_details))
                    {
                        JsonElement parsedContent = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(payment_syncProposalToSource.channel_application_details);
                        if (parsedContent.TryGetProperty("ID", out JsonElement idElement))
                        {
                            channel_application_id = idElement.GetString();
                        }
                        payment_request.AddHeader("ID", channel_application_id);
                    }

                    payment_request.AddHeader("Content-Type", "application/json");
                    payment_request.AddParameter("application/json", JsonConvert.SerializeObject(payment_syncProposalToSource), ParameterType.RequestBody);

                    stopwatch = Stopwatch.StartNew();

                    var payment_apiResponse = await payment_client.ExecuteAsync(payment_request);

                    stopwatch.Stop();
                    var payment_responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(payment_syncProposalToSource.trace_id, payment_syncProposalToSource.proposal_number, JsonConvert.SerializeObject(payment_syncProposalToSource), payment_apiResponse.Content, payment_url, "SYNC TO SOURCE", JsonConvert.SerializeObject(payment_apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(payment_responseTime));

                    if (payment_apiResponse.StatusCode == HttpStatusCode.OK)
                    {
                        await plutusDAL.SetIsPaymentResyncSent(payment_syncProposalToSource.trace_id, "Y");
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                  MethodName: "CallbackBAL.cs - SyncProposalToSource() ",
                  TraceId: syncProposalToSource.trace_id,
                  Message: $"Exception occurred: {ex.Message}",
                  StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return "N";
        }

        public void ScheduleExtractOrderDataWebhookFallback()
        {
            try
            {
                BackgroundJob.Enqueue(() => ExtractOrderDataWebhookFallback("HANGFIRE - WEBHOOK FALLBACK"));
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackBAL.cs - ScheduleExtractOrderDataWebhookFallback() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }
        }

        public async Task ExtractOrderDataWebhookFallback(string flow_type)
        {
            OrderBAL orderBAL = new OrderBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            JsonElement jsonOrderStatusResponse = new JsonElement();
            List<FetchWebhookFallbackCases> webhookFallbackCasesList = new List<FetchWebhookFallbackCases>();

            // Order Details
            string order_id = string.Empty;
            string auth_code = string.Empty;
            string udf9 = string.Empty;
            string trace_id = string.Empty;
            string transaction_id = string.Empty;
            string dps_order_id = string.Empty;
            string pg_status = string.Empty;
            string payment_status = string.Empty;
            string mandate_id = string.Empty;
            string payment_mode = string.Empty;
            string is_webhook_fallback_processing = string.Empty;

            try
            {
                webhookFallbackCasesList = await callbackDAL.GetWebhookFallbackRetryCases();

                foreach (FetchWebhookFallbackCases webhookFallbackCase in webhookFallbackCasesList)
                {
                    is_webhook_fallback_processing = await callbackDAL.GetIsWebhookFallbackProcessingFlag(webhookFallbackCase.dps_order_id);
                    if (is_webhook_fallback_processing?.ToUpper() == "Y")
                    {
                        continue;
                    }

                    await callbackDAL.SetIsWebhookFallbackProcessingFlag("Y", webhookFallbackCase.dps_order_id);

                    fetchOrderStatusReq = new FetchOrderStatusReq
                    {
                        dps_order_id = webhookFallbackCase.dps_order_id,
                        trace_id = webhookFallbackCase.trace_id,

                    };

                    var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, webhookFallbackCase.application_name, flow_type);

                    var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                    if (orderData != null)
                    {
                        order_id = orderData.order_id;
                        auth_code = orderData.auth_code;
                        trace_id = orderData.trace_id;
                        transaction_id = orderData.transaction_id;
                        dps_order_id = orderData.dps_order_id;
                        pg_status = orderData.status;
                        payment_status = orderData.payment_status;
                        mandate_id = orderData.mandate_id;
                        payment_mode = orderData.payment_mode;
                    }

                    if (string.IsNullOrEmpty(order_id) || string.IsNullOrEmpty(auth_code) || string.IsNullOrEmpty(trace_id) || string.IsNullOrEmpty(transaction_id) || string.IsNullOrEmpty(payment_status))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "CallbackBAL.cs - ExtractOrderDataWebhookFallback() ",
                            TraceId: trace_id,
                            Message: "Invalid Order Status details. Webhook Fallback Case Request: " + webhookFallbackCase), ApplicationConfig.CALLBACK_LOGS);

                        return;
                    }

                    if (payment_status != "SUCCESS")
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "CallbackBAL.cs - ExtractOrderDataWebhookFallback() ",
                           TraceId: trace_id,
                           Message: "Transaction not successful (Status is not SUCCESS). Webhook Fallback Case Request: " + webhookFallbackCase), ApplicationConfig.CALLBACK_LOGS);

                        return;
                    }

                    await callbackDAL.UpdateWebhookFallbackAttemptCount(webhookFallbackCase.webhook_fallback_attempt_count + 1, webhookFallbackCase.dps_order_id);

                    await callbackDAL.LogPaymentDetails(order_id, auth_code, transaction_id, trace_id, dps_order_id, payment_status, mandate_id, payment_mode);

                    await callbackDAL.SetIsWebhookFallbackProcessingFlag("N", webhookFallbackCase.dps_order_id);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CallbackBAL.cs - ExtractOrderDataWebhookFallback()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return;
        }

        public async Task<string> FetchSyncProposalToSourceToken(string application_name, string trace_id, string flow_type)
        {
            ReverseSyncToken reverseSyncToken = new ReverseSyncToken();
            string token = string.Empty;

            try
            {
                switch (application_name)
                {
                    case "MSME":
                        token = await reverseSyncToken.GetSMEAuthorizationToken(trace_id, flow_type);
                        break;
                    case "ILTCMotorEndorsement":
                        token = await reverseSyncToken.GetILTCAuthorizationToken(trace_id, ApplicationConfig.MOTOR_NON_NIL_TOKEN_REVERSE_SYNC_USERNAME, ApplicationConfig.MOTOR_NON_NIL_TOKEN_REVERSE_SYNC_PASSWORD, flow_type);
                        break;
                    case "ILTakeCare":
                        token = await reverseSyncToken.GetILTCAuthorizationToken(trace_id, ApplicationConfig.ILTC_TOKEN_REVERSE_SYNC_USERNAME, ApplicationConfig.ILTC_TOKEN_REVERSE_SYNC_PASSWORD, flow_type);
                        break;
                    case "MOTOR_CV_RENEWAL":
                        token = await reverseSyncToken.GetILTCAuthorizationToken(trace_id, ApplicationConfig.ILTC_TOKEN_REVERSE_SYNC_USERNAME, ApplicationConfig.ILTC_TOKEN_REVERSE_SYNC_PASSWORD, flow_type);
                        break;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                     MethodName: "CallbackBAL.cs - FetchSyncProposalToSourceToken()",
                     TraceId: trace_id,
                     Message: $"Exception occurred: {ex.Message}",
                     StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return token;
        }

        public async Task<DPSStatusResponse> ChannelApplicationOrderDetails(ChannelApplicationOrderDetailsReq webhookOrderDetailsReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            CallbackDAL callbackDAL = new CallbackDAL();
            OrderBAL orderBAL = new OrderBAL();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            DPSStatusResponse orderStatusResponse = new DPSStatusResponse();
            JsonElement jsonOrderStatusResponse = new JsonElement();
            ChannelApplicationOrderDetailsResp webhookOrderDetailsResp = new ChannelApplicationOrderDetailsResp();

            string dps_order_id = string.Empty;

            try
            {
                webhookOrderDetailsResp = await callbackDAL.ChannelApplicationOrderDetails(webhookOrderDetailsReq);

                if (webhookOrderDetailsResp != null)
                {
                    if (!string.IsNullOrEmpty(webhookOrderDetailsResp.dps_order_id))
                    {

                        fetchOrderStatusReq = new FetchOrderStatusReq
                        {
                            dps_order_id = webhookOrderDetailsResp.dps_order_id,
                            trace_id = webhookOrderDetailsResp.trace_id
                        };

                        orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, webhookOrderDetailsResp.application_name, flow_type);

                        var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                        if (orderData != null)
                        {
                            webhookOrderDetailsResp.pg_status = orderData.status;
                            webhookOrderDetailsResp.payment_status = orderData.payment_status;
                            webhookOrderDetailsResp.payment_received_on = orderData.payment_received_on;
                            webhookOrderDetailsResp.payment_mode = orderData.payment_mode;
                        }
                    }
                    else
                    {
                        webhookOrderDetailsResp.pg_status = "NEW";
                        webhookOrderDetailsResp.payment_status = "NEW";
                    }

                    if ((webhookOrderDetailsResp.pg_status.ToUpper() == "CHARGED" || webhookOrderDetailsResp.pg_status.ToUpper() == "SUCCESS") && webhookOrderDetailsResp.application_name == "DIGITAL_WEBSITE")
                    {
                        webhookOrderDetailsResp.pg_status = "CAPTURED";
                    }
                    if ((webhookOrderDetailsResp.pg_status.ToUpper() == "CHARGED" || webhookOrderDetailsResp.pg_status.ToUpper() == "SUCCESS") && webhookOrderDetailsResp.application_name == "HOME")
                    {
                        webhookOrderDetailsResp.pg_status = "CAPTURED";
                    }
                    if ((webhookOrderDetailsResp.pg_status.ToUpper() == "CHARGED" || webhookOrderDetailsResp.pg_status.ToUpper() == "SUCCESS") && webhookOrderDetailsResp.application_name == "MOTOR")
                    {
                        webhookOrderDetailsResp.pg_status = "CAPTURED";
                    }
                    if ((webhookOrderDetailsResp.pg_status.ToUpper() == "CHARGED" || webhookOrderDetailsResp.pg_status.ToUpper() == "SUCCESS") && webhookOrderDetailsResp.application_name == "TRAVEL")
                    {
                        webhookOrderDetailsResp.pg_status = "CAPTURED";
                    }
                    if ((webhookOrderDetailsResp.pg_status.ToUpper() == "CHARGED" || webhookOrderDetailsResp.pg_status.ToUpper() == "SUCCESS") && webhookOrderDetailsResp.application_name == "HEALTH")
                    {
                        webhookOrderDetailsResp.pg_status = "CAPTURED";
                    }
                    if (!string.IsNullOrEmpty(webhookOrderDetailsResp.pg_status) && !string.IsNullOrEmpty(webhookOrderDetailsResp.payment_status))
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Data Fetched Successfully";
                        response.TraceId = webhookOrderDetailsResp.trace_id;
                        response.Data = webhookOrderDetailsResp;
                    }
                    else
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Could not fetch pg order status against this Proposal number";
                        response.TraceId = webhookOrderDetailsResp.trace_id;
                        response.Data = null;
                    }
                }
                else
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Could not fetch data against this Proposal number";
                    response.TraceId = webhookOrderDetailsResp.trace_id;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                     MethodName: "CallbackBAL.cs - WebhookOrderDetails()",
                     TraceId: webhookOrderDetailsResp.trace_id,
                     Message: $"Exception occurred: {ex.Message}",
                     StackTrace: ex.StackTrace), ApplicationConfig.CALLBACK_LOGS);
            }

            return response;
        }
    }
}
