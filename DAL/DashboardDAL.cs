using Amazon.Runtime.Internal.Util;
using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Npgsql;
using Org.BouncyCastle.Asn1.Ocsp;
using static DigitalPaymentSystem_ILTC.Models.DashboardModel;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class DashboardDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<bool> IsValidDashboardAuth(string email)
        {
            bool validFlag = false;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.IS_VALID_DASHBOARD_AUTH;
                    int count = await connection.ExecuteScalarAsync<int>(query, new { EMAIL = email });

                    if (count > 0)
                    {
                        validFlag = true;
                    }
                    else
                    {
                        validFlag = false;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - IsValidDashboardAuth() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }

            return validFlag;
        }

        public async Task<bool> InsertDashboardEventLog(string email, string event_name, string request, string response, string api_endpoint, int status_code)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.INSERT_DASHBOARD_EVENT_LOG;
                    var parameters = new
                    {
                        EMAIL = email,
                        EVENT = event_name,
                        REQUEST = request,
                        RESPONSE = response,
                        API_ENDPOINT = api_endpoint,
                        STATUS_CODE = status_code
                    };
                    var result = await connection.ExecuteAsync(query, parameters);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - InsertDashboardEventLog()",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
                return false;
            }
        }

        public async Task<bool> ValidateDashboardUser(DashboardLoginReq dashboardLoginReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_EMAIL_LOGIN_CHECK;
                    var queryParams = new { EMAIL = dashboardLoginReq.email };
                    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(query, queryParams);
                    return result != null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - ValidateDashboardUser()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace),
                    ApplicationConfig.DASHBOARD_LOGS);

                return false;
            }
        }

        public async Task<bool> UpdateOtp(string otp, string email)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_EMAIL_LOGIN_OTP_SENT;
                    var queryParams = new { LATEST_OTP = otp, EMAIL = email };
                    var result = await connection.ExecuteAsync(query, queryParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - UpdateOtp()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                return false;
            }
        }

        public async Task<bool> ValidateDashboardOtp(DashboardLoginVerifyReq dashboardLoginVerifyReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_EMAIL_VALIDATE_OTP.Replace("##EXPIRY_MINUTES##", ApplicationConfig.DASHBOARD_OTP_EXPIRY_MINUTES);
                    var queryParams = new { EMAIL = dashboardLoginVerifyReq.email, LATEST_OTP = dashboardLoginVerifyReq.otp };
                    var result = await connection.QueryFirstOrDefaultAsync(query, queryParams);
                    return result != null;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - ValidateDashboardOtp()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                return false;
            }
        }

        public async Task<List<TransactionSummaryResp>> FetchTransactionSummary(TransactionSummaryReq transactionSummaryReq)
        {
            List<TransactionSummaryResp> transactionSummaryResList = new List<TransactionSummaryResp>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_TRANSACTION_SUMMARY_VIEW;

                    var queryParams = new { transactionSummaryReq.input_value };
                    var result = await connection.QueryAsync<TransactionSummaryResp>(query.Replace("INPUT_KEY", transactionSummaryReq.input_key), queryParams);
                    transactionSummaryResList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - FetchTransactionSummary() ",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }

            return transactionSummaryResList;
        }

        public async Task<string> GetDashboardRole(string email)
        {
            string role = string.Empty;
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_GET_ROLE;
                    var queryParams = new { EMAIL = email };
                    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(query, queryParams);
                    if (result != null)
                    {
                        role = result.role;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetDashboardRole() ",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }
            return role;
        }

        public async Task<List<string>> GetDatabaseTables()
        {
            List<string> tablesList = new List<string>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_GET_DATABASE_TABLES;

                    var result = await connection.QueryAsync<string>(query);
                    tablesList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetDatabaseTables()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }

            return tablesList;
        }

        public async Task<List<string>> GetTableColumns(string tableName)
        {
            List<string> columnsList = new List<string>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_GET_TABLE_COLUMNS;

                    var queryParams = new { TABLE_NAME = tableName.ToLower() };
                    var result = await connection.QueryAsync<string>(query, queryParams);
                    columnsList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetTableColumns()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }

            return columnsList;
        }

        public async Task<bool> OnboardUser(DashboardOnboardUserReq onboardUserReq)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    // Check if application already exists
                    string checkQuery = DatabaseConfig.DASHBOARD_CHECK_USER_ONBOARDED;
                    var checkParams = new { APPLICATION_NAME = onboardUserReq.application_name };
                    var existingCount = await connection.ExecuteScalarAsync<int>(checkQuery, checkParams);

                    if (existingCount > 0)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "DashboardDAL.cs - OnboardUser()",
                            Message: $"Application '{onboardUserReq.application_name}' already exists",
                            StackTrace: "Duplicate Application"), ApplicationConfig.DASHBOARD_LOGS);
                        return false;
                    }

                    // Insert new application
                    string insertQuery = DatabaseConfig.DASHBOARD_ONBOARD_NEW_USER;

                    var insertParams = new
                    {
                        APPLICATION_NAME = onboardUserReq.application_name,
                        APPLICATION_ID = onboardUserReq.application_id, // Using application_id as password
                        PF_MID = onboardUserReq.pf_mid,
                        ARTEMIS_MID = onboardUserReq.artemis_mid,
                        JUSPAY_MID = onboardUserReq.juspay_mid,
                        REVERSE_SYNC_URL = onboardUserReq.reverse_sync_url
                    };

                    var result = await connection.ExecuteAsync(insertQuery, insertParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - OnboardUser()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
                return false;
            }
        }

        public async Task<List<string>> GetApplicationNames()
        {
            List<string> applicationNamesList = new List<string>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_GET_APPLICATION_NAMES;

                    var result = await connection.QueryAsync<string>(query);
                    applicationNamesList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetApplicationNames()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }

            return applicationNamesList;
        }

        public async Task<List<TemplateIdResp>> GetTemplateIdsByApplicationName(string applicationName)
        {
            List<TemplateIdResp> templateIdsList = new List<TemplateIdResp>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_GET_TEMPLATE_IDS_BY_APPLICATION_NAME;
                    var queryParams = new { APPLICATION_NAME = applicationName };
                    var result = await connection.QueryAsync<TemplateIdResp>(query, queryParams);
                    templateIdsList = result.AsList();
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetTemplateIdsByApplicationName()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
            }

            return templateIdsList;
        }

        public async Task<bool> ToggleTemplateStatus(int templateId, int isActive)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    // Check if template exists
                    string checkQuery = DatabaseConfig.DASHBOARD_CHECK_TEMPLATE_ID_EXISTS;

                    var checkParams = new { TEMPLATE_ID = templateId };
                    var existingCount = await connection.ExecuteScalarAsync<int>(checkQuery, checkParams);

                    if (existingCount == 0)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "DashboardDAL.cs - ToggleTemplateStatus()",
                            Message: $"Template ID '{templateId}' does not exist",
                            StackTrace: "Template Not Found"), ApplicationConfig.DASHBOARD_LOGS);
                        return false;
                    }

                    // Update is_active status
                    string updateQuery = DatabaseConfig.DASHBOARD_TOGGLE_TEMPLATE_STATUS;

                    var updateParams = new
                    {
                        IS_ACTIVE = isActive,
                        TEMPLATE_ID = templateId
                    };

                    var result = await connection.ExecuteAsync(updateQuery, updateParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - ToggleTemplateStatus()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
                return false;
            }
        }

        public async Task<int> GetLatestTemplateId()
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.DASHBOARD_GET_LATEST_TEMPLATE_ID;

                    var result = await connection.ExecuteScalarAsync<int>(query);
                    return result;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - GetLatestTemplateId()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
                return 0;
            }
        }

        public async Task<bool> CreateTemplate(CreateTemplateReq createTemplateReq, int templateId, string returnLoaderUrl, string returnStatusUrl)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    // Check if template already exists for this application and product
                    string checkQuery = DatabaseConfig.DASHBOARD_CHECK_TEMPLATE_EXISTS_FOR_APPLICATION;

                    var checkParams = new
                    {
                        APPLICATION_NAME = createTemplateReq.application_name,
                        PRODUCT = createTemplateReq.product,
                        UPI = createTemplateReq.upi,
                        CREDIT = createTemplateReq.credit,
                        DEBIT = createTemplateReq.debit,
                        NETBANKING = createTemplateReq.netbanking,
                        WALLET = createTemplateReq.wallet,
                        BIMA_ASBA = createTemplateReq.bima_asba,
                        EMI = createTemplateReq.emi
                    };
                    var existingCount = await connection.ExecuteScalarAsync<int>(checkQuery, checkParams);

                    if (existingCount > 0)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "DashboardDAL.cs - CreateTemplate()",
                            Message: $"Template already exists for Application: '{createTemplateReq.application_name}' and Product: '{createTemplateReq.product}'",
                            StackTrace: "Duplicate Template"), ApplicationConfig.DASHBOARD_LOGS);
                        return false;
                    }

                    // Insert new template
                    string insertQuery = DatabaseConfig.DASHBOARD_INSERT_NEW_TEMPLATE;

                    var insertParams = new
                    {
                        TEMPLATE_ID = templateId,
                        PRODUCT = createTemplateReq.product,
                        APPLICATION_NAME = createTemplateReq.application_name,
                        UPI = createTemplateReq.upi,
                        CREDIT = createTemplateReq.credit,
                        DEBIT = createTemplateReq.debit,
                        NETBANKING = createTemplateReq.netbanking,
                        WALLET = createTemplateReq.wallet,
                        BIMA_ASBA = createTemplateReq.bima_asba,
                        EMI = createTemplateReq.emi,
                        IS_ACTIVE = createTemplateReq.is_active,
                        RETURN_LOADER_URL = returnLoaderUrl,
                        RETURN_STATUS_URL = returnStatusUrl
                    };

                    var result = await connection.ExecuteAsync(insertQuery, insertParams);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - CreateTemplate()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
                return false;
            }
        }

        public async Task<ExecuteRefreshResp> UpdateCache(string cache)
        {
            ExecuteRefreshResp executeRefreshResp = new ExecuteRefreshResp();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.UPDATE_CACHE_QUERY;
                    var queryParams = new { VALUE = cache };
                    var result = await connection.ExecuteAsync(query, queryParams);
                    if(result > 0)
                    {
                        executeRefreshResp = await RefreshCache();
                        return executeRefreshResp;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - UpdateCache()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);
                return null;
            }
        }

        public async Task<ExecuteRefreshResp> RefreshCache()
        {
            List<Dictionary<string, object>> resultList = new List<Dictionary<string, object>>();
            ExecuteRefreshResp executeRefreshResp = new ExecuteRefreshResp();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    DataLoader.DBQueriesKeyValueStore = DataLoader.LoadDBQueriesKeyValueData();
                    string cache = DatabaseConfig.REFRESH_CACHE;

                    // Security Check 1: Query must not be empty
                    if (string.IsNullOrEmpty(cache))
                    {
                        executeRefreshResp.cache = "Empty Cache!";
                        executeRefreshResp.rows = null;
                        executeRefreshResp.count = "0";
                    }
                        

                    // Security Check 2: Clean query
                    cache = cache.TrimEnd(';').Trim();
                    string lowerCache = cache.ToLower();

                    string[] dangerousKeywords = {
                        "DROP", "TRUNCATE", "ALTER", "CREATE", "GRANT", "REVOKE",
                        "EXEC", "EXECUTE", "DECLARE", "MERGE", "REPLACE", "RENAME", "COMMENT", "SHUTDOWN", "KILL"
                    };

                    foreach (string keyword in dangerousKeywords)
                    {
                        string pattern = $@"\b{keyword}\b";
                        if (System.Text.RegularExpressions.Regex.IsMatch(cache, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            executeRefreshResp.cache = "Cache Contains Dangerous Functions!";
                            executeRefreshResp.rows = null;
                            executeRefreshResp.count = "0";
                    }

                    string[] dangerousFunctions = {
                        "PG_SLEEP", "DBLINK", "COPY", "LO_IMPORT", "LO_EXPORT",
                        "PG_READ_FILE", "PG_WRITE_FILE", "PG_LS_DIR"
                    };

                    foreach (string function in dangerousFunctions)
                    {
                        if (cache.ToUpper().Contains(function))
                            executeRefreshResp.cache = "Cache Contains Dangerous Functions!" ;
                            executeRefreshResp.rows = null;
                            executeRefreshResp.count = "0";
                    }

                    if ((lowerCache.Contains("update") || lowerCache.Contains("delete")) && !lowerCache.Contains("where"))
                    {
                        executeRefreshResp.cache = "Can't Update/Delete without Where";
                        executeRefreshResp.rows = null;
                        executeRefreshResp.count = "0";
                    }

                    bool isSelectQuery = lowerCache.StartsWith("select");

                    string pgResult = "";

                    if (isSelectQuery)
                    {
                        var result = await connection.QueryAsync(cache);

                        foreach (var row in result)
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (var prop in (IDictionary<string, object>)row)
                            {
                                dict[prop.Key] = prop.Value;
                            }
                            resultList.Add(dict);
                        }

                        pgResult = "Query executed successfully";
                        executeRefreshResp.count = resultList.Count.ToString();
                    }
                    else
                    {
                        int affectedRows = await connection.ExecuteAsync(cache);

                        pgResult = $"{affectedRows} rows affected";
                        executeRefreshResp.count = affectedRows.ToString();
                    }

                    executeRefreshResp.cache = cache + "\n (" + pgResult + ")";
                    executeRefreshResp.rows = resultList;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DashboardDAL.cs - RefreshCache()",
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.DASHBOARD_LOGS);

                executeRefreshResp.cache = "EXCEPTION OCCURED: " + $"Exception occurred: {ex.Message}" +" \n StackTrace: "+ex.StackTrace+" \n" ;
                executeRefreshResp.rows = null;
                executeRefreshResp.count = "0";
            }

            return executeRefreshResp;
        }
    }
}