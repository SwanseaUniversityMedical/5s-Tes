
namespace FiveSafesTes.Core.Services
{
    public interface IEncDecHelper
    {
        string Decrypt(string encryptedText);
        string Encrypt(string text);
    }
}
