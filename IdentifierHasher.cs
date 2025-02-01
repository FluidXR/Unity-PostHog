using System;
using System.Security.Cryptography;
using System.Text;

// Use this class for hashing identifiers before sending them to any 3rd party analytics services
namespace UnityPosthog.Utilities {
    public static class IdentifierHasher
    {
        private static readonly string _secretKey = "saltUserData";
        
        public static string HashIdentifier(string identifier)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
            {
                var identifierBytes = Encoding.UTF8.GetBytes(identifier);
                var hashBytes = hmac.ComputeHash(identifierBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}