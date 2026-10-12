namespace Aiursoft.EmployeeCenter.Entities;

public enum FinanceAccountType
{
    /// <summary>
    /// Things you own (Cash, Bank, Accounts Receivable)
    /// </summary>
    Asset,

    /// <summary>
    /// Things you owe (Loans, Accounts Payable, Director's Loan)
    /// </summary>
    Liability,

    /// <summary>
    /// Owners' investment (Share Capital, Retained Earnings)
    /// </summary>
    Equity,

    /// <summary>
    /// Money coming in
    /// </summary>
    Income,

    /// <summary>
    /// Money going out
    /// </summary>
    Expense
}
