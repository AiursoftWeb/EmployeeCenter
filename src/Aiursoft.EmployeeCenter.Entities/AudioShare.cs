using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>
/// Shares a meeting recording with a specific user or role, mirroring <see cref="PasswordShare"/>.
/// </summary>
public class AudioShare
{
    [Key]
    public int Id { get; set; }

    public required int AudioId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AudioId))]
    [NotNull]
    public Audio? Audio { get; set; }

    /// <summary>
    /// The user this recording is shared with.
    /// If null, this share targets a role instead.
    /// </summary>
    [MaxLength(64)]
    public string? SharedWithUserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(SharedWithUserId))]
    public User? SharedWithUser { get; set; }

    /// <summary>
    /// The role this recording is shared with.
    /// If null, this share targets a specific user.
    /// </summary>
    [MaxLength(450)]
    public string? SharedWithRoleId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(SharedWithRoleId))]
    public IdentityRole? SharedWithRole { get; set; }

    public required SharePermission Permission { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
