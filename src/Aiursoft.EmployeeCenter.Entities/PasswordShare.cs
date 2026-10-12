using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.EmployeeCenter.Entities;

public class PasswordShare
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid PasswordId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(PasswordId))]
    [NotNull]
    public Password? Password { get; set; }

    /// <summary>
    /// The user this password is shared with.
    /// If null, it means this share is not for a specific user but maybe for a role.
    /// </summary>
    [MaxLength(64)]
    public required string? SharedWithUserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(SharedWithUserId))]
    [NotNull]
    public User? SharedWithUser { get; set; }

    /// <summary>
    /// The role this password is shared with.
    /// If null, it means this share is not for a specific role.
    /// </summary>
    [MaxLength(450)]
    public required string? SharedWithRoleId { get; set; }

    public required SharePermission Permission { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
