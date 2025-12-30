using System.Security.Cryptography;
using System.Text;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services;

public class EncryptionService : IScopedDependency
{
    private readonly string _key;

    public EncryptionService(IOptions<AppSettings> options)
    {
        var seed = options.Value.EncryptionKey;
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(seed));
        _key = Convert.ToBase64String(bytes); // 32 bytes for AES-256
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_key);
        aes.GenerateIV();

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

        using var ms = new MemoryStream();
        // Prepend IV to the stream
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_key);

        // Extract IV (first 16 bytes for AES)
        var iv = new byte[16];
        Array.Copy(fullCipher, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var ms = new MemoryStream(fullCipher.AsSpan(16).ToArray()); // Skip IV
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);

        return sr.ReadToEnd();
    }
}
