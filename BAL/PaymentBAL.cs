using Amazon.CloudWatchLogs;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Amazon.Runtime.Telemetry.Tracing;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Hangfire;
using ILTakeCare.DAL;
using Microsoft.AspNetCore.Authentication.OAuth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Cmp;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.GAEventsModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PlutusModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;
using static DigitalPaymentSystem_ILTC.Models.SendMessageServiceModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class PaymentBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<DPSStatusResponse> AddPaymentRequest(string username, AddPaymentReq addPaymentReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DropLeadBAL dropLeadBAL = new DropLeadBAL();
            SendPaymentLink sendPaymentLink = new SendPaymentLink();
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusBAL plutusBAL = new PlutusBAL();
            OrderDAL orderDAL = new OrderDAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            SendMessageService sendMessageService = new SendMessageService();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            DateFormatter dateFormatter = new DateFormatter();
            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();
            AddPaymentResp addPaymentResp = new AddPaymentResp();
            CheckProposalV2Resp checkProposalV2Resp = new CheckProposalV2Resp();

            string traceId = string.Empty;
            string basePaymentLink = string.Empty;
            string encryptedId = string.Empty;
            string paymentLink = string.Empty;
            bool isTemplateValid = false;
            HashSet<string> sendPaymentLinkActions;
            DateTime proposalStartDate;
            decimal totalAmount = 0;
            decimal totalProposalAmount = 0;
            bool isSameCustomer = true;
            bool doesProposalNumberExist = true;
            bool doesCustomerIdExist = true;
            bool doesDealIDorIntermediaryCodeExist = true;
            string firstCustomerId = string.Empty;
            int pfProposalStatusFetchFailedCount = 0;
            int artemisProposalDetailsFetchFailedCount = 0;
            string rm_text = string.Empty;
            string baseReturnLoaderUrl = string.Empty;
            string fullReturnLoaderUrl = string.Empty;
            string generateDpsOrderId = string.Empty;
            string order_id = string.Empty;


            try
            {
                if ((addPaymentReq.channel_application_details.ValueKind != JsonValueKind.Null && addPaymentReq.channel_application_details.ValueKind != JsonValueKind.Undefined) &&
                    addPaymentReq.channel_application_details.ValueKind != JsonValueKind.Object)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                           Message: "Channel Application Details format is invalid. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                           TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Channel Application Details format is invalid";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.JourneyType != "ADVANCE")
                {
                    checkProposalV2Resp = await checkproposal(addPaymentReq, flow_type);

                    if (checkProposalV2Resp.is_error)
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = checkProposalV2Resp.error_message;
                        response.TraceId = checkProposalV2Resp.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (checkProposalV2Resp.addPaymentResp != null &&
                        !string.IsNullOrEmpty(checkProposalV2Resp.addPaymentResp.URL) &&
                        !string.IsNullOrEmpty(checkProposalV2Resp.addPaymentResp.TransactionID))
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = null;
                        response.TraceId = checkProposalV2Resp.trace_id;
                        response.Data = checkProposalV2Resp.addPaymentResp;
                        return response;
                    }
                }

                traceId = Guid.NewGuid().ToString();

                await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(addPaymentReq), "", "", "ADD PAYMENT REQ", "", flow_type, "");

                totalAmount = Convert.ToDecimal(addPaymentReq.TotalAmount);
                addPaymentReq.Product = addPaymentReq.Product.ToUpper();

                if (!ApplicationConfig.PRODUCTS_LIST.ContainsKey(addPaymentReq.Product))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                        Message: "Product Does not Exist. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Product Does not Exist";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.JourneyType.ToUpper().Contains("EMI") && addPaymentReq.ProposalList.Count() > 1)
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Cannot Process MultiProposal Scenerio in EMI.";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.JourneyType != "ADVANCE")
                {
                    if (addPaymentReq.ProposalList != null && addPaymentReq.ProposalList.Count > 0)
                    {
                        for (int i = 0; i < addPaymentReq.ProposalList.Count; i++)
                        {
                            var proposal = addPaymentReq.ProposalList[i];

                            if (string.IsNullOrEmpty(proposal.ProposalNumber) || string.IsNullOrEmpty(proposal.ProposalAmount))
                            {
                                doesProposalNumberExist = false;
                                break;
                            }

                            totalProposalAmount += Convert.ToDecimal(proposal.ProposalAmount);

                            firstCustomerId = addPaymentReq.ProposalList[0].CustomerId;

                            if (proposal.CustomerId != firstCustomerId)
                            {
                                isSameCustomer = false;
                                break;
                            }

                            if (addPaymentReq.Core.ToUpper() == "PF")
                            {
                                if (string.IsNullOrEmpty(proposal.CustomerId))
                                {
                                    doesCustomerIdExist = false;
                                    break;
                                }

                                if (string.IsNullOrEmpty(proposal.DealId) && string.IsNullOrEmpty(proposal.IntermediaryCode))
                                {
                                    doesDealIDorIntermediaryCodeExist = false;
                                    break;
                                }

                                if (string.IsNullOrEmpty(proposal.ProposalStatus))
                                {
                                    proposal.ProposalStatus = await FetchProposalStatus(traceId, proposal.ProposalNumber, addPaymentReq.Core, flow_type);
                                }

                                if (!string.IsNullOrEmpty(proposal.ProposalStatus))
                                {
                                    if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(proposal.ProposalStatus.ToUpper()))
                                    {
                                        proposal.is_nstp_flow = "N";
                                    }
                                    else
                                    {
                                        proposal.is_nstp_flow = "Y";
                                    }
                                }
                                else
                                {
                                    pfProposalStatusFetchFailedCount = pfProposalStatusFetchFailedCount + 1;
                                }
                            }
                            else if (addPaymentReq.Core.ToUpper() == "ARTEMIS")
                            {
                                fetchArtemisProposalDetails = await apiFactoryInterface.FetchArtemisProposalDetails(proposal.ProposalNumber, traceId, addPaymentReq.Product, flow_type);
                                if ((addPaymentReq.PayerType == "C" && proposal.CustomerId != null) || (addPaymentReq.PayerType == "I" && proposal.IntermediaryCode != null))
                                {
                                    proposal.proposer_code = addPaymentReq.PayerType == "C" ? proposal.CustomerId : proposal.IntermediaryCode;
                                }
                                else
                                {
                                    artemisProposalDetailsFetchFailedCount = artemisProposalDetailsFetchFailedCount + 1;
                                }
                                if (fetchArtemisProposalDetails != null && !string.IsNullOrEmpty(fetchArtemisProposalDetails.intermediary_code) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.is_nstp_flow) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.marketing_officer) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.policy_status) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.policy_sub_status))
                                {
                                    proposal.is_nstp_flow = fetchArtemisProposalDetails.is_nstp_flow;
                                    //proposal.business_vertical = fetchArtemisProposalDetails.business_vertical;
                                    //proposal.sub_vertical = fetchArtemisProposalDetails.sub_vertical;
                                    proposal.marketing_officer = fetchArtemisProposalDetails.marketing_officer;
                                    proposal.IntermediaryCode = fetchArtemisProposalDetails.intermediary_code;
                                    proposal.ProposalStatus = string.IsNullOrEmpty(fetchArtemisProposalDetails.policy_sub_status) ? fetchArtemisProposalDetails.policy_status : fetchArtemisProposalDetails.policy_sub_status;
                                }
                                else
                                {
                                    artemisProposalDetailsFetchFailedCount = artemisProposalDetailsFetchFailedCount + 1;
                                }
                            }
                        }

                        if (!doesProposalNumberExist)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Proposal Number or Proposal Amount cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Proposal Number or Proposal Amount cannot be empty in the Proposal List for JourneyType: " + addPaymentReq.JourneyType;
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (!doesCustomerIdExist)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Customer ID cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Customer ID cannot be empty in the Proposal List for JourneyType: " + addPaymentReq.JourneyType;
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (!isSameCustomer)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Customer ID should be the same for each proposal. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Customer ID should be the same for each proposal.";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (!doesDealIDorIntermediaryCodeExist)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Either Deal ID or Intermediary Code is required in the Proposal List. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Either Deal ID or Intermediary Code is required in the Proposal List for JourneyType: " + addPaymentReq.JourneyType;
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (totalAmount != totalProposalAmount)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "The Total Amount does not match with the Proposal Amount Sum. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "The Total Amount does not match with the Proposal Amount Sum.";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (pfProposalStatusFetchFailedCount > 0)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "PF Proposal Status Fetching Failed. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Could not fetch PF Proposal Status.";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (artemisProposalDetailsFetchFailedCount > 0)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Artemis Proposal Details Fetching Failed. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Could not fetch Artemis Proposal Details.";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (string.IsNullOrEmpty(addPaymentReq.ProposalStartDate))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Proposal Start Date is required for JourneyType: " + addPaymentReq.JourneyType
                                         + ". Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Proposal Start Date is required for JourneyType: " + addPaymentReq.JourneyType;
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        bool isValidParsedDate = dateFormatter.TryNormalizeDate(addPaymentReq.ProposalStartDate, out DateTime parsedDate);
                        if (!isValidParsedDate)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                               Message: "Invalid Proposal Start Date. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                               TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Invalid Proposal Start Date. Please provide valid proposal start date";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }
                        proposalStartDate = parsedDate;

                        DateTime minAllowedDate = DateTime.Today;
                        DateTime maxAllowedDate = DateTime.Today.AddDays(180);
                        DateTime maxAllowedDateHome = DateTime.Today.AddYears(1);

                        if ((proposalStartDate < minAllowedDate || proposalStartDate > maxAllowedDate) && !addPaymentReq.Product.StartsWith("HOME"))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Proposal Start Date is outside allowed range. Date: " + proposalStartDate + ", Allowed Range: " + minAllowedDate + " to " + maxAllowedDate,
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Proposal Start Date should be today or any date within the next 180 days.";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }
                        else if ((proposalStartDate < minAllowedDate || proposalStartDate > maxAllowedDateHome) && addPaymentReq.Product.StartsWith("HOME"))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Proposal Start Date is outside allowed range. Date: " + proposalStartDate + ", Allowed Range: " + minAllowedDate + " to " + maxAllowedDate,
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Proposal Start Date should be today or any date within the next 1 Year.";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        //var tokenMinutes = Convert.ToInt32(ApplicationConfig.TOKEN_EXPIRY_TIME);
                        //TimeOnly currentTimeOnly = TimeOnly.FromDateTime(DateTime.Now);
                        //TimeOnly lastAllowedTimeOnly = TimeOnly.FromDateTime(DateTime.Today.AddMinutes(-tokenMinutes).AddSeconds(-1));

                        //if (proposalStartDate.Date == DateTime.Today && currentTimeOnly > lastAllowedTimeOnly)
                        //{
                        //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        //        MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                        //        Message: "Proposal Start Date cannot be before Payment Date. Proposal Start Date: " + proposalStartDate,
                        //        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                        //    response.StatusCode = 0;
                        //    response.StatusMessage = "Failed";
                        //    response.Message = "Proposal Start Date cannot be before Payment Date";
                        //    response.TraceId = traceId;
                        //    response.Data = null;
                        //    return response;
                        //}

                        if (addPaymentReq.Product.Contains("SME"))
                        {

                            rm_text = await dropLeadBAL.RMDetails(traceId, addPaymentReq.Product);
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                        Message: "Proposal List cannot be null for Journey Type: " + addPaymentReq.JourneyType + ". Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Proposal List cannot be null for Journey Type: " + addPaymentReq.JourneyType;
                        response.TraceId = traceId;
                        response.Data = null;
                        return response;
                    }
                }

                isTemplateValid = await paymentDAL.CheckValidTemplate(username, addPaymentReq, traceId);
                if (!isTemplateValid)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                        Message: "Invalid Template ID for the given Username and Product or the Template ID is Inactive. Template ID: " + addPaymentReq.TemplateID + " Application Name: " + username + " Product: " + addPaymentReq.Product,
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid Template ID for the given Username and Product or the Template ID is Inactive.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.JourneyType.ToUpper().Contains("PORTABILITY") && !addPaymentReq.Product.Contains("HEALTH"))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                        Message: "Porting is allowed only for Health Products. Request: " + addPaymentReq,
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Porting is allowed only for Health Products.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                addPaymentResp = await paymentDAL.LogDbTransaction(username, addPaymentReq, traceId, totalAmount, rm_text);
                if (addPaymentResp != null && !string.IsNullOrEmpty(addPaymentResp.TransactionID))
                {
                    if (!string.IsNullOrEmpty(addPaymentResp.pg_name) && addPaymentResp.pg_name.ToUpper() == "PLUTUS")
                    {
                        baseReturnLoaderUrl = ApplicationConfig.RETURN_LOADER_URL;

                        encryptedId = encryptDecrypt.EncryptRequest($"{addPaymentResp.TransactionID}|{traceId}");
                        if (string.IsNullOrEmpty(baseReturnLoaderUrl) || string.IsNullOrEmpty(encryptedId))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                               TraceId: traceId,
                               Message: "Unable to fetch Return Loader URL for the Given Template ID. Base URL: " + baseReturnLoaderUrl + " Encrypted ID: " + encryptedId + " Transaction ID: " + addPaymentResp.TransactionID + " Trace ID: " + traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch Return Loader URL for the Given Template ID";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }
                        fullReturnLoaderUrl = baseReturnLoaderUrl;

                        generateDpsOrderId = await orderDAL.GetNextDPSOrderSequence();

                        PlutusAddPaymentResponse plutusAddPaymentResponse = await plutusBAL.PlutusAddPaymentRequest(addPaymentReq, traceId, addPaymentResp.TransactionID, fullReturnLoaderUrl, generateDpsOrderId, flow_type);
                        if (plutusAddPaymentResponse.statusCode == 200 && plutusAddPaymentResponse.isSuccess)
                        {
                            order_id = Convert.ToString(plutusAddPaymentResponse.data.transactionId);
                            addPaymentResp.URL = ApplicationConfig.DPS_PROXY_URL + encryptedId;

                            await paymentDAL.updatePaymentLink(traceId, addPaymentResp.TransactionID, addPaymentResp.URL);

                            bool saved = await orderDAL.InsertJuspayOrderDetails(addPaymentResp.TransactionID, traceId, addPaymentReq.TotalAmount, order_id, "", order_id, "");
                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "";
                            response.TraceId = traceId;
                            response.Data = addPaymentResp;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                    Message: "Payment Link Generation Error. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Payment Link Generation Error";
                            response.TraceId = traceId;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        // Generate Payment Link
                        basePaymentLink = ApplicationConfig.PAYMENT_BASE_URL;
                        encryptedId = encryptDecrypt.EncryptRequest($"{addPaymentResp.TransactionID}|{traceId}");

                        if (!string.IsNullOrEmpty(basePaymentLink) && !string.IsNullOrEmpty(encryptedId))
                        {
                            paymentLink = $"{basePaymentLink}?id={encryptedId}";
                            await paymentDAL.updatePaymentLink(traceId, addPaymentResp.TransactionID, paymentLink);

                            if (!string.IsNullOrEmpty(addPaymentReq.SendPaymentLink))
                            {
                                sendPaymentLinkActions = new HashSet<string>(addPaymentReq.SendPaymentLink.Contains("|") ? addPaymentReq.SendPaymentLink.Split('|') : new string[] { addPaymentReq.SendPaymentLink });
                                foreach (string sendPaymentLinkTo in sendPaymentLinkActions)
                                {
                                    switch (sendPaymentLinkTo)
                                    {
                                        case "W":
                                            WhatsAppDynamicParams whatsAppDynamicParams = new WhatsAppDynamicParams()
                                            {
                                                customerName = addPaymentReq.Name,
                                                amount = addPaymentReq.TotalAmount,
                                                paymentLink = paymentLink
                                            };
                                            bool wsMessageSent = await sendMessageService.SendWhatsApp(traceId, addPaymentReq.MobileNumber, whatsAppDynamicParams, flow_type);
                                            if (wsMessageSent)
                                            {
                                                sendPaymentLink.WhatsAppSent = true;
                                            }

                                            break;

                                        case "S":
                                            bool messageSent = await sendMessageService.SendSMS(traceId, addPaymentReq.MobileNumber, $"Hi, your ICICI Lombard verification OTP is {paymentLink} , Please do not share with anyone", flow_type);
                                            if (messageSent)
                                            {
                                                sendPaymentLink.SmsSent = true;
                                            }

                                            break;

                                        case "E":
                                            bool emailSent = sendMessageService.SendMail($@"<html><body><p>Dear {addPaymentReq.Name},</p><p> Your payment link for Rs.{addPaymentReq.TotalAmount}: <a href='{paymentLink}'>{paymentLink}</a></p><p></p><p> Thank you for choosing ICICI Lombard! </p><p></p><b> Warm Regards,<br/><i>ICICI Lombard </b></i></body></html>", addPaymentReq.Email, @"ICICI Lombard – Complete Your Pending Payment");
                                            if (emailSent)
                                            {
                                                sendPaymentLink.EmailSent = true;
                                            }

                                            break;

                                        default:

                                            break;
                                    }
                                }
                                sendPaymentLink.TransactionID = addPaymentResp.TransactionID;

                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = null;
                                response.TraceId = traceId;
                                response.Data = sendPaymentLink;
                            }
                            else
                            {
                                addPaymentResp.URL = ApplicationConfig.DPS_PROXY_URL + encryptedId;

                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = null;
                                response.TraceId = traceId;
                                response.Data = addPaymentResp;
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                Message: "Payment Link Generation Error. Base URL: " + basePaymentLink + " Encrypted ID: " + encryptedId + " Transaction ID: " + addPaymentResp.TransactionID,
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Error while generating Payment Link";
                            response.TraceId = traceId;
                            response.Data = null;
                        }
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                            Message: "Payment Link Generation Error: Transaction ID is Null. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Failed to process the payment request.";
                    response.TraceId = traceId;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "PaymentController.cs - AddPaymentRequest() ",
                        TraceId: traceId,
                        Message: "Request: " + JsonConvert.SerializeObject(addPaymentReq) + " Response: " + JsonConvert.SerializeObject(response),
                        StackTrace: ""
                    ),
                    ApplicationConfig.PAYMENT_LOGS
                );
            }
            return response;
        }

        public async Task<DPSStatusResponse> AddPaymentRequestv2(AuthenticateTokenData authenticateTokenData, AddPaymentReqv2 addPaymentReq, string flow_type, string website_correlation_id)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DPSStatusResponse ILEMIValidationResponse = new DPSStatusResponse();
            DropLeadBAL dropLeadBAL = new DropLeadBAL();
            SendPaymentLink sendPaymentLink = new SendPaymentLink();
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusBAL plutusBAL = new PlutusBAL();
            OrderBAL orderBAL = new OrderBAL();
            OrderDAL orderDAL = new OrderDAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            SendMessageService sendMessageService = new SendMessageService();
            ILESBInterface ilesbInterface = new ILESBInterface();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            DateFormatter dateFormatter = new DateFormatter();
            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();
            AddPaymentRespv2 addPaymentResp = new AddPaymentRespv2();
            CheckProposalV2Resp checkProposalV2Resp = new CheckProposalV2Resp();

            FetchProposalDetailsResp fetchProposalDetailsResp = new FetchProposalDetailsResp();
            List<FetchProposalDetailsResp> fetchProposalDetailsRespList = new List<FetchProposalDetailsResp>();

            string traceId = string.Empty;
            string encryptedId = string.Empty;

            string basePaymentLink = string.Empty;
            string paymentLink = string.Empty;


            DateTime proposalStartDate;
            decimal totalAmount = 0;
            decimal totalProposalAmount = 0;
            decimal inputtotalProposalAmount = 0;

            HashSet<string> pfCustomerIdSet = new HashSet<string>();
            HashSet<string> artemisCustomerIdSet = new HashSet<string>();

            string rm_text = string.Empty;
            string baseReturnLoaderUrl = string.Empty;
            string fullReturnLoaderUrl = string.Empty;
            string generateDpsOrderId = string.Empty;
            string order_id = string.Empty;

            try
            {
                traceId = Guid.NewGuid().ToString();

                await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(addPaymentReq), "", "", "ADD PAYMENT REQ V2", "", flow_type, "");

                if ((addPaymentReq.channelApplicationDetails.ValueKind != JsonValueKind.Null && addPaymentReq.channelApplicationDetails.ValueKind != JsonValueKind.Undefined) &&
                    addPaymentReq.channelApplicationDetails.ValueKind != JsonValueKind.Object)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                           Message: "Channel Application Details format is invalid. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                           TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Channel Application Details format is invalid";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                addPaymentReq.product = GetProductFromProposalList(addPaymentReq.proposalList);
                if (!ApplicationConfig.PRODUCTS_LIST.ContainsKey(addPaymentReq.product.ToUpper()))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "Product Does not Exist. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Product Does not Exist";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                bool isAllowed = string.Equals(authenticateTokenData.username, "ILTakeCare", StringComparison.OrdinalIgnoreCase) || ((authenticateTokenData.username ?? string.Empty).StartsWith((addPaymentReq.product ?? string.Empty).Split('_')[0], StringComparison.OrdinalIgnoreCase) && !authenticateTokenData.username.Contains("_"));
                if (!isAllowed)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "User not allowed for this Product. Request: " + JsonConvert.SerializeObject(addPaymentReq) + " Token Data: " + authenticateTokenData,
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "User not allowed for this Product. User: " + authenticateTokenData.username + " Product: " + addPaymentReq.product;
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.journeyType == "ADVANCE" && string.IsNullOrEmpty(addPaymentReq.prePaymentReturnUrl))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                    Message: "For Advance journeys, return url cannot be null. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "For Advance journeys, return url cannot be null.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.paymentFilters.ILEMI && (addPaymentReq.paymentFilters.NoCostEMI || addPaymentReq.paymentFilters.StandardEMI || addPaymentReq.paymentFilters.CardlessEMI || addPaymentReq.paymentFilters.BimaAsba))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "IL EMI cannot be combined with NoCostEMI/StandardEMI/CardlessEMI/BimaAsba payment options. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "IL EMI cannot be combined with NoCostEMI/StandardEMI/CardlessEMI/BimaAsba payment options.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.paymentFilters.ILEMI)
                {
                    var allowedWithILEMI = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                     {
                         nameof(PaymentFilters.UPI),
                         nameof(PaymentFilters.CreditCard),
                         nameof(PaymentFilters.DebitCard)
                     };

                    int enabledAllowedCount = 0;
                    bool hasAnyDisallowedEnabled = false;
                    foreach (var prop in typeof(PaymentFilters).GetProperties())
                    {
                        if (prop.PropertyType != typeof(bool))
                            continue;
                        if (prop.Name.Equals(nameof(PaymentFilters.ILEMI), StringComparison.OrdinalIgnoreCase))
                            continue;

                        bool isEnabled = (bool)(prop.GetValue(addPaymentReq.paymentFilters) ?? false);
                        if (!isEnabled)
                            continue;

                        if (allowedWithILEMI.Contains(prop.Name))
                            enabledAllowedCount++;
                        else hasAnyDisallowedEnabled = true;
                    }

                    if (enabledAllowedCount != 1 || hasAnyDisallowedEnabled)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                            Message: "Invalid paymentFilters for ILEMI. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "IL EMI must be combined with atleast/only one of these UPI/CreditCard/DebitCard payment options, and all other options must be false.";
                        response.TraceId = traceId;
                        response.Data = null;
                        return response;
                    }
                }

                if (addPaymentReq.paymentFilters.ILEMI && addPaymentReq.proposalList.Count() > 1)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "Multi-proposal transactions are not supported with EMI payment options. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Multi-proposal transactions are not supported with EMI payment options.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if(addPaymentReq.paymentFilters.ILEMI && addPaymentReq.emiDetails == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "EMI details are required for ILEMI payment option. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "EMI details are required for ILEMI payment option.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if(addPaymentReq.paymentFilters.ILEMI && (string.IsNullOrEmpty(addPaymentReq.emiDetails.installment_number) || string.IsNullOrEmpty(addPaymentReq.emiDetails.mandate_max_amount) || string.IsNullOrEmpty(addPaymentReq.emiDetails.installment_amount))) 
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "Installment number & mandate max amount & installment amount are required for ILEMI payment option. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Installment number & mandate max amount & installment amount are required for ILEMI payment option.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if(addPaymentReq.emiDetails != null && (Convert.ToDecimal(addPaymentReq.emiDetails?.mandate_max_amount) <= Convert.ToDecimal(addPaymentReq.emiDetails?.installment_amount)))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "Mandate max amount cannot be less than or equal to installment amount. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Mandate max amount cannot be less than or equal to installment amount.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (!string.IsNullOrEmpty(addPaymentReq.emiDetails?.prev_transaction_id) && addPaymentReq.paymentFilters.ILEMI)
                {
                    ILEMIValidateInstallmentReq iLEMIValidateInstallmentReq = new ILEMIValidateInstallmentReq()
                    {
                        transaction_id = addPaymentReq.emiDetails.prev_transaction_id,
                        installment_number = addPaymentReq.emiDetails.installment_number,
                        trace_id = traceId,
                        application_name = authenticateTokenData.username,
                        policy_number = "",
                        proposal_number = addPaymentReq.proposalList != null && addPaymentReq.proposalList.Count > 0 ? addPaymentReq.proposalList[0].ProposalNumber : "",
                    };

                    //if(addPaymentReq.proposalList != null && addPaymentReq.proposalList.Count > 0)
                    //{
                    //    foreach (ProposalListv2 proposal in addPaymentReq.proposalList)
                    //    {
                    //        iLEMIValidateInstallmentReq.proposal_number = proposal.ProposalNumber;
                    //    }
                    //}
                    //else
                    //{
                    //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    //    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                    //    Message: "Proposal List cannot be empty. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                    //    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    //    response.StatusCode = 0;
                    //    response.StatusMessage = "Failed";
                    //    response.Message = "Proposal List cannot be empty.";
                    //    response.TraceId = traceId;
                    //    response.Data = null;
                    //    return response;
                    //}

                    ILEMIValidationResponse = await orderBAL.ILEMIValidateInstallment(iLEMIValidateInstallmentReq, flow_type);
                    if (ILEMIValidationResponse.StatusCode != 1)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: ILEMIValidationResponse.Message + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                        return ILEMIValidationResponse;
                    }
                }
                else
                {
                    checkProposalV2Resp = await checkproposalv2(addPaymentReq, flow_type);

                    if (checkProposalV2Resp.is_error)
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = checkProposalV2Resp.error_message;
                        response.TraceId = checkProposalV2Resp.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (checkProposalV2Resp.addPaymentResp != null &&
                        !string.IsNullOrEmpty(checkProposalV2Resp.addPaymentResp.URL) &&
                        !string.IsNullOrEmpty(checkProposalV2Resp.addPaymentResp.TransactionID))
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = null;
                        response.TraceId = checkProposalV2Resp.trace_id;
                        response.Data = checkProposalV2Resp.addPaymentResp;
                        return response;
                    }
                }

                totalAmount = Convert.ToDecimal(addPaymentReq.totalAmount);

                if (addPaymentReq.paymentFilters.Autopay && addPaymentReq.paymentFilters.BimaAsba)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "Autopay and BimaAsba payment options cannot be used together. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Autopay and BimaAsba payment options cannot be used together.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.proposalList != null && addPaymentReq.proposalList.Count > 0)
                {
                    foreach (ProposalListv2 proposal in addPaymentReq.proposalList)
                    {
                        if (string.IsNullOrEmpty(proposal.ProposalAmount))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                Message: "proposal amount cannot be empty in the Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Proposal amount cannot be empty in the Proposal List for proposal number: " + proposal.ProposalNumber;
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (string.IsNullOrEmpty(proposal.ProposalType))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                Message: "proposal type cannot be empty in the Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Proposal Type cannot be empty in the Proposal List for proposal number: " + proposal.ProposalNumber;
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        if (string.IsNullOrEmpty(proposal.core))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                Message: "core cannot be empty in the Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Core cannot be empty in the Proposal List for proposal number: " + proposal.ProposalNumber;
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }

                        inputtotalProposalAmount += Convert.ToDecimal(proposal.ProposalAmount);

                        if (addPaymentReq.journeyType != "ADVANCE")
                        {
                            if (string.IsNullOrEmpty(proposal.ProposalNumber))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                    Message: "proposal number cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal number cannot be empty in the Proposal List";
                                response.TraceId = traceId;
                                response.Data = null;
                                return response;
                            }

                            fetchProposalDetailsResp = await FetchProposalDetails(traceId, proposal.ProposalNumber, proposal.ProposalAmount, proposal.ProposalId, proposal.ProposalType, proposal.core, flow_type, website_correlation_id);
                            if (fetchProposalDetailsResp != null && fetchProposalDetailsResp.Success)
                            {
                                if (addPaymentReq.paymentFilters.ILEMI && (!fetchProposalDetailsResp.ProposalTenure.HasValue || fetchProposalDetailsResp.ProposalTenure > 1))
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                    Message: "Payment mode ILEMI not applicable for Proposal Tenure is null or > 1, for proposal Number: " + proposal.ProposalNumber + "Tenure" + fetchProposalDetailsResp.ProposalTenure + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Payment mode ILEMI not applicable for Proposal Tenure is null or > 1";
                                    response.TraceId = traceId;
                                    response.Data = null;
                                    return response;
                                }

                                totalProposalAmount += Convert.ToDecimal(fetchProposalDetailsResp.PremiumDetails.TotalPremium);

                                if (string.IsNullOrEmpty(fetchProposalDetailsResp.proposalNumber) || string.IsNullOrEmpty(fetchProposalDetailsResp.proposalAmount))
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                        Message: "Proposal Number or Proposal Amount cannot be empty in the Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Proposal Number or Proposal Amount cannot be empty in the Proposal List";
                                    response.TraceId = traceId;
                                    response.Data = null;
                                    return response;
                                }

                                if (addPaymentReq.journeyType != "ENDORSEMENT" && Math.Abs(Convert.ToDecimal(fetchProposalDetailsResp.proposalAmount) - Convert.ToDecimal(fetchProposalDetailsResp.PremiumDetails.TotalPremium)) > 5)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                        Message: "Proposal Amount does not match with amount fetched from core for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Proposal Amount does not match with amount fetched from core";
                                    response.TraceId = traceId;
                                    response.Data = null;
                                    return response;
                                }

                                if (addPaymentReq.payerType.ToUpper() == "C" && string.IsNullOrEmpty(fetchProposalDetailsResp.CustomerDetails?.CustomerId))
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                        Message: "Customer ID cannot be empty for payertype 'C' for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Customer ID cannot be empty for payertype 'C' for proposal number: " + proposal.ProposalNumber;
                                    response.TraceId = traceId;
                                    response.Data = null;
                                    return response;
                                }

                                if (addPaymentReq.payerType.ToUpper() == "I" && (string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode) && string.IsNullOrEmpty(addPaymentReq.intermediaryCode)))
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                        Message: "Intermediary Code cannot be empty for payertype 'I' for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Intermediary Code cannot be empty for payertype 'I' for proposal number: " + proposal.ProposalNumber;
                                    response.TraceId = traceId;
                                    response.Data = null;
                                    return response;
                                }

                                if (fetchProposalDetailsResp.core.ToUpper() == "PF")
                                {
                                    pfCustomerIdSet.Add(fetchProposalDetailsResp.CustomerDetails?.CustomerId);
                                    if (pfCustomerIdSet.Count > 1)
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "Customer ID cannot be different for the PF proposal numbers. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Customer ID cannot be different for the PF proposal numbers";
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }

                                    if (string.IsNullOrEmpty(fetchProposalDetailsResp.DealId) && (string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode) && string.IsNullOrEmpty(addPaymentReq.intermediaryCode)))
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "Either Deal ID or Intermediary Code is required in PF in the Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Either Deal ID or Intermediary Code is required in PF in the Proposal List for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }

                                    if (!string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalStatus))
                                    {
                                        if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(fetchProposalDetailsResp.ProposalStatus.ToUpper()))
                                        {
                                            fetchProposalDetailsResp.IsNSTPFlowStr = "N";
                                        }
                                        else
                                        {
                                            fetchProposalDetailsResp.IsNSTPFlowStr = "Y";
                                        }
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "PF Proposal Status Fetching Failed. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Could not fetch PF Proposal Status.";
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }
                                }
                                else if (fetchProposalDetailsResp.core.ToUpper() == "ARTEMIS")
                                {
                                    artemisCustomerIdSet.Add(fetchProposalDetailsResp.CustomerDetails?.CustomerId);
                                    if (artemisCustomerIdSet.Count > 1)
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "Customer ID cannot be different for the Artemis proposal numbers. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Customer ID cannot be different for the Artemis proposal numbers";
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }

                                    if (string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode) && string.IsNullOrEmpty(addPaymentReq.intermediaryCode))
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "Intermediary Code is required in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Intermediary Code is required in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }

                                    if (string.IsNullOrEmpty(fetchProposalDetailsResp.BusinessVertical) || string.IsNullOrEmpty(fetchProposalDetailsResp.SubVertical) || string.IsNullOrEmpty(fetchProposalDetailsResp.MarketingOfficer))
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "Business Vertical / Sub Vertical / Marketing Officer are required in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Business Vertical / Sub Vertical / Marketing Officer are required in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }

                                    if (string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalStatus) && string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalSubStatus))
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                            Message: "Proposal Status and Proposal Sub Status are required in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Proposal Status and Proposal Sub Status are required in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }

                                    if (fetchProposalDetailsResp.IsNSTPFlow == null)
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "IsNSTPFlow is reqiured in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "IsNSTPFlow is reqiured in Artemis in Proposal List for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }
                                    fetchProposalDetailsResp.IsNSTPFlowStr = fetchProposalDetailsResp.IsNSTPFlow == true ? "Y" : "N";
                                }

                                if (addPaymentReq.journeyType != "ENDORSEMENT")
                                {
                                    if (string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalStartDate))
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                            Message: "Proposal Start Date is required for JourneyType: " + addPaymentReq.journeyType
                                                     + ". Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                            TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Proposal Start Date is required for JourneyType: " + addPaymentReq.journeyType;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }

                                    bool isValidParsedDate = dateFormatter.TryNormalizeDate(fetchProposalDetailsResp.ProposalStartDate, out DateTime parsedDate);
                                    if (!isValidParsedDate)
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                           MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                           Message: "Invalid Proposal Start Date for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                           TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Invalid Proposal Start Date. Please provide valid proposal start date for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;
                                    }
                                    proposalStartDate = parsedDate;

                                    DateTime minAllowedDate = DateTime.Today;
                                    DateTime maxAllowedDate = DateTime.Today.AddDays(180);
                                    DateTime maxAllowedDateHome = DateTime.Today.AddYears(1);

                                    if ((proposalStartDate < minAllowedDate || proposalStartDate > maxAllowedDate) && !addPaymentReq.product.StartsWith("HOME"))
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                        Message: "Proposal Start Date is outside allowed range. Date: " + proposalStartDate + ", Allowed Range: " + minAllowedDate + " to " + maxAllowedDate + " for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Proposal Start Date should be today or any date within the next 180 days for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;

                                    }
                                    else if ((proposalStartDate < minAllowedDate || proposalStartDate > maxAllowedDateHome) && addPaymentReq.product.StartsWith("HOME"))
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                        Message: "Proposal Start Date is outside allowed range. Date: " + proposalStartDate + ", Allowed Range: " + minAllowedDate + " to " + maxAllowedDate + " for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Proposal Start Date should be today or any date within the next 1 year for proposal number: " + proposal.ProposalNumber;
                                        response.TraceId = traceId;
                                        response.Data = null;
                                        return response;

                                    }
                                    //var tokenMinutes = Convert.ToInt32(ApplicationConfig.TOKEN_EXPIRY_TIME);
                                    //TimeOnly currentTimeOnly = TimeOnly.FromDateTime(DateTime.Now);
                                    //TimeOnly lastAllowedTimeOnly = TimeOnly.FromDateTime(DateTime.Today.AddMinutes(-tokenMinutes).AddSeconds(-1));

                                    //if (proposalStartDate.Date == DateTime.Today && currentTimeOnly > lastAllowedTimeOnly)
                                    //{
                                    //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    //        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                    //        Message: "Proposal Start Date cannot be before Payment Date. Proposal Start Date: " + proposalStartDate + " for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    //        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                    //    response.StatusCode = 0;
                                    //    response.StatusMessage = "Failed";
                                    //    response.Message = "Proposal Start Date cannot be before Payment Date for proposal number: " + proposal.ProposalNumber;
                                    //    response.TraceId = traceId;
                                    //    response.Data = null;
                                    //    return response;
                                    //}
                                }

                                fetchProposalDetailsRespList.Add(fetchProposalDetailsResp);
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                    Message: "Could not fetch Proposal Details for proposal number: " + proposal.ProposalNumber + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Could not fetch Proposal Details for proposal number: " + proposal.ProposalNumber;
                                response.TraceId = traceId;
                                response.Data = null;
                                return response;
                            }
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(proposal.DealId) && string.IsNullOrEmpty(proposal.IntermediaryCode))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                    Message: "Either Deal Id or Intermediary Code is required in the Proposal List for journey type ADVANCE. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Either Deal Id or Intermediary Code is required in the Proposal List for journey type ADVANCE";
                                response.TraceId = traceId;
                                response.Data = null;
                                return response;
                            }

                            if (string.IsNullOrEmpty(proposal.CustomerId))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                    Message: "Customer Id cannot be empty in the Proposal List for journey type ADVANCE. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Customer Id cannot be empty in the Proposal List for journey type ADVANCE";
                                response.TraceId = traceId;
                                response.Data = null;
                                return response;
                            }
                        }
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                    Message: "Proposal List cannot be null for Journey Type: " + addPaymentReq.journeyType + ". Request: " + JsonConvert.SerializeObject(addPaymentReq),
                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Proposal List cannot be null for Journey Type: " + addPaymentReq.journeyType;
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.journeyType != "ADVANCE" && addPaymentReq.journeyType != "ENDORSEMENT" && Math.Abs(totalAmount - totalProposalAmount) > addPaymentReq.proposalList.Count * 5)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "The Total Amount does not match with the Proposal Amount Sum. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "The Total Amount does not match with the Proposal Amount Sum.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (totalAmount != inputtotalProposalAmount)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "The Total Amount does not match with the input Proposal Amount Sum. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "The Total Amount does not match with the input Proposal Amount Sum.";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }

                if (addPaymentReq.product.Contains("SME"))
                {

                    rm_text = await dropLeadBAL.RMDetails(traceId, addPaymentReq.product);
                }

                //if (addPaymentReq.journeyType.ToUpper().Contains("PORTABILITY") && !addPaymentReq.product.Contains("HEALTH"))
                //{
                //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                //        MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                //        Message: "Porting is allowed only for Health Products. Request: " + addPaymentReq,
                //        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                //    response.StatusCode = 0;
                //    response.StatusMessage = "Failed";
                //    response.Message = "Porting is allowed only for Health Products.";
                //    response.TraceId = traceId;
                //    response.Data = null;
                //    return response;
                //}

                addPaymentResp = await paymentDAL.LogDbTransactionv2(authenticateTokenData, addPaymentReq, traceId, totalAmount, rm_text, fetchProposalDetailsRespList);

                if (addPaymentResp != null && !string.IsNullOrEmpty(addPaymentResp.TransactionID))
                {
                    if (!string.IsNullOrEmpty(addPaymentResp.pg_name) && addPaymentResp.pg_name.ToUpper() == "PLUTUS")
                    {
                        baseReturnLoaderUrl = ApplicationConfig.RETURN_LOADER_URL;
                        encryptedId = encryptDecrypt.EncryptRequest($"{addPaymentResp.TransactionID}|{traceId}");
                        if (string.IsNullOrEmpty(baseReturnLoaderUrl) || string.IsNullOrEmpty(encryptedId))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                               TraceId: traceId,
                               Message: "Unable to fetch Return Loader URL for the Given Template ID. Base URL: " + baseReturnLoaderUrl + " Encrypted ID: " + encryptedId + " Transaction ID: " + addPaymentResp.TransactionID + " Trace ID: " + traceId + " Request: " + JsonConvert.SerializeObject(addPaymentReq)), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch Return Loader URL for the Given Template ID";
                            response.TraceId = traceId;
                            response.Data = null;
                            return response;
                        }
                        fullReturnLoaderUrl = $"{baseReturnLoaderUrl}{addPaymentResp.TransactionID}&gatewayid=";

                        //generateDpsOrderId = await orderDAL.GetNextDPSOrderSequence();

                        PlutusAddPaymentResponse plutusAddPaymentResponse = await plutusBAL.PlutusAddPaymentRequestv2(addPaymentReq, traceId, addPaymentResp.TransactionID, fullReturnLoaderUrl, fetchProposalDetailsRespList, flow_type);
                        if (plutusAddPaymentResponse.statusCode == 200 && plutusAddPaymentResponse.isSuccess)
                        {
                            order_id = Convert.ToString(plutusAddPaymentResponse.data.transactionId);
                            addPaymentResp.URL = plutusAddPaymentResponse.data.paymentURL;

                            await paymentDAL.updatePaymentLink(traceId, addPaymentResp.TransactionID, addPaymentResp.URL);

                            bool saved = await orderDAL.InsertJuspayOrderDetails(addPaymentResp.TransactionID, traceId, addPaymentReq.totalAmount, order_id, "", addPaymentResp.TransactionID, "");
                            encryptedId = encryptDecrypt.EncryptRequest($"{addPaymentResp.TransactionID}|{traceId}");
                            addPaymentResp.URL = ApplicationConfig.DPS_PROXY_URL + encryptedId;
                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = null;
                            response.TraceId = traceId;
                            response.Data = addPaymentResp;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                    Message: "Payment Link Generation Error. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                    TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Payment Link Generation Error";
                            response.TraceId = traceId;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        basePaymentLink = ApplicationConfig.PAYMENT_BASE_URL;
                        encryptedId = encryptDecrypt.EncryptRequest($"{addPaymentResp.TransactionID}|{traceId}");

                        if (!string.IsNullOrEmpty(basePaymentLink) && !string.IsNullOrEmpty(encryptedId))
                        {
                            paymentLink = $"{basePaymentLink}?id={encryptedId}";
                            await paymentDAL.updatePaymentLink(traceId, addPaymentResp.TransactionID, paymentLink);

                            addPaymentResp.URL = paymentLink;
                            addPaymentResp.URL = ApplicationConfig.DPS_PROXY_URL + encryptedId;

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = null;
                            response.TraceId = traceId;
                            response.Data = addPaymentResp;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                                Message: "Payment Link Generation Error. Base URL: " + basePaymentLink + " Encrypted ID: " + encryptedId + " Transaction ID: " + addPaymentResp.TransactionID + " Request: " + JsonConvert.SerializeObject(addPaymentReq),
                                TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Error while generating Payment Link";
                            response.TraceId = traceId;
                            response.Data = null;
                        }
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "Payment Link Generation Error: Transaction ID is Null. Request: " + JsonConvert.SerializeObject(addPaymentReq),
                        TraceId: traceId), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Failed to process the payment request.";
                    response.TraceId = traceId;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        TraceId: traceId,
                        Message: "Request: " + JsonConvert.SerializeObject(addPaymentReq) + " Response: " + JsonConvert.SerializeObject(response),
                        StackTrace: ""
                    ),
                    ApplicationConfig.PAYMENT_LOGS
                );


                if (addPaymentReq.product.Contains("MOTOR") && addPaymentReq.journeyType != "ADVANCE")
                {
                    _ = Task.Run(async () =>
                    {
                        string biztalk_token = string.Empty;
                        PfCustomerDetails pfCustomerDetails = new PfCustomerDetails();

                        try
                        {
                            biztalk_token = ilesbInterface.CreateBiztalkAuthToken(ApplicationConfig.BIZTALK_TOKEN_USERNAME, ApplicationConfig.BIZTALK_TOKEN_PASSWORD);
                            pfCustomerDetails = await ilesbInterface.FetchCustomerDetailsFromPF(biztalk_token, fetchProposalDetailsRespList[0].CustomerDetails?.CustomerId, traceId, flow_type);
                            if (pfCustomerDetails != null)
                            {
                                string customerAddress = pfCustomerDetails.MAILINGADDRESSLINE1 + " " + pfCustomerDetails.MAILINGADDRESSLINE2;
                                string state = pfCustomerDetails.MAILING_STATE_NAME;
                                string city = pfCustomerDetails.MAILING_CITYDISTRICT_NAME;
                                string pinCode = pfCustomerDetails.MAILINGPINCODE;

                                await paymentDAL.UpdateTransactionDetails(addPaymentResp.TransactionID, customerAddress, state, city, pinCode, traceId);
                            }
                        }
                        catch (Exception ex)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - AddPaymentRequestv2() - Background Customer API call for Customer Address, Pincode, City, State",
                                TraceId: traceId,
                                Message: $"Exception occurred: {ex.Message}",
                                StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
                        }
                    });
                }
            }
            return response;
        }

        public async Task<DPSStatusResponse> FetchPaymentTransaction(FetchPaymentReq fetchPaymentReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DPSStatusResponse authTokenResponse = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            DropLeadBAL dropLeadBAL = new DropLeadBAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            //JObject paymentDetails = new JObject();
            FetchPaymentResp fetchPaymentResp = new FetchPaymentResp();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            DateFormatter dateFormatter = new DateFormatter();

            string decID = string.Empty;
            string traceID = string.Empty;
            string transactionID = string.Empty;
            //string application_id = string.Empty;
            string authorization_token = string.Empty;
            string rm_details = string.Empty;

            DateTime proposalStartDate;
            DateTime link_expiry_time;

            try
            {
                decID = encryptDecrypt.DecryptResponse(fetchPaymentReq.ID);
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (string.IsNullOrEmpty(transactionID) || string.IsNullOrEmpty(traceID))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - FetchPaymentTransaction() ",
                            TraceId: traceID,
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID), ApplicationConfig.PAYMENT_LOGS);

                        response.Data = null;
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = traceID;
                        return response;
                    }

                    fetchPaymentResp = await paymentDAL.FetchPaymentTransaction(transactionID, traceID);
                    if (fetchPaymentResp != null)
                    {

                        //fetchPaymentResp = JsonConvert.DeserializeObject<FetchPaymentResp>(paymentDetails["transaction_data"].ToString());

                        fetchPaymentResp.ProductValue = ApplicationConfig.PRODUCTS_LIST[fetchPaymentResp.Product];
                        fetchPaymentResp.is_transaction_done = await paymentDAL.IsTransactionDone(transactionID);
                        //application_id = paymentDetails["application_id"].ToString();

                        link_expiry_time = DateTime.Now.AddMinutes(Convert.ToInt32(ApplicationConfig.TOKEN_EXPIRY_TIME));

                        if (fetchPaymentResp.created_on.HasValue && DateTime.Now > fetchPaymentResp.created_on.Value.AddHours(Convert.ToInt32(ApplicationConfig.PAYMENT_URL_LIFE_HOURS)))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                   Message: "Session Expired. Fetch Payment Transaction DB Response: " + JsonConvert.SerializeObject(fetchPaymentResp),
                                   TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 401;
                            response.StatusMessage = "Unauthorized";
                            response.Message = "Session Expired";
                            response.TraceId = traceID;
                            response.Data = null;
                            return response;
                        }

                        if (fetchPaymentResp.journey_type.ToUpper() != "ADVANCE" && fetchPaymentResp.journey_type.ToUpper() != "ENDORSEMENT")
                        {

                            bool isValidParsedDate = dateFormatter.TryNormalizeDate(fetchPaymentResp.ProposalStartDate, out DateTime parsedDate);
                            if (!isValidParsedDate)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - AddPaymentRequest() ",
                                   Message: "Invalid Proposal Start Date. Fetch Payment Transaction DB Response: " + JsonConvert.SerializeObject(fetchPaymentResp),
                                   TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Invalid Proposal Start Date. Please provide valid proposal start date";
                                response.TraceId = traceID;
                                response.Data = null;
                                return response;
                            }
                            proposalStartDate = parsedDate;

                            DateTime minAllowedDate = DateTime.Today;
                            DateTime maxAllowedDate = DateTime.Today.AddDays(180);
                            DateTime maxAllowedDateHome = DateTime.Today.AddYears(1);

                            if ((proposalStartDate < minAllowedDate || proposalStartDate > maxAllowedDate) && !fetchPaymentResp.Product.StartsWith("HOME") && fetchPaymentReq.is_landing_page == "Y")
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - FetchPaymentTransaction() ",
                                    Message: "Proposal Start Date is outside allowed range. Date: " + proposalStartDate + ", Allowed Range: " + minAllowedDate + " to " + maxAllowedDate,
                                    TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal Start Date should be today or any date within the next 180 days.";
                                response.TraceId = traceID;
                                response.Data = null;
                                return response;
                            }
                            else if ((proposalStartDate < minAllowedDate || proposalStartDate > maxAllowedDateHome) && fetchPaymentResp.Product.StartsWith("HOME") && fetchPaymentReq.is_landing_page == "Y")
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - FetchPaymentTransaction() ",
                                    Message: "Proposal Start Date is outside allowed range. Date: " + proposalStartDate + ", Allowed Range: " + minAllowedDate + " to " + maxAllowedDate,
                                    TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal Start Date should be today or any date within the next 1 Year.";
                                response.TraceId = traceID;
                                response.Data = null;
                                return response;
                            }

                            //var tokenMinutes = Convert.ToInt32(ApplicationConfig.TOKEN_EXPIRY_TIME);
                            //TimeOnly currentTimeOnly = TimeOnly.FromDateTime(DateTime.Now);
                            //TimeOnly lastAllowedTimeOnly = TimeOnly.FromDateTime(DateTime.Today.AddMinutes(-tokenMinutes).AddSeconds(-1));

                            //if (proposalStartDate.Date == DateTime.Today && currentTimeOnly > lastAllowedTimeOnly)
                            //{
                            //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            //        MethodName: "PaymentBAL.cs - FetchPaymentTransaction() ",
                            //        Message: "Proposal Start Date cannot be before Payment Date. Proposal Start Date: " + proposalStartDate,
                            //        TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                            //    response.StatusCode = 0;
                            //    response.StatusMessage = "Failed";
                            //    response.Message = "Proposal Start Date cannot be before Payment Date";
                            //    response.TraceId = traceID;
                            //    response.Data = null;
                            //    return response;
                            //}
                        }

                        if (fetchPaymentResp.journey_type.ToUpper() != "ADVANCE")
                        {
                            fetchPaymentResp.Email = MaskEmail(traceID, fetchPaymentResp.Email);
                            fetchPaymentResp.MobileNumber = MaskMobileNumber(traceID, fetchPaymentResp.MobileNumber);
                            fetchPaymentResp.Name = MaskDetails(traceID, fetchPaymentResp.Name);

                            if (string.IsNullOrEmpty(fetchPaymentResp.Email) || string.IsNullOrEmpty(fetchPaymentResp.MobileNumber))
                            {
                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Error in Mobile Number or Email Address format";
                                response.TraceId = traceID;
                                response.Data = null;
                                return response;
                            }
                        }

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = null;
                        response.TraceId = traceID;
                        response.Data = fetchPaymentResp;
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - FetchPaymentTransaction() ",
                                Message: "Unable to fetch transaction data. Transaction ID: " + transactionID,
                                TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                        response.Data = null;
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Data Not Found";
                        response.TraceId = traceID;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - FetchPaymentTransaction() ",
                               Message: "Invalid ID format. ID: " + fetchPaymentReq.ID,
                               TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                    response.Data = null;
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = traceID;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - FetchPaymentTransaction() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.Data = null;
                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = traceID;
            }

            return response;
        }

        public async Task<string> FetchProposalStatus(string trace_id, string proposal_number, string core, string flow_type)
        {
            ILESBInterface ilesbInterface = new ILESBInterface();
            FetchProposalStatusFromArtemisReq fetchProposalStatusFromArtemisReq = new FetchProposalStatusFromArtemisReq();
            FetchProposalStatusFromArtemisResp fetchProposalStatusFromArtemisResp = new FetchProposalStatusFromArtemisResp();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            JsonElement artemisProposalStatusResponse = new JsonElement();
            string policy_status = string.Empty;
            string policy_sub_status = string.Empty;

            PaymentDAL paymentDAL = new PaymentDAL();

            string proposal_status = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                if (core.ToUpper() == "PF")
                {
                    string apiUrl = ApplicationConfig.FETCH_ILESB_PROPOSAL_STATUS_API_URL + proposal_number;
                    var client = new RestClient(apiUrl);
                    var request = new RestRequest(apiUrl, Method.Get);
                    string Username = ApplicationConfig.BIZTALK_REFUND_USERNAME;
                    string Password = ApplicationConfig.BIZTALK_REFUND_PASSWORD;

                    string token = ilesbInterface.CreateBiztalkAuthToken(Username, Password);

                    request.AddHeader("Content-Type", "application/json");
                    request.AddHeader("ILAuthorization", token);

                    stopwatch = Stopwatch.StartNew();

                    RestResponse apiResponse = await client.ExecuteAsync(request);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(trace_id, proposal_number, apiUrl, apiResponse.Content, apiUrl, "ILESB FETCH PROPOSAL STATUS", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                    if (apiResponse != null && apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                    {
                        proposal_status = JsonConvert.DeserializeObject<string>(apiResponse.Content);
                    }
                }
                else if (core.ToUpper() == "ARTEMIS")
                {
                    string token = await apiFactoryInterface.GetApiFactoryToken(trace_id, flow_type);
                    var aesKey = Guid.NewGuid().ToString("N");

                    string apiUrl = ApplicationConfig.FETCH_APIFACTORY_PROPOSAL_STATUS_API_URL;

                    var request = new
                    {
                        proposal_no = proposal_number,
                    };

                    stopwatch = Stopwatch.StartNew();

                    string response = await apiFactoryInterface.ExecuteRequestEncypted(apiUrl, token, ApplicationConfig.ARTEMIS_MISC_PUBLIC_KEY, request, aesKey);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(trace_id, proposal_number, JsonConvert.SerializeObject(request), response, apiUrl, "APIFACTORY FETCH PROPOSAL STATUS", "", flow_type, JsonConvert.SerializeObject(responseTime));

                    if (!string.IsNullOrEmpty(response))
                    {
                        artemisProposalStatusResponse = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);
                        if (artemisProposalStatusResponse.TryGetProperty("data", out JsonElement dataElement))
                        {
                            if (dataElement.TryGetProperty("policy_status", out JsonElement policyStatusElement))
                            {
                                policy_status = policyStatusElement.GetString();
                            }
                            if (dataElement.TryGetProperty("policy_sub_status", out JsonElement policySubStatusElement))
                            {
                                policy_sub_status = policySubStatusElement.GetString();
                            }
                        }

                        if (string.IsNullOrEmpty(policy_sub_status) && !string.IsNullOrEmpty(policy_status))
                        {
                            proposal_status = policy_status;
                        }
                        else if (!string.IsNullOrEmpty(policy_sub_status))
                        {
                            proposal_status = policy_sub_status;
                        }
                    }
                }

                if (flow_type != "REALTIME - ADD PAYMENT REQUEST" && !string.IsNullOrEmpty(proposal_status))
                {
                    await paymentDAL.updateProposalStatus(trace_id, proposal_number, proposal_status);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "PaymentBAL.cs - FetchProposalStatus() ",
                   TraceId: trace_id,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return proposal_status;
        }

        public string MaskEmail(string traceID, string email)
        {
            if (string.IsNullOrEmpty(email))
                return email;

            try
            {
                var parts = email.Split('@');
                if (parts.Length != 2)
                    return email;

                var localPart = parts[0];
                var domainPart = parts[1];

                // Get first two characters of local part
                var firstTwo = localPart.Length >= 2 ? localPart.Substring(0, 2) : localPart;

                // Get domain extension (e.g., com, co.uk, org, etc.)
                var domainParts = domainPart.Split('.');
                string extension;

                // Handle cases like .co.uk (take last 2 parts) vs .com (take last 1 part)
                if (domainParts.Length >= 2 && domainParts[domainParts.Length - 2].Length <= 3)
                {
                    // For extensions like .co.uk, .com.au, etc.
                    extension = string.Join(".", domainParts.Skip(domainParts.Length - 2));
                }
                else
                {
                    // For extensions like .com, .org, .net, etc.
                    extension = domainParts[domainParts.Length - 1];
                }

                // Format: first2chars***@***.extension
                return $"{firstTwo}***@***.{extension}";
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "PaymentBAL.cs - MaskEmail() ",
                   TraceId: traceID,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return null;
        }

        public string MaskMobileNumber(string traceID, string mobileNumber)
        {
            if (string.IsNullOrEmpty(mobileNumber))
                return mobileNumber;

            try
            {
                // Remove any non-digit characters
                var digits = new string(mobileNumber.Where(char.IsDigit).ToArray());

                if (digits.Length < 3)
                    return mobileNumber;

                // Get last 3 digits
                var lastThree = digits.Substring(digits.Length - 3);

                // Format: XXXXXXX + last 3 digits
                return $"XXXXXXX{lastThree}";
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "PaymentBAL.cs - MaskMobileNumber() ",
                   TraceId: traceID,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return null;
        }

        public string MaskDetails(string traceID, string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            try
            {
                var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (int w = 0; w < words.Length; w++)
                {
                    var word = words[w];

                    int keepLength = word.Length / 2;
                    int maskLength = word.Length - keepLength;

                    words[w] = word[..keepLength] + new string('*', maskLength);
                }

                return string.Join(" ", words);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "PaymentBAL.cs - MaskDetails() ",
                   TraceId: traceID,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return null;
        }

        public async Task<DPSStatusResponse> TagProposal(TagProposalReq tagProposalReq, string username, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            CallbackBAL callbackBAL = new CallbackBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();
            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();

            HashSet<string> uniqueIsNSTPFlagList = new HashSet<string>();

            // PF 
            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            PFPolicyCreationResp pfPolicyCreationResp = new PFPolicyCreationResp();
            ILESBInterface ilesbInterface = new ILESBInterface();

            string pfPaymentId = string.Empty;
            string pfPolicyNumber = string.Empty;

            // Artemis
            ArtemisPolicyCreationReq artemisPolicyCreationReq = new ArtemisPolicyCreationReq();
            ArtemisPolicyCreationResp artemisPolicyCreationResp = new ArtemisPolicyCreationResp();
            ArtemisPolicyCreationRespTimeout artemisPolicyCreationRespTimeout = new ArtemisPolicyCreationRespTimeout();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();

            string receiptNumber = string.Empty;
            string artemisPolicyNumber = string.Empty;

            TagProposalResp tagProposalResp = new TagProposalResp();

            List<SyncProposalToSource> syncProposalToSourceList = new List<SyncProposalToSource>();

            try
            {
                if (string.IsNullOrEmpty(tagProposalReq.ProposalList.ProposalNumber) || string.IsNullOrEmpty(tagProposalReq.ProposalList.ProposalAmount))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "PaymentBAL.cs - TagProposal() ",
                      Message: "Proposal Number or Proposal Amount cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                      TraceId: tagProposalReq.TraceID), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Proposal Number or Proposal Amount cannot be empty in the Proposal List";
                    response.TraceId = tagProposalReq.TraceID;
                    response.Data = null;
                    return response;
                }

                if (string.IsNullOrEmpty(tagProposalReq.ProposalList.CustomerId))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - TagProposal() ",
                        Message: "Customer ID cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                        TraceId: tagProposalReq.TraceID), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Customer ID cannot be empty in the Proposal List";
                    response.TraceId = tagProposalReq.TraceID;
                    response.Data = null;
                    return response;
                }

                await paymentDAL.UpdateAdvanceDBTransaction(tagProposalReq);

                fetchPaymentDetails = await paymentDAL.FetchAdvancePaymentDetails(tagProposalReq.TransactionID);
                if (fetchPaymentDetails == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "PaymentBAL.cs - TagProposal() ",
                      TraceId: tagProposalReq.TraceID,
                      Message: "Cannot find details from the provided Transaction ID. TagProposal Request: " + tagProposalReq), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Cannot find details from the provided Transaction ID";
                    response.TraceId = tagProposalReq.TraceID;
                    response.Data = null;
                }
                else
                {
                    if (fetchPaymentDetails.total_amount != tagProposalReq.ProposalList.ProposalAmount)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposal() ",
                            Message: "The Total Amount does not match with the Proposal Amount Sum. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                            TraceId: tagProposalReq.TraceID), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "The Total Amount does not match with the Proposal Amount Sum.";
                        response.TraceId = tagProposalReq.TraceID;
                        response.Data = null;
                        return response;
                    }

                    if (fetchPaymentDetails.core.ToUpper() == "PF")
                    {
                        if (string.IsNullOrEmpty(tagProposalReq.ProposalList.DealId) && string.IsNullOrEmpty(tagProposalReq.ProposalList.IntermediaryCode))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposal() ",
                                Message: "Either Deal ID or Intermediary Code is required in the Proposal List. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                TraceId: tagProposalReq.TraceID), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Either Deal ID or Intermediary Code is required in the Proposal List";
                            response.TraceId = null;
                            response.Data = null;
                            return response;
                        }

                        if (string.IsNullOrEmpty(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status))
                        {
                            fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status = await FetchProposalStatus(fetchPaymentDetails.trace_id, fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, fetchPaymentDetails.core, flow_type);
                        }

                        if (!string.IsNullOrEmpty(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status))
                        {
                            if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status.ToUpper()))
                            {
                                fetchPaymentDetails.fetchPaymentProposalList[0].is_nstp_flow = "N";
                            }
                            else
                            {
                                fetchPaymentDetails.fetchPaymentProposalList[0].is_nstp_flow = "Y";
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposal() ",
                                Message: "PF Proposal Status Fetching Failed. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                TraceId: tagProposalReq.TraceID), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Could not fetch PF Proposal Status.";
                            response.TraceId = null;
                            response.Data = null;
                            return response;
                        }
                    }
                    else if (fetchPaymentDetails.core.ToUpper() == "ARTEMIS")
                    {
                        fetchArtemisProposalDetails = await apiFactoryInterface.FetchArtemisProposalDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, tagProposalReq.TraceID, username, flow_type);

                        if (fetchArtemisProposalDetails != null && !string.IsNullOrEmpty(fetchArtemisProposalDetails.intermediary_code) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.is_nstp_flow) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.business_vertical) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.sub_vertical) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.marketing_officer) && !string.IsNullOrEmpty(fetchArtemisProposalDetails.proposer_code))
                        {
                            fetchPaymentDetails.fetchPaymentProposalList[0].is_nstp_flow = fetchArtemisProposalDetails.is_nstp_flow;
                            fetchPaymentDetails.fetchPaymentProposalList[0].business_vertical = fetchArtemisProposalDetails.business_vertical;
                            fetchPaymentDetails.fetchPaymentProposalList[0].sub_vertical = fetchArtemisProposalDetails.sub_vertical;
                            fetchPaymentDetails.fetchPaymentProposalList[0].marketing_officer = fetchArtemisProposalDetails.marketing_officer;
                            fetchPaymentDetails.fetchPaymentProposalList[0].intermediary_code = fetchArtemisProposalDetails.intermediary_code;
                            fetchPaymentDetails.fetchPaymentProposalList[0].proposer_code = fetchArtemisProposalDetails.proposer_code;
                            fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status = string.IsNullOrEmpty(fetchArtemisProposalDetails.policy_sub_status) ? fetchArtemisProposalDetails.policy_status : fetchArtemisProposalDetails.policy_sub_status;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - TagProposal() ",
                               Message: "Artemis Proposal Details Fetching Failed. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                               TraceId: tagProposalReq.TraceID), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Could not fetch Artemis Proposal Details.";
                            response.TraceId = null;
                            response.Data = null;
                            return response;
                        }
                    }

                    await paymentDAL.UpdateAdvanceProposalDetails(
                        fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number,
                        fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status,
                        fetchPaymentDetails.fetchPaymentProposalList[0].is_nstp_flow,
                        fetchPaymentDetails.fetchPaymentProposalList[0].business_vertical,
                        fetchPaymentDetails.fetchPaymentProposalList[0].sub_vertical,
                        fetchPaymentDetails.fetchPaymentProposalList[0].marketing_officer,
                        fetchPaymentDetails.fetchPaymentProposalList[0].intermediary_code,
                        fetchPaymentDetails.fetchPaymentProposalList[0].proposer_code,
                        fetchPaymentDetails.trace_id);

                    foreach (FetchPaymentProposalList fetchPaymentProposal in fetchPaymentDetails.fetchPaymentProposalList)
                    {
                        uniqueIsNSTPFlagList.Add(fetchPaymentProposal.is_nstp_flow);
                    }

                    if (fetchPaymentDetails.core.ToUpper() == "PF")
                    {
                        // PF
                        // Create Payment Entry and Tagging Request Body (Check if any of the proposal statuses are NCCN or RCCN. If Yes, then do both Entry and Tagging, otherwise only Entry)
                        if (uniqueIsNSTPFlagList.Contains("N"))
                        {
                            pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(true, true, fetchPaymentDetails);
                        }
                        else
                        {
                            pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(true, false, fetchPaymentDetails);
                        }

                        if (pfPolicyCreationReq == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposal() ",
                                TraceId: tagProposalReq.TraceID,
                                Message: "Error while creating request body for PF Policy Creation. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                            response.TraceId = tagProposalReq.TraceID;
                            response.Data = null;
                        }
                        else
                        {
                            // ILESB Payment Entry and Tagging API Call
                            pfPolicyCreationResp = await ilesbInterface.GeneratePFPolicyData(pfPolicyCreationReq, flow_type);

                            if (pfPolicyCreationResp != null && pfPolicyCreationResp.StatusMessage.ToUpper() == "SUCCESS")
                            {
                                if (pfPolicyCreationResp.paymentEntryResponse != null && pfPolicyCreationResp.paymentEntryResponse.Status.ToUpper() == "SUCCESS")
                                {
                                    pfPaymentId = pfPolicyCreationResp.paymentEntryResponse.PaymentID;
                                    await callbackDAL.UpdateCorePaymentID(pfPaymentId, fetchPaymentDetails.transaction_id);

                                    if (pfPolicyCreationResp.paymentTagResponse != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList.Count > 0 && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].Status.ToUpper() == "SUCCESS" && !string.IsNullOrEmpty(pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo) && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo != "0")
                                    {
                                        pfPolicyNumber = pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo;

                                        SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                        {
                                            policy_number = pfPolicyNumber,
                                            payment_id = pfPaymentId,
                                            proposal_number = pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].ProposalNo,
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

                                        tagProposalResp.PolicyNumber = pfPolicyNumber;
                                        response.StatusCode = 1;
                                        response.StatusMessage = "Success";
                                        response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                                        response.TraceId = tagProposalReq.TraceID;
                                        response.Data = tagProposalResp;
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - TagProposal() ",
                                            TraceId: tagProposalReq.TraceID,
                                            Message: "Policy Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq) + " GeneratePFPolicyData Response Body: " + JsonConvert.SerializeObject(pfPolicyCreationResp)), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                        response.TraceId = tagProposalReq.TraceID;
                                        response.Data = null;
                                    }

                                    await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, pfPolicyNumber);
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposal() ",
                                        TraceId: tagProposalReq.TraceID,
                                        Message: "Payment ID Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq) + " GeneratePFPolicyData Response Body: " + JsonConvert.SerializeObject(pfPolicyCreationResp)), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                    response.TraceId = tagProposalReq.TraceID;
                                    response.Data = null;
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposal() ",
                                   TraceId: tagProposalReq.TraceID,
                                   Message: "Error while calling Policy Creation API. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                response.TraceId = tagProposalReq.TraceID;
                                response.Data = null;
                            }
                        }
                    }
                    else if (fetchPaymentDetails.core.ToUpper() == "ARTEMIS")
                    {
                        // ARTEMIS
                        // Create Payment Entry and Tagging Request Body (Check if any of the proposal statuses are NCCN or RCCN. If Yes, then do both Entry and Tagging, otherwise only Entry)
                        if (uniqueIsNSTPFlagList.Contains("N"))
                        {
                            artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                        }
                        else
                        {
                            artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                        }

                        if (artemisPolicyCreationReq == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposal() ",
                                TraceId: tagProposalReq.TraceID,
                                Message: "Error while creating request body for Artemis Policy Creation. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                            response.TraceId = tagProposalReq.TraceID;
                            response.Data = null;
                        }
                        else
                        {
                            // API FACTORY Payment Entry and Tagging API Call
                            RestResponse apiResponse = await apiFactoryInterface.GenerateArtemisPolicyData(tagProposalReq.TraceID, artemisPolicyCreationReq, flow_type);

                            if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                            {
                                artemisPolicyCreationResp = JsonConvert.DeserializeObject<ArtemisPolicyCreationResp>(apiResponse.Content);
                            }
                            else if (apiResponse.StatusCode == HttpStatusCode.GatewayTimeout && !string.IsNullOrEmpty(apiResponse.Content))
                            {
                                artemisPolicyCreationRespTimeout = JsonConvert.DeserializeObject<ArtemisPolicyCreationRespTimeout>(apiResponse.Content);
                            }

                            if (artemisPolicyCreationResp != null && artemisPolicyCreationResp.data != null && artemisPolicyCreationResp.statusCode == 200)
                            {
                                if (artemisPolicyCreationResp.data.receiptStatus.ToUpper() == "SUCCESSFUL")
                                {
                                    receiptNumber = artemisPolicyCreationResp.data.receiptNumber;
                                    await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);

                                    if (artemisPolicyCreationResp.data.tagging != null && artemisPolicyCreationResp.data.tagging.Count > 0 && !string.IsNullOrEmpty(artemisPolicyCreationResp.data.tagging[0].policyNumber))
                                    {
                                        artemisPolicyNumber = artemisPolicyCreationResp.data.tagging[0].policyNumber;

                                        SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                        {
                                            policy_number = artemisPolicyNumber,
                                            payment_id = receiptNumber,
                                            proposal_number = artemisPolicyCreationResp.data.tagging[0].proposalNumber,
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

                                        tagProposalResp.PolicyNumber = artemisPolicyNumber;
                                        response.StatusCode = 1;
                                        response.StatusMessage = "Success";
                                        response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                                        response.TraceId = tagProposalReq.TraceID;
                                        response.Data = tagProposalResp;
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - TagProposal() ",
                                            TraceId: tagProposalReq.TraceID,
                                            Message: "Policy Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationResp), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                        response.TraceId = tagProposalReq.TraceID;
                                        response.Data = null;
                                    }

                                    await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, artemisPolicyNumber);
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposal() ",
                                        TraceId: tagProposalReq.TraceID,
                                        Message: "Receipt Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationResp), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                    response.TraceId = tagProposalReq.TraceID;
                                    response.Data = null;
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposal() ",
                                   TraceId: tagProposalReq.TraceID,
                                   Message: "Error while calling Artemis Policy Creation API. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                response.TraceId = tagProposalReq.TraceID;
                                response.Data = null;
                            }

                            if (artemisPolicyCreationRespTimeout != null && artemisPolicyCreationRespTimeout.data != null && artemisPolicyCreationRespTimeout.statusCode == 400)
                            {
                                if (artemisPolicyCreationRespTimeout.data.taggingDetails != null && artemisPolicyCreationRespTimeout.data.taggingDetails.Count > 0 && !string.IsNullOrEmpty(artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedPolicyNumber) && !string.IsNullOrEmpty(artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedReceiptNumber))
                                {
                                    receiptNumber = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedReceiptNumber;
                                    artemisPolicyNumber = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedPolicyNumber;

                                    SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                    {
                                        policy_number = artemisPolicyNumber,
                                        payment_id = receiptNumber,
                                        proposal_number = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedProposalNumber,
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

                                    tagProposalResp.PolicyNumber = artemisPolicyNumber;
                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                                    response.TraceId = tagProposalReq.TraceID;
                                    response.Data = tagProposalResp;
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposal() ",
                                        TraceId: tagProposalReq.TraceID,
                                        Message: "Receipt Number / Policy Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationRespTimeout), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                    response.TraceId = tagProposalReq.TraceID;
                                    response.Data = null;
                                }

                                await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);
                                await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, artemisPolicyNumber);
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposal() ",
                                   TraceId: tagProposalReq.TraceID,
                                   Message: "Error while calling Artemis Policy Creation API. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                response.TraceId = tagProposalReq.TraceID;
                                response.Data = null;
                            }
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposal() ",
                            TraceId: tagProposalReq.TraceID,
                            Message: "Invalid Core fetched from DB. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid Core fetched from DB";
                        response.TraceId = tagProposalReq.TraceID;
                        response.Data = null;
                    }

                    await callbackDAL.UpdateNoOfAttemptsCountByTransactionID(tagProposalReq.TransactionID);

                    foreach (var reverse_sync_proposal in syncProposalToSourceList)
                    {
                        await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - TagProposal() ",
                    TraceId: tagProposalReq.TraceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        //public async Task<DPSStatusResponse> GetSendPaymentLinkData(GetSendPaymentLinkDataReq getSendPaymentLinkDataReq)
        //{
        //    EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
        //    string transactionID = string.Empty;
        //    string traceID = string.Empty;
        //    DPSStatusResponse response = new DPSStatusResponse();
        //    PaymentDAL paymentDAL = new PaymentDAL();
        //    JObject paymentDetails = new JObject();
        //    FetchPaymentResp fetchPaymentResp = new FetchPaymentResp();
        //    GetSendPaymentLinkDataResp getSendPaymentLinkDataResp = new GetSendPaymentLinkDataResp();
        //    AuthenticateBAL authenticateBAL = new AuthenticateBAL();
        //    DPSStatusResponse authTokenResponse = new DPSStatusResponse();

        //    try
        //    {
        //        string decID = encryptDecrypt.DecryptResponse(getSendPaymentLinkDataReq.ID);
        //        if (decID.Contains("|"))
        //        {
        //            string[] IDParts = decID.Split("|");

        //            transactionID = IDParts[0];
        //            traceID = IDParts[1];

        //            if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
        //            {
        //                paymentDetails = await paymentDAL.FetchPaymentTransaction(transactionID, traceID);
        //                if (paymentDetails != null)
        //                {
        //                    fetchPaymentResp = JsonConvert.DeserializeObject<FetchPaymentResp>(paymentDetails["transaction_data"].ToString());
        //                    fetchPaymentResp.ProductValue = ApplicationConfig.PRODUCTS_LIST[fetchPaymentResp.Product];
        //                    getSendPaymentLinkDataResp.product_value = fetchPaymentResp.ProductValue;
        //                    getSendPaymentLinkDataResp.email = fetchPaymentResp.Email;
        //                    getSendPaymentLinkDataResp.mobile_number = fetchPaymentResp.MobileNumber;
        //                    getSendPaymentLinkDataResp.proposal_list = fetchPaymentResp.ProposalList;
        //                    getSendPaymentLinkDataResp.amount = fetchPaymentResp.Amount;
        //                    getSendPaymentLinkDataResp.net_premium = fetchPaymentResp.basic_premium;
        //                    getSendPaymentLinkDataResp.gst = fetchPaymentResp.gst;
        //                    getSendPaymentLinkDataResp.product = fetchPaymentResp.Product;
        //                    getSendPaymentLinkDataResp.tokenExpiryTime = fetchPaymentResp.authTokenData.expiryTime;

        //                    authTokenResponse = authenticateBAL.CreateAuthToken(fetchPaymentResp.ApplicationName, paymentDetails["application_id"].ToString());
        //                    if (authTokenResponse.StatusMessage.ToUpper() == "SUCCESS")
        //                    {
        //                        string json = JsonConvert.SerializeObject(authTokenResponse.Data);
        //                        if (json != null)
        //                        {
        //                            using var doc = JsonDocument.Parse(json);

        //                            if (doc.RootElement.TryGetProperty("accessToken", out var accessToken))
        //                            {
        //                                getSendPaymentLinkDataResp.token = accessToken.GetString();
        //                            }
        //                        }

        //                        await paymentDAL.updateIsFirstTxnVisit(traceID, transactionID, getSendPaymentLinkDataResp.tokenExpiryTime, fetchPaymentResp.authTokenData.accessToken);
        //                    }
        //                    response.StatusCode = 1;
        //                    response.StatusMessage = "Success";
        //                    response.Message = null;
        //                    response.TraceId = null;
        //                    response.Data = getSendPaymentLinkDataResp;
        //                }
        //                else
        //                {
        //                    response.StatusCode = 0;
        //                    response.StatusMessage = "Failed";
        //                    response.Message = "Transaction Details Not Found";
        //                    response.TraceId = null;
        //                    response.Data = null;
        //                }
        //            }
        //            else
        //            {
        //                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
        //                   MethodName: "PaymentBAL.cs - GetSendPaymentLinkData() ",
        //                   Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
        //                   TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

        //                response.StatusCode = 0;
        //                response.StatusMessage = "Failed";
        //                response.Message = "Invalid ID format";
        //                response.TraceId = null;
        //                response.Data = null;
        //            }
        //        }
        //        else
        //        {
        //            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
        //                MethodName: "PaymentBAL.cs - GetSendPaymentLinkData() ",
        //                Message: "Invalid ID format. ID: " + getSendPaymentLinkDataReq.ID,
        //                TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

        //            response.StatusCode = 0;
        //            response.StatusMessage = "Failed";
        //            response.Message = "Invalid ID format";
        //            response.TraceId = null;
        //            response.Data = null;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
        //           MethodName: "PaymentBAL.cs - GetSendPaymentLinkData() ",
        //           TraceId: traceID,
        //           Message: $"Exception occurred: {ex.Message}",
        //           StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

        //        response.StatusCode = 0;
        //        response.StatusMessage = "Failed";
        //        response.Message = null;
        //        response.TraceId = null;
        //        response.Data = null;
        //    }
        //    return response;
        //}

        public async Task<DPSStatusResponse> SendPaymentLink(SendPaymentLinkReq sendPaymentLinkReq, string username, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            SendPaymentLinkResp sendPaymentLinkResp = new SendPaymentLinkResp();
            HashSet<string> sendPaymentLinkActions;
            GetMessageByProduct getMessageByProduct = new GetMessageByProduct();
            SendMessageService sendMessageService = new SendMessageService();
            PaymentDAL paymentDAL = new PaymentDAL();

            string transactionID = string.Empty;
            string traceID = string.Empty;

            List<GetPaymentLinkDetails> getPaymentLinkDetails = new List<GetPaymentLinkDetails>();

            string msgText = string.Empty;

            try
            {
                string decID = encryptDecrypt.DecryptResponse(sendPaymentLinkReq.ID);
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        getPaymentLinkDetails = await paymentDAL.GetPaymentLinkByTransactionID(transactionID, traceID);
                        if (getPaymentLinkDetails != null && !string.IsNullOrEmpty(getPaymentLinkDetails[0].product) && !string.IsNullOrEmpty(getPaymentLinkDetails[0].total_amount) && !string.IsNullOrEmpty(getPaymentLinkDetails[0].send_link_landing_url))
                        {
                            sendPaymentLinkActions = new HashSet<string>(sendPaymentLinkReq.paymentMethod.Contains("|") ? sendPaymentLinkReq.paymentMethod.Split('|') : new string[] { sendPaymentLinkReq.paymentMethod });
                            getMessageByProduct = await paymentDAL.getMessageByProduct(sendPaymentLinkReq.product);
                            foreach (string sendPaymentLinkTo in sendPaymentLinkActions)
                            {
                                switch (sendPaymentLinkTo)
                                {
                                    case "W":
                                        WhatsAppDynamicParams whatsAppDynamicParams = new WhatsAppDynamicParams()
                                        {
                                            customerName = getPaymentLinkDetails[0].name,
                                            amount = "Rs. " + getPaymentLinkDetails[0].total_amount,
                                            paymentLink = getPaymentLinkDetails[0].send_link_landing_url,
                                            proposalList = string.Join(", ", getPaymentLinkDetails.Select(x => x.proposal_number)),
                                            policyStartDate = getPaymentLinkDetails[0].proposal_start_date,
                                            policyEndDate = getPaymentLinkDetails.Any() ? getPaymentLinkDetails.Where(x => !string.IsNullOrEmpty(x.proposal_end_date)).Max(x => DateTime.Parse(x.proposal_end_date)).ToString("yyyy-MM-dd") : string.Empty,
                                            productName = getMessageByProduct.whatsapp_text
                                        };
                                        bool wsMessageSent = await sendMessageService.SendWhatsApp(traceID, sendPaymentLinkReq.whatsappMobileNumber, whatsAppDynamicParams, flow_type);
                                        if (wsMessageSent)
                                        {
                                            sendPaymentLinkResp.WhatsAppSent = true;
                                        }

                                        break;

                                    case "S":
                                        bool messageSent = await sendMessageService.SendSMS(traceID, sendPaymentLinkReq.mobileNumber, string.Format(getMessageByProduct.sms_text, [getPaymentLinkDetails[0].total_amount, getMessageByProduct.whatsapp_text, getPaymentLinkDetails[0].send_link_landing_url]), flow_type);
                                        if (messageSent)
                                        {
                                            sendPaymentLinkResp.SmsSent = true;
                                        }

                                        break;

                                    case "E":
                                        string emailContent = getMessageByProduct.mail_text;

                                        emailContent = emailContent
                                            .Replace("&lt;&lt;PRODUCTNAME&gt;&gt;", getMessageByProduct.whatsapp_text ?? string.Empty)
                                            .Replace("&lt;&lt;PROPOSALNO &gt;&gt;", string.Join(", ",
                                                getPaymentLinkDetails.Select(x => x.proposal_number)))
                                            .Replace("&lt;&lt;POLICYSTARTDATE&gt;&gt;", getPaymentLinkDetails[0].proposal_start_date ?? string.Empty)
                                            .Replace("&lt;&lt;POLICYENDDATE&gt;&gt;",
                                                getPaymentLinkDetails.Any()
                                                    ? getPaymentLinkDetails
                                                        .Where(x => !string.IsNullOrEmpty(x.proposal_end_date))
                                                        .Max(x => DateTime.Parse(x.proposal_end_date))
                                                        .ToString("yyyy-MM-dd")
                                                    : string.Empty)
                                            .Replace("&lt;&lt;TOTALPREMIUM&gt;&gt;", "Rs. " + getPaymentLinkDetails[0].total_amount)
                                            .Replace("&lt;&lt;REVIEWLINK&gt;&gt;", getPaymentLinkDetails[0].send_link_landing_url ?? string.Empty);

                                        bool emailSent = sendMessageService.SendMail(emailContent, !string.IsNullOrEmpty(sendPaymentLinkReq.alternateEmailId) ? sendPaymentLinkReq.emailId + "," + sendPaymentLinkReq.alternateEmailId : sendPaymentLinkReq.emailId, $"Complete your {getMessageByProduct.whatsapp_text ?? string.Empty} proposal at icicilombard.com");
                                        if (emailSent)
                                        {
                                            sendPaymentLinkResp.EmailSent = true;
                                        }

                                        break;

                                    default:

                                        break;
                                }
                            }
                            sendPaymentLinkResp.TransactionID = transactionID;

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = null;
                            response.TraceId = traceID;
                            response.Data = sendPaymentLinkResp;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PaymentBAL.cs - SendPaymentLink() ",
                           Message: "Unable to fetch payment link details for this ID. Decrypted ID: " + decID,
                           TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch payment link details for this ID";
                            response.TraceId = traceID;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - SendPaymentLink() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
                            TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = traceID;
                        response.Data = null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - SendPaymentLink() ",
                        Message: "Invalid ID format. ID: " + sendPaymentLinkReq.ID,
                        TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - SendPaymentLink() ",
                    TraceId: "",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<FetchProposalDetailsResp> FetchProposalDetails(string trace_id, string proposal_number, string proposal_amount, string proposal_id, string proposal_type, string core, string flow_type, string website_correlation_id = "")
        {
            PaymentDAL paymentDAL = new PaymentDAL();

            FetchProposalDetailsReq fetchProposalDetailsReq = new FetchProposalDetailsReq();
            FetchProposalDetailsResp fetchProposalDetailsResp = new FetchProposalDetailsResp();
            ReverseSyncToken reverseSyncToken = new ReverseSyncToken();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string token = await reverseSyncToken.GetWebAuthorizationToken(trace_id, flow_type);

                fetchProposalDetailsReq = new FetchProposalDetailsReq
                {
                    ProposalNumber = proposal_number,
                    ProductType = proposal_type,
                    IssuanceSystem = core
                };

                string URL = ApplicationConfig.FETCH_PROPOSAL_DETAILS_API_URL;
                var client = new RestClient();
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Authorization", "Bearer " + token);
                if (!string.IsNullOrEmpty(website_correlation_id))
                {
                    request.AddHeader("CorelationId", website_correlation_id);
                }
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(fetchProposalDetailsReq), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(fetchProposalDetailsReq), response.Content, URL, "ECHANNEL COMMON FETCH PROPOSAL DETAILS", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    fetchProposalDetailsResp = JsonConvert.DeserializeObject<FetchProposalDetailsResp>(response.Content);

                    fetchProposalDetailsResp.proposalNumber = proposal_number;
                    fetchProposalDetailsResp.proposalAmount = proposal_amount;
                    fetchProposalDetailsResp.proposalId = proposal_id;
                    fetchProposalDetailsResp.proposalType = proposal_type;
                    fetchProposalDetailsResp.core = core;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "PaymentBAL.cs - FetchProposalDetails() ",
                   TraceId: trace_id,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
            }

            return fetchProposalDetailsResp;
        }

        public bool ValidateProductProposalType(string traceId, string product, List<ProposalListv2> proposalList)
        {
            try
            {
                if (string.IsNullOrEmpty(product))
                {
                    return false;
                }

                KeyValuePair<string, string> productConfig = ApplicationConfig.PRODUCTS_LIST
                    .FirstOrDefault(x => string.Equals(x.Key, product, StringComparison.OrdinalIgnoreCase));

                string allowedProposalTypesValue = productConfig.Value;
                if (string.IsNullOrEmpty(allowedProposalTypesValue))
                {
                    return false;
                }

                HashSet<string> allowedProposalTypes = new HashSet<string>(
                    allowedProposalTypesValue.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);

                if (allowedProposalTypes.Count == 0)
                {
                    return false;
                }

                if (proposalList == null || proposalList.Count == 0)
                {
                    return false;
                }

                HashSet<string> requestProposalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ProposalListv2 proposal in proposalList)
                {
                    if (string.IsNullOrEmpty(proposal.ProposalType))
                    {
                        return false;
                    }

                    requestProposalTypes.Add(proposal.ProposalType.Trim());
                }

                foreach (string proposalType in requestProposalTypes)
                {
                    if (!allowedProposalTypes.Contains(proposalType))
                    {
                        return false;
                    }
                }

                foreach (string allowedProposalType in allowedProposalTypes)
                {
                    if (!requestProposalTypes.Contains(allowedProposalType))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - ValidateProductProposalType() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                return false;
            }
        }

        public async Task<CheckProposalV2Resp> checkproposalv2(AddPaymentReqv2 addPaymentReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            OrderBAL orderBAL = new OrderBAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            CheckProposalV2Resp result = new CheckProposalV2Resp();

            try
            {
                // 1) Basic request validation
                if (addPaymentReq == null || addPaymentReq.proposalList == null || addPaymentReq.proposalList.Count == 0)
                {
                    return result;
                }

                var proposalNumbers = addPaymentReq.proposalList
                    .Where(x => x != null && !string.IsNullOrEmpty(x.ProposalNumber))
                    .Select(x => x.ProposalNumber)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (proposalNumbers.Count == 0)
                {
                    return result;
                }

                // 2) Fetch historical txn/order rows for given proposal list
                var rows = await paymentDAL.checkproposalv2(addPaymentReq);
                if (rows == null || rows.Count == 0)
                {
                    return result;
                }

                // 3) Build active transaction map per proposal (active = SUCCESS/PENDING)
                var activeTxnByProposal = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (string proposal in proposalNumbers)
                {
                    activeTxnByProposal[proposal] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var proposalRows = rows
                        .Where(x => x != null && string.Equals(x.proposal_number, proposal, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var row in proposalRows)
                    {
                        string currentStatus = string.Empty;

                        // 4) Realtime order status fetch via unified GetOrderStatus (handles PG-specific logic internally)
                        if (!string.IsNullOrEmpty(row.dps_order_id) &&
                            !string.IsNullOrEmpty(row.trace_id) &&
                            !string.IsNullOrEmpty(row.application_name))
                        {
                            try
                            {
                                DPSStatusResponse orderResp = await orderBAL.GetOrderStatus(new FetchOrderStatusReq
                                {
                                    dps_order_id = row.dps_order_id,
                                    trace_id = row.trace_id
                                }, row.application_name, flow_type);

                                currentStatus = (orderResp?.Data as FetchOrderStatusResp)?.payment_status;
                            }
                            catch (Exception ex)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - checkproposalv2() - GetOrderStatus ",
                                    TraceId: row.trace_id,
                                    Message: $"Exception occurred: {ex.Message}",
                                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                                result.is_error = true;
                                result.trace_id = row.trace_id;
                                result.error_message = "Error while fetching order status for proposal: " + proposal;
                                return result;
                            }
                        }

                        currentStatus = (currentStatus ?? string.Empty).Trim().ToUpper();

                        // 5) Status classification
                        if (currentStatus == "SUCCESS" || currentStatus == "PENDING")
                        {
                            if (!string.IsNullOrEmpty(row.transaction_id))
                            {
                                activeTxnByProposal[proposal].Add(row.transaction_id);
                            }
                        }
                        else if (currentStatus == string.Empty || currentStatus == "NEW" || currentStatus == "FAILED")
                        {
                            // allowed non-active statuses
                        }
                        else
                        {
                            // unknown status => explicit business error
                            result.is_error = true;
                            result.trace_id = row.trace_id;
                            result.error_message = "Unknown status of the order for proposal: " + proposal;
                            return result;
                        }
                    }
                }

                // 6) If no active txn found for any proposal, allow new flow
                bool hasAnyActive = activeTxnByProposal.Any(x => x.Value.Count > 0);
                if (!hasAnyActive)
                {
                    return result;
                }

                // 7) Find common active transaction across all proposals
                HashSet<string> commonTxnIds = null;
                foreach (string proposal in proposalNumbers)
                {
                    if (commonTxnIds == null)
                    {
                        commonTxnIds = new HashSet<string>(activeTxnByProposal[proposal], StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        commonTxnIds.IntersectWith(activeTxnByProposal[proposal]);
                    }
                }

                // 8) If common active txn exists, return existing payment URL
                if (commonTxnIds != null && commonTxnIds.Count > 0)
                {
                    string selectedTxnId = commonTxnIds.First();

                    var selectedRows = rows
                        .Where(x => x != null && string.Equals(x.transaction_id, selectedTxnId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (selectedRows.Count == 0)
                    {
                        return result;
                    }

                    var first = selectedRows.FirstOrDefault(x => !string.IsNullOrEmpty(x.payment_url)) ?? selectedRows.First();

                    string encryptedId = encryptDecrypt.EncryptRequest($"{first.transaction_id}|{first.trace_id}");
                    string finalUrl = $"{ApplicationConfig.DPS_PROXY_URL}{encryptedId}";
                    if (!string.IsNullOrEmpty(first.send_payment_link) && first.send_payment_link.ToUpper() == "TRUE")
                    {
                        finalUrl = $"{ApplicationConfig.SEND_PAYMENT_LINK_BASE_URL}?id={encryptedId}";
                    }

                    result.addPaymentResp = new AddPaymentRespv2
                    {
                        TransactionID = first.transaction_id,
                        URL = finalUrl,
                        pg_name = first.pg_name
                    };
                    result.trace_id = first.trace_id;
                    return result;
                }

                // 9) Mixed combination (active for some but not all on same txn) => block
                result.is_error = true;
                result.error_message = "These combination of proposals are not allowed as payment is already in progress/done for few proposals.";
                return result;
            }
            catch (Exception ex)
            {
                // 10) Outer safety net to avoid unhandled 500
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - checkproposalv2() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                result.is_error = true;
                result.error_message = "Error while checking existing proposal transactions.";
                return result;
            }
        }

        //public async Task<FetchProposalDetailsResp> FetchProposalDetailsFWEXW(string trace_id, string proposal_number, string proposal_amount, string proposal_id, string proposal_type, string core, string flow_type)
        //{
        //    PaymentDAL paymentDAL = new PaymentDAL();

        //    FetchProposalDetailsReq fetchProposalDetailsReq = new FetchProposalDetailsReq();
        //    FetchProposalDetailsResp fetchProposalDetailsResp = new FetchProposalDetailsResp();
        //    ReverseSyncToken reverseSyncToken = new ReverseSyncToken();

        //    Stopwatch stopwatch = new Stopwatch();

        //    try
        //    {
        //        string token = await reverseSyncToken.GetWebAuthorizationToken(trace_id, flow_type);

        //        string URL = ApplicationConfig.FETCH_PROPOSAL_DETAILS_FWEXW_API_URL + proposal_number;
        //        var client = new RestClient();
        //        var request = new RestRequest(URL, Method.Get);
        //        request.AddHeader("Authorization", "Bearer " + token);
        //        request.AddHeader("Content-Type", "application/json");
        //        request.AddHeader("App-Platform", "WEB");

        //        stopwatch = Stopwatch.StartNew();

        //        RestResponse response = await client.ExecuteAsync(request);

        //        stopwatch.Stop();
        //        var responseTime = stopwatch.Elapsed.TotalSeconds;

        //        await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(fetchProposalDetailsReq), response.Content, URL, "ECHANNEL COMMON FETCH PROPOSAL DETAILS", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

        //        if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
        //        {
        //            var apiResponse = JsonConvert.DeserializeObject<ProposalFWEXWApiResponse>(response.Content);

        //            fetchProposalDetailsResp = new FetchProposalDetailsResp
        //            {
        //                Success = true,
        //                StatusCode = 200,

        //                // Basic info
        //                DealId = apiResponse.DealId,
        //                CustomerId = apiResponse.CustomerId.ToString(),
        //                CustomerName = apiResponse.CustomerName,
        //                CustomerEmail = apiResponse.CustomerEmail,
        //                CustomerMobile = apiResponse.MobileNumber,

        //                // Proposal details
        //                ProposalStatus = apiResponse.ProposalStatus,
        //                ProposalStartDate = apiResponse.ProposalStartDate,
        //                ProposalEndDate = apiResponse.ProposalEndDate,
        //                ProposalTenure = apiResponse.ProposalTenure ?? 0,

        //                // Handle nulls safely
        //                SumInsured = apiResponse.SumInsured ?? 0,

        //                // Nested object
        //                PremiumDetails = new ProposalPremiumDetails
        //                {
        //                    BasicPremium = apiResponse.BasicPremium ?? 0,
        //                    StampDuty = apiResponse.StampDuty ?? 0,
        //                    GST = apiResponse.GST ?? 0,
        //                    TotalPremium = apiResponse.TotalPremium ?? 0
        //                },

        //                // Optional fields
        //                PolicyNumber = null,
        //                ProposalSubStatus = null,
        //                IntermediaryCode = null,
        //                ProductCode = null,
        //                IsNSTPFlow = false,
        //                IsNSTPFlowStr = null,
        //                BusinessVertical = null,
        //                SubVertical = null,
        //                MarketingOfficer = null,

        //                //Breakin fields
        //                VehicleType = apiResponse.TypeVehicle,
        //                VehicleMake = apiResponse.VehicleMake,
        //                vehicleModel = apiResponse.VehicleModel,
        //                RegistrationNumber = apiResponse.RegistrationNo,
        //                EngineNumber = apiResponse.EngineNo,
        //                ChassisNumber = apiResponse.ChassisNo,
        //                YearOfManufacture = apiResponse.ManufactureYear,
        //                BreakInDays = apiResponse.BreakInDays ?? Convert.ToInt32(ApplicationConfig.BREAKIN_DAYS),
        //                SalesRemarks = apiResponse.SalesRemarks,
        //                BreakInType = apiResponse.BreakInType,
        //                IsPOSDealId = apiResponse.IsPOSDealId
        //            };

        //            fetchProposalDetailsResp.proposalNumber = proposal_number;
        //            fetchProposalDetailsResp.proposalAmount = proposal_amount;
        //            fetchProposalDetailsResp.proposalId = proposal_id;
        //            fetchProposalDetailsResp.proposalType = proposal_type;
        //            fetchProposalDetailsResp.core = core;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        stopwatch.Stop();

        //        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
        //           MethodName: "PaymentBAL.cs - FetchProposalDetails() ",
        //           TraceId: trace_id,
        //           Message: $"Exception occurred: {ex.Message}",
        //           StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
        //    }

        //    return fetchProposalDetailsResp;
        //}

        public async Task<DPSStatusResponse> GetPaymentLink(FetchPaymentReq fetchPaymentReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            GetPaymentDetails getPaymentDetails = new GetPaymentDetails();
            GetPaymentLinkResp getPaymentLinkResp = new GetPaymentLinkResp();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            string decID = string.Empty;
            string traceID = string.Empty;
            string transactionID = string.Empty;
            string expiry_time = string.Empty;
            string access_token = string.Empty;
            DateTime? transactionTime = null;

            try
            {
                decID = encryptDecrypt.DecryptResponse(fetchPaymentReq.ID);
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        getPaymentDetails = await paymentDAL.GetPaymentLink(transactionID, traceID);

                        if (getPaymentDetails != null && !string.IsNullOrEmpty(getPaymentDetails.payment_url))
                        {
                            transactionTime = getPaymentDetails.payment_done_at ?? getPaymentDetails.txn_created_on;

                            if (transactionTime.HasValue)
                            {
                                if (!(DateTime.Now >= transactionTime.Value.AddHours(12)))
                                {
                                    DPSStatusResponse authTokenResponse = await authenticateBAL.CreateAuthTokenv2(getPaymentDetails.application_name, getPaymentDetails.application_id);
                                    if (authTokenResponse.StatusMessage.ToUpper() == "SUCCESS")
                                    {
                                        string json = JsonConvert.SerializeObject(authTokenResponse.Data);
                                        if (json != null)
                                        {
                                            using var doc = JsonDocument.Parse(json);
                                            if (doc.RootElement.TryGetProperty("expiryTime", out var expiryTime))
                                            {
                                                expiry_time = expiryTime.GetString();
                                            }

                                            if (doc.RootElement.TryGetProperty("accessToken", out var accessToken))
                                            {
                                                access_token = accessToken.GetString();
                                            }
                                        }

                                        getPaymentLinkResp = new GetPaymentLinkResp
                                        {
                                            payment_url = getPaymentDetails.payment_url,
                                            pg_name = getPaymentDetails.pg_name,
                                            authTokenData = new AuthenticateModel.AuthenticateToken
                                            {
                                                expiryTime = expiry_time,
                                                accessToken = access_token
                                            }
                                        };

                                        await paymentDAL.updateIsFirstTxnVisit(traceID, transactionID, expiry_time, access_token);

                                        response.StatusCode = 1;
                                        response.StatusMessage = "Success";
                                        response.Message = null;
                                        response.TraceId = traceID;
                                        response.Data = getPaymentLinkResp;
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                                            TraceId: traceID,
                                            Message: "Failed to create auth token. Decrypted ID: " + decID), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Failed to create auth token";
                                        response.TraceId = traceID;
                                        response.Data = null;
                                    }
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                                    TraceId: traceID,
                                    Message: "Payment Session Expired. Txn initiated > 12hrs for Decrypted ID: " + decID), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 401;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Payment Session Expired";
                                    response.TraceId = traceID;
                                    response.Data = getPaymentLinkResp;
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                                TraceId: traceID,
                                Message: "No transaction details found. Decrypted ID: " + decID), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Invalid Transaction";
                                response.TraceId = traceID;
                                response.Data = null;
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                                TraceId: traceID,
                                Message: "Unable to fetch payment link details for this ID. Decrypted ID: " + decID), ApplicationConfig.PAYMENT_LOGS);
                            response.Data = null;
                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch payment link details for this ID";
                            response.TraceId = traceID;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                        TraceId: traceID,
                        Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID), ApplicationConfig.PAYMENT_LOGS);

                        response.Data = null;
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format -> Decrypted ID contains empty Transaction ID or Trace ID";
                        response.TraceId = traceID;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                               Message: "Invalid ID format. ID: " + fetchPaymentReq.ID,
                               TraceId: traceID), ApplicationConfig.PAYMENT_LOGS);

                    response.Data = null;
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = traceID;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - GetPaymentLink() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.Data = null;
                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = traceID;
            }

            return response;
        }

        public async Task<DPSStatusResponse> TagProposalv2(TagProposalReqv2 tagProposalReq, string username, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            OrderBAL orderBAL = new OrderBAL();
            CallbackBAL callbackBAL = new CallbackBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();
            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();

            string order_status = string.Empty;

            HashSet<string> uniqueIsNSTPFlagList = new HashSet<string>();

            HashSet<string> pfCustomerIdSet = new HashSet<string>();
            HashSet<string> artemisCustomerIdSet = new HashSet<string>();

            FetchProposalDetailsResp fetchProposalDetailsResp = new FetchProposalDetailsResp();

            // PF 
            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            PFPolicyCreationResp pfPolicyCreationResp = new PFPolicyCreationResp();
            ILESBInterface ilesbInterface = new ILESBInterface();

            string pfPaymentId = string.Empty;
            string pfPolicyNumber = string.Empty;

            // Artemis
            ArtemisPolicyCreationReq artemisPolicyCreationReq = new ArtemisPolicyCreationReq();
            ArtemisPolicyCreationResp artemisPolicyCreationResp = new ArtemisPolicyCreationResp();
            ArtemisPolicyCreationRespTimeout artemisPolicyCreationRespTimeout = new ArtemisPolicyCreationRespTimeout();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();

            string receiptNumber = string.Empty;
            string artemisPolicyNumber = string.Empty;

            TagProposalResp tagProposalResp = new TagProposalResp();

            List<SyncProposalToSource> syncProposalToSourceList = new List<SyncProposalToSource>();

            try
            {
                fetchPaymentDetails = await paymentDAL.FetchAdvancePaymentDetailsv2(tagProposalReq.TransactionID);
                if (fetchPaymentDetails == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "PaymentBAL.cs - TagProposalv2() ",
                      TraceId: fetchPaymentDetails.trace_id,
                      Message: "Cannot find details from the provided Transaction ID. TagProposal Request: " + tagProposalReq), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Cannot find details from the provided Transaction ID";
                    response.TraceId = fetchPaymentDetails.trace_id;
                    response.Data = null;
                }
                else
                {
                    await paymentDAL.LogIntegrationApi(fetchPaymentDetails.trace_id, "", JsonConvert.SerializeObject(tagProposalReq), "", "", "TAG PROPOSAL V2", "", flow_type, "");

                    if (string.IsNullOrEmpty(tagProposalReq.ProposalList.ProposalNumber) || string.IsNullOrEmpty(tagProposalReq.ProposalList.ProposalAmount))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                          MethodName: "PaymentBAL.cs - TagProposalv2() ",
                          Message: "Proposal Number or Proposal Amount cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                          TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Proposal Number or Proposal Amount cannot be empty in the Proposal List";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (string.IsNullOrEmpty(tagProposalReq.ProposalList.ProposalType))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposalv2() ",
                            Message: "Proposal Type cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                            TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Proposal Type cannot be empty in the Proposal List";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (string.IsNullOrEmpty(tagProposalReq.ProposalList.core))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposalv2() ",
                            Message: "Core cannot be empty in the Proposal List. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                            TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Core cannot be empty in the Proposal List";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (Convert.ToDecimal(fetchPaymentDetails.total_amount) != Convert.ToDecimal(tagProposalReq.ProposalList.ProposalAmount))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposalv2() ",
                            Message: "The Total Amount does not match with the Proposal Amount Sum. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                            TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "The Total Amount does not match with the Proposal Amount Sum.";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    fetchOrderStatusReq = new FetchOrderStatusReq
                    {
                        dps_order_id = fetchPaymentDetails.dps_order_id,
                        trace_id = fetchPaymentDetails.trace_id
                    };

                    var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, username, flow_type);

                    var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                    if (orderData != null)
                    {
                        order_status = orderData.payment_status;
                    }

                    if (string.IsNullOrEmpty(order_status) || (!string.IsNullOrEmpty(order_status) && order_status.ToUpper() != "SUCCESS"))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PaymentBAL.cs - TagProposalv2() ",
                           TraceId: fetchPaymentDetails.trace_id,
                           Message: "Transaction not successful (Status is not SUCCESS or Status is empty). Tag proposal Request: " + JsonConvert.SerializeObject(tagProposalReq)), ApplicationConfig.RETRY_RESYNC_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unknown Order Status";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    fetchProposalDetailsResp = await FetchProposalDetails(fetchPaymentDetails.trace_id, tagProposalReq.ProposalList.ProposalNumber, tagProposalReq.ProposalList.ProposalAmount, tagProposalReq.ProposalList.ProposalId, tagProposalReq.ProposalList.ProposalType, tagProposalReq.ProposalList.core, flow_type);
                    if (fetchProposalDetailsResp != null && fetchProposalDetailsResp.Success)
                    {
                        if (string.IsNullOrEmpty(fetchProposalDetailsResp.proposalNumber) || string.IsNullOrEmpty(fetchProposalDetailsResp.proposalAmount))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                Message: "Proposal Number or Proposal Amount cannot be empty in the Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Proposal Number or Proposal Amount cannot be empty in the Proposal List";
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                            return response;
                        }

                        //if (Math.Abs(Convert.ToDouble(fetchProposalDetailsResp.proposalAmount) - Convert.ToDouble(fetchProposalDetailsResp.PremiumDetails.TotalPremium)) > 5)
                        //{
                        //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        //        MethodName: "PaymentBAL.cs - TagProposalv2() ",
                        //        Message: "The Total Amount does not match with the Proposal Amount Sum for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                        //        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        //    response.StatusCode = 0;
                        //    response.StatusMessage = "Failed";
                        //    response.Message = "The Total Amount does not match with the Proposal Amount Sum for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                        //    response.TraceId = fetchPaymentDetails.trace_id;
                        //    response.Data = null;
                        //    return response;
                        //}

                        if (fetchPaymentDetails.payer_type.ToUpper() == "C" && string.IsNullOrEmpty(fetchProposalDetailsResp.CustomerDetails?.CustomerId))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                Message: "Customer ID cannot be empty for payertype 'C' for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Customer ID cannot be empty for payertype 'C' for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                            return response;
                        }

                        if (fetchPaymentDetails.payer_type.ToUpper() == "I" && string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                Message: "Intermediary Code cannot be empty for payertype 'I' for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Intermediary Code cannot be empty for payertype 'I' for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                            return response;
                        }

                        if (fetchProposalDetailsResp.core.ToUpper() == "PF")
                        {
                            pfCustomerIdSet.Add(fetchProposalDetailsResp.CustomerDetails?.CustomerId);
                            if (pfCustomerIdSet.Count > 1)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "Customer ID cannot be different for the PF proposal numbers. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Customer ID cannot be different for the PF proposal numbers";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (string.IsNullOrEmpty(fetchProposalDetailsResp.DealId) && string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "Either Deal ID or Intermediary Code is required in PF in the Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Either Deal ID or Intermediary Code is required in PF in the Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (!string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalStatus))
                            {
                                if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(fetchProposalDetailsResp.ProposalStatus.ToUpper()))
                                {
                                    fetchProposalDetailsResp.IsNSTPFlowStr = "N";
                                }
                                else
                                {
                                    fetchProposalDetailsResp.IsNSTPFlowStr = "Y";
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "PF Proposal Status Fetching Failed. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Could not fetch PF Proposal Status.";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }
                        }
                        else if (fetchProposalDetailsResp.core.ToUpper() == "ARTEMIS")
                        {
                            artemisCustomerIdSet.Add(fetchProposalDetailsResp.CustomerDetails?.CustomerId);
                            if (artemisCustomerIdSet.Count > 1)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "Customer ID cannot be different for the Artemis proposal numbers. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Customer ID cannot be different for the Artemis proposal numbers";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "Intermediary Code is required in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Intermediary Code is required in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (!string.IsNullOrEmpty(fetchProposalDetailsResp.BusinessVertical) && !string.IsNullOrEmpty(fetchProposalDetailsResp.SubVertical) && !string.IsNullOrEmpty(fetchProposalDetailsResp.MarketingOfficer))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "Business Vertical / Sub Vertical / Marketing Officer are required in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Business Vertical / Sub Vertical / Marketing Officer are required in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (!string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalStatus) && !string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalSubStatus))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "Proposal Status and Proposal Sub Status are required in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal Status and Proposal Sub Status are required in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (fetchProposalDetailsResp.IsNSTPFlow == null)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    Message: "IsNSTPFlow is reqiured in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "IsNSTPFlow is reqiured in Artemis in Proposal List for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }
                            fetchProposalDetailsResp.IsNSTPFlowStr = fetchProposalDetailsResp.IsNSTPFlow == true ? "Y" : "N";
                        }

                        fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number = fetchProposalDetailsResp.proposalNumber;
                        fetchPaymentDetails.fetchPaymentProposalList[0].proposal_status = string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalSubStatus) ? fetchProposalDetailsResp.ProposalStatus : fetchProposalDetailsResp.ProposalSubStatus;
                        fetchPaymentDetails.fetchPaymentProposalList[0].deal_id = fetchProposalDetailsResp.DealId;
                        fetchPaymentDetails.fetchPaymentProposalList[0].customer_id = fetchProposalDetailsResp.CustomerDetails.CustomerId;
                        fetchPaymentDetails.fetchPaymentProposalList[0].intermediary_code = fetchProposalDetailsResp.IntermediaryCode;
                        fetchPaymentDetails.fetchPaymentProposalList[0].is_nstp_flow = fetchProposalDetailsResp.IsNSTPFlowStr;
                        fetchPaymentDetails.fetchPaymentProposalList[0].business_vertical = fetchProposalDetailsResp.BusinessVertical;
                        fetchPaymentDetails.fetchPaymentProposalList[0].sub_vertical = fetchProposalDetailsResp.SubVertical;
                        fetchPaymentDetails.fetchPaymentProposalList[0].marketing_officer = fetchProposalDetailsResp.MarketingOfficer;
                        fetchPaymentDetails.fetchPaymentProposalList[0].proposer_code = fetchPaymentDetails.payer_type == "C" ? fetchProposalDetailsResp.CustomerDetails.CustomerId : fetchProposalDetailsResp.IntermediaryCode;
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposalv2() ",
                            Message: "Could not fetch Proposal Details for proposal number: " + tagProposalReq.ProposalList.ProposalNumber + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                            TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Could not fetch Proposal Details for proposal number: " + tagProposalReq.ProposalList.ProposalNumber;
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    await paymentDAL.UpdateAdvanceDBTransactionv2(tagProposalReq, fetchPaymentDetails, fetchProposalDetailsResp);

                    foreach (FetchPaymentProposalList fetchPaymentProposal in fetchPaymentDetails.fetchPaymentProposalList)
                    {
                        uniqueIsNSTPFlagList.Add(fetchPaymentProposal.is_nstp_flow);
                    }

                    if (tagProposalReq.ProposalList.core.ToUpper() == "PF")
                    {
                        // PF
                        // Create Payment Entry and Tagging Request Body (Check if any of the proposal statuses are NCCN or RCCN. If Yes, then do both Entry and Tagging, otherwise only Entry)
                        if (uniqueIsNSTPFlagList.Contains("N"))
                        {
                            pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(true, true, fetchPaymentDetails);
                        }
                        else
                        {
                            pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(true, false, fetchPaymentDetails);
                        }

                        if (pfPolicyCreationReq == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                TraceId: fetchPaymentDetails.trace_id,
                                Message: "Error while creating request body for PF Policy Creation. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                        }
                        else
                        {
                            // ILESB Payment Entry and Tagging API Call
                            pfPolicyCreationResp = await ilesbInterface.GeneratePFPolicyData(pfPolicyCreationReq, flow_type);

                            if (pfPolicyCreationResp != null && pfPolicyCreationResp.StatusMessage.ToUpper() == "SUCCESS")
                            {
                                if (pfPolicyCreationResp.paymentEntryResponse != null && pfPolicyCreationResp.paymentEntryResponse.Status.ToUpper() == "SUCCESS")
                                {
                                    pfPaymentId = pfPolicyCreationResp.paymentEntryResponse.PaymentID;
                                    await callbackDAL.UpdateCorePaymentID(pfPaymentId, fetchPaymentDetails.transaction_id);

                                    if (pfPolicyCreationResp.paymentTagResponse != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList.Count > 0 && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].Status.ToUpper() == "SUCCESS" && !string.IsNullOrEmpty(pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo) && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo != "0")
                                    {
                                        pfPolicyNumber = pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].PolicyNo;

                                        SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                        {
                                            policy_number = pfPolicyNumber,
                                            payment_id = pfPaymentId,
                                            proposal_number = pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList[0].ProposalNo,
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

                                        tagProposalResp.PolicyNumber = pfPolicyNumber;
                                        response.StatusCode = 1;
                                        response.StatusMessage = "Success";
                                        response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                                        response.TraceId = fetchPaymentDetails.trace_id;
                                        response.Data = tagProposalResp;
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                            TraceId: fetchPaymentDetails.trace_id,
                                            Message: "Policy Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq) + " GeneratePFPolicyData Response Body: " + JsonConvert.SerializeObject(pfPolicyCreationResp)), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                        response.TraceId = fetchPaymentDetails.trace_id;
                                        response.Data = null;
                                    }

                                    await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, pfPolicyNumber);
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                        TraceId: fetchPaymentDetails.trace_id,
                                        Message: "Payment ID Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq) + " GeneratePFPolicyData Response Body: " + JsonConvert.SerializeObject(pfPolicyCreationResp)), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                   TraceId: fetchPaymentDetails.trace_id,
                                   Message: "Error while calling Policy Creation API. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                            }
                        }
                    }
                    else if (tagProposalReq.ProposalList.core.ToUpper() == "ARTEMIS")
                    {
                        // ARTEMIS
                        // Create Payment Entry and Tagging Request Body (Check if any of the proposal statuses are NCCN or RCCN. If Yes, then do both Entry and Tagging, otherwise only Entry)
                        if (uniqueIsNSTPFlagList.Contains("N"))
                        {
                            artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                        }
                        else
                        {
                            artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                        }

                        if (artemisPolicyCreationReq == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                TraceId: fetchPaymentDetails.trace_id,
                                Message: "Error while creating request body for Artemis Policy Creation. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                        }
                        else
                        {
                            // API FACTORY Payment Entry and Tagging API Call
                            RestResponse apiResponse = await apiFactoryInterface.GenerateArtemisPolicyData(fetchPaymentDetails.trace_id, artemisPolicyCreationReq, flow_type);

                            if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                            {
                                artemisPolicyCreationResp = JsonConvert.DeserializeObject<ArtemisPolicyCreationResp>(apiResponse.Content);
                            }
                            else if (apiResponse.StatusCode == HttpStatusCode.GatewayTimeout && !string.IsNullOrEmpty(apiResponse.Content))
                            {
                                artemisPolicyCreationRespTimeout = JsonConvert.DeserializeObject<ArtemisPolicyCreationRespTimeout>(apiResponse.Content);
                            }

                            if (artemisPolicyCreationResp != null && artemisPolicyCreationResp.data != null && artemisPolicyCreationResp.statusCode == 200)
                            {
                                if (artemisPolicyCreationResp.data.receiptStatus.ToUpper() == "SUCCESSFUL")
                                {
                                    receiptNumber = artemisPolicyCreationResp.data.receiptNumber;
                                    await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);

                                    if (artemisPolicyCreationResp.data.tagging != null && artemisPolicyCreationResp.data.tagging.Count > 0 && !string.IsNullOrEmpty(artemisPolicyCreationResp.data.tagging[0].policyNumber))
                                    {
                                        artemisPolicyNumber = artemisPolicyCreationResp.data.tagging[0].policyNumber;

                                        SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                        {
                                            policy_number = artemisPolicyNumber,
                                            payment_id = receiptNumber,
                                            proposal_number = artemisPolicyCreationResp.data.tagging[0].proposalNumber,
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

                                        tagProposalResp.PolicyNumber = artemisPolicyNumber;
                                        response.StatusCode = 1;
                                        response.StatusMessage = "Success";
                                        response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                                        response.TraceId = fetchPaymentDetails.trace_id;
                                        response.Data = tagProposalResp;
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                            TraceId: fetchPaymentDetails.trace_id,
                                            Message: "Policy Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationResp), ApplicationConfig.PAYMENT_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                        response.TraceId = fetchPaymentDetails.trace_id;
                                        response.Data = null;
                                    }

                                    await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, artemisPolicyNumber);
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                        TraceId: fetchPaymentDetails.trace_id,
                                        Message: "Receipt Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationResp), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                   TraceId: fetchPaymentDetails.trace_id,
                                   Message: "Error while calling Artemis Policy Creation API. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                            }

                            if (artemisPolicyCreationRespTimeout != null && artemisPolicyCreationRespTimeout.data != null && artemisPolicyCreationRespTimeout.statusCode == 400)
                            {
                                if (artemisPolicyCreationRespTimeout.data.taggingDetails != null && artemisPolicyCreationRespTimeout.data.taggingDetails.Count > 0 && !string.IsNullOrEmpty(artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedPolicyNumber) && !string.IsNullOrEmpty(artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedReceiptNumber))
                                {
                                    receiptNumber = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedReceiptNumber;
                                    artemisPolicyNumber = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedPolicyNumber;

                                    SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                    {
                                        policy_number = artemisPolicyNumber,
                                        payment_id = receiptNumber,
                                        proposal_number = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedProposalNumber,
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

                                    tagProposalResp.PolicyNumber = artemisPolicyNumber;
                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = tagProposalResp;
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                        TraceId: fetchPaymentDetails.trace_id,
                                        Message: "Receipt Number / Policy Number Not Generated. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationRespTimeout), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                }

                                await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);
                                await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, artemisPolicyNumber);
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                   TraceId: fetchPaymentDetails.trace_id,
                                   Message: "Error while calling Artemis Policy Creation API. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                            }
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposalv2() ",
                            TraceId: fetchPaymentDetails.trace_id,
                            Message: "Invalid Core fetched from DB. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid Core fetched from DB";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                    }

                    await callbackDAL.UpdateNoOfAttemptsCountByTransactionID(tagProposalReq.TransactionID);

                    foreach (var reverse_sync_proposal in syncProposalToSourceList)
                    {
                        await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                objLog.LogAWSMessageAsync(
                   objLog.GenericAWSException_CWL(
                       MethodName: "PaymentBAL.cs - TagProposalv2() ",
                       TraceId: fetchPaymentDetails.trace_id,
                       Message: "Request: " + JsonConvert.SerializeObject(tagProposalReq) + " Response: " + JsonConvert.SerializeObject(response),
                       StackTrace: ""
                   ),
                   ApplicationConfig.PAYMENT_LOGS
               );
            }

            return response;
        }

        public async Task<DPSStatusResponse> TagProposalv3(TagProposalReqv3 tagProposalReq, string username, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            OrderBAL orderBAL = new OrderBAL();
            CallbackBAL callbackBAL = new CallbackBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            DateFormatter dateFormatter = new DateFormatter();

            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();
            FetchPaymentProposalList fetchPaymentProposalDetails = new FetchPaymentProposalList();

            string order_status = string.Empty;

            decimal totalAmount = 0;
            decimal totalProposalAmount = 0;
            decimal inputtotalProposalAmount = 0;

            string redirectUrl = string.Empty;
            string encryptedId = string.Empty;

            HashSet<string> uniqueIsNSTPFlagList = new HashSet<string>();

            HashSet<string> pfCustomerIdSet = new HashSet<string>();
            HashSet<string> artemisCustomerIdSet = new HashSet<string>();

            FetchProposalDetailsResp fetchProposalDetailsResp = new FetchProposalDetailsResp();
            List<UpdateAdvancePaymentDetails> updateAdvancePaymentDetailsList = new List<UpdateAdvancePaymentDetails>();

            // PF 
            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            PFPolicyCreationResp pfPolicyCreationResp = new PFPolicyCreationResp();
            ILESBInterface ilesbInterface = new ILESBInterface();

            string pfPaymentId = string.Empty;
            string pfPolicyNumber = string.Empty;

            // Artemis
            ArtemisPolicyCreationReq artemisPolicyCreationReq = new ArtemisPolicyCreationReq();
            ArtemisPolicyCreationResp artemisPolicyCreationResp = new ArtemisPolicyCreationResp();
            ArtemisPolicyCreationRespTimeout artemisPolicyCreationRespTimeout = new ArtemisPolicyCreationRespTimeout();
            ArtemisPolicyTaggingOnlyReq artemisPolicyTaggingOnlyReq = new ArtemisPolicyTaggingOnlyReq();
            ArtemisPolicyTaggingOnlyResp artemisPolicyTaggingOnlyResp = new ArtemisPolicyTaggingOnlyResp();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();

            string receiptNumber = string.Empty;
            string artemisPolicyNumber = string.Empty;

            TagProposalRespv3 tagProposalResp = new TagProposalRespv3();

            List<SyncProposalToSource> syncProposalToSourceList = new List<SyncProposalToSource>();

            try
            {
                fetchPaymentDetails = await paymentDAL.FetchAdvanceTxnDetailsv3(tagProposalReq.TransactionID);
                if (fetchPaymentDetails == null || string.IsNullOrEmpty(fetchPaymentDetails.dps_order_id) || string.IsNullOrEmpty(fetchPaymentDetails.trace_id))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "PaymentBAL.cs - TagProposalv3() ",
                      TraceId: fetchPaymentDetails.trace_id,
                      Message: "Cannot find payment details from the provided Transaction ID. TagProposal Request: " + tagProposalReq), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Cannot find payment details from the provided Transaction ID";
                    response.TraceId = fetchPaymentDetails.trace_id;
                    response.Data = null;
                }
                else
                {
                    tagProposalResp.ProposalList = new List<TagProposalListv3>();

                    await paymentDAL.LogIntegrationApi(fetchPaymentDetails.trace_id, "", JsonConvert.SerializeObject(tagProposalReq), "", "", "TAG PROPOSAL V3", "", flow_type, "");

                    fetchOrderStatusReq = new FetchOrderStatusReq
                    {
                        dps_order_id = fetchPaymentDetails.dps_order_id,
                        trace_id = fetchPaymentDetails.trace_id
                    };

                    var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, username, flow_type);

                    var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                    if (orderData != null)
                    {
                        order_status = orderData.payment_status;
                    }

                    if (string.IsNullOrEmpty(order_status) || (!string.IsNullOrEmpty(order_status) && order_status.ToUpper() != "SUCCESS"))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PaymentBAL.cs - TagProposalv3() ",
                           TraceId: fetchPaymentDetails.trace_id,
                           Message: "Transaction not successful (Status is not SUCCESS or Status is empty). Tag proposal Request: " + JsonConvert.SerializeObject(tagProposalReq)), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unknown Order Status";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    totalAmount = Convert.ToDecimal(fetchPaymentDetails.total_amount);

                    if (tagProposalReq.ProposalList == null || tagProposalReq.ProposalList.Count == 0)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PaymentBAL.cs - TagProposalv3() ",
                           TraceId: fetchPaymentDetails.trace_id,
                           Message: $"ProposalList empty | txn={tagProposalReq?.TransactionID} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Proposal List cannot be empty";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (fetchPaymentDetails.fetchPaymentProposalList.Count != tagProposalReq.ProposalList.Count)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                          MethodName: "PaymentBAL.cs - TagProposalv3() ",
                          TraceId: fetchPaymentDetails.trace_id,
                          Message: $"Proposal count mismatch | txn={tagProposalReq?.TransactionID} | dbCount={fetchPaymentDetails.fetchPaymentProposalList.Count} | inputCount={tagProposalReq.ProposalList.Count} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Different proposal numbers exist against this transaction.";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    var dbRows = fetchPaymentDetails.fetchPaymentProposalList;
                    fetchPaymentDetails.fetchPaymentProposalList = new List<FetchPaymentProposalList>();

                    var hasDbProposalNumbers = dbRows.Any(x => !string.IsNullOrEmpty(x.proposal_number));

                    if (hasDbProposalNumbers)
                    {
                        var dbByKeyAndProposal = dbRows
                            .GroupBy(x =>
                                $"{(Convert.ToDecimal(x.proposal_amount).ToString("F2") ?? string.Empty).Trim()}|{(x.proposal_type ?? string.Empty).Trim().ToUpper()}|{(x.core ?? string.Empty).Trim().ToUpper()}|{(x.proposal_number ?? string.Empty).Trim().ToUpper()}")
                            .ToDictionary(
                                g => g.Key,
                                g => new Queue<FetchPaymentProposalList>(g),
                                StringComparer.OrdinalIgnoreCase
                            );

                        foreach (var proposal in tagProposalReq.ProposalList)
                        {
                            if (string.IsNullOrEmpty(proposal.ProposalNumber))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                    TraceId: fetchPaymentDetails.trace_id,
                                    Message: $"Input proposal number missing | txn={tagProposalReq?.TransactionID} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal Number cannot be empty in the Proposal List";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            var mapKey =
                                $"{(Convert.ToDecimal(proposal.ProposalAmount).ToString("F2") ?? string.Empty).Trim()}|{(proposal.ProposalType ?? string.Empty).Trim().ToUpper()}|{(proposal.core ?? string.Empty).Trim().ToUpper()}|{(proposal.ProposalNumber ?? string.Empty).Trim().ToUpper()}";

                            if (!dbByKeyAndProposal.TryGetValue(mapKey, out var queue) || queue.Count == 0)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                   TraceId: fetchPaymentDetails.trace_id,
                                   Message: $"Proposal mismatch (amount/type/core/proposalNumber not found in DB) | txn={tagProposalReq?.TransactionID} | key={mapKey} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "different proposal numbers exist against this transaction.";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            fetchPaymentDetails.fetchPaymentProposalList.Add(queue.Dequeue());
                        }

                        if (dbByKeyAndProposal.Values.Any(q => q.Count > 0))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - TagProposalv3() ",
                               TraceId: fetchPaymentDetails.trace_id,
                               Message: $"Unmatched DB rows remain after strict mapping | txn={tagProposalReq?.TransactionID} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "different proposal numbers exist against this transaction.";
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                            return response;
                        }
                    }
                    else
                    {
                        var dbByKey = dbRows
                            .GroupBy(x =>
                                $"{(Convert.ToDecimal(x.proposal_amount).ToString("F2") ?? string.Empty).Trim()}|{(x.proposal_type ?? string.Empty).Trim().ToUpper()}|{(x.core ?? string.Empty).Trim().ToUpper()}")
                            .ToDictionary(
                                g => g.Key,
                                g => new Queue<FetchPaymentProposalList>(g),
                                StringComparer.OrdinalIgnoreCase
                            );

                        foreach (var proposal in tagProposalReq.ProposalList)
                        {
                            if (string.IsNullOrEmpty(proposal.ProposalNumber))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                    TraceId: fetchPaymentDetails.trace_id,
                                    Message: $"Input proposal number missing | txn={tagProposalReq?.TransactionID} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal Number cannot be empty in the Proposal List";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            var key =
                                $"{(Convert.ToDecimal(proposal.ProposalAmount).ToString("F2") ?? string.Empty).Trim()}|{(proposal.ProposalType ?? string.Empty).Trim().ToUpper()}|{(proposal.core ?? string.Empty).Trim().ToUpper()}";

                            if (!dbByKey.TryGetValue(key, out var queue) || queue.Count == 0)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                   TraceId: fetchPaymentDetails.trace_id,
                                   Message: $"Proposal mismatch (amount/type/core not found in DB) | txn={tagProposalReq?.TransactionID} | key={key} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "different proposal numbers exist against this transaction.";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            var dbRow = queue.Dequeue();
                            dbRow.proposal_number = proposal.ProposalNumber;
                            dbRow.proposal_id = proposal.ProposalId;
                            fetchPaymentDetails.fetchPaymentProposalList.Add(dbRow);
                        }

                        if (dbByKey.Values.Any(q => q.Count > 0))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - TagProposalv3() ",
                               TraceId: fetchPaymentDetails.trace_id,
                               Message: $"Unmatched DB rows remain after fallback mapping | txn={tagProposalReq?.TransactionID} | request={JsonConvert.SerializeObject(tagProposalReq)} | db={JsonConvert.SerializeObject(fetchPaymentDetails)}"), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "different proposal numbers exist against this transaction.";
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                            return response;
                        }
                    }

                    bool isValidParsedDate = dateFormatter.TryNormalizeDate(fetchPaymentDetails.created_on, out DateTime parsedDate);
                    if (!isValidParsedDate)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                           Message: "Invalid created on fetched for transaction id: " + fetchPaymentDetails.transaction_id + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                           TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid created on fetched for transaction id: " + fetchPaymentDetails.transaction_id;
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }
                    DateTime createdOn = parsedDate;

                    if (fetchPaymentDetails.application_name.ToUpper().StartsWith("MOTOR") && createdOn.AddDays(3) < DateTime.Now)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "created on is outside allowed range for application name: " + fetchPaymentDetails.application_name + ". Date: " + createdOn + ", Allowed Range within 3 days for transaction id: " + fetchPaymentDetails.transaction_id + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);
                    }
                    else if (!fetchPaymentDetails.application_name.ToUpper().StartsWith("MOTOR") && createdOn.AddHours(12) < DateTime.Now)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                        Message: "created on is outside allowed range for application name: " + fetchPaymentDetails.application_name + ". Date: " + createdOn + ", Allowed Range within 12 hours for transaction id: " + fetchPaymentDetails.transaction_id + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);
                    }
                    else
                    {
                        encryptedId = encryptDecrypt.EncryptRequest($"{tagProposalReq.TransactionID}|{fetchPaymentDetails.trace_id}");
                        redirectUrl = ApplicationConfig.RETURN_STATUS_URL + "id=" + encryptedId + "&status=" + order_status;
                        if (string.IsNullOrEmpty(redirectUrl) || string.IsNullOrEmpty(encryptedId))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "PaymentBAL.cs - TagProposalv3() ",
                               TraceId: fetchPaymentDetails.trace_id,
                               Message: "Unable to fetch Return Status URL. Base URL: " + redirectUrl + " Encrypted ID: " + encryptedId + " Transaction ID: " + tagProposalReq.TransactionID + " Trace ID: " + fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch Return Status URL";
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                            return response;
                        }
                    }

                    if (fetchPaymentDetails.fetchPaymentProposalList.Count > 0 && fetchPaymentDetails.fetchPaymentProposalList.All(x => !string.IsNullOrEmpty(x.proposal_number) && !string.IsNullOrEmpty(x.policy_number)))
                    {
                        tagProposalResp.redirectUrl = redirectUrl;
                        tagProposalResp.ProposalList = fetchPaymentDetails.fetchPaymentProposalList
                            .Select(x => new TagProposalListv3
                            {
                                proposalNumber = x.proposal_number,
                                policyNumber = x.policy_number
                            })
                            .ToList();

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Policy Numbers already exists";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = tagProposalResp;
                        return response;
                    }

                    var proposalTasks = new List<Task>();
                    var responses = new ConcurrentBag<(FetchProposalDetailsResp ApiResp, FetchPaymentProposalList fetchPaymentProposalDetails)>();

                    foreach (FetchPaymentProposalList proposal in fetchPaymentDetails.fetchPaymentProposalList)
                    {
                        proposalTasks.Add(Task.Run(async () =>
                        {
                            FetchProposalDetailsResp apiResp = await FetchProposalDetails(fetchPaymentDetails.trace_id, proposal.proposal_number, proposal.proposal_amount, proposal.proposal_id, proposal.proposal_type, proposal.core, flow_type);
                            responses.Add((apiResp, proposal));
                        }));
                    }

                    await Task.WhenAll(proposalTasks);

                    foreach (var item in responses)
                    {
                        fetchProposalDetailsResp = item.ApiResp;
                        fetchPaymentProposalDetails = item.fetchPaymentProposalDetails;

                        if (fetchProposalDetailsResp != null && fetchProposalDetailsResp.Success)
                        {
                            if (string.IsNullOrEmpty(fetchPaymentProposalDetails.customer_id))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                   Message: "Could not find customer id from proposal details for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                   TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Could not match proposal details for proposal number";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (!string.IsNullOrEmpty(fetchPaymentProposalDetails.customer_id) && fetchPaymentProposalDetails.customer_id != fetchProposalDetailsResp.CustomerDetails?.CustomerId)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                   Message: "Fetched Customer ID does not match input Customer ID for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                   TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Fetched Customer ID does not match input Customer ID for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            inputtotalProposalAmount += Convert.ToDecimal(fetchPaymentProposalDetails.proposal_amount);
                            totalProposalAmount += Convert.ToDecimal(fetchProposalDetailsResp.PremiumDetails.TotalPremium);

                            if (string.IsNullOrEmpty(fetchProposalDetailsResp.proposalNumber) || string.IsNullOrEmpty(fetchProposalDetailsResp.proposalAmount))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                    Message: "Proposal Number or Proposal Amount cannot be empty in the Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal Number or Proposal Amount cannot be empty in the Proposal List";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (Math.Abs(Convert.ToDecimal(fetchProposalDetailsResp.proposalAmount) - Convert.ToDecimal(fetchProposalDetailsResp.PremiumDetails.TotalPremium)) > 5)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                    Message: "Proposal Amount does not match with amount fetched from core for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Proposal Amount does not match with amount fetched from core";
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (fetchPaymentDetails.payer_type.ToUpper() == "C" && string.IsNullOrEmpty(fetchProposalDetailsResp.CustomerDetails?.CustomerId))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                    Message: "Customer ID cannot be empty for payertype 'C' for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Customer ID cannot be empty for payertype 'C' for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (fetchPaymentDetails.payer_type.ToUpper() == "I" && string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                    Message: "Intermediary Code cannot be empty for payertype 'I' for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                    TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Intermediary Code cannot be empty for payertype 'I' for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                response.TraceId = fetchPaymentDetails.trace_id;
                                response.Data = null;
                                return response;
                            }

                            if (fetchProposalDetailsResp.core.ToUpper() == "PF")
                            {
                                pfCustomerIdSet.Add(fetchProposalDetailsResp.CustomerDetails?.CustomerId);
                                if (pfCustomerIdSet.Count > 1)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Customer ID cannot be different for the PF proposal numbers. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Customer ID cannot be different for the PF proposal numbers";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (string.IsNullOrEmpty(fetchProposalDetailsResp.DealId) && string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode))
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Either Deal ID or Intermediary Code is required in PF in the Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Either Deal ID or Intermediary Code is required in PF in the Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (!string.IsNullOrEmpty(fetchPaymentProposalDetails.deal_id) && fetchPaymentProposalDetails.deal_id != fetchProposalDetailsResp.DealId)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Fetched Deal ID does not match input Deal ID for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Fetched Deal ID does not match input Deal ID for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (!string.IsNullOrEmpty(fetchPaymentProposalDetails.intermediary_code) && fetchPaymentProposalDetails.intermediary_code != fetchProposalDetailsResp.IntermediaryCode)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Fetched Intermediary Code does not match input Intermediary Code for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Fetched Intermediary Code does not match input Intermediary Code for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (!string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalStatus))
                                {
                                    if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(fetchProposalDetailsResp.ProposalStatus.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(fetchProposalDetailsResp.ProposalStatus.ToUpper()))
                                    {
                                        fetchProposalDetailsResp.IsNSTPFlowStr = "N";
                                    }
                                    else
                                    {
                                        fetchProposalDetailsResp.IsNSTPFlowStr = "Y";
                                    }
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "PF Proposal Status Fetching Failed. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Could not fetch PF Proposal Status.";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }
                            }
                            else if (fetchProposalDetailsResp.core.ToUpper() == "ARTEMIS")
                            {
                                artemisCustomerIdSet.Add(fetchProposalDetailsResp.CustomerDetails?.CustomerId);
                                if (artemisCustomerIdSet.Count > 1)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Customer ID cannot be different for the Artemis proposal numbers. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Customer ID cannot be different for the Artemis proposal numbers";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (string.IsNullOrEmpty(fetchProposalDetailsResp.IntermediaryCode))
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Intermediary Code is required in Artemis in Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Intermediary Code is required in Artemis in Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (!string.IsNullOrEmpty(fetchPaymentProposalDetails.intermediary_code) && fetchPaymentProposalDetails.intermediary_code != fetchProposalDetailsResp.IntermediaryCode)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Fetched Intermediary Code does not match input Intermediary Code in the Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Fetched Intermediary Code does not match input Intermediary Code in the Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalStatus) && string.IsNullOrEmpty(fetchProposalDetailsResp.ProposalSubStatus))
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "Proposal Status and Proposal Sub Status are required in Artemis in Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Proposal Status and Proposal Sub Status are required in Artemis in Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }

                                if (fetchProposalDetailsResp.IsNSTPFlow == null)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                        Message: "IsNSTPFlow is reqiured in Artemis in Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                        TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "IsNSTPFlow is reqiured in Artemis in Proposal List for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = null;
                                    return response;
                                }
                                fetchProposalDetailsResp.IsNSTPFlowStr = fetchProposalDetailsResp.IsNSTPFlow == true ? "Y" : "N";
                            }

                            fetchPaymentProposalDetails.proposal_status = string.IsNullOrEmpty(fetchProposalDetailsResp?.ProposalSubStatus) ? fetchProposalDetailsResp?.ProposalStatus : fetchProposalDetailsResp?.ProposalSubStatus;
                            fetchPaymentProposalDetails.is_nstp_flow = fetchProposalDetailsResp.IsNSTPFlowStr;

                            updateAdvancePaymentDetailsList.Add(new UpdateAdvancePaymentDetails()
                            {
                                proposal_number = fetchPaymentProposalDetails.proposal_number,
                                proposal_id = fetchPaymentProposalDetails.proposal_id,
                                proposal_status = fetchPaymentProposalDetails.proposal_status,
                                is_nstp_flow = fetchPaymentProposalDetails.is_nstp_flow,
                                proposal_start_date = fetchProposalDetailsResp.ProposalStartDate,
                                name = fetchProposalDetailsResp.CustomerDetails?.CustomerName,
                                email = fetchProposalDetailsResp.CustomerDetails?.CustomerEmail,
                                mobile_number = fetchProposalDetailsResp.CustomerDetails?.CustomerMobile,
                                proposal_end_date = fetchProposalDetailsResp.ProposalEndDate,
                                proposal_tenure = fetchProposalDetailsResp.ProposalTenure,
                                basic_premium = fetchProposalDetailsResp.PremiumDetails?.BasicPremium,
                                gst = fetchProposalDetailsResp.PremiumDetails?.GST,
                                vrn = fetchProposalDetailsResp.MotorAdditionalInfo?.RegistrationNo,
                                address = fetchProposalDetailsResp.CustomerDetails?.CustomerAddress,
                                state = fetchProposalDetailsResp.CustomerDetails?.State,
                                city = fetchProposalDetailsResp.CustomerDetails?.City,
                                pincode = fetchProposalDetailsResp.CustomerDetails?.Pincode,
                                proposal_amount = fetchPaymentProposalDetails.proposal_amount,
                                proposal_type = fetchPaymentProposalDetails.proposal_type,
                                core = fetchPaymentProposalDetails.core
                            });
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                Message: "Could not fetch Proposal Details for proposal number: " + fetchPaymentProposalDetails.proposal_number + " Request: " + JsonConvert.SerializeObject(tagProposalReq),
                                TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Could not fetch Proposal Details for proposal number: " + fetchPaymentProposalDetails.proposal_number;
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                            return response;
                        }
                    }

                    if (Math.Abs(totalAmount - totalProposalAmount) > tagProposalReq.ProposalList.Count * 5)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposalv3() ",
                            Message: "The Total Amount does not match with the Proposal Amount Sum. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                            TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "The Total Amount does not match with the Proposal Amount Sum.";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    if (totalAmount != inputtotalProposalAmount)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - AddPaymentRequestv2() ",
                            Message: "The Total Amount does not match with the input Proposal Amount Sum. Request: " + JsonConvert.SerializeObject(tagProposalReq),
                            TraceId: fetchPaymentDetails.trace_id), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "The Total Amount does not match with the input Proposal Amount Sum.";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                        return response;
                    }

                    await paymentDAL.UpdateAdvanceDBTransactionv3(updateAdvancePaymentDetailsList, fetchPaymentDetails.transaction_id, fetchPaymentDetails.trace_id);

                    foreach (FetchPaymentProposalList fetchPaymentProposal in fetchPaymentDetails.fetchPaymentProposalList)
                    {
                        uniqueIsNSTPFlagList.Add(fetchPaymentProposal.is_nstp_flow);
                    }

                    if (fetchPaymentDetails.fetchPaymentProposalList[0].core.ToUpper() == "PF")
                    {
                        // PF
                        // Create Payment Entry and Tagging Request Body (Check if any of the proposal statuses are NCCN or RCCN. If Yes, then do both Entry and Tagging, otherwise only Entry)
                        if (string.IsNullOrEmpty(fetchPaymentDetails.payment_id))
                        {
                            if (uniqueIsNSTPFlagList.Contains("N"))
                            {
                                pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(true, true, fetchPaymentDetails);
                            }
                            else
                            {
                                pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(true, false, fetchPaymentDetails);
                            }
                        }
                        else 
                        {
                            pfPolicyCreationReq = callbackBAL.CreatePFWebhookEntryTaggingReqBody(false, true, fetchPaymentDetails);
                        }

                        if (pfPolicyCreationReq == null)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                TraceId: fetchPaymentDetails.trace_id,
                                Message: "Error while creating request body for PF Policy Creation. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                            response.TraceId = fetchPaymentDetails.trace_id;
                            response.Data = null;
                        }
                        else
                        {
                            // ILESB Payment Entry and Tagging API Call
                            pfPolicyCreationResp = await ilesbInterface.GeneratePFPolicyData(pfPolicyCreationReq, flow_type);
                            if (pfPolicyCreationResp == null)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                   TraceId: fetchPaymentDetails.trace_id,
                                   Message: "Error while calling Policy Creation API. Tag proposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);
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
                                        if (!string.IsNullOrEmpty(item.PolicyNo) && item.PolicyNo != "0")
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
                                            tagProposalResp.ProposalList.Add(new TagProposalListv3()
                                            {
                                                policyNumber = item.PolicyNo,
                                                proposalNumber = item.ProposalNo
                                            });
                                        }

                                        await callbackDAL.updatePaymentEntryTaggingDetails(item.ProposalNo, item.PolicyNo);
                                    }

                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            foreach (var reverse_sync_proposal in syncProposalToSourceList)
                                            {
                                                await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                                MethodName: "PaymentBAL.cs - TagProposalv3() - Background SyncProposalToSource Policy Status",
                                                TraceId: fetchPaymentDetails.trace_id,
                                                Message: $"Exception occurred: {ex.Message}",
                                                StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
                                        }
                                    });

                                    tagProposalResp.redirectUrl = redirectUrl;
                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = "";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = tagProposalResp;
                                    return response;
                                }
                            }
                        }
                    }
                    else if (fetchPaymentDetails.fetchPaymentProposalList[0].core.ToUpper() == "ARTEMIS")
                    {
                        // ARTEMIS
                        // Create Payment Entry and Tagging Request Body (Check if any of the proposal statuses are NCCN or RCCN. If Yes, then do both Entry and Tagging, otherwise only Entry)
                        if (string.IsNullOrEmpty(fetchPaymentDetails.payment_id))
                        {
                            if (uniqueIsNSTPFlagList.Contains("N"))
                            {
                                artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                            }
                            else
                            {
                                artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(false, fetchPaymentDetails);
                            }

                            if (artemisPolicyCreationReq == null)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv2() ",
                                    TraceId: fetchPaymentDetails.trace_id,
                                    Message: "Error while creating request body for Artemis Policy Creation. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);
                            }
                            else
                            {
                                // API FACTORY Payment Entry and Tagging API Call
                                RestResponse apiResponse = await apiFactoryInterface.GenerateArtemisPolicyData(fetchPaymentDetails.trace_id, artemisPolicyCreationReq, flow_type);

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
                                       MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                       TraceId: fetchPaymentDetails.trace_id,
                                       Message: "Error while calling Artemis Policy Creation API. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);
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
                                                    tagProposalResp.ProposalList.Add(new TagProposalListv3()
                                                    {
                                                        policyNumber = item.policyNumber,
                                                        proposalNumber = item.proposalNumber
                                                    });
                                                }

                                                await callbackDAL.updatePaymentEntryTaggingDetails(item.proposalNumber, item.policyNumber);
                                            }

                                            _ = Task.Run(async () =>
                                            {
                                                try
                                                {
                                                    foreach (var reverse_sync_proposal in syncProposalToSourceList)
                                                    {
                                                        await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                                        MethodName: "PaymentBAL.cs - TagProposalv3() - Background SyncProposalToSource Policy Status",
                                                        TraceId: fetchPaymentDetails.trace_id,
                                                        Message: $"Exception occurred: {ex.Message}",
                                                        StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
                                                }
                                            });

                                            tagProposalResp.redirectUrl = redirectUrl;
                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "";
                                            response.TraceId = fetchPaymentDetails.trace_id;
                                            response.Data = tagProposalResp;
                                            return response;
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
                                                    tagProposalResp.ProposalList.Add(new TagProposalListv3()
                                                    {
                                                        policyNumber = item.taggedPolicyNumber,
                                                        proposalNumber = item.taggedProposalNumber
                                                    });
                                                }

                                                await callbackDAL.UpdateCorePaymentID(item.taggedReceiptNumber, fetchPaymentDetails.transaction_id);
                                                await callbackDAL.updatePaymentEntryTaggingDetails(item.taggedProposalNumber, item.taggedPolicyNumber);
                                            }

                                            _ = Task.Run(async () =>
                                            {
                                                try
                                                {
                                                    foreach (var reverse_sync_proposal in syncProposalToSourceList)
                                                    {
                                                        await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                                        MethodName: "PaymentBAL.cs - TagProposalv3() - Background SyncProposalToSource Policy Status",
                                                        TraceId: fetchPaymentDetails.trace_id,
                                                        Message: $"Exception occurred: {ex.Message}",
                                                        StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
                                                }
                                            });

                                            tagProposalResp.redirectUrl = redirectUrl;
                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "";
                                            response.TraceId = fetchPaymentDetails.trace_id;
                                            response.Data = tagProposalResp;
                                            return response;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            artemisPolicyTaggingOnlyReq = callbackBAL.CreateArtemisWebhookTaggingOnlyReqBody(fetchPaymentDetails);
                            if (artemisPolicyTaggingOnlyReq == null)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                    TraceId: fetchPaymentDetails.trace_id,
                                    Message: "Error while creating request body for Artemis Policy Tagging. Retry Case: " + JsonConvert.SerializeObject(tagProposalReq)), ApplicationConfig.PAYMENT_LOGS);
                            }
                            else
                            {
                                artemisPolicyTaggingOnlyResp = await apiFactoryInterface.GenerateArtemisTaggingOnlyData(fetchPaymentDetails.trace_id, artemisPolicyTaggingOnlyReq, flow_type);
                                if (artemisPolicyTaggingOnlyResp == null)
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                       MethodName: "PaymentBAL.cs - TagProposalv3() ",
                                       TraceId: fetchPaymentDetails.trace_id,
                                       Message: "Error while calling Artemis Policy Tagging API. Retry Case: " + JsonConvert.SerializeObject(tagProposalReq) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyTaggingOnlyReq)), ApplicationConfig.PAYMENT_LOGS);
                                }

                                if (artemisPolicyTaggingOnlyResp != null && artemisPolicyTaggingOnlyResp.data != null && artemisPolicyTaggingOnlyResp.statusCode == 200 && artemisPolicyTaggingOnlyResp.data.tagging != null && artemisPolicyTaggingOnlyResp.data.tagging.Count > 0)
                                {
                                    foreach (var item in artemisPolicyTaggingOnlyResp.data.tagging)
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
                                            tagProposalResp.ProposalList.Add(new TagProposalListv3()
                                            {
                                                policyNumber = item.policyNumber,
                                                proposalNumber = item.proposalNumber
                                            });
                                        }

                                        await callbackDAL.updatePaymentEntryTaggingDetails(item.proposalNumber, item.policyNumber);
                                    }

                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            foreach (var reverse_sync_proposal in syncProposalToSourceList)
                                            {
                                                await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                                MethodName: "PaymentBAL.cs - TagProposalv3() - Background SyncProposalToSource Policy Status",
                                                TraceId: fetchPaymentDetails.trace_id,
                                                Message: $"Exception occurred: {ex.Message}",
                                                StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);
                                        }
                                    });

                                    tagProposalResp.redirectUrl = redirectUrl;
                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = "";
                                    response.TraceId = fetchPaymentDetails.trace_id;
                                    response.Data = tagProposalResp;
                                    return response;
                                }
                            }
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - TagProposalv3() ",
                            TraceId: fetchPaymentDetails.trace_id,
                            Message: "Invalid Core fetched from DB. TagProposal Request: " + tagProposalReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid Core fetched from DB";
                        response.TraceId = fetchPaymentDetails.trace_id;
                        response.Data = null;
                    }

                    await callbackDAL.UpdateNoOfAttemptsCountByTransactionID(fetchPaymentDetails.transaction_id);

                    foreach (var reverse_sync_proposal in syncProposalToSourceList)
                    {
                        await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, fetchPaymentDetails.journey_type);
                    }

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                    response.TraceId = fetchPaymentDetails.trace_id;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - TagProposalv3() ",
                    TraceId: fetchPaymentDetails.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                objLog.LogAWSMessageAsync(
                   objLog.GenericAWSException_CWL(
                       MethodName: "PaymentBAL.cs - TagProposalv3() ",
                       TraceId: fetchPaymentDetails.trace_id,
                       Message: "Request: " + JsonConvert.SerializeObject(tagProposalReq) + " Response: " + JsonConvert.SerializeObject(response),
                       StackTrace: ""
                   ),
                   ApplicationConfig.PAYMENT_LOGS
               );          
            }

            return response;
        }

        public string? GetProductFromProposalList(List<ProposalListv2> proposalList)
        {
            if (proposalList == null || proposalList.Count == 0)
                return null;

            // Extract proposal types
            var incoming = proposalList
                .Where(p => !string.IsNullOrWhiteSpace(p.ProposalType))
                .Select(p => p.ProposalType.Trim().ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Normalize key (sort alphabetically)
            string key = string.Join("|", incoming.OrderBy(x => x));

            // Lookup in dictionary
            if (ApplicationConfig.ProposalToProductsMap.TryGetValue(key, out var products))
            {
                // return the first matching product
                return products.FirstOrDefault();
            }

            return ""; // no valid product combination found
        }

        public async Task<DPSStatusResponse> GetAgentDetails(string proposal_type, string intermediaryCode, string customer_id, string payer_type, string flow_type, string trace_id)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            ArtemisAgentDetailsResp artemisAgentDetails = new ArtemisAgentDetailsResp();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            ArtemisAgentDetailsForProductCode agentDetailsForProductCode = new ArtemisAgentDetailsForProductCode();
            PaymentDAL paymentDAL = new PaymentDAL();
            string productCode = string.Empty;

            try
            {
                productCode = await paymentDAL.GetProductCodeFromProposalType(trace_id, proposal_type);

                if (!string.IsNullOrEmpty(productCode))
                {
                    artemisAgentDetails = await apiFactoryInterface.GetAgentDetailsByIntermediaryCode(trace_id, intermediaryCode, flow_type);

                    if (artemisAgentDetails != null && artemisAgentDetails.statusCode == 200)
                    {
                        var productDetails = artemisAgentDetails.data?.products?.FirstOrDefault(x => x.productCode.Equals(productCode, StringComparison.OrdinalIgnoreCase));
                        if (productDetails != null)
                        {
                            agentDetailsForProductCode.business_vertical_code = productDetails.verticalCode;
                            agentDetailsForProductCode.sub_vertical_code = productDetails.subVerticalCode;
                            agentDetailsForProductCode.marketing_officer = productDetails.rmid;
                            agentDetailsForProductCode.proposer_code = payer_type == "C" ? customer_id : intermediaryCode;

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Agent details fetched successfully";
                            response.TraceId = trace_id;
                            response.Data = agentDetailsForProductCode;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "PaymentBAL.cs - FetchAgentDetails() ",
                                TraceId: trace_id,
                                Message: $"Product Code {productCode} not found in Artemis Agent Details for Intermediary Code: {intermediaryCode}",
                                StackTrace: ""), ApplicationConfig.PAYMENT_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = $"Product Code {productCode} not found in Artemis Agent Details for Intermediary Code: {intermediaryCode}";
                            response.TraceId = trace_id;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "PaymentBAL.cs - FetchAgentDetails() ",
                            TraceId: trace_id,
                            Message: $"Could not fetch agent details from Artemis for Intermediary Code: {intermediaryCode}. Status Code: {artemisAgentDetails?.statusCode}, Message: {artemisAgentDetails?.messages}",
                            StackTrace: ""), ApplicationConfig.PAYMENT_LOGS);
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = $"Could not fetch agent details from Artemis for Intermediary Code: {intermediaryCode}. Status Code: {artemisAgentDetails?.statusCode}, Message: {artemisAgentDetails?.messages}";
                        response.TraceId = trace_id;
                        response.Data = null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PaymentBAL.cs - FetchAgentDetails() ",
                        TraceId: trace_id,
                        Message: $"Product Code not found for Proposal Type: {proposal_type}",
                        StackTrace: ""), ApplicationConfig.PAYMENT_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = $"Product Code not found for Proposal Type: {proposal_type}";
                    response.TraceId = trace_id;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - FetchAgentDetails() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = trace_id;
                response.Data = null;
            }

            return response;
        }

        public async Task<CheckProposalV2Resp> checkproposal(AddPaymentReq addPaymentReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            OrderBAL orderBAL = new OrderBAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            CheckProposalV2Resp result = new CheckProposalV2Resp();

            try
            {
                // 1) Basic request validation
                if (addPaymentReq == null || addPaymentReq.ProposalList == null || addPaymentReq.ProposalList.Count == 0)
                {
                    return result;
                }

                var proposalNumbers = addPaymentReq.ProposalList
                    .Where(x => x != null && !string.IsNullOrEmpty(x.ProposalNumber))
                    .Select(x => x.ProposalNumber)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (proposalNumbers.Count == 0)
                {
                    return result;
                }

                // 2) Fetch historical txn/order rows for given proposal list
                var rows = await paymentDAL.checkproposal(addPaymentReq);
                if (rows == null || rows.Count == 0)
                {
                    return result;
                }

                // 3) Build active transaction map per proposal (active = SUCCESS/PENDING)
                var activeTxnByProposal = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (string proposal in proposalNumbers)
                {
                    activeTxnByProposal[proposal] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var proposalRows = rows
                        .Where(x => x != null && string.Equals(x.proposal_number, proposal, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var row in proposalRows)
                    {
                        string currentStatus = string.Empty;

                        // 4) Realtime order status fetch via unified GetOrderStatus (handles PG-specific logic internally)
                        if (!string.IsNullOrEmpty(row.dps_order_id) &&
                            !string.IsNullOrEmpty(row.trace_id) &&
                            !string.IsNullOrEmpty(row.application_name))
                        {
                            try
                            {
                                DPSStatusResponse orderResp = await orderBAL.GetOrderStatus(new FetchOrderStatusReq
                                {
                                    dps_order_id = row.dps_order_id,
                                    trace_id = row.trace_id
                                }, row.application_name, flow_type);

                                currentStatus = (orderResp?.Data as FetchOrderStatusResp)?.payment_status;
                            }
                            catch (Exception ex)
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "PaymentBAL.cs - checkproposal() - GetOrderStatus ",
                                    TraceId: row.trace_id,
                                    Message: $"Exception occurred: {ex.Message}",
                                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                                result.is_error = true;
                                result.trace_id = row.trace_id;
                                result.error_message = "Error while fetching order status for proposal: " + proposal;
                                return result;
                            }
                        }

                        currentStatus = (currentStatus ?? string.Empty).Trim().ToUpper();

                        // 5) Status classification
                        if (currentStatus == "SUCCESS" || currentStatus == "PENDING")
                        {
                            if (!string.IsNullOrEmpty(row.transaction_id))
                            {
                                activeTxnByProposal[proposal].Add(row.transaction_id);
                            }
                        }
                        else if (currentStatus == string.Empty || currentStatus == "NEW" || currentStatus == "FAILED")
                        {
                            // allowed non-active statuses
                        }
                        else
                        {
                            // unknown status => explicit business error
                            result.is_error = true;
                            result.trace_id = row.trace_id;
                            result.error_message = "Unknown status of the order for proposal: " + proposal;
                            return result;
                        }
                    }
                }

                // 6) If no active txn found for any proposal, allow new flow
                bool hasAnyActive = activeTxnByProposal.Any(x => x.Value.Count > 0);
                if (!hasAnyActive)
                {
                    return result;
                }

                // 7) Find common active transaction across all proposals
                HashSet<string> commonTxnIds = null;
                foreach (string proposal in proposalNumbers)
                {
                    if (commonTxnIds == null)
                    {
                        commonTxnIds = new HashSet<string>(activeTxnByProposal[proposal], StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        commonTxnIds.IntersectWith(activeTxnByProposal[proposal]);
                    }
                }

                // 8) If common active txn exists, return existing payment URL
                if (commonTxnIds != null && commonTxnIds.Count > 0)
                {
                    string selectedTxnId = commonTxnIds.First();

                    var selectedRows = rows
                        .Where(x => x != null && string.Equals(x.transaction_id, selectedTxnId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (selectedRows.Count == 0)
                    {
                        return result;
                    }

                    var first = selectedRows.FirstOrDefault(x => !string.IsNullOrEmpty(x.payment_url)) ?? selectedRows.First();

                    string encryptedId = encryptDecrypt.EncryptRequest($"{first.transaction_id}|{first.trace_id}");
                    string finalUrl = $"{ApplicationConfig.DPS_PROXY_URL}{encryptedId}";
                    if (!string.IsNullOrEmpty(first.send_payment_link) && first.send_payment_link.ToUpper() == "TRUE")
                    {
                        finalUrl = $"{ApplicationConfig.SEND_PAYMENT_LINK_BASE_URL}?id={encryptedId}";
                    }

                    result.addPaymentResp = new AddPaymentRespv2
                    {
                        TransactionID = first.transaction_id,
                        URL = finalUrl,
                        pg_name = first.pg_name
                    };
                    result.trace_id = first.trace_id;
                    return result;
                }

                // 9) Mixed combination (active for some but not all on same txn) => block
                result.is_error = true;
                result.error_message = "These combination of proposals are not allowed as payment is already in progress/done for few proposals.";
                return result;
            }
            catch (Exception ex)
            {
                // 10) Outer safety net to avoid unhandled 500
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - checkproposal() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                result.is_error = true;
                result.error_message = "Error while checking existing proposal transactions.";
                return result;
            }
        }

        public async Task<bool> EMIModifyProposal(string trace_id, string proposalId, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            ReverseSyncToken reverseSyncToken = new ReverseSyncToken();
            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string token = await reverseSyncToken.GetWebAuthorizationToken(trace_id, flow_type);

                var reqBody = new EMIModifyProposalReq { proposalid = proposalId };

                string url = ApplicationConfig.EMI_MODIFY_PROPOSAL_API_URL;
                var client = new RestClient();

                var request = new RestRequest(url, Method.Post);
                request.AddHeader("accept", "*/*");
                request.AddHeader("Content-Type", "application/json-patch+json");
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddParameter("application/json-patch+json", JsonConvert.SerializeObject(reqBody), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();
                RestResponse response = await client.ExecuteAsync(request);
                stopwatch.Stop();

                await paymentDAL.LogIntegrationApi(trace_id, proposalId, JsonConvert.SerializeObject(reqBody), response.Content, url, "ECHANNEL EMI MODIFY PROPOSAL", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(stopwatch.Elapsed.TotalSeconds));

                if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    EMIModifyProposalResp apiResponse = JsonConvert.DeserializeObject<EMIModifyProposalResp>(response.Content);

                    return apiResponse.success;
                }

            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PaymentBAL.cs - EMIModifyProposal() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PAYMENT_LOGS);

                return false;
            }

            return false;
        }
    }
}