using DigitalPaymentSystem_ILTC.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class NoHtmlTagsAttribute : ValidationAttribute
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();
        private const string Pattern = @"^[A-Za-z0-9./\-:,'#?@+_=!&()$% ;*~]+$";
        
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string strValue)
            {
                if (Regex.IsMatch(strValue, Pattern))
                {
                    return ValidationResult.Success;
                }
                // Allow null or empty values
                else if (value == null || string.IsNullOrEmpty(value.ToString()))
                {
                    return ValidationResult.Success;
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "ModelValidation.cs - IsValid() ",
                        TraceId: null,
                        Message: $"{validationContext.DisplayName} contains invalid characters.",
                        StackTrace: ""), ApplicationConfig.MODEL_VALIDATION_LOGS);                 
                    return new ValidationResult($"{validationContext.DisplayName} contains invalid characters.");

                }
            }

            return ValidationResult.Success; // If not a string, let it pass (or handle differently)
        }
    }
}
