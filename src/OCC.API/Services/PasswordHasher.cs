using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OCC.API.Services
{
    /// <summary>
    /// Provides OWASP-compliant password hashing using PBKDF2 with SHA-256 and salt.
    /// Employs constant-time string comparison to prevent timing attacks.
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        /// <summary>
        /// Hashes a password using PBKDF2 with a secure random salt.
        /// </summary>
        /// <param name="password">The plain-text password.</param>
        /// <returns>Formatted string: PBKDF2v1:{Iterations}:{SaltBase64}:{HashBase64}</returns>
        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password), "Password cannot be null or empty.");
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                Algorithm,
                KeySize);

            return $"PBKDF2v1:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifies a password against a stored hash string in constant time to avoid timing attacks.
        /// Supports backward compatibility with legacy raw SHA-256 hashes.
        /// </summary>
        /// <param name="password">The candidate plain-text password.</param>
        /// <param name="hash">The stored hash string.</param>
        /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
        public bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            {
                return false;
            }

            // Check if hash is in PBKDF2 format
            if (hash.StartsWith("PBKDF2v1:"))
            {
                var parts = hash.Split(':');
                if (parts.Length != 4) return false;

                if (!int.TryParse(parts[1], out int iterations)) return false;

                try
                {
                    byte[] salt = Convert.FromBase64String(parts[2]);
                    byte[] expectedHash = Convert.FromBase64String(parts[3]);

                    byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                        Encoding.UTF8.GetBytes(password),
                        salt,
                        iterations,
                        Algorithm,
                        expectedHash.Length);

                    return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
                }
                catch
                {
                    return false;
                }
            }

            // Fallback for legacy raw SHA256 hashes (constant time comparison)
            using var sha256 = SHA256.Create();
            byte[] computedLegacyHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            try
            {
                byte[] storedLegacyHash = Convert.FromBase64String(hash);
                return CryptographicOperations.FixedTimeEquals(computedLegacyHash, storedLegacyHash);
            }
            catch
            {
                // Fallback for plain hex or legacy direct comparison safely
                var newHash = Convert.ToBase64String(computedLegacyHash);
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(newHash),
                    Encoding.UTF8.GetBytes(hash));
            }
        }

        /// <summary>
        /// Validates that a password meets OWASP complexity requirements.
        /// Minimum 8 characters, containing uppercase, lowercase, and a digit or special character.
        /// </summary>
        /// <param name="password">The candidate password.</param>
        /// <returns><c>true</c> if complex enough; otherwise, <c>false</c>.</returns>
        public bool IsPasswordComplex(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && (hasDigit || hasSpecial);
        }
    }
}
