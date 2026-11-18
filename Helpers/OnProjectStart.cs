using DigitalPaymentSystem_ILTC.Controllers;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class OnProjectStart
    {
        public static void OnStart()
        {
            try
            {
                TechOpsController.ServerUpTime = DateTime.Now;
            }
            catch
            {

            }
        }
    }
}
