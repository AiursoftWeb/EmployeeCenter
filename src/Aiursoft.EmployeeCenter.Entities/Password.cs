using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Password
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [MaxLength(100)]
    public required string Title { get; set; }

    /// <summary>
    /// The account name for this password.
    /// If null, it means there is no specific account name.
    /// </summary>
    [MaxLength(100)]
    public string? Account { get; set; }

    // Data Protection ciphertext is longer than the original plaintext (which is
    // still limited to 100 characters by the create/edit view models).
    [MaxLength(2000)]
    public required string Secret { get; set; }

    /// <summary>
    /// Note for this password.
    /// If null, it means no note was provided.
    /// </summary>
    // Data Protection ciphertext is longer than the original plaintext (which is
    // still limited to 1000 characters by the create/edit view models).
    [MaxLength(8000)]
    public string? Note { get; set; }

    [MaxLength(500)]
    public string? FilePath { get; set; }

    [MaxLength(255)]
    public required string CreatorId { get; set; }

    [ForeignKey(nameof(CreatorId))]
    [JsonIgnore]
    public User? Creator { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    [JsonIgnore]
    [InverseProperty(nameof(PasswordShare.Password))]
    public IEnumerable<PasswordShare> PasswordShares { get; init; } = new List<PasswordShare>();
}
