namespace Aiursoft.EmployeeCenter.Services;

public record QualificationWatermarkRequest(
    string Purpose,
    string Recipient,
    string ApplicantName,
    DateTime CreatedAt,
    Guid TraceId,
    string WatermarkLanguage = "auto");
