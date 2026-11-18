using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Newtonsoft.Json;
using Org.BouncyCastle.Ocsp;
using RestSharp;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Transactions;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.BreakInModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RefundModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;
using static Hangfire.Storage.JobStorageFeatures;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DigitalPaymentSystem_ILTC.BAL
{

    public class BreakInBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<DPSStatusResponse> BreakInGeneration(CreateBreakInReq createBreakInReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            BreakInDAL breakinDAL = new BreakInDAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            IlesbCreateBreakInResp ilesbCreateBreakInResp = new IlesbCreateBreakInResp();
            List<breakInProposals> breakInProposals = new List<breakInProposals>();

            string transactionID = string.Empty;
            string traceID = string.Empty;

            CreateBreakInResp createBreakInResp = new CreateBreakInResp();
            List<BreakInIDList> breakInIDList = new List<BreakInIDList>();

            try
            {
                string decID = encryptDecrypt.DecryptResponse(createBreakInReq.ID);

                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        breakInProposals = await breakinDAL.GetBreakInProposalsByTransactionID(transactionID, traceID);
                        if (breakInProposals != null && breakInProposals.Count > 0)
                        {
                            foreach (breakInProposals breakInProposal in breakInProposals)
                            {
                                if (string.IsNullOrEmpty(breakInProposal.breakin_id))
                                {
                                    string breakInId = await ProcessEachBreakInCase(breakInProposal.proposal_number, breakInProposal.deal_id, transactionID, traceID, createBreakInReq, flow_type, breakInProposal.proposal_amount, breakInProposal.proposal_id, breakInProposal.proposal_type, breakInProposal.core);

                                    if (!string.IsNullOrEmpty(breakInId))
                                    {
                                        BreakInIDList breakInID = new BreakInIDList()
                                        {
                                            proposal_number = breakInProposal.proposal_number,
                                            breakin_id = breakInId
                                        };
                                        breakInIDList.Add(breakInID);
                                    }
                                }
                                else
                                {
                                    BreakInIDList breakInID = new BreakInIDList()
                                    {
                                        proposal_number = breakInProposal.proposal_number,
                                        breakin_id = breakInProposal.breakin_id
                                    };
                                    breakInIDList.Add(breakInID);
                                }

                            }

                            if (breakInIDList != null && breakInIDList.Count > 0)
                            {
                                createBreakInResp = new CreateBreakInResp()
                                {
                                    BreakInMessage = "Your details have been submitted successfully. We are reviewing your request and will provide an update shortly.",
                                    breakInIDLists = breakInIDList
                                };

                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = "BreakIn ID(s) Generated Successfully";
                                response.TraceId = traceID;
                                response.Data = createBreakInResp;
                            }
                            else
                            {
                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "No Proposals Found for BreakIn";
                                response.TraceId = traceID;
                                response.Data = createBreakInResp;
                            }
                        }
                        else
                        {
                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "No Proposals Found for BreakIn";
                            response.TraceId = traceID;
                            response.Data = createBreakInResp;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "BreakInBAL.cs - BreakInGeneration() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
                            TraceId: traceID), ApplicationConfig.BREAKIN_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = traceID;
                        response.Data = createBreakInResp;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "BreakInBAL.cs - BreakInGeneration() ",
                        Message: "Invalid ID format. ID: " + createBreakInReq.ID,
                        TraceId: traceID), ApplicationConfig.BREAKIN_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = traceID;
                    response.Data = createBreakInResp;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInBAL.cs - BreakInGeneration() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = traceID;
                response.Data = createBreakInResp;
            }

            return response;
        }

        public async Task<string> ProcessEachBreakInCase(string proposal_number, string deal_id, string transactionID, string traceID, CreateBreakInReq createBreakInReq, string flow_type, string proposal_amount, string proposal_id, string proposal_type, string core)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            BreakInDAL breakInDAL = new BreakInDAL();

            string breakInId = string.Empty;

            try
            {
                await breakInDAL.InsertBreakInData(proposal_number, transactionID, traceID, createBreakInReq.inspection_type, createBreakInReq.address, createBreakInReq.pincode, createBreakInReq.city, createBreakInReq.state);

                breakInId = await CreateBreakIn(proposal_number, deal_id, transactionID, traceID, createBreakInReq.inspection_type, createBreakInReq.address, createBreakInReq.pincode, createBreakInReq.city, createBreakInReq.state, flow_type, proposal_amount, proposal_id, proposal_type, core);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInBAL.cs - ProcessEachBreakInCase() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);
            }

            return breakInId;
        }

        public async Task<string> CreateBreakIn(string proposal_number, string deal_id, string transaction_id, string trace_id, string inspection_type, string address, string pincode, string city, string state, string flow_type, string proposal_amount, string proposal_id, string proposal_type, string core)
        {
            PaymentBAL paymentBAL = new PaymentBAL();
            BreakInDAL breakInDAL = new BreakInDAL();
            ILESBInterface ilesbInterface = new ILESBInterface();

            //string product = string.Empty;
            
            //string biztalk_token = string.Empty;
            //string proposal_details = string.Empty;
            string ilesb_token = string.Empty;

            //MTResponseFWBifurcationModels MTResponseFWBifurcationModels = new MTResponseFWBifurcationModels();
            //MTResponseTWBifurcationModels MTResponseTWBifurcationModels = new MTResponseTWBifurcationModels();
            FetchProposalDetailsResp fetchProposalDetailsResp = new FetchProposalDetailsResp();
            //PfCustomerDetails pfCustomerDetails = new PfCustomerDetails();
            IlesbCreateBreakInReq ilesbCreateBreakInReq = new IlesbCreateBreakInReq();
            IlesbCreateBreakInResp ilesbCreateBreakInResp = new IlesbCreateBreakInResp();

            try
            {
                //product = await breakInDAL.GetProductFromProposalNo(proposal_number);
                //if (string.IsNullOrEmpty(product))
                //{
                //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                //        MethodName: "BreakInBAL.cs - CreateBreakIn() ",
                //        TraceId: trace_id,
                //        Message: "Could not fetch Product from Proposal No. Proposal No: " + proposal_number + " Product: " + product), ApplicationConfig.BREAKIN_LOGS);

                //    return null;
                //}

                //if (product.ToUpper() == "MOTOR_18" || product.ToUpper() == "MOTOR_4" || product.ToUpper() == "MOTOR_6" || product.ToUpper() == "MOTOR_19" || product.ToUpper() == "MOTOR_20" || product.ToUpper() == "MOTOR_21" || product.ToUpper() == "MOTOR_5")
                //{
                //    biztalk_token = ilesbInterface.CreateBiztalkAuthToken(ApplicationConfig.BIZTALK_TOKEN_USERNAME, ApplicationConfig.BIZTALK_TOKEN_PASSWORD);
                //    proposal_details = await ilesbInterface.FetchMotorProposalDetailsFromPF(biztalk_token, proposal_number, product, trace_id, flow_type);

                //    if (!string.IsNullOrEmpty(proposal_details))
                //    {
                //        MTResponseFWBifurcationModels = JsonConvert.DeserializeObject<MTResponseFWBifurcationModels>(proposal_details);
                //        if (MTResponseFWBifurcationModels != null)
                //        {
                //            var risk = MTResponseFWBifurcationModels?.UserData?.Risks?.Risks;
                //            var cust = MTResponseFWBifurcationModels?.UserData?.CustomerDetails;

                //            ilesbCreateBreakInReq = new IlesbCreateBreakInReq
                //            {
                //                CorrelationId = trace_id,
                //                DealID = deal_id,
                //                CustomerId = cust?.CustomerID,
                //                CustomerName = cust?.CustomerName,
                //                CustomerAddress = null,
                //                State = null,
                //                City = null,
                //                Pincode = null,
                //                TelephoneNumber = null,
                //                MobileNumber = null,
                //                TypeVehicle = risk?.VehicleType,
                //                VehicleMake = risk?.VehicleMake,
                //                VehicleModel = risk?.vehicleModel,
                //                RegistrationNo = risk?.RegistrationNumber,
                //                EngineNo = risk?.EngineNumber,
                //                ChassisNo = risk?.ChassisNumber,
                //                ManufactureYear = risk?.YearOfManufacture.ToString(),
                //                BreakInDays = Convert.ToInt32(ApplicationConfig.BREAKIN_DAYS),
                //                SalesRemarks = ApplicationConfig.BREAKIN_SALES_REMARKS,
                //                BreakInType = ApplicationConfig.BREAKIN_TYPE,
                //                IsPOSDealId = Convert.ToBoolean(ApplicationConfig.BREAKIN_IS_POS_DEALID),
                //                SelfInspection = inspection_type == "SI" ? "Yes" : "No"
                //            };
                //        }
                //    }
                //}
                //else if (product.ToUpper() == "MOTOR_14" || product.ToUpper() == "MOTOR_1" || product.ToUpper() == "MOTOR_3" || product.ToUpper() == "MOTOR_15" || product.ToUpper() == "MOTOR_16" || product.ToUpper() == "MOTOR_17" || product.ToUpper() == "MOTOR_2")
                //{
                //    biztalk_token = ilesbInterface.CreateBiztalkAuthToken(ApplicationConfig.BIZTALK_TOKEN_USERNAME, ApplicationConfig.BIZTALK_TOKEN_PASSWORD);
                //    proposal_details = await ilesbInterface.FetchMotorProposalDetailsFromPF(biztalk_token, proposal_number, product, trace_id, flow_type);

                //    if (!string.IsNullOrEmpty(proposal_details))
                //    {
                //        MTResponseTWBifurcationModels = JsonConvert.DeserializeObject<MTResponseTWBifurcationModels>(proposal_details);

                //        if (MTResponseTWBifurcationModels != null)
                //        {
                //            var motor = MTResponseTWBifurcationModels?.MotorCommonResponse;
                //            var prop = motor?.PropRisks?.PropRisks;
                //            var cust = MTResponseTWBifurcationModels?.MotorCommonResponse?.CustomerDetails;

                //            ilesbCreateBreakInReq = new IlesbCreateBreakInReq
                //            {
                //                CorrelationId = trace_id,
                //                DealID = deal_id,
                //                CustomerName = cust?.CustomerName,
                //                CustomerId = cust?.CustomerID,
                //                CustomerAddress = null,
                //                State = null,
                //                City = null,
                //                Pincode = null,
                //                TelephoneNumber = null,
                //                MobileNumber = null,
                //                TypeVehicle = prop?.VehicleType,
                //                VehicleMake = prop?.VehicleMake,
                //                VehicleModel = prop?.vehicleModel,
                //                RegistrationNo = prop?.RegistrationNumber,
                //                EngineNo = prop?.EngineNumber,
                //                ChassisNo = prop?.ChassisNumber,
                //                ManufactureYear = prop?.YearOfManufacture,
                //                BreakInDays = Convert.ToInt32(ApplicationConfig.BREAKIN_DAYS),
                //                SalesRemarks = ApplicationConfig.BREAKIN_SALES_REMARKS,
                //                BreakInType = ApplicationConfig.BREAKIN_TYPE,
                //                IsPOSDealId = Convert.ToBoolean(ApplicationConfig.BREAKIN_IS_POS_DEALID),
                //                SelfInspection = inspection_type == "SI" ? "Yes" : "No"
                //            };
                //        }
                //    }
                //}
                //else if (product.ToUpper() == "MOTOR_13")
                //{
                //    fetchProposalDetailsResp = await paymentBAL.FetchProposalDetailsFWEXW(trace_id, proposal_number, "", "", "", "", flow_type);

                //    ilesbCreateBreakInReq = new IlesbCreateBreakInReq
                //    {
                //        CorrelationId = trace_id,
                //        DealID = deal_id,
                //        CustomerName = fetchProposalDetailsResp.CustomerName,
                //        CustomerId = fetchProposalDetailsResp.CustomerId,
                //        CustomerAddress = null,
                //        State = null,
                //        City = null,
                //        Pincode = null,
                //        TelephoneNumber = null,
                //        MobileNumber = null,
                //        TypeVehicle = fetchProposalDetailsResp.VehicleType,
                //        VehicleMake = fetchProposalDetailsResp.VehicleMake,
                //        VehicleModel = fetchProposalDetailsResp.vehicleModel,
                //        RegistrationNo = fetchProposalDetailsResp.RegistrationNumber,
                //        EngineNo = fetchProposalDetailsResp.EngineNumber,
                //        ChassisNo = fetchProposalDetailsResp.ChassisNumber,
                //        ManufactureYear = fetchProposalDetailsResp.YearOfManufacture,
                //        BreakInDays = fetchProposalDetailsResp.BreakInDays ?? Convert.ToInt32(ApplicationConfig.BREAKIN_DAYS),
                //        SalesRemarks = fetchProposalDetailsResp.SalesRemarks,
                //        BreakInType = fetchProposalDetailsResp.BreakInType,
                //        IsPOSDealId = fetchProposalDetailsResp.IsPOSDealId,
                //        SelfInspection = inspection_type == "SI" ? "Yes" : "No"
                //    };
                //}
                //else
                //{
                //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                //        MethodName: "BreakInBAL.cs - CreateBreakIn() ",
                //        TraceId: trace_id,
                //        Message: "Invalid Product Type for BreakIn Creation. Transaction ID: " + transaction_id + " Product: " + product), ApplicationConfig.BREAKIN_LOGS);

                //    return null;
                //}

                fetchProposalDetailsResp = await paymentBAL.FetchProposalDetails(trace_id, proposal_number, proposal_amount, proposal_id, proposal_type, core, flow_type);
                if (fetchProposalDetailsResp != null && fetchProposalDetailsResp.Success)
                {
                    ilesbCreateBreakInReq = new IlesbCreateBreakInReq
                    {
                        CorrelationId = trace_id,
                        DealID = deal_id,
                        CustomerId = fetchProposalDetailsResp?.CustomerDetails?.CustomerId,
                        CustomerName = fetchProposalDetailsResp?.CustomerDetails?.CustomerName,
                        CustomerAddress = inspection_type == "SI" ? fetchProposalDetailsResp?.CustomerDetails?.CustomerAddress : address,
                        State = inspection_type == "SI" ? fetchProposalDetailsResp?.CustomerDetails?.State : state,
                        City = inspection_type == "SI" ? fetchProposalDetailsResp?.CustomerDetails?.City : city,
                        Pincode = inspection_type == "SI" ? fetchProposalDetailsResp?.CustomerDetails?.Pincode : pincode,
                        TelephoneNumber = fetchProposalDetailsResp?.CustomerDetails?.CustomerMobile,
                        MobileNumber = fetchProposalDetailsResp?.CustomerDetails?.CustomerMobile,
                        SubLocation = inspection_type == "SI" ? fetchProposalDetailsResp?.CustomerDetails?.City : city,
                        TypeVehicle = fetchProposalDetailsResp?.MotorAdditionalInfo?.TypeVehicle,
                        VehicleMake = fetchProposalDetailsResp?.MotorAdditionalInfo?.VehicleMake,
                        VehicleModel = fetchProposalDetailsResp?.MotorAdditionalInfo?.VehicleModel,
                        RegistrationNo = fetchProposalDetailsResp?.MotorAdditionalInfo?.RegistrationNo,
                        EngineNo = fetchProposalDetailsResp?.MotorAdditionalInfo?.EngineNo,
                        ChassisNo = fetchProposalDetailsResp?.MotorAdditionalInfo?.ChassisNo,
                        ManufactureYear = fetchProposalDetailsResp?.MotorAdditionalInfo?.ManufactureYear,
                        BreakInDays = fetchProposalDetailsResp?.MotorAdditionalInfo?.BreakInDays ?? Convert.ToInt32(ApplicationConfig.BREAKIN_DAYS),
                        SalesRemarks = fetchProposalDetailsResp?.MotorAdditionalInfo?.SalesRemarks ?? ApplicationConfig.BREAKIN_SALES_REMARKS,
                        BreakInType = fetchProposalDetailsResp?.MotorAdditionalInfo?.BreakInType ?? ApplicationConfig.BREAKIN_TYPE,
                        IsPOSDealId = fetchProposalDetailsResp?.MotorAdditionalInfo?.IsPOSDealId ?? Convert.ToBoolean(ApplicationConfig.BREAKIN_IS_POS_DEALID),
                        SelfInspection = inspection_type == "SI" ? "Yes" : "No"
                    };


                    //if (!string.IsNullOrEmpty(ilesbCreateBreakInReq.CustomerId))
                    //{
                    //    biztalk_token = ilesbInterface.CreateBiztalkAuthToken(ApplicationConfig.BIZTALK_TOKEN_USERNAME, ApplicationConfig.BIZTALK_TOKEN_PASSWORD);
                    //    pfCustomerDetails = await ilesbInterface.FetchCustomerDetailsFromPF(biztalk_token, ilesbCreateBreakInReq.CustomerId, trace_id, flow_type);

                    //    if (pfCustomerDetails != null)
                    //    {
                    //        ilesbCreateBreakInReq.CustomerName = pfCustomerDetails.CustomerName;
                    //        ilesbCreateBreakInReq.CustomerAddress = inspection_type == "PI" ? address : pfCustomerDetails.MAILINGADDRESSLINE1 + " " + pfCustomerDetails.MAILINGADDRESSLINE2;
                    //        ilesbCreateBreakInReq.State = inspection_type == "PI" ? state : pfCustomerDetails.MAILING_STATE_NAME;
                    //        ilesbCreateBreakInReq.City = inspection_type == "PI" ? city : pfCustomerDetails.MAILING_CITYDISTRICT_NAME;
                    //        ilesbCreateBreakInReq.Pincode = inspection_type == "PI" ? pincode : pfCustomerDetails.MAILINGPINCODE;
                    //        ilesbCreateBreakInReq.TelephoneNumber = pfCustomerDetails.TELEPHONENUMBER;
                    //        ilesbCreateBreakInReq.MobileNumber = pfCustomerDetails.MOBILENUMBER;
                    //        ilesbCreateBreakInReq.SubLocation = pfCustomerDetails.MAILING_CITYDISTRICT_NAME;

                    ilesb_token = await ilesbInterface.GetILESBToken(ApplicationConfig.ILESB_MOTOR_SCOPE, trace_id, flow_type);
                    ilesbCreateBreakInResp = await ilesbInterface.ILESBCreateBreakIn(ilesb_token, proposal_number, ilesbCreateBreakInReq, flow_type);
                    if (ilesbCreateBreakInResp != null && ilesbCreateBreakInResp.BrkId > 0 && !string.IsNullOrEmpty(ilesbCreateBreakInResp.BrkId.ToString()))
                    {
                        ilesbCreateBreakInResp.VRN = ilesbCreateBreakInReq.RegistrationNo;
                        ilesbCreateBreakInResp.ProposalNumber = proposal_number;

                        await breakInDAL.UpdateBreakInDetails(ilesbCreateBreakInResp);

                        return ilesbCreateBreakInResp.BrkId.ToString();
                    }
                    //    }
                    //}
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "BreakInBAL.cs - CreateBreakIn() ",
                        TraceId: trace_id,
                        Message: "Could not fetch proposal details for Proposal No. Proposal No: " + proposal_number), ApplicationConfig.BREAKIN_LOGS);

                    return null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInBAL.cs - CreateBreakIn() ",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

            }

            return null;
        }

        public async Task<DPSStatusResponse> BreakInWebhook(BreakInWebhookReq breakInWebhookReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PaymentBAL paymentBAL = new PaymentBAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            BreakInDAL breakInDAL = new BreakInDAL();
            RefundBAL refundBAL = new RefundBAL();
            RetryResyncBAL retryResyncBAL = new RetryResyncBAL();
            CallbackBAL callbackBAL = new CallbackBAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            FetchRefundCase refundCase = new FetchRefundCase();

            string traceId = string.Empty;
            string token = string.Empty;
            string proposal_status = string.Empty;

            BreakInClearInspectionDetails breakInClearInspectionDetails = new BreakInClearInspectionDetails();
            BreakinClearInspectionStatusRes breakinClearInspectionStatusRes = new BreakinClearInspectionStatusRes();

            RetryResyncCase breakinTagCase = new RetryResyncCase();

            PFPolicyCreationReq pfPolicyCreationReq = new PFPolicyCreationReq();
            PFPolicyCreationResp pfPolicyCreationResp = new PFPolicyCreationResp();
            ILESBInterface ilesbInterface = new ILESBInterface();

            List<SyncProposalToSource> syncProposalToSourceList = new List<SyncProposalToSource>();

            try
            {
                if (string.IsNullOrEmpty(breakInWebhookReq.BreakInId) || string.IsNullOrEmpty(breakInWebhookReq.BreakinStatus))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "BreakInBAL.cs - BreakInWebhook()",
                            Message: "Invalid webhook details. Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                            TraceId: breakInWebhookReq?.CorelationId), ApplicationConfig.BREAKIN_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid webhook details";
                    response.TraceId = breakInWebhookReq.CorelationId;
                    response.Data = null;
                    return response;
                }

                breakInClearInspectionDetails = await breakInDAL.GetBreakInClearInspectionDetails(breakInWebhookReq.BreakInId);
                traceId = breakInClearInspectionDetails.traceId;

                await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(breakInWebhookReq), "", "", "BREAKIN WEBHOOK", "", flow_type, "");

                int update_count = await breakInDAL.UpdateBreakInStatus(breakInWebhookReq.BreakInId, breakInWebhookReq.BreakinStatus, breakInWebhookReq.InspectionDate, breakInWebhookReq.Remarks);
                if (update_count == 0)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                           MethodName: "BreakInBAL.cs - BreakInWebhook()",
                           Message: "Could not update webhook details. Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                           TraceId: traceId), ApplicationConfig.BREAKIN_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid webhook details";
                    response.TraceId = traceId;
                    response.Data = null;
                    return response;
                }
                else
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Details received successfully";
                    response.TraceId = traceId;
                    response.Data = null;
                }

                if (breakInWebhookReq.BreakinStatus.ToUpper() == "RECOMMENDED")
                {
                    breakinClearInspectionStatusRes = await ilesbInterface.ILESBBreakInClearance(breakInClearInspectionDetails.ProposalNumber, breakInClearInspectionDetails.ProposalStartDate, breakInWebhookReq.BreakInId, breakInClearInspectionDetails.DealId, breakInWebhookReq.BreakinStatus, traceId, breakInWebhookReq.InspectionDate, flow_type);
                    if (breakinClearInspectionStatusRes != null)
                    {
                        await breakInDAL.UpdateBreakInClearInspectionStatus(breakInWebhookReq.BreakInId, breakinClearInspectionStatusRes.VehicleInspectionStatus.ToUpper());

                        if (breakinClearInspectionStatusRes.VehicleInspectionStatus.ToUpper() == "PASS")
                        {
                            breakinTagCase = await breakInDAL.GetTransactionDetailsByBreakInId(breakInWebhookReq.BreakInId, traceId);
                            if (breakinTagCase != null)
                            {
                                proposal_status = await paymentBAL.FetchProposalStatus(traceId, breakinTagCase.proposal_number, breakinTagCase.core, flow_type);
                                if (!string.IsNullOrEmpty(proposal_status) && !string.IsNullOrEmpty(breakinTagCase.payment_id) && (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(proposal_status.ToUpper())))
                                {
                                    pfPolicyCreationReq = retryResyncBAL.CreatePFRetryTaggingReqBody(breakinTagCase);
                                    if (pfPolicyCreationReq == null)
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                                            TraceId: traceId,
                                            Message: "Error while creating request body for PF Policy Creation. BreakIn Case: " + JsonConvert.SerializeObject(breakinTagCase)), ApplicationConfig.BREAKIN_LOGS);
                                    }
                                    else
                                    {
                                        pfPolicyCreationResp = await ilesbInterface.GeneratePFPolicyData(pfPolicyCreationReq, flow_type);
                                        if (pfPolicyCreationResp == null)
                                        {
                                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                               MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                                               TraceId: traceId,
                                               Message: "Error while calling PF Policy Creation API. BreakIn Case: " + JsonConvert.SerializeObject(breakinTagCase) + " GeneratePFPolicyData Request Body: " + JsonConvert.SerializeObject(pfPolicyCreationReq)), ApplicationConfig.BREAKIN_LOGS);
                                        }

                                        if (pfPolicyCreationResp != null && pfPolicyCreationResp.StatusMessage.ToUpper() == "SUCCESS" && pfPolicyCreationResp.paymentTagResponse != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList != null && pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList.Count > 0)
                                        {
                                            foreach (var item in pfPolicyCreationResp.paymentTagResponse.PaymentTagResponseList)
                                            {
                                                if (item.Status.ToUpper() == "SUCCESS" && !string.IsNullOrEmpty(item.PolicyNo) && item.PolicyNo != "0")
                                                {
                                                    SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                                    {
                                                        policy_number = item.PolicyNo,
                                                        payment_id = breakinTagCase.payment_id,
                                                        proposal_number = item.ProposalNo,
                                                        reverse_sync_url = breakinTagCase.reverse_sync_url,
                                                        trace_id = breakinTagCase.trace_id,
                                                        transaction_id = breakinTagCase.transaction_id,
                                                        application_name = breakinTagCase.application_name,
                                                        channel_application_details = breakinTagCase.channel_application_details,
                                                        auth_code = breakinTagCase.auth_code,
                                                        sync_event = "POLICY_CONFIRMATION"
                                                    };
                                                    syncProposalToSourceList.Add(syncProposalToSource);
                                                }

                                                // UPDATE IN DB
                                                await callbackDAL.updatePaymentEntryTaggingDetails(item.ProposalNo, item.PolicyNo);
                                            }
                                        }
                                    }

                                    // SYNC TO SOURCE
                                    foreach (var reverse_sync_proposal in syncProposalToSourceList)
                                    {
                                        await callbackBAL.SyncProposalToSource(reverse_sync_proposal, flow_type, breakinTagCase.journey_type);
                                    }
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                     MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                                     Message: "Could not fetch Proposal Status / payment id not generated. Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                                     TraceId: traceId), ApplicationConfig.BREAKIN_LOGS);
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                                    Message: "Transaction Details not Found. Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                                    TraceId: traceId), ApplicationConfig.BREAKIN_LOGS);
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                                Message: "PF Syncing Failed. Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                                TraceId: traceId), ApplicationConfig.BREAKIN_LOGS);
                        }
                    }
                }
                else if (breakInWebhookReq.BreakinStatus.ToUpper() == "REJECTED" || breakInWebhookReq.BreakinStatus.ToUpper() == "REJECT")
                {
                    refundCase = await breakInDAL.GetBreakinRefundDetails(breakInClearInspectionDetails.ProposalNumber);
                    await refundBAL.InitiateRefund(refundCase, flow_type);

                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                        Message: "BreakIn Rejected. Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                        TraceId: traceId), ApplicationConfig.BREAKIN_LOGS);
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                        Message: "Unexpected BreakIn Status. Webhook Request: " + JsonConvert.SerializeObject(breakInWebhookReq),
                        TraceId: traceId), ApplicationConfig.BREAKIN_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInBAL.cs - BreakInWebhook() ",
                    Message: $"Exception occurred: {ex.Message}",
                    TraceId: traceId,
                    StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> CheckBreakInStatus(BreakInStatusReq breakInReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            BreakInDAL breakInDAL = new BreakInDAL();
            ILESBInterface ilesbInterface = new ILESBInterface();
            PaymentDAL paymentDAL = new PaymentDAL();

            ILESBBreakInStatusReq ilesbBreakInStatusReq = new ILESBBreakInStatusReq();

            string token = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                ilesbBreakInStatusReq = await breakInDAL.GetBreakInDetailsById(breakInReq.BreakInId);
                if (ilesbBreakInStatusReq == null)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "BreakInBAL.cs - CheckBreakInStatus()",
                        TraceId: null,
                        Message: "No data found for the given BreakInID: " + breakInReq.BreakInId), ApplicationConfig.BREAKIN_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "No data found for the given BreakInID.";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                token = await ilesbInterface.GetILESBToken(ApplicationConfig.ILESB_MOTOR_SCOPE, ilesbBreakInStatusReq.CorrelationId, flow_type);

                string breakInStatusApiUrl = ApplicationConfig.BREAKIN_STATUS_API_URL;
                var client = new RestClient(breakInStatusApiUrl);

                var request = new RestRequest { Method = Method.Post };
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddParameter("application/json", JsonConvert.SerializeObject(ilesbBreakInStatusReq), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                var apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(ilesbBreakInStatusReq.CorrelationId, "", JsonConvert.SerializeObject(ilesbBreakInStatusReq), apiResponse.Content, breakInStatusApiUrl, "FETCH BREAKIN STATUS", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Content) && apiResponse.StatusCode == HttpStatusCode.OK)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = null;
                    response.TraceId = ilesbBreakInStatusReq.CorrelationId;
                    JsonElement parsedContent = JsonSerializer.Deserialize<JsonElement>(apiResponse.Content);
                    response.Data = parsedContent;
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Could not fetch BreakIn status";
                    response.TraceId = ilesbBreakInStatusReq.CorrelationId;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "BreakInBAL.cs - CheckBreakInStatus()",
                    TraceId: ilesbBreakInStatusReq.CorrelationId,
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

        public async Task<DPSStatusResponse> FetchPincodeDetails(FetchPincodeDetailsReq fetchPincodeDetailsReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            PaymentDAL paymentDAL = new PaymentDAL();
            ReverseSyncToken reverseSyncToken = new ReverseSyncToken();
            FetchPincodeReq fetchPincodeReq = new FetchPincodeReq();
            FetchPincodeDetailsResp fetchPincodeDetailsResp = new FetchPincodeDetailsResp();

            string transactionID = string.Empty;
            string traceID = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string decID = encryptDecrypt.DecryptResponse(fetchPincodeDetailsReq.ID);

                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        string token = await reverseSyncToken.GetWebAuthorizationToken(traceID, flow_type);

                        fetchPincodeReq = new FetchPincodeReq
                        {
                            pincode = fetchPincodeDetailsReq.pincode,
                        };

                        string URL = ApplicationConfig.FETCH_PINCODE_DETAILS_API_URL;
                        var client = new RestClient();
                        var request = new RestRequest(URL, Method.Post);
                        request.AddHeader("Authorization", "Bearer " + token);
                        request.AddHeader("Content-Type", "application/json");
                        request.AddParameter("application/json", JsonConvert.SerializeObject(fetchPincodeDetailsReq), ParameterType.RequestBody);

                        stopwatch = Stopwatch.StartNew();

                        RestResponse restResponse = await client.ExecuteAsync(request);

                        stopwatch.Stop();
                        var responseTime = stopwatch.Elapsed.TotalSeconds;

                        await paymentDAL.LogIntegrationApi(traceID, "", JsonConvert.SerializeObject(fetchPincodeDetailsReq), restResponse.Content, URL, "ECHANNEL FETCH PINCODE DETAILS", JsonConvert.SerializeObject(restResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                        if (restResponse.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(restResponse.Content))
                        {
                            fetchPincodeDetailsResp = JsonConvert.DeserializeObject<FetchPincodeDetailsResp>(restResponse.Content);

                            if (fetchPincodeDetailsResp.Success && !string.IsNullOrEmpty(fetchPincodeDetailsResp.CityDistrictName) && !string.IsNullOrEmpty(fetchPincodeDetailsResp.StateName))
                            {
                                response.StatusCode = 1;
                                response.StatusMessage = "Success";
                                response.Message = "Pincode Details Fetched Successfully";
                                response.TraceId = traceID;
                                response.Data = fetchPincodeDetailsResp;
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "BreakInBAL.cs - FetchPincodeDetails() ",
                                    Message: "Could not fetch Pincode Details. Decrypted ID: " + decID,
                                    TraceId: traceID), ApplicationConfig.BREAKIN_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Could not fetch Pincode Details";
                                response.TraceId = traceID;
                                response.Data = null;
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "BreakInBAL.cs - FetchPincodeDetails() ",
                                Message: "Unable to fetch Pincode Details. Decrypted ID: " + decID,
                                TraceId: traceID), ApplicationConfig.BREAKIN_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch Pincode Details";
                            response.TraceId = traceID;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "BreakInBAL.cs - FetchPincodeDetails() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
                            TraceId: traceID), ApplicationConfig.BREAKIN_LOGS);

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
                        MethodName: "BreakInBAL.cs - FetchPincodeDetails() ",
                        Message: "Invalid ID format. ID: " + fetchPincodeDetailsReq.ID,
                        TraceId: traceID), ApplicationConfig.BREAKIN_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = traceID;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "BreakInBAL.cs - FetchPincodeDetails() ",
                   TraceId: traceID,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.BREAKIN_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = traceID;
                response.Data = null;
            }

            return response;
        }

    }
}
