using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Models.AudioViewModels;

public class AudioAccessContextViewModel
{
    public string? OwnerId { get; set; }

    public string? OwnerDisplayName { get; set; }

    public List<AudioAccessSourceViewModel> Sources { get; set; } = [];

    public SharePermission EffectivePermission { get; set; }
}

public class AudioAccessSourceViewModel
{
    public AudioAccessSourceType Type { get; set; }

    public string? RoleName { get; set; }

    public SharePermission Permission { get; set; }
}

public enum AudioAccessSourceType
{
    Owner,
    AudioManagementPermission,
    DirectShare,
    RoleShare
}
