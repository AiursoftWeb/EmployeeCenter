
namespace Aiursoft.EmployeeCenter.Entities;

public enum AssetStatus
{
    Available = 0, // 可用
    InUse = 1,     // 领用
    InRepair = 2,  // 维修中
    Lost = 3,      // 损失
    Frozen = 4     // 冻结
}