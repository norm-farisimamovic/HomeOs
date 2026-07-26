using HomeOs.Modules.Finance.Contracts;
using HomeOs.Modules.Finance.Domain;
using HomeOs.Modules.Finance.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Events;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Money;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Finance.Features;

/// <summary>Finance: transactions (expense/income), a monthly summary + who-owes, and recurring bills.</summary>
public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finance").RequireAuthorization().WithTags("Finance");

        group.MapGet("/transactions", ListTransactionsAsync).WithName("ListTransactions");
        group.MapPost("/transactions", CreateTransactionAsync).WithName("CreateTransaction");
        group.MapDelete("/transactions/{id:guid}", DeleteTransactionAsync).WithName("DeleteTransaction");
        group.MapGet("/summary", SummaryAsync).WithName("FinanceSummary");
        group.MapGet("/bills", ListBillsAsync).WithName("ListBills");
        group.MapPost("/bills", CreateBillAsync).WithName("CreateBill");
        group.MapDelete("/bills/{id:guid}", DeleteBillAsync).WithName("DeleteBill");
        group.MapGet("/budgets", ListBudgetsAsync).WithName("ListBudgets");
        group.MapPut("/budgets", SaveBudgetAsync).WithName("SaveBudget");
        group.MapDelete("/budgets/{category}", DeleteBudgetAsync).WithName("DeleteBudget");

        return app;
    }

    private static async Task<IResult> ListBudgetsAsync(ICurrentMember me, FinanceDbContext db, CancellationToken ct)
    {
        var today = Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var currency = me.PreferredCurrency;

        var budgets = await db.Budgets.AsNoTracking().Where(b => b.HouseholdId == me.HouseholdId).ToListAsync(ct);
        if (budgets.Count == 0) return Results.Ok(Array.Empty<BudgetDto>());

        var expenses = await VisibleTx(db, me)
            .Where(t => t.Kind == TransactionKind.Expense && t.OccurredOn >= monthStart && t.OccurredOn < monthEnd)
            .ToListAsync(ct);

        var dtos = budgets.Select(b =>
        {
            var limit = Currencies.Convert(b.MonthlyLimit, b.Currency, currency);
            var spent = expenses.Where(t => t.Category == b.Category).Sum(t => Currencies.Convert(t.Amount, t.Currency, currency));
            var percent = limit > 0 ? (int)Math.Round(spent / limit * 100) : 0;
            return new BudgetDto(b.Category, limit, spent, Math.Round(limit - spent, 2), percent, currency);
        }).OrderByDescending(b => b.Percent).ToList();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> SaveBudgetAsync(SaveBudgetRequest req, ICurrentMember me, FinanceDbContext db, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Category) || req.MonthlyLimit <= 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.finance.txRequired"]] });

        var category = req.Category.Trim();
        var currency = Currencies.Normalize(me.PreferredCurrency);
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.HouseholdId == me.HouseholdId && b.Category == category, ct);
        if (budget is null)
            db.Budgets.Add(Budget.Create(me.HouseholdId, category, req.MonthlyLimit, currency));
        else
            budget.SetLimit(req.MonthlyLimit, currency);

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteBudgetAsync(string category, ICurrentMember me, FinanceDbContext db, CancellationToken ct)
    {
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.HouseholdId == me.HouseholdId && b.Category == category, ct);
        if (budget is null) return Results.NotFound();
        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListTransactionsAsync(ICurrentMember me, FinanceDbContext db, IMemberDirectory dir, CancellationToken ct)
    {
        var cur = me.PreferredCurrency;
        var tx = await VisibleTx(db, me).OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        // Amounts are shown in the member's preferred currency (converted from each entry's own currency).
        return Results.Ok(tx.Select(t => new TransactionDto(
            t.Id, t.Kind.ToString(), Currencies.Convert(t.Amount, t.Currency, cur), cur, t.Category, t.OccurredOn.ToString("yyyy-MM-dd"),
            t.Description, t.PaidById, names.GetValueOrDefault(t.PaidById))).ToList());
    }

    private static async Task<IResult> CreateTransactionAsync(
        CreateTransactionRequest req, ICurrentMember me, FinanceDbContext db, IMemberDirectory dir, IEventBus bus, IAppText text, CancellationToken ct)
    {
        if (req.Amount <= 0 || string.IsNullOrWhiteSpace(req.Category))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.finance.txRequired"]] });

        var kind = Enum.TryParse<TransactionKind>(req.Kind, true, out var k) ? k : TransactionKind.Expense;
        // New entries are recorded in the member's preferred currency unless one is given.
        var currency = Currencies.Normalize(req.Currency ?? me.PreferredCurrency);
        var tx = Transaction.Create(me.HouseholdId, me.Id, kind, req.Amount, currency, req.Category,
            ParseDate(req.OccurredOn) ?? Today(), req.Description, req.PaidById ?? me.Id, ParseVisibility(req.Visibility));

        db.Transactions.Add(tx);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new TransactionAdded(tx.Id, tx.HouseholdId, tx.Amount, tx.Category), ct);

        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        var cur = me.PreferredCurrency;
        return Results.Created($"/api/finance/transactions/{tx.Id}", new TransactionDto(
            tx.Id, tx.Kind.ToString(), Currencies.Convert(tx.Amount, tx.Currency, cur), cur, tx.Category, tx.OccurredOn.ToString("yyyy-MM-dd"),
            tx.Description, tx.PaidById, names.GetValueOrDefault(tx.PaidById)));
    }

    private static async Task<IResult> DeleteTransactionAsync(Guid id, ICurrentMember me, FinanceDbContext db, CancellationToken ct)
    {
        var tx = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.HouseholdId == me.HouseholdId, ct);
        if (tx is null || !CanEdit(me, tx.Visibility, tx.OwnerId)) return Results.NotFound();
        db.Transactions.Remove(tx);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SummaryAsync(ICurrentMember me, FinanceDbContext db, IMemberDirectory dir, CancellationToken ct)
    {
        var today = Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var currency = me.PreferredCurrency;
        var tx = await VisibleTx(db, me).Where(t => t.OccurredOn >= monthStart && t.OccurredOn < monthEnd).ToListAsync(ct);
        // Convert every amount into the member's currency before aggregating.
        decimal Amt(Transaction t) => Currencies.Convert(t.Amount, t.Currency, currency);
        var expenses = tx.Where(t => t.Kind == TransactionKind.Expense).ToList();
        var income = tx.Where(t => t.Kind == TransactionKind.Income).Sum(Amt);
        var spent = expenses.Sum(Amt);

        var byCategory = expenses.GroupBy(t => t.Category)
            .Select(g => new CategoryTotalDto(g.Key, g.Sum(Amt)))
            .OrderByDescending(c => c.Amount).ToList();

        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        var memberCount = Math.Max(names.Count, 1);
        var fairShare = Math.Round(spent / memberCount, 2);
        var members = names.Select(kv =>
        {
            var paid = expenses.Where(t => t.PaidById == kv.Key).Sum(Amt);
            return new MemberBalanceDto(kv.Key, kv.Value, paid, Math.Round(paid - fairShare, 2));
        }).OrderByDescending(m => m.Paid).ToList();

        var dueSoon = await VisibleBills(db, me).Where(b => b.NextDue >= today && b.NextDue <= today.AddDays(30)).ToListAsync(ct);
        var dueSoonAmount = dueSoon.Sum(b => Currencies.Convert(b.Amount, b.Currency, currency));

        return Results.Ok(new FinanceSummaryDto(
            monthStart.ToString("yyyy-MM"), currency, income, spent, income - spent,
            byCategory, members, dueSoon.Count, dueSoonAmount));
    }

    private static async Task<IResult> ListBillsAsync(ICurrentMember me, FinanceDbContext db, IMemberDirectory dir, CancellationToken ct)
    {
        var today = Today();
        var cur = me.PreferredCurrency;
        var bills = await VisibleBills(db, me).OrderBy(b => b.NextDue).ToListAsync(ct);
        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        return Results.Ok(bills.Select(b => new BillDto(
            b.Id, b.Name, Currencies.Convert(b.Amount, b.Currency, cur), cur, b.Cadence.ToString(), b.NextDue.ToString("yyyy-MM-dd"),
            b.Category, b.WhoPaysId, b.WhoPaysId is { } w ? names.GetValueOrDefault(w) : null,
            b.NextDue.DayNumber - today.DayNumber)).ToList());
    }

    private static async Task<IResult> CreateBillAsync(
        CreateBillRequest req, ICurrentMember me, FinanceDbContext db, IMemberDirectory dir, IEventBus bus, IAppText text, CancellationToken ct)
    {
        if (req.Amount <= 0 || string.IsNullOrWhiteSpace(req.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.finance.billRequired"]] });

        var cadence = Enum.TryParse<BillCadence>(req.Cadence, true, out var c) ? c : BillCadence.Monthly;
        var currency = Currencies.Normalize(req.Currency ?? me.PreferredCurrency);
        // No explicit payer → the person adding it is responsible (matches the "Me" default in the UI).
        var bill = Bill.Create(me.HouseholdId, me.Id, req.Name, req.Amount, currency, cadence,
            ParseDate(req.NextDue) ?? Today(), req.Category, req.WhoPaysId ?? me.Id, ParseVisibility(req.Visibility));

        db.Bills.Add(bill);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new BillAdded(bill.Id, bill.HouseholdId, bill.Name, bill.NextDue), ct);
        await bus.PublishAsync(new AppActivity(bill.HouseholdId, me.Id, "bill.added", bill.Name, "/finance"), ct);

        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        var cur = me.PreferredCurrency;
        return Results.Created($"/api/finance/bills/{bill.Id}", new BillDto(
            bill.Id, bill.Name, Currencies.Convert(bill.Amount, bill.Currency, cur), cur, bill.Cadence.ToString(), bill.NextDue.ToString("yyyy-MM-dd"),
            bill.Category, bill.WhoPaysId, bill.WhoPaysId is { } w ? names.GetValueOrDefault(w) : null,
            bill.NextDue.DayNumber - Today().DayNumber));
    }

    private static async Task<IResult> DeleteBillAsync(Guid id, ICurrentMember me, FinanceDbContext db, CancellationToken ct)
    {
        var bill = await db.Bills.FirstOrDefaultAsync(b => b.Id == id && b.HouseholdId == me.HouseholdId, ct);
        if (bill is null || !CanEdit(me, bill.Visibility, bill.OwnerId)) return Results.NotFound();
        db.Bills.Remove(bill);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---- helpers (same role-based visibility as the rest of the platform) ----
    private static IQueryable<Transaction> VisibleTx(FinanceDbContext db, ICurrentMember me)
    {
        var q = db.Transactions.AsNoTracking().Where(t => t.HouseholdId == me.HouseholdId);
        return me.IsManager
            ? q.Where(t => t.Visibility != Visibility.Private || t.OwnerId == me.Id || t.PaidById == me.Id)
            : q.Where(t => t.OwnerId == me.Id || t.PaidById == me.Id || t.Visibility == Visibility.Household);
    }

    private static IQueryable<Bill> VisibleBills(FinanceDbContext db, ICurrentMember me)
    {
        var q = db.Bills.AsNoTracking().Where(b => b.HouseholdId == me.HouseholdId);
        return me.IsManager
            ? q.Where(b => b.Visibility != Visibility.Private || b.OwnerId == me.Id)
            : q.Where(b => b.OwnerId == me.Id || b.Visibility == Visibility.Household);
    }

    private static bool CanEdit(ICurrentMember me, Visibility visibility, Guid ownerId) =>
        (me.IsManager && visibility != Visibility.Private) || ownerId == me.Id;

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
    private static DateOnly? ParseDate(string? v) => DateOnly.TryParse(v, out var d) ? d : null;
    private static Visibility ParseVisibility(string? v) => Enum.TryParse<Visibility>(v, true, out var x) ? x : Visibility.Household;
}
