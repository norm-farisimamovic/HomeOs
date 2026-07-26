---
name: dotnet-backend
description: Use when writing, structuring, reviewing, or debugging any .NET / C# backend code for Home OS — ASP.NET Core Minimal APIs, EF Core + MySQL data access, vertical-slice features, the Platform kernel (event bus, app registry, entity links, permissions, members, sharing/visibility), SignalR real-time, notifications & email, and tests. Encodes the senior .NET conventions and the modular-monolith architecture this project is built on. Trigger on any *.cs / *.csproj / .sln work, EF migrations, API endpoints, or backend design questions.
---

# Home OS — .NET Backend (senior engineering standard)

You are a **senior .NET platform engineer**. Home OS is not a CRUD app — it is a **platform** on which "apps" (Tasks, Finance, Calendar, …) plug in as first-class modules. Every backend decision is judged against one question: *would a brand-new app, written by someone else, plug into this cleanly without touching existing code?* If not, the design is wrong.

Read `references/architecture.md` before designing anything that spans modules or touches the kernel. Read `references/conventions.md` before writing code, migrations, or tests. Load them; don't guess.

## Stack (pinned)

- **.NET 8 (LTS) / C# 12** — nullable enabled, implicit usings, file-scoped namespaces, primary constructors, collection expressions, `records` for DTOs/events. *(Targeting .NET 8 LTS for best free-hosting support + it's the installed SDK; upgradeable to a newer LTS later.)*
- **ASP.NET Core Minimal APIs** grouped per feature slice (no fat controllers).
- **EF Core 8 + Pomelo MySQL provider** (`Pomelo.EntityFrameworkCore.MySql`). `utf8mb4`, explicit key/index lengths.
- **SignalR** for real-time fan-out, driven by the platform event bus.
- **FluentValidation** (validation), **Mapperly** (source-gen mapping), **Serilog** (structured logs).
- **Tests:** xUnit v3 + Shouldly + NSubstitute + **Testcontainers (MySQL)** for integration.
- ⚠️ **Licensing landmines — do not add:** MediatR, AutoMapper, Moq, FluentAssertions v8+ all moved to commercial/controversial licenses. Use the alternatives above. See `references/conventions.md`.

## Fast, simple, modern, optimized (applies to everything)

These are direct requirements from the spec (*"low friction is what makes it actually get used"*), not polish:

- **Simple first (KISS / YAGNI).** The *platform kernel* is the only place that earns real abstraction. Individual slices stay direct — a command, a handler, a query. No layer, interface, or pattern without a concrete need *today*. Delete code before adding it.
- **Fast by default.** Every list endpoint is paginated; every read is `AsNoTracking` + projected to a DTO (never over-fetch, never N+1). Hot reads use `OutputCache` / `IMemoryCache` (or `HybridCache` via `Microsoft.Extensions.Caching.Hybrid`), invalidated by the matching domain event. Keep event handlers **off the request path** (dispatch after `SaveChanges` / via outbox) so writes return immediately.
- **Modern idioms.** Minimal APIs, records, primary constructors, collection expressions, source generators (Mapperly), typed `Results`, output caching, a sequential/v7 GUID helper, `System.Text.Json` source-gen. Write modern .NET, not 2016 .NET.
- **Optimized data layer.** Index every column you filter/sort/join on (`HouseholdId`, due dates, assignee/owner, `EntityLink` keys). `AddDbContextPool`, compiled queries on hot paths. Measure before micro-optimizing — correctness and simplicity win ties.

Full detail in `references/conventions.md` → *Performance & optimization*.

## Architecture at a glance

```
src/
  HomeOs.Platform/          # THE KERNEL — shared by everything, depends on nothing above it
      Events/               #   IEventBus, IDomainEvent, IEventHandler<T>
      Registry/             #   IAppModule manifest + extension contributions
      Entities/             #   IHomeObject, EntityLink (the "connected web")
      Access/               #   permissions/capabilities + sharing/visibility filter
      Members/              #   households & members (identity layer)
      Reminders/ Notifications/ Search/ Automations/   # shared capabilities
  HomeOs.Modules.Tasks/     # a Home OS "app" — vertical slices, owns its entities
  HomeOs.Modules.Finance/
  HomeOs.Modules.Calendar/  # a VIEW over Tasks + own Events — stores NO duplicate task data
  HomeOs.Modules.Kanban/    # also a VIEW over Tasks
  HomeOs.Api/               # thin host: wires modules, auth, SignalR, middleware
tests/
  HomeOs.<X>.Tests/
```

## The golden rules (non-negotiable)

1. **No module references another module.** `HomeOs.Modules.Finance` must never `using HomeOs.Modules.Tasks`. Modules communicate only through the **Platform kernel**: domain events, the entity-link service, and platform capabilities. A direct cross-module reference is a review-blocking defect.
2. **Everything connects through the kernel.** A bill creating a task = Finance *publishes* `BillDueSoon`; a Tasks handler *subscribes* and creates the task. Neither knows the other exists.
3. **Views don't duplicate stores.** Calendar and Kanban query the Tasks module's data through platform contracts / read models. They never own a `tasks` table. Duplicating storage is the #1 way to fail this assignment.
4. **Access is enforced at the platform boundary, always.** Every query is filtered by household + member visibility (`private` / `household` / `specific members`) via the shared visibility filter. Never hand-roll a WHERE clause that could leak another household's or member's data. Extensibility must never become a way around privacy.
5. **What a module exposes is a contract.** Public events, DTOs, and capability interfaces are promises other apps depend on. Change them additively; never break them silently. Version when you must.
6. **Fail gracefully on missing dependencies.** An app must behave sensibly if a capability it hoped for isn't installed — feature-detect, don't hard-crash.
7. **Async all the way**, `CancellationToken` on every I/O path, no `.Result` / `.Wait()`.

## Security & authorization (non-negotiable)

Security is **layered and enforced on the server** — the client only reflects it. Every request clears these gates in order, and the *most restrictive wins*:

1. **Authentication** — who is this? ASP.NET Core Identity user store; **cookie auth** (httpOnly, `SameSite`, `Secure`) for the SPA (no tokens in JS = XSS-safe) or JWT bearer for mobile/API. Password hashing via Identity (PBKDF2/Argon2); account lockout; optional 2FA. Identity/`HouseholdId`/`MemberId` come from the auth ticket, **never the request body**.
2. **Household membership** — is the caller in the household that owns the resource? The global `HouseholdId` query filter is a hard tenancy wall — data never crosses households.
3. **Role (RBAC)** — member roles: **Owner** (full control incl. members/settings), **Admin** (manage content & most settings), **Adult/Member** (normal use), **Child/Teen** (limited/curated), **Guest** (scoped/read-only). Check a **permission/policy** (`RequirePermission("members.manage")`), *never a raw role string* — the role→permission map lives in one place.
4. **App capability** — is this app even allowed this operation? (granted, reviewable, revocable — architecture.md §2.5).
5. **Resource + visibility** — may *this* member see/edit *this* item? Resource-based authorization (`IAuthorizationService`) for edit/delete + the visibility filter for reads.

**Baseline hardening (always on):** HTTPS + HSTS; CORS locked to known origins; anti-forgery (CSRF) for cookie auth; the built-in **rate limiter** on auth + write endpoints; security headers (CSP, `X-Content-Type-Options`, `Referrer-Policy`, frame-ancestors); **EF parameterized queries only** (never concatenated SQL); validate every input; secrets out of source (user-secrets/env/vault); audit-log sensitive actions; keep dependencies patched. Full checklist in `references/conventions.md` → *Security* and *Authorization, roles & permissions*.

## Errors, localization & comments

- **Handle every error; surface it clearly.** A global `IExceptionHandler` maps everything to RFC-9457 `ProblemDetails` with a stable machine `code`, a **localized** user-safe message, field-level validation errors, and a `traceId`. Log full detail server-side (Serilog); **never** leak stack traces / SQL / secrets to the client. Separate expected domain failures (typed results → 4xx) from unexpected (500).
- **Multilingual by default (i18n).** No user-facing string is hard-coded. Server messages — errors, notifications, and **emails** — go through the kernel's `IAppText` (in-code `bs`/`en` string table; see conventions → Localization). Request-localization middleware resolves culture from the frontend's `Accept-Language`; **emails render in each recipient's own `PreferredCulture`** via the explicit-culture overload, not the sender's. Identity errors are localized via `LocalizedIdentityErrorDescriber`.
- **Comment everything meaningfully.** `///` XML doc comments on every public type/member — they *are* the contract other apps depend on and they generate the OpenAPI docs. Inline comments explain **why** (business rule, trade-off, workaround), never restate the code; each non-obvious slice gets a one-line intent header.

## A vertical slice (the unit of backend work)

One folder per feature; everything the feature needs lives together:

```
Modules.Tasks/Features/CreateTask/
    CreateTask.cs         # request record + validator + handler + endpoint (or split if large)
```

```csharp
// The command + result (records)
public record CreateTaskCommand(string Title, DateOnly? DueDate, Guid? AssigneeId, Priority Priority);
public record TaskDto(Guid Id, string Title, DateOnly? DueDate, Guid? AssigneeId, bool IsDone);

// Endpoint — Minimal API, mapped via the module's IEndpointRegistrar
public static class CreateTaskEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/tasks", async (
                CreateTaskCommand cmd, CreateTaskHandler handler, CancellationToken ct) =>
            {
                var dto = await handler.Handle(cmd, ct);
                return Results.Created($"/api/tasks/{dto.Id}", dto);
            })
            .RequireAuthorization()
            .WithTags("Tasks");
}

// Handler — the slice's logic. Publishes a domain event; never calls another module.
public sealed class CreateTaskHandler(
    TasksDbContext db, ICurrentMember member, IEventBus bus, IValidator<CreateTaskCommand> validator)
{
    public async Task<TaskDto> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(cmd, ct);

        var task = TaskItem.Create(cmd.Title, cmd.DueDate, cmd.AssigneeId, cmd.Priority,
                                   householdId: member.HouseholdId, ownerId: member.Id);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new TaskCreated(task.Id, task.AssigneeId, task.DueDate), ct);
        return task.ToDto();
    }
}
```

The event is what makes the system "connected": Calendar surfaces it if it has a due date, Notifications emails the assignee, Automations may fire user rules — **all without CreateTaskHandler knowing any of them exist.**

## How a new app plugs in (the acceptance test)

A new module is just a class library that:
1. Implements `IAppModule` — declares its id, entity types, needed capabilities, and extension contributions (nav, dashboard widgets, search provider, automation triggers/actions).
2. Registers its slices + `DbContext` via one `AddXModule(this IServiceCollection)` call the host discovers.
3. Reuses platform capabilities (reminders, notifications, links, members) instead of rebuilding them.
4. Links its objects into the connected web via `IEntityLinkService`.

**Definition of done for any module:** you could delete it and nothing else breaks; you could add a *new* app (e.g. a meal-planner that reuses Tasks) touching zero existing files. If either fails, keep working. See the full walkthrough in `references/architecture.md`.

## Quality bar / definition of done

- Compiles with **zero warnings** (`TreatWarningsAsErrors`), nullable clean.
- Every write publishes the appropriate domain event(s).
- Every query goes through the visibility filter — verified by an integration test that a second household/member cannot read the data.
- **Authorization enforced** (authN + role/policy + app capability + resource/visibility); a negative test proves an unauthorized role/member/household is denied.
- Validation on all inputs; failures returned as **localized** RFC-9457 `ProblemDetails` with a stable `code` + `traceId`; no sensitive leakage.
- **No hard-coded user-facing strings** — new messages (errors, notifications, emails) added to resource files for every supported culture.
- **Public types/members have `///` XML doc comments**; non-obvious logic commented (the *why*).
- Slice covered by at least a handler unit test + an endpoint integration test (Testcontainers MySQL).
- No cross-module `using`. No new commercial-licensed dependency.
