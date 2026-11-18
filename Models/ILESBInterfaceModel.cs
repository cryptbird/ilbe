namespace DigitalPaymentSystem_ILTC.Models
{
    public class ILESBInterfaceModel
    {
        public class ILESBTokenResponseModel
        {
            public string access_token { get; set; }
            public string expires_in { get; set; }
            public string Token_type { get; set; }
            public string token { get; set; }
        }

        public class PFPolicyCreationReq
        {
            public string CorrelationId { get; set; }
            public string DealID { get; set; }
            public string IntermediaryID { get; set; }
            public bool isMappingRequired { get; set; }
            public bool isTaggingRequired { get; set; }
            public PFPaymentMapTagReq PaymentTagging { get; set; }
            public PFPaymentEntryReq PaymentEntry { get; set; }
        }

        public class PFPaymentEntryReq
        {
            public PFOnlineDAEntryReq onlineDAEntry { get; set; }
            public string? ProductCode { get; set; }
        }

        public class PFOnlineDAEntryReq
        {
            public string AuthCode { get; set; }
            public string MerchantID { get; set; }
            public string TransactionID { get; set; }
            public string PaymentAmount { get; set; }
            public string InstrumentDate { get; set; }
            public string CustomerID { get; set; }
            public string ReceiptDate { get; set; }
            public string PayerType { get; set; }
        }

        public class PFPaymentMapTagReq
        {
            public List<PFCustomerProposalReq> customerProposal { get; set; }
            public string PaymentID { get; set; }
        }

        public class PFCustomerProposalReq
        {
            public string ProposalNo { get; set; }
            public string CustomerID { get; set; }
        }

        public class PFPolicyCreationResp
        {
            public PFPaymentEntryResp paymentEntryResponse { get; set; }
            public PFPaymentMappingResp paymentMappingResponse { get; set; }
            public PFPaymentTaggingResp paymentTagResponse { get; set; }
            public object cdbgResponse { get; set; }
            public string Message { get; set; }
            public bool Status { get; set; }
            public string StatusMessage { get; set; }
            public string CorrelationId { get; set; }
        }

        public class PFPaymentEntryResp
        {
            public string PaymentID { get; set; }
            public string Error_ID { get; set; }
            public string ErrorText { get; set; }
            public string Status { get; set; }
        }

        public class PFPaymentMappingResp
        {
            public List<PFPaymentMapTagResplist> PaymentMapResponseList { get; set; }
        }

        public class PFPaymentTaggingResp
        {
            public List<PFPaymentMapTagResplist> PaymentTagResponseList { get; set; }
        }

        public class PFPaymentMapTagResplist
        {
            public string PolicyNo { get; set; }
            public string CoverNoteNo { get; set; }
            public string ProposalNo { get; set; }
            public string PaymentID { get; set; }
            public string Error_ID { get; set; }
            public string ErrorText { get; set; }
            public string Status { get; set; }
        }

        public class IlesbCreateBreakInReq
        {
            public string CorrelationId { get; set; }
            public string DealID { get; set; }
            public string CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string CustomerAddress { get; set; }
            public string State { get; set; }
            public string City { get; set; }
            public string Pincode { get; set; }
            public string TelephoneNumber { get; set; }
            public string MobileNumber { get; set; }
            public string TypeVehicle { get; set; }
            public string VehicleMake { get; set; }
            public string VehicleModel { get; set; }
            public string RegistrationNo { get; set; }
            public string EngineNo { get; set; }
            public string ChassisNo { get; set; }
            public string ManufactureYear { get; set; }
            public int BreakInDays { get; set; }
            public string SalesRemarks { get; set; }
            public string BreakInType { get; set; }
            public bool IsPOSDealId { get; set; }
            public string SubLocation { get; set; }
            public string SelfInspection { get; set; }

            public string PreviousInsurer { get; set; }
            public string PrevYearsPolicyNo { get; set; }
            public string PrevYearsClaimStatus { get; set; }
            public string PrevPolicyNCB { get; set; }
            public DateTime? AppointmentDateTime { get; set; }
            public string SubInspectionType { get; set; }
            public string AddOnCoverType { get; set; }
            public long? DistributorInterID { get; set; }
            public long? DistributorContact { get; set; }
            public DateTime? POSStarttime { get; set; }
            public DateTime? POSEndtime { get; set; }
            public string IsRegisteredCustomer { get; set; }
            public string DistributorName { get; set; }
            public string InspectionType { get; set; }
        }

        public class IlesbCreateBreakInResp
        {
            public long BrkId { get; set; }
            public string InspectionDate { get; set; }
            public string Link { get; set; }
            public string Location { get; set; }
            public double Mileage { get; set; }
            public string Remarks { get; set; }
            public string SurveyorName { get; set; }
            public string Message { get; set; }
            public string Status { get; set; }
            public string CorrelationId { get; set; }
            public string VRN { get; set; }
            public string ProposalNumber { get; set; }
        }

        public class ILESBBreakInStatusReq
        {
            public string CorrelationId { get; set; }
            public string BreakinId { get; set; }
            public string RegistrationNo { get; set; }
        }

        public class MTResponseFWBifurcationModels
        {
            public string CorrelationID { get; set; }
            public UserDataMT_FW UserData { get; set; }
            public string StatusText { get; set; }
        }

        public class CustomerDetailsMT_FW
        {
            public string CustomerID { get; set; }
            public string CustomerName { get; set; }
        }

        public class UserDataMT_FW
        {
            public CustomerDetailsMT_FW CustomerDetails { get; set; }
            public RiskMT_Main_FW Risks { get; set; }
        }

        public class VehicleDetailsMT_FW
        {
            public string vehicleModel { get; set; }
            public string breakInPeriod { get; set; }
        }

        public class RiskMT_Main_FW
        {
            public RiskMT_FW Risks { get; set; }
        }

        public class RiskMT_FW
        {
            public string RegistrationNumber { get; set; }
            public string VehicleType { get; set; }
            public string vehicleModel { get; set; }
            public double YearOfManufacture { get; set; }
            public string EngineNumber { get; set; }
            public string ChassisNumber { get; set; }
            public string VehicleMake { get; set; }
        }

        public class GetCustomerDetailsResponseModel
        {
            public string CustomerName { get; set; }
            public string CustomerId { get; set; }
            public string MOBILENUMBER { get; set; }
            public string TELEPHONENUMBER { get; set; }
            public string MAILINGCITYDISTRICT { get; set; }
            public string MAILING_CITYDISTRICT_NAME { get; set; }
            public string MAILINGADDRESSDETAIL { get; set; }
            public string MAILINGPINCODE { get; set; }
            public string MAILING_STATE_NAME { get; set; }
            public string MAILINGADDRESSLINE1 { get; set; }
            public string MAILINGADDRESSLINE2 { get; set; }
        }

        public class MTResponseTWBifurcationModels
        {
            public MotorCommonResponse MotorCommonResponse { get; set; }
            public string StatusText { get; set; }
        }

        public class MotorCommonResponse
        {
            public string CorelationdID { get; set; }
            public PropRisksMT_BF PropRisks { get; set; }
            public CustomerDetails CustomerDetails { get; set; }
        }

        public class CustomerDetails
        {
            public string CustomerID { get; set; }
            public string CustomerName { get; set; }
        }

        public class PropRisksMT_BF
        {
            public PropRisksMT PropRisks { get; set; }
        }

        public class PropRisksMT
        {
            public string RegistrationNumber { get; set; }
            public string VehicleType { get; set; }
            public string vehicleModel { get; set; }
            public string YearOfManufacture { get; set; }
            public string EngineNumber { get; set; }
            public string ChassisNumber { get; set; }
            public string VehicleMake { get; set; }
        }

        public class PfCustomerDetails
        {
            public string CustomerName { get; set; }
            public string MOBILENUMBER { get; set; }
            public string TELEPHONENUMBER { get; set; }
            public string MAILINGCITYDISTRICT { get; set; }
            public string MAILINGPINCODE { get; set; }
            public string MAILING_STATE_NAME { get; set; }
            public string MAILINGADDRESSLINE1 { get; set; }
            public string MAILINGADDRESSLINE2 { get; set; }
            public string Status { get; set; }
            public string MAILING_CITYDISTRICT_NAME { get; set; }
        }

        public class IASApiResp
        {
            public string CorrelationID { get; set; }
            public string Status { get; set; }
            public string ErrorId { get; set; }
            public string ErrorText { get; set; }
            public string UWProposalStatus { get; set; }
        }


        public class VoucherGenerationResp
        {
            public string CorelationID { get; set; }
            public string Status { get; set; }
            public string VoucherNo { get; set; }
            public string PaymentID { get; set; }
            public string PolicyNo { get; set; }
            public string Error_Id { get; set; }
            public string ErrorText { get; set; }
        }
        public class PaymentRefundVoucherResp
        {
            public string CorelationID { get; set; }
            public string VoucherNo { get; set; }
            public string ErrorMessage { get; set; }
            public string Status { get; set; }

        }
    }
}
