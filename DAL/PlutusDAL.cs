using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Npgsql;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;
using static DigitalPaymentSystem_ILTC.Models.PlutusModel;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class PlutusDAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<List<string>> GetPlutusTemplateDetails(AddPaymentReq addPaymentReq)
        {
            List<string> onlinePaymentFilters = new List<string>();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
            {
                var templateDetails = await connection.QueryAsync(DatabaseConfig.GET_TEMPLATE_DETAILS, new { TEMPLATE_ID = addPaymentReq.TemplateID });
                if (templateDetails != null && templateDetails.Count() > 0)
                {
                    var templateRow = templateDetails.First();
                    int upi = templateRow.upi;
                    int credit = templateRow.credit;
                    int debit = templateRow.debit;
                    int netbanking = templateRow.netbanking;
                    int wallet = templateRow.wallet;
                    int bima_asba = 0;
                    int emi = templateRow.emi;

                        if (addPaymentReq.Product.Contains("HEALTH") && addPaymentReq.ProposalList.Count == 1 && addPaymentReq.ProposalList[0].is_nstp_flow == "Y" && addPaymentReq.JourneyType.ToUpper() == "ASBA")
                        {
                            bima_asba = templateRow.upi;
                            upi = 0;
                        }
                        // Map payment methods with sequence
                        var paymentMap = new Dictionary<string, int>
                    {
                        { "UPI", upi },
                        { "CREDIT", credit },
                        { "DEBIT", debit },
                        { "NB", netbanking },
                        { "WALLET", wallet },
                        { "EMI", emi },
                        { "BIMA_ASBA", bima_asba }
                    };

                        // Filter > 0 and sort by sequence
                        onlinePaymentFilters = paymentMap
                            .Where(x => x.Value > 0)
                            .OrderBy(x => x.Value)
                            .Select(x => x.Key)
                            .ToList();
                    }
                }

                return onlinePaymentFilters;
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusDAL.cs - GetPlutusTemplateDetails() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
                return null;
            }
        }

        public async Task<GetPlutusOrderStatusData> GetPgNameByTraceId(string trace_id)
        {
            GetPlutusOrderStatusData getPlutusOrderStatusData = new GetPlutusOrderStatusData();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_PG_NAME_BY_TRACE_ID;
                    var parameters = new { TRACE_ID = trace_id };
                    getPlutusOrderStatusData = await connection.QueryFirstOrDefaultAsync<GetPlutusOrderStatusData>(query, parameters);
                    return getPlutusOrderStatusData;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusDAL.cs - GetPgNameByTraceId() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
                return null;
            }
        }

        public async Task<string> GetIsPaymentResyncSent(string trace_id)
        {

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.GET_IS_PAYMENT_RESYNC_SENT;
                    var parameters = new { TRACE_ID = trace_id };
                    var getIsPaymentResyncSent= await connection.QueryFirstOrDefaultAsync(query, parameters);
                    return getIsPaymentResyncSent.is_payment_resync_sent;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusDAL.cs - GetIsPaymentResyncSent() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
                return null;
            }
        }

        public async Task<int> SetIsPaymentResyncSent(string trace_id, string status)
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.SET_IS_PAYMENT_RESYNC_SENT;
                    var parameters = new { TRACE_ID = trace_id, STATUS = status };
                    var updateCount = await connection.ExecuteAsync(query, parameters);
                    return updateCount;
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "PlutusDAL.cs - SetIsPaymentResyncSent() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.PLUTUS_LOGS);
                return 0;

            }
        }
    }
}
