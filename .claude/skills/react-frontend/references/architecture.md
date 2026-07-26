# Home OS Frontend — Client Architecture (reference)

The client mirrors the backend: apps are plug-ins, shared surfaces render from a registry, and cooperation happens through the platform — never through app-to-app imports.

## 1. The app registry

The registry is the single mechanism that makes an app "appear everywhere the built-ins do".

```ts
export interface AppModule {
  id: string;
  displayName: string;
  icon: ComponentType;
  routes: RouteObject[];                       // React Router routes
  nav: NavItem[];                              // navigation entries
  dashboardWidgets: ComponentType[];           // Today-screen contributions
  searchProvider?: (q: string) => Promise<SearchResult[]>;  // global search
  commands?: Command[];                        // command palette / quick-capture
  automations?: { triggers?: TriggerDef[]; actions?: ActionDef[] };
  requiredCapabilities?: Capability[];         // reflected in UI gating
}

// platform/registry: register once, read everywhere
export function registerApps(apps: AppModule[]): void;
export function useRegistry(): AppModule[];
```

**Rule:** apps are listed in exactly one place (`app.tsx`). Every shared surface derives from `useRegistry()`. No surface may reference a specific app id.

## 2. Shared surfaces (render from the registry)

- **Dashboard / Today** — `useRegistry().flatMap(a => a.dashboardWidgets)` and render each in a grid. Each widget fetches its own data via TanStack Query and handles its own loading/empty/error. The dashboard knows nothing about what an app is.
- **Navigation** — maps registered `nav` items, filtered by the member's capabilities.
- **Global search** — fans a query out to every `searchProvider`, merges + ranks results, groups by app. A new app's results appear automatically.
- **Command palette / quick-capture** — lists all registered `commands`; the "low friction / fast to add" principle depends on this being global and keyboard-first (`Cmd/Ctrl-K`).
- **Router** — composed from every app's `routes`; a shell layout wraps them.

## 3. Real-time layer (SignalR → TanStack Query)

"Changes made by one member show up for everyone" is implemented by turning server events into cache invalidations.

```ts
// platform/realtime/connection.ts
const conn = new HubConnectionBuilder().withUrl('/hubs/home').withAutomaticReconnect().build();

// A single dispatcher maps domain events → query invalidation
conn.on('DomainEvent', (evt: { type: string; payload: any }) => {
  // apps register interest by event type; keep the mapping in each app, not here
  realtimeBus.emit(evt.type, evt.payload);
});
```

- Each app subscribes to the realtime bus for the event types it cares about and calls `queryClient.invalidateQueries({ queryKey: ... })` (or patches the cache directly for small changes).
- The dispatcher is app-agnostic — it never hard-codes event types. Apps declare which events invalidate which keys, keeping the mapping co-located with the data.
- Respect visibility: the server only pushes to groups the member belongs to; the client should still not assume it may render everything — trust the API's filtering.

## 4. The connected web on the client

- `platform/links` renders an object's linked items generically: given an `EntityRef` (`{ type, id }`), it looks up a **registered renderer** for that type (each app registers how to render a chip/preview of its own objects) and displays it.
- This lets a Note show its linked Bill, or a Task show the renewal it came from, **without** Notes importing Finance or Life-admin components. Apps register `renderRef(type, ref)`; the platform composes.

## 5. State ownership — the hard line

| Kind of state | Where it lives | Examples |
|---------------|----------------|----------|
| Server data | **TanStack Query** | tasks, bills, events, members, notifications |
| Ephemeral UI | **Zustand** (or local `useState`) | palette open, active board id, selected filters, drag state |
| URL state | **the URL** (router/search params) | current route, calendar date, board view |
| Forms | **react-hook-form** | in-progress edits before submit |

Never duplicate server data into Zustand/`useState`. If two components need the same server data, they call the same query hook — TanStack Query dedupes and caches.

## 6. Cross-app reuse (the frontend "don't duplicate" rule)

Each app exposes a small **public surface** other apps may import from its `index.ts` barrel: its query keys and read hooks (contracts), and its ref renderer. Apps import only these contracts, never another app's internal components/state.

- Calendar imports `useTasksDue` + `taskKeys` from Tasks — the same hook Tasks uses — so both stay in sync automatically.
- Kanban imports the task read hooks and the `changeTaskStatus` mutation contract; it owns only column layout state.

If an app needs another app's *internals*, that's a signal a contract is missing — add to the exposing app's public surface instead of reaching in.

## 7. New-app acceptance test (client side)

Mirror the backend meal-planner test: create `apps/meal-planner/index.ts`, add it to the `registerApps([...])` array, and it should — with zero edits to any shared surface or other app — get its nav entry, a dashboard widget, search results, palette commands, and reuse the Tasks hooks for its shopping list. If any surface needs editing to accommodate it, the registry abstraction is leaking and must be fixed.

## 8. Design system & consistency

`shared/` is the single design-system layer — **design tokens** (CSS custom properties for color / spacing / radius / type / shadow / motion, with a light & dark set) plus **shared breakpoint / container-query tokens** and accessible primitives (Button, Dialog, Card, Table, …) that every app composes. Responsiveness and cross-browser behavior are solved once in this layer, so every app is responsive and browser-consistent by default (see `conventions.md` → *Responsive & cross-browser*). Apps never hard-code styling or roll their own primitives; that discipline is what makes eight independently-built apps read as **one** product — and lets a ninth app look native on day one. It also keeps things **fast and simple**: shared primitives are optimized and code-split once, and an app author writes features, not CSS. Full guidance in `conventions.md` → *Modern design language* and *Performance & optimization*.

## 9. Auth, access & localization layers

Cross-cutting platform layers every app relies on but never reimplements:

- **`platform/api`** — the typed client owns auth (cookies/token), attaches the anti-forgery token, and centralizes `401` (→ re-auth) and `403` (→ "no access" UX) plus `ProblemDetails` parsing, so no app handles them ad hoc.
- **`platform/access`** — role/capability helpers (`useCan(permission, resource?)`, `<Can>`), fed by the member's role→permissions + granted app capabilities. Surfaces (nav, palette, quick-capture) and apps gate UI through these, **never** raw role strings. Purely presentational — the server enforces; the UI stays consistent with it so users never see an action that will 403.
- **`platform/i18n`** — the react-i18next instance, locale loading, and the language switcher. Apps contribute their own translation namespaces (`tasks.*`) that plug into the shared instance — the same "contribute, don't hard-wire" pattern as every other surface. The active language is the member's `PreferredCulture`, so server-rendered text (emails, notifications, errors) matches the UI exactly.

These three are the client mirror of the backend's access + localization pipeline (`dotnet-backend` architecture §2.5 and conventions → *Localization*). A new app inherits secure auth handling, role/capability gating, and full localization for free by living inside these layers.
