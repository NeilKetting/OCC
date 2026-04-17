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
        
        // RSA Encryption for AES Key Distribution
        string EncryptAesKeyWithRsa(string aesKeyBase64, string recipientPublicKeyXml);
        string DecryptAesKeyWithRsa(string encryptedAesKeyBase64);

        // AES Encryption for Messages
        string EncryptMessage(string plainText, string aesKeyBase64);
        string DecryptMessage(string cipherText, string aesKeyBase64);
    }

    public class LocalEncryptionService : ILocalEncryptionService
    {
        private Guid _currentUserId;
        private string _privateKeyXml = string.Empty;
        private string _publicKeyXml = string.Empty;
        private string _keyStoragePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCC", "Keys", $"{_currentUserId}_rsa.xml");

        public void InitializeOrLoadKeys(Guid userId)
        {
            _currentUserId = userId;
            
            var dir = Path.GetDirectoryName(_keyStoragePath);
            if (!Directory.Exists(dir) && dir != null)
                Directory.CreateDirectory(dir);

            if (File.Exists(_keyStoragePath))
            {
                // In production, encrypt this file or use Windows DPAPI
                _privateKeyXml = File.ReadAllText(_keyStoragePath);
                
                using var rsa = RSA.Create();
                rsa.FromXmlString(_privateKeyXml);
                _publicKeyXml = rsa.ToXmlString(false); // Export public only
            }
            else
            {
                using var rsa = RSA.Create(2048);
                _privateKeyXml = rsa.ToXmlString(true); // Export private
                _publicKeyXml = rsa.ToXmlString(false); // Export public

                File.WriteAllText(_keyStoragePath, _privateKeyXml);
            }
        }

        public string GetPublicKey() => _publicKeyXml;

        public void InitializeWithKey(Guid userId, string privateKeyXml)
        {
            _currentUserId = userId;
            _privateKeyXml = privateKeyXml;

            using var rsa = RSA.Create();
            rsa.FromXmlString(_privateKeyXml);
            _publicKeyXml = rsa.ToXmlString(false);

            var dir = Path.GetDirectoryName(_keyStoragePath);
            if (!Directory.Exists(dir) && dir != null)
                Directory.CreateDirectory(dir);

            File.WriteAllText(_keyStoragePath, _privateKeyXml);
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
            if (string.IsNullOrEmpty(recipientPublicKeyXml)) return string.Empty;
            
            using var rsa = RSA.Create();
            rsa.FromXmlString(recipientPublicKeyXml);
            var encryptedKeyBytes = rsa.Encrypt(Convert.FromBase64String(aesKeyBase64), RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(encryptedKeyBytes);
        }

        public string DecryptAesKeyWithRsa(string encryptedAesKeyBase64)
        {
            if (string.IsNullOrEmpty(_privateKeyXml) || string.IsNullOrEmpty(encryptedAesKeyBase64)) return string.Empty;
            
            using var rsa = RSA.Create();
            rsa.FromXmlString(_privateKeyXml);
            var decryptedKeyBytes = rsa.Decrypt(Convert.FromBase64String(encryptedAesKeyBase64), RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(decryptedKeyBytes);
        }

        public string EncryptMessage(string plainText, string aesKeyBase64)
        {
            if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(aesKeyBase64)) return plainText;

            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(aesKeyBase64);
            aes.GenerateIV(); // Unique IV for every message

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            
            // Prepend IV to ciphertext
            ms.Write(aes.IV, 0, aes.IV.Length);
            
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string DecryptMessage(string cipherTextBase64, string aesKeyBase64)
        {
            if (string.IsNullOrEmpty(cipherTextBase64) || string.IsNullOrEmpty(aesKeyBase64)) return cipherTextBase64;

            try
            {
                var fullCipherBytes = Convert.FromBase64String(cipherTextBase64);
                
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(aesKeyBase64);

                // Extract IV
                var iv = new byte[aes.BlockSize / 8];
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
                return "[Error: Decryption Failed]";
            }
        }
    }
}
