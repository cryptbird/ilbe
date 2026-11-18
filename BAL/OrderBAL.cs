using Amazon.Runtime.Telemetry.Tracing;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using DigitalPaymentSystem_ILTC.Models;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Pkcs;
using RestSharp;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.ApiFactoryInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.DashboardModel;
using static DigitalPaymentSystem_ILTC.Models.ILESBInterfaceModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PlutusModel;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class OrderBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<DPSStatusResponse> Session(SessionReq sessionReq, string application_name, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            OrderDAL orderDAL = new OrderDAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            JuspayCustomerResponse customerData = new JuspayCustomerResponse();
            FetchTxnDetails fetchTxnDetails = new FetchTxnDetails();

            string transactionID = string.Empty;
            string traceID = string.Empty;

            string juspaySessionApiUrl = string.Empty;
            string baseReturnLoaderUrl = string.Empty;
            string fullReturnLoaderUrl = string.Empty;
            string juspayMerchantID = string.Empty;
            string status = string.Empty;
            string order_id = string.Empty;
            string generateDpsOrderId = string.Empty;
            string encryptedDpsOrderId = string.Empty;
            string gateway_customer_id = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string decID = encryptDecrypt.DecryptResponse(sessionReq.ID);
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        juspaySessionApiUrl = ApplicationConfig.JUSPAY_SESSION_API_URL;

                        baseReturnLoaderUrl = ApplicationConfig.RETURN_LOADER_URL;

                        // Check if payment already completed for this transaction
                        bool isPaymentCompleted = await orderDAL.IS_PAYMENT_DONE(transactionID);
                        if (isPaymentCompleted)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - Session() ",
                                TraceId: traceID,
                                Message: "Payment already completed for Transaction ID: " + transactionID), ApplicationConfig.ORDER_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Payment already completed";
                            response.TraceId = traceID;
                            response.Data = null;
                            return response;
                        }

                        if (string.IsNullOrEmpty(baseReturnLoaderUrl))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "OrderBAL.cs - Session() ",
                               TraceId: traceID,
                               Message: "Unable to fetch Return Loader URL for the Given Template ID. Base URL: " + baseReturnLoaderUrl + " Encrypted ID: " + sessionReq.ID + " Transaction ID: " + transactionID + " Trace ID: " + traceID), ApplicationConfig.ORDER_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch Return Loader URL for the Given Template ID";
                            response.TraceId = traceID;
                            response.Data = null;
                            return response;
                        }

                        generateDpsOrderId = await orderDAL.GetNextDPSOrderSequence();
                        encryptedDpsOrderId = encryptDecrypt.EncryptRequest($"{generateDpsOrderId}");

                        fullReturnLoaderUrl = $"{baseReturnLoaderUrl}{generateDpsOrderId}";

                        GatewayParams gatewayParams = await orderDAL.GetGatewayParams(transactionID);

                        juspayMerchantID = await orderDAL.GetJuspayMerchantID(application_name);
                        if (string.IsNullOrEmpty(juspayMerchantID))
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - Session() ",
                                TraceId: traceID,
                                Message: "Unable to fetch Merchant ID for the Given Application. Application Name: " + application_name), ApplicationConfig.ORDER_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch Merchant ID for the Given Application";
                            response.TraceId = traceID;
                            response.Data = null;
                            return response;
                        }

                        fetchTxnDetails = await orderDAL.GetJourneyTypeAndAmount(transactionID);
                        if (fetchTxnDetails != null && !string.IsNullOrEmpty(fetchTxnDetails.journey_type) && !string.IsNullOrEmpty(fetchTxnDetails.amount))
                        {
                            //JuspaySessionReq juspaySessionReq = new JuspaySessionReq
                            //{
                            //    order_id = generateDpsOrderId,
                            //    amount = sessionReq.amount,
                            //    payment_page_client_id = ApplicationConfig.JUSPAY_CLIENT_ID,
                            //    action = ApplicationConfig.JUSPAY_ACTION,
                            //    return_url = fullReturnLoaderUrl,
                            //    udf6 = traceID,
                            //    udf7 = transactionID,
                            //    udf8 = generateDpsOrderId,
                            //    metadata_juspay_gateway_reference_id = ApplicationConfig.JUSPAY_GATEWAY_REFERENCE_ID,
                            //    metadata_webhook_url = ApplicationConfig.WEBHOOK_URL
                            //};
                            var juspaySessionReq = new JuspaySessionReq
                            {
                                order_id = generateDpsOrderId,
                                payment_page_client_id = ApplicationConfig.JUSPAY_CLIENT_ID,
                                action = ApplicationConfig.JUSPAY_ACTION,
                                return_url = fullReturnLoaderUrl,
                                udf1 = ApplicationConfig.JUSPAY_MERCHANT_ID_UDF1,
                                udf2 = gatewayParams.juspay_udf2,
                                udf3 = gatewayParams.juspay_udf3,
                                udf4 = gatewayParams.juspay_udf4,
                                udf5 = gatewayParams.juspay_udf5,
                                udf6 = gatewayParams.juspay_udf6,
                                udf7 = gatewayParams.juspay_udf7,
                                udf8 = gatewayParams.juspay_udf8,
                                udf9 = gatewayParams.juspay_udf9 + "|" + traceID + "|" + transactionID + "|" + generateDpsOrderId,
                                udf10 = gatewayParams.juspay_udf10,
                                metadata_juspay_gateway_reference_id = ApplicationConfig.JUSPAY_GATEWAY_REFERENCE_ID,
                                //metadata_webhook_url = ApplicationConfig.WEBHOOK_URL,
                                // Single webhook with custom headers, triggers only for ORDER_SUCCEEDED
                                metadata_multiWebhookUrlConfigs = new List<WebhookConfig>
                    {
                        new WebhookConfig
                        {
                            webhookUrl = ApplicationConfig.WEBHOOK_URL,
                            enabledEvents = new List<string>(
                                ApplicationConfig.JUSPAY_ORDER_STATUS_WEBHOOK.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            ),
                            customHeaders = new Dictionary<string, string>
                            {
                                { ApplicationConfig.WEBHOOK_HEADER_CONTENT_TYPE, ApplicationConfig.WEBHOOK_HEADER_CONTENT_TYPE_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_USER_AGENT, ApplicationConfig.WEBHOOK_HEADER_USER_AGENT_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_ACCEPT, ApplicationConfig.WEBHOOK_HEADER_ACCEPT_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_ACCEPT_LANGUAGE, ApplicationConfig.WEBHOOK_HEADER_ACCEPT_LANGUAGE_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_ORIGIN, ApplicationConfig.WEBHOOK_HEADER_ORIGIN_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_REFERER, ApplicationConfig.WEBHOOK_HEADER_REFERER_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_SEC_FETCH_SITE, ApplicationConfig.WEBHOOK_HEADER_SEC_FETCH_SITE_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_SEC_FETCH_MODE, ApplicationConfig.WEBHOOK_HEADER_SEC_FETCH_MODE_VALUE },
                                { ApplicationConfig.WEBHOOK_HEADER_SEC_FETCH_DEST, ApplicationConfig.WEBHOOK_HEADER_SEC_FETCH_DEST_VALUE }
                            }
                        }
                    }
                            };

                            if (sessionReq.payment_mode.ToUpper() == "ASBA" && fetchTxnDetails.payment_filters?.BimaAsba == true)
                            {

                                gateway_customer_id = await orderDAL.GetCustomerDetails(fetchTxnDetails.mobile_number);

                                if (gateway_customer_id == null || string.IsNullOrEmpty(gateway_customer_id))
                                {
                                    CreateJuspayCustomerReq customerReq = new CreateJuspayCustomerReq
                                    {
                                        object_reference_id = fetchTxnDetails.mobile_number,
                                        first_name = fetchTxnDetails.name,
                                        email_address = fetchTxnDetails.email,
                                        mobile_number = fetchTxnDetails.mobile_number,
                                        options_get_client_auth_token = false
                                    };

                                    DPSStatusResponse customerResp = await CreateCustomer(customerReq, application_name, "JUSPAY CREATE CUSTOMER API", traceID);

                                    if (customerResp == null || customerResp.StatusCode == 0 || customerResp.Data == null)
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "OrderBAL.cs - Session() ",
                                            TraceId: traceID,
                                            Message: "Failed to create Juspay customer for ASBA mandate. Request: " + JsonConvert.SerializeObject(customerReq) + " Response: " + JsonConvert.SerializeObject(customerResp)), ApplicationConfig.ORDER_LOGS);
                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Failed to create Juspay customer for ASBA mandate";
                                        response.TraceId = traceID;
                                        response.Data = null;
                                        return response;
                                    }

                                    customerData = customerResp?.Data as JuspayCustomerResponse;

                                    gateway_customer_id = customerData.id;
                                }

                                var endDate = DateTime.Now.AddDays(Convert.ToInt32(ApplicationConfig.DEFAULT_MANDATE_PERIOD_IN_DAYS));
                                juspaySessionReq.amount = fetchTxnDetails.amount;
                                juspaySessionReq.options_create_mandate = ApplicationConfig.JUSPAY_OPTIONS_CREATE_ASBA_MANDATE;
                                juspaySessionReq.mandate_start_date = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds().ToString();
                                juspaySessionReq.mandate_end_date = ((DateTimeOffset)endDate).ToUnixTimeSeconds().ToString();
                                juspaySessionReq.mandate_block_funds = Convert.ToBoolean(ApplicationConfig.JUSPAY_MANDATE_BLOCK_FUNDS);
                                juspaySessionReq.mandate_frequency = ApplicationConfig.JUSPAY_MANDATE_FREQUENCY;
                                juspaySessionReq.mandate_revokable_by_customer = Convert.ToBoolean(ApplicationConfig.JUSPAY_MANDATE_REVOKABLE_BY_CUSTOMER);
                                juspaySessionReq.metadata_juspay_gateway_reference_id = ApplicationConfig.JUSPAY_MANDATE_REFERENCE_ID;
                                juspaySessionReq.mandate_max_amount = fetchTxnDetails.amount;
                                juspaySessionReq.customer_id = gateway_customer_id;
                                juspaySessionReq.customer_email = fetchTxnDetails.email;
                                juspaySessionReq.customer_phone = fetchTxnDetails.mobile_number;
                                juspaySessionReq.payment_filter = new PaymentFilter
                                {
                                    allowDefaultOptions = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ALLOW_DEFAULT_OPTIONS),
                                    options = new List<Option>
                                    {
                                        new Option
                                        {
                                            paymentMethodType = "UPI",
                                            enable = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD)
                                        }
                                    }
                                };
                            }
                            else if (fetchTxnDetails.payment_filters?.ILEMI == true && !string.IsNullOrWhiteSpace(fetchTxnDetails.installment_amount) && !string.IsNullOrWhiteSpace(fetchTxnDetails.mandate_max_amount))
                            {

                                gateway_customer_id = await orderDAL.GetCustomerDetails(fetchTxnDetails.mobile_number);

                                if (gateway_customer_id == null || string.IsNullOrEmpty(gateway_customer_id))
                                {
                                    CreateJuspayCustomerReq customerReq = new CreateJuspayCustomerReq
                                    {
                                        object_reference_id = fetchTxnDetails.mobile_number,
                                        first_name = fetchTxnDetails.name,
                                        email_address = fetchTxnDetails.email,
                                        mobile_number = fetchTxnDetails.mobile_number,
                                        options_get_client_auth_token = false
                                    };

                                    DPSStatusResponse customerResp = await CreateCustomer(customerReq, application_name, "JUSPAY CREATE CUSTOMER API", traceID);

                                    if (customerResp == null || customerResp.StatusCode == 0 || customerResp.Data == null)
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "OrderBAL.cs - Session() ",
                                            TraceId: traceID,
                                            Message: "Failed to create Juspay customer for ASBA mandate. Request: " + JsonConvert.SerializeObject(customerReq) + " Response: " + JsonConvert.SerializeObject(customerResp)), ApplicationConfig.ORDER_LOGS);
                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Failed to create Juspay customer for EMI mandate";
                                        response.TraceId = traceID;
                                        response.Data = null;
                                        return response;
                                    }

                                    customerData = customerResp?.Data as JuspayCustomerResponse;

                                    gateway_customer_id = customerData.id;
                                }

                                string endDate = fetchTxnDetails.proposal_tenure > 0 ? ((DateTimeOffset)DateTime.Now).AddYears(fetchTxnDetails.proposal_tenure).AddMonths(6).AddDays(0).ToUnixTimeSeconds().ToString() : ((DateTimeOffset)DateTime.Now).AddYears(1).AddMonths(6).AddDays(0).ToUnixTimeSeconds().ToString();
                                juspaySessionReq.amount = fetchTxnDetails.installment_amount;
                                juspaySessionReq.options_create_mandate = ApplicationConfig.JUSPAY_OPTIONS_CREATE_MANDATE;
                                juspaySessionReq.mandate_start_date = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds().ToString();
                                juspaySessionReq.mandate_end_date = endDate;
                                juspaySessionReq.mandate_block_funds = Convert.ToBoolean(ApplicationConfig.JUSPAY_MANDATE_BLOCK_FUNDS);
                                juspaySessionReq.mandate_frequency = ApplicationConfig.JUSPAY_EMI_MANDATE_FREQUENCY;
                                juspaySessionReq.mandate_revokable_by_customer = Convert.ToBoolean(ApplicationConfig.JUSPAY_MANDATE_REVOKABLE_BY_CUSTOMER);
                                juspaySessionReq.metadata_juspay_gateway_reference_id = ApplicationConfig.JUSPAY_MANDATE_REFERENCE_ID;
                                juspaySessionReq.mandate_max_amount = fetchTxnDetails.mandate_max_amount;
                                juspaySessionReq.udf9 = gatewayParams.juspay_udf9 + "|" + traceID + "|" + transactionID + "|" + generateDpsOrderId + "|" + "ILEMI";
                                juspaySessionReq.customer_id = gateway_customer_id;
                                juspaySessionReq.customer_email = fetchTxnDetails.email;
                                juspaySessionReq.customer_phone = fetchTxnDetails.mobile_number;
                                juspaySessionReq.payment_filter = new()
                                {
                                    allowDefaultOptions = false,
                                };
                                if (fetchTxnDetails.payment_filters.UPI)
                                {
                                    juspaySessionReq.payment_filter.options = new()
                                    {
                                        new()
                                        {
                                            enable = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD),
                                            paymentMethodType = "UPI"
                                        }
                                    };
                                }
                                else if (fetchTxnDetails.payment_filters.CreditCard)
                                {
                                    juspaySessionReq.payment_filter.options = new()
                                        {
                                            new()
                                            {
                                                enable = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD),
                                                paymentMethodType = "CARD",
                                                cardFilters = new List<JuspayCardPaymentFilters>
                                                {
                                                    new JuspayCardPaymentFilters
                                                    {
                                                        enable = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD),
                                                        cardBrands = ["VISA", "MASTERCARD"],
                                                        cardTypes = ["CREDIT"]
                                                    }
                                                }
                                            }
                                        };
                                }
                                else if (fetchTxnDetails.payment_filters.DebitCard)
                                {
                                    juspaySessionReq.payment_filter.options = new()
                                        {
                                            new()
                                            {
                                                enable = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD),
                                                paymentMethodType = "CARD",
                                                cardFilters = new List<JuspayCardPaymentFilters>
                                                {
                                                    new JuspayCardPaymentFilters
                                                    {
                                                        enable = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD),
                                                        cardBrands = ["VISA", "MASTERCARD"],
                                                        cardTypes = ["DEBIT"]
                                                    }
                                                }
                                            }
                                        };
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                     MethodName: "OrderBAL.cs - Session() ",
                                     TraceId: traceID,
                                     Message: "IL EMI must be combined with atleast one of these UPI/CreditCard/DebitCard payment options." + transactionID), ApplicationConfig.ORDER_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "IL EMI must be combined with atleast one of these UPI/CreditCard/DebitCard payment options.";
                                    response.TraceId = traceID;
                                    response.Data = null;
                                    return response;

                                }
                            }
                            else if (sessionReq.payment_mode == "EMI" && fetchTxnDetails.payment_filters?.NoCostEMI == true)
                            {
                                juspaySessionReq.amount = fetchTxnDetails.amount;
                                juspaySessionReq.metadata_juspay_gateway_reference_id = ApplicationConfig.JUSPAY_GATEWAY_REFERENCE_ID;
                                juspaySessionReq.payment_filter = new()
                                {
                                    allowDefaultOptions = true,
                                    emiOptions = new()
                                    {
                                        standardEmi = new JuspayPaymentEmi { enable = false },
                                        lowCostEmi = new JuspayPaymentEmi { enable = false },
                                        noCostEmi = new JuspayPaymentEmi { enable = true, credit = new() { enable = true }, cardless = new() { enable = true } },
                                        showOnlyEmi = true
                                    },
                                };
                            }
                            else if (sessionReq.payment_mode == "EMI" && fetchTxnDetails.payment_filters?.StandardEMI == true)
                            {
                                juspaySessionReq.amount = fetchTxnDetails.amount;
                                juspaySessionReq.customer_phone = fetchTxnDetails.mobile_number;
                                juspaySessionReq.metadata_juspay_gateway_reference_id = ApplicationConfig.JUSPAY_GATEWAY_REFERENCE_ID;
                                juspaySessionReq.payment_filter = new()
                                {
                                    allowDefaultOptions = true,
                                    emiOptions = new()
                                    {
                                        noCostEmi = new JuspayPaymentEmi { enable = false },
                                        showOnlyEmi = true
                                    },
                                };
                            }
                            //else if (fetchTxnDetails.payment_filters?.CardlessEMI == true)
                            //{
                            //    juspaySessionReq.amount = fetchTxnDetails.amount;
                            //    juspaySessionReq.metadata_juspay_gateway_reference_id = ApplicationConfig.JUSPAY_GATEWAY_REFERENCE_ID;
                            //    juspaySessionReq.payment_filter = new()
                            //    {
                            //        allowDefaultOptions = true,
                            //        emiOptions = new()
                            //        {
                            //            standardEmi = new JuspayPaymentEmi { enable = true },
                            //            lowCostEmi = new JuspayPaymentEmi { enable = false },
                            //            noCostEmi = new JuspayPaymentEmi { enable = false }

                            //        },
                            //    };
                            //}
                            else
                            {
                                juspaySessionReq.amount = fetchTxnDetails.amount;
                                juspaySessionReq.payment_filter = new PaymentFilter
                                {
                                    allowDefaultOptions = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ALLOW_DEFAULT_OPTIONS),
                                    options = new List<Option>
                        {
                            new Option
                            {
                                paymentMethodType = sessionReq.payment_mode,
                                enable = Convert.ToBoolean(ApplicationConfig.JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD)
                            }
                        }
                                };
                            }

                            var client = new RestClient(juspaySessionApiUrl);
                            var request = new RestRequest { Method = Method.Post };
                            request.AddHeader("Content-Type", "application/json");
                            request.AddHeader("x-merchantid", juspayMerchantID);
                            request.AddHeader("Authorization", "Basic " + ApplicationConfig.JUSPAY_AUTH_TOKEN);
                            request.AddParameter("application/json", JsonConvert.SerializeObject(juspaySessionReq), ParameterType.RequestBody);

                            stopwatch = Stopwatch.StartNew();

                            var apiResponse = await client.ExecuteAsync(request);

                            stopwatch.Stop();
                            var responseTime = stopwatch.Elapsed.TotalSeconds;

                            await paymentDAL.LogIntegrationApi(traceID, "", JsonConvert.SerializeObject(juspaySessionReq), apiResponse.Content, juspaySessionApiUrl, "JUSPAY CREATE SESSION", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                            if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Content) && apiResponse.StatusCode == HttpStatusCode.OK)
                            {
                                JsonElement jsonSessionResponse = JsonSerializer.Deserialize<JsonElement>(apiResponse.Content);
                                if (jsonSessionResponse.TryGetProperty("status", out JsonElement orderStatus))
                                {
                                    status = orderStatus.GetString();
                                }

                                if (!string.IsNullOrEmpty(status) && (status.ToUpper() == "CREATED" || status.ToUpper() == "NEW"))
                                {
                                    if (jsonSessionResponse.TryGetProperty("id", out JsonElement orderId))
                                    {
                                        order_id = orderId.GetString();
                                    }

                                    if (!string.IsNullOrEmpty(order_id))
                                    {
                                        bool saved = await orderDAL.InsertJuspayOrderDetails(transactionID, traceID, fetchTxnDetails.amount, order_id, sessionReq.payment_mode, generateDpsOrderId, gateway_customer_id);
                                        if (saved)
                                        {
                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = null;
                                            response.TraceId = traceID;
                                            response.Data = jsonSessionResponse;
                                        }
                                        else
                                        {
                                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                                MethodName: "OrderBAL.cs - Session() ",
                                                TraceId: traceID,
                                                Message: "Failed to save order details for transaction: " + transactionID + " Request: " + JsonConvert.SerializeObject(juspaySessionReq) + " Response: " + apiResponse.Content), ApplicationConfig.ORDER_LOGS);

                                            response.StatusCode = 0;
                                            response.StatusMessage = "Failed";
                                            response.Message = "Failed to save order details";
                                            response.TraceId = traceID;
                                            response.Data = null;
                                            return response;
                                        }
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                            MethodName: "OrderBAL.cs - Session() ",
                                            TraceId: traceID,
                                            Message: "Order ID returned Null. Request: " + JsonConvert.SerializeObject(juspaySessionReq) + "Response: " + apiResponse.Content), ApplicationConfig.ORDER_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Order could not be processed, please try again";
                                        response.TraceId = traceID;
                                        response.Data = null;
                                    }
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "OrderBAL.cs - Session() ",
                                        TraceId: traceID,
                                        Message: "Unexpected status returned from API. Request: " + JsonConvert.SerializeObject(juspaySessionReq) + " Response: " + apiResponse.Content), ApplicationConfig.ORDER_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Order could not be processed, please try again";
                                    response.TraceId = traceID;
                                    response.Data = null;
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "OrderBAL.cs - Session() ",
                                    TraceId: traceID,
                                    Message: "API call failed or returned empty response. Request: " + JsonConvert.SerializeObject(juspaySessionReq) + " Response: " + apiResponse.Content), ApplicationConfig.ORDER_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Order could not be processed, please try again";
                                response.TraceId = traceID;
                                response.Data = null;
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - Session() ",
                                Message: "Unable to fetch txn details from transaction id. Decrypted ID: " + decID + " Transaction ID: " + transactionID,
                                TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Data Not Found";
                            response.TraceId = traceID;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderBAL.cs - Session() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
                            TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "OrderBAL.cs - Session() ",
                        Message: "Invalid ID format. ID: " + sessionReq.ID,
                        TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Invalid ID format";
                    response.TraceId = null;
                    response.Data = null;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderBAL.cs - Session() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = traceID;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> GetOrderStatus(FetchOrderStatusReq fetchOrderStatusReq, string application_name, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            PlutusBAL plutusBAL = new PlutusBAL();
            PlutusDAL plutusDAL = new PlutusDAL();
            OrderDAL orderDAL = new OrderDAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            FetchOrderStatusResp fetchOrderStatusResp = new FetchOrderStatusResp();

            string JuspayOrderStatusApiUrl = string.Empty;
            string juspayMerchantID = string.Empty;

            GetPlutusOrderStatusData getPlutusOrderStatusData = new GetPlutusOrderStatusData();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                getPlutusOrderStatusData = await plutusDAL.GetPgNameByTraceId(fetchOrderStatusReq.trace_id);

                if (getPlutusOrderStatusData != null && !string.IsNullOrEmpty(getPlutusOrderStatusData.pg_name) && getPlutusOrderStatusData.pg_name.ToUpper() == "PLUTUS")
                {
                    PlutusOrderStatusReq plutusOrderStatusReq = new PlutusOrderStatusReq
                    {
                        order_id = getPlutusOrderStatusData.order_id,
                        dps_order_id = fetchOrderStatusReq.dps_order_id,
                        trace_id = fetchOrderStatusReq.trace_id,
                        transaction_id = getPlutusOrderStatusData.transaction_id
                    };

                    fetchOrderStatusResp = await plutusBAL.PlutusOrderStatus(plutusOrderStatusReq, flow_type);

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Successfully Fetched Plutus Order Status";
                    response.TraceId = fetchOrderStatusReq.trace_id;
                    response.Data = fetchOrderStatusResp;
                }
                else
                {
                    juspayMerchantID = await orderDAL.GetJuspayMerchantID(application_name);
                    if (string.IsNullOrEmpty(juspayMerchantID))
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderBAL.cs - GetOrderStatus() ",
                            TraceId: fetchOrderStatusReq.trace_id,
                            Message: "Unable to fetch Merchant ID for the Given Application. Application Name: " + application_name), ApplicationConfig.ORDER_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Unable to fetch Merchant ID for the Given Application";
                        response.TraceId = fetchOrderStatusReq.trace_id;
                        response.Data = null;
                        return response;
                    }

                    JuspayOrderStatusApiUrl = ApplicationConfig.JUSPAY_ORDER_API_URL + "/" + fetchOrderStatusReq.dps_order_id;
                    var client = new RestClient(JuspayOrderStatusApiUrl);
                    var request = new RestRequest { Method = Method.Get };
                    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    request.AddHeader("x-merchantid", juspayMerchantID);
                    request.AddHeader("Authorization", "Basic " + ApplicationConfig.JUSPAY_AUTH_TOKEN);

                    stopwatch = Stopwatch.StartNew();

                    var apiResponse = await client.ExecuteAsync(request);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(fetchOrderStatusReq.trace_id, "", "", apiResponse.Content, JuspayOrderStatusApiUrl, "JUSPAY FETCH ORDER STATUS", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                    if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Content) && apiResponse.StatusCode == HttpStatusCode.OK)
                    {
                        JsonElement parsedContent = JsonSerializer.Deserialize<JsonElement>(apiResponse.Content);

                        if (parsedContent.TryGetProperty("id", out JsonElement idElement))
                        {
                            fetchOrderStatusResp.order_id = idElement.GetString();
                        }
                        if (parsedContent.TryGetProperty("udf9", out JsonElement udf9Element))
                        {
                            string udf9 = udf9Element.GetString();

                            if (udf9.Contains("|"))
                            {
                                string[] udf9Parts = udf9.Split("|");

                                fetchOrderStatusResp.trace_id = udf9Parts[1];
                                fetchOrderStatusResp.transaction_id = udf9Parts[2];
                                fetchOrderStatusResp.dps_order_id = udf9Parts[3];
                            }
                        }

                        if (parsedContent.TryGetProperty("payment_method_type", out JsonElement payment_method_typeElement))
                        {
                            fetchOrderStatusResp.payment_mode = payment_method_typeElement.GetString();
                        }

                        if (parsedContent.TryGetProperty("status", out JsonElement orderStatus))
                        {
                            fetchOrderStatusResp.status = orderStatus.GetString();
                        }
                        if (parsedContent.TryGetProperty("mandate", out JsonElement mandateElement))
                        {
                            if (mandateElement.TryGetProperty("mandate_id", out JsonElement mandateid))
                            {
                                fetchOrderStatusResp.mandate_id = mandateid.GetString();
                            }
                        }


                        if (parsedContent.TryGetProperty("amount", out JsonElement orderAmount))
                        {
                            fetchOrderStatusResp.amount = orderAmount.GetInt32();
                        }

                        if (parsedContent.TryGetProperty("amount_refunded", out JsonElement orderRefundedAmount))
                        {
                            fetchOrderStatusResp.amount_refunded = orderRefundedAmount.GetInt32();
                        }

                        if (!string.IsNullOrEmpty(fetchOrderStatusResp.status) && ApplicationConfig.DPS_JUSPAY_STATUS_LIST.ContainsKey(fetchOrderStatusResp.status))
                        {
                            fetchOrderStatusResp.payment_status = ApplicationConfig.DPS_JUSPAY_STATUS_LIST[fetchOrderStatusResp.status];
                            fetchOrderStatusResp.payment_status = fetchOrderStatusResp.payment_status.ToUpper();
                        }

                        if (parsedContent.TryGetProperty("payment_gateway_response", out JsonElement paymentGatewayResponseElement))
                        {
                            if (paymentGatewayResponseElement.TryGetProperty("created", out JsonElement createdElement))
                            {
                                fetchOrderStatusResp.payment_received_on = createdElement.GetString();
                            }

                            if (paymentGatewayResponseElement.TryGetProperty("epg_txn_id", out JsonElement epg_txn_idElement))
                            {
                                fetchOrderStatusResp.auth_code = epg_txn_idElement.GetString();
                            }
                            else if (paymentGatewayResponseElement.TryGetProperty("rrn", out JsonElement rrnElement))
                            {
                                fetchOrderStatusResp.auth_code = rrnElement.GetString();
                            }
                            else
                            {
                                fetchOrderStatusResp.auth_code = fetchOrderStatusResp.order_id;
                            }
                        }

                        if (parsedContent.TryGetProperty("refunds", out JsonElement refundsElement) && refundsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement refund in refundsElement.EnumerateArray())
                            {
                                var refundItem = new RefundDetail();

                                if (refund.TryGetProperty("amount", out JsonElement refundAmount))
                                    refundItem.amount = refundAmount.GetInt32();

                                if (refund.TryGetProperty("unique_request_id", out JsonElement uniqueRequestId))
                                    refundItem.unique_request_id = uniqueRequestId.GetString();

                                if (refund.TryGetProperty("ref", out JsonElement refRef))
                                    refundItem.ref_id = refRef.GetString();

                                if (refund.TryGetProperty("created", out JsonElement refundCreated))
                                    refundItem.created = refundCreated.GetString();

                                if (refund.TryGetProperty("last_updated", out JsonElement refundLastUpdated))
                                    refundItem.last_updated = refundLastUpdated.GetString();

                                if (refund.TryGetProperty("status", out JsonElement refundStatus))
                                    refundItem.status = refundStatus.GetString();

                                if (refund.TryGetProperty("error_message", out JsonElement errorMessage))
                                    refundItem.error_message = errorMessage.GetString();

                                if (refund.TryGetProperty("sent_to_gateway", out JsonElement sentToGateway))
                                    refundItem.sent_to_gateway = sentToGateway.GetBoolean();

                                if (refund.TryGetProperty("expected_refund_credit_time", out JsonElement expectedCreditTime))
                                    refundItem.expected_refund_credit_time = expectedCreditTime.GetString();

                                fetchOrderStatusResp.refunds.Add(refundItem);
                            }
                        }

                        response.StatusCode = 1;
                        response.StatusMessage = "Success";
                        response.Message = null;
                        response.TraceId = fetchOrderStatusReq.trace_id;

                        response.Data = fetchOrderStatusResp;
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderBAL.cs - GetOrderStatus() ",
                            TraceId: fetchOrderStatusReq.trace_id,
                            Message: "API call failed or returned empty response. Request: " + JsonConvert.SerializeObject(fetchOrderStatusReq) + " Response: " + apiResponse.Content), ApplicationConfig.ORDER_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Order could not be processed, please try again";
                        response.TraceId = fetchOrderStatusReq.trace_id;
                        response.Data = null;
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderBAL.cs - GetOrderStatus() ",
                    TraceId: fetchOrderStatusReq.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = fetchOrderStatusReq.trace_id;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> FetchRedirectionUrl(FetchRedirectionUrlReq fetchRedirectionUrlReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            OrderDAL orderDAL = new OrderDAL();
            CallbackDAL callbackDAL = new CallbackDAL();
            ILESBInterface ilesbInterface = new ILESBInterface();
            PaymentBAL paymentBAL = new PaymentBAL();
            PlutusDAL plutusDAL = new PlutusDAL();
            CallbackBAL callbackBAL = new CallbackBAL();
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();

            List<FetchOrderAndProposalDetails> fetchOrderAndProposalDetailsList = new List<FetchOrderAndProposalDetails>();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            DPSStatusResponse orderStatusResponse = new DPSStatusResponse();

            string transactionID = string.Empty;
            string traceID = string.Empty;
            string iasId = string.Empty;
            string token = string.Empty;
            string proposal_status = string.Empty;
            bool isIasSuccess = false;
            int ias_failed_count = 0;
            int fetch_new_proposal_status_failed_count = 0;
            string DPS_ORDER_ID = string.Empty;
            string ORDER_ID = string.Empty;
            string ORDER_STATUS = string.Empty;
            string AUTH_CODE = string.Empty;
            string mandate_id = string.Empty;
            string PAYMENT_MODE = string.Empty;

            string fullReturnStatusUrl = string.Empty;

            int break_in_count = 0;
            int success_count = 0;

            try
            {
                string decID = encryptDecrypt.DecryptResponse(fetchRedirectionUrlReq.ID);
                DPS_ORDER_ID = fetchRedirectionUrlReq.pg_id;
                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        fetchOrderAndProposalDetailsList = await orderDAL.GetOrderAndProposalDetails(transactionID, DPS_ORDER_ID);
                        if (fetchOrderAndProposalDetailsList != null && fetchOrderAndProposalDetailsList.Count > 0)
                        {
                            #region Fetch Return Status URLs from Template ID
                            fullReturnStatusUrl = ApplicationConfig.RETURN_STATUS_URL;
                            if (string.IsNullOrEmpty(fullReturnStatusUrl))
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                   MethodName: "OrderBAL.cs - FetchRedirectionUrl() ",
                                   Message: "Unable to fetch Return Status URL for the Given Template ID. Encrypted ID: " + fetchRedirectionUrlReq.ID + " Transaction ID: " + transactionID + " Trace ID: " + traceID + " Template ID: " + fetchOrderAndProposalDetailsList[0].template_id,
                                   TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Unable to fetch Return Status URL for the Given Template ID";
                                response.TraceId = traceID;
                                response.Data = null;
                                return response;
                            }

                            #endregion

                            for (int i = 0; i < 3; i++)
                            {
                                #region Get Order Status
                                fetchOrderStatusReq = new FetchOrderStatusReq
                                {
                                    dps_order_id = DPS_ORDER_ID,
                                    trace_id = traceID
                                };

                                orderStatusResponse = await GetOrderStatus(fetchOrderStatusReq, fetchOrderAndProposalDetailsList[0].application_name, flow_type);

                                var orderData = orderStatusResponse.Data as FetchOrderStatusResp;
                                if (orderData != null)
                                {
                                    ORDER_STATUS = orderData.payment_status;
                                    AUTH_CODE = orderData.auth_code;
                                    ORDER_ID = orderData.order_id;
                                    mandate_id = orderData.mandate_id;
                                    PAYMENT_MODE = orderData.payment_mode;
                                }
                                #endregion

                                if (!string.IsNullOrEmpty(ORDER_STATUS) && ORDER_STATUS == "SUCCESS")
                                {
                                    if (fetchOrderAndProposalDetailsList[0].journey_type == "ADVANCE")
                                    {
                                        if (!string.IsNullOrEmpty(fetchOrderAndProposalDetailsList[0].pre_payment_return_url))
                                        {
                                            FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                            {
                                                URL = fetchOrderAndProposalDetailsList[0].pre_payment_return_url,
                                                STATUS = ORDER_STATUS
                                            };

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "Both Order and Proposal Success";
                                            response.TraceId = traceID;
                                            response.Data = fetchRedirectionUrlResp;
                                            break;
                                        }
                                        else
                                        {
                                            FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                            {
                                                URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                                STATUS = ORDER_STATUS
                                            };

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "Both Order and Proposal Success";
                                            response.TraceId = traceID;
                                            response.Data = fetchRedirectionUrlResp;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        #region IAS Check and Clearance
                                        foreach (FetchOrderAndProposalDetails fetchOrderAndProposalDetails in fetchOrderAndProposalDetailsList)
                                        {
                                            if (ApplicationConfig.NPA_PROPOSAL_STATUS_ARRAY.Contains(fetchOrderAndProposalDetails.proposal_status.ToUpper()))
                                            {
                                                iasId = await orderDAL.GetNextIASSequence();

                                                token = ilesbInterface.CreateBiztalkAuthToken(ApplicationConfig.BIZTALK_TOKEN_USERNAME, ApplicationConfig.BIZTALK_TOKEN_PASSWORD);
                                                isIasSuccess = await ilesbInterface.CallIASApi(iasId, token, traceID, fetchOrderAndProposalDetails.proposal_number, flow_type);

                                                if (!isIasSuccess)
                                                {
                                                    ias_failed_count = ias_failed_count + 1;
                                                    break;
                                                }

                                                proposal_status = await paymentBAL.FetchProposalStatus(traceID, fetchOrderAndProposalDetails.proposal_number, fetchOrderAndProposalDetails.core, flow_type);
                                                if (!string.IsNullOrEmpty(proposal_status))
                                                {
                                                    fetchOrderAndProposalDetails.proposal_status = proposal_status;
                                                }
                                                else
                                                {
                                                    fetch_new_proposal_status_failed_count = fetch_new_proposal_status_failed_count + 1;
                                                    break;
                                                }
                                            }
                                        }

                                        if (ias_failed_count > 0)
                                        {
                                            FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                            {
                                                URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                                STATUS = ORDER_STATUS
                                            };

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "IAS API call failed Count : " + ias_failed_count;
                                            response.TraceId = traceID;
                                            response.Data = fetchRedirectionUrlResp;
                                            break;
                                        }

                                        if (fetch_new_proposal_status_failed_count > 0)
                                        {
                                            FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                            {
                                                URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                                STATUS = ORDER_STATUS
                                            };

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "Fetch Proposal call failed Count : " + fetch_new_proposal_status_failed_count;
                                            response.TraceId = traceID;
                                            response.Data = fetchRedirectionUrlResp;
                                            break;
                                        }
                                        #endregion

                                        #region Return URL based on Latest Proposal Status
                                        foreach (FetchOrderAndProposalDetails fetchOrderAndProposalDetails in fetchOrderAndProposalDetailsList)
                                        {
                                            if (ApplicationConfig.NPVI_PROPOSAL_STATUS_ARRAY.Contains(fetchOrderAndProposalDetails.proposal_status.ToUpper()))
                                            {
                                                break_in_count = break_in_count + 1;
                                                await orderDAL.UpdateIsBreakInReq(fetchOrderAndProposalDetails.proposal_number);
                                            }
                                            if ((fetchOrderAndProposalDetails.core == "PF" && (ApplicationConfig.NCCN_PROPOSAL_STATUS_ARRAY.Contains(fetchOrderAndProposalDetails.proposal_status.ToUpper()) || ApplicationConfig.NC_PROPOSAL_STATUS_ARRAY.Contains(fetchOrderAndProposalDetails.proposal_status.ToUpper()))) || fetchOrderAndProposalDetails.core == "ARTEMIS")
                                            {
                                                success_count = success_count + 1;
                                            }
                                            else
                                            {
                                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                                    MethodName: "OrderBAL.cs - FetchRedirectionUrl() ",
                                                    Message: "Unknown Proposal Status: " + fetchOrderAndProposalDetails.proposal_status + " Transaction ID: " + transactionID,
                                                    TraceId: traceID), ApplicationConfig.ORDER_LOGS);
                                            }
                                        }

                                        if (break_in_count > 0)
                                        {
                                            FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                            {
                                                URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                                STATUS = ORDER_STATUS
                                            };

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "Order Success but Proposal BreakIn";
                                            response.TraceId = traceID;
                                            response.Data = fetchRedirectionUrlResp;
                                            break;
                                        }
                                        else if (break_in_count == 0 && success_count == fetchOrderAndProposalDetailsList.Count)
                                        {
                                            FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                            {
                                                URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                                STATUS = ORDER_STATUS
                                            };

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "Both Order and Proposal Success";
                                            response.TraceId = traceID;
                                            response.Data = fetchRedirectionUrlResp;
                                            break;
                                        }
                                        else
                                        {
                                            FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                            {
                                                URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                                STATUS = ORDER_STATUS
                                            };

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = "Unknown Proposal Status";
                                            response.TraceId = traceID;
                                            response.Data = fetchRedirectionUrlResp;
                                        }
                                        #endregion
                                    }
                                }
                                else if (!string.IsNullOrEmpty(ORDER_STATUS) && ORDER_STATUS == "PENDING")
                                {
                                    FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                    {
                                        URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                        STATUS = ORDER_STATUS
                                    };

                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = "Order Status Pending";
                                    response.TraceId = traceID;
                                    response.Data = fetchRedirectionUrlResp;
                                }
                                else
                                {
                                    FetchRedirectionUrlResp fetchRedirectionUrlResp = new FetchRedirectionUrlResp
                                    {
                                        URL = fullReturnStatusUrl + "status=" + ORDER_STATUS,
                                        STATUS = ORDER_STATUS
                                    };

                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = "Order not Success";
                                    response.TraceId = traceID;
                                    response.Data = fetchRedirectionUrlResp;
                                }

                                if (i < 2)
                                {
                                    await Task.Delay(2000);
                                }
                            }

                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - FetchRedirectionUrl() ",
                                Message: "Could not fetch order and proposal details. Transaction ID: " + transactionID + " DPS Order ID: " + DPS_ORDER_ID,
                                TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Could not fetch order and proposal details";
                            response.TraceId = null;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderBAL.cs - FetchRedirectionUrl() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
                            TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = null;
                        response.Data = null;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "OrderBAL.cs - FetchRedirectionUrl() ",
                        Message: "Invalid ID format. ID: " + fetchRedirectionUrlReq.ID,
                        TraceId: traceID), ApplicationConfig.ORDER_LOGS);

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
                    MethodName: "OrderBAL.cs - FetchRedirectionUrl() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }
            finally
            {
                if (response.StatusCode == 1)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            fetchPaymentDetails = await callbackDAL.LogPaymentDetails(ORDER_ID, AUTH_CODE, transactionID, traceID, DPS_ORDER_ID, ORDER_STATUS, mandate_id, PAYMENT_MODE);

                            var is_payment_resync_sent = await plutusDAL.GetIsPaymentResyncSent(traceID);

                            if (!string.IsNullOrEmpty(is_payment_resync_sent) && is_payment_resync_sent.ToUpper() != "Y" && fetchPaymentDetails.journey_type != "ADVANCE" && ORDER_STATUS == "SUCCESS")
                            {
                                SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                                {
                                    reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                                    trace_id = fetchPaymentDetails.trace_id,
                                    transaction_id = fetchPaymentDetails.transaction_id,
                                    application_name = fetchPaymentDetails.application_name,
                                    channel_application_details = fetchPaymentDetails.channel_application_details,
                                    auth_code = AUTH_CODE,
                                    sync_event = "PAYMENT_CONFIRMATION"
                                };

                                await callbackBAL.PaymentSyncProposalToSource(syncProposalToSource, flow_type, fetchPaymentDetails.journey_type);
                            }
                        }
                        catch (Exception ex)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - FetchRedirectionUrl() - Background SyncProposalToSource",
                                TraceId: fetchPaymentDetails.trace_id,
                                Message: $"Exception occurred: {ex.Message}",
                                StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
                        }
                    });
                }
            }

            return response;
        }

        public async Task<DPSStatusResponse> FetchPolicyDetailsByID(FetchPolicyDetailsByIDReq fetchPolicyDetailsByIDReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            OrderDAL orderDAL = new OrderDAL();

            FetchPolicyDetailsByIDResp fetchPolicyDetailsByIDResp = new FetchPolicyDetailsByIDResp();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();
            GetDpsOrderStatusAndIsMobileUserResp getDpsOrderStatusAndIsMobileUserResp = new GetDpsOrderStatusAndIsMobileUserResp();
            FetchTxnDetails fetchTxnDetails = new FetchTxnDetails();

            string transactionID = string.Empty;
            string traceID = string.Empty;

            string dps_juspay_status = string.Empty;

            int count = 0;
            int breakin_count = 0;


            try
            {
                string decID = encryptDecrypt.DecryptResponse(fetchPolicyDetailsByIDReq.ID);

                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];
                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID))
                    {
                        getDpsOrderStatusAndIsMobileUserResp = await orderDAL.GetDpsOrderStatusAndIsMobileUser(transactionID);
                        dps_juspay_status = getDpsOrderStatusAndIsMobileUserResp.dps_juspay_status;
                        fetchPolicyDetailsByIDResp.is_mobile_user = getDpsOrderStatusAndIsMobileUserResp.is_mobile_user;
                        fetchPolicyDetailsByIDResp.email = getDpsOrderStatusAndIsMobileUserResp.email;

                        if (!string.IsNullOrEmpty(dps_juspay_status))
                        {
                            fetchTxnDetails = await orderDAL.GetJourneyTypeAndAmount(transactionID);
                            if (fetchTxnDetails != null && !string.IsNullOrEmpty(fetchTxnDetails.journey_type))
                            {
                                if (dps_juspay_status.ToUpper() == "SUCCESS")
                                {
                                    //if (fetchTxnDetails.journey_type == "ADVANCE" && flow_type != "REALTIME - SEND POLICY MAIL")
                                    //{
                                    //    fetchPolicyDetailsByIDResp.textMessage = "Your payment has been confirmed.";
                                    //    fetchPolicyDetailsByIDResp.dps_juspay_status = dps_juspay_status;
                                    //    fetchPolicyDetailsByIDResp.journey_type = fetchTxnDetails.journey_type;
                                    //    fetchPolicyDetailsByIDResp.is_mobile_user = getDpsOrderStatusAndIsMobileUserResp.is_mobile_user;

                                    //    response.StatusCode = 1;
                                    //    response.StatusMessage = "Success";
                                    //    response.Message = null;
                                    //    response.TraceId = traceID;
                                    //    response.Data = fetchPolicyDetailsByIDResp;
                                    //    return response;
                                    //}

                                    fetchPolicyDetailsByIDResp.proposalDetailsList = await orderDAL.GetPolicyDetailsByTransactionID(transactionID, traceID);

                                    if (fetchPolicyDetailsByIDResp.proposalDetailsList != null && fetchPolicyDetailsByIDResp.proposalDetailsList.Count > 0)
                                    {
                                        foreach (FetchPolicyDetailsByIDRespDetails fetchPolicyDetailsByIDRespDetails in fetchPolicyDetailsByIDResp.proposalDetailsList)
                                        {
                                            if (!string.IsNullOrEmpty(fetchPolicyDetailsByIDRespDetails.policy_number))
                                            {
                                                count = count + 1;
                                            }

                                            if (!string.IsNullOrEmpty(fetchPolicyDetailsByIDRespDetails.is_breakin_req) && fetchPolicyDetailsByIDRespDetails.is_breakin_req == "Y")
                                            {
                                                breakin_count = breakin_count + 1;
                                            }

                                            if (!string.IsNullOrEmpty(fetchPolicyDetailsByIDRespDetails.proposal_type))
                                            {
                                                fetchPolicyDetailsByIDRespDetails.proposal_type = GetProposalTypeFullForm(fetchPolicyDetailsByIDRespDetails.proposal_type);
                                            }
                                        }

                                        if (breakin_count > 0)
                                        {
                                            fetchPolicyDetailsByIDResp.textMessage = "Your payment has been successfully received. Please complete your inspection process to start your policy coverage. The policy will be issued once the inspection is complete.";
                                            fetchPolicyDetailsByIDResp.dps_juspay_status = dps_juspay_status;
                                            fetchPolicyDetailsByIDResp.journey_type = fetchTxnDetails.journey_type;

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = null;
                                            response.TraceId = traceID;
                                            response.Data = fetchPolicyDetailsByIDResp;
                                        }
                                        else if (breakin_count == 0 && count == fetchPolicyDetailsByIDResp.proposalDetailsList.Count)
                                        {
                                            fetchPolicyDetailsByIDResp.textMessage = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                                            fetchPolicyDetailsByIDResp.dps_juspay_status = dps_juspay_status;
                                            fetchPolicyDetailsByIDResp.journey_type = fetchTxnDetails.journey_type;
                                            fetchPolicyDetailsByIDResp.flightTracking = fetchTxnDetails.flightTracking;
                                            fetchPolicyDetailsByIDResp.flightTrackingURL = fetchTxnDetails.flightTrackingURL;

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = null;
                                            response.TraceId = traceID;
                                            response.Data = fetchPolicyDetailsByIDResp;
                                        }
                                        else
                                        {
                                            fetchPolicyDetailsByIDResp.textMessage = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                                            fetchPolicyDetailsByIDResp.dps_juspay_status = dps_juspay_status;
                                            fetchPolicyDetailsByIDResp.journey_type = fetchTxnDetails.journey_type;

                                            response.StatusCode = 1;
                                            response.StatusMessage = "Success";
                                            response.Message = null;
                                            response.TraceId = traceID;
                                            response.Data = fetchPolicyDetailsByIDResp;
                                        }
                                    }
                                    else
                                    {
                                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                                MethodName: "OrderBAL.cs - FetchPolicyDetailsByID() ",
                                                Message: "Data not found. TransactionID: " + transactionID,
                                                TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Data not found";
                                        response.TraceId = traceID;
                                        response.Data = null;
                                    }
                                }
                                else if (dps_juspay_status.ToUpper() == "PENDING")
                                {
                                    fetchPolicyDetailsByIDResp.textMessage = "Once your payment is confirmed, we will inform you and your policy application will be processed further. In case your payment fails and any money is deducted, we will refund it back. No Need to Worry.";
                                    fetchPolicyDetailsByIDResp.dps_juspay_status = dps_juspay_status;
                                    fetchPolicyDetailsByIDResp.journey_type = fetchTxnDetails.journey_type;

                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = null;
                                    response.TraceId = traceID;
                                    response.Data = fetchPolicyDetailsByIDResp;
                                }
                                else if (dps_juspay_status.ToUpper() == "FAILED")
                                {
                                    fetchPolicyDetailsByIDResp.textMessage = "Your payment attempt was not successful due to an unexpected issue. Please retry the transaction to continue.";
                                    fetchPolicyDetailsByIDResp.dps_juspay_status = dps_juspay_status;
                                    fetchPolicyDetailsByIDResp.journey_type = fetchTxnDetails.journey_type;

                                    response.StatusCode = 1;
                                    response.StatusMessage = "Success";
                                    response.Message = null;
                                    response.TraceId = traceID;
                                    response.Data = fetchPolicyDetailsByIDResp;
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "OrderBAL.cs - FetchPolicyDetailsByID() ",
                                        Message: "Unexpected Order Status. TransactionID: " + transactionID + " DPS Order Status: " + dps_juspay_status,
                                        TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Unexpected Order Status";
                                    response.TraceId = traceID;
                                    response.Data = null;
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - FetchPolicyDetailsByID() ",
                                Message: "Unable to fetch txn details from transaction id. TransactionID: " + transactionID,
                                TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Data Not Found";
                                response.TraceId = traceID;
                                response.Data = null;
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - FetchPolicyDetailsByID() ",
                                Message: "Unable to fetch Order Status. TransactionID: " + transactionID,
                                TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "Unable to fetch Order Status";
                            response.TraceId = traceID;
                            response.Data = null;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderBAL.cs - FetchPolicyDetailsByID() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID,
                            TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = traceID;
                        response.Data = null;
                        return response;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "OrderBAL.cs - FetchPolicyDetailsByID() ",
                               Message: "Invalid ID format. ID: " + fetchPolicyDetailsByIDReq.ID,
                               TraceId: traceID), ApplicationConfig.ORDER_LOGS);

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
                    MethodName: "OrderBAL.cs - FetchPolicyDetailsByID() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<MandateExecutionResp> CaptureMandate(CaptureMandateReq captureMandateReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            OrderDAL orderDAL = new OrderDAL();
            PlutusDAL plutusDAL = new PlutusDAL();
            PlutusBAL plutusBAL = new PlutusBAL();
            string merchant_id = string.Empty;

            GetPlutusOrderStatusData getPlutusOrderStatusData = new GetPlutusOrderStatusData();
            PlutusMandateReq plutusMandateReq = new PlutusMandateReq();
            PlutusMandateResp plutusMandateResp = new PlutusMandateResp();
            MandateExecutionResp mandateExecutionResp = new MandateExecutionResp();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                getPlutusOrderStatusData = await plutusDAL.GetPgNameByTraceId(captureMandateReq.trace_id);
                if (getPlutusOrderStatusData.pg_name.ToUpper() == "PLUTUS")
                {
                    plutusMandateReq.plutusMandateId = captureMandateReq.mandate_id;
                    plutusMandateReq.amount = captureMandateReq.amount;
                    plutusMandateReq.mandateAction = "EXECUTE";
                    plutusMandateReq.trace_id = captureMandateReq.trace_id;
                    plutusMandateReq.mandateExecutionTime = DateTime.UtcNow;
                    plutusMandateReq.customerId = captureMandateReq.customer_id;
                    plutusMandateResp = await plutusBAL.PlutusMandate(plutusMandateReq, flow_type);

                    mandateExecutionResp = new MandateExecutionResp
                    {
                        auth_code = plutusMandateResp.data?.authCode,
                        isSuccess = plutusMandateResp.isSuccess
                    };
                }
                else
                {
                    merchant_id = await orderDAL.GetJuspayMerchantID(captureMandateReq.application_name);

                    string url = ApplicationConfig.JUSPAY_CAPTURE_MANDATE_API_URL;

                    var client = new RestClient(url);
                    var request = new RestRequest { Method = Method.Post };

                    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    request.AddHeader("Authorization", "Basic " + ApplicationConfig.JUSPAY_AUTH_TOKEN);
                    request.AddParameter("order.order_id", captureMandateReq.order_id);
                    request.AddParameter("order.amount", captureMandateReq.amount);
                    request.AddParameter("order.customer_id", captureMandateReq.customer_id);
                    request.AddParameter("merchant_id", merchant_id);
                    request.AddParameter("mandate_id", captureMandateReq.mandate_id);
                    request.AddParameter("order.metadata.webhook_url", ApplicationConfig.WEBHOOK_URL);
                    //request.AddParameter("mandate.execution_date", captureMandateReq.mandate_execution_date);
                    request.AddParameter("format", "json");
                    stopwatch = Stopwatch.StartNew();

                    var apiResponse = await client.ExecuteAsync(request);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(captureMandateReq.trace_id, "", JsonConvert.SerializeObject(captureMandateReq), apiResponse.Content, url, "JUSPAY CAPTURE MANDATE", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                    if (apiResponse != null && apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                    {
                        JuspayCaptureMandateResp juspayCaptureMandateResp = JsonConvert.DeserializeObject<JuspayCaptureMandateResp>(apiResponse.Content);

                        mandateExecutionResp = new MandateExecutionResp
                        {
                            auth_code = juspayCaptureMandateResp.txn_uuid,
                            isSuccess = juspayCaptureMandateResp.status?.ToUpper() == "PENDING_VBV" ? true : false,
                            status = juspayCaptureMandateResp.status.ToUpper()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderBAL.cs - CaptureMandate() ",
                    TraceId: captureMandateReq.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return mandateExecutionResp;
        }

        public async Task<MandateExecutionResp> RevokeMandate(RevokeMandateReq revokeMandateReq, string flow_type)
        {
            PaymentDAL paymentDAL = new PaymentDAL();
            OrderDAL orderDAL = new OrderDAL();
            PlutusDAL plutusDAL = new PlutusDAL();
            PlutusBAL plutusBAL = new PlutusBAL();

            string merchant_id = string.Empty;
            GetPlutusOrderStatusData getPlutusOrderStatusData = new GetPlutusOrderStatusData();
            PlutusMandateReq plutusMandateReq = new PlutusMandateReq();
            PlutusMandateResp plutusMandateResp = new PlutusMandateResp();
            MandateExecutionResp mandateExecutionResp = new MandateExecutionResp();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                getPlutusOrderStatusData = await plutusDAL.GetPgNameByTraceId(revokeMandateReq.trace_id);
                if (getPlutusOrderStatusData.pg_name.ToUpper() == "PLUTUS")
                {
                    plutusMandateReq.plutusMandateId = revokeMandateReq.mandate_id;
                    plutusMandateReq.mandateAction = "REVOKE";
                    plutusMandateReq.trace_id = revokeMandateReq.trace_id;
                    plutusMandateReq.mandateExecutionTime = DateTime.UtcNow;
                    plutusMandateResp = await plutusBAL.PlutusMandate(plutusMandateReq, flow_type);

                    mandateExecutionResp = new MandateExecutionResp
                    {
                        auth_code = plutusMandateResp.data?.authCode,
                        isSuccess = plutusMandateResp.isSuccess,
                        mandate_id = Convert.ToString(plutusMandateResp.data?.mandateId)
                    };
                }
                else
                {
                    merchant_id = await orderDAL.GetJuspayMerchantID(revokeMandateReq.application_name);

                    string url = ApplicationConfig.JUSPAY_REVOKE_MANDATE_API_URL;

                    var client = new RestClient(url);
                    var request = new RestRequest($"{url}/{revokeMandateReq.mandate_id}", Method.Post);

                    request.AddHeader("Authorization", "Basic " + ApplicationConfig.JUSPAY_AUTH_TOKEN);
                    request.AddHeader("x-merchantid", merchant_id);
                    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    request.AddParameter("command", "revoke");

                    stopwatch = Stopwatch.StartNew();

                    var apiResponse = await client.ExecuteAsync(request);

                    stopwatch.Stop();
                    var responseTime = stopwatch.Elapsed.TotalSeconds;

                    await paymentDAL.LogIntegrationApi(revokeMandateReq.trace_id, "", JsonConvert.SerializeObject(revokeMandateReq), apiResponse.Content, url, "JUSPAY REVOKE MANDATE", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                    if (apiResponse != null && apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                    {
                        JuspayRevokeMandateResp juspayRevokeMandateResp = JsonConvert.DeserializeObject<JuspayRevokeMandateResp>(apiResponse.Content);

                        mandateExecutionResp = new MandateExecutionResp
                        {
                            auth_code = null,
                            isSuccess = juspayRevokeMandateResp.mandate_status?.ToUpper() == "REVOKED" ? true : false,
                            mandate_id = juspayRevokeMandateResp.mandate_id
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderBAL.cs - RevokeMandate() ",
                    TraceId: revokeMandateReq.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return mandateExecutionResp;
        }

        public DPSStatusResponse FetchCrossSellingDetails(FetchCrossSellingDetailsReq fetchCrossSellingDetailsReq)
        {
            DPSStatusResponse response = new DPSStatusResponse();

            FetchCrossSellingDetailsResp fetchCrossSellingDetailsResp = new FetchCrossSellingDetailsResp();
            EncryptDecrypt encryptDecrypt = new EncryptDecrypt();

            string transactionID = string.Empty;
            string traceID = string.Empty;

            string product = string.Empty;

            fetchCrossSellingDetailsResp.products_list = new List<CrossSellingProduct>();

            try
            {
                string decID = encryptDecrypt.DecryptResponse(fetchCrossSellingDetailsReq.ID);


                if (decID.Contains("|"))
                {
                    string[] IDParts = decID.Split("|");

                    transactionID = IDParts[0];
                    traceID = IDParts[1];

                    product = fetchCrossSellingDetailsReq.Product;

                    if (!string.IsNullOrEmpty(transactionID) && !string.IsNullOrEmpty(traceID) && !string.IsNullOrEmpty(product))
                    {
                        string crossSellData = ApplicationConfig.CROSS_SELLING_PRODUCTS_MAPPING[product];
                        if (!string.IsNullOrEmpty(crossSellData))
                        {
                            if (crossSellData.Contains(","))
                            {
                                string[] crossSellItems = crossSellData.Split(",");
                                foreach (string crossSellItem in crossSellItems)
                                {
                                    CrossSellingProduct crossSellingProduct = new CrossSellingProduct
                                    {
                                        product = crossSellItem
                                    };
                                    fetchCrossSellingDetailsResp.products_list.Add(crossSellingProduct);
                                }
                            }
                            else
                            {
                                CrossSellingProduct crossSellingProduct = new CrossSellingProduct
                                {
                                    product = crossSellData
                                };
                                fetchCrossSellingDetailsResp.products_list.Add(crossSellingProduct);
                            }

                            response.StatusCode = 1;
                            response.StatusMessage = "Success";
                            response.Message = "Cross-sell Data Fetched Successfully";
                            response.TraceId = traceID;
                            response.Data = fetchCrossSellingDetailsResp;
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - FetchCrossSellingDetails() ",
                                Message: "No Cross Selling Data found for the given Product. Product: " + product + " Transaction ID: " + transactionID,
                                TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = "No Cross Selling Data found for the given Product";
                            response.TraceId = traceID;
                            response.Data = null;
                            return response;
                        }
                    }
                    else
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "OrderBAL.cs - FetchCrossSellingDetails() ",
                            Message: "Decrypted ID contains empty Transaction ID or Trace ID. Decrypted ID: " + decID + " or Decrypted ProductData contains empty Product or Product Value. Product: " + fetchCrossSellingDetailsReq.Product,
                            TraceId: traceID), ApplicationConfig.ORDER_LOGS);

                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = "Invalid ID format";
                        response.TraceId = traceID;
                        response.Data = null;
                        return response;
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                               MethodName: "OrderBAL.cs - FetchCrossSellingDetails() ",
                               Message: "Invalid ID format. ID: " + fetchCrossSellingDetailsReq.ID + " or Invalid ProductData format. ProductData: " + fetchCrossSellingDetailsReq.Product,
                               TraceId: traceID), ApplicationConfig.ORDER_LOGS);

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
                    MethodName: "OrderBAL.cs - FetchCrossSellingDetails() ",
                    TraceId: traceID,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = null;
                response.TraceId = null;
                response.Data = null;
            }

            return response;
        }

        public async Task<DPSStatusResponse> ILEMIValidateInstallment(ILEMIValidateInstallmentReq iLEMIValidateInstallmentReq, string flow_type)
        {
            OrderDAL orderDAL = new OrderDAL();
            DPSStatusResponse response = new DPSStatusResponse();
            MandateExecutionResp mandateExecutionResp = new MandateExecutionResp();
            CaptureMandateReq captureMandateReq = new CaptureMandateReq();
            Stopwatch stopwatch = new Stopwatch();
            ArtemisEMISummaryDetailsResp artemisEMISummaryDetailsResp = new ArtemisEMISummaryDetailsResp();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            int latestDBEmiInstallment = 0;
            string recurring_id = string.Empty;
            LatestEmiInstallment latestEmi = new LatestEmiInstallment();
            LatestEmiInstallment scheduledEmiInstallment = new LatestEmiInstallment();
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            DPSStatusResponse juspayOrderStatus = new DPSStatusResponse();
            try
            {
                // Getting latest EMI Installment from DB
                LatestEmiInstallment latestEmiInstallmentDetails = await orderDAL.GetLatestEmiInstallment(iLEMIValidateInstallmentReq.proposal_number);
                // Check if any installment exists against given proposal number
                if (latestEmiInstallmentDetails == null)
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = $"No EMI installment details found for proposal number {iLEMIValidateInstallmentReq.proposal_number}.";
                    response.Data = null;
                    response.TraceId = iLEMIValidateInstallmentReq.trace_id;
                    return response;
                }
                else
                {
                    if (Convert.ToInt32(iLEMIValidateInstallmentReq.installment_number) < Convert.ToInt32(latestEmiInstallmentDetails.installment_number))
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "Failed";
                        response.Message = $"Mandate for installment number {iLEMIValidateInstallmentReq.installment_number} is already processed.";
                        response.Data = null;
                        return response;
                    }
                    else //if (Convert.ToInt32(iLEMIValidateInstallmentReq.installment_number) >= Convert.ToInt32(latestEmiInstallmentDetails.installment_number))
                    {
                        artemisEMISummaryDetailsResp = apiFactoryInterface.GetArtemisEMISummaryDetails(iLEMIValidateInstallmentReq.trace_id, new ArtemisEmiDetailsReqModel { proposalNumber = iLEMIValidateInstallmentReq.proposal_number, policyNumber = iLEMIValidateInstallmentReq.policy_number }, flow_type).Result;
                        if (artemisEMISummaryDetailsResp.statusCode != 200)
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "OrderBAL.cs - ILEMICaptureMandate() ",
                                TraceId: iLEMIValidateInstallmentReq.trace_id,
                                Message: $"Failed to fetch Artemis EMI Summary Details. Status Code: {artemisEMISummaryDetailsResp.statusCode}, Messages: {string.Join(", ", artemisEMISummaryDetailsResp.messages)}",
                                StackTrace: ""), ApplicationConfig.ORDER_LOGS);
                            response.StatusCode = 0;
                            response.StatusMessage = "Failed";
                            response.Message = $"Cannot Fetch EMI Schedule from Artemis.";
                            response.Data = null;
                            return response;
                        }
                        else
                        {
                            // Get EMI schedule list from Artemis
                            var emiList = artemisEMISummaryDetailsResp.response?.emiSchedule;
                            if (emiList == null || emiList.Count == 0)
                            {
                                response.StatusCode = 0;
                                response.StatusMessage = "Failed";
                                response.Message = "Artemis EMI schedule not available.";
                                response.Data = null;
                                return response;
                            }

                            // Convert user's installment number to int
                            int requestedInstallment = Convert.ToInt32(iLEMIValidateInstallmentReq.installment_number);
                            if (latestEmiInstallmentDetails.status.ToUpper() == "RAISED AND PAID")
                            {
                                latestDBEmiInstallment = Convert.ToInt32(latestEmiInstallmentDetails.installment_number) + 1;
                                recurring_id = await orderDAL.GetNextRecurringId();
                            }
                            else
                            {
                                latestDBEmiInstallment = Convert.ToInt32(latestEmiInstallmentDetails.installment_number);
                                recurring_id = latestEmiInstallmentDetails.recurring_id;
                            }
                            // Get largest EMI counter whose status = RAISED AND PAID
                            int maxRaisedPaidCounter = emiList
                                .Where(x => x.status != null && x.status.Trim().ToUpper() == "RAISED AND PAID")
                                .Select(x => Convert.ToInt32(x.emiCounter))
                                .DefaultIfEmpty(0)
                                .Max();

                            // If Artemis shows installments processed beyond DB
                            if (maxRaisedPaidCounter >= latestDBEmiInstallment)
                            {
                                int emiCounter = 0;

                                // Insert all missing EMI details into DB
                                for (emiCounter = latestDBEmiInstallment; emiCounter <= maxRaisedPaidCounter; emiCounter++)
                                {
                                    var emiRow = emiList.FirstOrDefault(x =>
                                        Convert.ToInt32(x.emiCounter) == emiCounter);

                                    if (emiRow == null)
                                        continue;

                                    latestEmi = new LatestEmiInstallment
                                    {
                                        recurring_id = recurring_id,
                                        transaction_id = iLEMIValidateInstallmentReq.transaction_id,
                                        trace_id = iLEMIValidateInstallmentReq.trace_id,
                                        proposal_number = iLEMIValidateInstallmentReq.proposal_number,
                                        policy_number = emiRow.endorsementNumber,
                                        installment_number = emiRow.emiCounter,
                                        amount = emiRow.netInstallmentAmount,
                                        status = emiRow.status,
                                        payment_id = emiRow.receiptID,
                                        mandate_id = latestEmiInstallmentDetails.mandate_id,
                                        dps_order_id = latestEmiInstallmentDetails.dps_order_id,
                                        order_id = latestEmiInstallmentDetails.order_id,
                                        payment_mode = emiRow.paymentCategory,
                                        due_date = Convert.ToDateTime(emiRow.installmentDueDate),
                                        execution_date = string.IsNullOrEmpty(emiRow.installmentPaidDate)
                                            ? DateTime.MinValue
                                            : Convert.ToDateTime(emiRow.installmentPaidDate),
                                        grace_end_date = Convert.ToDateTime(emiRow.installmentDueDate).AddMonths(4),
                                        remarks = "OFFLINE PROCESSED",
                                        no_of_attempt = "0",
                                        customer_id = latestEmiInstallmentDetails.customer_id,
                                        application_name = latestEmiInstallmentDetails.application_name
                                    };

                                    // Insert into database
                                    bool installmentUpdated = await orderDAL.upsertEmiInstallmentDetails(latestEmi);
                                    if (!installmentUpdated)
                                    {
                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Error Upserting the Artemis Installment Details.";
                                        response.Data = null;
                                        return response;
                                    }
                                }
                                if ((Convert.ToDecimal(iLEMIValidateInstallmentReq.amount) > Convert.ToDecimal(latestEmi.amount) - 2))
                                {
                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Amount mismatch with Artemis EMI Schedule.";
                                    response.Data = null;
                                    return response;
                                }
                                if (latestEmi.status == "RAISED AND PAID")
                                {
                                    // schedule next emi
                                    if (Convert.ToInt32(latestEmi.installment_number) + 1 <= 12)
                                    {
                                        recurring_id = await orderDAL.GetNextRecurringId();
                                        scheduledEmiInstallment = new LatestEmiInstallment
                                        {
                                            recurring_id = recurring_id,
                                            transaction_id = iLEMIValidateInstallmentReq.transaction_id,
                                            trace_id = iLEMIValidateInstallmentReq.trace_id,
                                            proposal_number = iLEMIValidateInstallmentReq.proposal_number,
                                            policy_number = latestEmiInstallmentDetails.policy_number,
                                            installment_number = (Convert.ToInt32(latestEmi.installment_number) + 1).ToString(),
                                            amount = latestEmiInstallmentDetails.amount,
                                            status = "TO BE RAISED",
                                            payment_id = null,
                                            mandate_id = latestEmiInstallmentDetails.mandate_id,
                                            dps_order_id = latestEmiInstallmentDetails.dps_order_id,
                                            order_id = latestEmiInstallmentDetails.order_id,
                                            payment_mode = latestEmiInstallmentDetails.payment_mode,
                                            due_date = latestEmi.due_date.HasValue
                                                        ? latestEmi.due_date.Value.AddMonths(1)
                                                        : DateTime.Now,
                                            execution_date = null,
                                            grace_end_date = latestEmi.due_date.HasValue
                                                             ? latestEmi.due_date.Value.AddMonths(4)
                                                             : DateTime.Now,
                                            remarks = "",
                                            no_of_attempt = "0",
                                            customer_id = latestEmiInstallmentDetails.customer_id,
                                            application_name = latestEmiInstallmentDetails.application_name
                                        };
                                        bool installmentScheduled = await orderDAL.upsertEmiInstallmentDetails(scheduledEmiInstallment);
                                        if (!installmentScheduled)
                                        {
                                            response.StatusCode = 0;
                                            response.StatusMessage = "Failed";
                                            response.Message = "Error Upserting the Artemis Installment Details.";
                                            response.Data = null;
                                            return response;
                                        }
                                    }
                                    if (requestedInstallment == Convert.ToInt32(scheduledEmiInstallment.installment_number) && (DateTime.Now <= scheduledEmiInstallment.grace_end_date && DateTime.Now >= scheduledEmiInstallment.due_date.Value.AddDays(-2)))
                                    {
                                        // Execute EMI Mandate for the next installment and update status in db to RAISED -> RAISED AND PAID / FAILED -> increment No. Of Attemt
                                        response.StatusCode = 1;
                                        response.StatusMessage = "SUCCESS";
                                        response.Message = "Can proceed with Mandate Execution, with new recurring ID.";
                                        response.Data = "Execute EMI Mandate for the next installment and update status in db to RAISED -> RAISED AND PAID / FAILED -> increment No. Of Attempt";
                                        return response;
                                    }
                                    else if (requestedInstallment < Convert.ToInt32(scheduledEmiInstallment.installment_number))
                                    {
                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Mandate is already Processed Offline for this Installment.";
                                        response.Data = null;
                                        return response;
                                    }
                                    else
                                    {

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Can't process this mandate as Previous installment is not completed.";
                                        response.Data = null;
                                        return response;
                                    }
                                }
                                else if (latestEmi.status == "TO BE RAISED")
                                {

                                    response.StatusCode = 0;
                                    response.StatusMessage = "Failed";
                                    response.Message = "Can't process this mandate as Installment Due Date is not near.";
                                    response.Data = null;
                                    return response;
                                }
                                else if (latestEmi.status == "RAISED")
                                {
                                    if (requestedInstallment == Convert.ToInt32(latestEmi.installment_number) && (DateTime.Now <= latestEmi.grace_end_date && DateTime.Now >= latestEmi.due_date.Value.AddDays(-2)))
                                    {
                                        // Check status with juspay if no. of attempts if > 0
                                        if (Convert.ToInt32(latestEmi.no_of_attempt) > 0)
                                        {
                                            // call juspay check status api 
                                            // if juspay status FAILED // Execute EMI Mandate for the next installment -> increment No. Of Attempt 
                                            if (latestEmi.recurring_id != null)
                                            {
                                                fetchOrderStatusReq.trace_id = iLEMIValidateInstallmentReq.trace_id;
                                                fetchOrderStatusReq.dps_order_id = latestEmi.recurring_id;
                                                juspayOrderStatus = await GetOrderStatus(fetchOrderStatusReq, latestEmiInstallmentDetails.application_name, flow_type);
                                                if (juspayOrderStatus.StatusCode == 1)
                                                {
                                                    if (juspayOrderStatus.Data != null && juspayOrderStatus.Data is FetchOrderStatusResp fetchOrderStatusResp)
                                                    {
                                                        if (fetchOrderStatusResp.status.ToUpper() == "FAILED")
                                                        {
                                                            // Execute EMI Mandate for the next installment -> increment No. Of Attempt 
                                                            response.StatusCode = 1;
                                                            response.StatusMessage = "SUCCESS";
                                                            response.Message = "Can proceed with Mandate Execution, with new recurring ID.";
                                                            response.Data = null;
                                                            return response;
                                                        }
                                                        else if (fetchOrderStatusResp.status.ToUpper() == "PENDING")
                                                        {
                                                            // Execute EMI Mandate for the next installment -> increment No. Of Attempt 
                                                            response.StatusCode = 0;
                                                            response.StatusMessage = "FAILED";
                                                            response.Message = "Mandate Execution is being processed.";
                                                            response.Data = null;
                                                            return response;
                                                        }
                                                        else
                                                        {
                                                            response.StatusCode = 2;
                                                            response.StatusMessage = "SUCCESS";
                                                            response.Message = "Mandate is already Processed for this Installment; Tagging is Required to be done.";
                                                            response.Data = null;
                                                            return response;
                                                        }
                                                    }
                                                }
                                                else
                                                {

                                                    response.StatusCode = 0;
                                                    response.StatusMessage = "Failed";
                                                    response.Message = "Failed to Fetch Juspay Status.";
                                                    response.Data = null;
                                                    return response;
                                                }
                                            }
                                            else
                                            {
                                                // Execute EMI Mandate for the next installment -> increment No. Of Attempt 
                                                response.StatusCode = 1;
                                                response.StatusMessage = "SUCCESS";
                                                response.Message = "Can proceed with Mandate Execution, with new recurring ID.";
                                                response.Data = null;
                                                return response;
                                            }


                                            //captureMandateReq = await orderDAL.GetCaptureMandateReq(iLEMIValidateInstallmentReq);
                                            //mandateExecutionResp = await CaptureMandate(captureMandateReq, flow_type);
                                        }
                                        else
                                        {
                                            // Execute EMI Mandate for the next installment -> increment No. Of Attempt 
                                            response.StatusCode = 1;
                                            response.StatusMessage = "SUCCESS";
                                            response.Message = "Can proceed with Mandate Execution, with new recurring ID.";
                                            response.Data = null;
                                            return response;
                                            //Execute EMI Mandate for the next installment -> increment No.Of Attempt
                                        }

                                        // Tagging the endorsement and update status in db from RAISED -> RAISED AND PAID if success also update payment id
                                    }
                                    else if (requestedInstallment < Convert.ToInt32(latestEmi.installment_number))
                                    {

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Mandate is already Processed Offline for this Installment or not under Installment Payable Duration.";
                                        response.Data = null;
                                        return response;
                                    }
                                    else
                                    {

                                        response.StatusCode = 0;
                                        response.StatusMessage = "Failed";
                                        response.Message = "Can't process this mandate as Previous installment is not completed. or not under Installment Payable Duration.";
                                        response.Data = null;
                                        return response;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderBAL.cs - ILEMICaptureMandate() ",
                    TraceId: captureMandateReq.trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
            }

            return response;
        }

        public async Task<DPSStatusResponse> CreateCustomer(CreateJuspayCustomerReq customerReq, string application_name, string flow_type, string trace_id)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            OrderDAL orderDAL = new OrderDAL();
            PaymentDAL paymentDAL = new PaymentDAL();
            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string merchantId = await orderDAL.GetJuspayMerchantID(application_name);
                if (string.IsNullOrEmpty(merchantId))
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "Failed";
                    response.Message = "Unable to fetch Merchant ID for the Given Application";
                    response.TraceId = trace_id;
                    return response;
                }

                string apiUrl = ApplicationConfig.JUSPAY_CREATE_CUSTOMER_API_URL;

                var client = new RestClient(apiUrl);
                var request = new RestRequest { Method = Method.Post };
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddHeader("x-merchantid", merchantId);
                request.AddHeader("Authorization", "Basic " + ApplicationConfig.JUSPAY_AUTH_TOKEN);

                request.AddParameter("object_reference_id", customerReq.object_reference_id);
                request.AddParameter("mobile_number", customerReq.mobile_number);
                request.AddParameter("email_address", customerReq.email_address);
                request.AddParameter("first_name", customerReq.first_name);
                //request.AddParameter("last_name", customerReq.last_name);
                //request.AddParameter("mobile_country_code", customerReq.mobile_country_code);
                request.AddParameter("options.get_client_auth_token", customerReq.options_get_client_auth_token.ToString().ToLower());

                stopwatch = Stopwatch.StartNew();
                var apiResponse = await client.ExecuteAsync(request);
                stopwatch.Stop();

                var responseTime = stopwatch.Elapsed.TotalSeconds;

                await paymentDAL.LogIntegrationApi(trace_id, "", JsonConvert.SerializeObject(customerReq), apiResponse.Content, apiUrl, "JUSPAY CREATE CUSTOMER", JsonConvert.SerializeObject(apiResponse.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                if (apiResponse != null && apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                {
                    JuspayCustomerResponse customerResponse = JsonSerializer.Deserialize<JuspayCustomerResponse>(apiResponse.Content);

                    await orderDAL.InsertGatewayCustomerDetails(customerReq.mobile_number, customerResponse.id);

                    response.StatusCode = 1;
                    response.StatusMessage = "Success";
                    response.Message = "Customer created successfully";
                    response.TraceId = trace_id;
                    response.Data = customerResponse;
                    return response;
                }

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Juspay customer creation failed";
                response.TraceId = trace_id;
                response.Data = apiResponse?.Content;
                return response;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderBAL.cs - CreateCustomer()",
                    TraceId: trace_id,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);

                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Exception occurred while creating customer";
                response.TraceId = trace_id;
                return response;
            }
        }

        public async Task<DPSStatusResponse> ILEmiExecution(ILEMIValidateInstallmentReq iLEMISubsequentApiReq, string flow_type)
        {
            DPSStatusResponse response = new DPSStatusResponse();
            DPSStatusResponse iLEMIValidateResp = new DPSStatusResponse();
            OrderDAL orderDAL = new OrderDAL();
            MandateExecutionResp mandateExecutionResp = new MandateExecutionResp();
            CaptureMandateReq captureMandateReq = new CaptureMandateReq();
            ApiFactoryInterface apiFactoryInterface = new ApiFactoryInterface();
            string recurring_id = string.Empty;
            FetchOrderStatusReq fetchOrderStatusReq = new FetchOrderStatusReq();
            ArtemisPolicyCreationReq artemisPolicyCreationReq = new ArtemisPolicyCreationReq();
            ArtemisPolicyCreationResp artemisPolicyCreationResp = new ArtemisPolicyCreationResp();
            ArtemisPolicyCreationRespTimeout artemisPolicyCreationRespTimeout = new ArtemisPolicyCreationRespTimeout();
            CallbackBAL callbackBAL = new CallbackBAL();
            FetchPaymentDetails fetchPaymentDetails = new FetchPaymentDetails();
            string receiptNumber = string.Empty;
            string artemisPolicyNumber = string.Empty;
            TagProposalResp tagProposalResp = new TagProposalResp();
            List<SyncProposalToSource> syncProposalToSourceList = new List<SyncProposalToSource>();
            CallbackDAL callbackDAL = new CallbackDAL();
            FetchArtemisProposalDetails fetchArtemisProposalDetails = new FetchArtemisProposalDetails();
            string traceId = string.Empty;
            try
            {
                iLEMIValidateResp = await ILEMIValidateInstallment(iLEMISubsequentApiReq, flow_type);
                if (iLEMIValidateResp.StatusCode == 0)
                {
                    return iLEMIValidateResp;
                }
                else
                {
                    LatestEmiInstallment latestEmiInstallmentDetails = await orderDAL.GetLatestEmiInstallment(iLEMISubsequentApiReq.proposal_number);
                    if (iLEMIValidateResp.StatusCode == 1 && latestEmiInstallmentDetails != null)
                    {
                        traceId = latestEmiInstallmentDetails.trace_id;
                        //CaptureMandate
                        captureMandateReq = await orderDAL.GetCaptureMandateReq(iLEMISubsequentApiReq.transaction_id);
                        if (!string.IsNullOrEmpty(latestEmiInstallmentDetails.recurring_id))
                        {
                            captureMandateReq.order_id = latestEmiInstallmentDetails.recurring_id;
                        }
                        else
                        {
                            captureMandateReq.order_id = await orderDAL.GetNextRecurringId();
                        }
                        captureMandateReq.mandate_id = latestEmiInstallmentDetails.mandate_id;
                        captureMandateReq.amount = iLEMISubsequentApiReq.amount;
                        //captureMandateReq.mandate_execution_date = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
                        captureMandateReq.transaction_id = latestEmiInstallmentDetails.transaction_id;
                        captureMandateReq.trace_id = latestEmiInstallmentDetails.trace_id;
                        await orderDAL.IncrementNoOfRecurringAttempt(captureMandateReq.order_id);
                        mandateExecutionResp = await CaptureMandate(captureMandateReq, flow_type);
                        if (mandateExecutionResp.isSuccess == false)
                        {
                            response.StatusCode = 0;
                            response.StatusMessage = "FAILED";
                            response.Message = "Mandate Execution Failed. Please try again later.";
                            response.TraceId = traceId;
                            response.Data = mandateExecutionResp;
                            return response;
                        }
                        else
                        {
                            response.StatusCode = 1;
                            response.StatusMessage = "SUCCESS";
                            response.Message = "Mandate Execution is being Processed.";
                            response.TraceId = traceId;
                            response.Data = mandateExecutionResp;
                            return response;
                        }
                    }
                    // tagging commented since we will never encounter CHARGED here 
                    #region
                    //tag
                    //fetchPaymentDetails = await callbackDAL.LogPaymentDetails(latestEmiInstallmentDetails.order_id, mandateExecutionResp.auth_code, captureMandateReq.transaction_id, captureMandateReq.trace_id, captureMandateReq.order_id, mandateExecutionResp.status, mandateExecutionResp.mandate_id, latestEmiInstallmentDetails.payment_mode);
                    //artemisPolicyCreationReq = callbackBAL.CreateArtemisWebhookEntryTaggingReqBody(true, fetchPaymentDetails);
                    //if (artemisPolicyCreationReq == null)
                    //{
                    //    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    //        MethodName: "PaymentBAL.cs - ILEMISubsequentApi() ",
                    //        TraceId: iLEMISubsequentApiReq.trace_id,
                    //        Message: "Error while creating request body for Artemis Policy Creation. ILEMISubsequentApi Request: " + iLEMISubsequentApiReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails)), ApplicationConfig.PAYMENT_LOGS);

                    //    response.StatusCode = 0;
                    //    response.StatusMessage = "Failed";
                    //    response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                    //    response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //    response.Data = null;
                    //}
                    //else
                    //{
                    //    // API FACTORY Payment Entry and Tagging API Call
                    //    RestResponse apiResponse = await apiFactoryInterface.GenerateArtemisPolicyData(iLEMISubsequentApiReq.trace_id, artemisPolicyCreationReq, flow_type);

                    //    if (apiResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(apiResponse.Content))
                    //    {
                    //        artemisPolicyCreationResp = JsonConvert.DeserializeObject<ArtemisPolicyCreationResp>(apiResponse.Content);
                    //    }
                    //    else if (apiResponse.StatusCode == HttpStatusCode.GatewayTimeout && !string.IsNullOrEmpty(apiResponse.Content))
                    //    {
                    //        artemisPolicyCreationRespTimeout = JsonConvert.DeserializeObject<ArtemisPolicyCreationRespTimeout>(apiResponse.Content);
                    //    }

                    //    if (artemisPolicyCreationResp != null && artemisPolicyCreationResp.data != null && artemisPolicyCreationResp.statusCode == 200)
                    //    {
                    //        if (artemisPolicyCreationResp.data.receiptStatus.ToUpper() == "SUCCESSFUL")
                    //        {
                    //            receiptNumber = artemisPolicyCreationResp.data.receiptNumber;
                    //            await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);

                    //            if (artemisPolicyCreationResp.data.tagging != null && artemisPolicyCreationResp.data.tagging.Count > 0 && !string.IsNullOrEmpty(artemisPolicyCreationResp.data.tagging[0].policyNumber))
                    //            {
                    //                artemisPolicyNumber = artemisPolicyCreationResp.data.tagging[0].policyNumber;

                    //                SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                    //                {
                    //                    policy_number = artemisPolicyNumber,
                    //                    payment_id = receiptNumber,
                    //                    proposal_number = artemisPolicyCreationResp.data.tagging[0].proposalNumber,
                    //                    reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                    //                    trace_id = fetchPaymentDetails.trace_id,
                    //                    transaction_id = fetchPaymentDetails.transaction_id,
                    //                    application_name = fetchPaymentDetails.application_name,
                    //                    channel_application_details = fetchPaymentDetails.channel_application_details,
                    //                    auth_code = fetchPaymentDetails.auth_code,
                    //                    sync_event = "POLICY_CONFIRMATION"
                    //                };
                    //                syncProposalToSourceList.Add(syncProposalToSource);

                    //                tagProposalResp.PolicyNumber = artemisPolicyNumber;
                    //                response.StatusCode = 1;
                    //                response.StatusMessage = "Success";
                    //                response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                    //                response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //                response.Data = tagProposalResp;
                    //            }
                    //            else
                    //            {
                    //                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    //                    MethodName: "PaymentBAL.cs - TagProposal() ",
                    //                    TraceId: iLEMISubsequentApiReq.trace_id,
                    //                    Message: "Policy Number Not Generated.  ILEMISubsequentApi Request: " + iLEMISubsequentApiReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationResp), ApplicationConfig.PAYMENT_LOGS);

                    //                response.StatusCode = 0;
                    //                response.StatusMessage = "Failed";
                    //                response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                    //                response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //                response.Data = null;
                    //            }

                    //            await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, artemisPolicyNumber);
                    //        }
                    //        else
                    //        {
                    //            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    //                MethodName: "PaymentBAL.cs - TagProposal() ",
                    //                TraceId: iLEMISubsequentApiReq.trace_id,
                    //                Message: "Receipt Number Not Generated.  ILEMISubsequentApi Request: " + iLEMISubsequentApiReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationResp), ApplicationConfig.PAYMENT_LOGS);

                    //            response.StatusCode = 0;
                    //            response.StatusMessage = "Failed";
                    //            response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                    //            response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //            response.Data = null;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    //           MethodName: "PaymentBAL.cs - TagProposal() ",
                    //           TraceId: iLEMISubsequentApiReq.trace_id,
                    //           Message: "Error while calling Artemis Policy Creation API.  ILEMISubsequentApi Request: " + iLEMISubsequentApiReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                    //        response.StatusCode = 0;
                    //        response.StatusMessage = "Failed";
                    //        response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                    //        response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //        response.Data = null;
                    //    }

                    //    if (artemisPolicyCreationRespTimeout != null && artemisPolicyCreationRespTimeout.data != null && artemisPolicyCreationRespTimeout.statusCode == 400)
                    //    {
                    //        if (artemisPolicyCreationRespTimeout.data.taggingDetails != null && artemisPolicyCreationRespTimeout.data.taggingDetails.Count > 0 && !string.IsNullOrEmpty(artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedPolicyNumber) && !string.IsNullOrEmpty(artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedReceiptNumber))
                    //        {
                    //            receiptNumber = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedReceiptNumber;
                    //            artemisPolicyNumber = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedPolicyNumber;

                    //            SyncProposalToSource syncProposalToSource = new SyncProposalToSource()
                    //            {
                    //                policy_number = artemisPolicyNumber,
                    //                payment_id = receiptNumber,
                    //                proposal_number = artemisPolicyCreationRespTimeout.data.taggingDetails[0].taggedProposalNumber,
                    //                reverse_sync_url = fetchPaymentDetails.reverse_sync_url,
                    //                trace_id = fetchPaymentDetails.trace_id,
                    //                transaction_id = fetchPaymentDetails.transaction_id,
                    //                application_name = fetchPaymentDetails.application_name,
                    //                channel_application_details = fetchPaymentDetails.channel_application_details,
                    //                auth_code = fetchPaymentDetails.auth_code,
                    //                sync_event = "POLICY_CONFIRMATION"
                    //            };
                    //            syncProposalToSourceList.Add(syncProposalToSource);

                    //            tagProposalResp.PolicyNumber = artemisPolicyNumber;
                    //            response.StatusCode = 1;
                    //            response.StatusMessage = "Success";
                    //            response.Message = "Your payment has been confirmed. Your policy has been generated, and the Policy Number(s) are provided below.";
                    //            response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //            response.Data = tagProposalResp;
                    //        }
                    //        else
                    //        {
                    //            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    //                MethodName: "PaymentBAL.cs - TagProposal() ",
                    //                TraceId: iLEMISubsequentApiReq.trace_id,
                    //                Message: "Receipt Number / Policy Number Not Generated.  ILEMISubsequentApi Request: " + iLEMISubsequentApiReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq) + "GenerateArtemisPolicyData Response Body: " + artemisPolicyCreationRespTimeout), ApplicationConfig.PAYMENT_LOGS);

                    //            response.StatusCode = 0;
                    //            response.StatusMessage = "Failed";
                    //            response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                    //            response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //            response.Data = null;
                    //        }

                    //        await callbackDAL.UpdateCorePaymentID(receiptNumber, fetchPaymentDetails.transaction_id);
                    //        await callbackDAL.updatePaymentEntryTaggingDetails(fetchPaymentDetails.fetchPaymentProposalList[0].proposal_number, artemisPolicyNumber);
                    //    }
                    //    else
                    //    {
                    //        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    //           MethodName: "PaymentBAL.cs - TagProposal() ",
                    //           TraceId: iLEMISubsequentApiReq.trace_id,
                    //           Message: "Error while calling Artemis Policy Creation API.  ILEMISubsequentApi Request: " + iLEMISubsequentApiReq + " Fetched DB Details: " + JsonConvert.SerializeObject(fetchPaymentDetails) + " GenerateArtemisPolicyData Request Body: " + JsonConvert.SerializeObject(artemisPolicyCreationReq)), ApplicationConfig.PAYMENT_LOGS);

                    //        response.StatusCode = 0;
                    //        response.StatusMessage = "Failed";
                    //        response.Message = "Your payment has been confirmed. Your policy is currently being processed, and we will notify you shortly once it is ready.";
                    //        response.TraceId = iLEMISubsequentApiReq.trace_id;
                    //        response.Data = null;
                    //    }
                    //}
                    #endregion
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "OrderBAL.cs - ILEmiExecution() ",
                    TraceId: traceId,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.ORDER_LOGS);
                response.StatusCode = 0;
                response.StatusMessage = "Failed";
                response.Message = "Some Error Occured.";
                response.TraceId = traceId;
                response.Data = null;
            }
            return response;
        }
        public static string GetProposalTypeFullForm(string proposalType)
        {
            if (string.IsNullOrWhiteSpace(proposalType))
            {
                return null;
            }
            switch (proposalType.Trim().ToUpperInvariant())
            {
                // Health
                case "ELEVATE": return "Elevate";
                case "AB": return "Activate Booster";
                case "HAP": return "Health Advantage Plus";
                case "MAXPROTECT": return "Max Protect";
                case "OPD": return "Out-Patient Department (OPD)";
                case "CASTUP": return "G2R / CASTUP";

                // Motor
                case "TW": return "Two Wheeler";
                case "TWTP": return "Two Wheeler Third Party";
                case "FW": return "Four Wheeler";
                case "FWTP": return "Four Wheeler Third Party";
                case "FWEXW": return "Four Wheeler Extended Warranty";
                case "PP": return "Personal Protect";
                case "SCPA": return "Standalone Compulsory Personal Accident";
                case "GCV": return "Goods Carrying Vehicle";
                case "PCV": return "Passenger Carrying Vehicle";
                case "MISCD": return "MISC-D";

                // Home
                case "CHP": return "Complete Home Protect";

                // Travel
                case "TRIPSECURE": return "International Travel";
                case "AMTDOMESTIC": return "Domestic Travel";

                // SME
                case "MSMEADVANCE": return "MSME Advance";

                default: return null;
            }
        }
    }
}

