using System.Security.Cryptography;
using System.Text.Json;

namespace Agent.Api;

/// <summary>What gets signed: a description of the file, not the file itself.</summary>
public sealed record SignedManifest(  string File, string Sha256, long Size, DateTimeOffset SignedAt, string KeyId, string Audience);

/// <summary>Detached signature written next to the file as &lt;file&gt;.sig.json. Both fields are base64.</summary>
public sealed record SignatureEnvelope(string Manifest, string Signature);

public static class FileHash
{
    public static string Sha256Hex(string path)
    {
        using var fs = File.OpenRead(path);          // streams; fine for multi-GB files
        return Convert.ToHexString(SHA256.HashData(fs));
    }

    public static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}

public static class FileSignerKeys
{
    public static void Generate(string privatePath, string publicPath)
    {
        using var k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(privatePath, k.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(publicPath, k.ExportSubjectPublicKeyInfoPem());
    }

    public static ECDsa Load(string pemPath)
    {
        var k = ECDsa.Create();
        k.ImportFromPem(File.ReadAllText(pemPath));
        return k;
    }
}

public static class FileSigner
{
    public static string Sign(string filePath, string privateKeyPath, string keyId, string audience)
    {
        using var key = FileSignerKeys.Load(privateKeyPath);

        var manifest = new SignedManifest(
            Path.GetFileName(filePath), FileHash.Sha256Hex(filePath), new FileInfo(filePath).Length,
            DateTimeOffset.UtcNow, keyId, audience);

        // Sign the exact bytes we send, so the verifier never has to re-serialise JSON.
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        byte[] sig = key.SignData(manifestBytes, HashAlgorithmName.SHA256);

        var env = new SignatureEnvelope(Convert.ToBase64String(manifestBytes), Convert.ToBase64String(sig));
        string sigPath = filePath + ".sig.json";
        File.WriteAllText(sigPath, JsonSerializer.Serialize(env, new JsonSerializerOptions { WriteIndented = true }));
        return sigPath;
    }

    public static SignatureEnvelope SignBytes(byte[] content, string name, string privateKeyPath, string keyId, string audience)
    {
        using var key = FileSignerKeys.Load(privateKeyPath);

        var manifest = new SignedManifest(  name, FileHash.Sha256Hex(content), content.Length, DateTimeOffset.UtcNow, keyId, audience);

        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        byte[] sig = key.SignData(manifestBytes, HashAlgorithmName.SHA256);

        return new SignatureEnvelope(Convert.ToBase64String(manifestBytes), Convert.ToBase64String(sig));
    }
}

public sealed record FileSignerVerifyResult(bool Valid, string Reason, SignedManifest? Manifest);

public static class FileSignerVerifier
{
    /// <param name="trusted">key_id → public key. Load from config / secret store in real use.</param>
    public static FileSignerVerifyResult Verify(string filePath, IReadOnlyDictionary<string, ECDsa> trusted,
                                      string expectedAudience, TimeSpan maxAge)
    {
        try
        {
            string sigPath = filePath + ".sig.json";
            if (!File.Exists(filePath)) return Fail("file not found");
            if (!File.Exists(sigPath)) return Fail("signature file not found");

            var env = JsonSerializer.Deserialize<SignatureEnvelope>(File.ReadAllText(sigPath));
            if (env is null) return Fail("malformed envelope");

            byte[] manifestBytes = Convert.FromBase64String(env.Manifest);
            byte[] sig = Convert.FromBase64String(env.Signature);
            var m = JsonSerializer.Deserialize<SignedManifest>(manifestBytes);
            if (m is null) return Fail("malformed manifest");

            // 1. Signature. KeyId is only a lookup hint until VerifyData succeeds.
            if (!trusted.TryGetValue(m.KeyId, out var pub)) return Fail($"unknown key '{m.KeyId}'");
            if (!pub.VerifyData(manifestBytes, sig, HashAlgorithmName.SHA256)) return Fail("bad signature");

            // 2. Signed claims
            if (m.Audience != expectedAudience) return Fail($"wrong audience '{m.Audience}'", m);
            var age = DateTimeOffset.UtcNow - m.SignedAt;
            if (age > maxAge) return Fail($"signature expired ({age:g} old)", m);
            if (age < TimeSpan.FromMinutes(-5)) return Fail("signed in the future (clock skew?)", m);
            if (m.File != Path.GetFileName(filePath)) return Fail($"filename mismatch (signed as '{m.File}')", m);

            // 3. File integrity
            if (new FileInfo(filePath).Length != m.Size) return Fail("size mismatch", m);
            if (!string.Equals(FileHash.Sha256Hex(filePath), m.Sha256, StringComparison.OrdinalIgnoreCase))
                return Fail("hash mismatch — file has been modified", m);

            return new FileSignerVerifyResult(true, "signature valid", m);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or IOException)
        {
            return Fail($"{ex.GetType().Name}: {ex.Message}");   // fail closed
        }

        static FileSignerVerifyResult Fail(string why, SignedManifest? m = null) => new(false, why, m);
    }

    public static FileSignerVerifyResult VerifyBytes(byte[] content, string name, SignatureEnvelope env,
                                      IReadOnlyDictionary<string, ECDsa> trusted, string expectedAudience, TimeSpan maxAge)
    {
        try
        {
            byte[] manifestBytes = Convert.FromBase64String(env.Manifest);
            byte[] sig = Convert.FromBase64String(env.Signature);
            var m = JsonSerializer.Deserialize<SignedManifest>(manifestBytes);
            if (m is null) return Fail("malformed manifest");

            // 1. Signature
            if (!trusted.TryGetValue(m.KeyId, out var pub)) return Fail($"unknown key '{m.KeyId}'");
            if (!pub.VerifyData(manifestBytes, sig, HashAlgorithmName.SHA256)) return Fail("bad signature");

            // 2. Signed claims
            if (m.Audience != expectedAudience) return Fail($"wrong audience '{m.Audience}'", m);
            var age = DateTimeOffset.UtcNow - m.SignedAt;
            if (age > maxAge) return Fail($"signature expired ({age:g} old)", m);
            if (age < TimeSpan.FromMinutes(-5)) return Fail("signed in the future (clock skew?)", m);
            if (m.File != name) return Fail($"name mismatch (signed as '{m.File}')", m);

            // 3. Integrity
            if (content.Length != m.Size) return Fail("size mismatch", m);
            if (!string.Equals(FileHash.Sha256Hex(content), m.Sha256, StringComparison.OrdinalIgnoreCase))
                return Fail("hash mismatch — content has been modified", m);

            return new FileSignerVerifyResult(true, "signature valid", m);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        {
            return Fail($"{ex.GetType().Name}: {ex.Message}");
        }

        static FileSignerVerifyResult Fail(string why, SignedManifest? m = null) => new(false, why, m);
    }
}
