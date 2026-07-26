# Database, Migrations & Seeding

How Home OS bootstraps its database, how to evolve the schema, and how to add **initial seed data**.
Engineering conventions live in `.claude/skills/dotnet-backend`; this is the practical how-to.

## How startup works

On every API start, `Program.cs` calls `app.InitializeHomeOsDatabaseAsync()` which:

1. **Creates the database if it doesn't exist** and **applies pending EF Core migrations**
   (`Database.MigrateAsync()`), then
2. **Runs every registered data seeder** in order (idempotently).

Both steps are config-gated (defaults `true`):

```jsonc
// appsettings.json
"Database": {
  "AutoMigrate": true,   // create DB + apply migrations on startup
  "Seed": true           // run data seeders on startup
}
```

If the database is unreachable, startup logs an error and the app **still starts** (so `/health/ready`
surfaces the problem) instead of crash-looping.

> **Prod note:** auto-migrate on startup is convenient and fine for a single instance. If we ever run
> multiple instances, set `Database:AutoMigrate=false` and apply migrations as a deploy step instead.

## Migrations

Migrations are owned per `DbContext`. The platform kernel uses `PlatformDbContext`; each app module
will own its own context. A design-time factory (`PlatformDbContextFactory`) lets the EF tools build
the context without booting the API.

### Prerequisites (once)

```bash
dotnet tool install --global dotnet-ef   # or: dotnet tool update --global dotnet-ef
```

### Add a migration (after changing entities)

```bash
dotnet ef migrations add <Name> \
  --project    backend/src/HomeOs.Platform \
  --startup-project backend/src/HomeOs.Api \
  --context    PlatformDbContext \
  --output-dir Persistence/Migrations
```

Naming: describe the change — `AddMembersAndHouseholds`, `AddTaskRecurrence`. It's applied
automatically on next run; to apply manually: `dotnet ef database update` (same `--project` flags).

### Undo the last (unapplied) migration

```bash
dotnet ef migrations remove --project backend/src/HomeOs.Platform --startup-project backend/src/HomeOs.Api --context PlatformDbContext
```

### Reset the local dev database

```bash
mysql -u root -p -e "DROP DATABASE IF EXISTS homeos;"
# next `dotnet run` recreates it, migrates, and re-seeds
```

## Seeding — entering initial data

A **seeder** inserts starting data (roles/permissions, a demo household, default categories, …).
Seeders implement `IDataSeeder`, are registered per module, and run once on startup after migrations.

**Rules**

- **Idempotent** — they run on *every* startup, so check before inserting (seed only if missing).
- **Ordered** — lower `Order` runs first; use gaps (10, 20, 30) so you can insert between later.
- **Owned by the module** whose data they seed (the platform seeds platform data; Tasks seeds Tasks).

### Example

```csharp
// backend/src/HomeOs.Platform/Seeding/RolesSeeder.cs
using HomeOs.Platform.Persistence;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;

public sealed class RolesSeeder(PlatformDbContext db) : IDataSeeder
{
    public int Order => 10;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Roles.AnyAsync(ct)) return;          // idempotent guard

        db.Roles.AddRange(
            new Role("Owner"), new Role("Admin"), new Role("Adult"),
            new Role("Child"), new Role("Guest"));

        await db.SaveChangesAsync(ct);
    }
}
```

### Register it

In the module's service registration (e.g. `AddHomeOsPlatform`, or a module's `AddXModule`):

```csharp
services.AddDataSeeder<RolesSeeder>();
```

That's it — it runs on the next `dotnet run`.

### Environment-specific seed data

- **Always** seed reference data needed to function (roles, permissions, default categories).
- **Dev/demo only** data (a sample household, fake tasks) should be guarded by environment:
  inject `IHostEnvironment` and `return` early when not `Development`, or gate with a
  `Database:SeedDemoData` flag. Never ship demo data to production.

## Status

- ✅ Auto-create DB + auto-migrate on startup (verified: app creates `homeos` and records
  `__EFMigrationsHistory`).
- ✅ Seeding pipeline wired (`IDataSeeder` + `AddDataSeeder<T>()`); **0 seeders today** — the first
  real ones (roles/permissions, demo household) arrive in **M1** alongside the first entities.
