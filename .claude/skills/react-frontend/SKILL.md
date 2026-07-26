---
name: react-frontend
description: Use when writing, structuring, reviewing, or debugging any React / TypeScript frontend code for Home OS — components, hooks, feature modules, the client-side app registry & extension points (nav, dashboard widgets, search, command palette), TanStack Query data access, Zustand UI state, the SignalR real-time client, forms, and tests. Encodes the senior React conventions and the plug-in-app model that mirrors the backend. Trigger on any *.tsx / *.ts / vite / package.json frontend work or frontend design questions.
---

# Home OS — React Frontend (senior engineering standard)

You are a **senior React/TypeScript engineer** building the client for a **platform**, not a single app. The UI must make eight apps (and any future one) feel like **one app** — each contributing to shared surfaces (Today dashboard, global search, command palette, navigation) without any surface hard-coding a list of apps. Same principle as the backend: *could a new app appear everywhere the built-ins do, touching zero existing files?* If not, the design is wrong.

Read `references/architecture.md` before touching the app registry, a shared surface, or the real-time layer. Read `references/conventions.md` before writing components, hooks, or tests. Load them; don't guess.

## Stack (pinned)

- **React 19 + TypeScript 5** (`strict: true`) on **Vite**.
- **TanStack Query v5** — the single source of truth for all *server* state (fetching, caching, invalidation, optimistic updates).
- **Zustand v5** — light *client/UI* state only (command-palette open, active board, filters). Never mirror server data here.
- **React Router v7** for routing (routes contributed by app modules, not hard-listed).
- **SignalR client** (`@microsoft/signalr`) → invalidates/patches TanStack Query caches so "changes by one member show up for everyone".
- **react-hook-form + Zod** for forms & validation (Zod schema shared as the type source).
- **Styling: token-based CSS** — `shared/styles/tokens.css` + `ui.css` primitives = the **Home OS design system** (`references/design-system.md`). **lucide-react** for icons. Accessible patterns for menus/dialogs.
- **Tests:** Vitest + React Testing Library + MSW (mock the API at the network boundary). Playwright for E2E.

## Fast, simple, modern (applies to everything)

Direct requirements from the spec (*"low friction is what makes it actually get used"*; *"one app, not a folder full of separate ones"*) — not nice-to-haves:

- **Simple.** Small components, few dependencies, no premature abstraction. Compose `shared/` primitives; don't reinvent. Delete before you add.
- **Fast — perceived.** Optimistic mutations, skeletons (never bare spinners), instant client-side nav, prefetch on hover/intent (`queryClient.prefetchQuery`). It must *feel* immediate, even on a phone, before it is fast.
- **Fast — real.** Route-level code splitting per app, list/board virtualization, debounced search, small bundles with a CI budget. Trust the React 19 compiler; memoize only against a measured problem.
- **Modern design.** Follow the **Home OS design system** (`references/design-system.md`): warm slate/pine/brass palette, **per-module hues**, the **`.thread`** signature for cross-module objects, Bricolage/Instrument/JetBrains fonts, and the shared `ui.css` primitives. Light & dark via tokens; **never hard-code color/px**. Every app consumes the same tokens + primitives so they read as one product.
- **Responsive & cross-browser (every device, every browser).** Every surface works from ~360px phones to ultrawide, and on the current two versions of Chrome, Edge, Firefox & Safari — including **iOS Safari** and Android Chrome. Mobile-first, fluid layouts (Grid/Flex + **container queries**), relative units, ≥44px touch targets, `dvh`/safe-area insets, **no horizontal scroll**. Feature-detect + progressively enhance; test on real WebKit, not just Chromium. See `references/conventions.md` → *Responsive & cross-browser*.

Full detail in `references/conventions.md` → *Modern design language* and *Performance & optimization*.

## Architecture at a glance

```
src/
  platform/               # THE CLIENT KERNEL — shared by everything
      registry/           #   AppModule type + registry: nav, widgets, search, commands
      surfaces/           #   Dashboard, GlobalSearch, CommandPalette, Nav (render from registry)
      realtime/           #   SignalR connection + event → query-invalidation
      api/                #   typed fetch client, auth, error handling
      links/              #   render the "connected web" (linked objects)
      access/             #   visibility/permission-aware UI helpers
  apps/                   # each Home OS app = one self-contained feature module
      tasks/              #   registers routes, a dashboard widget, search, commands
          index.ts        #     exports the AppModule manifest
          api/ hooks/ components/ routes/
      finance/  calendar/  kanban/  notes/  reminders/  lifeadmin/
  shared/                 # dumb, app-agnostic UI (Button, Dialog, Card, DataTable)
  app.tsx                 # composes registry → surfaces; hard-codes NO app
```

## The golden rules (non-negotiable)

1. **Surfaces render from the registry, never from a hard-coded list.** The dashboard maps over registered widgets; nav maps over registered nav items; search queries registered providers; the palette lists registered commands. Adding an app must not require editing any surface. A `switch (appId)` in a shared surface is a review-blocking defect.
2. **Apps don't import each other.** `apps/finance` never imports from `apps/tasks`. Cross-app cooperation goes through the platform: shared query keys/read hooks exposed as contracts, the link service, and the realtime bus. A cross-app import is a defect.
3. **Server state lives in TanStack Query; UI state lives in Zustand.** Never copy fetched data into Zustand or `useState`. This is the most common React mistake — don't make it.
4. **Reuse data, don't refetch-and-duplicate.** Calendar and Kanban consume the *same* task query hooks/keys the Tasks app exposes as a contract. They don't define their own task fetching. Mirrors backend rule #3.
5. **The UI respects visibility.** Never render an action the current member lacks the capability for; never show items the API shouldn't have returned. Treat the server as the enforcer, the UI as the honest reflector.
6. **Optimistic where it helps, consistent always.** Mutations do optimistic updates + rollback on error, and invalidate the authoritative query keys on settle.
7. **Accessible & keyboard-first.** Quick-capture and the command palette are core to the "low friction" principle — they must be fully keyboard-driven and screen-reader sane.

## Security, auth & roles (client reflects, server enforces)

- **Never store auth tokens in `localStorage`/`sessionStorage`** (XSS-stealable). Prefer httpOnly cookies set by the API; if a token must live in JS, keep it in memory only. Send the anti-forgery token with cookie-auth mutations.
- **Role- & capability-aware UI:** hide/disable what the member's role or an app's capability doesn't allow — through `platform/access` helpers (`useCan('task.edit', item)`, `<Can permission=…>`), **never** hard-coded role strings in components. This is UX only; the server is the real gate, so a hidden action is still rejected server-side.
- **XSS:** rely on React's escaping; never feed untrusted data to `dangerouslySetInnerHTML` (sanitize with DOMPurify if truly unavoidable). External links get `rel="noopener noreferrer"`.
- **No secrets in the bundle** — only `VITE_`-public config; anything sensitive stays server-side. Handle `401`→re-auth and `403`→friendly "no access" centrally in the API client.

## Errors, localization & comments

- **Every failure is visible and recoverable:** route/app-level **error boundaries** for render crashes; **toasts** for mutation failures; **inline field errors** mapped from the API's `ProblemDetails`; explicit loading / empty / error / success states; retry affordances and offline/network handling via TanStack Query.
- **Multilingual by default (i18n):** no hard-coded user-facing strings — all text via **react-i18next** keys. Format dates/numbers/currency with `Intl`; support pluralization and **RTL**; ship a language switcher; persist the member's language (synced to their backend `PreferredCulture`); lazy-load locale bundles. Server errors arrive already localized to the member's language.
- **Comment everything meaningfully:** **TSDoc** (`/** */`) on every exported component/hook/type and on the public app-module contract; inline comments explain **why** (non-obvious effect, workaround, business rule), never restate JSX. Props are documented via their TS types + TSDoc.

## An app module (the unit of frontend work)

Every app exports a manifest the registry consumes — the same shape for built-in and future apps:

```ts
// apps/tasks/index.ts
import type { AppModule } from '@/platform/registry';

export const tasksApp: AppModule = {
  id: 'tasks',
  displayName: 'Tasks',
  icon: CheckSquareIcon,
  routes: taskRoutes,                       // contributed to the router
  nav: [{ to: '/tasks', label: 'Tasks' }],  // appears in navigation
  dashboardWidgets: [DueTodayWidget],       // appears on the Today screen
  searchProvider: searchTasks,              // participates in global search
  commands: [                               // appears in the command palette / quick-capture
    { id: 'task.new', title: 'New task', run: openNewTaskDialog },
  ],
  automations: {                            // triggers/actions for user "when this, then that"
    triggers: [{ id: 'task.completed', label: 'When a task is completed' }],
  },
};
```

```ts
// registered once, in app.tsx — the ONLY place apps are listed
registerApps([dashboardApp, tasksApp, kanbanApp, calendarApp,
              remindersApp, notesApp, financeApp, lifeAdminApp]);
```

Shared surfaces then render purely from `useRegistry()`. A new app added to that one array lights up everywhere.

## Data access pattern

```ts
// apps/tasks/api/keys.ts  — query keys are a CONTRACT other apps may reuse
export const taskKeys = {
  all: ['tasks'] as const,
  dueBetween: (from: string, to: string) => ['tasks', 'due', from, to] as const,
};

// apps/tasks/hooks/useTasks.ts — exposed as the reuse point for Calendar/Kanban
export const useTasksDue = (from: string, to: string) =>
  useQuery({ queryKey: taskKeys.dueBetween(from, to),
             queryFn: () => api.get<TaskDto[]>(`/api/tasks?from=${from}&to=${to}`) });

// Calendar reuses the SAME hook — it does not define its own task fetching.
```

## Definition of done (frontend PR)

- [ ] TypeScript strict, no `any`, no unused; ESLint + Prettier clean.
- [ ] New app data lives in TanStack Query; only UI state in Zustand.
- [ ] No cross-app import; no `switch (appId)` in a shared surface.
- [ ] Reused existing query hooks/keys instead of duplicating fetching.
- [ ] Mutations: optimistic update + rollback + invalidation; loading/empty/error states handled.
- [ ] Real-time: relevant queries invalidate on the matching SignalR event.
- [ ] **No hard-coded user-facing strings** — all via i18n keys; new keys added for every supported language; dates/numbers/currency via `Intl`.
- [ ] **Role/capability gating** via `platform/access`; no tokens in web storage; no untrusted `dangerouslySetInnerHTML`.
- [ ] **Errors surfaced** (boundary + toast + inline from `ProblemDetails`); `401/403` handled centrally.
- [ ] **Exported components/hooks/types have TSDoc**; non-obvious logic commented (why).
- [ ] **Responsive** at mobile/tablet/desktop (no horizontal scroll, ≥44px touch targets, fluid units) and **verified cross-browser** (Playwright chromium + firefox + webkit).
- [ ] Accessible (keyboard, roles, labels); component test with RTL + MSW added.
