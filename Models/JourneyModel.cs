namespace DigitalPaymentSystem_ILTC.Models
{
    public class JourneyModel
    {
        // Quote
        public class QuoteRequest
        {
            public string HomeType { get; set; }
            public string MobileNumber { get; set; }
            public string EmailId { get; set; }
            public string Pincode { get; set; }
            public string CoverageType { get; set; }
            public double? CarpetArea { get; set; }
            public double? CostOfConstruction { get; set; }
        }

        public class QuoteResponse
        {
            public string QuoteId { get; set; }
            public double SumInsured { get; set; }

            public PremiumDetails Premiums { get; set; }
        }

        public class PremiumDetails
        {
            public double OneYear { get; set; }
            public double ThreeYears { get; set; }
            public double FiveYears { get; set; }
        }


        // Proposal
        public class CreateProposalRequest
        {
            public string QuoteId { get; set; }
            public int Tenure { get; set; }
            public double SelectedTenurePremium { get; set; }
            public List<AdditionalCover>? AdditionalCovers { get; set; }
            public DateTime CustomerDOB { get; set; }
            public string CustomerAddress { get; set; }
        }

        public class AdditionalCover
        {
            public string CoverName { get; set; }
            public double CoverAmount { get; set; }
        }

        public class CreateProposalResponse
        {
            public string ProposalNumber { get; set; }
            public string DealId { get; set; }
            public string CustomerId { get; set; }
            public string TotalAmount { get; set; }
        }
    }
}
