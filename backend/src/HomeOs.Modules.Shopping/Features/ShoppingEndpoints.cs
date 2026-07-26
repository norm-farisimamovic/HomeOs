using HomeOs.Modules.Shopping.Domain;
using HomeOs.Modules.Shopping.Persistence;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Shopping.Features;

/// <summary>Shared household lists — everyone in the home sees and edits them.</summary>
public static class ShoppingEndpoints
{
    public static IEndpointRouteBuilder MapShoppingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shopping").RequireAuthorization().WithTags("Shopping");

        group.MapGet("/lists", async (ICurrentMember me, ShoppingDbContext db, CancellationToken ct) =>
        {
            var lists = await db.Lists.AsNoTracking().Include(l => l.Items)
                .Where(l => l.HouseholdId == me.HouseholdId)
                .OrderBy(l => l.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(lists.Select(ToDto).ToList());
        }).WithName("ListShopping");

        group.MapPost("/lists", async (SaveListRequest req, ICurrentMember me, ShoppingDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest();
            var list = ShoppingList.Create(me.HouseholdId, req.Name);
            db.Lists.Add(list);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/shopping/lists/{list.Id}", ToDto(list));
        }).WithName("CreateShoppingList");

        group.MapDelete("/lists/{id:guid}", async (Guid id, ICurrentMember me, ShoppingDbContext db, CancellationToken ct) =>
        {
            var list = await db.Lists.FirstOrDefaultAsync(l => l.Id == id && l.HouseholdId == me.HouseholdId, ct);
            if (list is null) return Results.NotFound();
            db.Lists.Remove(list);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithName("DeleteShoppingList");

        group.MapPost("/lists/{id:guid}/items", async (Guid id, SaveItemRequest req, ICurrentMember me, ShoppingDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Text)) return Results.BadRequest();
            var list = await db.Lists.FirstOrDefaultAsync(l => l.Id == id && l.HouseholdId == me.HouseholdId, ct);
            if (list is null) return Results.NotFound();
            var item = ShoppingItem.Create(id, req.Text);
            db.Items.Add(item);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ItemDto(item.Id, item.Text, item.Done));
        }).WithName("AddShoppingItem");

        group.MapPost("/items/{id:guid}/toggle", async (Guid id, ICurrentMember me, ShoppingDbContext db, CancellationToken ct) =>
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct);
            if (item is null || !await OwnsItem(db, me, item, ct)) return Results.NotFound();
            item.Toggle();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ItemDto(item.Id, item.Text, item.Done));
        }).WithName("ToggleShoppingItem");

        group.MapDelete("/items/{id:guid}", async (Guid id, ICurrentMember me, ShoppingDbContext db, CancellationToken ct) =>
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct);
            if (item is null || !await OwnsItem(db, me, item, ct)) return Results.NotFound();
            db.Items.Remove(item);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithName("DeleteShoppingItem");

        return app;
    }

    private static Task<bool> OwnsItem(ShoppingDbContext db, ICurrentMember me, ShoppingItem item, CancellationToken ct) =>
        db.Lists.AnyAsync(l => l.Id == item.ListId && l.HouseholdId == me.HouseholdId, ct);

    private static ListDto ToDto(ShoppingList l) => new(
        l.Id, l.Name,
        l.Items.OrderBy(i => i.Done).ThenBy(i => i.CreatedAtUtc).Select(i => new ItemDto(i.Id, i.Text, i.Done)).ToList());
}

/// <summary>A list with its items.</summary>
public sealed record ListDto(Guid Id, string Name, IReadOnlyList<ItemDto> Items);
/// <summary>A list item.</summary>
public sealed record ItemDto(Guid Id, string Text, bool Done);
/// <summary>Create/rename a list.</summary>
public sealed record SaveListRequest(string Name);
/// <summary>Add an item.</summary>
public sealed record SaveItemRequest(string Text);
