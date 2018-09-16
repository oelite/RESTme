using System.Threading.Tasks;
using OElite.Restme.GoogleUtils.Models;

namespace OElite.Restme.GoogleUtils
{
    public class ReCaptchaUtils
    {
        public const string GoogleSiteVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

        public static async Task<bool> VerifyRecaptchaResponse(string secretKey, string response,
            string remoteIp = null)
        {
            if (secretKey.IsNotNullOrEmpty() && response.IsNotNullOrEmpty())
            {
                using (var rest = new Rest(GoogleSiteVerifyUrl))
                {
                    rest.Add("secret", secretKey);
                    rest.Add("response", response);
                    if (remoteIp.IsNotNullOrEmpty())
                    {
                        rest.Add("remoteip", remoteIp);
                    }

                    var result = await rest.PostAsync<ReCaptchaVerificationResult>();
                    if (result?.ErrorCodes?.Length > 0)
                    {
                        foreach (var error in result.ErrorCodes)
                        {
                            switch (error)
                            {
                                case "missing-input-secret":
                                    rest.LogError("The secret parameter is missing.");
                                    break;
                                case "invalid-input-secret":
                                    rest.LogError(" The secret parameter is invalid or malformed.");
                                    break;
                                case "missing-input-response":
                                    rest.LogError("The response parameter is missing.");
                                    break;
                                case "invalid-input-response":
                                    rest.LogError("The response parameter is invalid or malformed.");
                                    break;
                                case "bad-request":
                                    rest.LogError("The request is invalid or malformed.");
                                    break;
                            }
                        }
                    }

                    return result?.Success == true;
                }
            }
        }
    }
}