using System.Security.Cryptography;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

public sealed class PasswordSecretService(
    EmployeeCenterDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<PasswordSecretService> logger) : IScopedDependency
{
    internal const string ProtectedValuePrefix = "protected:v1:";
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("EmployeeCenter/PasswordVault/Secrets/v1");

    /// <summary>
    /// Protects plaintext submitted by a user. This method deliberately does not
    /// treat the envelope prefix as proof that the input is already protected:
    /// users are allowed to choose a secret that happens to start with that text.
    /// </summary>
    public string ProtectPlaintext(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return ProtectedValuePrefix + _protector.Protect(plaintext);
    }

    public string UnprotectStoredValue(string storedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(storedValue);
        if (!IsProtected(storedValue))
        {
            // Backward compatibility while an older database is being migrated.
            return storedValue;
        }

        try
        {
            return _protector.Unprotect(storedValue[ProtectedValuePrefix.Length..]);
        }
        catch (CryptographicException ex)
        {
            logger.LogCritical(ex, "Could not decrypt a password-vault secret");
            throw new InvalidOperationException(
                "A password-vault secret could not be decrypted with the current Data Protection key ring.", ex);
        }
    }

    /// <summary>
    /// Idempotent startup data migration. It has an effect only while legacy
    /// plaintext rows exist, so interrupted or concurrent deployments can retry it safely.
    /// </summary>
    public async Task<int> MigrateLegacySecretsAsync(CancellationToken cancellationToken = default)
    {
        var legacyPasswords = await dbContext.Passwords
            .Where(password => !password.Secret.StartsWith(ProtectedValuePrefix))
            .ToListAsync(cancellationToken);

        if (legacyPasswords.Count == 0)
        {
            logger.LogInformation("Password-vault secret migration is already complete");
            return 0;
        }

        foreach (var password in legacyPasswords)
        {
            password.Secret = ProtectPlaintext(password.Secret);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Protected {PasswordCount} legacy password-vault secrets",
            legacyPasswords.Count);
        return legacyPasswords.Count;
    }

    private static bool IsProtected(string storedValue) =>
        storedValue.StartsWith(ProtectedValuePrefix, StringComparison.Ordinal);
}
