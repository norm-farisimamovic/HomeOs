# Home OS Frontend — Code Conventions (reference)

Senior React 19 / TypeScript conventions for this repo. Defaults; deviate only with a stated reason.

## TypeScript

- `strict: true`. **No `any`** — use `unknown` + narrowing, or generics. No non-null `!` except at proven boundaries with a comment.
- Types/interfaces for all props, API DTOs, and store shapes. Derive types from **Zod schemas** (`z.infer<typeof Schema>`) so validation and types share one source.
- Prefer discriminated unions over booleans-soup for component variants and state (`{ status: 'loading' } | { status: 'error'; error } | { status: 'ok'; data }`).
- Path alias `@/` → `src/`. Barrel `index.ts` per app exposing only its public contract (hooks, keys, ref renderer) — nothing else is importable across apps.

## Components

- **Function components + hooks only.** No classes.
- Keep components small and focused; extract logic into custom hooks (`useX`) — components render, hooks decide.
- **Presentational vs. connected:** `shared/` components are dumb and app-agnostic (props in, callbacks out). Data-fetching lives in app hooks, not in `shared/`.
- Always handle the **four states** of async UI: loading, empty, error, success. No spinner-only screens; show skeletons and real empty states.
- Lists need stable `key`s (entity id, never index). Memoize (`memo`, `useMemo`, `useCallback`) only where profiling shows a need — React 19's compiler handles most cases; don't pre-optimize.
- Side effects: `useEffect` is for synchronizing with external systems (subscriptions, the SignalR bus), **not** for deriving state from props — derive during render instead.

## TanStack Query (server state)

- **Query keys are structured and centralized per app** (`taskKeys` factory). Keys are contracts other apps reuse — treat them as public API.
- `queryFn` uses the shared typed `api` client; return typed DTOs.
- Set sensible `staleTime`; rely on SignalR-driven invalidation for freshness rather than aggressive polling.
- **Mutations:** `onMutate` (optimistic update + snapshot) → `onError` (rollback) → `onSettled` (invalidate authoritative keys). Never leave the UI showing stale data after a write.
- Never store query results in Zustand or `useState`. Read them via the hook wherever needed; the cache dedupes.

## Zustand (UI state)

- One small store per concern (or slices), typed. Store **only** ephemeral UI state.
- Select narrowly (`useStore(s => s.isOpen)`) to avoid needless re-renders.
- No async server calls in the store — that's TanStack Query's job.

## Forms

- **react-hook-form + Zod** (`zodResolver`). The Zod schema is the single source of both validation and the TS type.
- Show field errors inline, disable submit while pending, surface server-side `ProblemDetails` errors back onto the form.

## Routing

- Routes are **contributed by app modules**, composed by the platform — no central hard-coded route list of app internals.
- Keep shareable view state in the URL (calendar date, board id, filters) so links are shareable and back/forward work.

## API client

- One typed `fetch` wrapper in `platform/api`: base URL, auth header, JSON, and a single place that turns non-2xx into typed errors (parse `ProblemDetails`).
- Never scatter raw `fetch` across components. Never build query strings by hand in components — do it in the app's `api/` layer.

## Accessibility & UX (the "low friction" principle is a requirement, not polish)

- Command palette + quick-capture: global hotkey, focus trap, arrow-key nav, `Esc` to close, announced to screen readers.
- Use accessible primitives (Radix) for dialogs, menus, popovers — correct roles, focus management, `aria-*` for free.
- All interactive elements keyboard-reachable; visible focus rings; labels on every input; color is never the only signal.
- Respect `prefers-reduced-motion` and `prefers-color-scheme` (light/dark).

## Modern design language

> **Canonical spec: [`design-system.md`](design-system.md)** — the slate/pine/brass palette, per-module
> hues, the **`.thread`** cross-module signature, the Bricolage/Instrument/JetBrains fonts, and the
> `ui.css` primitives (buttons, chips, cards, forms, rail/top shell). Match it exactly; the principles
> below are the general rules behind it.

Eight apps must look and feel like **one** app. That consistency comes from shared tokens + shared primitives, never per-app styling.

- **Design tokens** — CSS custom properties in one `:root`, plus a dark set via `prefers-color-scheme` / `[data-theme]`. Cover semantic color (`--bg`, `--surface`, `--text`, `--muted`, `--accent`, `--danger`), spacing on a 4/8px scale, radius, elevation/shadow, a type scale, and motion durations/easings. Apps consume tokens — **never hard-coded hex or px**.
- **One component library** in `shared/` (Button, Input, Card, Dialog, Menu, Table, Badge, Toast…) on accessible primitives (Radix). No app rolls its own button or modal.
- **Aesthetic** — clean and calm: generous whitespace, strong visual hierarchy, a restrained palette with a single accent, rounded corners, subtle depth. A good variable font or the system stack. Content-first, chrome-light.
- **Responsive & mobile-first** — household members live on phones; every surface works one-handed. Fluid layouts, touch targets ≥44px.
- **Dark mode** via tokens + `prefers-color-scheme`, with a manual toggle. Never a second stylesheet.
- **Motion** — subtle and fast (150–250ms), purposeful (feedback/continuity, not decoration), always honoring `prefers-reduced-motion`.
- **States are designed, not default** — real empty states (with a quick-capture CTA), skeletons for loading, friendly recoverable error states. This directly serves the "low friction" principle.
- **Charts** (Finance summaries/budgets, dashboards) — load the `dataviz` skill *before* building any chart/graph so visualizations share one system in light & dark.

## Responsive & cross-browser

The app must work on **every device and browser** — a household uses whatever they own.

**Responsive**
- **Mobile-first**: base styles target small screens; layer up with `min-width` breakpoints. Design the phone layout first, not last.
- **Fluid by default**: relative units (`rem`, `%`, `ch`), `clamp()` for fluid type/spacing, `min()/max()`. No fixed pixel widths that overflow; `max-width: 100%` on media.
- **Layout**: CSS **Grid** + **Flexbox**; use **container queries** so a component adapts to its slot (a dashboard widget / kanban card), not only the viewport.
- **Breakpoints are shared tokens** defined once (sm/md/lg/xl) — never scatter magic px per component.
- **Touch & pointer**: touch targets ≥44px; branch on `@media (hover: hover)` / `(pointer: coarse)` — never hide critical actions behind hover only.
- **Viewport realities**: correct `<meta viewport>`; use `dvh`/`svh` (not `vh`) to survive mobile URL bars; respect `env(safe-area-inset-*)` for notches/home indicators.
- **No horizontal scroll** at any breakpoint. Wide content (tables, kanban, code) scrolls inside its own `overflow-x:auto` container, never the page body.
- **Orientation & zoom**: usable in portrait/landscape and at 200% browser zoom (accessibility requirement).
- **Responsive media**: `srcset`/`sizes`, modern formats, lazy loading.

**Cross-browser**
- **Support matrix**: last 2 versions of Chrome, Edge, Firefox, Safari (desktop) + **iOS Safari** + Android Chrome. Encode it in **`browserslist`** so Vite / Autoprefixer / transpilation target it automatically.
- **Standards + progressive enhancement**: standard CSS/JS, **feature-detect** (`@supports`, capability checks) rather than UA-sniff, degrade gracefully when a feature is missing.
- **WebKit is the tax**: iOS Safari carries the most quirks (date inputs, `100vh`, sticky, flex `gap`, `backdrop-filter`, scroll behavior). Test on **real WebKit**, not just Chromium.
- **Polyfills only when needed**, driven by browserslist — don't ship them to browsers that don't need them.
- **Consistent baseline**: lean on the CSS reset + tokens for uniform rendering; avoid experimental/prefixed-only APIs without a fallback.

**Verify**
- **Playwright** with three projects — `chromium`, `firefox`, **`webkit`** — plus device emulation (iPhone, Pixel, tablet) for critical flows.
- Smoke-test the smallest phone (~360px) and an ultrawide; check mobile forms with the on-screen keyboard open.

## Notices, confirmation & destructive actions (house rules — apply everywhere)

- **Every success/error surfaces as a toast** via `toast.success/error(...)` (`platform/ui/toastStore`), never raw text dumped in the page. Wire it in the app's `hooks.ts`: mutations `onSuccess` → `toast.success(i18n.t('…'))`. For **non-modal** actions (toggle/delete/role-change) also add `onError` → toast the localized `ApiError.message`. For **modal-form** mutations (create/add/invite), skip `onError` — the modal shows the error inline near the fields (no double message).
- **Confirm before every destructive action (delete/remove/cancel) and before marking something complete** — `await confirm({ title, message, confirmLabel, danger })` (`platform/ui/confirmStore`); never the native `window.confirm`. Pure data *additions* don't need confirmation.
- **Permission-gate action buttons** from server-provided flags (e.g. task `canEdit`/`canDelete`), so users only see actions they may take — but the server still enforces.
- **Names are first + last.** Any name form uses two fields (`firstName`/`lastName`); the backend keeps a derived `displayName` for display.

## Error handling & display

- **Error boundaries** at the app-shell and per-route/app level so one app's render crash never blanks the whole product; boundary shows a friendly, localized fallback + reset.
- **Async errors via TanStack Query**: handle `isError`/`error` in every consumer — never a silent empty screen. Configure sensible `retry` for transient failures; show a retry button for the rest.
- **Mutations**: on failure roll back the optimistic update and raise a **toast** with a localized message from the API's `ProblemDetails`; keep the user's input.
- **Form errors**: map `ValidationProblemDetails.errors` (field → messages) onto react-hook-form fields; show a summary + inline messages.
- **Central HTTP handling** in `platform/api`: parse `ProblemDetails`, switch on its `code`, route `401`→re-auth and `403`→"no access" UX once; surface `traceId` in a "report a problem" affordance.
- **Design the four states** for every data view: loading (skeleton), empty (with a quick-capture CTA), error (recoverable), success. No spinner-only or dead-end screens.

## Internationalization (i18n)

- **react-i18next**, initialized in `platform/i18n`. **No hard-coded user-facing strings** — every label/message is a translation key. Enforce with an eslint rule against literal JSX text.
- **Namespaces per app** (`tasks`, `finance`, …) that plug into the shared instance, mirroring how apps contribute to every other surface; lazy-load a locale's bundles with the route.
- **Formatting via `Intl`**: dates/times (respect the member's timezone), numbers, and **currency** (Finance) — never hand-format. Use i18next plural rules; interpolate values, don't concatenate translated fragments.
- **Language = the member's `PreferredCulture`**, persisted (localStorage `homeos.lang`) and synced to the backend so server-rendered text (emails, notifications, errors) matches the UI. Provide a visible language switcher. The chosen language is saved at registration (`preferredCulture: i18n.resolvedLanguage`).
- **The API client sends `Accept-Language`** (`platform/api/client.ts`, read from `homeos.lang`) on every request, so the backend answers error titles in the current UI language. It reads localStorage directly rather than importing the i18n instance — keeps the client dependency-free. Backend emails, however, use the *recipient's* saved culture, not this header.
- **Keep locale copy natural, not literal.** Translations are product copy, not word-for-word: consistent register (formal "Vi" for full sentences in `bs`; short imperatives are fine on buttons), no calques ("who's holding it" ≠ "ko ih drži"). When adding a server message, add the matching key to `AppText.cs` (`bs`+`en`) too.
- **RTL**: drive direction from the locale (`dir="rtl"`); use logical CSS properties (`margin-inline`, `padding-inline`) so layouts mirror automatically. Keep translations out of code, in locale JSON, ready for translators.

## Security (client reflects, server enforces)

- **Token storage**: prefer httpOnly cookies (set by the API) — nothing auth-related in `localStorage`/`sessionStorage`. If a token must be in JS, keep it in memory only and refresh via a cookie. Send the anti-forgery token on cookie-auth mutations.
- **XSS**: React escapes by default — keep it that way. `dangerouslySetInnerHTML` only on data you sanitized (DOMPurify); never on user/content-derived HTML otherwise.
- **Links & targets**: `rel="noopener noreferrer"` on `target="_blank"`; validate/whitelist any URL you render from data.
- **Config**: only non-secret `VITE_` values reach the bundle; the API base + CSP come from the host. Assume everything shipped to the browser is public.
- **Transport of trust**: the client never decides authorization — it *reflects* it. Always assume the server may reject; handle `403` gracefully rather than pre-trusting hidden UI.

## Roles & permissions in the UI

- `platform/access` exposes `useCan(permission, resource?)` and a `<Can permission=… fallback=…>` component, fed by the member's role→permissions + app capabilities returned at login.
- **Gate through these helpers only** — never branch on a raw role string in a component. Hide *or* disable-with-reason based on the action's cost (destructive → disable + tooltip; irrelevant → hide).
- Gating is **UX, not security**: it reduces mistakes and clutter; the server still authorizes every call. Keep the two consistent so users don't see actions that will 403.
- Reflect role in navigation and quick-capture too (a Guest/Child sees a reduced surface), driven by the registry filtered through `useCan`.

## Comments & documentation

- **TSDoc (`/** */`) on every exported** component, hook, type, and the public app-module contract — what it does, params/props, returns, and any events/keys it exposes for other apps to reuse (those are contracts).
- **Inline comments explain _why_** — a non-obvious effect dependency, a workaround, a business rule, an intentional re-render trade-off. Never narrate JSX.
- Props self-document via precise TS types + TSDoc on the props interface; prefer that over prose.
- Keep comments current with the code; a stale comment is a defect. An eslint/jsdoc rule can enforce presence on exports.

## Testing

- **Vitest + React Testing Library** — test behavior via roles/text as a user would, not implementation details. No shallow rendering, no testing internal state.
- **MSW** mocks the API at the network layer for component/integration tests — real query hooks, faked HTTP.
- Test the **registry contract**: a test that registering a fake app makes it appear in nav/search/dashboard proves the extensibility abstraction holds.
- **Playwright** for critical E2E flows (quick-capture → task appears on dashboard/calendar; a member's change reflected via real-time).
- Cover loading/empty/error states, not just the happy path.

## Performance & optimization

- **Perceived speed first**: optimistic mutations, skeletons over spinners, instant client-side navigation, prefetch on hover/intent (`queryClient.prefetchQuery`). Make it *feel* instant before making it fast.
- **Bundle**: route-level code splitting (`lazy` + `Suspense`) per app so more apps never bloat the initial load; keep a bundle budget checked in CI.
- **Rendering**: virtualize long lists/boards; debounce search feeding `searchProvider`s; select narrowly from Zustand; stable keys. Trust the React 19 compiler — memoize only against a measured problem.
- **Data**: sensible `staleTime` + SignalR-driven invalidation instead of polling; paginate / infinite-scroll large lists; use `select` in queries so a component re-renders only on the slice it needs.
- **Assets**: modern image formats (AVIF/WebP), lazy images, subset fonts with `font-display: swap`.
- **Measure**: Web Vitals (LCP / INP / CLS) and Lighthouse are the bar; profile before optimizing.

## Definition of done (frontend PR)

- [ ] TS strict, no `any`/unused; ESLint + Prettier clean; build passes.
- [ ] Server state in TanStack Query, UI state in Zustand, URL state in the URL.
- [ ] No cross-app import; no `switch (appId)` in a shared surface; new app reachable purely via the registry.
- [ ] Reused existing query keys/hooks instead of duplicating fetching.
- [ ] Mutations optimistic + rollback + invalidate; four async states handled.
- [ ] Real-time invalidation wired for affected queries.
- [ ] Accessible + keyboard-tested; RTL + MSW test added; no console errors/warnings.
