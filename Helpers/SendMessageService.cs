using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.DAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Newtonsoft.Json;
using RestSharp;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Security.Cryptography;
using static DigitalPaymentSystem_ILTC.Models.SendMessageServiceModel;

namespace ILTakeCare.DAL
{
    public class SendMessageService
    {
        private readonly static CloudWatchLogs objLog = new CloudWatchLogs();

        public async Task<bool> SendSMS(string traceId, string mobileNo, string smsText, string flow_type, string smsType = "")
        {
            bool status = false;
            PaymentDAL paymentDAL = new PaymentDAL();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                if (string.IsNullOrEmpty(mobileNo) || mobileNo.Length != 10)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "SendMessageService.cs - SendSMS() ", 
                        TraceId: traceId,
                        Message: "Invalid mobileNo : " + mobileNo + " Reason : " + "Mobile Number length not equal to 10 for mobileNo : " + mobileNo), ApplicationConfig.SEND_MESSAGE_LOGS);
                    
                    return status;
                }

                if (ApplicationConfig.SMS_ILSANCHAR_FLAG == "TRUE")
                {
                    ILSancharRequest jsonrequest = new ILSancharRequest()
                    {
                        dcode = ApplicationConfig.SMS_ILSANCHAR_DCODE,
                        subuid = ApplicationConfig.SMS_ILSANCHAR_SUBUID,
                        from = ApplicationConfig.SMS_ILSANCHAR_FROM,
                        pwd = ApplicationConfig.SMS_ILSANCHAR_PASSWORD,
                        ctype = ApplicationConfig.SMS_ILSANCHAR_CTYPE,
                        msgtype = ApplicationConfig.SMS_ILSANCHAR_MSGTYPE,
                        to = mobileNo,
                        msgtxt = smsText,
                        OTPFlag = smsType == "OTP" ? "1" : ApplicationConfig.SMS_ILSANCHAR_OTPFLAG
                    };

                    AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);

                    try
                    {
                        AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);

                        string url = ApplicationConfig.SMS_ILSANCHAR_URL;
                        var client = new RestClient(url);
                        var request = new RestRequest(url, Method.Post);

                        request.AddHeader("Content-Type", "application/json");
                        request.AddParameter("application/json", JsonConvert.SerializeObject(jsonrequest), ParameterType.RequestBody);
                        
                        stopwatch = Stopwatch.StartNew();

                        RestResponse response = await client.ExecuteAsync(request);

                        stopwatch.Stop();
                        var responseTime = stopwatch.Elapsed.TotalSeconds;

                        await paymentDAL.LogIntegrationApi(traceId, "", JsonConvert.SerializeObject(jsonrequest), response.Content, url, "ILSANCHAR SMS SERVICE", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                        if (response != null && !string.IsNullOrEmpty(response.Content) && response.StatusCode == HttpStatusCode.OK)
                        {
                            ILSancharResponse objResponse = JsonConvert.DeserializeObject<ILSancharResponse>(response.Content);
                            if (objResponse != null)
                            {
                                if (objResponse.success)
                                {
                                    status = true;
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "SendMessageService.cs - SendSMS() ", 
                                        TraceId: traceId,
                                        Message: "ILSanchar Request is : " + JsonConvert.SerializeObject(jsonrequest) + " and Response is : " + JsonConvert.SerializeObject(objResponse)), ApplicationConfig.SEND_MESSAGE_LOGS);
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "SendMessageService.cs - SendSMS() ",
                                    TraceId: traceId,
                                    Message: "ILSanchar Request is : " + JsonConvert.SerializeObject(jsonrequest) + " and Response is null"), ApplicationConfig.SEND_MESSAGE_LOGS);
                            }
                        }
                        else
                        {
                            objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                MethodName: "SendMessageService.cs - SendSMS() ",
                                TraceId: traceId,
                                Message: "ILSanchar Request is : " + JsonConvert.SerializeObject(jsonrequest) + " and dataStreamResponse is null"), ApplicationConfig.SEND_MESSAGE_LOGS);
                        }
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();

                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "SendMessageService.cs - SendSMS() ",
                            TraceId: traceId,
                            Message: "ILSanchar Issue An exception was caught while sending SMS mobile No: " + mobileNo + " and smsText is : " + smsText + " and Exception : " + $"Exception occurred: {ex.Message}", 
                            StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
                    }
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "SendMessageService.cs - SendSMS() ",
                    TraceId: traceId,
                    Message: "An exception was caught while sending SMS mobile No: " + mobileNo + " and smsText is : " + smsText + " and Exception : " + $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
            }

            return status;
        }

        public bool SendMail(string content, string emailId, string subject, string attachmentPath = null, string attachmentName = null, bool isHighPriority = false, string base64Attachment = "")
        {
            bool status = false;
            string CCemailID = "";
            string BCCemailID = string.Empty;
            FileStream fs = null;
            MemoryStream ms = null;
            try
            {
                MailMessage message = new MailMessage();
                message.From = new MailAddress(ApplicationConfig.FROM_EMAIL_ID);

                //the email id and cc Are joined using '#' as the seperator
                if (emailId.Contains("#"))
                {
                    string[] emails = emailId.Split('#');
                    emailId = emails[0];
                    //CC email Id
                    CCemailID = emails[1];
                }

                //the emailId and BCC are joined using '~' as the seperator
                if (emailId.Contains("~"))
                {
                    string[] emails = emailId.Split('~');
                    emailId = emails[0];
                    // BCC emailId
                    BCCemailID = emails[1];
                }


                if (emailId.Contains(","))
                {
                    string[] emails = emailId.Split(',');
                    foreach (string email in emails)
                    {
                        message.To.Add(new MailAddress(email));
                    }
                }
                else
                {
                    message.To.Add(new MailAddress(emailId));
                }

                if (CCemailID != null && !CCemailID.Equals(""))
                {
                    if (CCemailID.Contains(","))
                    {
                        string[] emails = CCemailID.Split(',');
                        foreach (string email in emails)
                        {
                            message.CC.Add(new MailAddress(email));
                        }
                    }
                    else
                    {
                        message.CC.Add(new MailAddress(CCemailID));
                    }
                }

                if (BCCemailID != null && !BCCemailID.Equals(""))
                {
                    if (BCCemailID.Contains(","))
                    {
                        string[] emails = BCCemailID.Split(',');
                        foreach (string email in emails)
                        {
                            message.Bcc.Add(new MailAddress(email));
                        }
                    }
                    else
                    {
                        message.Bcc.Add(new MailAddress(BCCemailID));
                    }
                }
                if (!string.IsNullOrEmpty(base64Attachment))
                {
                    try
                    {
                        byte[] attachmentBytes = Convert.FromBase64String(base64Attachment);
                        ms = new MemoryStream(attachmentBytes);
                        message.Attachments.Add(new Attachment(ms, attachmentName));
                    }
                    catch (Exception ex)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "SendMessageService.cs - SendMail() Exception 1 while attaching the file for " + emailId,
                            TraceId: null,
                            Message: $"Exception occurred: {ex.Message}", 
                            StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
                    }
                }
                message.Subject = subject;
                message.Body = content;// +EmailHelper.SIGNATURE_TEXT;
                message.IsBodyHtml = true;

                if (isHighPriority)
                    message.Priority = MailPriority.High;

                if (attachmentPath != null)
                {
                    try
                    {
                        fs = new FileStream(@attachmentPath, FileMode.Open, FileAccess.Read, FileShare.Read, 256, FileOptions.None);
                        message.Attachments.Add(new Attachment(fs, attachmentName));
                    }
                    catch (Exception ex)
                    {
                        objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                            MethodName: "SendMessageService.cs - SendMail() Exception 2 while attaching the file for " + emailId,
                            TraceId: null,
                            Message: $"Exception occurred: {ex.Message}", 
                            StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
                    }
                }

                SmtpClient client = new SmtpClient();
                bool IsPriorityMailEnable = Convert.ToBoolean(ApplicationConfig.IS_PRIORITY_MAIL_ENABLE);
                //ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                //if (IsPriorityMailEnable && (subject.ToUpper() == ApplicationConfig.OTP_EMAIL_SUBJECT.ToUpper() || subject.ToUpper() == ApplicationConfig.GetInstance.PROACTIVE_MONITOR_EMAIL_SUBJECT.ToUpper() || subject.ToUpper() == ApplicationConfig.GetInstance.Endorsement_ErrorLogs_Email_Subject.ToUpper()))
                //{
                //    message.From = new MailAddress(ApplicationConfig.GetInstance.FROM_EMAIL_ID_FOR_PRIORITY_EMAILS);
                //    client.Host = ApplicationConfig.GetInstance.EMAIL_HOST_FOR_PRIORITY_EMAILS;
                //    client.Port = ApplicationConfig.GetInstance.EMAIL_PORT_FOR_PRIORITY_EMAILS;
                //}
                //else
                //{
                client.Host = ApplicationConfig.EMAIL_HOST;
                client.Port = Convert.ToInt32(ApplicationConfig.EMAIL_PORT);
                //}

                if (Convert.ToBoolean(ApplicationConfig.EMAIL_NEEDS_AUTHENTICATION))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new System.Net.NetworkCredential(ApplicationConfig.EMAIL_USER_ID, ApplicationConfig.EMAIL_PASSWORD);
                }

                client.EnableSsl = Convert.ToBoolean(ApplicationConfig.EMAIL_SSLON); // Bypass the SSL certificate validation on UAT
                try
                {
                    client.Send(message);
                    status = true;
                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "SendMessageService.cs - SendMail() Exception 1 in sending mail to " + emailId,
                        TraceId: null,
                        Message: $"Exception occurred: {ex.Message}", 
                        StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "SendMessageService.cs - SendMail() Exception 2 in sending mail to " + emailId,
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
            }
            finally
            {
                if (null != fs)
                {
                    fs.Close();
                }
            }

            return status;
        }

        public async Task<bool> SendWhatsApp(string traceId, string mobileNo, WhatsAppDynamicParams whatsappCommunicationUIreq, string flow_type, string attachmentBase64 = "")
        {
            bool status = false;
            PaymentDAL paymentDAL = new PaymentDAL();

            Stopwatch stopwatch = new Stopwatch();

            try
            {
                string URL = ApplicationConfig.WHATSAPP_URL;
                var client = new RestSharp.RestClient(URL);
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-api-key", ApplicationConfig.WHATSAPP_API_KEY);

                WhatsappReqModel whatsappReqModel = new WhatsappReqModel();
                whatsappReqModel.userDetails = new Userdetails();
                whatsappReqModel.notification = new Notification();
                whatsappReqModel.notification.@params = new Params();
                if (!String.IsNullOrEmpty(mobileNo))
                {
                    whatsappReqModel.userDetails.number = mobileNo;
                }
                whatsappReqModel.notification.type = ApplicationConfig.WHATSAPP_TYPE;
                whatsappReqModel.notification.sender = ApplicationConfig.WHATSAPP_SENDER;

                if (!string.IsNullOrEmpty(ApplicationConfig.WHATSAPP_EVENT.ToString()))
                {
                    string eventtype = ApplicationConfig.WHATSAPP_EVENT.ToString();
                    string Template = ApplicationConfig.WHATSAPP_EVENT_TEMPLATE.ToString();
                    whatsappReqModel.notification.templateId = Template;
                    if (!string.IsNullOrEmpty(Template))
                    {
                        string[] items = eventtype.Split(",");
                        Type type = whatsappCommunicationUIreq.GetType();
                        PropertyInfo[] properties = type.GetProperties();

                        bool allPropertiesPresent = true;

                        int index = 1;
                        foreach (var item in items)
                        {
                            string propertyName = item.Trim();
                            var property = Array.Find(properties, p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

                            if (property != null)
                            {
                                var value = property.GetValue(whatsappCommunicationUIreq);

                                if (item.Contains("supportEmail"))
                                {
                                    value = ApplicationConfig.WHATSAPP_SUPPORT_EMAIL;
                                }
                                if (item.Contains("supportNumber") || item.Contains("supportPhone"))
                                {
                                    value = ApplicationConfig.WHATSAPP_SUPPORT_NUMBER;
                                }

                                if (string.IsNullOrEmpty(value?.ToString()))
                                {
                                    allPropertiesPresent = false;
                                }

                                var paramProperty = whatsappReqModel.notification.@params.GetType().GetProperty($"_{index}");
                                if (paramProperty != null)
                                {
                                    paramProperty.SetValue(whatsappReqModel.notification.@params, value);
                                }

                                index++;
                            }
                        }

                        if (!string.IsNullOrEmpty(attachmentBase64))
                        {
                            WhatsAppMedia whatsAppMedia = new WhatsAppMedia();
                            whatsAppMedia.mediaBase64Type = "application/pdf";
                            whatsAppMedia.mediaBase64 = attachmentBase64;
                            whatsAppMedia.title = "Letter.pdf";

                            var paramProperty = whatsappReqModel.notification.@params.GetType().GetProperty($"_media");
                            if (paramProperty != null)
                            {
                                paramProperty.SetValue(whatsappReqModel.notification.@params, whatsAppMedia);
                            }
                        }

                        if (allPropertiesPresent)
                        {
                            var reqBody = JsonConvert.SerializeObject(whatsappReqModel, new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Formatting = Formatting.Indented
                            });

                            request.AddParameter("application/json", reqBody, ParameterType.RequestBody);

                            stopwatch = Stopwatch.StartNew();

                            RestResponse response = await client.ExecuteAsync(request);

                            stopwatch.Stop();
                            var responseTime = stopwatch.Elapsed.TotalSeconds;

                            await paymentDAL.LogIntegrationApi(traceId, "", reqBody, response.Content, URL, "YM WHATSAPP SERVICE", JsonConvert.SerializeObject(response.StatusCode), flow_type, JsonConvert.SerializeObject(responseTime));

                            if (response != null && !string.IsNullOrEmpty(response.Content) && response.StatusCode == HttpStatusCode.Accepted)
                            {
                                WhatsappResModel whatsappResModel = JsonConvert.DeserializeObject<WhatsappResModel>(response.Content);

                                if (whatsappResModel != null && whatsappResModel.msgId != null)
                                {
                                    status = true;
                                }
                                else
                                {
                                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                        MethodName: "SendMessageService.cs - SendWhatsApp() ",
                                        TraceId: traceId,
                                        Message: "WhatsApp Request is : " + reqBody + " and Response is : " + response.Content), ApplicationConfig.SEND_MESSAGE_LOGS);
                                }
                            }
                            else
                            {
                                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                                    MethodName: "SendMessageService.cs - SendWhatsApp() ",
                                    TraceId: traceId,
                                    Message: "WhatsApp Request is : " + reqBody + " and Response is : " + response.Content), ApplicationConfig.SEND_MESSAGE_LOGS);
                            }
                        }
                    }
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "SendMessageService.cs - SendWhatsApp() ",
                        TraceId: traceId,
                        Message: "Our WhatsApp Request is : " + JsonConvert.SerializeObject(whatsappCommunicationUIreq) + " and Event is empty"), ApplicationConfig.SEND_MESSAGE_LOGS);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "SendMessageService.cs - SendWhatsApp() ",
                    TraceId: traceId,
                    Message: "An exception was caught while sending WhatsApp mobile No: " + mobileNo + " and Exception : " + $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
            }

            return status;
        }

        public string GenerateOTP()
        {
            string otp_string = string.Empty;

            try
            {
                using (var rng = RandomNumberGenerator.Create())
                {
                    var bytes = new byte[4];
                    rng.GetBytes(bytes);

                    int otp = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
                    otp_string = otp.ToString("D6");
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                     MethodName: "SendMessageService.cs - GenerateOTP() ",
                     TraceId: null,
                     Message: "An exception was caught while sending Generating OTP. Exception : " + $"Exception occurred: {ex.Message}",
                     StackTrace: ex.StackTrace), ApplicationConfig.SEND_MESSAGE_LOGS);
            }

            return otp_string;
        }
    }
}
