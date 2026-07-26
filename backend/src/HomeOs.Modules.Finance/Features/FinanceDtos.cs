namespace HomeOs.Modules.Finance.Features;

public sealed record TransactionDto(
    Guid Id, string Kind, decimal Amount, string Currency, string Category,
    string OccurredOn, string? Description, Guid PaidById, string? PaidByName);

public sealed record BillDto(
    Guid Id, string Name, decimal Amount, string Currency, string Cadence, string NextDue,
    string Category, Guid? WhoPaysId, string? WhoPaysName, int DueInDays);

public sealed record CategoryTotalDto(string Category, decimal Amount);

public sealed record MemberBalanceDto(Guid MemberId, string Name, decimal Paid, decimal Net);

public sealed record FinanceSummaryDto(
    string Month, string Currency, decimal Income, decimal Spent, decimal Balance,
    IReadOnlyList<CategoryTotalDto> ByCategory, IReadOnlyList<MemberBalanceDto> Members,
    int DueSoonCount, decimal DueSoonAmount);

public sealed record CreateTransactionRequest(
    string Kind, decimal Amount, string? Currency, string Category, string? OccurredOn,
    string? Description, Guid? PaidById, string? Visibility);

public sealed record CreateBillRequest(
    string Name, decimal Amount, string? Currency, string Cadence, string NextDue,
    string Category, Guid? WhoPaysId, string? Visibility);

/// <summary>A category budget with this month's progress, in the requesting member's currency.</summary>
public sealed record BudgetDto(string Category, decimal Limit, decimal Spent, decimal Remaining, int Percent, string Currency);

/// <summary>Set/replace a category's monthly budget (interpreted in the member's currency).</summary>
public sealed record SaveBudgetRequest(string Category, decimal MonthlyLimit);
