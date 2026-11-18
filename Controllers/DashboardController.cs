using DigitalPaymentSystem_ILTC.Attributes;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.AuthenticateModel;
using static DigitalPaymentSystem_ILTC.Models.DashboardModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RetryResyncModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    [Route("dps-agorax/")]
    public class DashboardController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/dashboard/CreateToken")]
        public async Task<ActionResult> CreateDashboardToken([FromHeader] string Email)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();

            try
            {
                bool isValid = await dashboardDAL.IsValidDashboardAuth(Email);
                if (isValid)
                {
                    response = dashboardBAL.CreateDashboardAuthToken(Email);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - CreateDashboardAuthToken() ",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return Ok(response);
        }

        [HttpPost("api/v1/dashboard/LoginSendOtp")]
        [ApiAuthentication]
        public async Task<ActionResult> DashboardLoginSendOtp([FromHeader] string Authorization, [FromBody] DashboardLoginReq dashboardLoginReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(dashboardLoginReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.DashboardLogin(dashboardLoginReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }

            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardController.cs - DashboardLogin() ",
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
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "DashboardLoginSendOtp()", requestString, responseString, "api/v1/dashboard/LoginSendOtp", response.StatusCode);
            }
            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/LoginVerifyOtp")]
        [ApiAuthentication]
        public async Task<ActionResult> DashboardLoginVerifyOtp([FromHeader] string Authorization, [FromBody] DashboardLoginVerifyReq dashboardLoginVerifyReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(dashboardLoginVerifyReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.DashboardLoginVerify(dashboardLoginVerifyReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardController.cs - DashboardLoginVerify() ",
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Invalid OTP";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "DashboardLoginVerifyOtp()", requestString, responseString, "api/v1/dashboard/LoginVerifyOtp", response.StatusCode);
            }
            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/GetTransactionSummary")]
        [ApiAuthentication]
        public async Task<ActionResult> GetDashboardTransactionSummary([FromHeader] string Authorization, [FromBody] TransactionSummaryReq transactionSummaryReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(transactionSummaryReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.GetTransactionSummary(transactionSummaryReq, "DASHBOARD GET TRANSACTION SUMMARY");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "DashboardController.cs - GetDashboardTransactionSummary() ",
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
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "GetDashboardTransactionSummary()", requestString, responseString, "api/v1/dashboard/GetTransactionSummary", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/ClearCase")]
        [ApiAuthentication]
        public async Task<ActionResult> DashboardClearCase([FromHeader] string Authorization, [FromBody] RetryResyncCase retryResyncCase)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            ILESBInterfaceModel ilesbInterfaceModel = new ILESBInterfaceModel();
            PFPolicyCreationReq ilesbCreatePaymentIdResp = new PFPolicyCreationReq();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(retryResyncCase);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.DashboardClearCase(retryResyncCase, "DASHBOARD CLEAR CASE");
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "DashboardController.cs - DashboardClearCase() ",
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
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "DashboardClearCase()", requestString, responseString, "api/v1/dashboard/ClearCase", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/CheckProposalStatus")]
        [ApiAuthentication]
        public async Task<ActionResult> DashboardCheckProposalStatus([FromHeader] string Authorization, [FromBody] DashboardCheckProposalStatusReq dashboardCheckProposalStatusReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            PaymentBAL paymentBAL = new PaymentBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            string proposal_status = string.Empty;

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(dashboardCheckProposalStatusReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    proposal_status = await paymentBAL.FetchProposalStatus(dashboardCheckProposalStatusReq.trace_id, dashboardCheckProposalStatusReq.proposal_number, dashboardCheckProposalStatusReq.core, "DASHBOARD - CHECK PROPOSAL STATUS");
                    if (!string.IsNullOrEmpty(proposal_status))
                    {
                        DashboardCheckProposalStatusResp dashboardCheckProposalStatusResp = new DashboardCheckProposalStatusResp
                        {
                            proposal_number = dashboardCheckProposalStatusReq.proposal_number,
                            proposal_status = proposal_status
                        };

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Proposal Details fetched successfully";
                        response.TraceId = dashboardCheckProposalStatusReq.trace_id;
                        response.Data = dashboardCheckProposalStatusResp;
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unable to fetch Proposal Details";
                        response.TraceId = dashboardCheckProposalStatusReq.trace_id;
                        response.Data = null;
                    }
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "DashboardController.cs - DashboardCheckProposalStatus() ",
                      Message: $"Exception occurred: {ex.Message}",
                      StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Unable to fetch Proposal Details";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "DashboardCheckProposalStatus()", requestString, responseString, "api/v1/dashboard/CheckProposalStatus", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/EncryptData")]
        [ApiAuthentication]
        public async Task<ActionResult> DashboardEncryptData([FromHeader] string Authorization, [FromBody] JsonElement dashboardEncryptDataReq)
        {
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DPSStatusResponse response = new DPSStatusResponse();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            DashboardDAL dashboardDAL = new DashboardDAL();

            string encryptedData = string.Empty;

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(dashboardEncryptDataReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    encryptedData = encryptDecrypt.EncryptRequest(JsonSerializer.Serialize(dashboardEncryptDataReq));
                    if (!string.IsNullOrEmpty(encryptedData))
                    {
                        DashboardEncryptDataResp dashboardEncryptDataResp = new DashboardEncryptDataResp
                        {
                            encryptedData = encryptedData
                        };

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Data encrypted successfully";
                        response.TraceId = null;
                        response.Data = dashboardEncryptDataResp;
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unable to encrypt data";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "DashboardController.cs - DashboardEncryptData() ",
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
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "DashboardEncryptData()", requestString, responseString, "api/v1/dashboard/EncryptData", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/DecryptData")]
        [ApiAuthentication]
        public async Task<ActionResult> DashboardDecryptData([FromHeader] string Authorization, [FromBody] DashboardDecryptDataReq dashboardDecryptDataReq)
        {
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            DPSStatusResponse response = new DPSStatusResponse();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            string decryptedData = string.Empty;
            DashboardDAL dashboardDAL = new DashboardDAL();
            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(dashboardDecryptDataReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    decryptedData = encryptDecrypt.DecryptResponse(dashboardDecryptDataReq.encryptedData);
                    if (!string.IsNullOrEmpty(decryptedData))
                    {
                        DashboardDecryptDataResp dashboardDecryptDataResp = new DashboardDecryptDataResp
                        {
                            decryptedData = decryptedData
                        };

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Data decrypted successfully";
                        response.TraceId = null;
                        response.Data = dashboardDecryptDataResp;
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unable to decrypt data";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                      MethodName: "DashboardController.cs - DashboardDecryptData() ",
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
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "DashboardDecryptData()", requestString, responseString, "api/v1/dashboard/DecryptData", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpGet("api/v1/dashboard/GetDatabaseTables")]
        [ApiAuthentication]
        public async Task<ActionResult> GetDatabaseTables([FromHeader] string Authorization)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.GetDatabaseTables();
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - GetDatabaseTables()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching database tables";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "GetDatabaseTables()", requestString, responseString, "api/v1/dashboard/GetDatabaseTables", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/GetTableColumns")]
        [ApiAuthentication]
        public async Task<ActionResult> GetTableColumns([FromHeader] string Authorization, [FromBody] GetTableColumnsReq getTableColumnsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(getTableColumnsReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.GetTableColumns(getTableColumnsReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - GetTableColumns()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching table columns";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "GetTableColumns()", requestString, responseString, "api/v1/dashboard/GetTableColumns", response.StatusCode);
            }

            return Ok(response);
        }

        [HttpPost("api/v1/dashboard/OnboardUser")]
        [ApiAuthentication]
        public async Task<ActionResult> DashboardOnboardUser([FromHeader] string Authorization, [FromBody] DashboardOnboardUserReq onboardUserReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();

            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(onboardUserReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.OnboardUser(onboardUserReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - DashboardOnboardUser()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error onboarding application";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "DashboardOnboardUser()", requestString, responseString, "api/v1/dashboard/OnboardUser", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpGet("api/v1/dashboard/GetApplicationNames")]
        [ApiAuthentication]
        public async Task<ActionResult> GetApplicationNames([FromHeader] string Authorization)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.GetApplicationNames();
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - GetApplicationNames()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching application names";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "GetApplicationNames()", requestString, responseString, "api/v1/dashboard/GetApplicationNames", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/GetTemplateIds")]
        [ApiAuthentication]
        public async Task<ActionResult> GetTemplateIdsByApplicationName([FromHeader] string Authorization, [FromBody] GetTemplateIdsReq getTemplateIdsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();

            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(getTemplateIdsReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.GetTemplateIdsByApplicationName(getTemplateIdsReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - GetTemplateIdsByApplicationName()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching template IDs";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "GetTemplateIdsByApplicationName()", requestString, responseString, "api/v1/dashboard/GetTemplateIds", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/ChangeTemplateActiveStatus")]
        [ApiAuthentication]
        public async Task<ActionResult> ChangeTemplateActiveStatus([FromHeader] string Authorization, [FromBody] ToggleTemplateStatusReq toggleTemplateStatusReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();

            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(toggleTemplateStatusReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.ToggleTemplateStatus(toggleTemplateStatusReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - ChangeTemplateActiveStatus()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error updating template status";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "ChangeTemplateActiveStatus()", requestString, responseString, "api/v1/dashboard/ChangeTemplateActiveStatus", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpGet("api/v1/dashboard/GetProductsList")]
        [ApiAuthentication]
        public async Task<ActionResult> GetProductsList([FromHeader] string Authorization)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = dashboardBAL.GetProductsList();
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - GetProductsList()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error fetching products list";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "GetProductsList()", requestString, responseString, "api/v1/dashboard/GetProductsList", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/CreateTemplate")]
        [ApiAuthentication]
        public async Task<ActionResult> CreateTemplate([FromHeader] string Authorization, [FromBody] CreateTemplateReq createTemplateReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardBAL dashboardBAL = new DashboardBAL();
            DashboardDAL dashboardDAL = new DashboardDAL();

            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;

            string email = string.Empty;
            try
            {
                requestString = JsonSerializer.Serialize(createTemplateReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    response = await dashboardBAL.CreateTemplate(createTemplateReq);
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - CreateTemplate()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error creating template";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "CreateTemplate()", requestString, responseString, "api/v1/dashboard/CreateTemplate", response.StatusCode);
            }

            return Ok(response);
        }


        [HttpPost("api/v1/dashboard/AuthorizeHangfireAccess")]
        [ApiAuthentication]
        public async Task<ActionResult> AuthorizeHangfireAccess([FromHeader] string Authorization)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();

            string requestString = string.Empty;
            string responseString = string.Empty;
            string email = string.Empty;

            try
            {
                requestString = JsonSerializer.Serialize(new { action = "AuthorizeHangfireAccess" });
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    string role = await dashboardDAL.GetDashboardRole(email);

                    // Only ADMIN users can access Hangfire
                    if (role?.ToUpper() == "ADMIN")
                    {
                        // Set secure cookie for Hangfire authentication
                        string pathBase = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
                            ? "/digital-payment-be/hangfire"
                            : "/dps-agorax/hangfire";

                        Response.Cookies.Append("HangfireAuth", $"{email}|{role}", new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = DateTimeOffset.Now.AddHours(8),
                            Path = pathBase.Replace("/hangfire", ""),
                            Domain = null
                        });

                        string hangfireUrl = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
                            ? "https://www.icicilombard.com/dps-agorax/hangfire"
                            : "https://echannel.insurancearticlez.com/dps-agorax/hangfire";

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = "Hangfire access authorized";
                        response.TraceId = null;
                        response.Data = new { hangfire_url = hangfireUrl };
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Access denied. Admin privileges required.";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - AuthorizeHangfireAccess()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error authorizing Hangfire access";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "AuthorizeHangfireAccess()", requestString, responseString, "api/v1/dashboard/AuthorizeHangfireAccess", response.StatusCode);
            }

            return Ok(response);
        }

        [HttpPost("api/v1/dashboard/ExecuteRefresh")]
        [ApiAuthentication]
        public async Task<ActionResult> ExecuteRefresh([FromHeader] string Authorization, [FromBody] ExecuteRefreshReq executeRefreshReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DashboardDAL dashboardDAL = new DashboardDAL();
            AuthenticateBAL authenticateBAL = new AuthenticateBAL();
            string requestString = string.Empty;
            string responseString = string.Empty;
            string email = string.Empty;
            ExecuteRefreshResp executeRefreshResp;

            try
            {
                requestString = JsonSerializer.Serialize(executeRefreshReq);
                AuthenticateTokenData authenticateTokenData = authenticateBAL.GetDetailsFromAuthToken(Authorization);
                email = authenticateTokenData.unique_name;
                if (!string.IsNullOrEmpty(email))
                {
                    byte[] data = Convert.FromBase64String(executeRefreshReq.cache);
                    executeRefreshReq.cache = Encoding.UTF8.GetString(data);

                    executeRefreshResp = await dashboardDAL.UpdateCache(executeRefreshReq.cache);

                    if (executeRefreshResp != null)
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = $"Query executed successfully. {executeRefreshResp.count} rows returned/updated";
                        response.TraceId = null;
                        response.Data = executeRefreshResp;
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Error executing query";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                else
                {
                    return BadRequest("Invalid User Credentials");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardController.cs - CreateTemplate()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Error creating template";
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                responseString = JsonSerializer.Serialize(response);
                await dashboardDAL.InsertDashboardEventLog(email, "ExecuteDatabaseQuery()", requestString, responseString, "api/v1/dashboard/CoreRefresh", response.StatusCode);
            }
            return Ok(response);
        }

    }
}
