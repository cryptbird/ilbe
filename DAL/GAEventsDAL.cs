using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Hangfire.Logging.LogProviders;
using Npgsql;
using static DigitalPaymentSystem_ILTC.Models.DropLeadModels;
using static DigitalPaymentSystem_ILTC.Models.GAEventsModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.DAL
{
    public class GAEventsDAL
    {
        private readonly CloudWatchLogs objLog = new CloudWatchLogs();
        public async Task<WebRespBody> GetProposalDetails(string traceID, string transactionID, string product_value)
        {
            WebRespBody webRespBody = new WebRespBody();
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string query = DatabaseConfig.FETCH_GA_PROPOSAL_DETAILS;
                    var targetProduct = product_value?.Split('|')?.FirstOrDefault();

                    if (string.IsNullOrEmpty(targetProduct))
                        return null;

                    var parameters = new
                    {
                        TRANSACTION_ID = transactionID,
                        PRODUCT_VALUE = targetProduct
                    };

                    var result = (await connection.QueryFirstOrDefaultAsync<GAProposalDetails>(query, parameters));

                    if (result == null)
                        return null;

                    webRespBody = new WebRespBody
                    {
                        CustomerName = result.NAME,
                        TransactionType = result.JOURNEY_TYPE,
                        TransactionAffiliation = "Webstore",
                        UserType = "Customer",
                        Amount = Convert.ToDouble(result.TOTAL_AMOUNT),
                        Success = true,
                        StatusCode = 200,
                        CorelationId = traceID,
                        TripType = result.TRIP_TYPE,
                        Policy =
                        new Policy
                        {
                            EmailId = result.EMAIL,
                            ProposalId = result.PROPOSAL_ID,
                            ProposalNo = result.PROPOSAL_NUMBER,
                            PolicyType = result.PROPOSAL_TYPE,
                            PolicyStartDate = result.PROPOSAL_START_DATE?.ToString("yyyy-MM-dd"),
                            PolicyEndDate = result.PROPOSAL_END_DATE?.ToString("yyyy-MM-dd"),
                            PolicyTenure = result.PROPOSAL_TENURE,
                            BasicPremium = Convert.ToDouble(result.BASIC_PREMIUM),
                            Gst = Convert.ToDouble(result.GST),
                            Premium = Convert.ToDouble(result.TOTAL_AMOUNT),
                            DealId = result.DEAL_ID,
                            mobile_number = result.MOBILE_NUMBER,
                            payment_mode = result.PAYMENT_MODE,
                            QuoteId = result.PROPOSAL_ID,
                            ProductDescription = result.PROPOSAL_TYPE,
                            ProductSku = result.DEAL_ID + "|" + result.PRODUCT.Split('_')[0] + "|" + result.JOURNEY_TYPE + "|" + result.PAYMENT_MODE + "|" + "WEBSTORE"
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                       MethodName: "GAEventsDAL.cs - GetProposalDetails()",
                       TraceId: traceID,
                       Message: $"Exception occurred: {ex.Message}",
                       StackTrace: ex.StackTrace),
                       ApplicationConfig.GA_EVENT_LOGS);

                return null;
            }
            return webRespBody;
        }
    }
}
