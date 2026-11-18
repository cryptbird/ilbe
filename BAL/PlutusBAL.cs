using Amazon.SecretsManager.Model;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Text.Json;
using System.Text.Json.Nodes;
using static DigitalPaymentSystem_ILTC.Models.GAEventsModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PlutusModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class PlutusBAL
    {
        private readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<string> GetPlutusAuthenticationToken(string traceID, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusTokenReq plutusTokenReq = new PlutusTokenReq();

            string token = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                plutusTokenReq = new PlutusTokenReq()
                {
                    UserName = ApplicationConfig.PLUTUS_AUTH_TOKEN_USERNAME,
                    SecretKey = ApplicationConfig.PLUTUS_AUTH_TOKEN_SECRET_KEY
                };

                string URL = ApplicationConfig.PLUTUS_AUTH_TOKEN_API_URL;
                var client = new RestClient();
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(plutusTokenReq), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(traceID, "", JsonConvert.SerializeObject(request), response.Content, URL, "PLUTUS TOKEN API", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    PlutusTokenResp plutusTokenResp = JsonConvert.DeserializeObject<PlutusTokenResp>(response.Content);
                    if (plutusTokenResp.statusCode == 200 && plutusTokenResp.isSuccess)
                    {
                        token = plutusTokenResp.data.accessToken;
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusBAL.cs - GetPlutusAuthenticationToken() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }
            return token;
        }

        public async Task<PlutusAddPaymentResponse> PlutusAddPaymentRequest(AddPaymentReq addPaymentReq, string? trace_id, string? transaction_id, string? redirectURL, string? dps_order_id, string flow_type)
        {
            PlutusAddPaymentRequest plutusAddPaymentRequest = new PlutusAddPaymentRequest();
            PlutusAddPaymentResponse plutusAddPaymentResponse = new PlutusAddPaymentResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusDAL plutusDAL = new PlutusDAL();
            string token = string.Empty;
            List<string> onlinePaymentFilters = new List<string>();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                if (!string.IsNullOrEmpty(addPaymentReq.Core) && addPaymentReq.Core.ToUpper() == "PF")
                {
                    plutusAddPaymentRequest.CoreType = Convert.ToInt32(Core_Type.PF);
                }
                else
                {
                    plutusAddPaymentRequest.CoreType = Convert.ToInt32(Core_Type.Artemis);
                }

                onlinePaymentFilters = await plutusDAL.GetPlutusTemplateDetails(addPaymentReq);

                plutusAddPaymentRequest = new PlutusAddPaymentRequest
                {
                    CorrelationId = trace_id,
                    Email = addPaymentReq.Email,
                    ContactNo = addPaymentReq.MobileNumber,
                    Amount = Convert.ToInt32(Convert.ToDecimal(addPaymentReq.TotalAmount)),
                    Name = addPaymentReq.Name,
                    PaymentAction = Convert.ToInt32(Payment_Action.Payment_Collection),
                    IsRedirectRequired = Convert.ToBoolean(ApplicationConfig.PLUTUS_IS_REDIRECT_REQUIRED),
                    RedirectUrl = redirectURL,
                    SyncUrl = ApplicationConfig.PLUTUS_WEBHOOK_URL,
                    PayerType = addPaymentReq.PayerType,
                    IntermediaryCode = null,
                    AgentName = null,
                    ProductCode = null,
                    MarketingOfficer = null,
                    BusinessVertical = null,
                    SubVertical = null,
                    OfficeCode = null,
                    Notes = new Notes
                    {
                        udf1 = addPaymentReq.gateway_params?.juspay_udf1,
                        udf2 = addPaymentReq.gateway_params?.juspay_udf2,
                        udf3 = addPaymentReq.gateway_params?.juspay_udf3,
                        udf4 = addPaymentReq.gateway_params?.juspay_udf4,
                        udf5 = addPaymentReq.gateway_params?.juspay_udf5,
                        udf6 = addPaymentReq.gateway_params?.juspay_udf6,
                        udf7 = addPaymentReq.gateway_params?.juspay_udf7,
                        udf8 = addPaymentReq.gateway_params?.juspay_udf8,
                        udf9 = addPaymentReq.gateway_params?.juspay_udf9,
                        udf10 = addPaymentReq.gateway_params?.juspay_udf10,
                        AgoraX_TxnID = transaction_id,
                        AgoraX_OrderID = dps_order_id
                    },
                    PaymentFilters = new Paymentfilters
                    {
                        Online = onlinePaymentFilters,
                        Offline = null,
                        bankFilter = null,
                        AdvancePIDFilter = null
                    },
                    ProposalDetails = null,
                    BimaPayInfo = null,
                    IsDirectPayment = false,
                    IsAdvancePayment = false,
                    AgentUid = null
                };             

                if(addPaymentReq.JourneyType.ToUpper() == "ASBA")
                {
                    var endDate = DateTime.Now.AddDays(Convert.ToInt32(ApplicationConfig.DEFAULT_MANDATE_PERIOD_IN_DAYS));

                    plutusAddPaymentRequest.mandateDetails = new PlutusMandateDetails 
                    { 
                        frequency = ApplicationConfig.JUSPAY_MANDATE_FREQUENCY,
                        mandateMaxLimit = addPaymentReq.TotalAmount,
                        mandateExpiry = ((DateTimeOffset)endDate).ToUnixTimeSeconds().ToString(),
                        mandateType = "ASBA",
                        plutusMandateId = null
                    };
                }

                if (addPaymentReq.JourneyType.ToUpper() == "AUTOMANDATE")
                {
                    var endDate = DateTime.Now.AddYears(Convert.ToInt32(ApplicationConfig.DEFAULT_AUTOMANDATE_PERIOD_IN_YEARS));

                    plutusAddPaymentRequest.mandateDetails = new PlutusMandateDetails
                    {
                        frequency = ApplicationConfig.AUTOMANDATE_FREQUENCY,
                        mandateMaxLimit = Convert.ToString(1.3 * Convert.ToDouble(addPaymentReq.TotalAmount)),
                        mandateExpiry = ((DateTimeOffset)endDate).ToUnixTimeSeconds().ToString(),
                        mandateType = "RENEWAL",
                        plutusMandateId = null
                    };
                }

                token = await GetPlutusAuthenticationToken(trace_id, flow_type);
                string URL = ApplicationConfig.PLUTUS_ADD_PAYMENT_REQ_API_URL;
                var client = new RestClient();
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(plutusAddPaymentRequest), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(request), response.Content, URL, "PLUTUS ADD PAYMENT REQUEST API", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    plutusAddPaymentResponse = JsonConvert.DeserializeObject<PlutusAddPaymentResponse>(response.Content);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusBAL.cs - PlutusAddPaymentRequest()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }
            return plutusAddPaymentResponse;
        }

        public async Task<PlutusAddPaymentResponse> PlutusAddPaymentRequestv2(AddPaymentReqv2 addPaymentReq, string? trace_id, string? transaction_id, string? redirectURL, List<FetchProposalDetailsResp> fetchProposalDetailsRespList, string flow_type)
        {
            PlutusAddPaymentRequest plutusAddPaymentRequest = new PlutusAddPaymentRequest();
            PlutusAddPaymentResponse plutusAddPaymentResponse = new PlutusAddPaymentResponse();
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusDAL plutusDAL = new PlutusDAL();
            string token = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                plutusAddPaymentRequest = new PlutusAddPaymentRequest
                {
                    CorrelationId = trace_id,
                    Email = fetchProposalDetailsRespList[0].CustomerDetails?.CustomerEmail,
                    ContactNo = fetchProposalDetailsRespList[0].CustomerDetails?.CustomerMobile,
                    Amount = Convert.ToInt32(Convert.ToDecimal(addPaymentReq.totalAmount)),
                    Name = fetchProposalDetailsRespList[0].CustomerDetails?.CustomerName,
                    PaymentAction = Convert.ToInt32(Payment_Action.Payment_Collection),
                    IsRedirectRequired = Convert.ToBoolean(ApplicationConfig.PLUTUS_IS_REDIRECT_REQUIRED),
                    RedirectUrl = redirectURL,
                    SyncUrl = ApplicationConfig.PLUTUS_WEBHOOK_URL,
                    PayerType = addPaymentReq.payerType,
                    IntermediaryCode = null,
                    AgentName = null,
                    ProductCode = null,
                    MarketingOfficer = null,
                    BusinessVertical = null,
                    SubVertical = null,
                    OfficeCode = null,
                    Notes = new Notes
                    {
                        //udf1 = addPaymentReq.gateway_params?.juspay_udf1,
                        //udf2 = addPaymentReq.gateway_params?.juspay_udf2,
                        //udf3 = addPaymentReq.gateway_params?.juspay_udf3,
                        //udf4 = addPaymentReq.gateway_params?.juspay_udf4,
                        //udf5 = addPaymentReq.gateway_params?.juspay_udf5,
                        //udf6 = addPaymentReq.gateway_params?.juspay_udf6,
                        //udf7 = addPaymentReq.gateway_params?.juspay_udf7,
                        //udf8 = addPaymentReq.gateway_params?.juspay_udf8,
                        //udf9 = addPaymentReq.gateway_params?.juspay_udf9,
                        //udf10 = addPaymentReq.gateway_params?.juspay_udf10,
                        AgoraX_TxnID = transaction_id,
                        AgoraX_OrderID = transaction_id
                    },
                    PaymentFilters = BuildPaymentFilters(addPaymentReq.paymentFilters),
                    ProposalDetails = null,
                    BimaPayInfo = null,
                    IsDirectPayment = false,
                    IsAdvancePayment = false,
                    AgentUid = null
                };

                if (addPaymentReq.journeyType.ToUpper() == "ASBA")
                {
                    var endDate = DateTime.Now.AddDays(Convert.ToInt32(ApplicationConfig.DEFAULT_MANDATE_PERIOD_IN_DAYS));

                    plutusAddPaymentRequest.mandateDetails = new PlutusMandateDetails
                    {
                        frequency = ApplicationConfig.JUSPAY_MANDATE_FREQUENCY,
                        mandateMaxLimit = addPaymentReq.totalAmount,
                        mandateExpiry = ((DateTimeOffset)endDate).ToUnixTimeSeconds().ToString(),
                        mandateType = "ASBA",
                        plutusMandateId = null
                    };
                }

                if (addPaymentReq.journeyType.ToUpper() == "AUTOMANDATE")
                {
                    var endDate = DateTime.Now.AddYears(Convert.ToInt32(ApplicationConfig.DEFAULT_AUTOMANDATE_PERIOD_IN_YEARS));

                    plutusAddPaymentRequest.mandateDetails = new PlutusMandateDetails
                    {
                        frequency = ApplicationConfig.AUTOMANDATE_FREQUENCY,
                        mandateMaxLimit = Convert.ToString(1.3 * Convert.ToDouble(addPaymentReq.totalAmount)),
                        mandateExpiry = ((DateTimeOffset)endDate).ToUnixTimeSeconds().ToString(),
                        mandateType = "RENEWAL",
                        plutusMandateId = null
                    };
                }

                token = await GetPlutusAuthenticationToken(trace_id, flow_type);
                string URL = ApplicationConfig.PLUTUS_ADD_PAYMENT_REQ_API_URL;
                var client = new RestClient();
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(plutusAddPaymentRequest), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(request), response.Content, URL, "PLUTUS ADD PAYMENT REQUEST API V2", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    plutusAddPaymentResponse = JsonConvert.DeserializeObject<PlutusAddPaymentResponse>(response.Content);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusBAL.cs - PlutusAddPaymentRequest()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }
            return plutusAddPaymentResponse;
        }

        public async Task<FetchOrderStatusResp> PlutusOrderStatus(PlutusOrderStatusReq plutusOrderStatusReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusOrderStatusRequest plutusOrderStatusRequest = new PlutusOrderStatusRequest();
            PlutusOrderStatusResponse plutusOrderStatusResponse = new PlutusOrderStatusResponse();
            FetchOrderStatusResp fetchOrderStatusResp = new FetchOrderStatusResp();

            string token = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                token = await GetPlutusAuthenticationToken(plutusOrderStatusReq.trace_id, flow_type);

                plutusOrderStatusRequest = new PlutusOrderStatusRequest()
                {
                    TransactionID = plutusOrderStatusReq.order_id
                };

                string URL = ApplicationConfig.PLUTUS_ORDER_STATUS_API_URL;
                var client = new RestClient();
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(plutusOrderStatusRequest), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse response = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(plutusOrderStatusReq.trace_id, "", JsonConvert.SerializeObject(plutusOrderStatusRequest), response.Content, URL, "PLUTUS ORDER STATUS API", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    plutusOrderStatusResponse = JsonConvert.DeserializeObject<PlutusOrderStatusResponse>(response.Content);

                    fetchOrderStatusResp = new FetchOrderStatusResp()
                    {
                        order_id = plutusOrderStatusReq.order_id,
                        auth_code = plutusOrderStatusResponse.data?.authCode,
                        status = plutus_payment_status_map.ContainsKey(plutusOrderStatusResponse.data.paymentStatus) ? plutus_payment_status_map[plutusOrderStatusResponse.data.paymentStatus] : "",
                        mandate_id = plutusOrderStatusResponse.data?.mandateDetails?.plutusMandateId,
                        payment_status = plutus_payment_status_map.ContainsKey(plutusOrderStatusResponse.data.paymentStatus) ? plutus_payment_status_map[plutusOrderStatusResponse.data.paymentStatus] : "",
                        payment_received_on = plutusOrderStatusResponse.timestamp,
                        transaction_id = plutusOrderStatusReq.transaction_id,
                        trace_id = plutusOrderStatusReq.trace_id,
                        dps_order_id = plutusOrderStatusReq.dps_order_id,
                        amount = 0,
                        amount_refunded = 0,
                        refunds = null,
                        payment_mode = plutusOrderStatusResponse.data?.payment_mode
                    };

                    fetchOrderStatusResp.payment_status = fetchOrderStatusResp.payment_status.ToUpper();
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusBAL.cs - PlutusOrderStatus()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }
            return fetchOrderStatusResp;
        }

        public async Task ExtractPlutusOrderDataViaCallbackWebhook(JsonElement plutusCallbackRequest, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            CallbackBAL callbackBAL = new CallbackBAL();           

            // Webhook Details
            string order_id = string.Empty;
            string auth_code = string.Empty;
            string udf9 = string.Empty;
            string trace_id = string.Empty;
            string transaction_id = string.Empty;
            string dps_order_id = string.Empty;
            string sync_event = string.Empty;
            string status = string.Empty;
            string mandate_id = string.Empty;
            string payment_mode = string.Empty;

            try
            {
                if (plutusCallbackRequest.TryGetProperty("TransactionID", out JsonElement TransactionIDElement))
                {
                    order_id = Convert.ToString(TransactionIDElement.GetInt32());
                }
                if (plutusCallbackRequest.TryGetProperty("AuthCode", out JsonElement AuthCodeElement))
                {
                    auth_code = AuthCodeElement.GetString();
                }
                if (plutusCallbackRequest.TryGetProperty("correlationID", out JsonElement correlationIDElement))
                {
                    trace_id = correlationIDElement.GetString();
                }
                if (plutusCallbackRequest.TryGetProperty("Notes", out JsonElement NotesElement))
                {
                    if (NotesElement.TryGetProperty("AgoraX_TxnID", out JsonElement AgoraX_TxnIDElement))
                    {
                        transaction_id = AgoraX_TxnIDElement.GetString();
                    }
                    if (NotesElement.TryGetProperty("AgoraX_OrderID", out JsonElement AgoraX_OrderIDElement))
                    {
                        dps_order_id = AgoraX_OrderIDElement.GetString();
                    }
                }
                if (plutusCallbackRequest.TryGetProperty("SyncEvent", out JsonElement SyncEventElement))
                {
                    sync_event = SyncEventElement.GetString();
                }
                if (plutusCallbackRequest.TryGetProperty("Status", out JsonElement statusElement))
                {
                    status = statusElement.GetString();
                }
                if (plutusCallbackRequest.TryGetProperty("mandateDetails", out JsonElement mandateDetailsElement) && mandateDetailsElement.ValueKind == JsonValueKind.Object && mandateDetailsElement.TryGetProperty("plutusMandateId", out JsonElement mandateIdElement))
                {
                    mandate_id = mandateIdElement.GetString();
                }
                if (plutusCallbackRequest.TryGetProperty("PaymentMode", out JsonElement PaymentModeElement))
                {
                    payment_mode = PaymentModeElement.GetString();
                }

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(plutusCallbackRequest), "", "", "PLUTUS WEBHOOK", "", flow_type, "");

                if (string.IsNullOrEmpty(order_id) || string.IsNullOrEmpty(trace_id) || string.IsNullOrEmpty(transaction_id) || string.IsNullOrEmpty(status))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PlutusBAL.cs - ExtractPlutusOrderDataViaCallbackWebhook() ",
                        TraceId: trace_id,
                        Message: "Invalid webhook details. Callback Request: " + plutusCallbackRequest), ApplicationConfig.PLUTUS_LOGS);

                    return;
                }

                if (status.ToUpper() != "SUCCESS")
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "PlutusBAL.cs - ExtractPlutusOrderDataViaCallbackWebhook() ",
                       TraceId: trace_id,
                       Message: "Transaction not successful (Status is not SUCCESS). Callback Request: " + plutusCallbackRequest), ApplicationConfig.PLUTUS_LOGS);

                    return;
                }
               
                await callbackBAL.CreatePolicyFromCallbackWebhook(plutusCallbackRequest, order_id, auth_code, transaction_id, trace_id, dps_order_id, status, mandate_id, payment_mode, flow_type, "");
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusBAL.cs - ExtractPlutusOrderDataViaCallbackWebhook()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }

            return;
        }

        public async Task<PlutusMandateResp> PlutusMandate(PlutusMandateReq plutusMandateReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            PlutusMandateResp plutusMandateResp = new PlutusMandateResp();
            string token = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                token = await GetPlutusAuthenticationToken(plutusMandateReq.trace_id, flow_type);

                if (string.IsNullOrEmpty(token))
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "PlutusBAL.cs - PlutusMandate()",
                        TraceId: plutusMandateReq.trace_id,
                        Message: "Failed to retrieve Plutus authentication token"), ApplicationConfig.PLUTUS_LOGS);

                    return null;
                }

                var mandateRequestPayload = new
                {
                    plutusMandateId = plutusMandateReq.plutusMandateId,
                    mandateAction = plutusMandateReq.mandateAction,
                    amount = plutusMandateReq.amount ?? "",
                    mandateExecutionTime = plutusMandateReq.mandateExecutionTime,
                    customerId = plutusMandateReq.customerId ?? ""
                };

                string URL = ApplicationConfig.PLUTUS_MANDATE_ACTION_API_URL;
                var client = new RestClient();
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Authorization", "Bearer " + token);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(mandateRequestPayload), ParameterType.RequestBody);

                stopwatch = Stopwatch.StartNew();

                RestResponse apiResponse = await client.ExecuteAsync(request);

                stopwatch.Stop();
                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(plutusMandateReq.trace_id, "", JsonConvert.SerializeObject(mandateRequestPayload), apiResponse.Content, URL, "PLUTUS MANDATE ACTION API", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (apiResponse.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                {
                    plutusMandateResp = JsonConvert.DeserializeObject<PlutusMandateResp>(apiResponse.Content);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusBAL.cs - PlutusMandate()",
                    TraceId: plutusMandateReq.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
            }

            return plutusMandateResp;
        }

        private Paymentfilters BuildPaymentFilters(PaymentFilters model)
        {
            var online = new List<string>();

            // Online payment modes
            if (model.UPI)
                online.Add("UPI");

            if (model.NetBanking)
                online.Add("NB");

            if (model.CreditCard)
                online.Add("CARD");

            if (model.DebitCard)
                online.Add("CARD");

            if (model.Wallet)
                online.Add("WALLET");

            if (model.BimaAsba)
                online.Add("BIMAASBA");

            if (model.CardlessEMI)
                online.Add("CARDLESS_EMI");

            if (model.StandardEMI || model.NoCostEMI)
            {
                online.Add("CREDIT_CARD_EMI");
                online.Add("DEBIT_CARD_EMI");
            }

            if (model.Autopay)
                online.Add("AUTOPAY");

            // EMI Modes
            var emiModes = new List<string>();

            if (model.StandardEMI)
                emiModes.Add("STANDARDEMI");

            if (model.NoCostEMI)
                emiModes.Add("NOCOST");

            return new Paymentfilters
            {
                Online = online.Distinct().ToList(),

                Offline = new List<string>(),

                bankFilter = null,

                brandFilter = null,

                emiFilter = emiModes.Any()
                    ? new emiFilter
                    {
                        CREDIT_CARD_EMI_MODE = emiModes,
                        DEBIT_CARD_EMI_MODE = emiModes
                    }
                    : null,

                AdvancePIDFilter = null
            };
        }
    }
}
