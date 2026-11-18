using DigitalPaymentSystem_ILTC.Constants;
using System.Globalization;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class DateFormatter
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public bool TryNormalizeDate(string inputDate, out DateTime parsedDate)
        {
            parsedDate = DateTime.MinValue;

            try
            {
                if (string.IsNullOrEmpty(inputDate))
                {
                    return false;
                }

                string[] acceptedFormats = new[]
                {
                    "dd/MM/yy", "MM/dd/yy", "dd-MM-yyyy", "dd-MMM-yyyy",
                    "yyyy-MM-dd", "dd MMM yyyy", "M/d/yyyy", "dd/MM/yyyy",
                    "d-MMM-yy", "d/M/yyyy", "d/M/yy", "d-MM-yy",
                    "d-M-yyyy", "d-M-yy", "MMM dd, yyyy", "dd MMM, yyyy",
                    "yyyyMMdd", "yyyy/MM/dd", "dd.MM.yyyy",
                    "dddd, dd MMMM yyyy", "ddd, dd MMM yyyy",
                    "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff",
                    "yyyy-MM-dd HH:mm:ss.FFF", "M/d/yyyy h:mm:ss tt", "M/d/yyyy HH:mm:ss"
                };

                return DateTime.TryParseExact(inputDate.Trim(), acceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsedDate);
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "DateFormatter.cs - TryNormalizeDate() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.DATE_FORMATTER_LOGS);
            }

            return false;
        }
    }
}
