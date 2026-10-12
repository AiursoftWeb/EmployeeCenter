namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>
/// Status of a reimbursement application
/// </summary>
public enum ReimbursementStatus
{
    /// <summary>
    /// Draft status, not yet submitted
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Submitted and waiting for review
    /// </summary>
    Applying = 1,

    /// <summary>
    /// Revoked by the submitter
    /// </summary>
    Revoked = 2,

    /// <summary>
    /// Acknowledged by the reviewer, but not yet reimbursed
    /// </summary>
    Acknowledged = 3,

    /// <summary>
    /// Reimbursed
    /// </summary>
    Reimbursed = 4,

    /// <summary>
    /// Rejected by the reviewer
    /// </summary>
    Rejected = 5
}
