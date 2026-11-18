using Amazon.CloudWatchLogs;
using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Hangfire;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using RestSharp;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.BreakInModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PlutusModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;
namespace DigitalPaymentSystem_ILTC.BAL
{
    public class RetryResyncBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public void ScheduleSTPRetryResync()
        {
            try
            {
                BackgroundJob.Enqueue(() => STPRetryResync("HANGFIRE - SCHEDULE STP RETRY RESYNC"));
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncBAL.cs - ScheduleSTPRetryResync() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }
        }

        public async Task<DPSStatusResponse> STPRetryResync(string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            OrderBAL orderBAL = new OrderBAL();
            PaymentBAL paymentBAL = new PaymentBAL();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();
            List<RetryResyncCase> retryResyncCasesList = new List<RetryResyncCase>();

            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            JsonElement jsonOrderStatusResponse = new JsonElement();

            string order_status = string.Empty;

            string is_dashboard_processing = string.Empty;
            string is_retryresync_processing = string.Empty;
            List<string> is_processing = new List<string>();

            List<RetryResyncCaseResp> retryResyncCaseRespList = new List<RetryResyncCaseResp>();

            try
            {
                retryResyncCasesList = await retryResyncDAL.GetSTPRetryResyncCases();

                if (retryResyncCasesList == null && retryResyncCasesList.Count <= 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "No Cases To Retry/Resync";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                foreach (RetryResyncCase retryResyncCase in retryResyncCasesList)
                {
                    is_processing = await retryResyncDAL.GetIsProcessingFlag(retryResyncCase.proposal_number);
                    is_retryresync_processing = is_processing[0];
                    is_dashboard_processing = is_processing[1];
                    if (is_retryresync_processing.ToUpper() == "Y" || is_dashboard_processing.ToUpper() == "Y")
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Another process is already running for this Proposal Number. Please try after sometime.";
                        response.TraceId = null;
                        response.Data = null;

                        RetryResyncCaseResp retryResyncCaseResp_error = new RetryResyncCaseResp
                        {
                            proposal_number = retryResyncCase.proposal_number,
                            status = response.Message
                        };
                        retryResyncCaseRespList.Add(retryResyncCaseResp_error);

                        continue;
                    }

                    fetchOrderStatusReq = new FetchOrderStatusReq
                    {
                        dps_order_id = retryResyncCase.dps_order_id,
                        trace_id = retryResyncCase.trace_id
                    };

                    var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, retryResyncCase.application_name, flow_type);

                    var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                    if (orderData != null)
                    {
                        order_status = orderData.payment_status;
                    }

                    if (string.IsNullOrEmpty(order_status) || (!string.IsNullOrEmpty(order_status) && order_status.ToUpper() != "SUCCESS"))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "RetryResyncBAL.cs - STPRetryResync() ",
                           TraceId: retryResyncCase.trace_id,
                           Message: "Transaction not successful (Status is not SUCCESS or Status is empty). Dashboard Clear Case Request: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.RETRY_RESYNC_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unknown Order Status";
                        response.TraceId = retryResyncCase.trace_id;
                        response.Data = null;

                        RetryResyncCaseResp retryResyncCaseResp_error = new RetryResyncCaseResp
                        {
                            proposal_number = retryResyncCase.proposal_number,
                            status = response.Message
                        };
                        retryResyncCaseRespList.Add(retryResyncCaseResp_error);

                        continue;
                    }

                    string proposal_status = await paymentBAL.FetchProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, retryResyncCase.core, flow_type);
                    if (string.IsNullOrEmpty(proposal_status) || (!string.IsNullOrEmpty(proposal_status) && ApplicationConfig.NCN_PROPOSAL_STATUS_ARRAY.Contains(proposal_status.ToUpper())))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "RetryResyncBAL.cs - STPRetryResync() ",
                           TraceId: retryResyncCase.trace_id,
                           Message: "Proposal Status returned empty or proposal cancelled. Dashboard Clear Case Request: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.RETRY_RESYNC_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Proposal Cancelled";
                        response.TraceId = retryResyncCase.trace_id;
                        response.Data = null;

                        RetryResyncCaseResp retryResyncCaseResp_error = new RetryResyncCaseResp
                        {
                            proposal_number = retryResyncCase.proposal_number,
                            status = response.Message
                        };
                        retryResyncCaseRespList.Add(retryResyncCaseResp_error);

                        continue;
                    }
                    else
                    {
                        retryResyncCase.proposal_status = proposal_status;
                    }

                    await retryResyncDAL.SetIsRetryResyncProcessing("Y", retryResyncCase.proposal_number);

                    string status = await ClearSTPRetryResyncTransactionCase(retryResyncCase, flow_type);

                    RetryResyncCaseResp retryResyncCaseResp = new RetryResyncCaseResp
                    {
                        proposal_number = retryResyncCase.proposal_number,
                        status = status
                    };

                    retryResyncCaseRespList.Add(retryResyncCaseResp);

                    await retryResyncDAL.UpdateNoOfAttemptsCount(retryResyncCase.no_of_attempts + 1, retryResyncCase.proposal_number);

                    await retryResyncDAL.SetIsRetryResyncProcessing("N", retryResyncCase.proposal_number);
                }

                response.StatusCode = 1;
                response.StatusMessage = "Success";
                response.Message = "Retry/Resync Process Completed";
                response.TraceId = null;
                response.Data = retryResyncCaseRespList;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncBAL.cs - STPRetryResync()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<string> ClearSTPRetryResyncTransactionCase(RetryResyncCase retryResyncCase, string flow_type)
        {
            OrderBAL orderBAL = new OrderBAL();
            OrderDAL orderDAL = new OrderDAL();
            PaymentBAL paymentBAL = new PaymentBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();

            string proposal_status = string.Empty;
            string is_porting_done = string.Empty;
            bool isEMIProposalModified = false;

            try
            {
                if (retryResyncCase.is_ilemi_proposal_mod_reqd && !retryResyncCase.is_ilemi_proposal_modified) 
                {
                    isEMIProposalModified = await paymentBAL.EMIModifyProposal(retryResyncCase.trace_id, retryResyncCase.proposal_id, flow_type);
                    await callbackDAL.UpdateILEMIProposalModifyStatus(isEMIProposalModified, retryResyncCase.trace_id, retryResyncCase.proposal_id, retryResyncCase.transaction_id);
                }

                if (!retryResyncCase.is_ilemi_proposal_mod_reqd || (retryResyncCase.is_ilemi_proposal_mod_reqd && isEMIProposalModified) || retryResyncCase.is_ilemi_proposal_modified)
                {
                    if (string.IsNullOrEmpty(retryResyncCase.payment_id))
                    {
                        // Payment ID Generation
                        retryResyncCase.payment_id = await RetryResyncGeneratePaymentId(retryResyncCase, flow_type);
                    }
                }

                if (!string.IsNullOrEmpty(retryResyncCase.payment_id))
                {

                    if (string.IsNullOrEmpty(retryResyncCase.policy_number))
                    {
                        if (retryResyncCase.product.ToUpper().Contains("HEALTH") && retryResyncCase.journey_type.ToUpper() == "PORTABILITY")
                        {
                            is_porting_done = await retryResyncDAL.GetPortingUpdate(retryResyncCase.trace_id, retryResyncCase.proposal_number);
                            if (is_porting_done != "1")
                            {
                                return "Porting Not Approved from QC";
                            }
                        }

                        // Policy Number Generation
                        retryResyncCase.policy_number = await RetryResyncTagProposal(retryResyncCase, flow_type);
                    }

                    if (!string.IsNullOrEmpty(retryResyncCase.policy_number))
                    {
                        if (retryResyncCase.resync_status == "N")
                        {
                            // Sync To Source
                            retryResyncCase.resync_status = await RetryResyncSyncToSource(retryResyncCase, flow_type);
                        }

                        if (retryResyncCase.resync_status == "Y")
                        {
                            return "Case Cleared Successfully";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "RetryResyncBAL.cs - ClearSTPRetryResyncTransactionCase()",
                   TraceId: retryResyncCase.trace_id,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return "Case Not Cleared";
        }

        public void ScheduleNSTPRetryResync()
        {
            try
            {
                BackgroundJob.Enqueue(() => NSTPRetryResync("HANGFIRE - SCHEDULE NSTP RETRY RESYNC"));
                BackgroundJob.Enqueue(() => UpdateAsbaPaymentId("HANGFIRE - SCHEDULE UPDATE ASBA PAYMENT ID"));
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncBAL.cs - ScheduleNSTPRetryResync() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }
        }

        public async Task<DPSStatusResponse> NSTPRetryResync(string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentBAL paymentBAL = new PaymentBAL();
            OrderBAL orderBAL = new OrderBAL();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();
            List<RetryResyncCase> retryResyncCasesList = new List<RetryResyncCase>();

            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            JsonElement jsonOrderStatusResponse = new JsonElement();

            string order_status = string.Empty;

            string is_dashboard_processing = string.Empty;
            string is_retryresync_processing = string.Empty;
            List<string> is_processing = new List<string>();

            List<RetryResyncCaseResp> retryResyncCaseRespList = new List<RetryResyncCaseResp>();

            try
            {
                retryResyncCasesList = await retryResyncDAL.GetNSTPRetryResyncCases();

                if (retryResyncCasesList == null && retryResyncCasesList.Count <= 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "No Cases To Retry/Resync";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                foreach (RetryResyncCase retryResyncCase in retryResyncCasesList)
                {
                    is_processing = await retryResyncDAL.GetIsProcessingFlag(retryResyncCase.proposal_number);
                    is_retryresync_processing = is_processing[0];
                    is_dashboard_processing = is_processing[1];
                    if (is_retryresync_processing.ToUpper() == "Y" || is_dashboard_processing.ToUpper() == "Y")
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Another process is already running for this Proposal Number. Please try after sometime.";
                        response.TraceId = null;
                        response.Data = null;

                        RetryResyncCaseResp retryResyncCaseResp_error = new RetryResyncCaseResp
                        {
                            proposal_number = retryResyncCase.proposal_number,
                            status = response.Message
                        };
                        retryResyncCaseRespList.Add(retryResyncCaseResp_error);

                        continue;
                    }

                    fetchOrderStatusReq = new FetchOrderStatusReq
                    {
                        dps_order_id = retryResyncCase.dps_order_id,
                        trace_id = retryResyncCase.trace_id
                    };

                    var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, retryResyncCase.application_name, flow_type);

                    var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                    if (orderData != null)
                    {
                        order_status = orderData.payment_status;
                    }

                    if (string.IsNullOrEmpty(order_status) || (!string.IsNullOrEmpty(order_status) && order_status.ToUpper() != "SUCCESS"))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "RetryResyncBAL.cs - NSTPRetryResync() ",
                           TraceId: retryResyncCase.trace_id,
                           Message: "Transaction not successful (Status is not SUCCESS or Status is empty). Dashboard Clear Case Request: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.RETRY_RESYNC_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unknown Order Status";
                        response.TraceId = retryResyncCase.trace_id;
                        response.Data = null;

                        RetryResyncCaseResp retryResyncCaseResp_error = new RetryResyncCaseResp
                        {
                            proposal_number = retryResyncCase.proposal_number,
                            status = response.Message
                        };
                        retryResyncCaseRespList.Add(retryResyncCaseResp_error);

                        continue;
                    }

                    string proposal_status = await paymentBAL.FetchProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, retryResyncCase.core, flow_type);
                    if (string.IsNullOrEmpty(proposal_status) || (!string.IsNullOrEmpty(proposal_status) && ApplicationConfig.NCN_PROPOSAL_STATUS_ARRAY.Contains(proposal_status.ToUpper())))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "RetryResyncBAL.cs - NSTPRetryResync() ",
                           TraceId: retryResyncCase.trace_id,
                           Message: "Proposal Status returned empty or proposal cancelled. Dashboard Clear Case Request: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.RETRY_RESYNC_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Proposal Cancelled";
                        response.TraceId = retryResyncCase.trace_id;
                        response.Data = null;

                        RetryResyncCaseResp retryResyncCaseResp_error = new RetryResyncCaseResp
                        {
                            proposal_number = retryResyncCase.proposal_number,
                            status = response.Message
                        };
                        retryResyncCaseRespList.Add(retryResyncCaseResp_error);

                        continue;
                    }
                    else
                    {
                        retryResyncCase.proposal_status = proposal_status;
                    }

                    await retryResyncDAL.SetIsRetryResyncProcessing("Y", retryResyncCase.proposal_number);

                    string status = await ClearNSTPRetryResyncTransactionCase(retryResyncCase, flow_type);

                    RetryResyncCaseResp retryResyncCaseResp = new RetryResyncCaseResp
                    {
                        proposal_number = retryResyncCase.proposal_number,
                        status = status
                    };

                    retryResyncCaseRespList.Add(retryResyncCaseResp);

                    await retryResyncDAL.SetIsRetryResyncProcessing("N", retryResyncCase.proposal_number);
                }

                response.StatusCode = 1;
                response.StatusMessage = "Success";
                response.Message = "Retry/Resync Process Completed";
                response.TraceId = null;
                response.Data = retryResyncCaseRespList;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncBAL.cs - NSTPRetryResync()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<string> ClearNSTPRetryResyncTransactionCase(RetryResyncCase retryResyncCase, string flow_type)
        {
            OrderBAL orderBAL = new OrderBAL();
            OrderDAL orderDAL = new OrderDAL();
            PaymentBAL paymentBAL = new PaymentBAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();

            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();

            string proposal_status = string.Empty;
            string is_porting_done = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(retryResyncCase.payment_id))
                {
                    // Payment ID Generation
                    retryResyncCase.payment_id = await RetryResyncGeneratePaymentId(retryResyncCase, flow_type);
                }

                if (!string.IsNullOrEmpty(retryResyncCase.payment_id))
                {
                    if (retryResyncCase.product.Contains("HEALTH") && retryResyncCase.core.ToUpper() == "ARTEMIS" && retryResyncCase.payment_mode.ToUpper() != "ASBA")
                    {
                        if (string.IsNullOrEmpty(retryResyncCase.policy_number))
                        {
                            fetchArtemisProposalDetails = await apiFactoryInterface.FetchArtemisProposalDetails(retryResyncCase.proposal_number, retryResyncCase.trace_id, retryResyncCase.application_name, flow_type);
                            if (fetchArtemisProposalDetails != null)
                            {
                                if (!string.IsNullOrEmpty(fetchArtemisProposalDetails.policy_number))
                                {
                                    // Fetched Policy Number From Artemis API
                                    retryResyncCase.policy_number = fetchArtemisProposalDetails.policy_number;

                                    await retryResyncDAL.UpdatePolicyNoForProposalNo(retryResyncCase.policy_number, retryResyncCase.proposal_number);
                                }
                                else
                                {
                                    if (fetchArtemisProposalDetails.policy_status.ToUpper() == "REJECTED")
                                    {
                                        await paymentDAL.updateProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, "REJECTED");
                                        return "Policy Rejected";
                                    }
                                    else if (fetchArtemisProposalDetails.policy_status.ToUpper() == "DRAFT" && fetchArtemisProposalDetails.policy_sub_status.ToUpper() == "COUNTEROFFER RAISED")
                                    {
                                        await paymentDAL.updateProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, "COUNTEROFFER RAISED");
                                        return "Counter Offer Raised";
                                    }
                                    else
                                    {
                                        if (retryResyncCase.journey_type.ToUpper() == "PORTABILITY")
                                        {
                                            is_porting_done = await retryResyncDAL.GetPortingUpdate(retryResyncCase.trace_id, retryResyncCase.proposal_number);
                                            if (is_porting_done != "1")
                                            {
                                                return "Porting Not Approved from QC";
                                            }
                                        }

                                        // Policy Number Generation
                                        retryResyncCase.policy_number = await RetryResyncTagProposal(retryResyncCase, flow_type);
                                    }
                                }
                            }
                            else
                            {
                                return "Proposal Details Could Not be Fetched";
                            }
                        }

                        if (!string.IsNullOrEmpty(retryResyncCase.policy_number))
                        {
                            if (retryResyncCase.resync_status == "N")
                            {
                                // Sync To Source
                                retryResyncCase.resync_status = await RetryResyncSyncToSource(retryResyncCase, flow_type);
                            }

                            if (retryResyncCase.resync_status == "Y")
                            {
                                return "Case Cleared Successfully";
                            }
                        }
                    }
                    else if (retryResyncCase.product.Contains("MOTOR") && ApplicationConfig.NPA_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()))
                    {
                        if (string.IsNullOrEmpty(retryResyncCase.ias_id))
                        {
                            // IAS Clearance
                            retryResyncCase.ias_id = await RetryResyncIasClearance(retryResyncCase, flow_type);
                        }

                        if (!string.IsNullOrEmpty(retryResyncCase.ias_id))
                        {
                            proposal_status = await paymentBAL.FetchProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, retryResyncCase.core, flow_type);
                            if (!string.IsNullOrEmpty(proposal_status))
                            {
                                retryResyncCase.proposal_status = proposal_status;
                            }
                            else
                            {
                                return "Proposal Status Could Not be Fetched";
                            }

                            if (ApplicationConfig.NPVI_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()))
                            {
                                if (string.IsNullOrEmpty(retryResyncCase.breakin_id))
                                {
                                    // BreakIn ID Generation
                                    await orderDAL.UpdateIsBreakInReq(retryResyncCase.proposal_number);
                                    retryResyncCase.breakin_id = await RetryResyncGenerateBreakIn(retryResyncCase, flow_type);
                                }

                                if (!string.IsNullOrEmpty(retryResyncCase.breakin_id))
                                {
                                    if (!string.IsNullOrEmpty(retryResyncCase.breakin_status) && retryResyncCase.breakin_status == "RECOMMENDED" && string.IsNullOrEmpty(retryResyncCase.breakin_clear_inspection_status))
                                    {
                                        // BreakIn Clearance
                                        retryResyncCase.breakin_clear_inspection_status = await RetryResyncBreakInClearance(retryResyncCase, flow_type);
                                    }

                                    if (!string.IsNullOrEmpty(retryResyncCase.breakin_status) && retryResyncCase.breakin_status == "RECOMMENDED" && !string.IsNullOrEmpty(retryResyncCase.breakin_clear_inspection_status) && retryResyncCase.breakin_clear_inspection_status == "PASS")
                                    {
                                        proposal_status = await paymentBAL.FetchProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, retryResyncCase.core, flow_type);
                                        if (!string.IsNullOrEmpty(proposal_status))
                                        {
                                            retryResyncCase.proposal_status = proposal_status;
                                        }
                                        else
                                        {
                                            return "Proposal Status Could Not be Fetched";
                                        }

                                        if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()))
                                        {
                                            if (string.IsNullOrEmpty(retryResyncCase.policy_number))
                                            {
                                                // Policy Number Generation
                                                retryResyncCase.policy_number = await RetryResyncTagProposal(retryResyncCase, flow_type);
                                            }

                                            if (!string.IsNullOrEmpty(retryResyncCase.policy_number))
                                            {
                                                if (retryResyncCase.resync_status == "N")
                                                {
                                                    // Sync To Source
                                                    retryResyncCase.resync_status = await RetryResyncSyncToSource(retryResyncCase, flow_type);
                                                }

                                                if (retryResyncCase.resync_status == "Y")
                                                {
                                                    return "Case Cleared Successfully";
                                                }
                                            }
                                        }
                                        else
                                        {
                                            return "Unknown Proposal Status";
                                        }
                                    }
                                }
                            }
                        }
                        else if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()))
                        {
                            if (string.IsNullOrEmpty(retryResyncCase.policy_number))
                            {
                                // Policy Number Generation
                                retryResyncCase.policy_number = await RetryResyncTagProposal(retryResyncCase, flow_type);
                            }

                            if (!string.IsNullOrEmpty(retryResyncCase.policy_number))
                            {
                                if (retryResyncCase.resync_status == "N")
                                {
                                    // Sync To Source
                                    retryResyncCase.resync_status = await RetryResyncSyncToSource(retryResyncCase, flow_type);
                                }

                                if (retryResyncCase.resync_status == "Y")
                                {
                                    return "Case Cleared Successfully";
                                }
                            }
                        }
                        else
                        {
                            return "Unknown Proposal Status";
                        }
                    }
                    else if (retryResyncCase.product.Contains("MOTOR") && ApplicationConfig.NPVI_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()))
                    {
                        if (string.IsNullOrEmpty(retryResyncCase.breakin_id))
                        {
                            // BreakIn ID Generation
                            await orderDAL.UpdateIsBreakInReq(retryResyncCase.proposal_number);
                            retryResyncCase.breakin_id = await RetryResyncGenerateBreakIn(retryResyncCase, flow_type);
                        }

                        if (!string.IsNullOrEmpty(retryResyncCase.breakin_id))
                        {
                            if (!string.IsNullOrEmpty(retryResyncCase.breakin_status) && retryResyncCase.breakin_status == "RECOMMENDED" && string.IsNullOrEmpty(retryResyncCase.breakin_clear_inspection_status))
                            {
                                // BreakIn Clearance
                                retryResyncCase.breakin_clear_inspection_status = await RetryResyncBreakInClearance(retryResyncCase, flow_type);
                            }

                            if (!string.IsNullOrEmpty(retryResyncCase.breakin_status) && retryResyncCase.breakin_status == "RECOMMENDED" && !string.IsNullOrEmpty(retryResyncCase.breakin_clear_inspection_status) && retryResyncCase.breakin_clear_inspection_status == "PASS")
                            {
                                proposal_status = await paymentBAL.FetchProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, retryResyncCase.core, flow_type);
                                if (!string.IsNullOrEmpty(proposal_status))
                                {
                                    retryResyncCase.proposal_status = proposal_status;
                                }
                                else
                                {
                                    return "Proposal Status Could Not be Fetched";
                                }

                                if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(retryResyncCase.proposal_status.ToUpper()))
                                {
                                    if (string.IsNullOrEmpty(retryResyncCase.policy_number))
                                    {
                                        // Policy Number Generation
                                        retryResyncCase.policy_number = await RetryResyncTagProposal(retryResyncCase, flow_type);
                                    }

                                    if (!string.IsNullOrEmpty(retryResyncCase.policy_number))
                                    {
                                        if (retryResyncCase.resync_status == "N")
                                        {
                                            // Sync To Source
                                            retryResyncCase.resync_status = await RetryResyncSyncToSource(retryResyncCase, flow_type);
                                        }

                                        if (retryResyncCase.resync_status == "Y")
                                        {
                                            return "Case Cleared Successfully";
                                        }
                                    }
                                }
                                else
                                {
                                    return "Unknown Proposal Status";
                                }
                            }
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(retryResyncCase.policy_number))
                        {
                            // Policy Number Generation
                            retryResyncCase.policy_number = await RetryResyncTagProposal(retryResyncCase, flow_type);
                        }

                        if (!string.IsNullOrEmpty(retryResyncCase.policy_number))
                        {
                            if (retryResyncCase.resync_status == "N")
                            {
                                // Sync To Source
                                retryResyncCase.resync_status = await RetryResyncSyncToSource(retryResyncCase, flow_type);
                            }

                            if (retryResyncCase.resync_status == "Y")
                            {
                                return "Case Cleared Successfully";
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "RetryResyncBAL.cs - ClearNSTPRetryResyncTransactionCase()",
                   TraceId: retryResyncCase.trace_id,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return "Case Not Cleared";
        }

        public PFPolicyCreationReq CreatePFRetryTaggingReqBody(RetryResyncCase retryResyncCase)
        {
            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            DateFormatter dateFormatter = new DateFormatter();

            DateTime createdDate;

            try
            {
                retryResyncCase.auth_code = retryResyncCase.auth_code.Replace("pay_", "");

                string created_on = retryResyncCase.transaction_initiation_time;

                bool isValidParsedDate = dateFormatter.TryNormalizeDate(created_on, out DateTime parsedDate);
                if (!isValidParsedDate)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RetryResyncBAL.cs - CreatePFRetryTaggingReqBody() ",
                       Message: "Invalid Created on. Created On: " + JsonConvert.SerializeObject(created_on),
                       TraceId: retryResyncCase.trace_id), ApplicationConfig.RETRY_RESYNC_LOGS);

                    return null;
                }
                createdDate = parsedDate;

                string formattedDate = createdDate.ToString("yyyy-MM-dd");

                pfPolicyCreationReq.CorrelationId = retryResyncCase.trace_id;
                pfPolicyCreationReq.PaymentEntry = null;

                if (retryResyncCase.deal_id != null)
                {
                    pfPolicyCreationReq.DealID = retryResyncCase.deal_id;
                    pfPolicyCreationReq.IntermediaryID = null;
                }
                else
                {
                    pfPolicyCreationReq.DealID = null;
                    pfPolicyCreationReq.IntermediaryID = retryResyncCase.intermediary_code;
                }
                pfPolicyCreationReq.isMappingRequired = false;
                pfPolicyCreationReq.isTaggingRequired = true;

                pfPolicyCreationReq.PaymentTagging = new PFPaymentMapTagReq
                {
                    customerProposal = new List<PFCustomerProposalReq>()
                    {
                        new PFCustomerProposalReq
                        {
                            ProposalNo = retryResyncCase.proposal_number,
                            CustomerID = retryResyncCase.customer_id
                        }
                    },
                    PaymentID = retryResyncCase.payment_id
                };
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncBAL.cs - CreatePFTaggingReqBody() ",
                    TraceId: retryResyncCase.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return pfPolicyCreationReq;
        }

        public ArtemisPolicyTaggingOnlyReq CreateArtemisRetryTaggingReqBody(RetryResyncCase retryResyncCase)
        {
            ArtemisPolicyTaggingOnlyReq artemisPolicyTaggingOnlyReq = new ArtemisPolicyTaggingOnlyReq();

            try
            {
                artemisPolicyTaggingOnlyReq.tagging = new List<ArtemisTaggingOnlyReq>()
                {
                     new ArtemisTaggingOnlyReq
                     {
                         receiptNumber = retryResyncCase.payment_id,
                         proposalNumber = retryResyncCase.proposal_number
                     }
                };
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RetryResyncBAL.cs - CreateArtemisRetryTaggingReqBody() ",
                    TraceId: retryResyncCase.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return artemisPolicyTaggingOnlyReq;
        }

        public async Task<string> RetryResyncGeneratePaymentId(RetryResyncCase retryResyncCase, string flow_type)
        {
            CallbackBAL callbackBAL = new CallbackBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();

            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();

            string dbPaymentId = string.Empty;

            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            PFPolicyCreationResp pfPolicyCreationResp = new PFPolicyCreationResp();
            ILESBInterface ilesbInterface = new ILESBInterface();

            string pfPaymentId = string.Empty;

            ArtemisPolicyCreationReq artemisPolicyCreationReq = new ArtemisPolicyCreationReq();
            ArtemisPolicyCreationResp artemisPolicyCreationResp = new ArtemisPolicyCreationResp();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();

            string receiptNumber = string.Empty;

            try
            {
                dbPaymentId = await retryResyncDAL.GetPaymentIDFromDb(retryResyncCase.transaction_id);
                if (!string.IsNullOrEmpty(dbPaymentId))
                {
                    return dbPaymentId;
                }

                fetchPaymentDetails = new FetchPaymentDetails()
                {
                    trace_id = retryResyncCase.trace_id,
                    transaction_id = retryResyncCase.transaction_id,
                    total_amount = retryResyncCase.total_amount,
                    application_name = retryResyncCase.application_name,
                    product = retryResyncCase.product,
                    core = retryResyncCase.core,
                    created_on = retryResyncCase.transaction_initiation_time,
                    auth_code = retryResyncCase.auth_code,
                    payment_mode = retryResyncCase.payment_mode,
                    payer_type = retryResyncCase.payer_type,
                    pf_mid = retryResyncCase.pf_mid,
                    artemis_mid = retryResyncCase.artemis_mid,
                    fetchPaymentProposalList = new List<FetchPaymentProposalList>
                    {
                        new FetchPaymentProposalList
                        {
                            proposal_number = retryResyncCase.proposal_number,
                            proposal_status = retryResyncCase.proposal_status,
                            deal_id = retryResyncCase.deal_id,
                            intermediary_code = retryResyncCase.intermediary_code,
                            customer_id = retryResyncCase.customer_id,
                            is_nstp_flow = retryResyncCase.is_nstp_flow,
                            business_vertical = retryResyncCase.business_vertical,
                            sub_vertical = retryResyncCase.sub_vertical,
                            marketing_officer = retryResyncCase.marketing_officer,
                            proposer_code = retryResyncCase.proposer_code
                        }
                    }
                };

                if (retryResyncCase.core.ToUpper() == "PF")
                {
                    pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(true, false, fetchPaymentDetails);

                    if (pfPolicyCreationReq == null)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "RetryResyncBAL.cs - RetryResyncGeneratePaymentId() ",
                            TraceId: retryResyncCase.trace_id,
                            Message: "Error while creating request body for PF Policy Creation. Callback Request: " + fetchPaymentDetails + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.RETRY_RESYNC_LOGS);

                        return null;
                    }
                    else
                    {
                        // ILESB Payment Entry and Tagging API Call
                        pfPolicyCreationResp = await ilesbInterface.GeneratePFPolicyData(pfPolicyCreationReq, flow_type);
                        if (pfPolicyCreationResp == null)
                        {
                            return null;
                        }
                        else
                        {
                            if (pfPolicyCreationResp != null && pfPolicyCreationResp.StatusMessage.ToUpper() == "SUCCESS" && pfPolicyCreationResp.paymentEntryResponse != null && pfPolicyCreationResp.paymentEntryResponse.Status.ToUpper() == "SUCCESS")
                            {
                                pfPaymentId = pfPolicyCreationResp.paymentEntryResponse.PaymentID;

                                await callbackDAL.UpdateCorePaymentID(pfPaymentId, fetchPaymentDetails.transaction_id);

                                return pfPaymentId;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
                else if (retryResyncCase.core.ToUpper() == "ARTEMIS")
                {
                    artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                    if (artemisPolicyCreationReq == null)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "RetryResyncBAL.cs - RetryResyncGeneratePaymentId() ",
                            TraceId: retryResyncCase.trace_id,
                            Message: "Error while creating request body for Artemis Policy Creation. Callback Request: " + fetchPaymentDetails + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.RETRY_RESYNC_LOGS);

                        return null;
                    }
                    else
                    {
                        // API FACTORY Payment Entry and Tagging API Call
                        RestResponse apiResponse = await apiFactoryInterface.GenerateArtemisPolicyData(retryResyncCase.trace_id, artemisPolicyCreationReq, flow_type);
                        if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                        {
                            artemisPolicyCreationResp = JsonConvert.DeserializeObject<ArtemisPolicyCreationResp>(apiResponse.Content);
                        }

                        if (artemisPolicyCreationResp == null)
                        {
                            return null;
                        }
                        else
                        {
                            if (artemisPolicyCreationResp != null && artemisPolicyCreationResp.data != null && artemisPolicyCreationResp.statusCode == 200 && artemisPolicyCreationResp.data.receiptStatus.ToUpper() == "SUCCESSFUL")
                            {
                                receiptNumber = artemisPolicyCreationResp.data.receiptNumber;

                                await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);

                                return receiptNumber;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RetryResyncBAL.cs - RetryResyncGeneratePaymentId() ",
                       TraceId: retryResyncCase.trace_id,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return null;
        }

        public async Task<string> RetryResyncIasClearance(RetryResyncCase retryResyncCase, string flow_type)
        {
            OrderDAL orderDAL = new OrderDAL();
            ILESBInterface ilesbInterface = new ILESBInterface();

            string iasId = string.Empty;
            string token = string.Empty;

            bool isIasSuccess = false;

            try
            {
                iasId = await orderDAL.GetNextIASSequence();

                token = ilesbInterface.CreateBiztalkAuthToken(ApplicationConfig.BIZTALK_TOKEN_USERNAME, ApplicationConfig.BIZTALK_TOKEN_PASSWORD);
                isIasSuccess = await ilesbInterface.CallIASApi(iasId, token, retryResyncCase.trace_id, retryResyncCase.proposal_number, flow_type);
                if (isIasSuccess)
                {
                    return iasId;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RetryResyncBAL.cs - RetryResyncIasClearance() ",
                       TraceId: retryResyncCase.trace_id,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return null;
        }

        public async Task<string> RetryResyncGenerateBreakIn(RetryResyncCase retryResyncCase, string flow_type)
        {
            BreakInBAL breakInBAL = new BreakInBAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            CreateBreakInReq createBreakInReq = new CreateBreakInReq();

            string encryptedId = string.Empty;

            string is_dashboard_processing = string.Empty;
            string is_retryresync_processing = string.Empty;
            List<string> is_processing = new List<string>();

            string breakInId = string.Empty;

            try
            {
                encryptedId = encryptDecrypt.EncryptRequest($"{retryResyncCase.transaction_id}|{retryResyncCase.trace_id}");
                if (!string.IsNullOrEmpty(encryptedId))
                {
                    if (string.IsNullOrEmpty(retryResyncCase.breakin_inspection_type))
                    {
                        createBreakInReq = new CreateBreakInReq()
                        {
                            ID = encryptedId,
                            inspection_type = "SI",
                            address = null,
                            pincode = null,
                            city = null,
                            state = null
                        };
                    }
                    else
                    {
                        createBreakInReq = new CreateBreakInReq()
                        {
                            ID = encryptedId,
                            address = retryResyncCase.breakin_address,
                            pincode = retryResyncCase.breakin_pincode,
                            city = retryResyncCase.breakin_city,
                            state = retryResyncCase.breakin_state,
                            inspection_type = retryResyncCase.breakin_inspection_type,
                        };
                    }

                    breakInId = await breakInBAL.ProcessEachBreakInCase(retryResyncCase.proposal_number, retryResyncCase.deal_id, retryResyncCase.transaction_id, retryResyncCase.trace_id, createBreakInReq, flow_type, Convert.ToString(retryResyncCase.proposal_amount), retryResyncCase.proposal_id, retryResyncCase.proposal_type, retryResyncCase.core);
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "RetryResyncBAL.cs - RetryResyncGenerateBreakIn() ",
                            Message: "ID Encryption Error. Encrypted ID: " + encryptedId + " Transaction ID: " + retryResyncCase.transaction_id + " Trace ID: " + retryResyncCase.trace_id), ApplicationConfig.RETRY_RESYNC_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "RetryResyncBAL.cs - RetryResyncGenerateBreakIn() ",
                        TraceId: retryResyncCase.trace_id,
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return breakInId;
        }

        public async Task<string> RetryResyncBreakInClearance(RetryResyncCase retryResyncCase, string flow_type)
        {
            BreakInDAL breakInDAL = new BreakInDAL();
            ILESBInterface ilesbInterface = new ILESBInterface();

            BreakinClearInspectionStatusRes breakinClearInspectionStatusRes = new BreakinClearInspectionStatusRes();

            string breakInClearInspectionStatus = string.Empty;

            try
            {
                breakinClearInspectionStatusRes = await ilesbInterface.ILESBBreakInClearance(retryResyncCase.proposal_number, retryResyncCase.proposal_start_date, retryResyncCase.breakin_id, retryResyncCase.deal_id, retryResyncCase.breakin_status, retryResyncCase.trace_id, retryResyncCase.breakin_inspection_date, flow_type);
                if (breakinClearInspectionStatusRes != null && !string.IsNullOrEmpty(breakinClearInspectionStatusRes.VehicleInspectionStatus))
                {
                    await breakInDAL.UpdateBreakInClearInspectionStatus(retryResyncCase.breakin_id, breakinClearInspectionStatusRes.VehicleInspectionStatus.ToUpper());

                    breakInClearInspectionStatus = breakinClearInspectionStatusRes.VehicleInspectionStatus.ToUpper();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RetryResyncBAL.cs - RetryResyncBreakInClearance() ",
                       TraceId: retryResyncCase.trace_id,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return breakInClearInspectionStatus;
        }

        public async Task<string> RetryResyncTagProposal(RetryResyncCase retryResyncCase, string flow_type)
        {
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();

            ILESBInterface ilesbInterface = new ILESBInterface();
            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            PFPolicyCreationResp pfPolicyCreationResp = new PFPolicyCreationResp();

            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            ArtemisPolicyTaggingOnlyReq artemisPolicyTaggingOnlyReq = new ArtemisPolicyTaggingOnlyReq();
            ArtemisPolicyTaggingOnlyResp artemisPolicyTaggingOnlyResp = new ArtemisPolicyTaggingOnlyResp();

            string policy_number = string.Empty;

            try
            {
                if (retryResyncCase.core.ToUpper() == "PF")
                {
                    pfPolicyCreationReq = CreatePFRetryTaggingReqBody(retryResyncCase);
                    if (pfPolicyCreationReq == null)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "RetryResyncBAL.cs - RetryResyncTagProposal() ",
                            TraceId: retryResyncCase.trace_id,
                            Message: "Error while creating request body for PF Policy Creation. Retry Case: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.RETRY_RESYNC_LOGS);
                    }
                    else
                    {
                        pfPolicyCreationResp = await ilesbInterface.GeneratePFPolicyData(pfPolicyCreationReq, flow_type);
                        if (pfPolicyCreationResp == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "RetryResyncBAL.cs - RetryResyncTagProposal() ",
                               TraceId: retryResyncCase.trace_id,
                               Message: "Error while calling PF Policy Creation API. Retry Case: " + JsonConvert.SerializeObject(retryResyncCase) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq)), ApplicationConfig.RETRY_RESYNC_LOGS);
                        }

                        if (pfPolicyCreationResp != null && pfPolicyCreationResp.StatusMessage.ToUpper() == "SUCCESS" && pfPolicyCreationResp.paymentTagResponse != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList.Count > 0 && !string.IsNullOrEmpty(pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo) && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo != "0")
                        {
                            policy_number = pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo;
                        }
                    }
                    await retryResyncDAL.UpdatePolicyNoForProposalNo(policy_number, retryResyncCase.proposal_number);
                }
                else if (retryResyncCase.core.ToUpper() == "ARTEMIS")
                {
                    artemisPolicyTaggingOnlyReq = CreateArtemisRetryTaggingReqBody(retryResyncCase);
                    if (artemisPolicyTaggingOnlyReq == null)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "RetryResyncBAL.cs - RetryResyncTagProposal() ",
                            TraceId: retryResyncCase.trace_id,
                            Message: "Error while creating request body for Artemis Policy Tagging. Retry Case: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.RETRY_RESYNC_LOGS);
                    }
                    else
                    {
                        artemisPolicyTaggingOnlyResp = await apiFactoryInterface.GenerateArtemisTaggingOnlyData(retryResyncCase.trace_id, artemisPolicyTaggingOnlyReq, flow_type);
                        if (artemisPolicyTaggingOnlyResp == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "RetryResyncBAL.cs - RetryResyncTagProposal() ",
                               TraceId: retryResyncCase.trace_id,
                               Message: "Error while calling Artemis Policy Tagging API. Retry Case: " + JsonConvert.SerializeObject(retryResyncCase) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyTaggingOnlyReq)), ApplicationConfig.RETRY_RESYNC_LOGS);
                        }

                        if (artemisPolicyTaggingOnlyResp != null && artemisPolicyTaggingOnlyResp.data != null && artemisPolicyTaggingOnlyResp.statusCode == 200 && artemisPolicyTaggingOnlyResp.data.tagging != null && artemisPolicyTaggingOnlyResp.data.tagging.Count > 0)
                        {
                            policy_number = artemisPolicyTaggingOnlyResp.data.tagging[0].policyNumber;
                        }
                    }
                    await retryResyncDAL.UpdatePolicyNoForProposalNo(policy_number, retryResyncCase.proposal_number);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RetryResyncBAL.cs - RetryResyncTagProposal() ",
                       TraceId: retryResyncCase.trace_id,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return policy_number;
        }

        public async Task<string> RetryResyncSyncToSource(RetryResyncCase retryResyncCase, string flow_type)
        {
            CallbackBAL callbackBAL = new CallbackBAL();
            string resync_status = "N";

            try
            {
                SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                {
                    policy_number = retryResyncCase.policy_number,
                    payment_id = retryResyncCase.payment_id,
                    proposal_number = retryResyncCase.proposal_number,
                    reverse_sync_url = retryResyncCase.reverse_sync_url,
                    trace_id = retryResyncCase.trace_id,
                    transaction_id = retryResyncCase.transaction_id,
                    application_name = retryResyncCase.application_name,
                    channel_application_details = retryResyncCase.channel_application_details,
                    auth_code = retryResyncCase.auth_code,
                    sync_event = "POLICY_CONFIRMATION"
                };

                resync_status = await callbackBAL.SyncProposalToSource(syncProposalToSource, flow_type, retryResyncCase.journey_type);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "RetryResyncBAL.cs - RetryResyncSyncToSource() ",
                       TraceId: retryResyncCase.trace_id,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return resync_status;
        }

        public async Task<string> UpdateAsbaPaymentId(string flow_type)
        {
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();
            KafkaWebhookDAL kafkaWebhookDAL = new KafkaWebhookDAL();
            UpdatePIDReq updatePIDReq = new UpdatePIDReq();

            try
            {

                List<UpdatePIDReq> updatePIDReqCases = await retryResyncDAL.GetUpdateAsbaPaymentIdCases();

                foreach (UpdatePIDReq updatePIDReqCase in updatePIDReqCases)
                {
                    updatePIDReq = new UpdatePIDReq()
                    {
                        trace_id = updatePIDReqCase.trace_id,
                        proposal_number = updatePIDReqCase.proposal_number,
                        mandateNumber = updatePIDReqCase.mandateNumber,
                        instrumentAmount = updatePIDReqCase.instrumentAmount,
                        instrumentDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        instrumentNumber = updatePIDReqCase.instrumentNumber,
                        receiptNumber = updatePIDReqCase.receiptNumber
                    };

                    UpdatePIDResp updatePIDResponse = await apiFactoryInterface.ArtemisUpdatePaymentID(updatePIDReq, flow_type);
                    if (updatePIDResponse != null && updatePIDResponse.StatusCode == 200 && updatePIDResponse.Data.isProcessed.ToUpper() == "Y")
                    {
                        bool isProcessed = await kafkaWebhookDAL.UpdateAsbaProcessedFlag(updatePIDReqCase.proposal_number, "Y", updatePIDReqCase.trace_id);
                        if (isProcessed)
                        {
                            return "Case cleared";
                        }
                        else
                        {
                            return "Case Not Cleared";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "RetryResyncBAL.cs - UpdateAsbaPaymentId()",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.RETRY_RESYNC_LOGS);
            }

            return "Case Not Cleared";
        }
    }
}