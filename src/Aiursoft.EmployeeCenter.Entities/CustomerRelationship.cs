using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class CustomerRelationship
{
    [Key]
    public int Id { get; set; }

    public int? CompanyEntityId { get; set; }

    [ForeignKey(nameof(CompanyEntityId))]
    public CompanyEntity? CompanyEntity { get; set; }

    [MaxLength(100)]
    [Display(Name = "Name")]
    public required string Name { get; set; }

    [MaxLength(100)]
    [Display(Name = "Job Title")]
    public string? JobTitle { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone]
    [MaxLength(50)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [MaxLength(500)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [MaxLength(100)]
    [Display(Name = "WeChat ID")]
    public string? WeChatId { get; set; }

    [Display(Name = "Remark")]
    public string? Remark { get; set; }

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
