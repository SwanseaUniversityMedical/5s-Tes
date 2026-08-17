using System.Security.Cryptography;
using System.Text;
using FiveSafesTes.Core.Models.Settings;

namespace FiveSafesTes.Core.Services
{
    public class EncDecHelper : IEncDecHelper
    {
        public string _Key { get; set; }

        public EncDecHelper(EncryptionSettings encset)
        {
            _Key = encset.Key;
        }
        
        public string Decrypt(string encryptedText)
        {
            byte[] fullBytes = Convert.FromBase64String(encryptedText);

            Aes cipher = CreateCipher(_Key);

            // Extract the IV that was prepended during encryption (first 16 bytes)
            byte[] iv = new byte[cipher.BlockSize / 8];
            byte[] cipherBytes = new byte[fullBytes.Length - iv.Length];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

            cipher.IV = iv;

            ICryptoTransform cryptTransform = cipher.CreateDecryptor();
            byte[] plainBytes = cryptTransform.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }

        public string Encrypt(string text)
        {
            Aes cipher = CreateCipher(_Key);
            // Generate a fresh random IV for every encryption
            cipher.GenerateIV();

            ICryptoTransform cryptTransform = cipher.CreateEncryptor();
            byte[] plaintext = Encoding.UTF8.GetBytes(text);
            byte[] cipherText = cryptTransform.TransformFinalBlock(plaintext, 0, plaintext.Length);

            // Prepend the IV to the ciphertext so Decrypt can recover it
            byte[] result = new byte[cipher.IV.Length + cipherText.Length];
            Buffer.BlockCopy(cipher.IV, 0, result, 0, cipher.IV.Length);
            Buffer.BlockCopy(cipherText, 0, result, cipher.IV.Length, cipherText.Length);

            return Convert.ToBase64String(result);
        }

        public static string GetEncodedRandomString(int length)
        {
            var base64 = Convert.ToBase64String(GenerateRandomBytes(length));
            return base64;
        }

        private Aes CreateCipher(string keyBase64)
        {
            // Default values: Keysize 256, Padding PKC27
            Aes cipher = Aes.Create();
            cipher.Mode = CipherMode.CBC; // Ensure the integrity of the ciphertext if using CBC

            cipher.Padding = PaddingMode.ISO10126;
            cipher.Key = Convert.FromBase64String(keyBase64);

            return cipher;
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            var byteArray = new byte[length];
            var rg = RandomNumberGenerator.Create();
            rg.GetBytes(byteArray);
            return byteArray;
        }
    }
}
