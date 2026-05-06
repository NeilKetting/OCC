using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OCC.WpfClient.Services.Infrastructure
{
    public interface ILocalEncryptionService
    {
        void InitializeOrLoadKeys(Guid userId);
        string GetPublicKey();
        void InitializeWithKey(Guid userId, string privateKeyXml);
        
        string GenerateAesKey(); // Returns Base64 AES Key
        
        // RSA Encryption for AES Key Distribution (DEPRECATED: Now server managed)
        string EncryptAesKeyWithRsa(string aesKeyBase64, string recipientPublicKeyXml);
        string DecryptAesKeyWithRsa(string encryptedAesKeyBase64);

        // AES Encryption for Messages
        string EncryptMessage(string plainText, string aesKeyBase64);
        string DecryptMessage(string cipherText, string aesKeyBase64);
    }

    public class LocalEncryptionService : ILocalEncryptionService
    {
        public void InitializeOrLoadKeys(Guid userId)
        {
            // No longer needed for Shared Keys
        }

        public string GetPublicKey() => string.Empty; // No longer needed

        public void InitializeWithKey(Guid userId, string privateKeyXml)
        {
            // No longer needed
        }

        public string GenerateAesKey()
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            return Convert.ToBase64String(aes.Key);
        }

        public string EncryptAesKeyWithRsa(string aesKeyBase64, string recipientPublicKeyXml)
        {
            // Deprecated logic
            return aesKeyBase64; 
        }

        public string DecryptAesKeyWithRsa(string encryptedAesKeyBase64)
        {
            // Now just returns the key since it's stored plain on server (or just handled by DTO)
            return encryptedAesKeyBase64;
        }

        public string EncryptMessage(string plainText, string aesKeyBase64)
        {
            if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(aesKeyBase64)) return plainText;

            try 
            {
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(aesKeyBase64);
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                
                ms.Write(aes.IV, 0, aes.IV.Length);
                
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception)
            {
                return plainText;
            }
        }

        public string DecryptMessage(string cipherTextBase64, string aesKeyBase64)
        {
            if (string.IsNullOrEmpty(cipherTextBase64) || string.IsNullOrEmpty(aesKeyBase64)) return cipherTextBase64;

            try
            {
                var fullCipherBytes = Convert.FromBase64String(cipherTextBase64);
                
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(aesKeyBase64);

                var iv = new byte[aes.BlockSize / 8];
                if (fullCipherBytes.Length < iv.Length) return cipherTextBase64;
                
                Array.Copy(fullCipherBytes, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(fullCipherBytes, iv.Length, fullCipherBytes.Length - iv.Length);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch (Exception)
            {
                // If it fails, maybe it wasn't encrypted or key is wrong
                // Returning original text for resilience
                return cipherTextBase64;
            }
        }
    }
}
