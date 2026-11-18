using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using RestSharp;
using System;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static DigitalPaymentSystem_ILTC.Models.BreakInModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.RefundModel;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace DigitalPaymentSystem_ILTC.Helpers
{

    public class ILESBInterface
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public string CreateBiztalkAuthToken(string Username, string Password)
        {
            try
            {
                var hashedPwd = string.Join("", SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(Password)).Select(s => s.ToString("x2"))).ToLower();
                string sAuthStr = Username + "|" + hashedPwd + "|" + DateTime.Now.ToString("s");

                byte[] bytesToEncode = Encoding.UTF8.GetBytes(sAuthStr);
                return Convert.ToBase64String(bytesToEncode);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILESBInterface.cs - CreateBiztalkAuthToken() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);
                return null;
            }
        }

        public async Task<string> GetILESBToken(string scope, string trace_id, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            ILESBTokenResponseModel model = new ILESBTokenResponseModel();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string URL = ApplicationConfig.ILESB_TOKEN_URL;
                var client = new RestClient(URL);
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("grant_type", ApplicationConfig.ILESB_GRANT_TYPE);
                request.AddParameter("username", ApplicationConfig.ILESB_TOKEN_USERNAME);
                request.AddParameter("password", ApplicationConfig.ILESB_TOKEN_PASSWORD);
                request.AddParameter("scope", scope);
                request.AddParameter("client_id", ApplicationConfig.ILESB_CLIENT_ID);
                request.AddParameter("client_secret", ApplicationConfig.ILESB_CLIENT_SECRET);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(scope), response.Content, URL, "ILESB Token", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (!string.IsNullOrEmpty(response.Content) && response.StatusCode == HttpStatusCode.OK)
                {
                    model = JsonConvert.DeserializeObject<ILESBTokenResponseModel>(response.Content);
                    return model.access_token;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ILESBInterface.cs - GetILESBToken() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);
            }
            return null;
        }

        public async Task<PFPolicyCreationResp> GeneratePFPolicyData(PFPolicyCreationReq requestBody, string flow_type)
        {
            PFPolicyCreationResp paymentResponseModel = new PFPolicyCreationResp();
            PaymentDAL paymentDAL = new PaymentDAL();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string apiUrl = ApplicationConfig.ILESB_PAYMENT_ENTRY_TAGGING_URL;
                var client = new RestClient(apiUrl);
                var request = new RestRequest(apiUrl, Method.Post);

                string token = await GetILESBToken(ApplicationConfig.ILESB_PAYMENT_SCOPE, requestBody.CorrelationId, flow_type);

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddParameter("application/json", JsonConvert.SerializeObject(requestBody), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(requestBody.CorrelationId, "", JsonConvert.SerializeObject(requestBody), apiResponse.Content, apiUrl, "ILESB PAYMENT ENTRY AND TAGGING", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (apiResponse != null && apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                {
                    paymentResponseModel = JsonConvert.DeserializeObject<PFPolicyCreationResp>(apiResponse.Content);
                    return paymentResponseModel;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ILESBInterface.cs - GeneratePFPolicyData() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);
            }

            return null;
        }

        public async Task<string> FetchMotorProposalDetailsFromPF(string token, string proposal_number, string product, string trace_id, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            string URL = string.Empty;

            RestClient client = new RestClient();
            RestRequest request = new RestRequest();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                if (product.ToUpper() == "MOTOR_18" || product.ToUpper() == "MOTOR_4" || product.ToUpper() == "MOTOR_6" || product.ToUpper() == "MOTOR_19" || product.ToUpper() == "MOTOR_20" || product.ToUpper() == "MOTOR_21" || product.ToUpper() == "MOTOR_5" || product.ToUpper() == "MOTOR_13")
                {
                    URL = ApplicationConfig.CAR_FETCH_PROPOSAL_URL;
                    client = new RestClient(URL);
                    request = new RestRequest(URL, Method.Get);
                    request.AddParameter("ProposalNo", proposal_number);
                    request.AddParameter("ProductCode", "2311");
                }
                else if (product.ToUpper() == "MOTOR_14" || product.ToUpper() == "MOTOR_1" || product.ToUpper() == "MOTOR_3" || product.ToUpper() == "MOTOR_15" || product.ToUpper() == "MOTOR_16" || product.ToUpper() == "MOTOR_17" || product.ToUpper() == "MOTOR_2")
                {
                    URL = ApplicationConfig.TW_FETCH_PROPOSAL_URL;
                    client = new RestClient(URL);
                    request = new RestRequest(URL, Method.Get);
                    request.AddParameter("ProposalID", proposal_number);
                    request.AddParameter("ProductCode", "2312");
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "ILESBInterface.cs - FetchProposalDetailsFromPF() ",
                       TraceId: null,
                       Message: "Product is Invalid."), ApplicationConfig.ILESB_INTERFACE_LOGS);

                    return null;
                }

                request.AddHeader("ILAuthorization", token);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request, Method.Get);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                var requestBodyParameters = request.Parameters.Where(p => p.Type == ParameterType.GetOrPost && p.Name != null).ToDictionary(p => p.Name, p => p.Value);
                var proposalReq = JsonConvert.SerializeObject(requestBodyParameters);

                await paymentDAL.LogIntegrationApi(trace_id, proposal_number, proposalReq, response.Content, URL, "ILESB FETCH MOTOR PROPOSAL DETAILS", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    return response.Content;
                }

                return null;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "ILESBInterface.cs - FetchProposalDetailsFromPF() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);

                return null;
            }
        }

        public async Task<PfCustomerDetails> FetchCustomerDetailsFromPF(string token, string customerId, string trace_id, string flow_type)
        {
            string pfCustomerDetails = string.Empty;
            PfCustomerDetails serializeClass = new PfCustomerDetails();

            try
            {
                if (!string.IsNullOrEmpty(customerId))
                {
                    pfCustomerDetails = await InvokeBiztalkGetServiceJson(trace_id, token, ApplicationConfig.GET_CUSTOMER_DETAILS_URL, customerId, flow_type);

                    if (!string.IsNullOrEmpty(pfCustomerDetails))
                    {
                        serializeClass = JsonConvert.DeserializeObject<PfCustomerDetails>(pfCustomerDetails);
                        if (serializeClass.Status.ToUpper() == "SUCCESS")
                        {
                            return serializeClass;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "ILESBInterface.cs - FetchCustomerDetailsFromPF() ",
                                  TraceId: null,
                                  Message: "Customer Details PF Response Failed. Customer ID: " + customerId), ApplicationConfig.ILESB_INTERFACE_LOGS);

                            return null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "ILESBInterface.cs - FetchCustomerDetailsFromPF() ",
                                  TraceId: null,
                                  Message: "Customer details not found in PF response. Customer ID: " + customerId), ApplicationConfig.ILESB_INTERFACE_LOGS);

                        return null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                  MethodName: "ILESBInterface.cs - FetchCustomerDetailsFromPF() ",
                                  TraceId: null,
                                  Message: "Customer ID in Request is Null or Empty"), ApplicationConfig.ILESB_INTERFACE_LOGS);
                    return null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "ILESBInterface.cs - FetchCustomerDetailsFromPF() ",
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}"), ApplicationConfig.ILESB_INTERFACE_LOGS);
                return null;
            }
        }

        public async Task<string> InvokeBiztalkGetServiceJson(string trace_id, string token, string serviceURL, string param_value, string flow_type)
        {
            string StatusCodeCaptured = string.Empty;
            try
            {
                string output = string.Empty;
                serviceURL = serviceURL + param_value;
                AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);

                Dictionary<string, string> keyValues = new Dictionary<string, string>();
                keyValues.Add("ILAuthorization", token);
                RestResponse resp = await DynamicRestAPIWOBody(trace_id, serviceURL, string.Empty, keyValues, Method.Get, "application/json", flow_type);
                StatusCodeCaptured = !string.IsNullOrEmpty(Convert.ToString(resp.StatusCode)) ? Convert.ToString(resp.StatusCode) : "NA";
                if (resp != null && resp.ContentLength > 0)
                {
                    output = resp.Content;
                }
                string responseDataType = (output != null) ? output.GetType().FullName : "null";

                return output;
            }
            catch (WebException exp)
            {
                try
                {
                    var resp = new StreamReader(exp.Response.GetResponseStream()).ReadToEnd();
                    return string.Empty;
                }
                catch (Exception ex) 
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILESBInterface.cs - InvokeBiztalkGetServiceJson() WebException ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);

                    return string.Empty; 
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILESBInterface.cs - InvokeBiztalkGetServiceJson() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);

                return string.Empty;
            }
        }

        public async Task<RestResponse> DynamicRestAPIWOBody(string trace_id, string serviceURL, string inputJsonXmlRequest, Dictionary<string, string> AddHeader, Method method, string ContentType, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
                var client = new RestClient(serviceURL);
                var request = new RestRequest(serviceURL, method);

                for (int i = 0; i < AddHeader.Count; i++)
                {
                    request.AddHeader(AddHeader.Keys.ElementAt(i), AddHeader.Values.ElementAt(i));
                }

                if (ContentType.Contains("XML", StringComparison.CurrentCultureIgnoreCase))
                {
                    request.AddHeader("Accept", ContentType);
                }
                else
                {
                    request.AddHeader("Content-Type", ContentType);
                }

                request.AddParameter(ContentType, inputJsonXmlRequest, ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(request), response.Content, serviceURL, "ILESB FETCH CUSTOMER DETAILS", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILESBInterface.cs - DynamicRestAPIWOBody() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);

                return null;
            }
        }

        public async Task<IlesbCreateBreakInResp> ILESBCreateBreakIn(string token, string proposal_number, IlesbCreateBreakInReq ilesbCreateBreakInReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                ilesbCreateBreakInReq.CustomerAddress = Regex.Replace(ilesbCreateBreakInReq.CustomerAddress, "[^a-zA-Z0-9 ]", "");

                string breakInUrl = ApplicationConfig.BREAKIN_CREATE_URL;
                var breakInClient = new RestClient(breakInUrl);
                var breakInRequest = new RestRequest(breakInUrl, Method.Post);

                breakInRequest.AddHeader("Authorization", "Bearer " + token);
                breakInRequest.AddHeader("Content-Type", "application/json");

                string breakInBody = JsonConvert.SerializeObject(ilesbCreateBreakInReq);
                breakInRequest.AddParameter("application/json", breakInBody, ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse breakInResponse = await breakInClient.ExecuteAsync(breakInRequest, Method.Post);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(ilesbCreateBreakInReq.CorrelationId, proposal_number, breakInBody, breakInResponse.Content, breakInUrl, "ILESB BREAKIN GENERATE", JsonConvert.SerializeObject(breakInResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (!string.IsNullOrEmpty(breakInResponse.Content) && breakInResponse.StatusCode == HttpStatusCode.OK)
                {
                    IlesbCreateBreakInResp breakInRespModel = JsonConvert.DeserializeObject<IlesbCreateBreakInResp>(breakInResponse.Content);
                    return breakInRespModel;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILESBInterface.cs - ILESBCreateBreakIn() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);
            }
            return null;
        }

        public async Task<bool> CallIASApi(string iasId, string token, string traceid, string proposal_number, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            OrderDAL orderDAL = new OrderDAL();

            IASApiResp iasApiResp = new IASApiResp();
            string requestXML = string.Empty;
            bool isSuccess = false;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string iasApiUrl = ApplicationConfig.IAS_API_URL;
                var client = new RestClient(iasApiUrl);
                var request = new RestRequest { Method = Method.Post };

                requestXML = $@"
                <UWStatusRequest xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                    <CorrelationID>{traceid}</CorrelationID>
                    <ProposalNo>{proposal_number}</ProposalNo>
                    <Channel>{ApplicationConfig.IAS_CHANNEL}</Channel>
                    <UWStatusDetails>
                        <DeviationTab>{ApplicationConfig.IAS_DEVIATION_TAB}</DeviationTab>
                        <IASID>{iasId}</IASID>
                        <ApprovalFlag>{ApplicationConfig.IAS_APPROVAL_FLAG}</ApprovalFlag>
                    </UWStatusDetails>
                </UWStatusRequest>";

                var requestXMLbytes = Encoding.UTF8.GetBytes(requestXML);

                request.AddHeader("ILAuthorization", token);
                request.AddHeader("Content-Type", "application/xml");
                request.AddParameter("application/xml", requestXMLbytes, ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                var apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceid, proposal_number, JsonConvert.SerializeObject(requestXML), apiResponse.Content, iasApiUrl, "IAS API CALL", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Content) && apiResponse.StatusCode == HttpStatusCode.OK)
                {
                    iasApiResp = JsonConvert.DeserializeObject<IASApiResp>(apiResponse.Content);
                    if (iasApiResp.Status.ToUpper() == "SUCCESS")
                    {
                        isSuccess = true;
                        await orderDAL.UpdateIASID(iasId, proposal_number);
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILESBInterface.cs - CallIASApi()",
                    Message: $"Exception occurred: {ex.Message}",
                    TraceId: traceid,
                    StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);
            }

            return isSuccess;
        }

        public async Task<string> CreatePremiumRefundILESBVoucher(string traceId, string proposal_number, string canc_proposal_number, string payer_type, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            RefundDAL refundDAL = new RefundDAL();
            ILESBInterface ilesbInterface = new ILESBInterface();
            string url = string.Empty;
            string ilesbAuthToken = string.Empty;
            string requestXML = string.Empty;
            VoucherGenerationResp voucherGenerationResp = new VoucherGenerationResp();
            string voucher_no = string.Empty;
            string payerTypeValue = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                if (payer_type == "C")
                {
                    payerTypeValue = "Customer";
                }
                else if (payer_type == "I")
                {
                    payerTypeValue = "Intermediary";
                }

                url = ApplicationConfig.VOUCHER_GENERATION_API_URL;

                ilesbAuthToken = ilesbInterface.CreateBiztalkAuthToken(ApplicationConfig.BIZTALK_REFUND_USERNAME, ApplicationConfig.BIZTALK_REFUND_PASSWORD);

                requestXML = $@"
                <PremiumRefundRequest xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://IL.ESB.Payment.PremiumRefund""> 
                    <CorelationID xmlns="""">{traceId}</CorelationID> 
                    <ProposalNo xmlns="""">{canc_proposal_number}</ProposalNo> 
                    <RefundType xmlns="""">2</RefundType> 
                    <RefundMode xmlns=""""></RefundMode> 
                    <CDBGAccountNumber xmlns=""""></CDBGAccountNumber> 
                    <RefundPayableTo xmlns="""">{payerTypeValue}</RefundPayableTo> 
                </PremiumRefundRequest>";

                var requestXMLbytes = Encoding.UTF8.GetBytes(requestXML);

                var client = new RestClient(url);
                var request = new RestRequest { Method = Method.Post };
                request.AddHeader("ILAuthorization", ilesbAuthToken);
                request.AddHeader("Content-Type", "application/xml");
                request.AddParameter("application/xml", requestXMLbytes, ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                var response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, proposal_number, requestXML, response.Content, url, "ILESB PREMIUM REFUND VOUCHER CREATE", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                         MethodName: "ILESBInterface.cs - CreatePremiumRefundILESBVoucher() ",
                         TraceId: traceId,
                         Message: "ILESB PREMIUM REFUND VOUCHER CREATE. Proposal Number: " + proposal_number + " Request: " + requestXML + " Response: " + response.Content + " URL: " + url + " Status Code: " + JsonConvert.SerializeObject(response.StatusCode)), ApplicationConfig.ILESB_INTERFACE_LOGS);

                if (response != null && !string.IsNullOrEmpty(response.Content) && response.StatusCode == HttpStatusCode.OK)
                {
                    voucherGenerationResp = JsonConvert.DeserializeObject<VoucherGenerationResp>(response.Content);
                    if (voucherGenerationResp.Status.ToUpper() == "SUCCESS")
                    {
                        //update voucherno in db
                        voucher_no = voucherGenerationResp.VoucherNo;

                        await refundDAL.UpdateVoucherNumber(traceId, proposal_number, voucherGenerationResp.VoucherNo);
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "RefundBAL.cs - CreateILESBVoucher() ",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace,
                    TraceId: traceId), ApplicationConfig.ILESB_INTERFACE_LOGS);
            }

            return voucher_no;
        }

        public async Task<string> CreatePaymentRefundILESBVoucher(string traceId, string proposal_number, string paymentId, string customerId, string payer_type, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            RefundDAL refundDAL = new RefundDAL();
            ILESBInterface ilesbInterface = new ILESBInterface();
            PaymentRefundVoucherResp paymentRefundVoucherResp = new PaymentRefundVoucherResp();

            string url = string.Empty;
            string ilesbAuthToken = string.Empty;
            string requestXML = string.Empty;
            bool isRefundInitiated = false;
            string payerTypeValue = string.Empty;
            string voucher_no = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                if (payer_type == "C")
                {
                    payerTypeValue = "Customer";
                }
                else if (payer_type == "I")
                {
                    payerTypeValue = "Intermediary";
                }
                url = ApplicationConfig.PAYMENT_REFUND_VOUCHER_API_URL;

                ilesbAuthToken = ilesbInterface.CreateBiztalkAuthToken(
                    ApplicationConfig.BIZTALK_REFUND_USERNAME,
                    ApplicationConfig.BIZTALK_REFUND_PASSWORD
                );

                requestXML = $@"
                <PaymentRefundRequest xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://IL.ESB.Payment.PaymentRefund"">
                    <CorelationId xmlns="""">{traceId}</CorelationId>
                    <PaymentId xmlns="""">{paymentId}</PaymentId>
                    <ILBusinessLocation xmlns="""">{ApplicationConfig.PAYMENT_REFUND_IL_BUSINESS_LOCATION}</ILBusinessLocation>
                    <PayerType xmlns="""">{payerTypeValue}</PayerType>
                    <PayerId xmlns="""">{customerId}</PayerId>
                    <RefundType xmlns="""">Credit Card</RefundType>
                    <RefundMode xmlns="""">Credit Card</RefundMode>
                    <Source xmlns="""">eChannel</Source>
                </PaymentRefundRequest>";

                var requestXMLbytes = Encoding.UTF8.GetBytes(requestXML);

                var client = new RestClient(url);
                var request = new RestRequest { Method = Method.Post };

                request.AddHeader("ILAuthorization", ilesbAuthToken);
                request.AddHeader("Content-Type", "application/xml");
                request.AddParameter("application/xml", requestXMLbytes, ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                var response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, paymentId, requestXML, response.Content, url, "ILESB PAYMENT REFUND", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response != null && response.StatusCode == HttpStatusCode.OK)
                {
                    paymentRefundVoucherResp = JsonConvert.DeserializeObject<PaymentRefundVoucherResp>(response.Content);

                    if (paymentRefundVoucherResp?.Status.ToUpper() == "SUCCESS")
                    {
                        //update voucherno in db
                        voucher_no = paymentRefundVoucherResp.VoucherNo;

                        await refundDAL.UpdateVoucherNumber(traceId, proposal_number, paymentRefundVoucherResp.VoucherNo);
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(
                    objLog.GenericAWSException_CWL(
                        MethodName: "RefundBAL.cs - CreatePaymentRefundILESBVoucher()",
                        Message: $"Exception occurred: {ex.Message}",
                        StackTrace: ex.StackTrace,
                        TraceId: traceId
                    ),
                    ApplicationConfig.ILESB_INTERFACE_LOGS
                );
            }

            return voucher_no;
        }


        public async Task<BreakinClearInspectionStatusRes> ILESBBreakInClearance(string proposal_number, string proposal_start_date, string breakin_id, string deal_id, string breakin_status, string traceId, string inspection_date, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            DateFormatter dateFormatter = new DateFormatter();

            BreakinClearInspectionStatusReq breakinClearInspectionStatusReq = new BreakinClearInspectionStatusReq();
            BreakinClearInspectionStatusRes breakinClearInspectionStatusRes = new BreakinClearInspectionStatusRes();

            string token = string.Empty;
            string formatted_inpsection_date = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                if (breakin_status.ToUpper() == "RECOMMENDED")
                {
                    breakin_status = "Ok";
                }

                bool isValidParsedDate = dateFormatter.TryNormalizeDate(inspection_date, out DateTime parsedDate);
                if (!isValidParsedDate)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "ILESBInterface.cs - ILESBBreakInClearance() ",
                       Message: "Invalid Inspection Date format. Inpsection Date DB Response: " + inspection_date,
                       TraceId: traceId), ApplicationConfig.ILESB_INTERFACE_LOGS);

                    return null;
                }
                formatted_inpsection_date = parsedDate.ToString("dd-MM-yyyy");

                breakinClearInspectionStatusReq = new BreakinClearInspectionStatusReq
                {
                    ReferenceNo = proposal_number,
                    ReferenceDate = formatted_inpsection_date,
                    InspectionId = breakin_id,
                    DealNo = deal_id,
                    InspectionStatus = breakin_status,
                    CorrelationId = traceId
                };

                token = await GetILESBToken(ApplicationConfig.ILESB_MOTOR_SCOPE, traceId, flow_type);

                string syncApiUrl = ApplicationConfig.BREAKIN_PF_SYNC_API_URL;
                var syncClient = new RestClient(syncApiUrl);
                var syncRequest = new RestRequest(syncApiUrl, Method.Post);

                syncRequest.AddHeader("Content-Type", "application/json");
                syncRequest.AddHeader("Authorization", "Bearer " + token);
                syncRequest.AddParameter("application/json", JsonConvert.SerializeObject(breakinClearInspectionStatusReq), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse syncApiResponse = await syncClient.ExecuteAsync(syncRequest);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(breakinClearInspectionStatusReq), syncApiResponse.Content, syncApiUrl, "BREAKIN PF CLEARANCE", JsonConvert.SerializeObject(syncApiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (syncApiResponse != null && !string.IsNullOrEmpty(syncApiResponse.Content) && syncApiResponse.StatusCode == HttpStatusCode.OK)
                {
                    breakinClearInspectionStatusRes = JsonConvert.DeserializeObject<BreakinClearInspectionStatusRes>(syncApiResponse.Content);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "ILESBInterface.cs - ILESBBreakInClearance()",
                    Message: $"Exception occurred: {ex.Message}",
                    TraceId: traceId,
                    StackTrace: ex.StackTrace), ApplicationConfig.ILESB_INTERFACE_LOGS);
            }

            return breakinClearInspectionStatusRes;
        }
    }
}
