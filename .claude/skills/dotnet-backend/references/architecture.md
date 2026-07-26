# Home OS Backend — Platform Architecture (reference)

The kernel exists so that **apps cooperate without knowing about each other**. Read this before touching `HomeOs.Platform` or designing anything cross-module.

## 1. Dependency direction

```
HomeOs.Api  ──►  HomeOs.Modules.*  ──►  HomeOs.Platform
                       (apps)              (kernel)
```

- `HomeOs.Platform` depends on **nothing** in the solution above it.
- Every module depends on `HomeOs.Platform` and on nothing else in `Modules.*`.
- `HomeOs.Api` is the only project that references all modules — purely so their DLLs ship; it does **not** name them in code.

Enforce this. A quick guard: an architecture test (NetArchTest / a unit test scanning assembly references) that fails the build if any `Modules.X` assembly references `Modules.Y`.

**Adding a module touches nothing else.** Each module ships an `IHostModule` (`HomeOs.Platform.Startup`) with `Add(services, config)` + `Map(endpoints)`. `ModuleLoader.AddHomeOsModules`/`MapHomeOsModules` scan every `HomeOs.Modules.*.dll` in the app directory, discover the `IHostModule`s, and wire them — so `Program.cs` calls those two methods once and names no module. To add an app: create the module project, implement `IHostModule` (+ its `IAppModule` manifest, and any `ISearchProvider`/`ICalendarSource`/`IUpcomingProvider`/event handlers), and add a `ProjectReference` in the API `.csproj` so the DLL is present. Nothing in the host or any existing module changes. *(This is a modular monolith — one process, module isolation by assembly + kernel contracts — not microservices; there's no per-module network boundary or independent deploy.)*

## 2. The kernel components

### 2.1 Event bus (the backbone of "everything connects")
In-process pub/sub. Publishers never know subscribers.

```csharp
public interface IDomainEvent { }                       // marker
public interface IEventBus { Task PublishAsync(IDomainEvent e, CancellationToken ct); }
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent e, CancellationToken ct);
}
```

- Handlers are registered by DI scanning; the bus resolves all `IEventHandler<T>` for a published `T`.
- Handlers must be **idempotent** and **isolated** — one failing handler must not roll back the publisher's transaction. Dispatch handlers *after* `SaveChanges` (outbox pattern for reliability once you need it).
- Events fan out to three consumers: other modules' handlers, the **SignalR** hub (live UI), and the **notifications** pipeline.
- Events are **public contracts** — put shared event records in the *publishing* module's `Contracts/` folder (or a tiny `HomeOs.Contracts` package) so consumers reference the contract, not the module internals.

### 2.2 App registry & manifest (apps as first-class citizens)
Every module — built-in or new — registers the **same way**.

```csharp
public interface IAppModule
{
    AppId Id { get; }                       // e.g. "tasks"
    string DisplayName { get; }
    IReadOnlyList<Capability> RequiredCapabilities { get; }   // what it needs granted
    void RegisterServices(IServiceCollection services);       // DbContext, handlers, validators
    void RegisterEndpoints(IEndpointRouteBuilder app);        // its slices
    void Contribute(IExtensionRegistry ext);                  // nav, widgets, search, automations
}
```

`IExtensionRegistry` is how an app appears "everywhere the built-ins do":
- `ext.AddNavItem(...)` — navigation entry.
- `ext.AddDashboardWidget(...)` — a Today-screen contribution.
- `ext.AddSearchProvider<T>()` — participates in global search.
- `ext.AddCommand(...)` — command-palette / quick-capture actions.
- `ext.AddAutomationTrigger(...)` / `AddAutomationAction(...)` — "when this, then that".

Built-in apps use these APIs too — **no special-casing**. That is the whole point.

### 2.3 The connected web — entities & links
Objects across apps link to each other generically.

```csharp
public interface IHomeObject                 // every linkable domain object implements this
{
    Guid Id { get; }
    string ObjectType { get; }               // "task", "bill", "note", "event", "document"
    Guid HouseholdId { get; }
}

public sealed class EntityLink               // the generic relationship table
{
    public Guid Id { get; init; }
    public string SourceType { get; init; }  public Guid SourceId { get; init; }
    public string TargetType { get; init; }  public Guid TargetId { get; init; }
    public string Relationship { get; init; } // "created-from", "about", "attached-to"
    public Guid HouseholdId { get; init; }
}

public interface IEntityLinkService
{
    Task LinkAsync(IHomeObject source, IHomeObject target, string relationship, CancellationToken ct);
    Task<IReadOnlyList<EntityRef>> GetLinksAsync(IHomeObject obj, CancellationToken ct);
}
```

A Note linking to a Bill, a Task created from a renewal date — all live in the same `EntityLink` table. Apps resolve `EntityRef` (type+id) to display data via each type's registered resolver, so no module imports another module's entity classes.

### 2.4 Members & households (identity layer)
Provided once. `Household` has many `Member`. `ICurrentMember` is a scoped service resolving the authenticated caller's `Id` + `HouseholdId`. Every entity carries `HouseholdId` (+ `OwnerId` where relevant).

### 2.5 Access: authentication → roles → capabilities → visibility
Authorization is layered. A request must clear every applicable gate, and the **most restrictive wins**.

1. **Authentication** — identity + `HouseholdId`/`MemberId` come from the auth ticket, never the request body. ASP.NET Core Identity user store; cookie auth for the SPA or JWT for API/mobile.
2. **Roles (RBAC)** — each member has a household role: `Owner`, `Admin`, `Adult`, `Child`, `Guest`. Roles are expressed as **permissions behind policies**; handlers/endpoints require a *permission* (`RequirePermission("members.manage")`), never a raw role string, so the role→permission mapping evolves in one place. Store the role on the member; register policies at startup.
3. **Capabilities** — what an *app* may do (`read:tasks`, `write:tasks`), declared in the manifest, granted by the household, enforced at the platform boundary by `AppAccessMiddleware`, reviewable/revocable. Extensibility never widens access silently. **Deliberately coarse (per-app read/write), by design** — capabilities answer "may this app touch this app's data at all"; *which specific rows* a member sees is a **separate concern** owned by the visibility layer below. The two compose (both must pass); adding per-resource *capability* scopes would duplicate visibility, so it's intentionally not done (YAGNI).
4. **Visibility / ownership** — **role-based** (the household's rule):
   - **Owner/Admin** (managers) see **everything in the household except another member's `Private` items** (their own private is always theirs).
   - **Everyone else** (Adult/Child/Guest) sees **only their own items** (owner or assignee) **plus anything marked `Household`** (whole-home).
   - `ICurrentMember.IsManager` (from role claims) drives the branch; the reference implementation is `TasksEndpoints.VisibleTo` / `Editable`. Every module applies the same shape. Cover with tests: a member can't see another's private/own items; a manager can't see another's `Private`.

   Item visibility levels: `Private` (owner-only), `Household` (whole home), `Shared` (specific members — share-list TBD). Resource-based authorization for edit/delete, and a single read filter for queries:

```csharp
public interface IVisibilityFilter
{
    IQueryable<T> Visible<T>(IQueryable<T> query) where T : IHomeObject;  // applies household + member rules
}

// edit/delete go through resource-based authorization, not just the read filter:
await authz.AuthorizeAsync(user, task, "CanEditItem");   // owner / role / shared-with check
```

Enforce with: EF Core **global query filters** for the household boundary (never leak across households), **policy-based** authorization for roles/permissions, **resource-based** authorization for per-item edit rights, and the `IVisibilityFilter` for per-item read visibility. Cover each with a negative integration test — wrong household, wrong role, wrong owner.

### 2.6 Shared capabilities (built once, reused by all)
- **`IReminderService`** (`HomeOs.Platform.Reminders`) — **implemented (M5).** Any app schedules a reminder for one of its objects via `ScheduleAsync(ScheduledReminder)`; the Reminders module implements it (`ReminderService`) and makes it **idempotent per source** (`SourceKey`+`SourceId`) so re-saving updates instead of duplicating, and `RemoveAsync` cleans up. Life admin uses this to turn a record's expiry into an auto-reminder (7 days before) with zero coupling — the canonical "renewal → reminder" automation. Same shape as `ICalendarSource`: depend on the Platform interface, never the other module.
- **`INotificationService`** (`HomeOs.Platform.Notifications`) — **implemented (M6).** `NotifyAsync(householdId, memberId, category, title, body?, link?, alsoEmail)` writes an in-app `Notification`, pushes it live over **SignalR** (`NotificationsHub` at `/hubs/notifications`, keyed by member id via the default `IUserIdProvider`), and — if `alsoEmail` and the member's `NotificationPreference` for that category allows — sends a localized email (skips `@homeos.local` demo addresses). Apps call this instead of `IEmailSender` directly (see `TaskAssignedEmailHandler`). Reminders' `ReminderDispatcher` (a `BackgroundService`) fires due reminders through it.
- Email pipeline: event → notification rule → per-member category preference → send (in-app + email) → optional daily/weekly digest (scheduled job).
- **`ISearchProvider`** (`HomeOs.Platform.Search`) — **implemented.** Each app registers a scoped provider returning `SearchHit`s for the current member (respecting visibility); `/api/search` injects `IEnumerable<ISearchProvider>` and merges. A new app appears in global search the instant it registers one — same aggregation-via-contract shape as `ICalendarSource`.
- **`ICalendarSource`** (`HomeOs.Platform.Calendar`) — **implemented (M4).** Any module with dated items exposes them as `CalendarItem`s by registering a scoped `ICalendarSource` (e.g. `services.AddScoped<ICalendarSource, TasksCalendarSource>()`); the source resolves `ICurrentMember` itself and returns only what that member may see. The Calendar app injects `IEnumerable<ICalendarSource>` and merges — **the canonical example of aggregation-via-contract with zero cross-module references.** Copy this shape for any new "contribute to a shared surface" need (search, dashboard widgets, automations).
- **Automations engine** (`HomeOs.Modules.Automations`) — **implemented (M6).** Built on a generic kernel event **`AppActivity`** (`HouseholdId`, `ActorMemberId`, `Kind` like `task.completed`, `Title`, `Link`) that apps publish alongside their typed events. The `AutomationRunner` is a single `IEventHandler<AppActivity>` that matches a household's enabled rules by `Kind` and runs the action (currently `notify` via `INotificationService`). Add a new trigger by publishing `AppActivity` with a new `Kind`; add a new action inside the runner — no new coupling. This is the reference pattern for "user rules over the event bus".
- **`IAuditLog`** (`HomeOs.Platform.Audit`) — **implemented.** `RecordAsync(action, detail)` writes an `AuditEntry` (household, actor, action, detail, UTC) attributed to the current member. Three feeds: (1) an EF **`AuditInterceptor`** on every module DbContext (added via `.AddAuditing(sp)`) captures **every create/update/delete** automatically (`taskitem.created`, `note.deleted`, … with a readable label); (2) `AuditActivityHandler : IEventHandler<AppActivity>` records the "notable moments" apps announce; (3) explicit `RecordAsync` for platform-side admin actions (member invite/role/edit/remove, household rename, app enable/disable). The interceptor only fires for authenticated requests (background jobs have no actor) and is **not** added to `PlatformDbContext` (it writes there — avoids re-entrancy). `GET /api/audit` is **Owner/Admin-only**.
- **`IAssistant`** (`HomeOs.Platform.Assistant`) — **implemented.** An LLM tool-use loop over **kernel contracts only** (`IReminderService`, `IUpcomingProvider`), so it acts as the current member with the same auth/visibility, and a new app that registers an `IUpcomingProvider` is answerable with no change. **Provider-agnostic**: OpenAI-compatible (Groq/Gemini/OpenRouter/Ollama — free tiers) or Anthropic, via `Assistant:Provider`/`ApiKey`/`BaseUrl`/`Model`. Disabled (graceful) until a key is set. The frontend calls `/api/assistant/chat` (same-origin); the LLM key stays server-side.
- **App registry + household control** (`HomeOs.Platform.Apps`) — **implemented.** The platform's answer to "new apps are first-class citizens" + "the household stays in control". Each module provides an `IAppModule` with an `AppManifest` (id, name/description keys, icon, hue, route, `ApiPrefix`, declared capabilities); `IAppRegistry` aggregates them with the core surfaces. Per-household state (`HouseholdApp`: enabled + granted capabilities) is read/written through `IAppAccess` (defaults keep a new app working — enabled, all capabilities — until the household narrows it). Enforcement is a single kernel middleware (`AppAccessMiddleware`, after auth): for any `/api/{app}` request it 403s if the app is disabled or the verb's capability (`read:` for safe methods, `write:` otherwise) isn't granted — **zero per-module edits**. Shared surfaces filter by enablement too (search + calendar drop hits/items whose `Source` app is off). `GET /api/apps` + Owner/Admin `PUT /api/apps/{id}/enabled|capabilities` drive the frontend `/apps` control panel; the nav hides disabled apps. Adding an app = add its `IAppModule`; it appears on nav, the Apps page, and in enforcement with no other change. **Verified via curl**: disabling Finance 403s its API and removes it from search + calendar; revoking `write:finance` makes it read-only; core apps can't be disabled; every change is audited.
- **`IEntityLinks`** (`HomeOs.Platform.Links`) — **implemented.** The object-to-object "connected web": link any app object to any other (`LinkAsync(fromType, fromId, toType, toId, toTitle, toLink)`). The target is stored **denormalized** (type/id + a title + deep-link snapshot) so an app shows its links without referencing — or even having installed — the other module. Targets are chosen through global search on the frontend (`LinkedItems` component); `/api/links` is the API. Notes use it (note↔task/bill/event); any app can.
- **`IShareNotifier`** (`HomeOs.Platform.Notifications`) — **implemented.** `NotifySharedAsync(householdId, actor, sharedWith, title, link)` pings each newly-shared member (in-app + email, category `shared`, in their own language, never the actor). Calendar + Notes call it after saving; on update they diff old vs new `SharedWith` so only *newly* added members are notified.
- **`IUpcomingProvider` + digest** (`HomeOs.Platform.Digest`) — **implemented.** The member-explicit sibling of `ICalendarSource`: a digest is built by a **background job** (no current request), so `GetUpcomingAsync(householdId, memberId, from, to)` takes the member rather than resolving it. Apps (Tasks/Finance/Reminders) register one; `DigestService` merges them into a localized "what's coming up" email and `DigestDispatcher` (hourly) sends it on each member's opt-in cadence (`Member.DigestFrequency` Off/Daily/Weekly). `POST /api/digest/preview` sends on demand. Same aggregation-via-contract shape — a new app's upcoming items join everyone's digest with one registration. **Note:** where a shared surface runs outside a request (dispatchers, digests), prefer a member-explicit contract over one that reads `ICurrentMember`.
- **`LeadSchedule`** (`HomeOs.Platform.Scheduling`) — **implemented.** Pure helper that turns one due date into an escalating ladder of once-each alerts ("in 7 / 3 / 1 days, then today"). `StageToFire(daysUntil, ladder, lastNotifiedStage)` returns the stage to fire now, or null. The bill + reminder dispatchers store the last stage on the row and localize each alert to the recipient's culture. Unit-tested in `LeadScheduleTests`. Recurrence (`Cadence`/`Recurrence`/`TaskRecurrence` enums per module) rolls bills/reminders/tasks to their next occurrence and resets the ladder.
- **Currency registry** (`HomeOs.Platform.Money.Currencies`) — **implemented.** Static, deterministic (no external FX call → CSP-safe): `CurrencyInfo(Code, Symbol, Name, RateToBase)` with base `BAM` (shown "KM", EUR peg 1.95583). `Normalize` (legacy "KM"→BAM, empty/unknown→base), `Get`, `Convert(amount, from, to)` (2-dp). Each member has a `PreferredCurrency`; Finance converts **all** amounts to it on read via `ICurrentMember.PreferredCurrency`, and `GET /api/currencies` feeds the picker. A future "live rates" app can refresh the table behind this same API.

## 3. Data & EF Core layout

- **One `DbContext` per module** (`TasksDbContext`, `FinanceDbContext`), each mapping only its own entities via `IEntityTypeConfiguration<T>`. Kernel tables (members, links, notifications) live in a `PlatformDbContext`.
- All contexts point at the same MySQL database; migrations per context (`--context TasksDbContext`). Use **PascalCase table names** (`Households`, `Tasks`, `Bills`) — consistent with Identity's `AspNet*` tables. Prefix only where a module genuinely needs disambiguation.
- No cross-context navigation properties — cross-module relationships go through `EntityLink`, not FK. This is what keeps modules independent.

## 4. Worked example — Calendar & Kanban as views (not stores)

Calendar must show "tasks with due dates" automatically without owning them:

- Tasks module exposes a **read contract**: either a public read model (`ITaskReadApi.GetDueBetween(...)`) or, better, Tasks publishes `TaskCreated` / `TaskDueDateChanged` / `TaskCompleted` events and Calendar maintains a lightweight projection of *just* what it needs to render (id, title, date, ownerId).
- Calendar owns its own `CalendarEvent` entity for genuine events, but **never** a copy of tasks.
- Kanban likewise projects task status into columns and maps a drag → a `ChangeTaskStatus` command exposed by Tasks. Kanban stores column layout, not tasks.

If you find yourself copying task fields into a `kanban_cards` table, stop — you've broken rule #3.

## 5. The extensibility acceptance test — a meal-planner

The spec's own example. A *new* app must plug in touching zero existing files:

1. `HomeOs.Modules.MealPlanner` implements `IAppModule`, declares capabilities `read:tasks`, `create:task`, `create:reminder`.
2. It **reuses Tasks** for its shopping/prep to-dos (via the platform's create-task capability / command) instead of inventing a task system.
3. It links a `MealPlan` to the tasks it created via `IEntityLinkService`.
4. It fires reminders through `IReminderService`; those emails flow through the existing pipeline automatically.
5. It contributes a nav item, a dashboard widget ("meals this week"), a search provider, and an automation trigger (`MealPlanned`) via `IExtensionRegistry`.
6. Host discovers it by the same module-scan the built-ins use.

When this works end-to-end without editing Tasks/Finance/etc., the platform requirement is satisfied. Build toward this test from day one.

## 6. Module authoring checklist (reference: `HomeOs.Modules.Tasks`)

The first real module is `HomeOs.Modules.Tasks` — copy its shape for every new app:

1. **Project** `HomeOs.Modules.X` → references `HomeOs.Platform` only (+ EF `Design` for its own migrations). Never another module.
2. **Domain** entities implement `IHomeObject` (`Id`, `ObjectType`, `HouseholdId`); rich behaviour (factory `Create`, methods like `Complete`), private setters.
3. **`XDbContext`** owns only its tables (PascalCase); enums → `.HasConversion<string>()`; collections → JSON converter + `ValueComparer`; `Ignore` computed props. Add a `IDesignTimeDbContextFactory<XDbContext>`.
4. **Contracts/** public domain events (`: IDomainEvent`), changed only additively.
5. **Features/** endpoints (Minimal API group, `RequireAuthorization()`), scoped by `ICurrentMember.HouseholdId` + visibility; **publish events after `SaveChanges`**; return DTOs (dates as ISO strings; enrich names via `IMemberDirectory`).
6. **`XModule`**: `AddXModule(IServiceCollection, IConfiguration)` registers the DbContext (`AddDbContextPool`), `new MigratableContext(typeof(XDbContext))` (startup auto-migrates it), `AddEventHandlers(assembly)`, and any `AddDataSeeder<>`; `MapXModule(IEndpointRouteBuilder)` maps endpoints. The host calls both — that's the only place the module is composed.
7. **Kernel services** an app uses: `ICurrentMember` (identity/household — never trust the body), `IMemberDirectory` (member names/list), `IHouseholdLookup`, `IEventBus`. Don't touch `PlatformDbContext` or the user table directly.
8. **Seeding** is per-module + dev-gated (`IHostEnvironment.IsDevelopment()`), idempotent; demo data can build on the platform's `Demo Home` household.
9. **Tests**: domain unit tests (no infra) + a live/integration **negative-access** check (another household sees nothing, 404 on cross-household mutate, 401 anonymous).
