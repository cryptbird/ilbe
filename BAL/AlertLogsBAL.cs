using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using static DigitalPaymentSystem_ILTC.Models.AlertLogsModel;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.BAL
{
    public class AlertLogsBAL
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<DPSStatusResponse> CallAlert(string alert_event, JsonArray alert_attachment = null, string alert_type = "")
        {
            DPSStatusResponse response = new DPSStatusResponse();
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            PaymentDAL paymentDAL = new PaymentDAL();
            try
            {
                string eventAlertsConfig = ApplicationConfig.EVENT_ALERTS_CONFIG;

                if (string.IsNullOrEmpty(eventAlertsConfig))
                {
                    response.StatusCode = 0;
                    response.StatusMessage = "FAILED";
                    response.Message = "EVENT_ALERTS_CONFIG is not configured";
                    response.Data = null;
                    return response;
                }

                object payload;
                if (alert_event.ToUpper() == "UAT-DEPLOYED")
                {
                    var matchedAlert = new AlertConfig
                    {
                        alert_event = "UAT-DEPLOYED",
                        alert_webhook = eventAlertsConfig
                    };

                    var adaptiveCardJson = @"{
                      ""type"": ""message"",
                      ""attachments"": [
                        {
                          ""contentType"": ""application/vnd.microsoft.card.adaptive"",
                          ""content"": {
                            ""type"": ""AdaptiveCard"",
                            ""body"": [
                              {
                                ""type"": ""TextBlock"",
                                ""text"": ""UAT Deployed""
                              }
                            ],
                            ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
                            ""version"": ""1.0""
                          }
                        }
                      ]
                    }";
                    string requestBody = adaptiveCardJson;

                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        var content = new StringContent(adaptiveCardJson, System.Text.Encoding.UTF8, "application/json");
                        httpResponse = await client.PostAsync(matchedAlert.alert_webhook, content);
                        await paymentDAL.LogIntegrationApi("ALERT", null, requestBody, "Response:" + httpResponse.Content, matchedAlert.alert_webhook, "ALERT", "", "", "");
                    }
                }
                else
                {
                    // Deserialize as List (because JSON is an array)
                    var alertConfigs = JsonSerializer.Deserialize<List<AlertConfig>>(eventAlertsConfig);

                    if (alertConfigs == null || alertConfigs.Count == 0)
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "FAILED";
                        response.Message = "No alert configurations found";
                        response.Data = null;
                        return response;
                    }

                    // Find matching event (case-insensitive)
                    var matchedAlert = alertConfigs
                        .FirstOrDefault(x =>
                            string.Equals(x.alert_event, alert_event, StringComparison.OrdinalIgnoreCase));

                    if (matchedAlert == null)
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "FAILED";
                        response.Message = $"No webhook configured for event: {alert_event}";
                        response.Data = null;
                        return response;
                    }

                    if (string.IsNullOrWhiteSpace(matchedAlert.alert_webhook))
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "FAILED";
                        response.Message = "alert_webhook URL is empty";
                        response.Data = null;
                        return response;
                    }

                    payload = new
                    {
                        type = alert_type,
                        attachments = alert_attachment
                    };

                    string jsonPayload = JsonSerializer.Serialize(payload);

                    using (HttpClient client = new HttpClient())
                    {
                        var content = new StringContent(
                            jsonPayload,
                            System.Text.Encoding.UTF8,
                            "application/json");

                        httpResponse =
                            await client.PostAsync(matchedAlert.alert_webhook, content);
                        
                        await paymentDAL.LogIntegrationApi("ALERT", null, jsonPayload, "Response:" + httpResponse.Content, matchedAlert.alert_webhook, "ALERT", "", "", "");
                    }

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        response.StatusCode = 1;
                        response.StatusMessage = "SUCCESS";
                        response.Message = "Alert sent successfully";
                        response.Data = null;
                    }
                    else
                    {
                        response.StatusCode = 0;
                        response.StatusMessage = "FAILED";
                        response.Message = $"Failed to send alert. Status Code: {httpResponse.StatusCode}";
                        response.Data = null;
                    }
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = 0;
                response.StatusMessage = "FAILED";
                response.Message = $"Exception occurred: {ex.Message}";
                response.Data = null;
            }

            return response;
        }
    }
}