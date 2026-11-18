using Amazon.CloudWatchLogs;
using Amazon.Runtime.Telemetry.Tracing;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Hangfire;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Data;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RefundModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;
namespace DigitalPaymentSystem_ILTC.BAL
{
    public class RefundBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task ScheduleRefundSync()
        {
            try
            {
                RefundDAL refundDAL = new RefundDAL();
                var refundCases = await refundDAL.FetchRefundCases();
                if (refundCases == null || refundCases.Count == 0)
                    return;

                foreach (var refundCase in refundCases)
                {
                    BackgroundJob.Enqueue(() => RefundSync("HANGFIRE - SCHEDULE REFUND SYNC"));
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - ScheduleRefundSync() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }
        }

        public async Task RefundSync(string flow_type)
        {
            RefundDAL refundDAL = new RefundDAL();
            List<FetchRefundCase> fetchRefundCaseList = new List<FetchRefundCase>();
            bool refundCaseExists = true;

            try
            {
                fetchRefundCaseList = await refundDAL.FetchRefundCases();
                if (fetchRefundCaseList == null && fetchRefundCaseList.Count <= 0)
                {
                    return;
                }
                string is_refund_sync_processing = string.Empty;

                foreach (FetchRefundCase refundCase in fetchRefundCaseList)
                {
                    //refundCaseExists = await refundDAL.CheckRefundCaseExists(refundCase.proposal_number);
                    //if (!refundCaseExists)
                    //{
                    //    await refundDAL.InsertRefundDetails(refundCase);
                    //}

                    is_refund_sync_processing = await refundDAL.GetIsRefundSyncProcessingFlag(refundCase.proposal_number);
                    if (is_refund_sync_processing?.ToUpper() == "Y")
                    {
                        continue;
                    }

                    await refundDAL.SetIsRefundSyncProcessingFlag("Y", refundCase.proposal_number);

                    await InitiateRefund(refundCase, flow_type);

                    await refundDAL.SetIsRefundSyncProcessingFlag("N", refundCase.proposal_number);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - RefundSync() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }

            return;
        }

        public async Task<DPSStatusResponse> ValidateManualRefund(RefundReq refundReq, string application_name, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            RefundDAL refundDAL = new RefundDAL();
            OrderBAL orderBAL = new OrderBAL();

            FetchRefundDetails fetchRefundDetails = new FetchRefundDetails();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();

            string order_status = string.Empty;

            List<RefundDetail> refunds = new List<RefundDetail>();
            FetchOrderStatusResp orderData = new FetchOrderStatusResp();

            try
            {
                fetchRefundDetails = await refundDAL.FetchRefundDetails(refundReq.transaction_id, refundReq.proposal_number, refundReq.auth_code);
                if (fetchRefundDetails != null && fetchRefundDetails.IsValid())
                {
                    var dbAmount = Convert.ToDecimal(fetchRefundDetails.proposal_amount);
                    var reqAmount = Convert.ToDecimal(refundReq.proposal_amount);

                    if (fetchRefundDetails.refund_status == "SUCCESS" || fetchRefundDetails.refund_status == "OFFLINE")
                    {
                        RefundResp refundResp = new RefundResp
                        {
                            refund_status = "SUCCESS"
                        };

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Refund already completed";
                        response.TraceId = fetchRefundDetails.trace_id;
                        response.Data = refundResp;
                        return response;
                    }

                    fetchOrderStatusReq = new FetchOrderStatusReq
                    {
                        dps_order_id = fetchRefundDetails.dps_order_id,
                        trace_id = fetchRefundDetails.trace_id
                    };

                    var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, fetchRefundDetails.application_name, flow_type);

                    orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                    if (orderData != null)
                    {
                        order_status = orderData.payment_status;
                        refunds = orderData.refunds;
                    }

                    if (string.IsNullOrEmpty(order_status) || (!string.IsNullOrEmpty(order_status) && order_status.ToUpper() != "SUCCESS"))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "RefundBAL.cs - ValidateManualRefund() ",
                            TraceId: fetchRefundDetails.trace_id,
                            Message: "Invalid Order Status / Status is not Success. Request: " + refundReq + " Refund Details: " + fetchRefundDetails + " Order Data: " + JsonConvert.SerializeObject(orderData)), ApplicationConfig.REFUND_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid Order Status / Status is not Success";
                        response.TraceId = fetchRefundDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (refunds != null && refunds.Count > 0)
                    {
                        var matchingRefund = refunds.FirstOrDefault(r => r.unique_request_id == fetchRefundDetails.refund_id);
                        if (matchingRefund != null)
                        {
                            if (matchingRefund.status?.Equals("FAILURE", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                // Refund is FAILURE so proceed with new refund initiation
                            }
                            else if (matchingRefund.status?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "RefundBAL.cs - ValidateManualRefund() ",
                                  TraceId: fetchRefundDetails.trace_id,
                                  Message: "Refund completed. Request: " + refundReq + " Refund Details: " + fetchRefundDetails), ApplicationConfig.REFUND_LOGS);

                                await refundDAL.UpdateRefundStatus(fetchRefundDetails.trace_id, refundReq.proposal_number, "SUCCESS");

                                RefundResp refundResp = new RefundResp
                                {
                                    refund_status = "SUCCESS"
                                };

                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = "Refund completed";
                                response.TraceId = fetchRefundDetails.trace_id;
                                response.Data = refundResp;
                                return response;
                            }
                            else if (matchingRefund.status?.Equals("PENDING", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "RefundBAL.cs - ValidateManualRefund() ",
                                  TraceId: fetchRefundDetails.trace_id,
                                  Message: "Refund has already been initiated. Status: PENDING. Request: " + refundReq + " Refund Details: " + fetchRefundDetails), ApplicationConfig.REFUND_LOGS);

                                await refundDAL.UpdateRefundStatus(fetchRefundDetails.trace_id, refundReq.proposal_number, "PENDING");

                                RefundResp refundResp = new RefundResp
                                {
                                    refund_status = "PENDING"
                                };

                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = "Refund already initiated";
                                response.TraceId = fetchRefundDetails.trace_id;
                                response.Data = refundResp;
                                return response;
                            }
                            else if (matchingRefund.status?.Equals("MANUAL_REVIEW", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "RefundBAL.cs - ValidateManualRefund() ",
                                  TraceId: fetchRefundDetails.trace_id,
                                  Message: "Refund still under processing. Manual Review Required. Request: " + refundReq + " Refund Details: " + fetchRefundDetails), ApplicationConfig.REFUND_LOGS);

                                await refundDAL.UpdateRefundStatus(fetchRefundDetails.trace_id, refundReq.proposal_number, "MANUAL_REV");

                                RefundResp refundResp = new RefundResp
                                {
                                    refund_status = "MANUAL_REVIEW"
                                };

                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = "Refund still under processing. Manual Review Required";
                                response.TraceId = fetchRefundDetails.trace_id;
                                response.Data = refundResp;
                                return response;
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "RefundBAL.cs - ValidateManualRefund() ",
                                  TraceId: fetchRefundDetails.trace_id,
                                  Message: "Unique request id does not match with refund id for this proposal number. Request: " + refundReq + " Refund Details: " + fetchRefundDetails), ApplicationConfig.REFUND_LOGS);
                        }
                    }

                    if (Math.Abs(dbAmount - reqAmount) > 2)
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Proposal Amount mismatch in input and DB";
                        response.TraceId = fetchRefundDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (refundReq.auth_code != fetchRefundDetails.auth_code)
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Auth Code mismatch in input and DB";
                        response.TraceId = fetchRefundDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (string.IsNullOrEmpty(fetchRefundDetails.refund_proposal_number))
                    {
                        await refundDAL.InsertRefundDetails(fetchRefundDetails, refundReq);
                    }

                    FetchRefundCase fetchRefundCase = new FetchRefundCase
                    {
                        proposal_number = refundReq.proposal_number,
                        auth_code = refundReq.auth_code,
                        policy_number = fetchRefundDetails.policy_number,
                        proposal_amount = refundReq.proposal_amount,
                        canc_proposal_number = refundReq.canc_proposal_number,
                        transaction_id = refundReq.transaction_id,
                        trace_id = fetchRefundDetails.trace_id,
                        core = fetchRefundDetails.core,
                        dps_order_id = fetchRefundDetails.dps_order_id,
                        channel_application_details = fetchRefundDetails.channel_application_details,
                        product = fetchRefundDetails.product,
                        application_name = fetchRefundDetails.application_name,
                        payment_id = fetchRefundDetails.payment_id,
                        proposal_start_date = fetchRefundDetails.proposal_start_date,
                        payer_type = fetchRefundDetails.payer_type,
                        customer_id = fetchRefundDetails.customer_id,
                        voucher_number = fetchRefundDetails.voucher_number,
                        refund_status = fetchRefundDetails.refund_status,
                        refund_id = fetchRefundDetails.refund_id
                    };

                    response = await InitiateManualRefund(fetchRefundCase, flow_type, order_status, orderData);
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "RefundBAL.cs - ValidateManualRefund() ",
                        TraceId: fetchRefundDetails?.trace_id,
                        Message: "Unable to fetch Details from DB. Refund Req: " + JsonConvert.SerializeObject(refundReq)), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Data Not Found";
                    response.TraceId = fetchRefundDetails?.trace_id;
                    response.Data = null;
                }

            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - ValidateManualRefund() ",
                    TraceId: fetchRefundDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = fetchRefundDetails.trace_id;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> InitiateManualRefund(FetchRefundCase fetchRefundCase, string flow_type, string order_status, FetchOrderStatusResp orderData)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            RefundDAL refundDAL = new RefundDAL();
            OrderBAL orderBAL = new OrderBAL();
            OrderDAL orderDAL = new OrderDAL();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            ILESBInterface ilesbInterface = new ILESBInterface();

            FetchApplicationDetails fetchApplicationDetails = new FetchApplicationDetails();

            try
            {
                if (fetchRefundCase.core.ToUpper() == "PF")
                {
                    if (string.IsNullOrEmpty(fetchRefundCase.voucher_number))
                    {
                        fetchRefundCase.voucher_number = await ilesbInterface.CreatePremiumRefundILESBVoucher(fetchRefundCase.trace_id, fetchRefundCase.proposal_number, fetchRefundCase.canc_proposal_number, fetchRefundCase.payer_type, flow_type);
                    }
                }
                else if (fetchRefundCase.core.ToUpper() == "ARTEMIS")
                {
                    // Artemis Create Covernote

                    //isArtemisCovernoteGenerated = await CreateArtemisCovernote(fetchRefundDetails.trace_id, refundReq.proposal_number);
                    //if (!isArtemisCovernoteGenerated)
                    //{
                    //    response.StatusCode = 0;
                    //    response.StatusMessage = "Failed";
                    //    response.Message = "Covernote Generation Failed for Given Proposal Number";
                    //    response.TraceId = fetchRefundDetails.trace_id;
                    //    response.Data = null;
                    //    return response;
                    //}
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RefundBAL.cs - InitiateManualRefund() ",
                       TraceId: fetchRefundCase.trace_id,
                       Message: "Invalid Core fetched from DB. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid Core for given Proposal Number";
                    response.TraceId = fetchRefundCase.trace_id;
                    response.Data = null;
                    return response;
                }

                if (string.IsNullOrEmpty(fetchRefundCase.voucher_number))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RefundBAL.cs - InitiateManualRefund() ",
                       TraceId: fetchRefundCase.trace_id,
                       Message: "Voucher Generation Failed for Given Proposal Number. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Voucher Generation Failed for Given Proposal Number";
                    response.TraceId = fetchRefundCase.trace_id;
                    response.Data = null;
                    return response;
                }
                else
                {
                    fetchApplicationDetails = await refundDAL.GetApplicationDetails(fetchRefundCase.application_name);
                    if (fetchApplicationDetails == null || (fetchApplicationDetails != null && string.IsNullOrEmpty(fetchApplicationDetails.juspay_mid) && string.IsNullOrEmpty(fetchApplicationDetails.reverse_sync_url)))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "RefundBAL.cs - InitiateManualRefund() ",
                            TraceId: fetchRefundCase.trace_id,
                            Message: "Unable to fetch Application Details for the Given Application. Application Name: " + fetchRefundCase.application_name), ApplicationConfig.REFUND_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unable to fetch Application Details for the Given Application";
                        response.TraceId = fetchRefundCase.trace_id;
                        response.Data = null;
                        return response;
                    }

                    response = await CallJuspayRefund(fetchRefundCase.transaction_id, fetchRefundCase.proposal_number, fetchRefundCase.trace_id, fetchRefundCase.proposal_amount, fetchApplicationDetails, fetchRefundCase.payment_id, fetchRefundCase.application_name, fetchRefundCase.dps_order_id, fetchRefundCase.channel_application_details, fetchRefundCase.auth_code, flow_type, fetchRefundCase.policy_number, fetchRefundCase.voucher_number);
                    return response;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - InitiateManualRefund() ",
                    TraceId: fetchRefundCase.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = fetchRefundCase.trace_id;
                response.Data = null;
                return response;
            }
        }

        public async Task<DPSStatusResponse> InitiateRefund(FetchRefundCase fetchRefundCase, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            RefundDAL refundDAL = new RefundDAL();
            OrderBAL orderBAL = new OrderBAL();
            OrderDAL orderDAL = new OrderDAL();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            ILESBInterface ilesbInterface = new ILESBInterface();

            FetchApplicationDetails fetchApplicationDetails = new FetchApplicationDetails();
            JsonElement jsonOrderStatusResponse = new JsonElement();
            string order_status = string.Empty;

            List<RefundDetail> refunds = new List<RefundDetail>();

            try
            {
                fetchApplicationDetails = await refundDAL.GetApplicationDetails(fetchRefundCase.application_name);
                if (fetchApplicationDetails == null || (fetchApplicationDetails != null && string.IsNullOrEmpty(fetchApplicationDetails.juspay_mid) && string.IsNullOrEmpty(fetchApplicationDetails.reverse_sync_url)))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "RefundBAL.cs - InitiateRefund() ",
                        TraceId: fetchRefundCase.trace_id,
                        Message: "Unable to fetch Application Details for the Given Application. Application Name: " + fetchRefundCase.application_name), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Unable to fetch Application Details for the Given Application";
                    response.TraceId = fetchRefundCase.trace_id;
                    response.Data = null;
                    return response;
                }

                fetchOrderStatusReq = new FetchOrderStatusReq
                {
                    dps_order_id = fetchRefundCase.dps_order_id,
                    trace_id = fetchRefundCase.trace_id
                };

                var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, fetchRefundCase.application_name, flow_type);

                var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                if (orderData != null)
                {
                    order_status = orderData.payment_status;
                    refunds = orderData.refunds;
                }

                if (string.IsNullOrEmpty(order_status) || (!string.IsNullOrEmpty(order_status) && order_status.ToUpper() != "SUCCESS"))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "RefundBAL.cs - InitiateRefund() ",
                        TraceId: fetchRefundCase.trace_id,
                        Message: "Invalid Order Status / Status is not Success. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid Order Status / Status is not Success";
                    response.TraceId = fetchRefundCase.trace_id;
                    response.Data = null;
                    return response;
                }

                if (refunds != null && refunds.Count > 0)
                {
                    var matchingRefund = refunds.FirstOrDefault(r => r.unique_request_id == fetchRefundCase.refund_id);
                    if (matchingRefund != null)
                    {
                        if (matchingRefund.status?.Equals("FAILURE", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            // Refund is FAILURE so proceed with new refund initiation
                        }
                        else if (matchingRefund.status?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                              MethodName: "RefundBAL.cs - InitiateRefund() ",
                              TraceId: fetchRefundCase.trace_id,
                              Message: "Refund completed. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);

                            await refundDAL.UpdateRefundStatus(fetchRefundCase.trace_id, fetchRefundCase.proposal_number, "SUCCESS");

                            RefundResp refundResp = new RefundResp
                            {
                                refund_status = "SUCCESS"
                            };

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Refund completed";
                            response.TraceId = fetchRefundCase.trace_id;
                            response.Data = refundResp;
                            return response;
                        }
                        else if (matchingRefund.status?.Equals("PENDING", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                              MethodName: "RefundBAL.cs - InitiateRefund() ",
                              TraceId: fetchRefundCase.trace_id,
                              Message: "Refund has already been initiated. Status: PENDING. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);

                            await refundDAL.UpdateRefundStatus(fetchRefundCase.trace_id, fetchRefundCase.proposal_number, "PENDING");

                            RefundResp refundResp = new RefundResp
                            {
                                refund_status = "PENDING"
                            };

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Refund already initiated";
                            response.TraceId = fetchRefundCase.trace_id;
                            response.Data = refundResp;
                            return response;
                        }
                        else if (matchingRefund.status?.Equals("MANUAL_REVIEW", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                              MethodName: "RefundBAL.cs - InitiateRefund() ",
                              TraceId: fetchRefundCase.trace_id,
                              Message: "Refund still under processing. Manual Review Required. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);

                            await refundDAL.UpdateRefundStatus(fetchRefundCase.trace_id, fetchRefundCase.proposal_number, "MANUAL_REV");

                            RefundResp refundResp = new RefundResp
                            {
                                refund_status = "MANUAL_REVIEW"
                            };

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Refund still under processing. Manual Review Required";
                            response.TraceId = fetchRefundCase.trace_id;
                            response.Data = refundResp;
                            return response;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                              MethodName: "RefundBAL.cs - InitiateRefund() ",
                              TraceId: fetchRefundCase.trace_id,
                              Message: "Unique request id does not match with refund id for this proposal number. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);
                    }
                }


                if (fetchRefundCase.core.ToUpper() == "PF")
                {
                    if (string.IsNullOrEmpty(fetchRefundCase.voucher_number))
                    {
                        fetchRefundCase.voucher_number = await ilesbInterface.CreatePaymentRefundILESBVoucher(fetchRefundCase.trace_id, fetchRefundCase.proposal_number, fetchRefundCase.payment_id, fetchRefundCase.customer_id, fetchRefundCase.payer_type, flow_type);
                    }
                }
                else if (fetchRefundCase.core.ToUpper() == "ARTEMIS")
                {
                    // Artemis Create Covernote

                    //isArtemisCovernoteGenerated = await CreateArtemisCovernote(fetchRefundDetails.trace_id, refundReq.proposal_number);
                    //if (!isArtemisCovernoteGenerated)
                    //{
                    //    response.StatusCode = 0;
                    //    response.StatusMessage = "Failed";
                    //    response.Message = "Covernote Generation Failed for Given Proposal Number";
                    //    response.TraceId = fetchRefundDetails.trace_id;
                    //    response.Data = null;
                    //    return response;
                    //}
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RefundBAL.cs - InitiateRefund() ",
                       TraceId: fetchRefundCase.trace_id,
                       Message: "Invalid Core fetched from DB. Request: " + fetchRefundCase), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid Core for given Proposal Number";
                    response.TraceId = fetchRefundCase.trace_id;
                    response.Data = null;
                    return response;
                }

                if (string.IsNullOrEmpty(fetchRefundCase.voucher_number))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RefundBAL.cs - InitiateRefund() ",
                       TraceId: fetchRefundCase.trace_id,
                       Message: "Voucher Generation Failed for Given Proposal Number. Request: " + fetchRefundCase + " Order Status Details " + orderStatusResponse), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Voucher Generation Failed for Given Proposal Number";
                    response.TraceId = fetchRefundCase.trace_id;
                    response.Data = null;
                    return response;
                }
                else
                {
                    response = await CallJuspayRefund(fetchRefundCase.transaction_id, fetchRefundCase.proposal_number, fetchRefundCase.trace_id, fetchRefundCase.proposal_amount, fetchApplicationDetails, fetchRefundCase.payment_id, fetchRefundCase.application_name, fetchRefundCase.dps_order_id, fetchRefundCase.channel_application_details, fetchRefundCase.auth_code, flow_type, fetchRefundCase.policy_number, fetchRefundCase.voucher_number);
                    return response;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - InitiateRefund() ",
                    TraceId: fetchRefundCase.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = fetchRefundCase.trace_id;
                response.Data = null;
                return response;
            }
        }

        public async Task<DPSStatusResponse> CallJuspayRefund(string transactionId, string proposalNumber, string traceId, string amount, FetchApplicationDetails fetchApplicationDetails, string payment_id, string application_name, string dpsOrderId, string channel_application_details, string auth_code, string flow_type, string policy_number, string voucher_number)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            CallbackBAL callbackBAL = new CallbackBAL();
            RefundDAL refundDAL = new RefundDAL();
            FetchBreakInDetailsByProposalNo fetchBreakInDetailsByProposalNo = new FetchBreakInDetailsByProposalNo();
            JuspayRefundRequest juspayRefundRequest = new JuspayRefundRequest();

            string refundUrl = string.Empty;
            string refund_status = string.Empty;
            string refund_id = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                refund_id = await refundDAL.InitiateRefundTransaction(proposalNumber);
                if (string.IsNullOrEmpty(refund_id))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "RefundBAL.cs - CallJuspayRefund() ",
                               Message: "Could not create refund id. Proposal Number: " + proposalNumber,
                               TraceId: traceId), ApplicationConfig.REFUND_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Could not create refund id";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                refundUrl = ApplicationConfig.JUSPAY_REFUND_API_BASE_URL;
                var url = refundUrl.Replace("##ORDERID##", dpsOrderId);

                juspayRefundRequest = new JuspayRefundRequest
                {
                    unique_request_id = refund_id,
                    amount = amount,
                    order_id = dpsOrderId
                };

                var client = new RestClient(url);
                var request = new RestRequest
                {
                    Method = Method.Post
                };

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-merchantid", fetchApplicationDetails.juspay_mid);
                request.AddHeader("Authorization", "Basic " + ApplicationConfig.JUSPAY_AUTH_TOKEN);
                request.AddParameter("application/json", JsonConvert.SerializeObject(juspayRefundRequest), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                var apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, proposalNumber, JsonConvert.SerializeObject(juspayRefundRequest), apiResponse.Content, url, "JUSPAY REFUND", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - CallJuspayRefund() ",
                    TraceId: traceId,
                    Message: "JUSPAY REFUND. Proposal Number: " + proposalNumber + " Request: " + JsonConvert.SerializeObject(juspayRefundRequest) + " Response: " + apiResponse.Content + " URL: " + url + " Status Code: " + JsonConvert.SerializeObject(apiResponse.StatusCode)), ApplicationConfig.REFUND_LOGS);

                if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Content) && apiResponse.StatusCode == HttpStatusCode.OK)
                {
                    JsonElement jsonRefundResponse = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(apiResponse.Content);

                    if (jsonRefundResponse.TryGetProperty("refunds", out JsonElement refundsElement) && refundsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement refund in refundsElement.EnumerateArray())
                        {
                            if (refund.TryGetProperty("unique_request_id", out JsonElement uniqueRequestId) &&
                                uniqueRequestId.GetString() == refund_id)
                            {
                                if (refund.TryGetProperty("status", out JsonElement refundStatus))
                                {
                                    refund_status = refundStatus.GetString();
                                }
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(refund_status))
                    {
                        //update refund status in DB
                        if (refund_status == "PENDING")
                        {
                            await refundDAL.UpdateRefundStatus(traceId, proposalNumber, "PENDING");

                            RefundResp refundResp = new RefundResp
                            {
                                refund_status = "PENDING"
                            };

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Refund Initiated Successfully";
                            response.TraceId = traceId;
                            response.Data = refundResp;
                            return response;
                        }
                        else if (refund_status == "SUCCESS")
                        {
                            await refundDAL.UpdateRefundStatus(traceId, proposalNumber, "SUCCESS");

                            RefundResp refundResp = new RefundResp
                            {
                                refund_status = "SUCCESS"
                            };

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Refund Completed Successfully";
                            response.TraceId = traceId;
                            response.Data = refundResp;
                            return response;
                        }
                        else if (refund_status == "FAILURE" || refund_status == "FAILED")
                        {
                            await refundDAL.UpdateRefundStatus(traceId, proposalNumber, "FAILED");

                            RefundResp refundResp = new RefundResp
                            {
                                refund_status = "FAILURE"
                            };

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Refund failed";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }
                        else if (refund_status == "MANUAL_REVIEW")
                        {
                            await refundDAL.UpdateRefundStatus(traceId, proposalNumber, "MANUAL_REV");

                            RefundResp refundResp = new RefundResp
                            {
                                refund_status = "MANUAL_REVIEW"
                            };

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Refund under processing. Manual Review Required";
                            response.TraceId = traceId;
                            response.Data = refundResp;
                            return response;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "RefundBAL.cs - CallJuspayRefund() ",
                                Message: "Unknown refund status. Request: " + JsonConvert.SerializeObject(juspayRefundRequest) + " Refund Status: " + refund_status,
                                TraceId: traceId), ApplicationConfig.REFUND_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unknown refund status";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }
                    }
                }
                else if (apiResponse != null && apiResponse.StatusCode == HttpStatusCode.GatewayTimeout)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                              MethodName: "RefundBAL.cs - CallJuspayRefund() ",
                              Message: "Refund under processing. Juspay Api Gateway Timeout. Proposal Number: " + proposalNumber + " ",
                              TraceId: traceId), ApplicationConfig.REFUND_LOGS);

                    //response.StatusCode = 1;
                    //response.StatusMessage = "Success";
                    //response.Message = "Refund Under Processing";
                    //response.TraceId = traceId;
                    //response.Data = null;
                    //return response;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - CallJuspayRefund() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.REFUND_LOGS);
            }

            response.StatusCode = 0;
            response.StatusMessage = "Failed";
            response.Message = "Refund Processing Failed";
            response.TraceId = traceId;
            response.Data = null;
            return response;
        }
    }
}
