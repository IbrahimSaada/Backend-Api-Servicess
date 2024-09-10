using System.Security.Cryptography;
using System.Text;

namespace Backend_Api_services.Services
{
    public class SignatureService
    {
        private readonly IConfiguration _configuration;

        public SignatureService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool ValidateSignature(string receivedSignature, string dataToSign)
        {
            var secretKey = _configuration["AppSecretKey"];

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
                var computedSignature = Convert.ToBase64String(computedHash);

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedSignature),
                    Encoding.UTF8.GetBytes(receivedSignature)
                );
            }
        }
    }
}
