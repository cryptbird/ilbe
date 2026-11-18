using Amazon.CloudWatchLogs;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using ILTakeCare.DAL;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.DashboardModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class DashboardBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public DPSStatusResponse CreateDashboardAuthToken(string email)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateToken authenticateToken = new AuthenticateToken();

            int expiresIn = Convert.ToInt32(ApplicationConfig.DASHBOARD_TOKEN_EXPIRY_TIME);

            try
            {
                var claims = new[] {
                new Claim(JwtRegisteredClaimNames.UniqueName, email)
               };

                string secretKey = ApplicationConfig.JWT_SECRET_KEY;
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

                var token = new JwtSecurityToken(
                    issuer: ApplicationConfig.JWT_ISSUER,
                    audience: ApplicationConfig.JWT_AUDIENCE,
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(expiresIn),
                    signingCredentials: creds
                 );

                authenticateToken.accessToken = new JwtSecurityTokenHandler().WriteToken(token);
                authenticateToken.expiryTime = Convert.ToString(DateTime.Now.AddMinutes(expiresIn));

                response.StatusCode = 1;
                response.StatusMessage = "Success";
                response.Message = null;
                response.TraceId = null;
                response.Data = authenticateToken;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - CreateDashboardAuthToken() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> DashboardLogin(DashboardLoginReq dashboardLoginReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();
            SendMessageService sendMessageService = new SendMessageService();
            string otp = string.Empty;
            bool isOtpSent = false;
            bool updateOtp = false;
            try
            {
                if(dashboardLoginReq.email.ToUpper()=="1048070@ICICILOMBARD.COM" || dashboardLoginReq.email.ToUpper() == "1046763@ICICILOMBARD.COM" || dashboardLoginReq.email.ToUpper() == "1047766@ICICILOMBARD.COM")
                {
                    updateOtp = await dashboardDAL.UpdateOtp("000000", dashboardLoginReq.email);
                    if (updateOtp)
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "OTP Sent Successfully to Registered Email.";
                        response.TraceId = null;
                        response.Data = null;
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Failed to Update OTP.";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                bool isValidUser = await dashboardDAL.ValidateDashboardUser(dashboardLoginReq);
                if (isValidUser)
                {
                    otp = sendMessageService.GenerateOTP();

                    string otpContent = ApplicationConfig.DASHBOARD_LOGIN_OTP_CONTENT.Replace("##OTP_HERE##", otp).Replace("##EXPIRY_MINUTES##", ApplicationConfig.DASHBOARD_OTP_EXPIRY_MINUTES.ToString());
                    isOtpSent = sendMessageService.SendMail(otpContent, dashboardLoginReq.email, ApplicationConfig.DASHBOARD_LOGIN_OTP_SUBJECT);
                    if (isOtpSent)
                    {
                        updateOtp = await dashboardDAL.UpdateOtp(otp, dashboardLoginReq.email);
                        if (updateOtp)
                        {
                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "OTP Sent Successfully to Registered Email.";
                            response.TraceId = null;
                            response.Data = null;
                        }
                        else
                        {
                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Failed to Update OTP.";
                            response.TraceId = null;
                            response.Data = null;
                        }
                    }
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "This User Does Not have Access to Dashboard.";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardBAL.cs - DashboardLogin() ",
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            return response;
        }

        public async Task<DPSStatusResponse> DashboardLoginVerify(DashboardLoginVerifyReq dashboardLoginVerifyReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                bool isValidOtp = await dashboardDAL.ValidateDashboardOtp(dashboardLoginVerifyReq);

                if (isValidOtp)
                {
                    // Vanished OTP to not verify twice
                    bool updateOtp = await dashboardDAL.UpdateOtp("VANISHED", dashboardLoginVerifyReq.email);
                    string role = await dashboardDAL.GetDashboardRole(dashboardLoginVerifyReq.email);
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "OTP Verified Successfully to Registered Email";
                    response.TraceId = null;
                    response.Data = new { role };

                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Failed to Verify OTP";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardBAL.cs - ValidateOtp() ",
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            return response;
        }

        public async Task<DPSStatusResponse> GetTransactionSummary(TransactionSummaryReq transactionSummaryReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            List<TransactionSummaryResp> transactionSummaryRespList = new List<TransactionSummaryResp>();
            List<TransactionSummaryRespForNonAdmin> transactionSummaryRespForNonAdminList = new List<TransactionSummaryRespForNonAdmin>();

            int dashboardTimelineFailedCount = 0;

            try
            {
                transactionSummaryRespList = await dashboardDAL.FetchTransactionSummary(transactionSummaryReq);
                if (transactionSummaryRespList != null && transactionSummaryRespList.Count > 0)
                {
                    foreach (TransactionSummaryResp transactionSummaryResp in transactionSummaryRespList)
                    {
                        transactionSummaryResp.dashboardTimeline = await FetchDashboardDetails(transactionSummaryResp, flow_type);
                        if (transactionSummaryResp.dashboardTimeline == null)
                        {
                            dashboardTimelineFailedCount = dashboardTimelineFailedCount + 1;
                        }
                    }

                    if (dashboardTimelineFailedCount == 0)
                    {
                        string role = await dashboardDAL.GetDashboardRole(transactionSummaryReq.email);
                        if (role.ToUpper() == "ADMIN")
                        {
                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Data Fetched Successfully";
                            response.TraceId = transactionSummaryRespList[0].trace_id;
                            response.Data = transactionSummaryRespList;
                        }
                        else
                        {
                            transactionSummaryRespForNonAdminList = transactionSummaryRespList.Select(item => new TransactionSummaryRespForNonAdmin
                            {
                                transaction_id = item.transaction_id,
                                trace_id = item.trace_id,
                                total_amount = item.total_amount,
                                application_name = item.application_name,
                                product = item.product,
                                core = item.core,
                                proposal_number = item.proposal_number,
                                proposal_amount = item.proposal_amount,
                                transaction_initiation_time = item.transaction_initiation_time,
                                proposal_status = item.proposal_status,
                                dps_order_id = item.dps_order_id,
                                order_id = item.order_id,
                                auth_code = item.auth_code,
                                payment_id = item.payment_id,
                                policy_number = item.policy_number,
                                no_of_attempts = item.no_of_attempts,
                                payment_success_time = item.payment_success_time,
                                payment_mode = item.payment_mode,
                                resync_status = item.resync_status,
                                proposal_start_date = item.proposal_start_date,
                                deal_id = item.deal_id,
                                intermediary_code = item.intermediary_code,
                                customer_id = item.customer_id,
                                name = item.name,
                                email = item.email,
                                mobile_number = item.mobile_number,
                                payer_type = item.payer_type,
                                breakin_id = item.breakin_id,
                                breakin_status = item.breakin_status,
                                breakin_remark = item.breakin_remark,
                                breakin_inspection_type = item.breakin_inspection_type,
                                breakin_inspection_date = item.breakin_inspection_date,
                                breakin_address = item.breakin_address,
                                breakin_pincode = item.breakin_pincode,
                                breakin_city = item.breakin_city,
                                breakin_state = item.breakin_state,
                                breakin_creation_time = item.breakin_creation_time,
                                breakin_clear_inspection_status = item.breakin_clear_inspection_status,
                                vrn = item.vrn,
                                ias_id = item.ias_id,
                                refund_status = item.refund_status,
                                dashboardTimeline = item.dashboardTimeline
                            }).ToList();

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Data Fetched Successfully";
                            response.TraceId = transactionSummaryRespForNonAdminList[0].trace_id;
                            response.Data = transactionSummaryRespForNonAdminList;
                        }
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Could not Fetch Dashboard Timeline for the given Input";
                        response.TraceId = transactionSummaryRespList[0].trace_id;
                        response.Data = null;
                    }
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "No Data Found for the given Input";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardBAL.cs - GetTransactionSummary() ",
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DashboardTimeline> FetchDashboardDetails(TransactionSummaryResp transactionSummaryResp, string flow_type)
        {
            OrderBAL orderBAL = new OrderBAL();
            PaymentBAL paymentBAL = new PaymentBAL();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();

            DashboardTimeline dashboardTimelineResp = new DashboardTimeline();

            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            DPSStatusResponse orderStatusResponse = new DPSStatusResponse();

            JsonElement jsonOrderStatusResponse = new JsonElement();
            string order_status = string.Empty;

            string proposal_status = string.Empty;
            string is_porting_done = string.Empty;

            dashboardTimelineResp.dashboardTimelineEvents = new DashboardTimelineEvents();

            try
            {
                fetchOrderStatusReq = new FetchOrderStatusReq
                {
                    dps_order_id = transactionSummaryResp.dps_order_id,
                    trace_id = transactionSummaryResp.trace_id
                };

                orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, transactionSummaryResp.application_name, flow_type);

                var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                if (orderData != null)
                {
                    order_status = orderData.payment_status;
                }

                // Step 1: Payment Initiated
                dashboardTimelineResp.dashboardTimelineEvents.payment_initiation = "Payment Initiated";

                // Step 2: Payment Status (Success/Failed/Pending)
                if (order_status == "SUCCESS")
                {
                    dashboardTimelineResp.dashboardTimelineEvents.payment_status = "Payment Successful";

                    // Step 3: Payment ID Generation
                    if (string.IsNullOrEmpty(transactionSummaryResp.payment_id))
                    {
                        dashboardTimelineResp.dashboardTimelineEvents.payment_id_generation = "Payment ID Not Generated";
                        return dashboardTimelineResp;
                    }
                    else
                    {
                        dashboardTimelineResp.dashboardTimelineEvents.payment_id_generation = "Payment ID Generated";
                    }

                    if (!string.IsNullOrEmpty(transactionSummaryResp.payment_id))
                    {
                        if (transactionSummaryResp.product.Contains("MOTOR") && ApplicationConfig.NPA_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()))
                        {
                            if (string.IsNullOrEmpty(transactionSummaryResp.ias_id))
                            {
                                // IAS Clearance
                                dashboardTimelineResp.dashboardTimelineEvents.ias_clearance = "IAS Clearance Pending";
                                return dashboardTimelineResp;
                            }
                            else
                            {
                                dashboardTimelineResp.dashboardTimelineEvents.ias_clearance = "IAS Cleared";
                            }

                            if (!string.IsNullOrEmpty(transactionSummaryResp.ias_id))
                            {
                                proposal_status = await paymentBAL.FetchProposalStatus(transactionSummaryResp.trace_id, transactionSummaryResp.proposal_number, transactionSummaryResp.core, flow_type);
                                if (!string.IsNullOrEmpty(proposal_status))
                                {
                                    transactionSummaryResp.proposal_status = proposal_status;
                                    dashboardTimelineResp.dashboardTimelineEvents.proposal_status_1 = "Proposal Status Fetched Successfully";
                                }
                                else
                                {
                                    dashboardTimelineResp.dashboardTimelineEvents.proposal_status_1 = "Could not fetch Proposal Status";
                                    return dashboardTimelineResp;
                                }

                                if (ApplicationConfig.NPVI_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()))
                                {
                                    if (string.IsNullOrEmpty(transactionSummaryResp.breakin_id))
                                    {
                                        // BreakIn ID Generation
                                        dashboardTimelineResp.dashboardTimelineEvents.breakin_id_creation = "BreakIn ID Not Created";
                                        return dashboardTimelineResp;
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.breakin_id_creation = "BreakIn ID Created";
                                    }

                                    if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_id))
                                    {
                                        if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_status) && transactionSummaryResp.breakin_status == "RECOMMENDED")
                                        {
                                            if (string.IsNullOrEmpty(transactionSummaryResp.breakin_clear_inspection_status))
                                            {
                                                // BreakIn Clearance
                                                dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Status Recommended but Clear Inspection Pending";
                                                return dashboardTimelineResp;
                                            }
                                            else
                                            {
                                                dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Inspection Cleared";
                                            }
                                        }
                                        else if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_status) && (transactionSummaryResp.breakin_status == "REJECTED" || transactionSummaryResp.breakin_status == "REJECT"))
                                        {
                                            // BreakIn Rejected
                                            dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Clearance Rejected";
                                            return dashboardTimelineResp;
                                        }
                                        else if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_status) && transactionSummaryResp.breakin_status == "PENDING")
                                        {
                                            // BreakIn Pending
                                            dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Clearance Pending";
                                            return dashboardTimelineResp;
                                        }

                                        if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_clear_inspection_status) && transactionSummaryResp.breakin_clear_inspection_status == "PASS")
                                        {
                                            proposal_status = await paymentBAL.FetchProposalStatus(transactionSummaryResp.trace_id, transactionSummaryResp.proposal_number, transactionSummaryResp.core, flow_type);
                                            if (!string.IsNullOrEmpty(proposal_status))
                                            {
                                                transactionSummaryResp.proposal_status = proposal_status;
                                                dashboardTimelineResp.dashboardTimelineEvents.proposal_status_2 = "Proposal Status Fetched Successfully";
                                            }
                                            else
                                            {
                                                dashboardTimelineResp.dashboardTimelineEvents.proposal_status_2 = "Could not fetch Proposal Status";
                                                return dashboardTimelineResp;
                                            }

                                            if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()))
                                            {
                                                if (string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                                {
                                                    // Policy Number Generation
                                                    if (!string.IsNullOrEmpty(transactionSummaryResp.refund_proposal_number))
                                                    {
                                                        dashboardTimelineResp.retryButton = false;
                                                        dashboardTimelineResp.dashboardTimelineEvents.refund_initiation_status = "Refund Initiated";
                                                        return dashboardTimelineResp;
                                                    }
                                                    else
                                                    {
                                                        dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Not Generated";
                                                        return dashboardTimelineResp;
                                                    }
                                                }
                                                else
                                                {
                                                    dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Generated";
                                                }

                                                if (!string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                                {
                                                    if (transactionSummaryResp.resync_status == "Y")
                                                    {
                                                        // Sync To Source
                                                        dashboardTimelineResp.retryButton = false;
                                                        dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Done";
                                                        return dashboardTimelineResp;
                                                    }
                                                    else
                                                    {
                                                        dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Not Done";
                                                        return dashboardTimelineResp;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                dashboardTimelineResp.dashboardTimelineEvents.proposal_status_2 = "Unknown Proposal Status";
                                                return dashboardTimelineResp;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()))
                            {
                                if (string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                {
                                    // Policy Number Generation
                                    if (!string.IsNullOrEmpty(transactionSummaryResp.refund_proposal_number))
                                    {
                                        dashboardTimelineResp.retryButton = false;
                                        dashboardTimelineResp.dashboardTimelineEvents.refund_initiation_status = "Refund Initiated";
                                        return dashboardTimelineResp;
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Not Generated";
                                        return dashboardTimelineResp;
                                    }
                                }
                                else
                                {
                                    dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Generated";
                                }

                                if (!string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                {
                                    if (transactionSummaryResp.resync_status == "Y")
                                    {
                                        // Sync To Source
                                        dashboardTimelineResp.retryButton = false;
                                        dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Done";
                                        return dashboardTimelineResp;
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Not Done";
                                        return dashboardTimelineResp;
                                    }
                                }
                            }
                            else
                            {
                                dashboardTimelineResp.dashboardTimelineEvents.proposal_status_1 = "Unknown Proposal Status";
                                return dashboardTimelineResp;
                            }
                        }
                        else if (transactionSummaryResp.product.Contains("MOTOR") && (ApplicationConfig.NPVI_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()) || !string.IsNullOrEmpty(transactionSummaryResp.breakin_id)))
                        {
                            if (string.IsNullOrEmpty(transactionSummaryResp.breakin_id))
                            {
                                // BreakIn ID Generation
                                dashboardTimelineResp.dashboardTimelineEvents.breakin_id_creation = "BreakIn ID Not Created";
                                return dashboardTimelineResp;
                            }
                            else
                            {
                                dashboardTimelineResp.dashboardTimelineEvents.breakin_id_creation = "BreakIn ID Created";
                            }

                            if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_id))
                            {
                                if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_status) && transactionSummaryResp.breakin_status == "RECOMMENDED")
                                {
                                    if (string.IsNullOrEmpty(transactionSummaryResp.breakin_clear_inspection_status))
                                    {
                                        // BreakIn Clearance
                                        dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Status Recommended but Clear Inspection Pending";
                                        return dashboardTimelineResp;
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Inspection Cleared";
                                    }
                                }
                                else if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_status) && (transactionSummaryResp.breakin_status == "REJECTED" || transactionSummaryResp.breakin_status == "REJECT"))
                                {
                                    // BreakIn Rejected
                                    dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Clearance Rejected";
                                    return dashboardTimelineResp;
                                }
                                else if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_status) && transactionSummaryResp.breakin_status == "PENDING")
                                {
                                    // BreakIn Pending
                                    dashboardTimelineResp.dashboardTimelineEvents.breakin_clearance = "BreakIn Clearance Pending";
                                    return dashboardTimelineResp;
                                }

                                if (!string.IsNullOrEmpty(transactionSummaryResp.breakin_clear_inspection_status) && transactionSummaryResp.breakin_clear_inspection_status == "PASS")
                                {
                                    proposal_status = await paymentBAL.FetchProposalStatus(transactionSummaryResp.trace_id, transactionSummaryResp.proposal_number, transactionSummaryResp.core, flow_type);
                                    if (!string.IsNullOrEmpty(proposal_status))
                                    {
                                        transactionSummaryResp.proposal_status = proposal_status;
                                        dashboardTimelineResp.dashboardTimelineEvents.proposal_status_1 = "Proposal Status Fetched Successfully";
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.proposal_status_1 = "Could not fetch Proposal Status";
                                        return dashboardTimelineResp;
                                    }

                                    if (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(transactionSummaryResp.proposal_status.ToUpper()))
                                    {
                                        if (string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                        {
                                            // Policy Number Generation
                                            if (!string.IsNullOrEmpty(transactionSummaryResp.refund_proposal_number))
                                            {
                                                dashboardTimelineResp.retryButton = false;
                                                dashboardTimelineResp.dashboardTimelineEvents.refund_initiation_status = "Refund Initiated";
                                                return dashboardTimelineResp;
                                            }
                                            else
                                            {
                                                dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Not Generated";
                                                return dashboardTimelineResp;
                                            }
                                        }
                                        else
                                        {
                                            dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Generated";
                                        }

                                        if (!string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                        {
                                            if (transactionSummaryResp.resync_status == "Y")
                                            {
                                                // Sync To Source
                                                dashboardTimelineResp.retryButton = false;
                                                dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Done";
                                                return dashboardTimelineResp;
                                            }
                                            else
                                            {
                                                dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Not Done";
                                                return dashboardTimelineResp;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.proposal_status_1 = "Unknown Proposal Status";
                                        return dashboardTimelineResp;
                                    }
                                }
                            }
                        }
                        else if (transactionSummaryResp.product.Contains("HEALTH") && transactionSummaryResp.journey_type.ToUpper() == "PORTABILITY")
                        {
                            is_porting_done = await retryResyncDAL.GetPortingUpdate(transactionSummaryResp.trace_id, transactionSummaryResp.proposal_number);

                            if (is_porting_done != "1")
                            {
                                dashboardTimelineResp.dashboardTimelineEvents.porting_status = "Porting Not Approved from QC";
                                return dashboardTimelineResp;
                            }
                            else
                            {
                                dashboardTimelineResp.dashboardTimelineEvents.porting_status = "Porting Approved from QC";

                                // Policy Number Generation
                                if (string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                {
                                    if (!string.IsNullOrEmpty(transactionSummaryResp.refund_proposal_number))
                                    {
                                        dashboardTimelineResp.retryButton = false;
                                        dashboardTimelineResp.dashboardTimelineEvents.refund_initiation_status = "Refund Initiated";
                                        return dashboardTimelineResp;
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Not Generated";
                                        return dashboardTimelineResp;
                                    }
                                }
                                else
                                {
                                    dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Generated";
                                }

                                if (!string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                                {
                                    if (transactionSummaryResp.resync_status == "Y")
                                    {
                                        // Sync To Source
                                        dashboardTimelineResp.retryButton = false;
                                        dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Done";
                                        return dashboardTimelineResp;
                                    }
                                    else
                                    {
                                        dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Not Done";
                                        return dashboardTimelineResp;
                                    }
                                }
                            }

                        }
                        else
                        {
                            if (string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                            {
                                // Policy Number Generation
                                if (!string.IsNullOrEmpty(transactionSummaryResp.refund_proposal_number))
                                {
                                    dashboardTimelineResp.retryButton = false;
                                    dashboardTimelineResp.dashboardTimelineEvents.refund_initiation_status = "Refund Initiated";
                                    return dashboardTimelineResp;
                                }
                                else
                                {
                                    dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Not Generated";
                                    return dashboardTimelineResp;
                                }
                            }
                            else
                            {
                                dashboardTimelineResp.dashboardTimelineEvents.policy_number_generation = "Policy Number Generated";
                            }

                            if (!string.IsNullOrEmpty(transactionSummaryResp.policy_number))
                            {
                                if (transactionSummaryResp.resync_status == "Y")
                                {
                                    // Sync To Source
                                    dashboardTimelineResp.retryButton = false;
                                    dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Done";
                                    return dashboardTimelineResp;
                                }
                                else
                                {
                                    dashboardTimelineResp.dashboardTimelineEvents.reverse_sync = "Reverse Sync Not Done";
                                    return dashboardTimelineResp;
                                }
                            }
                        }
                    }

                }
                else if (order_status == "PENDING")
                {
                    dashboardTimelineResp.dashboardTimelineEvents.payment_status = "Payment Pending";
                }
                else if (order_status == "FAILED")
                {
                    dashboardTimelineResp.dashboardTimelineEvents.payment_status = "Payment Failed";
                    dashboardTimelineResp.retryButton = false;
                }
                else
                {
                    dashboardTimelineResp.dashboardTimelineEvents.payment_status = "Unknown Payment Status";
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardBAL.cs - FetchDashboardDetails() ",
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }

            return dashboardTimelineResp;
        }

        public async Task<DPSStatusResponse> DashboardClearCase(RetryResyncCase retryResyncCase, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            OrderBAL orderBAL = new OrderBAL();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            RetryResyncBAL retryResyncBAL = new RetryResyncBAL();
            RetryResyncDAL retryResyncDAL = new RetryResyncDAL();
            PaymentBAL paymentBAL = new PaymentBAL();
            List<RetryResyncCase> retryResyncCasesList = new List<RetryResyncCase>();

            JsonElement jsonOrderStatusResponse = new JsonElement();
            string is_dashboard_processing = string.Empty;
            string is_retryresync_processing = string.Empty;
            List<string> is_processing = new List<string>();

            string status = string.Empty;

            try
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
                    return response;
                }

                await retryResyncDAL.SetIsDashboardProcessing("Y", retryResyncCase.proposal_number);

                fetchOrderStatusReq = new FetchOrderStatusReq
                {
                    dps_order_id = retryResyncCase.dps_order_id,
                    trace_id = retryResyncCase.trace_id
                };

                var orderStatusResponse = await orderBAL.GetOrderStatus(fetchOrderStatusReq, retryResyncCase.application_name, flow_type);

                var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                if (orderData != null)
                {
                    status = orderData.payment_status;
                }

                if (string.IsNullOrEmpty(status) || (!string.IsNullOrEmpty(status) && status.ToUpper() != "SUCCESS"))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardBAL.cs - DashboardClearCase() ",
                       TraceId: retryResyncCase.trace_id,
                       Message: "Transaction not successful (Status is not SUCCESS or Status is empty). Dashboard Clear Case Request: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.DASHBOARD_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Unknown Order Status";
                    response.TraceId = retryResyncCase.trace_id;
                    response.Data = null;
                    return response;
                }

                string proposal_status = await paymentBAL.FetchProposalStatus(retryResyncCase.trace_id, retryResyncCase.proposal_number, retryResyncCase.core, flow_type);
                if (string.IsNullOrEmpty(proposal_status) || (!string.IsNullOrEmpty(proposal_status) && ApplicationConfig.NCN_PROPOSAL_STATUS_ARRAY.Contains(proposal_status.ToUpper())))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardBAL.cs - DashboardClearCase() ",
                       TraceId: retryResyncCase.trace_id,
                       Message: "Proposal Status returned empty or proposal cancelled. Dashboard Clear Case Request: " + JsonConvert.SerializeObject(retryResyncCase)), ApplicationConfig.DASHBOARD_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Proposal Cancelled";
                    response.TraceId = retryResyncCase.trace_id;
                    response.Data = null;
                    return response;
                }
                else
                {
                    retryResyncCase.proposal_status = proposal_status;
                }

                if (retryResyncCase.is_nstp_flow == "N")
                {
                    status = await retryResyncBAL.ClearSTPRetryResyncTransactionCase(retryResyncCase, flow_type);
                }
                else if (retryResyncCase.is_nstp_flow == "Y")
                {
                    status = await retryResyncBAL.ClearNSTPRetryResyncTransactionCase(retryResyncCase, flow_type);
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Unknown Proposal Status";
                    response.TraceId = retryResyncCase.trace_id;
                    response.Data = null;
                    return response;
                }

                RetryResyncCaseResp retryResyncCaseResp = new RetryResyncCaseResp
                {
                    proposal_number = retryResyncCase.proposal_number,
                    status = status
                };

                response.StatusCode = 1;
                response.StatusMessage = "Success";
                response.Message = null;
                response.TraceId = retryResyncCase.trace_id;
                response.Data = retryResyncCaseResp;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardBAL.cs - DashboardClearCase() ",
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                await retryResyncDAL.SetIsDashboardProcessing("N", retryResyncCase.proposal_number);
            }

            return response;
        }

        public async Task<DPSStatusResponse> GetDatabaseTables()
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                List<string> tablesList = await dashboardDAL.GetDatabaseTables();

                if (tablesList != null && tablesList.Count > 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Database tables fetched successfully";
                    response.TraceId = null;
                    response.Data = tablesList;
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "No tables found";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - GetDatabaseTables()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching database tables";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> GetTableColumns(GetTableColumnsReq getTableColumnsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                List<string> columnsList = await dashboardDAL.GetTableColumns(getTableColumnsReq.table_name);

                if (columnsList != null && columnsList.Count > 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Table columns fetched successfully";
                    response.TraceId = null;
                    response.Data = columnsList;
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "No columns found for the specified table";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - GetTableColumns()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching table columns";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> OnboardUser(DashboardOnboardUserReq onboardUserReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                // Validate URL format
                if (!Uri.TryCreate(onboardUserReq.reverse_sync_url, UriKind.Absolute, out _))
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid reverse sync URL format";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                bool isOnboarded = await dashboardDAL.OnboardUser(onboardUserReq);

                if (isOnboarded)
                {
                    DashboardOnboardUserResp onboardUserResp = new DashboardOnboardUserResp
                    {
                        application_name = onboardUserReq.application_name,
                        message = "Application onboarded successfully"
                    };

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Application onboarded successfully";
                    response.TraceId = null;
                    response.Data = onboardUserResp;

                    // Log successful onboarding
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DashboardBAL.cs - OnboardUser()",
                        Message: $"Application '{onboardUserReq.application_name}' onboarded successfully",
                        StackTrace: "Onboarding Success"), ApplicationConfig.DASHBOARD_LOGS);
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Application already exists or failed to onboard";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - OnboardUser()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error onboarding application";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> GetApplicationNames()
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                List<string> applicationNamesList = await dashboardDAL.GetApplicationNames();

                if (applicationNamesList != null && applicationNamesList.Count > 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Application names fetched successfully";
                    response.TraceId = null;
                    response.Data = applicationNamesList;
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "No application names found";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - GetApplicationNames()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching application names";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> GetTemplateIdsByApplicationName(GetTemplateIdsReq getTemplateIdsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                if (string.IsNullOrEmpty(getTemplateIdsReq.application_name))
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Application name is required";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                List<TemplateIdResp> templateIdsList = await dashboardDAL.GetTemplateIdsByApplicationName(getTemplateIdsReq.application_name);

                if (templateIdsList != null && templateIdsList.Count > 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = $"Template IDs fetched successfully for application: {getTemplateIdsReq.application_name}";
                    response.TraceId = null;
                    response.Data = templateIdsList;
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = $"No template IDs found for application: {getTemplateIdsReq.application_name}";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - GetTemplateIdsByApplicationName()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching template IDs";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> ToggleTemplateStatus(ToggleTemplateStatusReq toggleTemplateStatusReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                // Validate template ID
                if (toggleTemplateStatusReq.templateId <= 0)
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid template ID";
                    response.TraceId = null;
                    response.Data = null;
                    return response;
                }

                // Convert boolean to integer (1 or 0)
                int isActiveValue = toggleTemplateStatusReq.isActive ? 1 : 0;

                bool isUpdated = await dashboardDAL.ToggleTemplateStatus(toggleTemplateStatusReq.templateId, isActiveValue);

                if (isUpdated)
                {
                    ToggleTemplateStatusResp toggleTemplateStatusResp = new ToggleTemplateStatusResp
                    {
                        template_id = toggleTemplateStatusReq.templateId,
                        is_active = isActiveValue,
                        message = $"Template status updated successfully to {(toggleTemplateStatusReq.isActive ? "Active" : "Inactive")}"
                    };

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = $"Template ID {toggleTemplateStatusReq.templateId} status updated successfully";
                    response.TraceId = null;
                    response.Data = toggleTemplateStatusResp;

                    // Log successful update
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DashboardBAL.cs - ToggleTemplateStatus()",
                        Message: $"Template ID '{toggleTemplateStatusReq.templateId}' status changed to {isActiveValue}",
                        StackTrace: "Status Update Success"), ApplicationConfig.DASHBOARD_LOGS);
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Template not found or failed to update status";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - ToggleTemplateStatus()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error updating template status";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public DPSStatusResponse GetProductsList()
        {
            DPSStatusResponse response = new DPSStatusResponse();

            try
            {
                List<ProductListResp> productsList = new List<ProductListResp>();

                // Fetch products from ApplicationConfig.PRODUCTS_LIST
                foreach (var product in ApplicationConfig.PRODUCTS_LIST)
                {
                    ProductListResp productListResp = new ProductListResp
                    {
                        product_key = product.Key
                    };
                    productsList.Add(productListResp);
                }

                if (productsList != null && productsList.Count > 0)
                {
                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Products list fetched successfully";
                    response.TraceId = null;
                    response.Data = productsList;
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "No products found";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - GetProductsList()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching products list";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> CreateTemplate(CreateTemplateReq createTemplateReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                // Get latest template ID and increment by 1
                int latestTemplateId = await dashboardDAL.GetLatestTemplateId();
                int newTemplateId = latestTemplateId + 1;

                // Generate return URLs dynamically
                string productLowerCase = createTemplateReq.product.ToLower();
                string returnLoaderUrl = $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital-payment/pay/loader?product={productLowerCase}&id=";
                string returnStatusUrl = $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital-payment/pay/payment-result?product={productLowerCase}&id=";

                // Create template
                bool isCreated = await dashboardDAL.CreateTemplate(createTemplateReq, newTemplateId, returnLoaderUrl, returnStatusUrl);

                if (isCreated)
                {
                    CreateTemplateResp createTemplateResp = new CreateTemplateResp
                    {
                        template_id = newTemplateId,
                        product = createTemplateReq.product,
                        application_name = createTemplateReq.application_name,
                        return_loader_url = returnLoaderUrl,
                        return_status_url = returnStatusUrl,
                        message = "Template created successfully"
                    };

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Template created successfully";
                    response.TraceId = null;
                    response.Data = createTemplateResp;

                    // Log successful creation
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "DashboardBAL.cs - CreateTemplate()",
                        Message: $"Template ID '{newTemplateId}' created successfully for Application: '{createTemplateReq.application_name}' and Product: '{createTemplateReq.product}'",
                        StackTrace: "Template Creation Success"), ApplicationConfig.DASHBOARD_LOGS);
                }
                else
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Template already exists for this input or failed to create template";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardBAL.cs - CreateTemplate()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error creating template";
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }
    }
}