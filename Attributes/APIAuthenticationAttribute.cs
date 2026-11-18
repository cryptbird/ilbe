using DigitalPaymentSystem_ILTC.Constants;
using DigitalPaymentSystem_ILTC.Helpers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

namespace DigitalPaymentSystem_ILTC.Attributes
{
    public class ApiAuthenticationAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (Authenticate(context))
            {
                return;
            }
            HandleUnauthorizedRequest(context);
        }

        public void HandleUnauthorizedRequest(AuthorizationFilterContext context)
        {
            context.Result = new ForbidResult();
            // Add Content Security Policy header for unauthorized responses
            context.HttpContext.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'self' https://www.icicilombard.com https://echannel.insurancepp.com https://echannel.insurancearticlez.com;");
            context.HttpContext.Response.Headers.Append("AuthStatus", "Unauthorized");
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.HttpContext.Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "Unauthorized";
            context.Result = new JsonResult("Unauthorized")
            {
                Value = "You do not have permission to view this directory or page.",
            };
        }

        public bool Authenticate(AuthorizationFilterContext context)
        {
            bool validFlag = false;
            try
            {
                var Authorization = Convert.ToString(context.HttpContext.Request.Headers["Authorization"]);
                var refreshAuthorization = Convert.ToString(context.HttpContext.Request.Headers["RefreshAuthorization"]);
               
                if (!string.IsNullOrEmpty(Authorization))
                {
                    if (Authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var jwt_String = Authorization.Substring(7);
                        var tokenHandler = new JwtSecurityTokenHandler();
                        var key = System.Text.Encoding.UTF8.GetBytes(ApplicationConfig.JWT_SECRET_KEY);

                        try
                        {
                            var validationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = ApplicationConfig.JWT_ISSUER,
                                ValidAudience = ApplicationConfig.JWT_AUDIENCE,
                                IssuerSigningKey = new SymmetricSecurityKey(key)
                            };

                            SecurityToken validatedToken;
                            var principal = tokenHandler.ValidateToken(jwt_String, validationParameters, out validatedToken);

                            if (principal != null)
                            {
                                var jwtToken = validatedToken as JwtSecurityToken;

                                var expiryUtc = jwtToken.ValidTo;
                                var expiryIst = TimeZoneInfo.ConvertTimeFromUtc(expiryUtc, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));

                                var currentIst = DateTime.Now;

                                if (currentIst > expiryIst)
                                {
                                    validFlag = false;
                                }
                                else
                                {
                                    validFlag = true;
                                }
                            }
                            else
                            {
                                validFlag = false;
                            }
                        }
                        catch (SecurityTokenExpiredException)
                        {
                            validFlag = false;
                        }
                        catch (Exception)
                        {
                            validFlag = false;
                        }
                    }    
                    else
                    {
                        // Invalid or missing authorization header
                        validFlag = false;
                    }
                }
                else if(!string.IsNullOrEmpty(refreshAuthorization))
                {
                    if (refreshAuthorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var jwt_String = refreshAuthorization.Substring(7);
                        var tokenHandler = new JwtSecurityTokenHandler();
                        var key = System.Text.Encoding.UTF8.GetBytes(ApplicationConfig.JWT_SECRET_KEY);

                        try
                        {
                            var validationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = false,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = ApplicationConfig.JWT_ISSUER,
                                ValidAudience = ApplicationConfig.JWT_AUDIENCE,
                                IssuerSigningKey = new SymmetricSecurityKey(key)
                            };

                            SecurityToken validatedToken;
                            var principal = tokenHandler.ValidateToken(jwt_String, validationParameters, out validatedToken);

                            if (principal != null)
                            {
                                validFlag = true;
                            }
                            else
                            {
                                validFlag = false;
                            }
                        }
                        catch (Exception)
                        {
                            validFlag = false;
                        }
                    }    
                    else
                    {
                        // Invalid or missing refresh authorization header
                        validFlag = false;
                    }
                }
                else
                {
                    validFlag = false;
                }
                return validFlag;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
