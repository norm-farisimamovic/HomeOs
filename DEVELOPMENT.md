# Home OS — Development Plan

The master build plan for Home OS. **Product/roadmap decisions live here; detailed coding conventions live in the skills** (`.claude/skills/dotnet-backend/`, `.claude/skills/react-frontend/`) and evolve as we go. Read this first, then the relevant skill before writing code.

> **Vision (from `ZADATAK.pdf`):** one personal "home operating system" that brings a household's whole life-admin into a single connected place, shared between members, with email notifications — where *everything is connected* (a bill creates a task, a task shows on the calendar, a reminder comes from anywhere). Crucially it is a **platform**: the built-in apps are just the first ones installed, and new apps must plug in as first-class citizens without touching existing code.

---

## 1. Product decisions (locked)

| Area | Decision |
|------|----------|
| Purpose | **Real product** to actually use — robustness + daily-use polish matter. |
| Pace | **Fast but professional** — move quickly, never at the cost of quality gates. |
| Build order | **MVP vertical slice first**: platform kernel + auth/members + Tasks + Dashboard end-to-end, prove extensibility, then add apps. |
| First app after MVP | **Finance** (bills/subscriptions/budgets/who-owes). |
| Tenancy | **Single household now, multi-tenant-ready** — `HouseholdId` on every entity; no rework to scale later. |
| Platform | **Installable PWA** — responsive web (phone→ultrawide), all modern browsers, installable + offline app shell. |
| Onboarding | **Self-register + email invites.** First user creates the household and is **Owner**; invites others by email → accept → set password. |
| Email | **SMTP (own mailbox)** behind a swappable `IEmailSender`; a dev catcher/console sink for local dev. |
| Languages | **Bosnian + English**, per-member preference; full i18n front **and** back (emails in each recipient's language). |
| Run (dev) | **No Docker.** Localhost: API `http://localhost:5080`, web `http://localhost:5173`, local MySQL `:3306`. |
| Deploy | **Free hosting at the end** (see §9). |
| Design | **Home OS design system** — warm slate/pine/brass palette, **per-module hues**, the **`.thread`** cross-module signature, Bricolage/Instrument/JetBrains fonts; light + dark via tokens. Canonical spec: `.claude/skills/react-frontend/references/design-system.md` (code: `frontend/src/shared/styles/`). |
| Extensibility | **In-codebase modules obeying platform contracts** (compile-time plug-in via manifest + registry). A new module is discovered automatically and touches **zero** existing files. (Not runtime-loaded DLL plugins — unnecessary complexity/risk for this product; the contract discipline is what the spec requires.) |
| Security | ASP.NET Core Identity; **cookie auth** (httpOnly) for the PWA; **RBAC** (Owner/Admin/Adult/Child/Guest); OWASP hardening; deny-by-default. |

---

## 2. Tech stack

**Backend** — .NET 8 (LTS) / C# 12 · ASP.NET Core Minimal APIs · **Modular Monolith + Vertical Slices** on a shared `HomeOs.Platform` kernel · EF Core 8 + **MySQL (Pomelo)** · **SignalR** real-time · ASP.NET Core Identity · FluentValidation · Mapperly · Serilog · `IStringLocalizer`/`.resx`. Tests: xUnit v3 + Shouldly + NSubstitute + Testcontainers(MySQL). *(.NET 8 LTS — the installed SDK + best free-hosting support.)*

**Frontend** — React 19 + TypeScript (strict) · **Vite** (PWA plugin) · **TanStack Query** (server state) + **Zustand** (UI state) · React Router v7 · **@microsoft/signalr** · **react-i18next** · react-hook-form + Zod · **lucide-react** icons · **token-based design system** (`frontend/src/shared/styles/tokens.css` + `ui.css`). Tests: Vitest + RTL + MSW · Playwright (chromium/firefox/webkit).

**Avoid** (licensing): MediatR, AutoMapper, Moq, FluentAssertions v8+. See the backend skill.

---

## 3. Repository layout (target)

```
HomeOs/
├─ DEVELOPMENT.md            # this file
├─ ZADATAK.pdf               # the spec
├─ .claude/skills/           # coding standards (dotnet-backend, react-frontend)
├─ backend/
│   ├─ HomeOs.sln
│   ├─ src/
│   │   ├─ HomeOs.Platform/          # kernel: events, registry, links, access, members, i18n, notifications, email, search, automations
│   │   ├─ HomeOs.Modules.Tasks/     # app module (vertical slices)
│   │   ├─ HomeOs.Modules.Finance/
│   │   ├─ HomeOs.Modules.Calendar/  # view over Tasks + own events
│   │   ├─ HomeOs.Modules.Kanban/    # view over Tasks
│   │   ├─ HomeOs.Modules.Reminders/
│   │   ├─ HomeOs.Modules.Notes/
│   │   ├─ HomeOs.Modules.LifeAdmin/
│   │   └─ HomeOs.Api/               # thin host: composes modules, auth, SignalR, middleware
│   └─ tests/
└─ frontend/
    ├─ index.html
    ├─ vite.config.ts
    └─ src/
        ├─ platform/        # registry, surfaces, realtime, api, access, i18n, links
        ├─ apps/            # tasks, finance, calendar, kanban, reminders, notes, lifeadmin
        ├─ shared/          # design tokens + primitives (Button, Card, Dialog, Table, StatTile…)
        └─ app.tsx          # registers apps → renders surfaces (only place apps are listed)
```

Architecture rules (no cross-module/app refs, views-not-stores, registry-driven surfaces, layered access) are in the two skills — treat those as binding.

---

## 4. Core platform concepts (built once, reused by all)

- **Members & households** — identity layer; `ICurrentMember`; `HouseholdId` on everything.
- **Roles & permissions (RBAC)** — Owner/Admin/Adult/Child/Guest → permissions → policies; check permissions, never role strings; resource-based checks for per-item edit.
- **Event bus** — in-process pub/sub; the backbone of "everything connects"; fans out to other modules, SignalR, and notifications.
- **App registry + manifest** — every app (built-in or new) registers the same way and contributes to shared surfaces (nav, dashboard widgets, search, command palette, automation triggers/actions).
- **Connected web (entity links)** — generic `EntityLink` so any object links to any other across apps.
- **Access: authN → household → role → capability → visibility** — layered, server-enforced; `Private`/`Household`/`Shared` visibility.
- **Reminders service · Notifications · Email (SMTP) · Digests** — platform capabilities; per-member email preferences; emails/notifications localized per recipient.
- **Search & command palette · Automations** ("when this, then that") — registry-driven.
- **i18n** — BE (`IStringLocalizer`) + FE (react-i18next), driven by member `PreferredCulture`.
- **Real-time** — SignalR hub, grouped by household/member, driven by the event bus.

---

## 5. The apps (scope from the spec)

| App | Scope | Milestone |
|-----|-------|-----------|
| **Dashboard** | "Today" aggregate (tasks due, today's events, upcoming bills, active reminders); quick-capture; global search. | M2 |
| **Tasks** | Due dates, priorities, assignee, sub-tasks, tags, recurring; complete/overdue. | M2 |
| **Kanban** | Board/columns view over Tasks (To do/Doing/Done), drag between columns, multiple boards. *View, not a store.* | M2 |
| **Finance** | Expenses/income by category + budgets; subscriptions/recurring bills w/ due dates; bill-due alerts; monthly summary; who-paid/who-owes. Charts. | M3 |
| **Calendar** | Month/week/day; task due dates auto-appear; shared household events. *Consumes Tasks.* | M4 |
| **Reminders** | One-off + recurring, aimed at members, triggerable from anywhere; in-app + email. (Service exists in kernel from M1; full app UI here.) | M4 |
| **Notes** | Tagged notes; daily journal; link a note to a task/bill/event. | M5 |
| **Life admin** | Documents/warranties/renewals/contacts; expiry dates auto-trigger reminders; shared shopping/household lists. | M5 |

---

## 6. Cross-cutting standards (enforced on every change)

Full detail in the skills; the non-negotiables:

- **Security & authZ** layered + server-enforced; RBAC; OWASP hardening; deny-by-default; no tokens in web storage.
- **Errors** → localized RFC-9457 `ProblemDetails` (code + traceId); global handler; FE error boundaries + toasts + inline field errors.
- **i18n** — no hard-coded user-facing strings anywhere; BS + EN; emails per recipient's language.
- **Comments** — XML docs (BE) / TSDoc (FE) on all public/exported members; inline comments explain *why*.
- **Responsive + cross-browser** — mobile-first, fluid, container queries, `dvh`/safe-area, no horizontal scroll; browserslist matrix; verified on chromium/firefox/webkit.
- **Fast/simple/modern/optimized** — pagination, no N+1, caching, code-splitting, virtualization; KISS/YAGNI.
- **Tests** — handler unit + integration (Testcontainers) per slice incl. a negative-access test; FE component tests (RTL+MSW) + Playwright for critical flows.
- **The extensibility gate** — after M1, adding a new app must touch **zero** existing files. Verified whenever we add one.

---

## 7. Milestone roadmap

Each milestone ends only when its **acceptance gate** passes (build clean, tests green, standards met).

### M0 — Foundations & tooling ✅ (complete — 2026-07-23)
Repo structure; `HomeOs.sln` + projects; Vite app; MySQL connection; Serilog; `TreatWarningsAsErrors`; lint (oxlint) + `dotnet format`; browserslist; design-token system; i18n bootstrap (BS/EN); base `ProblemDetails` + exception handler; health checks; README run steps.
**Gate:** `dotnet run` serves the API on `:5080`, `vite` serves the web on `:5173`, both talk to MySQL; lint/build clean.
**Status:** ✅ Backend builds (0 warnings) & runs on `:5080` (`/api/ping`, `/health`, Swagger). ✅ Frontend builds & runs on `:5173`; PWA SW + manifest generate; proxy `/api`→API verified live. ✅ Design tokens (light/dark), i18n (BS/EN), TanStack Query, Zustand theme store, router shell all wired. ✅ **Startup auto-creates the DB + applies migrations, then runs data seeders** (`Database:AutoMigrate`/`Database:Seed`; `IDataSeeder` + `AddDataSeeder<T>()`) — verified: app creates `homeos` and records `__EFMigrationsHistory`; `/health/ready` green. See `docs/SEEDING.md`. (First real seeders — roles/permissions, demo household — land in M1.)

### M1 — Platform kernel + Identity/Access
Members & households; ASP.NET Identity + **cookie auth**; **self-register (creates household, Owner) + email invites**; **RBAC** (roles→permissions→policies) + resource-based authz; **event bus**; **app registry + extension points**; **entity links**; **visibility filter** + household global query filter; **SignalR** hub; **email (`IEmailSender` SMTP)** + notifications + per-member preferences; localization pipeline (server messages in BS/EN). Frontend: `platform/*` layers (api, access, i18n, realtime, registry, surfaces shell), auth flows, app shell.
**Gate:** a member can register, create a household, invite + accept; login/logout secure; a trivial demo module proves registry/events/links/visibility work; negative-access tests pass (wrong household/role/owner denied).
**Status (in progress):** ✅ **Auth vertical slice** — `Member` + `Household` entities, ASP.NET Identity + cookie auth, self-register (creates household + Owner), `login`/`logout`/`me`, 5 roles seeded via `RolesSeeder`, `AddIdentityAndHouseholds` migration auto-applied on startup. Frontend: login + register pages (react-hook-form + Zod), protected routes, dashboard shows member/household/role, logout — **verified end-to-end through the Vite proxy incl. cookies**. ⏳ **Remaining M1:** email invites (SMTP), RBAC policies + resource-based authz, event bus, app registry / extension points, entity links, visibility filter, SignalR, notifications + per-member preferences, tests (unit + integration + negative-access).
**Design:** ✅ Adopted the **Home OS design system** (slate/pine/brass, per-module hues, `.thread` signature, Bricolage/Instrument/JetBrains fonts, `ui.css` primitives). Rebuilt shell (rail + top), split-screen auth, and the Today dashboard (hero + connection-map signature) to it. Grouped module-hued nav; unbuilt apps route to a themed *Coming soon* page. Spec: `react-frontend/references/design-system.md`.
**Members, invites, email, profile, settings:** ✅ **Email** capability (`IEmailSender` — SMTP via `Email:Smtp:*`, dev logging sink otherwise). ✅ **Invite family members** (Owner/Admin): `/api/members/invite` → emailed accept link → public `/invite/{token}` accept page → member joins with role; list/cancel invites; change role; remove member (RBAC-gated). ✅ **Profile** (edit name/language, change password) + **Settings** (theme/density/language) + **Notifications** page. ✅ **Every topbar button wired**: quick-capture → New Task, search/⌘K → command palette, avatar → menu (Profile/Settings/Household/Sign out), bell → Notifications. ✅ **Task-assigned email** via event handler (event-driven notification). ✅ **Role-based visibility**: managers see all except others' `Private`; members see own + `Household` (verified). ✅ **Strict email confirmation**: registration sends a verify link and creates **no session** until confirmed; login blocks unconfirmed accounts (403); `POST /api/auth/confirm-email` + `resend-confirmation` + public `/confirm-email` page (invited members are auto-confirmed when they accept). Verified end-to-end. ✅ **Real SMTP** (Brevo) configured in user-secrets; live confirmation email verified delivered. ✅ **Forgot/reset password** (`/api/auth/forgot-password` → localized reset email → `/reset-password` page → `/api/auth/reset-password`); login page shows a **"resend confirmation"** action when a login is blocked by an unconfirmed email (403), plus a **"Forgot password?"** link. ✅ **Full server-side localization**: kernel `IAppText` (`bs`/`en`) localizes all API error titles + validation + **emails**; the SPA sends `Accept-Language` (current UI language) so errors come back localized, while **emails render in the recipient's own `PreferredCulture`** (branded HTML shell); `LocalizedIdentityErrorDescriber` localizes password/account errors. Verified both languages via curl. Frontend BS copy given a naturalness pass (consistent formal register, de-calqued). ✅ **Notifications system (M6):** kernel `Notification`/`NotificationPreference` tables + **`INotificationService`** — every notification is an in-app feed item, pushed **live over SignalR** (`/hubs/notifications`), and optionally emailed per the member's **per-category preference** (persisted; demo `@homeos.local` addresses are skipped so nothing bounces). Endpoints: feed + unread count, mark-read / read-all, get/set preferences. Bell shows a live unread badge; Notifications page is a real feed + working email toggles. Task-assigned now flows through this (in-app + email + push). ⏳ Still ahead: digests, more event→notification rules.

### M2 — Tasks + Dashboard (usable MVP) + Kanban + extensibility proof
Tasks (CRUD, due/priority/assignee, sub-tasks, tags, recurring, complete/overdue) emitting domain events; **Dashboard "Today"** aggregating from the registry; **quick-capture** + **command palette** + **global search**; **Kanban** as a pure view over Tasks (drag = status command); real-time updates; email on task-assigned. **Extensibility proof:** add a tiny throwaway app (e.g. a mini "meal idea") reusing Tasks — touching zero existing files — then remove it cleanly.
**Gate:** a household can genuinely manage tasks daily across devices; Dashboard/search/palette include Tasks purely via the registry; Kanban stores no task data; extensibility proof passes.
**Status (in progress):** ✅ **Tasks app** — first real module `HomeOs.Modules.Tasks` (own `TasksDbContext`, vertical-slice endpoints: list/summary/create/update/toggle/delete), `TaskItem : IHomeObject` (title/description/due/priority/assignee/tags/visibility/status), household-scoped + member-visible, publishes `TaskCreated/Updated/Completed/Deleted` on the **event bus**. Frontend: `apps/tasks` (grouped list, New/Edit modal, complete toggle, priority/tags/assignee) + **live dashboard** (real stats + tasks-due widget). Kernel added: **event bus**, `ICurrentMember`, `IMemberDirectory`, `IHouseholdLookup`, `/api/members`, per-module **migration registry** (`MigratableContext`). Dev **demo household** seeded (`demo@imel.ba` / `Demo1234!`) + demo tasks. Tests: 5 domain unit tests pass; live **negative-access** test proves cross-household isolation (B sees 0 of A's tasks, 404 on toggle/delete, 401 anonymous).
**UX pass (2026-07-23):** ✅ Global **toast** system (`toastStore` + `<Toaster/>`) — every success/error is a themed notice, no raw text. ✅ Global **confirm dialog** (`confirmStore` + `<ConfirmHost/>`, replaces native `window.confirm`): required **before every delete/remove/cancel and before marking a task complete**. ✅ **Task delete** in the UI, **permission-gated** — server computes `canEdit`/`canDelete` per member (delete = author or Owner/Admin, via `Deletable`); modern task rows (priority accent, hover-reveal edit/delete). ✅ Same confirm+toast pattern applied to Finance (delete tx/bill) and Household (remove member / cancel invite / role change). ✅ **First + last name** everywhere: `Member`/`HouseholdInvite` gained `FirstName`+`LastName` (migration `AddMemberNames`, existing rows backfilled from `DisplayName`); register/profile/invite forms split the field; `DisplayName` kept as derived "First Last". ✅ **Required fields marked** with `<Req/>` asterisk across forms. Skills updated (design-system + conventions "house rules"). ✅ **Kanban board (M2)** — `apps/kanban` at `/boards`: a **view over Tasks** (not a new store), three columns **To do / Doing / Done** with native HTML5 **drag-and-drop** between them. Backend: `TaskItem.MoveTo(status)` + `POST /api/tasks/{id}/status` (Editable-gated, publishes `TaskUpdated`/`TaskCompleted`); frontend `useSetTaskStatus` (optimistic, rollback+toast on error). Dropping into **Done** triggers the complete-confirm (house rule); cards reuse the Tasks data + `TaskModal` for create/edit; permission-gated (only editable tasks are draggable). Verified via curl: Todo→Doing→Done→Todo transitions flip `status`/`isDone` correctly. ⏳ Remaining M2: quick-capture/palette/search surfaces, recurring + sub-tasks, real-time via SignalR.

### M3 — Finance (first real value app)
Expenses/income by category + budgets; subscriptions/recurring bills with due dates; **bill-due → reminder/task/calendar** via events (showcases "everything connects"); monthly summary + who-paid/who-owes; charts (load the `dataviz` skill).
**Gate:** a bill due soon creates a reminder + shows on Dashboard/Calendar with no direct coupling; summary + budgets correct; charts responsive in light/dark.
**Status (in progress):** ✅ **Finance app** — module `HomeOs.Modules.Finance` (own `FinanceDbContext`; PascalCase `Transactions`/`Bills`; decimals `HasPrecision(12,2)`, enums as strings). `Transaction : IHomeObject` (expense/income, amount/currency/category/date/paidBy/visibility) and `Bill : IHomeObject` (name/amount/cadence/nextDue/category/whoPays, `IsDueWithin`). Vertical-slice endpoints: list/create/delete transactions & bills + **`/summary`** (this-month income/spent/balance, spend-by-category, per-member paid + fair-share **net owed/owes**, bills due within 30 days). Role-based visibility (`VisibleTx`/`VisibleBills`/`CanEdit`); publishes `TransactionAdded`/`BillAdded` on the event bus. Dev seeder adds ~5 transactions + 3 bills to the demo household. Frontend `apps/finance`: summary stat cards, transactions list, upcoming bills, who-paid balances, Add-transaction/Add-bill modals (shared `<Modal>`), BS/EN i18n, `/finance` route live (replaced *Coming soon*). **Verified end-to-end via curl**: login → GET summary/transactions/bills (correct totals), POST/DELETE round-trips (201/204), unauth → 401. ⏳ Remaining M3: budgets, charts (`dataviz`), and the **bill-due → reminder/task/calendar** cross-module automation (lands with M4 Reminders/Calendar).

### M4 — Calendar + Reminders
Calendar month/week/day; **task due dates auto-appear** (via Tasks contract/projection, not duplicated); shared events; full Reminders app UI (one-off + recurring, aimed at members) on the kernel reminder service; in-app + email.
**Gate:** tasks/bills/events all surface on the calendar without cross-module refs; reminders fire in-app + email, localized, respecting preferences.
**Status (in progress):** ✅ **Calendar app** — module `HomeOs.Modules.Calendar` (own `CalendarDbContext`; `CalendarEvents` table; `CalendarEvent : IHomeObject` — title/date/time/location/notes/visibility). Vertical-slice endpoints: events CRUD + **`/month`** feed. ✅ **"Everything connects" — proven & decoupled:** new kernel contract **`ICalendarSource`** (`HomeOs.Platform.Calendar`); Tasks and Finance each register a source (`TasksCalendarSource` = due-dated tasks, `BillsCalendarSource` = bills' next-due) touching **zero** other modules; the Calendar's `/month` injects `IEnumerable<ICalendarSource>` and merges everything. Verified via curl: one month feed returned **6 tasks + 2 bills + 2 events merged**, role-based visibility applied, unauth → localized 401. Frontend `apps/calendar`: responsive **month grid** (colour-coded pills by source, today marker, click-a-day to add), legend, editable **upcoming-events** list, add/edit **EventModal** (with delete-confirm + toasts + `<Req/>` markers), BS/EN i18n, `/calendar` route live (replaced *Coming soon*). ✅ **Sharing ("Chosen people")**: events gained a `SharedWith` member list; when visibility is *Shared*, the modal shows a **member picker**, and those members **see the event even though they didn't create it** ("attached to me"). Note: Pomelo can't query the JSON `SharedWith` collection, so visibility uses a translatable coarse DB filter + an in-memory `CanSee` refinement (fine at household scale). Verified: a member sees a household event + an event shared with them, but not another member's Private event. ✅ **Reminders app** — module `HomeOs.Modules.Reminders` (`Reminders` table; `Reminder : IHomeObject` — title/date/time/notes/**forMember**/visibility/done). CRUD + toggle-done endpoints; role-based visibility where the **target member always sees it** ("attached to me"); publishes `ReminderCreated`. Registers a `RemindersCalendarSource` so reminders **auto-appear on the calendar** (feed now merges 4 sources: tasks + reminders + finance + events). Frontend `apps/reminders`: grouped page (overdue/today/upcoming/done) with complete-confirm + delete-confirm + toasts, `ReminderModal` (target-member select, date/time, `<Req/>`), BS/EN i18n, `/reminders` route live. Dev seeder adds 3 reminders. Verified end-to-end via curl (migration, seed, CRUD, toggle, calendar merge). ⏳ Remaining M4: week/day calendar views, the **kernel reminder service firing** (in-app + email, per-member prefs, scheduled job), recurring events/reminders.

### M5 — Notes + Life admin (+ shopping lists)
Notes with tags, daily journal, linking to task/bill/event via entity links; Life admin records (documents/warranties/renewals/contacts) with expiry → auto-reminders; shared shopping/household lists.
**Gate:** a renewal's expiry auto-creates a reminder; a note links to a bill and both render each other via the connected-web renderer; lists shared/real-time.
**Status (in progress):** ✅ **Notes app** — module `HomeOs.Modules.Notes` (`Notes` table; `Note : IHomeObject` — title/content/tags/pinned/visibility/`SharedWith`). CRUD + pin/unpin endpoints; role-based visibility with the same **member-picker sharing** as Calendar (own + Household + shared-with-me + manager-non-private, in-memory `CanSee`); publishes `NoteCreated`. Frontend `apps/notes`: responsive **card grid** (pinned first, tags, content preview), `NoteModal` (content, tags, visibility + share picker, `<Req/>`), pin toggle, delete-confirm + toasts, BS/EN i18n, `/notes` route live (replaced *Coming soon*). Dev seeder adds 3 notes. Verified end-to-end via curl (migration, seed, CRUD, pinned ordering, shared-to-me visibility, private hidden from other member). ✅ **Life admin app** — module `HomeOs.Modules.LifeAdmin` (`LifeRecords` table; `LifeRecord : IHomeObject` — title/category/expiresOn/provider/notes/visibility). CRUD, role-based visibility, expiry dates on the calendar (`LifeCalendarSource` → feed now merges **5 sources**). ✅ **Gate met — "renewal's expiry auto-creates a reminder":** new kernel capability **`IReminderService`** (`HomeOs.Platform.Reminders`, implemented by the Reminders module as `ReminderService`, idempotent per `SourceKey`+`SourceId`); Life admin injects it and, on save, schedules a reminder **7 days before expiry** — updating the date **reschedules** it (no duplicate), deleting the record **removes** it. Life admin references **zero** other modules. Frontend `apps/life`: grouped page (expiring-soon / all), `LifeRecordModal` (category, expiry, provider, notes, `<Req/>` + expiry hint), delete-confirm + toasts, BS/EN i18n, `/life` route live. Dev seeder adds 3 records + their auto-reminders. **Verified via curl**: create→+1 reminder (7 days before), edit expiry→reschedules (no dup), delete→reminder removed, calendar merges the `life` source. ⏳ Remaining M5: shopping lists, entity-links (note ↔ bill/task) via the connected web, Notes daily-journal.

### M6 — PWA + polish + automations + digests
Installable PWA (manifest, icons, offline app shell, update flow); optional push; **automations** ("when this, then that") over the event bus; **daily/weekly email digest**; performance pass (Web Vitals), full a11y + cross-browser QA (chromium/firefox/webkit + real iOS), empty/loading/error states everywhere.
**Gate:** installable on phone + desktop; Lighthouse PWA/perf/a11y green; digests + at least one user automation working end-to-end.
**Status (in progress):** ✅ **Notifications** (in-app feed + SignalR live push + per-category email prefs) and **reminder firing** (`ReminderDispatcher` background job) — see M1 note. ✅ **Automations engine** — module `HomeOs.Modules.Automations` (`Automations` table; rule = trigger + action + message + enabled). New kernel event **`AppActivity`** (generic "something happened", `Kind` like `task.completed`/`bill.added`/`event.scheduled`) that Tasks/Finance/Calendar publish; the `AutomationRunner` (`IEventHandler<AppActivity>`) matches the household's enabled rules and runs the action (**notify**) — **zero references to those app modules**. Frontend `apps/automations` at `/automations`: rules list with enable/disable switch + `AutomationModal` (trigger/action/message), toasts + delete-confirm, BS/EN i18n, nav entry. **Verified via curl**: rule "when bill added → notify" fired a notification when a bill was created. ✅ **PWA polish**: manifest aligned to the pine brand (`theme_color`), `lang`/`categories`/`scope`/maskable icon, Workbox app-shell offline fallback (`navigateFallback`), autoUpdate SW. ⏳ Remaining M6: daily/weekly **email digest**, more triggers/actions, Lighthouse/a11y/cross-browser pass.

### M7 — Deploy to free hosting
Pick host (see §9); production config + secrets; migrations; prod SMTP; HTTPS/HSTS/CORS/CSP; seed/first-run; smoke test.
**Gate:** live URL, installable PWA, real email working, secure headers verified.

---

## 7b. Spec audit vs ZADATAK.pdf (2026-07-24)

Full pass over every checkbox in `ZADATAK.pdf`. **All 8 apps + the extensibility/sharing/email pillars are in place.** Fixed in this pass:

- ✅ **Dashboard now pulls real data** — was showing hard-coded `0` for events/bills; now: live task stats, today's events (from Calendar), bills due soon (from Finance), and a unified **"Coming up"** feed merging every app (events/tasks/bills/reminders/renewals). Stat cards deep-link.
- ✅ **Global search across everything** — new kernel `ISearchProvider`; Tasks/Notes/Finance/Calendar/Reminders/Life each register one; `/api/search` merges them; the ⌘K palette shows grouped, colour-coded results (a new app appears in search the moment it registers a provider — extensibility surface).
- ✅ **Get alerted before a bill is due** — `BillDispatcher` background job notifies (in-app + email, `billDue` category) 3 days before a bill's due date (mirrors the reminder dispatcher). Verified.
- ✅ **Quick capture = task / note / reminder** (was task-only) — the top-bar ⚡ opens a menu.

**Prioritized backlog (spec items not yet built — none block the core, all are additive):**

1. **Recurring** tasks & reminders (spec lists both) — cadence field + expansion on completion/fire.
2. **Sub-tasks** (Tasks) — parent/child on `TaskItem`.
3. **Notes → daily journal** space + **entity links** (link a note to a task/bill/event) — the kernel "connected web" (`IHomeObject` + a links table); would also let apps render each other.
4. **Finance budgets** (per-category monthly limits + progress).
5. **Shopping / household lists** (Life admin) — a simple checkable shared list app.
6. **Calendar week & day views** (month is done).
7. **Multiple Kanban boards** (per household area; currently one board over all tasks).
8. **Email digest** (daily/weekly summary) — scheduled job over the notification data.
9. **"Shared with you" notification** when an item is shared with specific members.
10. **App registry / dashboard-widget + palette-action extension points** so a brand-new app self-surfaces on nav/dashboard without editing the shell (nav is still a static list); + **per-app capability grants** (household grants/revokes app access) for the "household stays in control" principle.

---

## 7c. Platform + UX pass (2026-07-24)

Follow-up hardening pass. All items built, backend rebuilt green, and **verified via curl on `:5090`**.

- ✅ **Audit log (Owner/Admin)** — new kernel capability `IAuditLog` (`Audit/AuditLog.cs`) writing `AuditEntry` rows (household, actor, action, detail, UTC). It is fed **automatically** from the generic `AppActivity` stream via `AuditActivityHandler : IEventHandler<AppActivity>` — every "notable moment" any app announces is recorded with **zero per-app wiring** — plus explicit `RecordAsync` calls on member admin (invite / role change / removal). `GET /api/audit` is **manager-only** (`Results.Forbid()` for non-Owner/Admin). Frontend `/audit` page + nav entry are gated with `managerOnly` so non-managers never see the option. *Verified:* completing a task recorded `task.completed` attributed to the actor; Owner gets 200, the route is role-gated.
- ✅ **Multi-currency** — static registry `Money/Currencies.cs` (BAM base = "KM", EUR peg 1.95583, USD/GBP/CHF/RSD) with `Normalize`/`Get`/`Convert` (2-dp). Per-member `PreferredCurrency` (defaults + normalizes empty→BAM). `GET /api/currencies` feeds the profile picker; Finance converts **all** amounts (transactions, summary, bills, members) into the caller's preferred currency on read. Frontend shows symbols via shared `formatMoney`. *Verified:* switching profile to EUR turned 1920.00 KM into 981.68 € across the summary.
- ✅ **ErrorBoundary** — class component with a localized friendly fallback (reset + reload), wrapping both the app root (`main.tsx`) and the routed `<Outlet/>` so one app crashing never white-screens the shell.
- ✅ **Collapsible nav rail** — the top-bar hamburger now works: on desktop it collapses the rail to icons + small labels (persisted to `localStorage`), on mobile it toggles the off-canvas rail.
- ✅ **⌘K palette fixes** — added a window Escape listener + an explicit close button so the command palette can always be dismissed.
- ✅ **Demo login is `demo@imel.ba` / `Demo1234!`** (was `faris@…`); `@homeos.local` addresses are never sent real mail. Seed data confirmed present for **all 8 apps** (a few rows each).

---

## 7d. Modularity — app registry + household control (2026-07-24)

The spec's headline requirement (*Extensibility* + *The household stays in control*). Built in the kernel (`HomeOs.Platform.Apps`), backend + frontend green, **verified via curl on `:5090`**.

- **Self-describing apps** — each module provides an `IAppModule` with an `AppManifest` (id, name/description, icon, hue, route, API prefix, declared capabilities like `read:tasks`/`write:tasks`). `IAppRegistry` aggregates them with the core surfaces. Adding an app = add its `IAppModule`; it shows up on nav, the Apps page, and in enforcement with no other change.
- **Household control** — `HouseholdApp` per-household state (enabled + granted capabilities) via `IAppAccess`; sensible defaults (a new app is enabled with all its capabilities until the household narrows it). `GET /api/apps` + Owner/Admin `PUT /api/apps/{id}/enabled|capabilities`.
- **Real enforcement, zero per-app edits** — one kernel middleware (`AppAccessMiddleware`, after auth) 403s any `/api/{app}` call when the app is disabled or the verb's capability isn't granted (`read:` for GET/HEAD, `write:` otherwise). Search + calendar also drop contributions from disabled apps. Core surfaces can't be disabled. Every change is audited (`app.enabled`/`app.disabled`/`app.capabilitiesChanged`).
- **Frontend** — the `/apps` route is now a real control panel (was a placeholder): app cards with enable/disable switch + read/write capability chips (Owner/Admin only; others read-only), core badge, toasts. The nav rail hides disabled apps.
- *Verified:* disabling Finance 403s its API and removes it from search + calendar; revoking `write:finance` makes it read-only (GET 200 / POST 403); re-enabling restores it; disabling a core app returns 400.

**Still open from the extensibility section (additive):** dashboard-widget + command-palette action extension points so an app also self-surfaces there (search/nav/enforcement already do); an entity-link service (`IHomeObject` + links table) for the cross-app "connected web"; capability enforcement is coarse (per-app read/write) — finer per-resource scopes could layer on the same `IAppAccess`.

---

## 7e. Reminders/notifications depth (2026-07-24)

Three follow-ups on "get alerted before things are due", all built + **verified via curl on `:5090`**.

- ✅ **Escalating alerts (multi-stage)** — bills and reminders no longer fire once; they climb a lead-day ladder (bills **7 → 3 → 1 → 0** days out, reminders **3 → 1 → 0**) with each stage firing once, via shared kernel helper `HomeOs.Platform.Scheduling.LeadSchedule` (unit-tested). Messages are now written in **each recipient's own language** ("in 3 days" / "za 3 dana", "tomorrow", "due today") — the dispatchers look up the target member's culture. *Verified:* seed bills/reminders produced "Netflix is due in 5 days", "BH Telecom is due tomorrow", reminder "In 3 days".
- ✅ **Recurring** — a `TaskRecurrence`/`Recurrence`/`Cadence` (None/Daily/Weekly/Monthly/Yearly) on tasks, reminders, and bills. Ticking off a recurring **task**/**reminder** rolls it to the next occurrence (past today) and reopens it; the `BillDispatcher` rolls a recurring **bill** forward once its due date passes (and resets its alert ladder) — fixing the old bug where a monthly bill alerted only once. Recurrence picker in the Task/Reminder modals + a repeat chip on rows. *Verified:* weekly task 24 Jul → 31 Jul on complete; daily reminder → next day; overdue monthly bill 23 Jul → 23 Aug on the tick.
- ✅ **Daily/weekly digest** — opt-in per member (profile: Off/Daily/Weekly). New kernel contract **`IUpcomingProvider`** (the member-explicit sibling of `ICalendarSource`, usable from a background job) implemented by Tasks/Finance/Reminders; `DigestService` builds a localized "what's coming up" email and `DigestDispatcher` (hourly) sends it on each member's cadence. `POST /api/digest/preview` sends an on-demand preview (a "Send preview" button on the profile). *Verified:* preview emailed to the demo owner via Brevo (`sent:true`, no errors), and `@homeos.local` sinks are skipped.

---

## 7f. Zero-touch modules + login polish (2026-07-24)

- ✅ **Module auto-discovery** — modules no longer named in `Program.cs`. Each ships an `IHostModule` (`Add`/`Map`); `ModuleLoader` scans every `HomeOs.Modules.*.dll` in the app folder and wires them. Host calls `AddHomeOsModules()` + `MapHomeOsModules()` once. **Adding an app = new project implementing `IHostModule` + a `ProjectReference` so its DLL ships — no edit to the host or any existing module.** *Verified:* all 7 module APIs reachable after the switch; registry still lists 14 apps. It's a **modular monolith** (assembly isolation + kernel contracts), not microservices.
- ✅ **Login "Remember me"** — the login form had it hard-coded to `true`; now it's a checkbox. *Verified:* `true` → persistent cookie (2-week expiry), `false` → session cookie.

---

## 7g. Senior audit vs ZADATAK.pdf — remaining gaps (2026-07-24)

Full pass. **Every app + all extensibility/sharing/email pillars are in place.** Honest, prioritized list of spec items *not yet built* (none block the core) + quality debt:

**Spec features still open**
1. **Sub-tasks** (Tasks) — spec lists "sub-tasks, tags, recurring"; tags + recurring done, sub-tasks not.
2. **Multiple Kanban boards** — one board over all tasks today; spec lists per-area boards.
3. **Calendar week & day views** — only month is built.
4. **Notes: daily journal space** + **entity linking** (link a note to a task/bill/event) — the note↔object "connected web".
5. **Finance budgets** — per-category monthly limits + progress.
6. **Shopping / household lists** (Life admin) — a checkable shared-list app.
7. **"Shared with you" notification** — no alert fires when an item is shared with specific members (other email categories do).
8. **Dashboard-widget + command-palette action extension points** — an app self-surfaces in nav/search/enforcement but the dashboard cards and palette actions are still fixed, not registry-driven.

**Quality debt (not spec features, but a senior would flag)**
- **Security headers not yet applied** — HSTS, CSP, `X-Content-Type-Options`, `X-Frame-Options`, and **rate limiting** are **not** implemented yet (planned for **M7 / deploy**). What *is* in place: httpOnly + `SameSite=Lax` auth cookie, `SecurePolicy=SameAsRequest`, CORS allow-list, Identity **lockout on failed login**, localized ProblemDetails. The cross-cutting "OWASP hardening" list in the stack notes is the *target*, not current state.
- **Thin test coverage** — only `TaskItemTests` + `LeadScheduleTests`. No tests for Finance/Calendar/Reminders/Notes/Life/Automations or the platform (access/visibility/apps/digest). DoD asks for tests incl. negative-access.
- **No architecture guard test** — nothing fails the build if a module references another module (currently clean by convention only).

---

## 7h. Spec gaps closed + hardening (2026-07-24)

Worked through **every** open item from the 7g audit + the senior-flagged quality debt. All build green, **21 tests** pass, and each was **verified via curl on `:5090`**.

- ✅ **Security headers + rate limiting** — baseline headers on every response (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, restrictive CSP; HSTS in prod), and a per-IP rate limiter (120/10s global, **10/30s on `/api/auth`** to blunt brute-force → 429). *Verified:* headers present; 10× 401 then 429.
- ✅ **Architecture guard test** — new `HomeOs.Architecture.Tests` fails the build if any `Modules.X` references `Modules.Y`, and asserts every module ships an `IHostModule`. Plus cross-module domain tests (bill/reminder/task recurrence).
- ✅ **"Shared with you" notification** — kernel `IShareNotifier`; Calendar + Notes ping newly-shared members (in-app + email, category `shared`, their language). New `shared` category in the settings toggles.
- ✅ **Finance budgets** — per-category monthly limit + this-month progress (`Budget` entity, `/api/finance/budgets`), converted to the member's currency; progress bars on the Finance page. *Verified:* Groceries 184.20/300 = 61%.
- ✅ **Sub-tasks** — `TaskItem.ParentId`; sub-tasks nest under their parent on the Tasks page with a done/total chip + inline "add sub-task"; excluded from the Kanban board. *Verified:* parent shows `subtaskTotal` 1.
- ✅ **Multiple Kanban boards** — `Board` entity + `TaskItem.BoardId`; board tabs (All / General / per board + inline "new board"); deleting a board keeps its tasks. *Verified:* board created + listed.
- ✅ **Calendar week & day views** — month/week/day toggle; week fetches both months it spans; day is an agenda. (Frontend over the existing month feed.)
- ✅ **Notes: journal + entity linking** — `Note.EntryDate` gives a dated **journal** mode (All/Notes/Journal filter + "new entry"); and the kernel **`IEntityLinks`** ("connected web") lets a note link to any task/bill/event, chosen via global search (reusable `LinkedItems` component). *Verified:* note→task link created + listed.
- ✅ **Shopping / household lists** — a **brand-new module** (`HomeOs.Modules.Shopping`) added with **no change to the host or any existing module**: it implements `IHostModule` (auto-discovered) + `IAppModule` + an `ISearchProvider`, plus a `ProjectReference` so its DLL ships. *Verified:* it appears in the app registry (15 apps), in nav, and in global search; **disabling it 403s its API and removes it from search** — the whole modularity system covers it for free. This is the reference "new app is a first-class citizen".
- ✅ **Dashboard-widget + palette-action extension points** — the command palette is now **registry-driven** (search-result styling + "go to" come from `useApps`), so a new app self-surfaces there; and a `dashboardWidgets` registry lets an app add a dashboard card (Shopping ships one) — the dashboard hides it when the app is disabled.

**Migration workflow lesson (encoded in the skill):** `dotnet ef migrations add --no-build` silently produced **empty/partial migrations** from stale assemblies (`Budgets`, `Boards` tables never created → 500s). Fixed with corrective migrations generated **with a build**; the convention is now "never `--no-build`; always eyeball the generated `Up()`".

**Still deferred (honest):** integration-level negative-authZ tests (domain + arch tests broadened, but no `WebApplicationFactory` suite yet); per-resource capability scopes (still coarse per-app read/write); moving a task between boards from the edit modal (board is set on create today).

---

## 7i. UX fixes, AI assistant + deferred closed (2026-07-24)

Round of user-reported bugs + the AI feature + the last deferred items. Build green, **32 tests** pass, verified on `:5090`.

- 🐞 **Calendar showed nothing** — the month grid spans 3 months (prev tail / focused / next head) but only the first + last day's months were fetched, so the *focused* month's events were missing. Now fetches all three; a loader was added too. *Verified:* July shows 19 items across all 5 sources.
- ✅ **Currency where it belongs** — the per-member currency picker already lived in the profile; added a **currency switcher on the Finance page header** (re-converts all amounts on read). *Verified.*
- ✅ **Household editing** — member management already allowed Owner **and** Admin (backend + UI); added **rename the household** inline for managers (`PUT /api/members/household`, audited). *Verified: admin rename 200.*
- ✅ **Loaders** — only the calendar lacked one; added.
- ✅ **Move a task between boards from the edit modal** — TaskModal now has a board picker (was set only on create).
- ✅ **AI assistant** (`HomeOs.Platform.Assistant`) — a dashboard "ask anything" box. `POST /api/assistant/chat` runs **Claude tool-use** over **kernel contracts only** (`IReminderService` to schedule, `IUpcomingProvider` to answer "what's coming up") — so it acts as the member, with the same auth/visibility, and a new app that registers `IUpcomingProvider` becomes answerable with no change. Reads `Anthropic:ApiKey` from config (user-secrets, like SMTP); **degrades gracefully** to a "not configured" state when absent. Handles the user's two example prompts (upcoming query + "remind me next week to pay the electricity bill"). *Verified: graceful no-key path; live path needs the key added.*
- 🧹 **Removed my own test noise** — deleted the `REC…`/`ESC…`/`Audit test` rows my curl verifications left in the demo DB, and reset the demo owner's culture to **bs** (notifications localize to the recipient, so the Bosnian demo now reads "sutra", not "tomorrow"). Lesson: verify against a throwaway household, not the demo.
- ✅ **Deferred → done:** `HomeOs.Api.Tests` — **WebApplicationFactory** integration tests assert every protected endpoint 401s an anonymous caller and `/api/ping` stays public (11 tests). Per-resource capability scopes: **documented as intentional** — app-level `read/write` capabilities and the per-row visibility layer are two composed concerns; scoping capabilities per-resource would duplicate visibility (YAGNI).

**To enable the assistant:** `dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-…"` in `HomeOs.Api` (optionally `Anthropic:Model`). Without it the box shows a friendly "not set up" note.

---

## 7j. AI providers, audit-all, member edit, loaders + deploy-ready (2026-07-24)

- ✅ **Free AI providers** — the assistant is now provider-agnostic: any **OpenAI-compatible** endpoint (**Groq** and **Google Gemini** have free tiers, also OpenRouter / local Ollama) or **Anthropic**, chosen by `Assistant:Provider` + `Assistant:ApiKey`/`BaseUrl`/`Model`. Groq is the default base URL. Still degrades gracefully with no key.
- ✅ **Comprehensive audit** — an EF `SaveChanges` **interceptor** (`AuditInterceptor`, added to every module DbContext via `.AddAuditing(sp)`) records **every create/update/delete** with the entity, a readable label, and the acting member — no per-endpoint wiring. Platform admin actions stay explicitly audited (and the interceptor skips platform tables to avoid re-entrancy); background jobs (no actor) are skipped. *Verified: task created/updated/deleted all logged with the actor.*
- ✅ **Global loading bar** — a thin top progress bar driven by `useIsFetching()`/`useIsMutating()` shows on **every** GET/mutation, app-wide (the per-page loaders were too easy to miss). Calendar also got a loader.
- 🐞 **Calendar (again)** — a month grid spans up to 3 months; only the first + last were fetched, so the focused month was blank. Now fetches all three.
- ✅ **Currency on Finance** — a currency switcher in the Finance header (the picker also stays in the profile).
- ✅ **Edit household member** — managers can now edit a member's **name and login email** (`PUT /api/members/{id}`, audited), plus the earlier role-change/remove and household rename.
- ✅ **Deploy-ready** — same-origin architecture: **one image serves the API + the built PWA** (the app now `UseStaticFiles` + `MapFallbackToFile`, with a split CSP so the SPA isn't blocked). Shipped: root **`Dockerfile`** (SPA→wwwroot + API), **`docker-compose.yml`** (api + MySQL + Caddy auto-HTTPS), **`deploy/Caddyfile`**, **`.env.example`**, **`fly.toml`** (free alternative), and **GitHub Actions**: `ci.yml` (build + all tests with a MySQL service) and `deploy.yml` (**auto-deploy on push to main** via SSH `docker compose up -d --build`). Full guide rewritten in **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** — recommended ~€4–5/mo VPS or free Fly.io, cheap/free domain, secrets via env. This effectively delivers **M7**.

---

## 7k. Dashboard, real-time, avatars, richer audit (2026-07-25)

- ✅ **Live cross-screen refresh** — the SignalR hub now joins each connection to a `household:{id}` group; the audit interceptor broadcasts a **`changed`** signal on every module save, and the client invalidates its queries. So a task ticked off on the dashboard updates the Tasks page (and vice-versa) for everyone in the home — cause-and-effect across screens.
- ✅ **No more infinite lists** — long lists scroll inside their card (`.scroll-list` on Tasks/Reminders/Finance/Life/Audit; Kanban columns capped), and the dashboard lists stay capped. The page never grows without bound.
- ✅ **Richer audit** — updates now record **old → new** per changed field (e.g. `TaskItem: Struja — Priority: Normal → High`), not just `task.updated`. (Detail column widened to 1000.)
- ✅ **Weather widget** — a compact dashboard card via a keyless **Open-Meteo** proxy (`/api/weather`, same-origin so CSP stays strict); uses the browser location once (remembered), else Sarajevo; fails soft.
- ✅ **Avatars** — members upload a profile photo (`POST /api/auth/avatar`, ≤2 MB, stored in `MemberAvatars`), served at `/api/members/{id}/avatar` (household-scoped). The shared `Avatar` shows the photo (initials fallback on none/error) **everywhere a member appears** — tasks, household, finance, reminders, the rail. Profile has upload + remove.
- ✅ **Card polish** — softer depth, hover elevation, per-hue stat tiles, cleaner headers.

---

## 7l. Household chat + assistant that teaches the app (2026-07-25)

- ✅ **Household chat** — a **new plug-in module** (`HomeOs.Modules.Chat`, auto-discovered like the rest; no host/other-module changes) with a live message stream: `GET/POST /api/chat`, pushed over SignalR (`chatMessage` → the household group) so messages appear instantly for everyone. Bubble UI with member avatars, nav entry under Household. Not audited (would flood the log). *Verified: 16 apps in the registry incl. chat; send + list live.*
- ✅ **Assistant explains the app** — the system prompt now carries a concise guide to every screen (Tasks/Boards/Calendar/Reminders/Notes/Finance/Life/Shopping/Chat, sharing, notifications, digest, household), so "how do I add a recurring bill / share a note / invite someone?" gets a real answer, alongside the existing act-with-tools behaviour.

---

## 7m. Ten "fancy" features — chat depth, files, gamification, multi-household (2026-07-25)

A batch of ten user-requested features, each built to the platform's isolation rules (kernel contracts, event bus, registries), builds clean, **36 tests pass** (11→15 API auth tests), and smoke-tested live on `:5090`.

- ✅ **Chat @mentions + @asistent + →reminder** — `POST /api/chat` now parses `@firstname` and notifies those members (`mention` category, in-app only) and, on `@asistent`/`@ai`, runs the assistant *as the sender* and posts its reply as a bot message (all-zero sender id, `Bot` avatar). `POST /api/chat/{id}/reminder` turns any message into a reminder via the kernel `IReminderService`. *Verified: send 200, bot reply skipped cleanly when no key.*
- ✅ **Kernel attachments** — new `Attachment` kernel entity + `/api/attachments` (upload ≤10 MB / list-metadata / download / delete, household-scoped, longblob) mirroring avatar storage. Reusable `<Attachments ownerType ownerId>` dropped into the Task modal, Life-record modal, and a per-bill paperclip modal in Finance — any app can offer "attach a bill photo / warranty PDF" with two props. *Verified: upload→list→download→delete round-trip.*
- ✅ **Data export** — `Settings → Data & privacy → Export my data` gathers every enabled app's data (client-side, from the same authorized GETs; disabled apps skipped) into one `homeos-export-YYYY-MM-DD.json`. "Household stays in control."
- ✅ **Accent colour** — six theme accents (`data-accent` overriding `--brand`, theme-aware via `color-mix`), persisted per member in `uiStore`/localStorage, picker in Settings.
- ✅ **AI digest intro** — `IAssistant.SummarizeAsync` (one-shot, no tools) writes a warm 2–3-sentence "what's ahead" opener prepended to the digest email; best-effort, silently skipped without a key.
- ✅ **Onboarding** — a dashboard welcome card shows only for a brand-new (empty) household: first-step links + one-click **Load examples** seeding a couple of tasks, a note, a reminder and a shopping list across the core apps.
- ✅ **Gamification** — kernel `IScoreboard` (`PointsEntries` ledger, idempotent per source). The Tasks module awards **10 pts** on `TaskCompleted` (to assignee, else completer) and revokes on the new `TaskReopened` event — via the **event bus**, so the kernel never learns what a "task" is. `GET /api/scoreboard` + a medal leaderboard card. *Verified: complete→+10, reopen→revoked.*
- ✅ **Reorderable dashboard widgets** — the `dashboardWidgets` registry now carries weather/scoreboard/household/shopping; the column renders them in the member's saved order (localStorage) with a drag grip.
- ✅ **Multiple households + switching** — `Member.PersonId` groups a person's accounts across households (migration backfills `PersonId = Id` so existing members are never grouped). `POST /api/households` creates a linked secondary owner account (sink `@switch.local` email, no password — never a login), `GET /api/households/switchable` lists them, `POST /api/households/switch` re-issues the cookie as the sibling (same-`PersonId` only → **403** otherwise). Switcher lives in the account menu. Email-login stays unambiguous because only the primary real-email account ever signs in with a password. *Verified live: create "Posao" → switch → `/me` reflects it → 403 on unrelated household → switch back.*

### Follow-ups (2026-07-26)

- ✅ **Chat is a full platform citizen** — `POST /api/chat` now publishes `AppActivity("chat.message")` on the event bus, so "a chat message is posted" is a selectable **automation trigger** and part of the connected web. To avoid swamping the audit log, `AuditActivityHandler` skips `chat.*` kinds (announced, not recorded). Added the trigger to the Automations whitelist + UI (BS/EN). *Verified live: automation with `chat.message` trigger fired on send and created its notification; zero `chat.*` audit entries.*
- ✅ **Discoverable widget controls** — the dashboard widget column now shows an always-visible tools cluster per card (up / down / drag grip), not a hover-only handle, so reordering works on touch and is obvious. Single widget → no controls. (The rail is one column, so there's no width/resize — members reorder, and hide a widget by disabling its app.)
- ⏳ **AI assistant key** — all plumbing is provider-agnostic and defaults to free Groq; it only needs a free `Assistant:ApiKey` in user-secrets/host env (a key can't be minted from here — it's tied to the owner's own free Groq/Gemini account). Works the moment a valid key is pasted. *Gotcha found live:* the demo secret held a Groq **org id** (`org_…`) instead of an **API key** (`gsk_…`) → Groq returns 401; the assistant now surfaces a clear "the AI key looks invalid" message instead of a generic error.

---

## 7n. Assistant made a separate private surface + dashboard polish (2026-07-26)

- ✅ **AI assistant is its own private space** — the assistant no longer replies inside the **household chat** (that's people only now). It lives on a dedicated **`/assistant` page** (nav: *Asistent*, Sparkles): a private 1:1 conversation, per member, thread persisted in localStorage, running as the current member (tool actions scoped to them). Family chat keeps human `@mentions`, `→ reminder`, and the `chat.message` automation trigger. *Rationale: personal Q&A shouldn't clutter the shared stream — matches user feedback.*
- ✅ **Invalid-key UX** — assistant detects provider 401/403 and replies with a clear "key looks invalid (Groq keys start with `gsk_`)" hint.
- ✅ **Discoverable, non-broken widget controls** — replaced the hover-only overlay grip (which floated as orphan controls over card headers) with an **"Arrange" toggle**: editing shows a per-widget strip *above* each card (drag · up · down · show/hide eye), so nothing overlaps and empty/hidden widgets stay reorderable. Hidden set + order persist in localStorage.
- ✅ **Chart widget** — new `SpendingChartWidget` (this month's spending by category as horizontal bars) registered in the dashboard-widget registry (finance-gated).
- ✅ **Modernized "coming up"** — `UpcomingRow`: coloured per-source icon tile, title + meta, and an urgency pill that counts down (Today / Tomorrow / in N days / overdue) shifting colour (brand → amber → danger) as the date nears, with a left accent on hover.

---

## 7o. Exam prep — a new plug-in app (2026-07-30)

A study app for the **professional exam** (stručni ispit), built as a brand-new module the way §7h's Shopping app was: `HomeOs.Modules.Exams` implements `IHostModule` (auto-discovered) + `IAppModule` + an `ISearchProvider`, and touches **no other module**.

- ✅ **Question bank — 727 questions across four laws**, written from the actual legal texts: **ZUP FBiH** (300), **Zakon o inspekcijama TK** (150), **Zakon o državnoj službi u TK** (145), **Zakon o zaštiti na radu FBiH** (132). Every question carries its law, **article reference**, topic, and an explanation. Mix: 601 single-choice, 71 multi-choice, 55 written. The bank is **embedded JSON inside the DLL** (`Bank/Data/*.json`) — reference data, so growing it is a release, not a migration.
- ✅ **Sitting a paper** — `POST /api/exams/attempts` draws a paper (5–100 questions, **round-robin across the chosen laws** so no law dominates, `mixed`/`choice`/`open`), answers autosave (`PUT …/answers/{questionId}`), `POST …/finish` marks it. Correct answers/model answers/explanations are **withheld from the DTO while the paper is open** and only appear on the mark sheet.
- ✅ **Marking on meaning, not wording** — multiple-choice is set equality; written answers go to an **AI examiner** in one batched call through a new kernel method `IAssistant.CompleteAsync` (provider-agnostic — the same `Assistant:*` config the household assistant uses), which returns 0/1/2 points plus one sentence of feedback **in the candidate's language**. With no key configured it falls back to a **key-term/stem overlap** marker (diacritic- and inflection-tolerant), so the exam always produces a mark offline. *Verified live: a paraphrased answer without diacritics scored 2/2; nonsense scored 0/2 with a real explanation of what was missing.*
- ✅ **Grade at the end** — local **1–5 scale** (`GradeScale`: ≥90→5, ≥80→4, ≥70→3, ≥60→2, else 1) with a **60% pass mark**, shown as a score dial + pass/fail + full answer review.
- ✅ **Personal by design** — attempts are filtered by `HouseholdId` **and** `MemberId`; managers do **not** see another member's results (a study record isn't household business). Finished papers are immutable (editing returns 409).
- ✅ **Frontend `apps/exams`** at `/exams`: setup (law picker with per-law counts, length, question mix), a one-question-at-a-time runner (progress bar, question dots, autosave, hand-in confirm), the mark sheet, a **Study** tab over the whole bank (law filter + search, answers folded away for self-testing) and a **History** tab. New `--m-exams` hue token, nav entry, `apps.desc.exams`, full BS/EN copy, and a dashboard widget (last grade + a small pass/fail spark).
- ✅ **Platform surfaces for free** — the bank is in **global search** (⌘K "žalba" → the questions and their articles, deep-linking into study mode), the app appears in the registry (**17 apps**) and obeys enable/disable + `read:exams`/`write:exams` capabilities.
- ✅ **Tests: 34 new** (70 total, all green). `QuestionBankTests` validates the whole bank at test time — unique ids, in-range correct indices, single=1/multi≥2 correct, every written question has a model answer *and* key terms, every question names a law and an article, plus draw/mix/pagination behaviour. `GradingTests` covers choice set-equality, key-term marking (paraphrase → full, partial → half, off-topic → 0, one-word → capped), the 1–5 scale and the pass mark. The architecture guard confirms zero cross-module references.
- *Verified live on `:5090`:* anonymous → 401; perfect choice paper → **100%, ocjena 5**; all-wrong → **0%, ocjena 1**; paraphrased written paper → **100% (AI-marked)**; blank → 0 with "Odgovor nije upisan."; a 20-question mixed paper split exactly 5/5/5/5 across the four laws; editing a marked paper → 409; delete → 204. All verification attempts were then deleted from the demo account.

### Follow-ups (2026-07-30)

- ✅ **Free choice of paper length** — the four presets (10/20/30/50) are now joined by a **number input (5–100)**, and the setup says up front how many questions the current law/mix selection actually holds (`Za ovaj izbor test će imati N pitanja` when the pool is the limit).
- ✅ **Revision mode reworked** — the Study tab is now explicitly *"Ponavljanje — pitanja i odgovori"*: the **answers are visible immediately**, nothing to guess and nothing marked. It uses the **same law picker as the exam** (one law, several for a mix, or none for everything — extracted as a shared `LawPicker`), plus search, and an **eye toggle** that hides the answers to turn the same list into a self-test (each row can then be revealed individually). `GET /api/exams/study` accepts a **comma-separated `law` list**, and the list is **paged (`useInfiniteQuery`, 40/page + "Prikaži još")** so 700 answered questions never land in one response or one DOM tree.
- ✅ **Choice answers never touch the AI** — reaffirmed and now stated in the UI: *"Pitanja sa zaokruživanjem provjeravaju se lokalno — tačni odgovori su u aplikaciji, bez interneta i bez AI-a"*, with a second line clarifying the AI only reads written answers. The code path was already split (`AnswerGrader.GradeChoice` is pure set comparison; only `QuestionType.Open` reaches `GradeOpenAsync`).
- ✅ **Responsive pass, verified in a real browser** — drove Chromium (puppeteer-core) through setup → runner → mark sheet → review → revision → history at **360, 414, 768 and 1440 px** and asserted `document.scrollWidth <= innerWidth` plus "no element extends past the viewport" on every screen: **zero horizontal scroll, zero overflowing nodes**. Fixes that came out of it: `.seg.wrap` for the length/mix controls, law cards to one column under 560px, option rows stacking their tag, the question dots dropping to their own row, full-width primary actions, history rows wrapping, and exam card headers wrapping so the counter chip can't spill out of its pill (counter labels also shortened to `727 pitanja` / `40/432`).
- 🧪 **Rate limiter caught the test harness** — the screenshot run tripped the platform's 120-req/10s per-IP limiter and got 429s (which is the §7h hardening doing its job, not a bug); the harness was slowed down to stay under it. Worth remembering when scripting UI verification.
- ✅ **Tests: 35** in the module (72 total, all green) — added a case proving `Study` mixes several laws and that an empty law list means the whole bank.

---

## 8. Local development (target)

Prereqs: **.NET 8 SDK**, **Node 20+**, **MySQL 8** (local).

- **API:** `cd backend/src/HomeOs.Api && dotnet run` → `http://localhost:5080` (Swagger at `/swagger` in Dev).
- **Web:** `cd frontend && npm install && npm run dev` → `http://localhost:5173` (proxied to the API).
- **DB:** local MySQL on `:3306`; connection string via **user-secrets** (never in source). `dotnet ef database update` applies migrations per context.
- **Email (dev):** `IEmailSender` points at a dev sink (console/file or a local catcher) so we don't send real mail while building; prod uses real SMTP.
- **Port & secrets are launch-mode-proof:** `Program.cs` loads user-secrets in **every** environment and, **only when no URL is set** (no `ASPNETCORE_URLS`/`--urls`), falls back to `UseUrls("http://localhost:5080")` — so the IDE Run/Debug button or a bare `dotnet run --no-launch-profile` (which run as *Production*) still bind to 5080 and find the DB connection string, while a deployment that sets `ASPNETCORE_URLS` is never overridden. For full dev features (Swagger, demo seeding), run with `ASPNETCORE_ENVIRONMENT=Development` — the `scripts/*.sh` and the `http` launch profile already set it.
- Detailed steps land in `README.md` as M0 completes.

---

## 9. Free deployment plan (decide at M7)

> **Full step-by-step guide: [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** — same-origin build, hosts, env vars, SMTP.

Recommended free combo (no Docker required):

- **API + MySQL:** **MonsterASP.NET** (free ASP.NET Core hosting + free MySQL, supports current .NET, publish via WebDeploy/FTP — no Docker) — best fit for our constraints. *Alternatives:* Somee.com (free ASP.NET + MySQL); or an app host + external free MySQL (Aiven free tier / Clever Cloud).
- **PWA frontend (static):** **Cloudflare Pages** or **Netlify** (both free, great for PWA + custom domain + HTTPS). *Alt:* Vercel, GitHub Pages.
- **Config:** lock CORS to the Pages origin; secrets via host env vars; HTTPS + HSTS on; run migrations on deploy.

We'll confirm the exact host near M7 based on current free tiers.

---

## 10. Definition of done (every PR)

Zero warnings/lint · no cross-module/app coupling · authZ enforced + negative test · localized `ProblemDetails` · no hard-coded strings (BS/EN) · public members documented · responsive + cross-browser · tests added (incl. negative-access) · events published/consumed correctly · optimistic + real-time on the FE. (Full checklists in the skills.)

---

## 11. Working agreement

- We build **milestone by milestone**, each behind its acceptance gate.
- **Skills are living** — when we establish a new convention, we update `.claude/skills/*` in the same change.
- Every new app is a test of the platform: if adding it needs edits to existing apps/surfaces, the abstraction is leaking — we fix the platform, not special-case the app.

---

## 12. Backlog

Out-of-current-milestone ideas parked here (with a one-line rationale) so momentum stays on the gate in front of us and nothing good gets lost.

- **Proper PWA icons** (192/512 PNG + maskable) — M0 ships a placeholder SVG; do real icons in M6.
- **Central Package Management** (`Directory.Packages.props`) — nice once module count grows.
- **CI pipeline** (build + test + lint on push) — add once tests exist (M1+).
- **`.env.example`** for frontend + backend config — add when real config/secrets appear.
