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
    private readonly IDataProtector _secretProtector =
        dataProtectionProvider.CreateProtector("EmployeeCenter/PasswordVault/Secrets/v1");
    private readonly IDataProtector _noteProtector =
        dataProtectionProvider.CreateProtector("EmployeeCenter/PasswordVault/Notes/v1");

    /// <summary>
    /// Protects plaintext submitted by a user. This method deliberately does not
    /// treat the envelope prefix as proof that the input is already protected:
    /// users are allowed to choose a secret that happens to start with that text.
    /// </summary>
    public string ProtectPlaintext(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return ProtectedValuePrefix + _secretProtector.Protect(plaintext);
    }

    public string? ProtectNote(string? plaintext) =>
        plaintext == null ? null : ProtectedValuePrefix + _noteProtector.Protect(plaintext);

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
            return _secretProtector.Unprotect(storedValue[ProtectedValuePrefix.Length..]);
        }
        catch (CryptographicException ex)
        {
            logger.LogCritical(ex, "Could not decrypt a password-vault secret");
            throw new InvalidOperationException(
                "A password-vault secret could not be decrypted with the current Data Protection key ring.", ex);
        }
    }

    public string? UnprotectStoredNote(string? storedValue)
    {
        if (storedValue == null || !IsProtected(storedValue))
        {
            // Backward compatibility while an older database is being migrated.
            return storedValue;
        }

        try
        {
            return _noteProtector.Unprotect(storedValue[ProtectedValuePrefix.Length..]);
        }
        catch (CryptographicException ex)
        {
            logger.LogCritical(ex, "Could not decrypt a password-vault note");
            throw new InvalidOperationException(
                "A password-vault note could not be decrypted with the current Data Protection key ring.", ex);
        }
    }

    /// <summary>
    /// Idempotent startup data migration. It has an effect only while legacy
    /// plaintext rows exist, so interrupted or concurrent deployments can retry it safely.
    /// </summary>
    public async Task<int> MigrateLegacySecretsAsync(CancellationToken cancellationToken = default)
    {
        var legacyPasswords = await dbContext.Passwords
            .Where(password =>
                !password.Secret.StartsWith(ProtectedValuePrefix) ||
                (password.Note != null && !password.Note.StartsWith(ProtectedValuePrefix)))
            .ToListAsync(cancellationToken);

        if (legacyPasswords.Count == 0)
        {
            logger.LogInformation("Password-vault value migration is already complete");
            return 0;
        }

        var protectedValueCount = 0;
        foreach (var password in legacyPasswords)
        {
            if (!IsProtected(password.Secret))
            {
                password.Secret = ProtectPlaintext(password.Secret);
                protectedValueCount++;
            }

            if (password.Note != null && !IsProtected(password.Note))
            {
                password.Note = ProtectNote(password.Note);
                protectedValueCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Protected {ValueCount} legacy password-vault values across {PasswordCount} records",
            protectedValueCount,
            legacyPasswords.Count);
        return protectedValueCount;
    }

    private static bool IsProtected(string storedValue) =>
        storedValue.StartsWith(ProtectedValuePrefix, StringComparison.Ordinal);
}
