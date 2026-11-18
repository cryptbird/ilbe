using DigitalPaymentSystem_ILTC.Helpers;
using System.ComponentModel.DataAnnotations;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class BreakInModel
    {
        public class CreateBreakInReq
        {
            [Required(ErrorMessage = "ID is required.")]
            public string ID { get; set; }

            [Required(ErrorMessage = "Inspection Type is required.")]
            [RegularExpression(@"^(?i)(PI|SI)$", ErrorMessage = "Inspection Type must be one of  'PI' or 'SI'.")]
            [NoHtmlTags(ErrorMessage = "Inspection Type contains invalid characters.")]
            public string inspection_type { get; set; }
            [NoHtmlTags(ErrorMessage = "Sublocation contains invalid characters.")]
            public string address { get; set; }

            [NoHtmlTags(ErrorMessage = "Sublocation contains invalid characters.")]
            public string city { get; set; }

            [NoHtmlTags(ErrorMessage = "Sublocation contains invalid characters.")]
            public string state { get; set; }

            [NoHtmlTags(ErrorMessage = "Sublocation contains invalid characters.")]
            public string pincode { get; set; }
        }

        public class BreakInIDList
        {
            public string proposal_number { get; set; }
            public string breakin_id { get; set; }
        }

        public class CreateBreakInResp
        {
            public string BreakInMessage { get; set; } = "We could not submit your details. If the issue persists, don’t worry — we will update you once it’s resolved. Thank you for your patience.";
            public List<BreakInIDList> breakInIDLists { get; set; }
        }

        public class BreakInWebhookReq
        {
            public string BreakInId { get; set; }
            public string ModeofInspection { get; set; }
            public string InspectionDate { get; set; }
            public string BreakinStatus { get; set; }
            public string ChassisNumber { get; set; }
            public string EngineNumber { get; set; }
            public string VehicleRegistrationNumber { get; set; }
            public string Location { get; set; }
            public string Remarks { get; set; }
            public string SurveyorName { get; set; }
            public string ReasonForRejection { get; set; }
            public string ReasonForClosure { get; set; }
            public string CorelationId { get; set; }
        }

        public class BreakInClearInspectionDetails
        {
            public string ProposalNumber { get; set; }
            public string ProposalStartDate { get; set; }
            public string DealId { get; set; }
            public string traceId { get; set; }
        }

        public class BreakinClearInspectionStatusReq
        {
            public string ReferenceNo { get; set; }
            public string ReferenceDate { get; set; }
            public string InspectionId { get; set; }
            public string DealNo { get; set; }
            public string InspectionStatus { get; set; }
            public string CorrelationId { get; set; }
        }

        public class BreakinClearInspectionStatusRes
        {
            public string ReferenceNo { get; set; }
            public string PolicyNo { get; set; }
            public string Error_Id { get; set; }
            public string VehicleInspectionStatus { get; set; }
            public bool Status { get; set; }
            public string Message { get; set; }
            public string StatusMessage { get; set; }
            public string correlationId { get; set; }
        }

        public class BreakInStatusReq
        {
            [Required(ErrorMessage = "BreakIn ID is required.")]
            public string BreakInId { get; set; }
        }

        public class PiSublocationReq
        {
            [Required(ErrorMessage = "Sublocation is required.")]
            public string Sublocation { get; set; }
        }

        public class PiSublocationResp
        {
            public List<string> PiSublocations { get; set; }
        }

        public class breakInProposals
        {
            public string proposal_number { get; set; }
            public string deal_id { get; set; }
            public string breakin_id { get; set; }
            public string proposal_amount { get; set; }
            public string proposal_id { get; set; }
            public string proposal_type { get; set; }
            public string core { get; set; }
        }

        public class FetchPincodeDetailsReq
        {
            [Required(ErrorMessage = "Pincode is required.")]
            public string pincode { get; set; }

            [Required(ErrorMessage = "ID is required.")]
            public string ID { get; set; }
        }

        public class FetchPincodeReq
        {
            public string pincode { get; set; }
        }

        public class FetchPincodeDetailsResp
        {
            public string PinCode { get; set; }
            public int StateId { get; set; }
            public string StateName { get; set; }
            public int CityDistrictId { get; set; }
            public string CityDistrictName { get; set; }
            public string Zone { get; set; }
            public bool Success { get; set; }
            public int StatusCode { get; set; }
            public string TechnicalError { get; set; }
            public string DisplayMessage { get; set; }
            public string CorelationId { get; set; }
        }
    }
}
