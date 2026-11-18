using Newtonsoft.Json;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class ApiFactoryInterfaceModel
    {
        public class ApiFactoryAccessToken
        {
            public string token_type { get; set; }
            public int expires_in { get; set; }
            public int ext_expires_in { get; set; }
            public string access_token { get; set; }
        }

        public class ArtemisPolicyCreationReq
        {
            public string billingPartyStake { get; set; }
            public string mid { get; set; }
            public string systemSource { get; set; } = "API";
            public ArtemisImDetails imDetails { get; set; }
            public ArtemisReceiptDetails receiptDetails { get; set; }
            public List<ArtemisTaggingReq> tagging { get; set; }
        }

        public class ArtemisImDetails
        {
            public string imdCode { get; set; }
            public string marketingOfficer { get; set; }
            public string businessVertical { get; set; }
            public string subVertical { get; set; }
        }

        public class ArtemisReceiptDetails
        {
            public string subCode { get; set; }
            public string mode { get; set; }
            public decimal instrumentAmount { get; set; }
            public string instrumentNumber { get; set; }
            public string instrumentDate { get; set; }
            public string payerPayeeCode { get; set; }
            public string officeCode { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public onlineDetails onlineDetails { get; set; }
        }

        public class onlineDetails
        {
            public string subModeOnline { get; set; }
        }
        public class ArtemisTaggingReq
        {
            public string proposalNumber { get; set; }
        }

        public class ArtemisPolicyCreationResp
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public List<string> messages { get; set; }
            public ArtemisData data { get; set; }
        }

        public class ArtemisData
        {
            public string receiptNumber { get; set; }
            public DateTime instrumentDate { get; set; }
            public decimal instrumentAmount { get; set; }
            public string payerPayeeCode { get; set; }
            public string payerPayeeName { get; set; }
            public string receiptStatus { get; set; }
            public string remarks { get; set; }
            public string mode { get; set; }
            public string subCode { get; set; }
            public string subModeOnline { get; set; }
            public List<ArtemisTaggingResp> tagging { get; set; }
        }

        public class ArtemisTaggingResp
        {
            public string taggingStatus { get; set; }
            public string policyNumber { get; set; }
            public string policyStatus { get; set; }
            public string policyInceptionDate { get; set; }
            public decimal collectionPaymentAmount { get; set; }
            public decimal amountToBeCollectedPaid { get; set; }
            public string proposalNumber { get; set; }
        }

        public class ArtemisPolicyTaggingOnlyReq
        {
            public List<ArtemisTaggingOnlyReq> tagging { get; set; }
        }

        public class ArtemisTaggingOnlyReq
        {
            public string receiptNumber { get; set; }
            public string proposalNumber { get; set; }
        }

        public class ArtemisPolicyTaggingOnlyResp
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public List<string> messages { get; set; }
            public ArtemisTaggingOnlyData data { get; set; }
        }

        public class ArtemisTaggingOnlyData
        {
            public string remarks { get; set; }
            public string payerPayeeCode { get; set; }
            public string payerPayeeName { get; set; }
            public string receiptStatus { get; set; }
            public List<ArtemisTaggingOnlyResp> tagging { get; set; }
        }

        public class ArtemisTaggingOnlyResp
        {
            public string mode { get; set; }
            public string subCode { get; set; }
            public string receiptNumber { get; set; }
            public string instrumentDate { get; set; }
            public decimal instrumentAmount { get; set; }
            public string taggingStatus { get; set; }
            public string policyNumber { get; set; }
            public string policyStatus { get; set; }
            public string policyInceptionDate { get; set; }
            public decimal collectionPaymentAmount { get; set; }
            public decimal amountToBeCollectedPaid { get; set; }
            public string proposalNumber { get; set; }
        }

        public class ArtemisPolicyCreationRespTimeout
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public string[] messages { get; set; }
            public Data data { get; set; }
        }

        public class Data
        {
            public List<Taggingdetail> taggingDetails { get; set; }
        }

        public class Taggingdetail
        {
            public string taggedProposalNumber { get; set; }
            public string taggedReceiptNumber { get; set; }
            public string taggedPolicyNumber { get; set; }
        }

        public class FetchArtemisProposalDetails
        {
            public string is_nstp_flow { get; set; }
            public string business_vertical { get; set; }
            public string sub_vertical { get; set; }
            public string marketing_officer { get; set; }
            public string intermediary_code { get; set; }
            public string proposer_code { get; set; }
            public string policy_status { get; set; }
            public string policy_sub_status { get; set; }
            public string policy_number { get; set; }
        }

        public class ArtemisEmiDetailsReqModel
        {
            public string policyNumber { get; set; }
            public string proposalNumber { get; set; }
        }

        public class ArtemisEMISummaryDetailsResp
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public List<string> messages { get; set; }
            public ArtemisEMISummaryDetailsResponse response { get; set; }
        }

        public class ArtemisEMISummaryDetailsResponse
        {
            public string policyNumber { get; set; }
            public string proposalNumber { get; set; }
            public string transactionType { get; set; }
            public string premiumFrequency { get; set; }
            public string totalNumberOfInstallment { get; set; }
            public string totalInstallmentAmount { get; set; }
            public ArtemisMandateInfo mandateInfo { get; set; }
            public List<ArtemisEmiSchedule> emiSchedule { get; set; }
        }

        public class ArtemisEmiSchedule
        {
            public string emiCounter { get; set; }
            public string netInstallmentAmount { get; set; }
            public string gst { get; set; }
            public string installmentDueDate { get; set; }
            public string installmentPaidDate { get; set; }
            public string installmentEndDate { get; set; }
            public string paymentCategory { get; set; }
            public string status { get; set; }
            public string receiptID { get; set; }
            public string claimNumber { get; set; }
            public string endorsementNumber { get; set; }
        }

        public class ArtemisMandateInfo
        {
            public string customerName { get; set; }
            public string customerEmail { get; set; }
            public string customerMobileNumber { get; set; }
            public string customerID { get; set; }
            public string orderID { get; set; }
            public string paymentMethod { get; set; }
            public string maxAmount { get; set; }
            public string authType { get; set; }
            public string recurringPaymentAmount { get; set; }
            public string expireAt { get; set; }
            public string authCode { get; set; }
            public string tokenID { get; set; }
            public string proposalNumber { get; set; }
            public string productCode { get; set; }
            public string creationDate { get; set; }
            public string sourceFlag { get; set; }
            public string insertDate { get; set; }
            public string status { get; set; }
            public string umrnNumber { get; set; }
            public string mandateDeviationFlag { get; set; }
            public string offlineMandate { get; set; }
        }

        public class ArtemisAgentDetailsResp
        {
            public string traceId { get; set; }
            public int statusCode { get; set; }
            public List<string> messages { get; set; }
            public AgentDetailsData data { get; set; }
        }

        public class AgentDetailsData
        {
            public AgentIntermediary intermediary { get; set; }
            public AgentPersonalDetails personalDetails { get; set; }
            public List<AgentProduct> products { get; set; }
            public AgentCertificate certificate { get; set; }
            public AgentPaymentMode paymentMode { get; set; }
            public AgentBankingLimit bankingLimit { get; set; }
            public AgentLicense license { get; set; }
            public AgentAdditionalInfo additionalInfo { get; set; }
            public string partyCode { get; set; }
            public string stakeHolder { get; set; }
            public string createOrUpdateFlag { get; set; }
            public string partyStatus { get; set; }
        }

        public class AgentIntermediary
        {
            public List<string> intermediaryId { get; set; }
            public string ilLocationName { get; set; }
            public string intermediaryCategory { get; set; }
            public string intermediarySubCategory { get; set; }
            public string intermediaryType { get; set; }
            public string intermediaryName { get; set; }
            public string licenseStartDate { get; set; }
            public string licenseEndDate { get; set; }
            public string ilLocation { get; set; }
            public string imAssociationType { get; set; }
            public string parentID { get; set; }
            public string cscid { get; set; }
            public string intermediaryStatus { get; set; }
            public string pfid { get; set; }
            public string dmaCode { get; set; }
            public string rmbsFlag { get; set; }
            public string referralScheme { get; set; }
        }

        public class AgentPersonalDetails
        {
            public AgentContactDetails contactDetails { get; set; }
        }

        public class AgentContactDetails
        {
            public string mobileNumber { get; set; }
            public string emailIDPrimary { get; set; }
        }

        public class AgentProduct
        {
            public string productCode { get; set; }
            public string rmid { get; set; }
            public string rmName { get; set; }
            public string status { get; set; }
            public string VerticalName { get; set; }
            public string SubVerticalName { get; set; }
            public string verticalCode { get; set; }
            public string subVerticalCode { get; set; }
            public string productName { get; set; }
            public string instaStatus { get; set; }
            public List<object> productCategory { get; set; }
        }

        public class AgentCertificate
        {
            public string certificateNo { get; set; }
            public string certificateCategory { get; set; }
            public string certificatePAN { get; set; }
            public string certificateNumber { get; set; }
            public string certificateType { get; set; }
            public string certificateStatus { get; set; }
            public string certificateStartDate { get; set; }
            public string certificateEndDate { get; set; }
            public string licenseNumber { get; set; }
        }

        public class AgentPaymentMode
        {
            public string online { get; set; }
            public string offline { get; set; }
        }

        public class AgentBankingLimit
        {
            public string bankingLimitAmount { get; set; }
            public string bankingLimitDays { get; set; }
        }

        public class AgentLicense
        {
            public string agentandBrokerName { get; set; }
            public string licenseNumber { get; set; }
            public string panNumber { get; set; }
            public string status { get; set; }
        }

        public class AgentAdditionalInfo
        {
            public string additionalField1 { get; set; }
            public string additionalField2 { get; set; }
            public string additionalField3 { get; set; }
            public string additionalField4 { get; set; }
            public string additionalField5 { get; set; }
            public string additionalField6 { get; set; }
            public string additionalField7 { get; set; }
            public string additionalField8 { get; set; }
            public string additionalField9 { get; set; }
            public string additionalField10 { get; set; }
        }
    }
}
