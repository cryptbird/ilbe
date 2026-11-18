using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static DigitalPaymentSystem_ILTC.Models.JourneyModel;

namespace DigitalPaymentSystem_ILTC.Controllers
{
    [ApiController]
    [EnableCors("CorsPolicy")]
    [Route("dps-agorax/")]
    public class JourneyController : ControllerBase
    {

        [HttpPost("api/v1/pay/GenerateQuote")]
        public IActionResult GenerateQuote([FromBody] QuoteRequest request)
        {
            // Basic validations
            if (string.IsNullOrEmpty(request.HomeType) ||
                string.IsNullOrEmpty(request.MobileNumber) ||
                string.IsNullOrEmpty(request.EmailId) ||
                string.IsNullOrEmpty(request.Pincode) ||
                string.IsNullOrEmpty(request.CoverageType))
            {
                return BadRequest("Missing required fields");
            }

            double sumInsured = 0;

            // STRUCTURE LOGIC
            if (request.CoverageType.ToLower().Contains("structure"))
            {
                if (!request.CarpetArea.HasValue || !request.CostOfConstruction.HasValue)
                    return BadRequest("CarpetArea and CostOfConstruction are required for structure coverage");

                if (request.CostOfConstruction < 3200 || request.CostOfConstruction > 7000)
                    return BadRequest("CostOfConstruction must be between 3200 and 7000");

                sumInsured += request.CarpetArea.Value * request.CostOfConstruction.Value;
            }

            // CONTENT LOGIC (dummy fixed value for now)
            if (request.CoverageType.ToLower().Contains("content"))
            {
                sumInsured += 500000; // Example fixed content coverage
            }

            // Generate Quote ID
            var quoteId = $"Q-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

            // Premium Calculation (simple % logic)
            var oneYearPremium = sumInsured * 0.001; // 0.1%
            var threeYearPremium = oneYearPremium * 3 * 0.95; // 5% discount
            var fiveYearPremium = oneYearPremium * 5 * 0.90; // 10% discount

            var response = new QuoteResponse
            {
                QuoteId = quoteId,
                SumInsured = sumInsured,
                Premiums = new PremiumDetails
                {
                    OneYear = Math.Round(oneYearPremium, 2),
                    ThreeYears = Math.Round(threeYearPremium, 2),
                    FiveYears = Math.Round(fiveYearPremium, 2)
                }
            };

            return Ok(response);
        }


        [HttpPost("api/v1/pay/CreateProposal")]
        public IActionResult CreateProposal([FromBody] CreateProposalRequest request)
        {
            if (string.IsNullOrEmpty(request.QuoteId))
                return BadRequest("QuoteId is required");

            if (request.Tenure != 1 && request.Tenure != 3 && request.Tenure != 5)
                return BadRequest("Tenure must be 1, 3, or 5 years");

            if (request.SelectedTenurePremium <= 0)
                return BadRequest("SelectedTenurePremium must be greater than 0");

            // 🔹 Calculate additional covers total
            double additionalCoverTotal = 0;
            if (request.AdditionalCovers != null && request.AdditionalCovers.Any())
            {
                additionalCoverTotal = request.AdditionalCovers.Sum(x => x.CoverAmount);
            }

            // 🔹 Total amount
            double totalAmount = request.SelectedTenurePremium + additionalCoverTotal;

            // 🔹 Generate IDs
            string proposalNumber = "217709" + Generate4DigitNumber();
            string dealId = "DEAL-4118-6929482";
            string customerId = "101740639699";

            var response = new CreateProposalResponse
            {
                ProposalNumber = proposalNumber,
                DealId = dealId,
                CustomerId = customerId,
                TotalAmount = totalAmount.ToString("0.00") // ✅ string formatted
            };

            return Ok(response);
        }

        private string Generate4DigitNumber()
        {
            var random = new Random();
            return random.Next(1000, 10000).ToString(); // ensures 4 digits
        }
    }
}
