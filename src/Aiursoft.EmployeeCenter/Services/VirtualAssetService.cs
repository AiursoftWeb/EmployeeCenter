using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Aiursoft.EmployeeCenter.Services;

public class VirtualAssetService : IScopedDependency
{
    private readonly TemplateDbContext _dbContext;
    private readonly EncryptionService _encryptionService;

    public VirtualAssetService(
        TemplateDbContext dbContext,
        EncryptionService encryptionService)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
    }

    public async Task<string> GetPasswordAsync(string userId, Guid assetId, string totpCode)
    {
        var asset = await _dbContext.VirtualAssets.FindAsync(assetId);
        if (asset == null)
        {
            throw new Exception("Asset not found");
        }

        // Verify TOTP
        bool mfaVerified = false;
        if (!string.IsNullOrEmpty(asset.EncryptedTotpSecret))
        {
            // Decrypt Secret
            var secret = _encryptionService.Decrypt(asset.EncryptedTotpSecret);
            var totp = new Totp(Base32Encoding.ToBytes(secret));

            // Otp.NET 1.4+ uses VerificationWindow class
            mfaVerified = totp.VerifyTotp(totpCode, out _, new VerificationWindow(previous: 1, future: 1));

            if (!mfaVerified)
            {
                throw new Exception("Invalid TOTP code");
            }
        }
        else
        {
            // 如果没有配置 TOTP Secret
            mfaVerified = true;
        }

        // Log Access
        var log = new VirtualAssetAccessLog
        {
            AssetId = assetId,
            UserId = userId,
            AccessTime = DateTime.UtcNow,
            MfaVerified = mfaVerified,
            IpAddress = "Unknown"
        };
        _dbContext.VirtualAssetAccessLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        // Check High Risk Alert
        if (asset.IsHighRisk)
        {
            // TODO: Trigger IM Alert / Email
        }

        return _encryptionService.Decrypt(asset.EncryptedPassword);
    }
}
