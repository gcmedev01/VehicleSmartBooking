using System.Security.Cryptography;
using System.Text;

namespace VehicleBooking.Web.Domain.Services
{
    public class Pbkdf2PasswordHasher : IPasswordHasher
    {
        // ปรับได้ตาม policy
        private const int DefaultIterations = 120_000;
        private const int SaltSize = 16;     // 128-bit
        private const int HashSize = 32;     // 256-bit
        private const string AlgoName = "pbkdf2-sha256";

        public PasswordHashResult Hash(string password, int? iterations = null)
        {
            var it = iterations.GetValueOrDefault(DefaultIterations);
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password: password,
                salt: salt,
                iterations: it,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashSize
            );

            return new PasswordHashResult(hash, salt, it, AlgoName);
        }

        public bool Verify(string password, byte[] hash, byte[] salt, int iterations, string? algo)
        {
            // ถ้า algo ไม่ตรง ก็ยัง verify ด้วย SHA256 PBKDF2 ก่อน (ง่ายสุดสำหรับตอนเริ่ม)
            var computed = Rfc2898DeriveBytes.Pbkdf2(
                password: password,
                salt: salt,
                iterations: iterations <= 0 ? DefaultIterations : iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: hash.Length
            );

            return CryptographicOperations.FixedTimeEquals(computed, hash);
        }
    }
}