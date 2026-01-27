using System.Security.Cryptography;
using System.Text;
using FiveSafesTes.Core.Models.Settings;

namespace FiveSafesTes.Core.Services
{
    public class EncDecHelper : IEncDecHelper
    {
        public string _Key { get; set; }
        public string _IVBase64 { get; set; }

        public (string Key, string IVBase64) InitSymmetricEncryptionKeyIV(string key)
        {

            Aes cipher = CreateCipher(key);
            var IVBase64 = Convert.ToBase64String(cipher.IV);
            return (key, IVBase64);
        }

        public EncDecHelper(EncryptionSettings encset)
        {
            _Key = encset.Key;
            _IVBase64 = encset.Base;
        }

        

        public string Decrypt(string encryptedText)
        {
            Aes cipher = CreateCipher(_Key);
            cipher.IV = Convert.FromBase64String(_IVBase64);

            ICryptoTransform cryptTransform = cipher.CreateDecryptor();
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = cryptTransform.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }

        public string Encrypt(string text)
        {
            Aes cipher = CreateCipher(_Key);
            cipher.IV = Convert.FromBase64String(_IVBase64);

            ICryptoTransform cryptTransform = cipher.CreateEncryptor();
            byte[] plaintext = Encoding.UTF8.GetBytes(text);
            byte[] cipherText = cryptTransform.TransformFinalBlock(plaintext, 0, plaintext.Length);

            return Convert.ToBase64String(cipherText);
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
