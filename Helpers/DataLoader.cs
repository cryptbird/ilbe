using Dapper;
using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Constants;
using Npgsql;
using System.Collections;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public static class DataLoader
    {
        private readonly static CloudWatchLogs objLog = new CloudWatchLogs();
        public static Hashtable KeyValueStore { get; set; } = new Hashtable();
        public static Hashtable DBQueriesKeyValueStore { get; set; } = new Hashtable();

        public static Hashtable LoadKeyValueData()
        {
            Hashtable hash_table = new Hashtable();
            AlertLogsBAL alertBAL = new AlertLogsBAL();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = "SELECT KEY, VALUE FROM " + DatabaseConfig.TABLE_TBL_DPS_KEY_VALUE_MST;
                    var data = connection.Query<(string key, string value)>(query);

                    foreach (var item in data)
                    {
                        hash_table[item.key] = item.value;
                    }
                }
            }
            catch(Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DataLoader.cs - LoadKeyValueData() ", 
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.HASH_REFRESH_LOGS);
            }
            finally
            {
                alertBAL.CallAlert("UAT-Deployed");
            }
            return hash_table;
        }

        public static Hashtable LoadDBQueriesKeyValueData()
        {
            Hashtable hash_table = new Hashtable();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = "SELECT KEY, VALUE FROM " + DatabaseConfig.TABLE_TBL_DPS_DB_QUERIES_KEY_VALUE_MST;
                    var data = connection.Query<(string key, string value)>(query);

                    foreach (var item in data)
                    {
                        hash_table[item.key] = item.value;
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DataLoader.cs - LoadDBQueriesKeyValueData() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.HASH_REFRESH_LOGS);
            }
            return hash_table;
        }

    }

}
