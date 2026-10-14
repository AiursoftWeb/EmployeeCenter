using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public sealed class QualificationFileService(
    StorageService storage,
    QualificationWatermarkService watermark,
    ILogger<QualificationFileService> logger) : ITransientDependency
{
    public const string OriginalsFolder = "qualifications/originals";
    public const string CopiesFolder = "qualifications/copies";
    private const string UploadsFolder = "qualifications/uploads";
    private const string DetectionsFolder = "qualifications/detections";

    public static string UploadSubfolder(string userId, bool detection = false, bool unique = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var owner = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
        var folder = $"{(detection ? DetectionsFolder : UploadsFolder)}/{owner}";
        return unique ? $"{folder}/{Guid.NewGuid():N}" : folder;
    }

    public async Task<BusinessQualification> PrepareOriginalAsync(
        string uploadedPath, string userId, int companyId, string name, string version,
        CancellationToken cancellationToken)
    {
        var sourcePath = UploadedPhysicalPath(uploadedPath, userId, detection: false);
        await using var source = File.OpenRead(sourcePath);
        watermark.ValidateSource(source);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken));
        source.Position = 0;
        var extension = Path.GetExtension(uploadedPath);
        var originalPath = await storage.SaveFromStream($"{OriginalsFolder}/{Guid.NewGuid():N}{extension}",
            source, isVault: true, cancellationToken);
        source.Close();
        DeleteUploadedFile(uploadedPath, userId, detection: false);
        return new BusinessQualification
        {
            CompanyEntityId = companyId,
            Name = name,
            Version = version,
            OriginalPath = originalPath,
            OriginalSha256 = hash,
            CreatedById = userId
        };
    }

    public async Task<QualificationDownload> GenerateAsync(
        BusinessQualification qualification, User applicant, string applicantName, string purpose, string recipient,
        string watermarkLanguage, CancellationToken cancellationToken)
    {
        var traceId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var physicalPath = storage.GetVaultSubfolderFilePhysicalPath(qualification.OriginalPath, OriginalsFolder);
        await using var source = File.OpenRead(physicalPath);
        watermark.ValidateSource(source);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken));
        if (!string.Equals(hash, qualification.OriginalSha256, StringComparison.Ordinal))
            throw new InvalidDataException("The qualification original failed its integrity check.");
        source.Position = 0;
        var generated = watermark.Generate(source,
            new QualificationWatermarkRequest(purpose, recipient, applicantName, createdAt, traceId, watermarkLanguage));
        await using var output = new MemoryStream(generated);
        var generatedPath = await storage.SaveFromStream($"{CopiesFolder}/{traceId:N}.png", output,
            isVault: true, cancellationToken);
        var record = new QualificationDownload
        {
            Id = traceId,
            BusinessQualificationId = qualification.Id,
            ApplicantId = applicant.Id,
            ApplicantName = applicantName,
            QualificationName = qualification.Name,
            CompanyName = qualification.CompanyEntity.CompanyName,
            Version = qualification.Version,
            OriginalSha256 = qualification.OriginalSha256,
            Purpose = purpose,
            Recipient = recipient,
            GeneratedPath = generatedPath,
            GeneratedSha256 = Convert.ToHexString(SHA256.HashData(generated)),
            CreatedAt = createdAt
        };
        try
        {
            await EnsureArchiveAsync(record, cancellationToken);
            return record;
        }
        catch
        {
            DeleteCopy(record);
            throw;
        }
    }

    public async Task EnsureArchiveAsync(QualificationDownload record, CancellationToken cancellationToken)
    {
        if (record.ArchivePath != null && File.Exists(storage.GetVaultSubfolderFilePhysicalPath(record.ArchivePath, CopiesFolder)))
            return;
        var source = CopyPhysicalPath(record) ?? throw new FileNotFoundException("The generated image is missing.");
        var bytes = await File.ReadAllBytesAsync(source, cancellationToken);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), record.GeneratedSha256, StringComparison.Ordinal))
            throw new InvalidDataException("The generated image failed its integrity check.");
        await using var archiveBytes = new MemoryStream();
        using (var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"qualification-{record.Id:N}.png", CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(bytes, cancellationToken);
        }
        archiveBytes.Position = 0;
        record.ArchivePath = await storage.SaveFromStream($"{CopiesFolder}/{record.Id:N}.zip", archiveBytes,
            isVault: true, cancellationToken);
        record.ArchiveSha256 = Convert.ToHexString(SHA256.HashData(archiveBytes.ToArray()));
    }

    public Guid? Detect(string uploadedPath, string userId)
    {
        var physicalPath = UploadedPhysicalPath(uploadedPath, userId, detection: true);
        try
        {
            using var source = File.OpenRead(physicalPath);
            watermark.ValidateSource(source);
            source.Position = 0;
            return watermark.Detect(source);
        }
        finally
        {
            DeleteUploadedFile(uploadedPath, userId, detection: true);
        }
    }

    public string? CopyPhysicalPath(QualificationDownload record)
    {
        var path = storage.GetVaultSubfolderFilePhysicalPath(record.GeneratedPath, CopiesFolder);
        return File.Exists(path) ? path : null;
    }

    public void DeleteOriginal(BusinessQualification qualification) =>
        DeletePrivateFile(qualification.OriginalPath, OriginalsFolder);

    public void DeleteCopy(QualificationDownload record)
    {
        DeletePrivateFile(record.GeneratedPath, CopiesFolder);
        if (record.ArchivePath != null) DeletePrivateFile(record.ArchivePath, CopiesFolder);
    }

    public void DeleteExpiredUploads(DateTime cutoff)
    {
        foreach (var subfolder in new[] { UploadsFolder, DetectionsFolder })
        {
            var folder = storage.GetFilePhysicalPath(subfolder, isVault: true);
            if (!Directory.Exists(folder)) continue;
            foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(path) >= cutoff) continue;
                try { File.Delete(path); }
                catch (IOException exception) { logger.LogWarning(exception, "Unable to remove expired qualification upload."); }
            }
        }
    }

    private string UploadedPhysicalPath(string path, string userId, bool detection)
    {
        var subfolder = UploadSubfolder(userId, detection);
        var physicalPath = storage.GetVaultSubfolderFilePhysicalPath(path, subfolder);
        if (!File.Exists(physicalPath)) throw new FileNotFoundException("The uploaded image is missing. Please upload it again.");
        return physicalPath;
    }

    private void DeleteUploadedFile(string path, string userId, bool detection) =>
        DeletePrivateFile(path, UploadSubfolder(userId, detection));

    private void DeletePrivateFile(string path, string subfolder)
    {
        try { File.Delete(storage.GetVaultSubfolderFilePhysicalPath(path, subfolder)); }
        catch (IOException exception) { logger.LogError(exception, "Unable to clean up qualification file {Path}", path); }
    }
}
