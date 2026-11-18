using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using static DigitalPaymentSystem_ILTC.Models.DataLoaderModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    [Route("dps-agorax/")]
    public class DataLoaderController : ControllerBase
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        [HttpPost("api/v1/pay/HashRefresh")]
        public ActionResult HashRefresh([FromBody] HashRefreshType hashRefreshType)
        {
            var dict = new Dictionary<string, object>();
            try
            {
                switch (hashRefreshType.RefreshType.ToUpper())
                {
                    case "DATA-MASTER":

                        DataLoader.KeyValueStore = DataLoader.LoadKeyValueData();

                        foreach (DictionaryEntry entry in DataLoader.KeyValueStore)
                        {
                            dict[entry.Key.ToString()] = entry.Value;
                        }

                        break;

                    case "DB-QUERIES-MASTER":

                        DataLoader.DBQueriesKeyValueStore = DataLoader.LoadDBQueriesKeyValueData();

                        foreach (DictionaryEntry entry in DataLoader.DBQueriesKeyValueStore)
                        {
                            dict[entry.Key.ToString()] = entry.Value;
                        }

                        break;

                    default:
                        return BadRequest("Invalid hash refresh type. Valid types are: DATA-MASTER and DB-QUERIES-MASTER");
                }
                return Ok(dict);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "DataLoaderController.cs - HashRefresh() ", 
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.HASH_REFRESH_LOGS);

                return BadRequest("An error occurred while processing your request.");
            }
        }
    }
}
