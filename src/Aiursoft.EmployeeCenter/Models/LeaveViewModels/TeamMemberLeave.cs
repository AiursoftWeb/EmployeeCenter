using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Models.LeaveViewModels;

public class TeamMemberLeave
{
    public required User User { get; set; }
    public required List<LeaveApplication> Leaves { get; set; }
    public required string Relation { get; set; } // Boss, Direct Report, Colleague
}
