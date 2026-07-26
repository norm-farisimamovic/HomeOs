# Home OS Backend — Code Conventions (reference)

Senior .NET / C# 12 conventions for this repo (targeting **.NET 8 LTS**). These are defaults; deviate only with a reason stated in the PR.

## Language & style

- **Nullable reference types on** everywhere. No `!` null-forgiving except at proven-safe boundaries with a comment.
- File-scoped namespaces, implicit usings, one top-level type per file.
- `record` for DTOs, commands, queries, and domain events (immutable). `sealed class` for handlers, services, entities.
- **Primary constructors** for DI: `public sealed class Handler(TasksDbContext db, IEventBus bus)`.
- Prefer `DateOnly` / `TimeOnly` / `DateTimeOffset` (UTC) — never naked `DateTime` for stored timestamps. Store UTC; convert at the edge.
- `Guid` for entity ids via a sequential/v7 GUID helper — time-ordered, index-friendly on MySQL. (.NET 8 has no built-in `Guid.CreateVersion7()`; add a small helper or use a package.)
- Entities are **rich, not anemic**: business rules live in factory methods (`TaskItem.Create(...)`) and behavior methods (`task.Complete()`), not in handlers or setters. Private setters; no public parameterless ctors except EF's.
- `async`/`await` end-to-end, `CancellationToken ct` as the last parameter on every I/O method, flowed through. Never `.Result`, `.Wait()`, `async void` (except event handlers explicitly).

## Vertical-slice mechanics (no MediatR)

MediatR is now commercially licensed — we do **not** use it. A slice is just an endpoint + a handler class resolved from DI. Keep it that simple.

- Handlers are plain classes registered `AddScoped`. Endpoints call them directly. No pipeline indirection unless a cross-cutting need (e.g. transaction, logging) justifies a thin decorator.
- Group endpoints with `MapGroup("/api/tasks").RequireAuthorization().WithTags("Tasks")`.
- Return `Results.Ok/Created/NoContent/ValidationProblem`; never return entities — map to DTOs.

## Validation & mapping

- **FluentValidation** (`IValidator<T>`), one validator per command. Call `ValidateAndThrowAsync` in the handler or a validation filter on the endpoint group.
- **Mapperly** (`[Mapper]` source generator) for entity↔DTO. Do **not** add AutoMapper (commercial). Hand-written mapping is also fine for small DTOs.

## EF Core + MySQL (Pomelo)

- Provider: `builder.UseMySql(conn, ServerVersion.AutoDetect(conn))`. Charset `utf8mb4`, collation `utf8mb4_unicode_ci`.
- **MySQL requires a length on indexed/keyed strings** — never index an unbounded `string`. Set `HasMaxLength(...)` on every indexed column (255 for keys). Configure in `IEntityTypeConfiguration<T>`.
- **Table naming: PascalCase** (`Households`, `Tasks`, `Bills`) via `ToTable("Pascal")`, consistent with Identity's `AspNet*` tables. Not snake_case, not prefixed (unless a module needs disambiguation).
- One `DbContext` per module (see architecture.md). Migrations: `dotnet ef migrations add X --context TasksDbContext -o Migrations`. Never edit applied migrations. Provide an `IDesignTimeDbContextFactory<T>` per context so the EF tools don't boot the host.
- **Never pass `--no-build` to `dotnet ef migrations add`.** It diffs against the *compiled* model, so a stale assembly silently produces an **empty or partial migration** (e.g. a new `DbSet<>`'s `CreateTable` missing) that then records as "applied" with the table never created — a 500 at runtime (`Table 'X' doesn't exist`). Let `ef` build, or `dotnet build` first. **Always open the generated `*.cs` and confirm its `Up()` contains the expected `CreateTable`/`AddColumn`** before moving on. If one slipped through, add a corrective migration *with a fresh build* (its `Up()` will contain what was missed).
- **Startup bootstrap:** the host auto-creates the DB + applies migrations, then runs `IDataSeeder`s — config-gated by `Database:AutoMigrate` / `Database:Seed`. Seeders are **idempotent**, ordered, module-owned, and registered with `AddDataSeeder<T>()`. Reference data always; demo data gated to Development. Full guide: `docs/SEEDING.md`.
- **Global query filter** for `HouseholdId` on every `IHomeObject` — the hard tenancy boundary. Layer the member `IVisibilityFilter` on top for per-item sharing.
- Use `AsNoTracking()` for reads, projection (`Select` to DTO) to avoid over-fetching. `AsSplitQuery()` for multi-collection includes.
- No lazy loading. No `EnableSensitiveDataLogging` outside Development.
- Concurrency: a `rowversion`/`Timestamp` or `xmin`-style token on mutable aggregates where two members may edit.

## Errors, results, logging (handle everything, display clearly)

- **Single funnel:** a global `IExceptionHandler` (+ `AddProblemDetails`) turns *every* unhandled failure into RFC-9457 `ProblemDetails`. No endpoint returns a bare 500 or a raw exception.
- **Shape of an error response:** stable machine `code` (e.g. `task.not_found`), a **localized** human `title`/`detail`, `status`, a `traceId` (correlate to logs), and field-level `errors` for validation (`ValidationProblemDetails`). Clients switch on `code`, show `detail` to users, and quote `traceId` in support.
- **Expected vs. exceptional:** model expected domain failures as a typed `Result`/`ErrorOr` outcome → mapped to the right 4xx (`404/409/422`). Reserve exceptions for the genuinely unexpected → 500. Don't use exceptions for control flow in hot paths.
- **Never leak** stack traces, SQL, connection strings, or PII to the client. Full detail is logged server-side only; `detail` shown to users is safe and localized.
- **Serilog** structured logging: named properties (`log.Information("Task {TaskId} created for {Member}", id, memberId)`), never interpolated messages; enrich with `traceId`/request id; no secrets/PII; log at the right level (expected 4xx = Information/Warning, unexpected = Error).

## Authorization, roles & permissions

The authZ pipeline is defined in `architecture.md` §2.5 (authentication → roles → capabilities → visibility). Implementation rules:

- **Roles → permissions → policies.** Map each role (`Owner/Admin/Adult/Child/Guest`) to a permission set; register a policy per permission at startup. Endpoints/handlers require the **permission**, never a role literal:
  ```csharp
  app.MapDelete("/api/members/{id}", ...).RequireAuthorization("members.manage");
  ```
- **Resource-based authorization** for "can this member act on *this* item": `await authz.AuthorizeAsync(user, item, "CanEditItem")` with an `AuthorizationHandler<CanEditItem, IHomeObject>` that checks ownership / role / shared-with. Reads still go through `IVisibilityFilter`.
- **Capabilities** (app-level) are checked at the platform boundary, independent of member role — an app the household hasn't granted can't act even for an Owner.
- **Deny by default.** Every endpoint `RequireAuthorization()`; opt out explicitly with a comment. Never trust client-supplied `HouseholdId`/`MemberId`/role — derive from `ICurrentMember`.
- **Test the negatives:** wrong household (tenancy), wrong role (permission), wrong owner (resource) each get a failing-to-access integration test.

## Localization (i18n)

The kernel provides `IAppText` (`HomeOs.Platform.Localization`) — a lightweight, in-code string table for **server-produced** text (API error titles + emails). We chose it over `.resx`/`IStringLocalizer` deliberately: no satellite assemblies or designer files, keys live next to each other, and it exposes an **explicit-culture** overload that `.resx` makes awkward. `RequestLocalizationMiddleware` is registered in `Program.cs` (supported `bs`/`en`, default `bs`) and resolves the request culture from the frontend's `Accept-Language` header (the SPA sends the current UI language on every request).

- **Two resolution paths — use the right one:**
  - `text["key"]` / `text.T("key", args)` → resolves in the **current request** culture. Use for API error titles / validation messages (`Results.Problem`, `ValidationProblem`).
  - `text.T(culture, "key", args)` → resolves in an **explicit** culture. Use for **emails**, which must be in the *recipient's* language (`member.PreferredCulture`), not the sender's. `MemberSummary.PreferredCulture` carries it to event handlers.
- **No hard-coded user-facing strings.** Add new messages to **both** the `bs` and `en` maps in `AppText.cs`. Keys are dotted (`error.finance.txRequired`, `email.confirm.subject`). Unknown key → falls back to `bs`, then the raw key.
- **Emails** are composed with `IAppText.EmailHtml(culture, greeting, bodyLine, ctaLabel, ctaUrl, showRawLink)` for the branded shell; HTML-encode any user-supplied value (name, title) with `WebUtility.HtmlEncode` before interpolating.
- **Identity messages** (password/email) are localized by `LocalizedIdentityErrorDescriber` (wired via `.AddErrorDescriber<>()`), so `UserManager` errors returned in `ProblemDetails` come back in the request language.
- **Formatting:** avoid named-culture lookups that may be absent on minimal Linux images — format dates culture-agnostically per language (e.g. dotted numeric for `bs`, `InvariantCulture` month names for `en`). Store timestamps in UTC, format at the edge.
- **Keep in sync with the frontend:** only strings the server actually emits live in `AppText`; everything else stays in the frontend locale files (`src/platform/i18n/locales/*.json`).

## Comments & documentation

- **`///` XML doc comments on every public type and member** — endpoints, handlers, commands/DTOs, domain events, and kernel interfaces. These are the *contracts other apps depend on*, and they feed OpenAPI/Swagger (`<GenerateDocumentationFile>true`). Document params, returns, thrown domain errors, and events published.
- **Inline comments explain _why_,** not what: business rules, invariants, trade-offs, workarounds, non-obvious MySQL/EF quirks. Delete comments that merely restate code.
- Each vertical slice gets a **one-line intent header** on the handler. Domain events carry a doc comment stating when they fire (their consumers rely on it).
- Keep comments truthful — update them with the code; a stale comment is a bug.

## Security

Defaults are secure; you opt *out* explicitly with a stated reason. Treat OWASP Top 10 as the baseline.

- **Authentication:** ASP.NET Core Identity user store. **Cookie auth** (httpOnly + `SameSite` + `Secure`) for the SPA — no tokens in JS, so XSS can't steal a session; JWT bearer only for mobile/other API clients. Argon2/PBKDF2 hashing (Identity default), account lockout on repeated failures, optional TOTP 2FA, secure password reset + email confirmation.
- **Authorization:** deny-by-default (`RequireAuthorization()` everywhere). Roles→permissions→policies; resource-based checks for per-item actions. See *Authorization, roles & permissions*.
- **Never trust the client for identity/household/role** — always derive from `ICurrentMember` (the auth ticket), never the request body or a header.
- **Tenancy:** every read filtered by household (global query filter) + visibility. App capability checked at the platform boundary.
- **Transport:** enforce HTTPS + HSTS; HTTP→HTTPS redirect. Secure cookie flags.
- **CORS:** allow-list known frontend origins only; never `AllowAnyOrigin` with credentials.
- **CSRF:** anti-forgery tokens for cookie-authenticated mutations.
- **Rate limiting:** the built-in `RateLimiter` on auth (login/reset) and write endpoints to blunt brute-force/abuse.
- **Security headers:** CSP, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `X-Frame-Options`/frame-ancestors, `Permissions-Policy` (via middleware).
- **Injection:** EF Core parameterizes — **never** build SQL by string concatenation; if raw SQL is unavoidable use `FromSqlInterpolated`. Validate + bound every input (FluentValidation).
- **Secrets:** user-secrets (dev) / environment / vault (prod) — never in `appsettings.json` or source, connection strings included. Rotate leaked secrets immediately.
- **Data protection & privacy:** encrypt sensitive fields at rest where warranted; least-privilege DB account; PII minimized and never logged.
- **Auditing:** append-only audit log for sensitive actions (member/role changes, permission grants, deletes) with actor + traceId.
- **Files/uploads (Life admin docs):** validate type/size, store outside webroot, scan, serve via authorized, signed access — never a public path.
- **Supply chain:** pin versions, run `dotnet list package --vulnerable` in CI, keep the framework patched.

## SignalR (real-time)

- One hub (e.g. `HomeHub`); clients join groups by `HouseholdId` (and per-member groups for private items).
- The **event bus** drives it: a notification handler subscribes to domain events and pushes typed messages to the right groups — the UI updates so "changes by one member show up for everyone".
- Push *minimal* payloads (ids + change type); let clients refetch via TanStack Query, or push small DTOs. Respect visibility when choosing groups — never broadcast a private item to the whole household.

## Testing

- **xUnit v3**. Assertions with **Shouldly** (or the free AwesomeAssertions fork). Mocks with **NSubstitute**. Do **not** add Moq (SponsorLink history) or FluentAssertions v8+ (commercial).
- **Handler unit tests**: real logic, faked collaborators (bus, current member). Assert the correct domain event was published.
- **Integration tests**: `WebApplicationFactory` + **Testcontainers MySQL** (a real MySQL in Docker) — the only trustworthy way to test EF + Pomelo + query filters. Reset state between tests (Respawn or per-test DB).
- **Mandatory negative test per module**: a second household/member must NOT be able to read another's data. This guards the whole privacy promise.
- Architecture test: assert no `Modules.X` assembly references another `Modules.Y`.

## Performance & optimization

- **Pagination is mandatory** on every list/collection endpoint — prefer keyset/seek pagination over `Skip/Take` for deep pages.
- **Kill N+1**: project with `Select` to DTOs, `Include` only what you render, `AsSplitQuery()` for multiple collections, `AsNoTracking()` for all reads.
- **Indexes**: every column used in a filter/join/sort gets one — start with `HouseholdId`, due dates, assignee/owner ids, and `EntityLink` (source/target) columns. MySQL keyed strings need a length.
- **Pooling & compiled queries**: `AddDbContextPool<T>()`, healthy Pomelo connection pool, `EF.CompileAsyncQuery` on the hottest paths.
- **Caching**: `OutputCache` / `IMemoryCache` (or `HybridCache` via the `Microsoft.Extensions.Caching.Hybrid` package) for hot, read-heavy endpoints (e.g. the dashboard aggregate); invalidate on the relevant domain event, not on blind TTLs alone.
- **Keep writes fast**: run event handlers *after* the transaction (outbox / background) so fan-out (emails, projections, SignalR) never blocks the request.
- **Transport**: response compression (Brotli), HTTP/2+, `System.Text.Json` source-generated serializer.
- **Measure first**: no micro-optimization without a benchmark/profile. Simplicity and correctness win ties.

## Simplicity (KISS / YAGNI)

- Abstract only in the kernel; keep slices concrete and readable. No repository-over-EF, no generic "manager" layers, no speculative extension points inside a single app.
- Fewer dependencies is a feature. Prefer the framework/BCL before adding a package.
- If a junior can't follow a slice end-to-end in one file, it's too clever.

## Library choices — quick reference (licensing-aware)

| Need | Use | Avoid (why) |
|------|-----|-------------|
| Mediator/slices | plain DI handlers | MediatR (commercial) |
| Mapping | Mapperly / by hand | AutoMapper (commercial) |
| Assertions | Shouldly / AwesomeAssertions | FluentAssertions v8+ (commercial) |
| Mocking | NSubstitute | Moq (SponsorLink) |
| Validation | FluentValidation | — |
| Logging | Serilog | — |
| Integration DB | Testcontainers | in-memory provider (hides SQL/MySQL bugs) |

## Definition of done (backend PR)

- [ ] Zero build warnings; nullable clean; `dotnet format` applied.
- [ ] No cross-module reference introduced.
- [ ] Writes publish domain event(s); reads go through the visibility filter.
- [ ] Authorization enforced (deny-by-default; permission/policy + resource check); negative tests for wrong household / role / owner pass.
- [ ] Inputs validated; failures return **localized** `ProblemDetails` (code + traceId); no sensitive data leaked.
- [ ] No hard-coded user-facing strings — new messages added to **both** `bs`/`en` maps in `AppText.cs`; emails/notifications render in the recipient's language via the explicit-culture overload.
- [ ] Public types/members carry `///` XML docs; non-obvious logic commented (why).
- [ ] Handler unit test + integration test (Testcontainers) added; negative-visibility test still green.
- [ ] No commercial-licensed dependency added.
- [ ] Public events/DTOs changed only additively.
