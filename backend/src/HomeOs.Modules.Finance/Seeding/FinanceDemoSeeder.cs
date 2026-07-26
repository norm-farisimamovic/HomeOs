using HomeOs.Modules.Finance.Domain;
using HomeOs.Modules.Finance.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.Finance.Seeding;

/// <summary>Dev-only: seeds a few transactions + bills into the demo household so Finance has data to show.</summary>
public sealed class FinanceDemoSeeder(
    IHostEnvironment env, IConfiguration config, IHouseholdLookup households, IMemberDirectory members, FinanceDbContext db) : IDataSeeder
{
    public int Order => 120;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!DemoMode.IsEnabled(env, config)) return;
        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } hid) return;
        if (await db.Transactions.AnyAsync(t => t.HouseholdId == hid, cancellationToken)) return;

        var people = await members.GetHouseholdMembersAsync(hid, cancellationToken);
        if (people.Count == 0) return;
        var ana = people[0].Id;
        var mirza = people.Count > 1 ? people[1].Id : ana;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var month = new DateOnly(today.Year, today.Month, 1);

        db.Transactions.AddRange(
            Transaction.Create(hid, ana, TransactionKind.Income, 1920m, "KM", "Salary", month.AddDays(14), "Salary", ana, Visibility.Household),
            Transaction.Create(hid, ana, TransactionKind.Expense, 184.20m, "KM", "Groceries", month.AddDays(21), "Weekly shop", ana, Visibility.Household),
            Transaction.Create(hid, mirza, TransactionKind.Expense, 90m, "KM", "Transport", month.AddDays(20), "Fuel", mirza, Visibility.Household),
            Transaction.Create(hid, ana, TransactionKind.Expense, 132.40m, "KM", "Utilities", month.AddDays(19), "Electricity", ana, Visibility.Household),
            Transaction.Create(hid, mirza, TransactionKind.Expense, 37.60m, "KM", "Health", month.AddDays(11), "Pharmacy", mirza, Visibility.Household));

        db.Bills.AddRange(
            Bill.Create(hid, ana, "BH Telecom", 46m, "KM", BillCadence.Monthly, today.AddDays(2), "Utilities", ana, Visibility.Household),
            Bill.Create(hid, ana, "Netflix", 21.50m, "KM", BillCadence.Monthly, today.AddDays(6), "Fun", ana, Visibility.Household),
            Bill.Create(hid, mirza, "Building maintenance", 18m, "KM", BillCadence.Monthly, today.AddDays(9), "Utilities", mirza, Visibility.Household));

        await db.SaveChangesAsync(cancellationToken);
    }
}
