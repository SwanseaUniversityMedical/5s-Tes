using System.Security.Cryptography;

namespace TeleportAD.Utilities
{
    public class PasswordGenerator
    {
        private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Lowercase = "abcdefghijkmnpqrstuvwxyz";
        private const string Digits = "23456789";
        private const string Special = "!@#$%^&*-_=+";

        // Meets typical AD complexity policy: length 16, at least one char from each class
        public static string Generate(int length = 16)
        {
            var all = Uppercase + Lowercase + Digits + Special;
            var chars = new char[length];

            chars[0] = PickRandom(Uppercase);
            chars[1] = PickRandom(Lowercase);
            chars[2] = PickRandom(Digits);
            chars[3] = PickRandom(Special);

            for (var i = 4; i < length; i++)
            {
                chars[i] = PickRandom(all);
            }

            Shuffle(chars);
            return new string(chars);
        }

        private static char PickRandom(string alphabet)
        {
            return alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        private static void Shuffle(char[] chars)
        {
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
        }
    }
}
