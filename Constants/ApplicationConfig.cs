using DigitalPaymentSystem_ILTC.Helpers;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace DigitalPaymentSystem_ILTC.Constants
{
    public class ApplicationConfig
    {
        // ------------------ Stream Name to be used in CloudWatch Logs ------------------
        public static string AUTHENTICATION_LOGS = "AUTHENTICATION_LOGS";
        public static string HASH_REFRESH_LOGS = "HASH_REFRESH_LOGS";
        public static string PAYMENT_LOGS = "PAYMENT_LOGS";
        public static string PENNY_DROP_LOGS = "PENNY_DROP_LOGS";
        public static string BREAKIN_LOGS = "BREAKIN_LOGS";
        public static string ENCRYPT_DECRYPT_LOGS = "ENCRYPT_DECRYPT_LOGS";
        public static string SEND_MESSAGE_LOGS = "SEND_MESSAGE_LOGS";
        public static string ORDER_LOGS = "ORDER_LOGS";
        public static string CORE_PROC_CALLS_LOGS = "CORE_PROC_CALLS_LOGS";
        public static string ILESB_INTERFACE_LOGS = "ILESB_INTERFACE_LOGS";
        public static string CALLBACK_LOGS = "CALLBACK_LOGS";
        public static string RETRY_RESYNC_LOGS = "RETRY_RESYNC_LOGS";
        public static string REFUND_LOGS = "REFUND_LOGS";
        public static string APIFACTORY_INTERFACE_LOGS = "APIFACTORY_INTERFACE_LOGS";
        public static string DASHBOARD_LOGS = "DASHBOARD_LOGS";
        public static string DATE_FORMATTER_LOGS = "DATE_FORMATTER_LOGS";
        public static string MODEL_VALIDATION_LOGS = "MODEL_VALIDATION_LOGS";
        public static string DOWNLOAD_POLICY_PDF_LOGS = "DOWNLOAD_POLICY_PDF_LOGS";
        public static string SECRET_MANAGER_LOGS = "SECRET_MANAGER_LOGS";
        public static string ASBA_PAYMENT_STATUS_LOGS = "ASBA_PAYMENT_STATUS_LOGS";
        public static string DROP_LEAD_LOGS = "DROP_LEAD_LOGS";
        public static string GA_EVENT_LOGS = "GA_EVENT_LOGS";
        public static string PLUTUS_LOGS = "PLUTUS_LOGS";
        public static string ALERT_LOGS = "ALERT_LOGS";
        public static string ILTC_LOGS = "ILTC_LOGS";
        public static string MISC_LOGS = "MISC_LOGS";



        // ------------------ Hash Table Mapping ------------------
        // Log Group Name
        // 1
        public static string CW_LOG_GROUP_NAME = "/ecs/IL_TD_DigitalPaymentSystem_DU";
        // 2
        public static string CW_DR_LOG_GROUP_NAME = "";

        // DPS Token Generation Credentials
        // 3
        public static string JWT_SECRET_KEY => DataLoader.KeyValueStore["JWT_SECRET_KEY"]?.ToString() ?? "EI4gdjlEjZXbntSVDGP8bSYFuzwbgLq35jTAqhBohkPb8Ji1Z5T1qhcjcnmf5pTDev";
        // 4
        public static string JWT_ISSUER => DataLoader.KeyValueStore["JWT_ISSUER"]?.ToString() ?? "https://www.icicilombard.com/digital/v2.0/";
        // 5
        public static string JWT_AUDIENCE => DataLoader.KeyValueStore["JWT_AUDIENCE"]?.ToString() ?? "https://www.icicilombard.com";
        // 6
        public static string TOKEN_EXPIRY_TIME => DataLoader.KeyValueStore["TOKEN_EXPIRY_TIME"]?.ToString() ?? "20";

        // Payment Service Base URL
        // 7
        public static string PAYMENT_BASE_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["PAYMENT_BASE_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital-payment/pay";

        // Encryption Decryption Key
        // 8
        public static string ENCRYPT_DECRYPT_KV => DataLoader.KeyValueStore["ENCRYPT_DECRYPT_KV"]?.ToString() ?? SecretManager.GetSecretValue("dps_encrypt_decrypt_key").Result;

        // SMS Constants
        // 9
        public static string SMS_ILSANCHAR_FLAG => DataLoader.KeyValueStore["SMS_ILSANCHAR_FLAG"]?.ToString() ?? "TRUE";
        // 10
        public static string SMS_ILSANCHAR_DCODE => DataLoader.KeyValueStore["SMS_ILSANCHAR_DCODE"]?.ToString() ?? "ILTakeCarePro";
        // 11
        public static string SMS_ILSANCHAR_SUBUID => DataLoader.KeyValueStore["SMS_ILSANCHAR_SUBUID"]?.ToString() ?? "ILTC";
        // 12
        public static string SMS_ILSANCHAR_FROM => DataLoader.KeyValueStore["SMS_ILSANCHAR_FROM"]?.ToString() ?? "ICICIL";
        // 13
        public static string SMS_ILSANCHAR_PASSWORD => DataLoader.KeyValueStore["SMS_ILSANCHAR_PASSWORD"]?.ToString() ?? "j#6#Y0Z";
        // 14
        public static string SMS_ILSANCHAR_CTYPE => DataLoader.KeyValueStore["SMS_ILSANCHAR_CTYPE"]?.ToString() ?? "1";
        // 15
        public static string SMS_ILSANCHAR_MSGTYPE => DataLoader.KeyValueStore["SMS_ILSANCHAR_MSGTYPE"]?.ToString() ?? "S";
        // 16
        public static string SMS_ILSANCHAR_OTPFLAG => DataLoader.KeyValueStore["SMS_ILSANCHAR_OTPFLAG"]?.ToString() ?? "0";
        // 17
        public static string SMS_ILSANCHAR_URL => DataLoader.KeyValueStore["SMS_ILSANCHAR_URL"]?.ToString() ?? "https://ilsanchar.icicilombard.com/publisher/json6listener";

        // Email Constants
        // 18
        public static string FROM_EMAIL_ID => DataLoader.KeyValueStore["FROM_EMAIL_ID"]?.ToString() ?? "mobileadmin@insurancearticlez.com";
        // 19
        public static string IS_PRIORITY_MAIL_ENABLE => DataLoader.KeyValueStore["IS_PRIORITY_MAIL_ENABLE"]?.ToString() ?? "TRUE";
        // 20
        public static string EMAIL_HOST => DataLoader.KeyValueStore["EMAIL_HOST"]?.ToString() ?? "email-smtp.ap-south-1.amazonaws.com";
        // 21
        public static string EMAIL_PORT => DataLoader.KeyValueStore["EMAIL_PORT"]?.ToString() ?? "587";
        // 22
        public static string EMAIL_NEEDS_AUTHENTICATION => DataLoader.KeyValueStore["EMAIL_NEEDS_AUTHENTICATION"]?.ToString() ?? "TRUE";
        // 23
        public static string EMAIL_USER_ID => DataLoader.KeyValueStore["EMAIL_USER_ID"]?.ToString() ?? SecretManager.GetSecretValue("dps_email_user_id").Result;

        // 24
        public static string EMAIL_PASSWORD => DataLoader.KeyValueStore["EMAIL_PASSWORD"]?.ToString() ?? "BC8zSINUWNp6N8T5bS0MjcnElE4wVissY6qNLQhSkHa1";
        // 25
        public static string EMAIL_SSLON => DataLoader.KeyValueStore["EMAIL_SSLON"]?.ToString() ?? "TRUE";

        // WhatsApp Constants
        // 26
        public static string WHATSAPP_URL => DataLoader.KeyValueStore["WHATSAPP_URL"]?.ToString() ?? "https://cloud.yellow.ai/api/engagements/notifications/v2/push?bot=x1725620362017";
        // 27
        public static string WHATSAPP_API_KEY => DataLoader.KeyValueStore["WHATSAPP_API_KEY"]?.ToString() ?? "QsWrjyDyFdlCD0jpXsZJUX_KkDp7jaBOIzUoWfFV";
        // 28
        public static string WHATSAPP_TYPE => DataLoader.KeyValueStore["WHATSAPP_TYPE"]?.ToString() ?? "whatsapp";
        // 29
        public static string WHATSAPP_SENDER => DataLoader.KeyValueStore["WHATSAPP_SENDER"]?.ToString() ?? "919152162666";
        // 30
        public static string WHATSAPP_EVENT => DataLoader.KeyValueStore["WHATSAPP_EVENT"]?.ToString() ?? "productName,amount,paymentLink,whatsappDeadline,whatsappAttempts,productName,proposalList,policyStartDate,policyEndDate,amount";
        // 31
        public static string WHATSAPP_EVENT_TEMPLATE => DataLoader.KeyValueStore["WHATSAPP_EVENT_TEMPLATE"]?.ToString() ?? "monika_03092025_ech_d_1896_payment_3";
        // 32
        public static string WHATSAPP_SUPPORT_EMAIL => DataLoader.KeyValueStore["WHATSAPP_SUPPORT_EMAIL"]?.ToString() ?? "customersupport@icicilombard.com";
        // 33
        public static string WHATSAPP_SUPPORT_NUMBER => DataLoader.KeyValueStore["WHATSAPP_SUPPORT_NUMBER"]?.ToString() ?? "9311373231";

        // Juspay Session API
        // 34
        public static string JUSPAY_ORDER_API_URL => DataLoader.KeyValueStore["JUSPAY_ORDER_API_URL"]?.ToString() ?? "https://sandbox.juspay.in/orders";
        // 35
        public static string JUSPAY_AUTH_TOKEN => DataLoader.KeyValueStore["JUSPAY_AUTH_TOKEN"]?.ToString() ?? "NTdEMUE3MkMwQjQ0MzIzQTEyQ0E2REUyNTIxNjVE";
        // 36
        public static string JUSPAY_CLIENT_ID => DataLoader.KeyValueStore["JUSPAY_CLIENT_ID"]?.ToString() ?? "digilombard";
        // 37
        public static string JUSPAY_GATEWAY_REFERENCE_ID => DataLoader.KeyValueStore["JUSPAY_GATEWAY_REFERENCE_ID"]?.ToString() ?? "test";
        // 38
        public static string JUSPAY_SESSION_API_URL => DataLoader.KeyValueStore["JUSPAY_SESSION_API_URL"]?.ToString() ?? "https://sandbox.juspay.in/session";
        // 39
        public static string JUSPAY_ACTION => DataLoader.KeyValueStore["JUSPAY_ACTION"]?.ToString() ?? "paymentPage";

        // Oracle PF Connection String
        // 40
        public static string ORACLE_PATH_FINDER => DataLoader.KeyValueStore["ORACLE_PATH_FINDER"]?.ToString() ?? "Data Source=172.30.49.254:1689/GENTEST;User ID=ILTC;Password=iltc#1234;Pooling=true;Min Pool Size=1;Max Pool Size=250;";

        // PathFinder PROCs
        // 41
        public static string PATH_FINDER_HEALTH_SP => DataLoader.KeyValueStore["PATH_FINDER_HEALTH_SP"]?.ToString() ?? "INS.PROC_GET_HEALTH_DATA";
        // 42
        public static string PATH_FINDER_MOTOR_SP => DataLoader.KeyValueStore["PATH_FINDER_MOTOR_SP"]?.ToString() ?? "INS.PROC_GET_MOTOR_DATA";
        // 43
        public static string PATH_FINDER_HOME_SP => DataLoader.KeyValueStore["PATH_FINDER_HOME_SP"]?.ToString() ?? "INS.PROC_GET_HOME_DATA";
        // 44
        public static string PATH_FINDER_TRAVEL_SP => DataLoader.KeyValueStore["PATH_FINDER_TRAVEL_SP"]?.ToString() ?? "INS.PROC_GET_TRAVEL_DATA";
        // 45
        public static string PATH_FINDER_COMMON_SP => DataLoader.KeyValueStore["PATH_FINDER_COMMON_SP"]?.ToString() ?? "INS.PROC_GET_COMMON_DATA";

        // ILESB Token Generation Credentials
        // 46
        public static string ILESB_GRANT_TYPE => DataLoader.KeyValueStore["ILESB_GRANT_TYPE"]?.ToString() ?? "password";
        // 47
        public static string ILESB_TOKEN_USERNAME => DataLoader.KeyValueStore["ILESB_TOKEN_USERNAME"]?.ToString() ?? "DPS_AgoraX";
        // 48
        public static string ILESB_TOKEN_PASSWORD => DataLoader.KeyValueStore["ILESB_TOKEN_PASSWORD"]?.ToString() ?? "tQfqHIJcfmwOENw";
        // 49
        public static string ILESB_CLIENT_ID => DataLoader.KeyValueStore["ILESB_CLIENT_ID"]?.ToString() ?? "DPS_AgoraX";
        // 50
        public static string ILESB_CLIENT_SECRET => DataLoader.KeyValueStore["ILESB_CLIENT_SECRET"]?.ToString() ?? "CYiTMaw3FCuulZeIWQNPnYXe";

        // ILESB Payment Scope
        // 51
        public static string ILESB_PAYMENT_SCOPE => DataLoader.KeyValueStore["ILESB_PAYMENT_SCOPE"]?.ToString() ?? "esbpayment";

        // ILESB Token URL
        // 52
        public static string ILESB_TOKEN_URL => DataLoader.KeyValueStore["ILESB_TOKEN_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/cerberus/connect/token";

        // ILESB Payment Entry And Tagging URL
        // 53
        public static string ILESB_PAYMENT_ENTRY_TAGGING_URL => DataLoader.KeyValueStore["ILESB_PAYMENT_ENTRY_TAGGING_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilservices/payment/v1/payment";

        // API Factory Token Generation Credentials
        // 54
        public static string APIFACTORY_GRANT_TYPE => DataLoader.KeyValueStore["APIFACTORY_GRANT_TYPE"]?.ToString() ?? "client_credentials";
        // 55
        public static string APIFACTORY_CLIENT_ID => DataLoader.KeyValueStore["APIFACTORY_CLIENT_ID"]?.ToString() ?? "fda97068-c43d-44bd-b62f-e13d4fcce61e";
        // 56
        public static string APIFACTORY_CLIENT_SECRET => DataLoader.KeyValueStore["APIFACTORY_CLIENT_SECRET"]?.ToString() ?? "";

        // API Factory Scope
        // 57
        public static string APIFACTORY_SCOPE => DataLoader.KeyValueStore["APIFACTORY_SCOPE"]?.ToString() ?? ".default";

        // API Factory Token URL
        // 58
        public static string APIFACTORY_TOKEN_URL => DataLoader.KeyValueStore["APIFACTORY_TOKEN_URL"]?.ToString() ?? "https://login.microsoftonline.com/bcb33dcc-236e-4431-8beb-bd065435b8da/oauth2/v2.0/token";

        // API Factory Payment Entry And Tagging URL
        // 59
        public static string APIFACTORY_PAYMENT_ENTRY_TAGGING_URL => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_ENTRY_TAGGING_URL"]?.ToString() ?? "https://apifactoryparty.orion.insurancearticlez.com/api/Receipt/CreateReceipt";

        // API Factory Payment Tagging URL
        // 60
        public static string APIFACTORY_PAYMENT_TAGGING_URL => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_TAGGING_URL"]?.ToString() ?? "https://apifactoryparty.orion.insurancearticlez.com/api/Receipt/TagReceipt";

        // API Factory Payment Constants
        // 61
        public static string APIFACTORY_PAYMENT_PAYERTYPE_I => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_PAYERTYPE_I"]?.ToString() ?? "AGENT";
        // 62
        public static string APIFACTORY_PAYMENT_PAYERTYPE_C => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_PAYERTYPE_C"]?.ToString() ?? "POLICY-HOL";
        // 63
        public static string APIFACTORY_PAYMENT_SUB_CODE => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_SUB_CODE"]?.ToString() ?? "";
        // 64
        public static string APIFACTORY_PAYMENT_MODE => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_MODE"]?.ToString() ?? "OP";
        // 65
        public static string APIFACTORY_PAYMENT_OFFICE_CODE => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_OFFICE_CODE"]?.ToString() ?? "HO";

        // CreateBiztalkAuthToken Credentials
        // 66
        public static string BIZTALK_TOKEN_USERNAME => DataLoader.KeyValueStore["BIZTALK_TOKEN_USERNAME"]?.ToString() ?? "MobileUser";
        // 67
        public static string BIZTALK_TOKEN_PASSWORD => DataLoader.KeyValueStore["BIZTALK_TOKEN_PASSWORD"]?.ToString() ?? "M06!L3u$e7U@t";

        // Fetch Proposal Details API URLs for FW and TW
        // 68
        public static string CAR_FETCH_PROPOSAL_URL => DataLoader.KeyValueStore["CAR_FETCH_PROPOSAL_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ILESB/api/Motor/CarPackageFetchProposal";
        // 69
        public static string TW_FETCH_PROPOSAL_URL => DataLoader.KeyValueStore["TW_FETCH_PROPOSAL_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilesb/api/Motor/TWLTFetchProposal";

        // Fetch Customer Details API
        // 70
        public static string GET_CUSTOMER_DETAILS_URL => DataLoader.KeyValueStore["GET_CUSTOMER_DETAILS_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilesb/api/Customer/GetCustomerDetails?id=";

        // ILESB Motor Scope
        // 71
        public static string ILESB_MOTOR_SCOPE => DataLoader.KeyValueStore["ILESB_MOTOR_SCOPE"]?.ToString() ?? "esbmotor";

        // BreakIn Creation URL
        // 72
        public static string BREAKIN_CREATE_URL => DataLoader.KeyValueStore["BREAKIN_CREATE_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilservices/motor/v1/breakin/createbreakinid";

        // BreakIn Creation Constants
        // 73
        public static string BREAKIN_DAYS => DataLoader.KeyValueStore["BREAKIN_DAYS"]?.ToString() ?? "5";
        // 74
        public static string BREAKIN_SALES_REMARKS => DataLoader.KeyValueStore["BREAKIN_SALES_REMARKS"]?.ToString() ?? "Customer Interface";
        // 75
        public static string BREAKIN_TYPE => DataLoader.KeyValueStore["BREAKIN_TYPE"]?.ToString() ?? "Break-in Policy lapse";
        // 76
        public static string BREAKIN_IS_POS_DEALID => DataLoader.KeyValueStore["BREAKIN_IS_POS_DEALID"]?.ToString() ?? "FALSE";

        // BreakIn PF Deviation Sync API URL
        // 77
        public static string BREAKIN_PF_SYNC_API_URL => DataLoader.KeyValueStore["BREAKIN_PF_SYNC_API_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilservices/motor/v1/breakin/clearinspectionstatus";

        // BreakIn Status API URL
        // 78
        public static string BREAKIN_STATUS_API_URL => DataLoader.KeyValueStore["BREAKIN_STATUS_API_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilservices/motor/v1/breakin/checkstatus";

        // IAS URL
        // 79
        public static string IAS_API_URL => DataLoader.KeyValueStore["IAS_API_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilesb/api/pfgeneric/uwapproval";

        // IAS Constants
        // 80
        public static string IAS_APPROVAL_FLAG => DataLoader.KeyValueStore["IAS_APPROVAL_FLAG"]?.ToString() ?? "Approved";
        // 81
        public static string IAS_DEVIATION_TAB => DataLoader.KeyValueStore["IAS_DEVIATION_TAB"]?.ToString() ?? "Other Discounts";
        // 82
        public static string IAS_CHANNEL => DataLoader.KeyValueStore["IAS_CHANNEL"]?.ToString() ?? "echannel";

        // ILESB Voucher Generation API URL
        // 83
        public static string VOUCHER_GENERATION_API_URL => DataLoader.KeyValueStore["VOUCHER_GENERATION_API_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilesb/api/payment/premiumrefund";

        // Penny Drop Juspay Base Url
        // 84
        public static string JUSPAY_PENNY_DROP_API_URL => DataLoader.KeyValueStore["JUSPAY_PENNY_DROP_API_URL"]?.ToString() ?? "https://sandbox.juspay.in/customers/";

        // Juspay Penny Drop Constants
        // 85
        public static string JUSPAY_PENNY_DROP_API_COMMAND => DataLoader.KeyValueStore["JUSPAY_PENNY_DROP_API_COMMAND"]?.ToString() ?? "validate";

        // Products List
        // 86
        public static string TRAVEL_1 => DataLoader.KeyValueStore["TRAVEL_1"]?.ToString() ?? "TRIPSECURE";
        // 87
        public static string HOME_1 => DataLoader.KeyValueStore["HOME_1"]?.ToString() ?? "CHP";
        // 88
        public static string MOTOR_1 => DataLoader.KeyValueStore["MOTOR_1"]?.ToString() ?? "TW|PP";
        // 89
        public static string MOTOR_2 => DataLoader.KeyValueStore["MOTOR_2"]?.ToString() ?? "TWTP";
        // 90
        public static string MOTOR_3 => DataLoader.KeyValueStore["MOTOR_3"]?.ToString() ?? "TW";
        // 91
        public static string MOTOR_4 => DataLoader.KeyValueStore["MOTOR_4"]?.ToString() ?? "FW|PP";
        // 92
        public static string MOTOR_5 => DataLoader.KeyValueStore["MOTOR_5"]?.ToString() ?? "FWTP";
        // 93
        public static string MOTOR_6 => DataLoader.KeyValueStore["MOTOR_6"]?.ToString() ?? "FW";
        // 94
        public static string HEALTH_1 => DataLoader.KeyValueStore["HEALTH_1"]?.ToString() ?? "ELEVATE";
        // 95
        public static string HEALTH_2 => DataLoader.KeyValueStore["HEALTH_2"]?.ToString() ?? "AB";
        // 96
        public static string HEALTH_3 => DataLoader.KeyValueStore["HEALTH_3"]?.ToString() ?? "ELEVATE|AB";
        // 97
        public static string HEALTH_4 => DataLoader.KeyValueStore["HEALTH_4"]?.ToString() ?? "HAP";

        public static Dictionary<string, string> PRODUCTS_LIST => new Dictionary<string, string>
        {
            { "TRAVEL_1", TRAVEL_1 },
            { "HOME_1", HOME_1 },
            { "MOTOR_1", MOTOR_1 },
            { "MOTOR_2", MOTOR_2 },
            { "MOTOR_3", MOTOR_3 },
            { "MOTOR_4", MOTOR_4 },
            { "MOTOR_5", MOTOR_5 },
            { "MOTOR_6", MOTOR_6 },
            { "HEALTH_1", HEALTH_1 },
            { "HEALTH_2", HEALTH_2 },
            { "HEALTH_3", HEALTH_3 },
            { "HEALTH_4", HEALTH_4 },
            { "SME_1", SME_1 },
            { "MOTOR_7", MOTOR_7 },
            { "MOTOR_8", MOTOR_8 },
            { "MOTOR_9", MOTOR_9 },
            { "MOTOR_10", MOTOR_10 },
            { "MOTOR_11", MOTOR_11 },
            { "MOTOR_12", MOTOR_12 },
            { "MOTOR_13", MOTOR_13 },
            { "MOTOR_14", MOTOR_14 },
            { "MOTOR_15", MOTOR_15 },
            { "MOTOR_16", MOTOR_16 },
            { "MOTOR_17", MOTOR_17 },
            { "MOTOR_18", MOTOR_18 },
            { "MOTOR_19", MOTOR_19 },
            { "MOTOR_20", MOTOR_20 },
            { "MOTOR_21", MOTOR_21 },
            { "MOTOR_22", MOTOR_22 },
            { "MOTOR_23", MOTOR_23 },
            { "MOTOR_24", MOTOR_24 },
            { "MOTOR_25", MOTOR_25 },
            { "HEALTH_5", HEALTH_5 },
            { "HEALTH_6", HEALTH_6 },
            { "HEALTH_7", HEALTH_7 },
            { "MISC_1", MISC_1 },
            { "MISC_2", MISC_2 },
            { "MISC_3", MISC_3 },
            { "MISC_4", MISC_4 },
            { "HEALTH_8", HEALTH_8 },
            { "HEALTH_9", HEALTH_9 },
            { "SME_2", SME_2 },
            { "SME_3", SME_3 },
            { "SME_4", SME_4 },
            { "SME_5", SME_5 },
            { "SME_6", SME_6 },
            { "SME_7", SME_7 },
            { "SME_8", SME_8 },
            { "SME_9", SME_9 },
            { "SME_10", SME_10 },
            { "SME_11", SME_11 }
        };

        // 98
        public static string JUSPAY_PAYMENT_FILTER_ALLOW_DEFAULT_OPTIONS => DataLoader.KeyValueStore["JUSPAY_PAYMENT_FILTER_ALLOW_DEFAULT_OPTIONS"]?.ToString() ?? "FALSE";
        // 99
        public static string JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD => DataLoader.KeyValueStore["JUSPAY_PAYMENT_FILTER_ENABLE_PAYMENT_METHOD"]?.ToString() ?? "TRUE";

        // Dashboard Login OTP Content
        // 100
        public static string DASHBOARD_LOGIN_OTP_CONTENT => DataLoader.KeyValueStore["DASHBOARD_LOGIN_OTP_CONTENT"]?.ToString() ?? "<p>Your OTP for <strong>Digital Payment System Dashboard</strong> login is <br><br> <strong>##OTP_HERE##</strong> <br><br> This OTP is valid for ##EXPIRY_MINUTES## minutes. Please do not share this OTP with anyone.</p>";
        // 101
        public static string DASHBOARD_OTP_EXPIRY_MINUTES => DataLoader.KeyValueStore["DASHBOARD_OTP_EXPIRY_MINUTES"]?.ToString() ?? "10";
        // 102
        public static string DASHBOARD_LOGIN_OTP_SUBJECT => DataLoader.KeyValueStore["DASHBOARD_LOGIN_OTP_SUBJECT"]?.ToString() ?? "OTP for Email Verification | AgoraX Dashboard";

        // Juspay Status Mapped Status
        // 103
        public static string JUSPAY_NEW => DataLoader.KeyValueStore["JUSPAY_NEW"]?.ToString() ?? "NEW";
        // 104
        public static string JUSPAY_PENDING_VBV => DataLoader.KeyValueStore["JUSPAY_PENDING_VBV"]?.ToString() ?? "PENDING";
        // 105
        public static string JUSPAY_CHARGED => DataLoader.KeyValueStore["JUSPAY_CHARGED"]?.ToString() ?? "SUCCESS";
        // 106
        public static string JUSPAY_AUTHENTICATION_FAILED => DataLoader.KeyValueStore["JUSPAY_AUTHENTICATION_FAILED"]?.ToString() ?? "FAILED";
        // 107
        public static string JUSPAY_AUTHORIZATION_FAILED => DataLoader.KeyValueStore["JUSPAY_AUTHORIZATION_FAILED"]?.ToString() ?? "FAILED";
        // 108
        public static string JUSPAY_JUSPAY_DECLINED => DataLoader.KeyValueStore["JUSPAY_JUSPAY_DECLINED"]?.ToString() ?? "FAILED";
        // 109
        public static string JUSPAY_AUTHORIZING => DataLoader.KeyValueStore["JUSPAY_AUTHORIZING"]?.ToString() ?? "PENDING";
        // 110
        public static string JUSPAY_COD_INITIATED => DataLoader.KeyValueStore["JUSPAY_COD_INITIATED"]?.ToString() ?? "SUCCESS";
        // 111
        public static string JUSPAY_STARTED => DataLoader.KeyValueStore["JUSPAY_STARTED"]?.ToString() ?? "PENDING";
        // 112
        public static string JUSPAY_AUTO_REFUNDED => DataLoader.KeyValueStore["JUSPAY_AUTO_REFUNDED"]?.ToString() ?? "SUCCESS";
        // 113
        public static string JUSPAY_PARTIAL_CHARGED => DataLoader.KeyValueStore["JUSPAY_PARTIAL_CHARGED"]?.ToString() ?? "SUCCESS";
        // 114
        public static string JUSPAY_AUTHORIZED => DataLoader.KeyValueStore["JUSPAY_AUTHORIZED"]?.ToString() ?? "PENDING";
        // 115
        public static string JUSPAY_CAPTURE_INITIATED => DataLoader.KeyValueStore["JUSPAY_CAPTURE_INITIATED"]?.ToString() ?? "PENDING";
        // 116
        public static string JUSPAY_CAPTURE_FAILED => DataLoader.KeyValueStore["JUSPAY_CAPTURE_FAILED"]?.ToString() ?? "FAILED";
        // 117
        public static string JUSPAY_VOIDED => DataLoader.KeyValueStore["JUSPAY_VOIDED"]?.ToString() ?? "SUCCESS";
        // 118
        public static string JUSPAY_VOID_INITIATED => DataLoader.KeyValueStore["JUSPAY_VOID_INITIATED"]?.ToString() ?? "PENDING";
        // 119
        public static string JUSPAY_VOID_FAILED => DataLoader.KeyValueStore["JUSPAY_VOID_FAILED"]?.ToString() ?? "FAILED";
        // 120
        public static string JUSPAY_NOT_FOUND => DataLoader.KeyValueStore["JUSPAY_NOT_FOUND"]?.ToString() ?? "FAILED";

        public static Dictionary<string, string> DPS_JUSPAY_STATUS_LIST => new Dictionary<string, string>
        {
            { "NEW", JUSPAY_NEW },
            { "PENDING_VBV", JUSPAY_PENDING_VBV },
            { "CHARGED", JUSPAY_CHARGED },
            { "AUTHENTICATION_FAILED", JUSPAY_AUTHENTICATION_FAILED },
            { "AUTHORIZATION_FAILED", JUSPAY_AUTHORIZATION_FAILED },
            { "JUSPAY_DECLINED", JUSPAY_JUSPAY_DECLINED },
            { "AUTHORIZING", JUSPAY_AUTHORIZING },
            { "COD_INITIATED", JUSPAY_COD_INITIATED },
            { "STARTED", JUSPAY_STARTED },
            { "AUTO_REFUNDED", JUSPAY_AUTO_REFUNDED },
            { "PARTIAL_CHARGED", JUSPAY_PARTIAL_CHARGED },
            { "AUTHORIZED", JUSPAY_AUTHORIZED },
            { "CAPTURE_INITIATED", JUSPAY_CAPTURE_INITIATED },
            { "CAPTURE_FAILED", JUSPAY_CAPTURE_FAILED },
            { "VOIDED", JUSPAY_VOIDED },
            { "VOID_INITIATED", JUSPAY_VOID_INITIATED },
            { "VOID_FAILED", JUSPAY_VOID_FAILED },
            { "NOT_FOUND", JUSPAY_NOT_FOUND }
        };

        // BreakIn Creation Time Limit
        // 121
        public static string BREAKIN_CREATION_TIME_LIMIT_MINUTES => DataLoader.KeyValueStore["BREAKIN_CREATION_TIME_LIMIT_MINUTES"]?.ToString() ?? "30";

        // Webhook URL
        // 122
        public static string WEBHOOK_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["WEBHOOK_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital/v2.0/digital-payment-be/api/v1/pay/CallbackWebhook";

        // Fetch PF Proposal Status API URL
        // 123
        public static string FETCH_ILESB_PROPOSAL_STATUS_API_URL => DataLoader.KeyValueStore["FETCH_ILESB_PROPOSAL_STATUS_API_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilesb/api/proposal/getproposalstatus?proposalnumber=";

        // Fetch Proposal Status Scope
        // 124
        public static string ILESB_PROPOSAL_STATUS_SCOPE => DataLoader.KeyValueStore["ILESB_PROPOSAL_STATUS_SCOPE"]?.ToString() ?? "esbproposalstatusv2";

        // Juspay Mandate API URL
        // 125
        public static string JUSPAY_CAPTURE_MANDATE_API_URL => DataLoader.KeyValueStore["JUSPAY_CAPTURE_MANDATE_API_URL"]?.ToString() ?? "https://payments.sandbox.juspay.in/txns";
        // 126
        public static string JUSPAY_REVOKE_MANDATE_API_URL => DataLoader.KeyValueStore["JUSPAY_REVOKE_MANDATE_API_URL"]?.ToString() ?? "https://payments.sandbox.juspay.in/mandates";

        // Juspay Mandate Constants
        // 127
        public static string DEFAULT_MANDATE_PERIOD_IN_DAYS => DataLoader.KeyValueStore["DEFAULT_MANDATE_PERIOD_IN_DAYS"]?.ToString() ?? "14";
        // 128
        public static string JUSPAY_OPTIONS_CREATE_MANDATE => DataLoader.KeyValueStore["JUSPAY_OPTIONS_CREATE_MANDATE"]?.ToString() ?? "REQUIRED";
        // 129
        public static string JUSPAY_MANDATE_BLOCK_FUNDS => DataLoader.KeyValueStore["JUSPAY_MANDATE_BLOCK_FUNDS"]?.ToString() ?? "true";
        // 130
        public static string JUSPAY_MANDATE_FREQUENCY => DataLoader.KeyValueStore["JUSPAY_MANDATE_FREQUENCY"]?.ToString() ?? "ONETIME";
        // 131
        public static string JUSPAY_MANDATE_REVOKABLE_BY_CUSTOMER => DataLoader.KeyValueStore["JUSPAY_MANDATE_REVOKABLE_BY_CUSTOMER"]?.ToString() ?? "false";

        // Fetch Artemis Proposal Status API URL
        // 132
        public static string FETCH_APIFACTORY_PROPOSAL_STATUS_API_URL => DataLoader.KeyValueStore["FETCH_APIFACTORY_PROPOSAL_STATUS_API_URL"]?.ToString() ?? "https://apifactorymiscellaneous.orion.insurancearticlez.com/api/v2/Minimart/GetProposalDataWithProposalNo";
        // 133
        public static string ARTEMIS_MISC_PUBLIC_KEY => DataLoader.KeyValueStore["ARTEMIS_MISC_PUBLIC_KEY"]?.ToString() ?? "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwln2lYZd9cR12cgvbpux0Hcgjm3YIOvdyZfec3SWc4S5eCj7kw2CQXFW6Vzmnvgx/yacbK9yKr8BJ/WHaUhVE1MjAmD6EY2c+Cf+YuiuDuODmvTzQBEW/8fFl3yjm6YgIPgLaL4QpEU9RE/iFLfx5N9m9p67mFCl+s86ufo2FoIWi+ZBFI602FpdrwJhzRm2rnfbt2PG3kFtWI+llPWQ82JJj7eQySjuviEgGlDXGZoy1iSEwytfBIVWATRpR8DpUVhuBvI7GignsVv3qAd8p97CRUfh7Oe9mepah+ooL+dsmb91FNnPyuQ2/xtdMYSZLPylojhbikKMlBs7cHOOWQIDAQAH";

        // Cross Selling Products Mapping
        // 134
        public static string CROSS_SELL_TRAVEL_1 => DataLoader.KeyValueStore["CROSS_SELL_TRAVEL_1"]?.ToString() ?? "HOME_1,MOTOR_2";
        // 135
        public static string CROSS_SELL_HOME_1 => DataLoader.KeyValueStore["CROSS_SELL_HOME_1"]?.ToString() ?? "MOTOR_3,HEALTH_2";
        // 136
        public static string CROSS_SELL_MOTOR_1 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_1"]?.ToString() ?? "HEALTH_3,MOTOR_6,HEALTH_2";
        // 137
        public static string CROSS_SELL_MOTOR_2 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_2"]?.ToString() ?? "TRAVEL_1,HOME_1,HEALTH_3,MOTOR_4";
        // 138
        public static string CROSS_SELL_MOTOR_3 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_3"]?.ToString() ?? "TRAVEL_1,HOME_1,HEALTH_1,MOTOR_5,HEALTH_4";
        // 139
        public static string CROSS_SELL_MOTOR_4 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_4"]?.ToString() ?? "TRAVEL_1,HOME_1,MOTOR_2,HEALTH_2,HEALTH_3,MOTOR_6";
        // 140
        public static string CROSS_SELL_MOTOR_5 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_5"]?.ToString() ?? "TRAVEL_1,HOME_1";
        // 141
        public static string CROSS_SELL_MOTOR_6 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_6"]?.ToString() ?? "TRAVEL_1";
        // 142
        public static string CROSS_SELL_HEALTH_1 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_1"]?.ToString() ?? "TRAVEL_1,HOME_1,MOTOR_3";
        // 143
        public static string CROSS_SELL_HEALTH_2 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_2"]?.ToString() ?? "TRAVEL_1,HOME_1,MOTOR_1,MOTOR_4";
        // 144
        public static string CROSS_SELL_HEALTH_3 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_3"]?.ToString() ?? "TRAVEL_1,HOME_1";
        // 145
        public static string CROSS_SELL_HEALTH_4 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_4"]?.ToString() ?? "TRAVEL_1,HOME_1,MOTOR_6";

        public static Dictionary<string, string> CROSS_SELLING_PRODUCTS_MAPPING => new Dictionary<string, string>
        {
            { "TRAVEL_1", CROSS_SELL_TRAVEL_1 },
            { "HOME_1", CROSS_SELL_HOME_1 },
            { "MOTOR_1", CROSS_SELL_MOTOR_1 },
            { "MOTOR_2", CROSS_SELL_MOTOR_2 },
            { "MOTOR_3", CROSS_SELL_MOTOR_3 },
            { "MOTOR_4", CROSS_SELL_MOTOR_4 },
            { "MOTOR_5", CROSS_SELL_MOTOR_5 },
            { "MOTOR_6", CROSS_SELL_MOTOR_6 },
            { "HEALTH_1", CROSS_SELL_HEALTH_1 },
            { "HEALTH_2", CROSS_SELL_HEALTH_2 },
            { "HEALTH_3", CROSS_SELL_HEALTH_3 },
            { "HEALTH_4", CROSS_SELL_HEALTH_4 },
            { "SME_1", CROSS_SELL_SME_1 },
            { "MOTOR_7", CROSS_SELL_MOTOR_7 },
            { "MOTOR_8", CROSS_SELL_MOTOR_8 },
            { "MOTOR_9", CROSS_SELL_MOTOR_9 },
            { "MOTOR_10", CROSS_SELL_MOTOR_10 },
            { "MOTOR_11", CROSS_SELL_MOTOR_11 },
            { "MOTOR_12", CROSS_SELL_MOTOR_12 },
            { "MOTOR_13", CROSS_SELL_MOTOR_13 },
            { "MOTOR_14", CROSS_SELL_MOTOR_14 },
            { "MOTOR_15", CROSS_SELL_MOTOR_15 },
            { "MOTOR_16", CROSS_SELL_MOTOR_16 },
            { "MOTOR_17", CROSS_SELL_MOTOR_17 },
            { "MOTOR_18", CROSS_SELL_MOTOR_18 },
            { "MOTOR_19", CROSS_SELL_MOTOR_19 },
            { "MOTOR_20", CROSS_SELL_MOTOR_20 },
            { "MOTOR_21", CROSS_SELL_MOTOR_21 },
            { "MOTOR_22", CROSS_SELL_MOTOR_22 },
            { "MOTOR_23", CROSS_SELL_MOTOR_23 },
            { "MOTOR_24", CROSS_SELL_MOTOR_24 },
            { "MOTOR_25", CROSS_SELL_MOTOR_25 },
            { "HEALTH_5", CROSS_SELL_HEALTH_5 },
            { "HEALTH_6", CROSS_SELL_HEALTH_6 },
            { "HEALTH_7", CROSS_SELL_HEALTH_7 },
            { "MISC_1", CROSS_SELL_MISC_1 },
            { "MISC_2", CROSS_SELL_MISC_2 },
            { "MISC_3", CROSS_SELL_MISC_3 },
            { "MISC_4", CROSS_SELL_MISC_4 },
            { "HEALTH_8", CROSS_SELL_HEALTH_8 },
            { "HEALTH_9", CROSS_SELL_HEALTH_9 },
            { "SME_2", CROSS_SELL_SME_2 },
            { "SME_3", CROSS_SELL_SME_3 },
            { "SME_4", CROSS_SELL_SME_4 },
            { "SME_5", CROSS_SELL_SME_5 },
            { "SME_6", CROSS_SELL_SME_6 },
            { "SME_7", CROSS_SELL_SME_7 },
            { "SME_8", CROSS_SELL_SME_8 },
            { "SME_9", CROSS_SELL_SME_9 },
            { "SME_10", CROSS_SELL_SME_10 },
            { "SME_11", CROSS_SELL_SME_11 }
        };

        // 146
        public static string SME_1 => DataLoader.KeyValueStore["SME_1"]?.ToString() ?? "MSMEADVANCE";

        // 147
        public static string CROSS_SELL_SME_1 => DataLoader.KeyValueStore["CROSS_SELL_SME_1"]?.ToString() ?? "HOME_1,MOTOR_1";

        // MSME Reverse Sync Token API Details
        // 148
        public static string SME_REVERSE_SYNC_TOKEN_API_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["SME_REVERSE_SYNC_TOKEN_API_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/business-insurance/digital-sme/v2.0/auth-api/client/initialize";
        // 149
        public static string SME_REVERSE_SYNC_TOKEN_VERSION => DataLoader.KeyValueStore["SME_REVERSE_SYNC_TOKEN_VERSION"]?.ToString() ?? "v6.1.1";
        // 150
        public static string SME_REVERSE_SYNC_TOKEN_PUBLIC_KEY => DataLoader.KeyValueStore["SME_REVERSE_SYNC_TOKEN_PUBLIC_KEY"]?.ToString() ?? "-----BEGIN PUBLIC KEY-----MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAp1C5CT6JJ4FswevuuaDP9cjp6k9G0Gg29wgLFywVLdTnHksjrVFS5TcmyOFu6oKqfuDe15jqTba12XDaWJORnfuqnPFiSxKnK1SZedzlrMpcFvfjfmOTNxXtQYYELfGXZr0JXqig+Zr23VYhgRGhdKZezzJEY5ZPUQ75YHX4IzwUeRTRKfGReDFHahGoY56Z0FlPRoFkrxVz/3wvtDWgPOyVXBHMSVe8jaOS5RHpw3sNm5aKR7ZGCRUBchxGY6bjF1oGcYHWnf/wgqkGjiBfpB7O6k3V3rLM4ZmOM+Ck83/8VrmuPbI1qIY6A27JDZDGHEC0EyVv1CEjwqYP1wUNTwIDAQAB-----END PUBLIC KEY-----";
        // 151
        public static string SME_REVERSE_SYNC_TOKEN_CLIENT => DataLoader.KeyValueStore["SME_REVERSE_SYNC_TOKEN_CLIENT"]?.ToString() ?? "SmeWebSite";
        // 152
        public static string SME_REVERSE_SYNC_TOKEN_SCOPE => DataLoader.KeyValueStore["SME_REVERSE_SYNC_TOKEN_SCOPE"]?.ToString() ?? "0rg5nef2YrRfASZ5jYXkk6DmJlX2";

        // ILESB Download Policy Scope
        // 153
        public static string ILESB_DOWNLOAD_POLICY_SCOPE => DataLoader.KeyValueStore["ILESB_DOWNLOAD_POLICY_SCOPE"]?.ToString() ?? "esbpolicypdf";

        // ILESB Download Policy Copy API URL
        // 154
        public static string PF_POLICY_COPY_DOWNLOAD_URL => DataLoader.KeyValueStore["PF_POLICY_COPY_DOWNLOAD_URL"]?.ToString() ?? "https://internalilesbsanity.insurancearticlez.com/ilservices/misc/v2/generic/policycertificate";

        // API Factory Download Policy Copy API URL
        // 155
        public static string APIFACTORY_POLICY_COPY_DOWNLOAD_URL => DataLoader.KeyValueStore["APIFACTORY_POLICY_COPY_DOWNLOAD_URL"]?.ToString() ?? "https://ilpdfgenerator.orion.insurancearticlez.com/api/v1/SendCommunication/SendCommunication";

        // 156
        public static string NCCN_PROPOSAL_STATUSES => DataLoader.KeyValueStore["NCCN_PROPOSAL_STATUSES"]?.ToString() ?? "NCCN|RCCN|ECCN";
        // 157
        public static string NC_PROPOSAL_STATUSES => DataLoader.KeyValueStore["NC_PROPOSAL_STATUSES"]?.ToString() ?? "NC|RC|EC";
        // 158
        public static string NPVI_PROPOSAL_STATUSES => DataLoader.KeyValueStore["NPVI_PROPOSAL_STATUSES"]?.ToString() ?? "NPVI|RPVI|EPVI";
        // 159
        public static string NPMR_PROPOSAL_STATUSES => DataLoader.KeyValueStore["NPMR_PROPOSAL_STATUSES"]?.ToString() ?? "NPMR|RPMR|EPMR";
        // 160
        public static string NPA_PROPOSAL_STATUSES => DataLoader.KeyValueStore["NPA_PROPOSAL_STATUSES"]?.ToString() ?? "NPA|RPA|EPA";

        public static string[] NCCN_PROPOSAL_STATUS_ARRAY = NCCN_PROPOSAL_STATUSES.Split('|');
        public static string[] NC_PROPOSAL_STATUS_ARRAY = NC_PROPOSAL_STATUSES.Split('|');
        public static string[] NPVI_PROPOSAL_STATUS_ARRAY = NPVI_PROPOSAL_STATUSES.Split('|');
        public static string[] NPMR_PROPOSAL_STATUS_ARRAY = NPMR_PROPOSAL_STATUSES.Split('|');
        public static string[] NPA_PROPOSAL_STATUS_ARRAY = NPA_PROPOSAL_STATUSES.Split('|');
        public static string[] NCN_PROPOSAL_STATUS_ARRAY = NCN_PROPOSAL_STATUSES.Split('|');

        // Fetch Artemis Health Proposal Details API URL
        // 161
        public static string ARTEMIS_HEALTH_PROPOSAL_DETAILS_URL => DataLoader.KeyValueStore["ARTEMIS_HEALTH_PROPOSAL_DETAILS_URL"]?.ToString() ?? "https://apifactoryhealth.orion.insurancearticlez.com/api/v2/HealthProposal/GetDetails";

        // 162
        public static string ARTEMIS_HEALTH_PUBLIC_KEY => DataLoader.KeyValueStore["ARTEMIS_HEALTH_PUBLIC_KEY"]?.ToString() ?? "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCo5O5j9S9ptR7sucXnl7Ro5TwiRviZEqNwNqbPSQVI/dbcD/9iNRmR5fMhoSkLzAS3DoW0A4wjm5l+9LeTuGgV8lTwt+D9PJ+pXrnXOMCfciZORGAqA3TMt7MRh/MN8qrDPrODwaFMHFfecMDj64Opq9/wYOvRRiRouli33OwSpQIDCABB";

        // API FACTORY Download Policy Copy Constants
        // 163
        public static string APIFACTORY_POLICY_DOWNLOAD_TEMPLATE_NAME => DataLoader.KeyValueStore["APIFACTORY_POLICY_DOWNLOAD_TEMPLATE_NAME"]?.ToString() ?? "POLICY_SCHEDULE_DOC";

        // 164
        public static string APIFACTORY_DOWNLOAD_POLICY_ISDOCUMENT_REQD => DataLoader.KeyValueStore["APIFACTORY_DOWNLOAD_POLICY_ISDOCUMENT_REQD"]?.ToString() ?? "Y";

        // 165
        public static string APIFACTORY_DOWNLOAD_POLICY_COMMUNICATION_REQD => DataLoader.KeyValueStore["APIFACTORY_DOWNLOAD_POLICY_COMMUNICATION_REQD"]?.ToString() ?? "N";

        // 166
        public static string BIZTALK_REFUND_USERNAME => DataLoader.KeyValueStore["BIZTALK_REFUND_USERNAME"]?.ToString() ?? "EChannelUser";

        // 167
        public static string BIZTALK_REFUND_PASSWORD => DataLoader.KeyValueStore["BIZTALK_REFUND_PASSWORD"]?.ToString() ?? "ZABHEg5YZgXnMzdn";

        // 168
        public static string MOTOR_NON_NIL_TOKEN_REVERSE_SYNC_USERNAME => DataLoader.KeyValueStore["MOTOR_NON_NIL_TOKEN_REVERSE_SYNC_USERNAME"]?.ToString() ?? "NonnilAuth";

        // 169
        public static string MOTOR_NON_NIL_TOKEN_REVERSE_SYNC_PASSWORD => DataLoader.KeyValueStore["MOTOR_NON_NIL_TOKEN_REVERSE_SYNC_PASSWORD"]?.ToString() ?? "xL9E4CkP7y0mNwZqB8cR4Q==";

        // 170
        public static string ILTC_TOKEN_REVERSE_SYNC_API_URL => DataLoader.KeyValueStore["ILTC_TOKEN_REVERSE_SYNC_API_URL"]?.ToString() ?? "https://api-iltc.insurancearticlez.com/revamp-user/api/v1/CommonRedirection/CreateToken";

        // 171
        public static string ILTC_TOKEN_REVERSE_SYNC_USERNAME => DataLoader.KeyValueStore["ILTC_TOKEN_REVERSE_SYNC_USERNAME"]?.ToString() ?? "AgoraXAuth";

        // 172
        public static string ILTC_TOKEN_REVERSE_SYNC_PASSWORD => DataLoader.KeyValueStore["ILTC_TOKEN_REVERSE_SYNC_PASSWORD"]?.ToString() ?? "ZFkLa1HXFMnN46E7218PcA==";

        // 173
        public static string MOTOR_7 => DataLoader.KeyValueStore["MOTOR_7"]?.ToString() ?? "GCV";
        // 174
        public static string MOTOR_8 => DataLoader.KeyValueStore["MOTOR_8"]?.ToString() ?? "GCV";
        // 175
        public static string MOTOR_9 => DataLoader.KeyValueStore["MOTOR_9"]?.ToString() ?? "PCV";
        // 176
        public static string MOTOR_10 => DataLoader.KeyValueStore["MOTOR_10"]?.ToString() ?? "PCV";
        // 177
        public static string MOTOR_11 => DataLoader.KeyValueStore["MOTOR_11"]?.ToString() ?? "MISCD";
        // 178
        public static string MOTOR_12 => DataLoader.KeyValueStore["MOTOR_12"]?.ToString() ?? "MISCD";
        // 179
        public static string CROSS_SELL_MOTOR_7 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_7"]?.ToString() ?? "HEALTH_3,MOTOR_6,HEALTH_2";
        // 180
        public static string CROSS_SELL_MOTOR_8 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_8"]?.ToString() ?? "TRAVEL_1,HOME_1,HEALTH_3,MOTOR_4";
        // 181
        public static string CROSS_SELL_MOTOR_9 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_9"]?.ToString() ?? "TRAVEL_1,HOME_1,HEALTH_1,MOTOR_5,HEALTH_4";
        // 182
        public static string CROSS_SELL_MOTOR_10 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_10"]?.ToString() ?? "TRAVEL_1,HOME_1,MOTOR_2,HEALTH_2,HEALTH_3,MOTOR_6";
        // 183
        public static string CROSS_SELL_MOTOR_11 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_11"]?.ToString() ?? "TRAVEL_1,HOME_1";
        // 184
        public static string CROSS_SELL_MOTOR_12 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_12"]?.ToString() ?? "TRAVEL_1";

        // 185
        public static string JUSPAY_ORDER_STATUS_WEBHOOK => DataLoader.KeyValueStore["JUSPAY_ORDER_STATUS_WEBHOOK"]?.ToString() ?? "ORDER_SUCCEEDED,ORDER_REFUNDED,ORDER_FAILED,ORDER_REFUND_FAILED,MANDATE_CREATED,MANDATE_ACTIVATED,MANDATE_FAILED,MANDATE_REVOKED";
        // 186
        public static string WEBHOOK_HEADER_CONTENT_TYPE => DataLoader.KeyValueStore["WEBHOOK_HEADER_CONTENT_TYPE"]?.ToString() ?? "Content-Type";
        // 187
        public static string WEBHOOK_HEADER_CONTENT_TYPE_VALUE => DataLoader.KeyValueStore["WEBHOOK_HEADER_CONTENT_TYPE_VALUE"]?.ToString() ?? "application/json";
        // 188
        public static string WEBHOOK_HEADER_ACCEPT => DataLoader.KeyValueStore["WEBHOOK_HEADER_ACCEPT"]?.ToString() ?? "Accept";
        // 189
        public static string WEBHOOK_HEADER_ACCEPT_VALUE => DataLoader.KeyValueStore["WEBHOOK_HEADER_ACCEPT_VALUE"]?.ToString() ?? "application/json, text/plain, */*";
        // 190
        public static string WEBHOOK_HEADER_ACCEPT_LANGUAGE => DataLoader.KeyValueStore["WEBHOOK_HEADER_ACCEPT_LANGUAGE"]?.ToString() ?? "Accept-Language";
        // 191
        public static string WEBHOOK_HEADER_ACCEPT_LANGUAGE_VALUE => DataLoader.KeyValueStore["WEBHOOK_HEADER_ACCEPT_LANGUAGE_VALUE"]?.ToString() ?? "en-US,en;q=0.9";
        // 192
        public static string WEBHOOK_HEADER_USER_AGENT => DataLoader.KeyValueStore["WEBHOOK_HEADER_USER_AGENT"]?.ToString() ?? "User-Agent";
        // 193
        public static string WEBHOOK_HEADER_USER_AGENT_VALUE => DataLoader.KeyValueStore["WEBHOOK_HEADER_USER_AGENT_VALUE"]?.ToString() ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0";
        // 194
        public static string WEBHOOK_HEADER_ORIGIN => DataLoader.KeyValueStore["WEBHOOK_HEADER_ORIGIN"]?.ToString() ?? "Origin";
        // 195
        public static string WEBHOOK_HEADER_ORIGIN_VALUE => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["WEBHOOK_HEADER_ORIGIN_VALUE"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}";
        // 196
        public static string WEBHOOK_HEADER_REFERER => DataLoader.KeyValueStore["WEBHOOK_HEADER_REFERER"]?.ToString() ?? "Referer";
        // 197
        public static string WEBHOOK_HEADER_REFERER_VALUE => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["WEBHOOK_HEADER_REFERER_VALUE"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/";
        // 198
        public static string WEBHOOK_HEADER_SEC_FETCH_SITE => DataLoader.KeyValueStore["WEBHOOK_HEADER_SEC_FETCH_SITE"]?.ToString() ?? "Sec-Fetch-Site";
        // 199
        public static string WEBHOOK_HEADER_SEC_FETCH_SITE_VALUE => DataLoader.KeyValueStore["WEBHOOK_HEADER_SEC_FETCH_SITE_VALUE"]?.ToString() ?? "same-origin";
        // 200
        public static string WEBHOOK_HEADER_SEC_FETCH_MODE => DataLoader.KeyValueStore["WEBHOOK_HEADER_SEC_FETCH_MODE"]?.ToString() ?? "Sec-Fetch-Mode";
        // 201
        public static string WEBHOOK_HEADER_SEC_FETCH_MODE_VALUE => DataLoader.KeyValueStore["WEBHOOK_HEADER_SEC_FETCH_MODE_VALUE"]?.ToString() ?? "cors";
        // 202
        public static string WEBHOOK_HEADER_SEC_FETCH_DEST => DataLoader.KeyValueStore["WEBHOOK_HEADER_SEC_FETCH_DEST"]?.ToString() ?? "Sec-Fetch-Dest";
        // 203
        public static string WEBHOOK_HEADER_SEC_FETCH_DEST_VALUE => DataLoader.KeyValueStore["WEBHOOK_HEADER_SEC_FETCH_DEST_VALUE"]?.ToString() ?? "empty";

        // MSME Product Code
        // 204
        public static string MSME_PRODUCT_CODE => DataLoader.KeyValueStore["MSME_PRODUCT_CODE"]?.ToString() ?? "";

        // 205
        public static string PAYMENT_REFUND_VOUCHER_API_URL => DataLoader.KeyValueStore["PAYMENT_REFUND_VOUCHER_API_URL"]?.ToString() ?? "https://ilesb.icicilombard.com/ilesb/api/payment/paymentrefund";

        // 206
        public static string PAYMENT_REFUND_IL_BUSINESS_LOCATION => DataLoader.KeyValueStore["PAYMENT_REFUND_IL_BUSINESS_LOCATION"]?.ToString() ?? "9999";

        // Fetch Artemis Commercial Proposal Details API URL
        // 207
        public static string ARTEMIS_COMMERCIAL_PROPOSAL_DETAILS_URL => DataLoader.KeyValueStore["ARTEMIS_COMMERCIAL_PROPOSAL_DETAILS_URL"]?.ToString() ?? "https://apifactorycommercial.orion.insurancearticlez.com/api/v2/Proposal/GetDetails";

        // 208
        public static string JUSPAY_REFUND_API_BASE_URL => DataLoader.KeyValueStore["JUSPAY_REFUND_API_BASE_URL"]?.ToString() ?? "https://sandbox.juspay.in/orders/##ORDERID##/refunds";

        // 209
        public static string DASHBOARD_TOKEN_EXPIRY_TIME => DataLoader.KeyValueStore["DASHBOARD_TOKEN_EXPIRY_TIME"]?.ToString() ?? "30";

        // 210
        public static string JUSPAY_MANDATE_REFERENCE_ID => DataLoader.KeyValueStore["JUSPAY_MANDATE_REFERENCE_ID"]?.ToString() ?? "otmupdated";

        // 211
        public static string REQUEST_FAILED_TEXT => DataLoader.KeyValueStore["REQUEST_FAILED_TEXT"]?.ToString() ?? "Unable to Process your Request at the moment!";

        // 212
        public static string ADF0 => DataLoader.KeyValueStore["ADF0"]?.ToString() ?? "Y";

        // 213
        public static string EVENT_ALERTS_CONFIG => DataLoader.KeyValueStore["EVENT_ALERTS_CONFIG"]?.ToString() ?? SecretManager.GetSecretValue("dps_alert_uat_deployed").Result;

        // 214
        public static string UPDATEASBAAUTHCODEPID_API_URL => DataLoader.KeyValueStore["UPDATEASBAAUTHCODEPID_API_URL"]?.ToString() ?? "https://apifactoryparty.orion.insurancearticlez.com/api/Receipt/UpdateASBAAuthcode";

        // 215
        public static string APIFACTORY_PAYMENT_MODE_ASBA => DataLoader.KeyValueStore["APIFACTORY_PAYMENT_MODE_ASBA"]?.ToString() ?? "ASBA";

        // 216
        public static string DROP_LEAD_SME_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["DROP_LEAD_SME_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/business-insurance/digital-sme/v2.0/Sme-customer-api/Lead";

        //217
        public static string GA_EVENT_DATA_WEBSITE_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["GA_EVENT_DATA_WEBSITE_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital/v2.0/payment-api/product/v2/policy-issuance";

        //218
        public static string WEBSITE_AUTH_TOKEN_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["WEBSITE_AUTH_TOKEN_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital/v2.0/auth-api/client/initialize";
        //219
        public static string WEB_TOKEN_CLIENT => DataLoader.KeyValueStore["WEB_TOKEN_CLIENT"]?.ToString() ?? "website";
        // 220
        public static string WEB_TOKEN_SCOPE => DataLoader.KeyValueStore["WEB_TOKEN_SCOPE"]?.ToString() ?? "MvImfz1HGC4UzIbv6oQT4UvKT83p";

        // 221
        public static string PLUTUS_AUTH_TOKEN_API_URL => DataLoader.KeyValueStore["PLUTUS_AUTH_TOKEN_API_URL"]?.ToString() ?? "https://plutus.insurancearticlez.com/payment-gateway/api/v1/Auth/Authenticate";

        // 222
        public static string PLUTUS_AUTH_TOKEN_USERNAME => DataLoader.KeyValueStore["PLUTUS_AUTH_TOKEN_USERNAME"]?.ToString() ?? "2ubE0CqZ/Vps3TyiqE+oig==";

        // 223
        public static string PLUTUS_AUTH_TOKEN_SECRET_KEY => DataLoader.KeyValueStore["PLUTUS_AUTH_TOKEN_SECRET_KEY"]?.ToString() ?? "9SPV+imDpUAHcTKWvosR+g==";

        // 224
        public static string PLUTUS_ORDER_STATUS_API_URL => DataLoader.KeyValueStore["PLUTUS_ORDER_STATUS_API_URL"]?.ToString() ?? "https://plutus.insurancearticlez.com/payment-gateway/api/v1/Payment/GetTransactionStatus";

        // 225
        public static string PLUTUS_ADD_PAYMENT_REQ_API_URL => DataLoader.KeyValueStore["PLUTUS_ADD_PAYMENT_REQ_API_URL"]?.ToString() ?? "https://plutus.insurancearticlez.com/payment-gateway/api/v1/payment/AddPaymentRequest";

        // 226
        public static string PLUTUS_IS_REDIRECT_REQUIRED => DataLoader.KeyValueStore["PLUTUS_IS_REDIRECT_REQUIRED"]?.ToString() ?? "true";

        // 227
        public static string PLUTUS_MANDATE_ACTION_API_URL => DataLoader.KeyValueStore["PLUTUS_MANDATE_ACTION_API_URL"]?.ToString() ?? "https://plutus.insurancearticlez.com/payment-gateway/api/v1/Mandate/Action";

        // Plutus Webhook URL
        // 228
        public static string PLUTUS_WEBHOOK_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["PLUTUS_WEBHOOK_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital/v2.0/digital-payment-be/api/v1/pay/PlutusWebhook";

        // 229
        public static string DEFAULT_AUTOMANDATE_PERIOD_IN_YEARS => DataLoader.KeyValueStore["DEFAULT_AUTOMANDATE_PERIOD_IN_YEARS"]?.ToString() ?? "3";

        // 230
        public static string AUTOMANDATE_FREQUENCY => DataLoader.KeyValueStore["AUTOMANDATE_FREQUENCY"]?.ToString() ?? "ASPRESENTED";

        // 231
        public static string RETURN_LOADER_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["RETURN_LOADER_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital-payment/pay/loader?pgid=";

        // 232
        public static string RETURN_STATUS_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["RETURN_STATUS_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital-payment/pay/payment-result?";

        // 233
        public static string FETCH_PROPOSAL_DETAILS_API_URL => DataLoader.KeyValueStore["FETCH_PROPOSAL_DETAILS_API_URL"]?.ToString() ?? "http://echannelrenewalcore.corp:5000/api/proposal/get-proposal";

        // Payment Service Base URL
        // 234
        public static string SEND_PAYMENT_LINK_BASE_URL => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["SEND_PAYMENT_LINK_BASE_URL"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital-payment/pay/send";

        // 235
        public static string ADF1 => DataLoader.KeyValueStore["ADF1"]?.ToString() ?? "N";

        // 236
        public static string MOTOR_13 => DataLoader.KeyValueStore["MOTOR_13"]?.ToString() ?? "FWEXW";
        // 237
        public static string MOTOR_14 => DataLoader.KeyValueStore["MOTOR_14"]?.ToString() ?? "TW";
        // 238
        public static string MOTOR_15 => DataLoader.KeyValueStore["MOTOR_15"]?.ToString() ?? "TW|PP";
        // 239
        public static string MOTOR_16 => DataLoader.KeyValueStore["MOTOR_16"]?.ToString() ?? "TW|SCPA";
        // 240
        public static string MOTOR_17 => DataLoader.KeyValueStore["MOTOR_17"]?.ToString() ?? "TW|PP|SCPA";
        // 241
        public static string MOTOR_18 => DataLoader.KeyValueStore["MOTOR_18"]?.ToString() ?? "FW";
        // 242
        public static string MOTOR_19 => DataLoader.KeyValueStore["MOTOR_19"]?.ToString() ?? "FW|PP";
        // 243
        public static string MOTOR_20 => DataLoader.KeyValueStore["MOTOR_20"]?.ToString() ?? "FW|SCPA";
        // 244
        public static string MOTOR_21 => DataLoader.KeyValueStore["MOTOR_21"]?.ToString() ?? "FW|PP|SCPA";
        // 245
        public static string MOTOR_22 => DataLoader.KeyValueStore["MOTOR_22"]?.ToString() ?? "GCV";
        // 246
        public static string MOTOR_23 => DataLoader.KeyValueStore["MOTOR_23"]?.ToString() ?? "PCV";
        // 247
        public static string MOTOR_24 => DataLoader.KeyValueStore["MOTOR_24"]?.ToString() ?? "MISCD";
        // 248
        public static string HEALTH_5 => DataLoader.KeyValueStore["HEALTH_5"]?.ToString() ?? "MAXPROTECT";
        // 249
        public static string HEALTH_6 => DataLoader.KeyValueStore["HEALTH_6"]?.ToString() ?? "ELEVATE|OPD";
        // 250
        public static string CROSS_SELL_MOTOR_13 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_13"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 251
        public static string CROSS_SELL_MOTOR_14 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_14"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 252
        public static string CROSS_SELL_MOTOR_15 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_15"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 253
        public static string CROSS_SELL_MOTOR_16 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_16"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 254
        public static string CROSS_SELL_MOTOR_17 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_17"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 255
        public static string CROSS_SELL_MOTOR_18 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_18"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 256
        public static string CROSS_SELL_MOTOR_19 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_19"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 257
        public static string CROSS_SELL_MOTOR_20 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_20"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 258
        public static string CROSS_SELL_MOTOR_21 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_21"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 259
        public static string CROSS_SELL_MOTOR_22 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_22"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 260
        public static string CROSS_SELL_MOTOR_23 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_23"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 261
        public static string CROSS_SELL_MOTOR_24 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_24"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 262
        public static string CROSS_SELL_HEALTH_5 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_5"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 263
        public static string CROSS_SELL_HEALTH_6 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_6"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";

        // 264
        public static string FETCH_PROPOSAL_DETAILS_FWEXW_API_URL => DataLoader.KeyValueStore["FETCH_PROPOSAL_DETAILS_FWEXW_API_URL"]?.ToString() ?? "https://api-iltc.insurancearticlez.com/iltc-buy/api/extendedwarranty/FetchProposalDetails?proposalNo=";

        // 265
        public static string FETCH_PINCODE_DETAILS_API_URL => DataLoader.KeyValueStore["FETCH_PINCODE_DETAILS_API_URL"]?.ToString() ?? "https://echannel.insurancearticlez.com/digital/v2.0/master-api/generic/pincode";

        // 266
        public static string HEALTH_7 => DataLoader.KeyValueStore["HEALTH_7"]?.ToString() ?? "CAST-UP";
        // 267
        public static string CROSS_SELL_HEALTH_7 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_7"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";

        // 268
        public static string PAYMENT_URL_LIFE_HOURS => DataLoader.KeyValueStore["PAYMENT_URL_LIFE_HOURS"]?.ToString() ?? "12";

        // 269
        public static string NCN_PROPOSAL_STATUSES => DataLoader.KeyValueStore["NCN_PROPOSAL_STATUSES"]?.ToString() ?? "NCN|RCN|ECN|REJECTED";

        // 270
        public static string DPS_PROXY_URL => DataLoader.KeyValueStore["DPS_PROXY_URL"]?.ToString() ?? "https://echannel.insurancearticlez.com/digital-payment/pay/gateway?id=";

        // 271
        public static string JUSPAY_OPTIONS_CREATE_ASBA_MANDATE => DataLoader.KeyValueStore["JUSPAY_OPTIONS_CREATE_ASBA_MANDATE"]?.ToString() ?? "OPTIONAL";

        // EMI
        // 272
        public static string JUSPAY_EMI_MANDATE_FREQUENCY => DataLoader.KeyValueStore["JUSPAY_EMI_MANDATE_FREQUENCY"]?.ToString() ?? "ASPRESENTED";

        // 273
        public static string ARTEMIS_EMI_SUMMARY_DETAILS_URL => DataLoader.KeyValueStore["ARTEMIS_EMI_SUMMARY_DETAILS_URL"]?.ToString() ?? "https://apifactoryhealth.orion.insurancearticlez.com/api/HealthProposal/GetEMISummary";

        // JUSPAY
        // 274
        public static string JUSPAY_CREATE_CUSTOMER_API_URL => DataLoader.KeyValueStore["JUSPAY_CREATE_CUSTOMER_API_URL"]?.ToString() ?? "https://sandbox.juspay.in/customers";

        // 275
        public static string SME_1_CALLBACK_REQD => DataLoader.KeyValueStore["SME_1_CALLBACK_REQD"]?.ToString() ?? "Y";
        // 276
        public static string SME_1_CALLBACK_PRODUCT_NAME => DataLoader.KeyValueStore["SME_1_CALLBACK_PRODUCT_NAME"]?.ToString() ?? "MSME-Adv";
        // 277
        public static string SME_1_RM_TEXT => DataLoader.KeyValueStore["SME_1_RM_TEXT"]?.ToString() ?? "We have assigned Vinay as your Relationship Manager to assist you with all your insurance needs. You can reach out to him at vinay@gmail.com|We have assigned Ajay as your Relationship Manager to assist you with all your insurance needs. You can reach out to him at ajay@gmail.com|We have assigned Rohit as your Relationship Manager to assist you with all your insurance needs. You can reach out to her at rohit@gmail.com|We have assigned Kadan as your Relationship Manager to assist you with all your insurance needs. You can reach out to him at kadan@gmail.com|We have assigned Khush as your Relationship Manager to assist you with all your insurance needs. You can reach out to her at khush@gmail.com";


        // 278
        public static string MISC_4 => DataLoader.KeyValueStore["MISC_4"]?.ToString() ?? "PAW";

        // 279
        public static string MISC_3 => DataLoader.KeyValueStore["MISC_3"]?.ToString() ?? "EMISURAKSHA";

        // 280
        public static string MISC_1 => DataLoader.KeyValueStore["MISC_1"]?.ToString() ?? "MOBILEINSURANCE";

        // 281
        public static string MISC_2 => DataLoader.KeyValueStore["MISC_2"]?.ToString() ?? "EXW";

        // 282
        public static string CROSS_SELL_MISC_4 => DataLoader.KeyValueStore["CROSS_SELL_MISC_4"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 283
        public static string CROSS_SELL_MISC_3 => DataLoader.KeyValueStore["CROSS_SELL_MISC_3"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 284
        public static string CROSS_SELL_MISC_1 => DataLoader.KeyValueStore["CROSS_SELL_MISC_1"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";
        // 285
        public static string CROSS_SELL_MISC_2 => DataLoader.KeyValueStore["CROSS_SELL_MISC_2"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";

        // 286
        public static string MOTOR_25 => DataLoader.KeyValueStore["MOTOR_25"]?.ToString() ?? "RYDRAKSHA";
        // 287
        public static string CROSS_SELL_MOTOR_25 => DataLoader.KeyValueStore["CROSS_SELL_MOTOR_25"]?.ToString() ?? "HOME_1,TRAVEL_1,HEALTH_1";

        // 288
        public static string JUSPAY_MERCHANT_ID_UDF1 => DataLoader.KeyValueStore["JUSPAY_MERCHANT_ID_UDF1"]?.ToString() ?? "100000000099010";

        // 289
        public static string FOOTER_CONTENT_URL => DataLoader.KeyValueStore["FOOTER_CONTENT_URL"]?.ToString() ?? "https://echannel.insurancearticlez.com/digital/v2.0/integration-api/sf/get-content?t=revamp-transaction-footer-inner-nextJS";

        // 290
        public static string IS_FOOTER_REQUIRED => DataLoader.KeyValueStore["IS_FOOTER_REQUIRED"]?.ToString() ?? "Y";

        // 291
        public static string IS_HEADER_REQUIRED => DataLoader.KeyValueStore["IS_HEADER_REQUIRED"]?.ToString() ?? "Y";

        // 292
        public static string APIFACTORY_AGENT_DETAILS_URL => DataLoader.KeyValueStore["APIFACTORY_AGENT_DETAILS_URL"]?.ToString() ?? "https://apifactoryparty.orion.insurancearticlez.com/api/Agent?agentCode=";

        // 293
        public static string DOWNLOAD_POLICY_CERTIFICATE => AppConfiguration.Configuration.GetConnectionString("EchannelDomain") + DataLoader.KeyValueStore["DOWNLOAD_POLICY_CERTIFICATE"]?.ToString() ?? $"{AppConfiguration.Configuration.GetConnectionString("EchannelDomain")}/digital/v2.0/digital-payment-be/api/v2/pay/DownloadPolicyCertificate";

        // 294
        public static string HEALTH_8 => DataLoader.KeyValueStore["HEALTH_8"]?.ToString() ?? "HB";

        //295
        public static string HEALTH_9 => DataLoader.KeyValueStore["HEALTH_9"]?.ToString() ?? "VIBE";

        // 296
        public static string CROSS_SELL_HEALTH_8 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_8"]?.ToString() ?? "TRAVEL_1,HOME_1,MOTOR_3";

        // 297
        public static string CROSS_SELL_HEALTH_9 => DataLoader.KeyValueStore["CROSS_SELL_HEALTH_9"]?.ToString() ?? "TRAVEL_1,HOME_1,MOTOR_3";

        // 298
        public static string SME_2 => DataLoader.KeyValueStore["SME_2"]?.ToString() ?? "CARI";
        // 299
        public static string SME_3 => DataLoader.KeyValueStore["SME_3"]?.ToString() ?? "PICA";
        // 300
        public static string SME_4 => DataLoader.KeyValueStore["SME_4"]?.ToString() ?? "PIDR";
        // 301
        public static string SME_5 => DataLoader.KeyValueStore["SME_5"]?.ToString() ?? "WC";
        // 302
        public static string SME_6 => DataLoader.KeyValueStore["SME_6"]?.ToString() ?? "MARINEINLO";
        // 303
        public static string SME_7 => DataLoader.KeyValueStore["SME_7"]?.ToString() ?? "MARINEIMPO";
        // 304
        public static string SME_8 => DataLoader.KeyValueStore["SME_8"]?.ToString() ?? "MARINEEXPO";
        // 305
        public static string SME_9 => DataLoader.KeyValueStore["SME_9"]?.ToString() ?? "MARINEINLS";
        // 306
        public static string SME_10 => DataLoader.KeyValueStore["SME_10"]?.ToString() ?? "MARINEIMPS";
        // 307
        public static string SME_11 => DataLoader.KeyValueStore["SME_11"]?.ToString() ?? "MARINEEXPS";

        // 308
        public static string CROSS_SELL_SME_2 => DataLoader.KeyValueStore["CROSS_SELL_SME_2"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 309
        public static string CROSS_SELL_SME_3 => DataLoader.KeyValueStore["CROSS_SELL_SME_3"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 310
        public static string CROSS_SELL_SME_4 => DataLoader.KeyValueStore["CROSS_SELL_SME_4"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 311
        public static string CROSS_SELL_SME_5 => DataLoader.KeyValueStore["CROSS_SELL_SME_5"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 312
        public static string CROSS_SELL_SME_6 => DataLoader.KeyValueStore["CROSS_SELL_SME_6"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 313
        public static string CROSS_SELL_SME_7 => DataLoader.KeyValueStore["CROSS_SELL_SME_7"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 314
        public static string CROSS_SELL_SME_8 => DataLoader.KeyValueStore["CROSS_SELL_SME_8"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 315
        public static string CROSS_SELL_SME_9 => DataLoader.KeyValueStore["CROSS_SELL_SME_9"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 316
        public static string CROSS_SELL_SME_10 => DataLoader.KeyValueStore["CROSS_SELL_SME_10"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";
        // 317
        public static string CROSS_SELL_SME_11 => DataLoader.KeyValueStore["CROSS_SELL_SME_11"]?.ToString() ?? "MOTOR_1,MOTOR_4,TRAVEL_1,HEALTH_1,HOME_1";

        // SME Product Codes
        // 318
        public static string CARI_PRODUCT_CODE => DataLoader.KeyValueStore["CARI_PRODUCT_CODE"]?.ToString() ?? "";
        // 319
        public static string PICA_PRODUCT_CODE => DataLoader.KeyValueStore["PICA_PRODUCT_CODE"]?.ToString() ?? "";
        // 320
        public static string PIDR_PRODUCT_CODE => DataLoader.KeyValueStore["PIDR_PRODUCT_CODE"]?.ToString() ?? "";
        // 321
        public static string WC_PRODUCT_CODE => DataLoader.KeyValueStore["WC_PRODUCT_CODE"]?.ToString() ?? "";
        // 322
        public static string MARINEINLO_PRODUCT_CODE => DataLoader.KeyValueStore["MARINEINLO_PRODUCT_CODE"]?.ToString() ?? "";
        // 323
        public static string MARINEIMPO_PRODUCT_CODE => DataLoader.KeyValueStore["MARINEIMPO_PRODUCT_CODE"]?.ToString() ?? "";
        // 324
        public static string MARINEEXPO_PRODUCT_CODE => DataLoader.KeyValueStore["MARINEEXPO_PRODUCT_CODE"]?.ToString() ?? "";
        // 325
        public static string MARINEINLS_PRODUCT_CODE => DataLoader.KeyValueStore["MARINEINLS_PRODUCT_CODE"]?.ToString() ?? "";
        // 326
        public static string MARINEIMPS_PRODUCT_CODE => DataLoader.KeyValueStore["MARINEIMPS_PRODUCT_CODE"]?.ToString() ?? "";
        // 327
        public static string MARINEEXPS_PRODUCT_CODE => DataLoader.KeyValueStore["MARINEEXPS_PRODUCT_CODE"]?.ToString() ?? "";
        // 328
        public static string EMI_MODIFY_PROPOSAL_API_URL => DataLoader.KeyValueStore["EMI_MODIFY_PROPOSAL_API_URL"]?.ToString() ?? "http://api-echannelhealthfreshproposal.corp:5000/api/Proposal/EMIModifyProposal";

        // 500 TEST
        public static string TEST_DATA => DataLoader.KeyValueStore["TEST_DATA"]?.ToString();

        // 1000
        public static string ILTC_WIZ_TOKEN_URL => DataLoader.KeyValueStore["ILTC_WIZ_TOKEN_URL"]?.ToString() ?? "https://wizergosruleapp.insurancearticlez.com/wizask/wiz_get_event_task_token/";
        // 1001
        public static string ILTC_WIZ_CLIENT_KEY => DataLoader.KeyValueStore["ILTC_WIZ_CLIENT_KEY"]?.ToString() ?? "dQgRdRegQviyh7qgzYkz4QAcDN5t6RQICyBzjYmKYOsbc4VDiBBrlse0yEo0Biq3nSqdk5E4ieiXMftUG2LUxFKA5KcjExEX4Ro5Rllx8PSCzSlQuDGCrgA0DmuVRPMS";
        // 1002
        public static string ILTC_WIZ_CLIENT_ID => DataLoader.KeyValueStore["ILTC_WIZ_CLIENT_ID"]?.ToString() ?? "42fc4a80-93fe-c508-9e48-688cef59e840";
        // 1003
        public static string ILTC_WIZ_EXECUTE_BASE_URL => DataLoader.KeyValueStore["ILTC_WIZ_EXECUTE_BASE_URL"]?.ToString() ?? "https://wizergosruleapp.insurancearticlez.com/wizask/wiz_execute_event_task/";
        // 1004
        public static string ILTC_WIZ_TASK_ID => DataLoader.KeyValueStore["ILTC_WIZ_TASK_ID"]?.ToString() ?? "4c44856e-7a4a-55de-96a2-4b508ac447f7";
        public static readonly Dictionary<string, List<string>> ProposalToProductsMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AB"] = new() { "HEALTH_2" },
            ["CASTUP"] = new() { "HEALTH_7" },
            ["ELEVATE"] = new() { "HEALTH_1" },
            ["ELEVATE|AB"] = new() { "HEALTH_3" },
            ["ELEVATE|OPD"] = new() { "HEALTH_6" },
            ["HAP"] = new() { "HEALTH_4" },
            ["MAXPROTECT"] = new() { "HEALTH_5" },
            ["HB"] = new() { "HEALTH_8" },
            ["VIBE"] = new() { "HEALTH_9" },

            ["TW"] = new() { "MOTOR_14", "MOTOR_3" },
            ["TW|PP"] = new() { "MOTOR_1", "MOTOR_15" },
            ["TW|SCPA"] = new() { "MOTOR_16" },
            ["TW|PP|SCPA"] = new() { "MOTOR_17" },
            ["TWTP"] = new() { "MOTOR_2" },

            ["FW"] = new() { "MOTOR_18", "MOTOR_6" },
            ["FW|PP"] = new() { "MOTOR_4", "MOTOR_19" },
            ["FW|SCPA"] = new() { "MOTOR_20" },
            ["FW|PP|SCPA"] = new() { "MOTOR_21" },
            ["FWTP"] = new() { "MOTOR_5" },
            ["FWEXW"] = new() { "MOTOR_13" },

            ["GCV"] = new() { "MOTOR_22", "MOTOR_7", "MOTOR_8" },

            ["PCV"] = new() { "MOTOR_23", "MOTOR_9", "MOTOR_10" },

            ["MISCD"] = new() { "MOTOR_24", "MOTOR_11", "MOTOR_12" },

            ["CHP"] = new() { "HOME_1" },

            ["TRIPSECURE"] = new() { "TRAVEL_1" },
            ["AMTDOMESTIC"] = new() { "TRAVEL_2" },
            ["AMTDOMESTIC|TRIPSECURE"] = new() { "TRAVEL_3" },

            ["MSMEADVANCE"] = new() { "SME_1" },
            ["CARI"] = new() { "SME_2" },
            ["PICA"] = new() { "SME_3" },
            ["PIDR"] = new() { "SME_4" },
            ["WC"] = new() { "SME_5" },
            ["MARINEINLO"] = new() { "SME_6" },
            ["MARINEIMPO"] = new() { "SME_7" },
            ["MARINEEXPO"] = new() { "SME_8" },
            ["MARINEINLS"] = new() { "SME_9" },
            ["MARINEIMPS"] = new() { "SME_10" },
            ["MARINEEXPS"] = new() { "SME_11" },

            ["MOBILEINSURANCE"] = new() { "MISC_1" },
            ["EXW"] = new() { "MISC_2" },
            ["EMISURAKSHA"] = new() { "MISC_3" },
            ["PAW"] = new() { "MISC_4" }
        };
    }
}