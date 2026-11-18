using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RestSharp;
using System.Net;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class KafkaWebhookBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<AsbaWebhookRespModel> AsbaPaymentStatusWebhook(AsbaWebhookReqModel asbaWebhookReq, string username, string flow_type)
        {
            AsbaWebhookRespModel response = new AsbaWebhookRespModel();
            KafkaWebhookDAL kafkaWebhookDAL = new KafkaWebhookDAL();
            ProposalDetails proposalDetails = new ProposalDetails();
            CaptureMandateReq captureMandateReq = new CaptureMandateReq();
            RevokeMandateReq revokeMandateReq = new RevokeMandateReq();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            UpdatePIDReq updatePIDReq = new UpdatePIDReq();
            OrderBAL orderBAL = new OrderBAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            string proposal_number = string.Empty;

            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "KafkaWebhookBAL.cs - AsbaPaymentStatusWebhook() ",
                   Message: "Test Hit"), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                proposal_number = asbaWebhookReq.P1;
                proposalDetails = await kafkaWebhookDAL.CheckProposalExists(proposal_number);
                if (proposalDetails == null)
                { 
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "KafkaWebhookBAL.cs - AsbaPaymentStatusWebhook() ",
                       Message: "Proposal No:- " + asbaWebhookReq.P1 + " does not exist in the database"), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                    response.Success = true;
                }
                else
                {
                    if (proposalDetails.IS_ASBA_PROCESSED.ToUpper() == "Y")
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "KafkaWebhookBAL.cs - AsbaPaymentStatusWebhook() ",
                            Message: "Proposal No:- " + asbaWebhookReq.P1 + " is already processed in the database"), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                        response.Success = true;
                    }
                    else
                    {
                        await paymentDAL.LogIntegrationApi(proposalDetails.TRACE_ID, proposalDetails.PROPOSAL_NUMBER, JsonConvert.SerializeObject(asbaWebhookReq), "", "", "ASBA PAYMENT STATUS WEBHOOK", "", flow_type, "");
                        if (asbaWebhookReq.RetainAmount == 0)
                        {
                            revokeMandateReq.trace_id = proposalDetails.TRACE_ID;
                            revokeMandateReq.transaction_id = proposalDetails.TRANSACTION_ID;
                            revokeMandateReq.mandate_id = proposalDetails.MANDATE_ID;
                            if (username.ToUpper() == proposalDetails.APPLICATION_NAME.ToUpper())
                            {
                                revokeMandateReq.application_name = proposalDetails.APPLICATION_NAME;
                            }
                            else
                            {
                                response.Success = false;
                                return response;
                            }
                            MandateExecutionResp mandateExecutionResp = await orderBAL.RevokeMandate(revokeMandateReq, flow_type);
                            
                            if (mandateExecutionResp != null && mandateExecutionResp.isSuccess)
                            {
                                bool isUpdated = await kafkaWebhookDAL.UpdateMandateDetails(mandateExecutionResp.mandate_id, "REVOKED", proposal_number, asbaWebhookReq.RetainAmount.ToString());
                                bool isProcessed = await kafkaWebhookDAL.UpdateAsbaProcessedFlag(proposal_number, "Y", proposalDetails.TRACE_ID);
                                response.Success = true;
                            }
                            else
                            {
                                response.Success = false;
                            }

                        }
                        else
                        {
                            //if retain amount is greter than the officially billed mandate amount check...
                            captureMandateReq.transaction_id = proposalDetails.TRANSACTION_ID;
                            captureMandateReq.amount = asbaWebhookReq.RetainAmount.ToString();
                            captureMandateReq.mandate_id = proposalDetails.MANDATE_ID;
                            captureMandateReq.customer_id = proposalDetails.CUSTOMER_ID;
                            captureMandateReq.mandate_execution_date = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
                            captureMandateReq.trace_id = proposalDetails.TRACE_ID;
                            captureMandateReq.order_id = proposalDetails.ORDER_ID;

                            if (username.ToUpper() == proposalDetails.APPLICATION_NAME.ToUpper())
                            {
                                captureMandateReq.application_name = proposalDetails.APPLICATION_NAME;
                            }
                            else
                            {
                                response.Success = false;
                                return response;
                            }
                            MandateExecutionResp captureMandateResp = await orderBAL.CaptureMandate(captureMandateReq, flow_type);
                            
                            if (captureMandateResp != null && !string.IsNullOrEmpty(captureMandateResp.auth_code) && captureMandateResp.isSuccess)
                            {
                                bool isUpdated = await kafkaWebhookDAL.UpdateMandateDetails(captureMandateResp.auth_code, "CAPTURED", proposal_number, asbaWebhookReq.RetainAmount.ToString());

                                //Update PID 
                                updatePIDReq.trace_id = proposalDetails.TRACE_ID;
                                updatePIDReq.proposal_number = proposalDetails.PROPOSAL_NUMBER;
                                updatePIDReq.mandateNumber = proposalDetails.MANDATE_ID;
                                updatePIDReq.instrumentAmount = asbaWebhookReq.RetainAmount.ToString();
                                updatePIDReq.instrumentDate = DateTime.Now.ToString("yyyy-MM-dd");
                                updatePIDReq.instrumentNumber = captureMandateResp.auth_code;
                                updatePIDReq.receiptNumber = proposalDetails.PAYMENT_ID;
                                UpdatePIDResp updatePIDResponse = await apiFactoryInterface.ArtemisUpdatePaymentID(updatePIDReq, flow_type);
                                if (updatePIDResponse != null && updatePIDResponse.StatusCode == 200 && updatePIDResponse.Data.isProcessed.ToUpper() == "Y")
                                {
                                    bool isProcessed = await kafkaWebhookDAL.UpdateAsbaProcessedFlag(proposal_number, "Y", proposalDetails.TRACE_ID);
                                    response.Success = true;
                                }
                                else
                                {
                                    //Retry code to update PID at artemis end
                                    response.Success = true;
                                }

                            }
                            else
                            {
                                response.Success = false;
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookBAL.cs - AsbaPaymentStatusWebhook() ",
                    TraceId: proposalDetails.TRACE_ID,
                    Message: "Proposal No:- " + asbaWebhookReq.P1 + " Exception:- " + $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                response.Success = false;
            }
            return response;
        }

        public async Task<PolicyNumberTopicResp> PolicyNumberStatusWebhook(PolicyNumberTopicReq policyNumberTopicReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            PolicyNumberTopicResp response = new PolicyNumberTopicResp();
            KafkaWebhookDAL kafkaWebhookDAL = new KafkaWebhookDAL();
            ProposalDetails proposalDetails = new ProposalDetails();
            string proposal_number = string.Empty;

            try
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "KafkaWebhookBAL.cs - PolicyNumberStatusWebhook() ",
                       Message: "Test Hit"), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                proposal_number = policyNumberTopicReq.ProposalNo;
                proposalDetails = await kafkaWebhookDAL.CheckProposalExists(proposal_number);
                if (proposalDetails == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "KafkaWebhookBAL.cs - PolicyNumberStatusWebhook() ",
                       TraceId:null,
                       Message: "Proposal No:- " + policyNumberTopicReq.ProposalNo + " does not exist in the database"), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                    response.Success = true;
                }
                else
                {
                    await paymentDAL.LogIntegrationApi(proposalDetails.TRACE_ID, proposal_number, JsonConvert.SerializeObject(policyNumberTopicReq), "", "", "POLICY NUMBER STATUS WEBHOOK", "", flow_type, "");

                    if (!string.IsNullOrEmpty(proposalDetails.POLICY_NUMBER))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "KafkaWebhookBAL.cs - PolicyNumberStatusWebhook() ",
                            TraceId: proposalDetails.TRACE_ID,
                            Message: "Proposal No:- " + policyNumberTopicReq.ProposalNo + " is mapped with Policy No:- " + proposalDetails.POLICY_NUMBER), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);
                        response.Success = true;
                    }
                    else
                    {
                        bool isUpdated = await kafkaWebhookDAL.UpdatePolicyNumberDetails(proposal_number, policyNumberTopicReq.PolicyNo, proposalDetails.TRACE_ID);
                        if(!isUpdated)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "KafkaWebhookBAL.cs - PolicyNumberStatusWebhook() ",
                            TraceId: proposalDetails.TRACE_ID,
                            Message: "Proposal No:- " + policyNumberTopicReq.ProposalNo + " Policy No:- " + policyNumberTopicReq.PolicyNo + " mapping failed at DB"), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                            response.Success = false;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "KafkaWebhookBAL.cs - PolicyNumberStatusWebhook() ",
                            TraceId: proposalDetails.TRACE_ID,
                            Message: "Proposal No:- " + policyNumberTopicReq.ProposalNo + " Policy No:- " + policyNumberTopicReq.PolicyNo + " mapped successfully at DB"), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);
                            response.Success = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "KafkaWebhookBAL.cs - PolicyNumberStatusWebhook() ",
                    Message: "Proposal No:- " + policyNumberTopicReq.ProposalNo + " Exception:- " + $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ASBA_PAYMENT_STATUS_LOGS);

                response.Success = false;
            }
            return response;                
        }
    }
}
