using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Dapper;
using DigitalPaymentSystem_ILTC.Constants;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class CloudWatchLogs
    {
        public async void LogAWSMessageAsync(string message, string streamName = "")
        {
            string logGroupName = string.Empty;
            string logStreamName = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(streamName))
                {
                    streamName = Assembly.GetEntryAssembly().GetName().Name;
                }
                logGroupName = GetLogGroupAndRegion();
                logStreamName = DateTime.Now.ToString("yyyy-MM-dd") + "-" + streamName;
                await CreateLogStreamAsync(logGroupName, logStreamName);
                PutLogEventsRequest request = new PutLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName,
                    LogEvents = new List<InputLogEvent>
                    {
                        new InputLogEvent
                        {
                            Message = message,
                            Timestamp = DateTime.Now
                        }
                    }
                };

                var client = new AmazonCloudWatchLogsClient();
                if (logGroupName.Contains("DR"))
                {
                    client = new AmazonCloudWatchLogsClient(RegionEndpoint.APSouth2);
                }
                else
                {
                    client = new AmazonCloudWatchLogsClient(RegionEndpoint.APSouth1);
                }

                await client.PutLogEventsAsync(request);
            }
            catch (Exception ex)
            {
                WriteLogDB("CloudWatchLogs.cs -> LogAWSMessageAsync() Exception : " + $"Exception occurred: {ex.Message}" + "\r\n" + ex.StackTrace);
            }

        }

        public string GenericAWSException_CWL(string MethodName, string Message, string TraceId = "NA", string StackTrace = "NA", string InnerException = "NA", string AdditionalInfo = "NA")
        {
            string message = string.Empty;

            try
            {
                InnerException = (!string.IsNullOrEmpty(InnerException) ? InnerException : "NA");
                message = $"[{MethodName} Message - {Message}], [{TraceId} TraceId - {TraceId}], [StackTrace - {StackTrace}], [InnerException - {InnerException}], [AdditionalInfo - {AdditionalInfo}]";
            }
            catch (Exception ex)
            {
                WriteLogDB("CloudWatchLogs.cs -> GenericAWSException_CWL() Exception : " + $"Exception occurred: {ex.Message}" + "\r\n" + ex.StackTrace);
            }

            return message;
        }

        public string GetLogGroupAndRegion()
        {
            string CW = string.Empty;

            try
            {
                string CloudWatchLogGroupName = ApplicationConfig.CW_LOG_GROUP_NAME;
                string DRCloudWatchLogGroupName = ApplicationConfig.CW_DR_LOG_GROUP_NAME;
                string containerName = System.Environment.GetEnvironmentVariable("FARGATE_TASK") ?? "UnknownContainer";
                if (containerName.ToUpper().Contains("ILDR"))
                {
                    CW = DRCloudWatchLogGroupName;
                }
                else
                {
                    CW = CloudWatchLogGroupName;
                }
            }
            catch (Exception ex)
            {
                WriteLogDB("CloudWatchLogs.cs -> GetLogGroupAndRegion() Exception : " + $"Exception occurred: {ex.Message}" + "\r\n" + ex.StackTrace);
            }
            return CW;
        }

        public async Task CreateLogStreamAsync(string logGroupName, string logStreamName)
        {
            AmazonCloudWatchLogsClient client = new AmazonCloudWatchLogsClient();

            try
            {    
                if (logGroupName.Contains("DR"))
                {
                    client = new AmazonCloudWatchLogsClient(RegionEndpoint.APSouth2);
                }
                else
                {
                    client = new AmazonCloudWatchLogsClient(RegionEndpoint.APSouth1);
                }

                await client.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName
                });
            }
            catch (Exception ex)
            {
                WriteLogDB("CloudWatchLogs.cs -> CreateLogStreamAsync() Exception : " + $"Exception occurred: {ex.Message}" + "\r\n" + ex.StackTrace);
            }

        }

        public void WriteLogDB(string error_text)
        {
            try
            {
                using (var connection = new Npgsql.NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string server_ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToString();

                    string query = DatabaseConfig.INSERT_CLOUD_WATCH_LOGS;
                    connection.Execute(query, new { server_ip, error_text });
                }
            }
            catch (Exception ex) 
            {
                using (var connection = new Npgsql.NpgsqlConnection(DatabaseConfig.DB_CONN))
                {
                    string server_ip = "error while logging";
                    string query = DatabaseConfig.INSERT_CLOUD_WATCH_LOGS;
                    connection.Execute(query, new { server_ip, error_text });
                }
            }
        }
    }
}
