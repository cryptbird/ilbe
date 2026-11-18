using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using DigitalPaymentSystem_ILTC.Constants;
using System.Text.Json;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class SecretManager
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();
        private static readonly string SecretManagerGroup = "UAT/ILTC/Devsecops";
        private static readonly string EchannelOracleSecretManager = "EChannelWebsiteDBConnection";

        public static async Task<string> GetSecretValue(string? key)
        {
            RegionEndpoint regionEndpoint = RegionEndpoint.APSouth1;

            try
            {
                string containerName = System.Environment.GetEnvironmentVariable("FARGATE_TASK") ?? "UnknownContainer";

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                         MethodName: "SecretManager.cs - GetSecretValue() ",
                         Message: containerName), ApplicationConfig.SECRET_MANAGER_LOGS);

                if (containerName.ToUpper().Contains("ILDR"))
                {
                    regionEndpoint = RegionEndpoint.APSouth2;
                }
                else
                {
                    regionEndpoint = RegionEndpoint.APSouth1;
                }

                var client = new AmazonSecretsManagerClient(regionEndpoint);

                var requestForConnectionString = new GetSecretValueRequest
                {
                    SecretId = SecretManagerGroup,
                    VersionStage = "AWSCURRENT"
                };

                var secretValue = await client.GetSecretValueAsync(requestForConnectionString);

                var secretJson = secretValue.SecretString;

                var secretDict = JsonSerializer.Deserialize<Dictionary<string, string>>(secretJson);

                return secretDict[key];
            }

            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "SecretManager.cs - GetSecretValue() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.SECRET_MANAGER_LOGS);
            }
            return "";
        }

        public static async Task<string> GetEchannelOracleSecretValue()
        {
            RegionEndpoint regionEndpoint = RegionEndpoint.APSouth1;

            try
            {
                string containerName = System.Environment.GetEnvironmentVariable("FARGATE_TASK") ?? "UnknownContainer";

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                         MethodName: "SecretManager.cs - GetEchannelOracleSecretValue() ",
                         Message: containerName), ApplicationConfig.SECRET_MANAGER_LOGS);

                if (containerName.ToUpper().Contains("ILDR"))
                {
                    regionEndpoint = RegionEndpoint.APSouth2;
                }
                else
                {
                    regionEndpoint = RegionEndpoint.APSouth1;
                }

                var client = new AmazonSecretsManagerClient(regionEndpoint);

                var requestForConnectionString = new GetSecretValueRequest
                {
                    SecretId = EchannelOracleSecretManager,
                    VersionStage = "AWSCURRENT"
                };

                var secretValue = await client.GetSecretValueAsync(requestForConnectionString);

                return secretValue.SecretString;
            }

            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                   MethodName: "SecretManager.cs - GetEchannelOracleSecretValue() ",
                   TraceId: null,
                   Message: $"Exception occurred: {ex.Message}",
                   StackTrace: ex.StackTrace), ApplicationConfig.SECRET_MANAGER_LOGS);
            }
            return "";
        }
    }
}