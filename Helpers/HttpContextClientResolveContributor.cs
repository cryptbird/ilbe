using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class HttpContextClientResolveContributor : IClientResolveContributor
    {
        public async Task<string> ResolveClientAsync(HttpContext httpContext)
        {
            try
            {
                // 1. Get Authorization Header
                string authHeader = httpContext.Request.Headers["Authorization"];

                // 2. Check if it is a valid Bearer token
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    string token = authHeader.Substring(7);

                    // 3. Fast Read: Decode token without signature verification to get the name
                    var handler = new JwtSecurityTokenHandler();

                    if (handler.CanReadToken(token))
                    {
                        var jwtToken = handler.ReadJwtToken(token);

                        // 4. Extract Username (looks for "unique_name", "sub", "name", or "Username")
                        var usernameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == "sub" || c.Type == "name" || c.Type == "Username");

                        if (usernameClaim != null)
                        {
                            return usernameClaim.Value; // Returns User's Name (e.g., "Ziyan")
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors and fall back to anonymous
            }

            // 5. Default fallback. 
            // Since we removed IP logic, ALL users on the Login page share this single "anonymous" bucket.
            return "anonymous";
        }
    }
}