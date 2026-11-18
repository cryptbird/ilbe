using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using static DigitalPaymentSystem_ILTC.Models.PaymentModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    [Route("dps-agorax/")]
    public class TechOpsController : ControllerBase
    {
        public static DateTime ServerUpTime;

        [HttpGet("api/heartbeat")]
        public ActionResult Heartbeat()
        {
            return Ok("Hello World. (GIT) AgoraX Backend is working fine.| CICD: " + ServerUpTime.ToString("dd-MM-yyyy h:mm:ss tt"));
        }


        [HttpGet("api/GetCurrentServerTime")]
        public ActionResult GetCurrentServerTime()
        {
            var process = Process.GetCurrentProcess();
            DPSStatusResponse response = new DPSStatusResponse();
            response.StatusCode = 1;
            response.StatusMessage = process.ProcessName;
            response.Message = null;
            response.TraceId = null;
            response.Data = new
            {
                Now = DateTime.Now.ToString("dd-MM-yyyy h:mm:ss tt")
            };
            return Ok(response);
        }

        [HttpGet]
        [Route("api/TimeCheck")]
        public ActionResult TimeCheck()
        {
            var process = Process.GetCurrentProcess();
            DPSStatusResponse response = new DPSStatusResponse();
            response.StatusCode = 1;
            response.StatusMessage = process.ProcessName;
            response.Message = null;
            response.TraceId = null;
            response.Data = new
            {
                Now = DateTime.Now.ToString("dd-MM-yyyy h:mm:ss tt")
            };
            return Ok(response);
        }
    }
}
