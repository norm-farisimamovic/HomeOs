# Home OS — Design System (the pattern to follow)

This is the **canonical UI pattern**. Every new screen, component, and app must follow it so the whole
product reads as one system. The source of truth lives in code:

- **Tokens:** `frontend/src/shared/styles/tokens.css` (color, type, radius, shadow, motion, module hues)
- **Primitives:** `frontend/src/shared/styles/ui.css` (buttons, chips, thread, avatars, cards, forms, …)
- **Shell:** `frontend/src/app/app.css` (rail + top + dashboard) · **Auth:** `frontend/src/app/auth.css`

**Rule zero:** consume tokens and primitive classes — **never hard-code a color, px value, font, radius,
or shadow.** If something's missing, add a token, don't inline a literal.

## Aesthetic

Warm, calm, "house materials" — **slate, pine, brass**. Not flat SaaS-indigo. Light is a warm off-white
(`--bg #ECEFEA`), pine green is the brand (`--brand #24685A`), brass is the secondary accent. Generous
whitespace, hairline borders (`--line`), soft shadows, rounded corners (`--r-*`).

## Fonts (loaded in `index.html`)

- **Bricolage Grotesque** → display/headings (`--font-display`, `h1`–`h4`).
- **Instrument Sans** → body (`--font-body`).
- **JetBrains Mono** → numbers, code, `.eyebrow`, `.kbd`, avatars, tabular data (`--font-mono`, `.mono`).

## Module hues — every app owns a colour

Each module has a hue token; anything belonging to or derived from that module carries it:

| Module | token | Module | token |
|--------|-------|--------|-------|
| Tasks | `--m-tasks` (blue) | Finance | `--m-finance` (amber) |
| Boards | `--m-boards` (purple) | Life admin | `--m-life` (teal) |
| Calendar | `--m-calendar` (orange) | Lists | `--m-lists` (olive) |
| Reminders | `--m-reminders` (pink) | Meals | `--m-meals` (orange) |
| Notes | `--m-notes` (green) | | |

Apply a hue by setting `--mc` on the element: `style={{ ['--mc']: 'var(--m-tasks)' }}`. Chips, nav
accents, card dots (`.mdot`), stat numbers, empty-state icons and threads all read `--mc`.

## THE SIGNATURE — the "thread"

The product's core promise is *everything connects*. Show it with a **`.thread`**: a dashed pill,
tinted with the **origin module's hue**, that marks an object which came from (or links to) another app.

```tsx
// A task that was created from a bill — the thread carries Finance's hue:
<span className="thread" style={{ ['--mc']: 'var(--m-finance)' }}>
  <Wallet size={14} className="ic" /> {t('thread.from')} <b>Finance · BH Telecom</b>
</span>
```

Use a thread **anywhere a cross-module relationship exists** (a task from a bill, a calendar entry from a
task's due date, a reminder from a renewal, a note linked to an event). This is non-negotiable — it's the
visual language of the platform. Don't invent a different treatment.

## Primitives (class-based, from `ui.css`)

- **Buttons:** `.btn` + `.primary` / `.ghost` / `.danger`, sizes `.sm`, `.icon` (`.icon.sm`).
- **Chips/badges:** `.chip`, `.chip[data-m]` (module-hued via `--mc`), `.chip.solid/.danger/.warn/.ok`.
- **Thread:** `.thread` (see above).
- **Avatars:** `.av` (`.lg`/`.xs`), `.av-stack` for overlaps. Initials, mono font.
- **Cards:** `.card` + `.card-h` (`.t` title row) + `.card-b` (`.flush` for edge-to-edge lists) + `.mdot`.
- **Rows:** `.row-item` (`.body`/`.ttl`/`.meta`/`.end`) for list rows.
- **Forms:** `.field` + `.inp` / `.sel` / `.ta`; `.form-grid` (2-col, `.full` to span); `.err-msg`.
- **Controls:** `.seg` (segmented), `.sw` (switch), `.cb` (checkbox), `.tags`/`.tag` (tag input).
- **Required fields:** mark every required label with `<Req />` (`shared/components/Req.tsx` → `.req-star` asterisk, accessible). Do this consistently on all forms.
- **Toasts:** `.toaster`/`.toast` (`.success`/`.error`/`.info`), rendered by `<Toaster />` (mounted once in `main.tsx`).
- **Confirm dialog:** `.confirm-msg`, `.btn.danger-solid`; rendered by `<ConfirmHost />` (mounted once in `main.tsx`).
- **Text:** `.eyebrow` (mono, uppercase, tracked), `.mono`, `.hint`.
- **Layout:** `.grid` + `.g2`/`.g3`/`.g4`; page wrapper `.wrap` (`.wrap.wide`); header `.page-h` with a
  module-hued `.eyebrow`, `h1`, `.sub`.
- **Empty state:** `.empty` with `.empty-ico` (module-hued).

## Shell

- **Rail** (`.rail`): `HomeOS` brand mark, **grouped** nav (`.nav-grp` labels: Apps, Household), each
  `NavLink` carries its module hue (`--mc`) → coloured icon + active accent bar; `.rail-foot` user card.
- **Top** (`.top`): `.searchbox` (⌘K), primary quick-capture, notifications, language, theme toggle, avatar.
- Nav is rendered from a single array (see `AppShell.tsx`) — the client mirror of the app registry.

## Icons

**lucide-react**, `strokeWidth` default, `className="ic"` (+ size prop). Sizes: 17 (default), 14 (`.sm`),
19–20 (nav / `.lg`). Colour comes from the surrounding `--mc` / `currentColor` — don't set icon colours inline.

## Theming & density

- Light + dark: tokens flip via `@media (prefers-color-scheme: dark)` **and** `:root[data-theme="dark"]`
  (the manual toggle). All colours are tokens, so components theme automatically — never write per-theme CSS.
- Density: `html[data-density="compact"]` tightens `--tap` + base size. Respect `prefers-reduced-motion`.

## Building a new app (visual checklist)

1. Pick/confirm the app's **module hue** token.
2. Add a nav entry (icon + hue) — it lights up the rail automatically.
3. Page = `.wrap` → `.page-h` (module-hued `.eyebrow` + `h1` + `.sub` + `.actions`) → `.card`s.
4. Use existing **primitives**; never reinvent a button/card/field.
5. Show every cross-module link as a **`.thread`** in the other module's hue.
6. Provide **empty / loading / error** states (`.empty`, skeletons) — designed, not blank.
7. Verify light **and** dark, and ≤360px width. No hard-coded values — grep your diff for hex/px.

**Reference implementation: `src/apps/tasks/`** — copy its layout for a new app:
`api.ts` (typed client + **query keys as reusable contracts**), `hooks.ts` (TanStack Query hooks; mutations invalidate keys, toggles are optimistic), a page (`TasksPage.tsx` — `.wrap` + `.page-h` + `.seg` filter + grouped `.card`s), a **reusable row** (`TaskRow.tsx`, used on both the page and the dashboard widget), and a `.modal` form (`TaskModal.tsx`). Member data comes from `platform/members/useMembers`. The dashboard consumes the app's hooks — it doesn't refetch or duplicate.
