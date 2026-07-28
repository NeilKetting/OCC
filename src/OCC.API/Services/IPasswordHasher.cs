namespace OCC.API.Services
{
    /// <summary>
    /// Contract for secure password hashing and verification services compliant with OWASP guidelines.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Hashes a plain-text password using PBKDF2 with HMAC-SHA256 and a random salt.
        /// </summary>
        /// <param name="password">The plain-text password to hash.</param>
        /// <returns>A formatted base64 hash string containing iteration count, salt, and hash value.</returns>
        string HashPassword(string password);

        /// <summary>
        /// Verifies a plain-text password against a stored password hash in constant time.
        /// Supports legacy SHA-256 hashes with automatic backward compatibility.
        /// </summary>
        /// <param name="password">The input plain-text password to verify.</param>
        /// <param name="hash">The stored hash string.</param>
        /// <returns><c>true</c> if the password matches the hash; otherwise, <c>false</c>.</returns>
        bool VerifyPassword(string password, string hash);

        /// <summary>
        /// Validates that a password satisfies OWASP complexity requirements.
        /// </summary>
        /// <param name="password">The candidate password string.</param>
        /// <returns><c>true</c> if complex enough; otherwise, <c>false</c>.</returns>
        bool IsPasswordComplex(string password);
    }
}
