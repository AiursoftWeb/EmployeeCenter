using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class BlueprintFolder
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public int? ParentFolderId { get; set; }
    [ForeignKey(nameof(ParentFolderId))]
    public BlueprintFolder? ParentFolder { get; set; }

    public ICollection<BlueprintFolder> SubFolders { get; set; } = new List<BlueprintFolder>();
    public ICollection<Blueprint> Blueprints { get; set; } = new List<Blueprint>();

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
