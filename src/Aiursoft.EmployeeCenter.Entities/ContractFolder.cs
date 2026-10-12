using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class ContractFolder
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public int? ParentFolderId { get; set; }
    [ForeignKey(nameof(ParentFolderId))]
    public ContractFolder? ParentFolder { get; set; }

    public ICollection<ContractFolder> SubFolders { get; set; } = new List<ContractFolder>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
