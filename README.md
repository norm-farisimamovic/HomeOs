# Home OS

A personal "home operating system" — one app that brings a household's whole life admin into a
single **connected** place, shared between members, with email notifications. It is also a
**platform**: the built-in apps (Tasks, Finance, Calendar, …) are just the first ones installed,
and new apps plug in as first-class citizens without touching existing code.

- 📋 **Product & roadmap:** [DEVELOPMENT.md](DEVELOPMENT.md)
- 🏛️ **Architecture & code structure (report):** [docs/arhitektura-izvjestaj.html](docs/arhitektura-izvjestaj.html) — why the codebase is laid out as a modular monolit + vertical slices, a file-by-file walkthrough of a module (Notes), the frontend `platform`/`shared`/`apps` split, and tests. Open in a browser.
- 🧭 **Engineering standards:** `.claude/skills/` (`dotnet-backend`, `react-frontend`, `project-manager`)
- 🗄️ **Database, migrations & seeding:** [docs/SEEDING.md](docs/SEEDING.md)
- 🚀 **Deployment + SMTP setup:** [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)
- 📄 **Original brief:** [ZADATAK.pdf](ZADATAK.pdf)

## Stack

- **Backend:** .NET 8 (LTS) · ASP.NET Core Minimal APIs · Modular Monolith + Vertical Slices · EF Core + MySQL (Pomelo) · SignalR
- **Frontend:** React 19 + TypeScript · Vite (PWA) · TanStack Query + Zustand · react-i18next (Bosnian + English)

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [MySQL 8](https://dev.mysql.com/downloads/) running locally

## Quick start (scripts)

```bash
scripts/setup-db.sh    # one-time: create the homeos DB + user (prompts for MySQL admin password)
scripts/run.sh         # run API (:5080) + web (:5173) together — Ctrl+C stops both
```

Or run them separately: `scripts/run-api.sh` and `scripts/run-web.sh`.

## Debugging (VS Code)

Press **F5** and pick a configuration (requires the C# extension):

- **API (.NET)** — debug the backend with breakpoints.
- **Web (Chrome)** / **Web (Edge)** — starts Vite and debugs the frontend in the browser.
- **Full Stack (API + Web)** — both at once.

## One-time setup (manual)

**1. Database** — the API **auto-creates the `homeos` database + schema on first run** and keeps
migrations applied (see [docs/SEEDING.md](docs/SEEDING.md)). You only need MySQL running and a
connection string whose user can create/access it.

- **Dedicated app user (recommended):** run `scripts/setup-db.sh` (creates the `homeos` user with
  rights on the `homeos` database). The default dev connection string already matches it.
- **Admin/root user:** point the connection string at it and the app does the rest.

> **🔒 Don't commit credentials.** `appsettings.Development.json` is tracked by git — don't leave a real
> password in it. Use user-secrets instead:
>
> ```bash
> cd backend/src/HomeOs.Api
> dotnet user-secrets init
> dotnet user-secrets set "ConnectionStrings:HomeOsDb" "Server=localhost;Port=3306;Database=homeos;User Id=homeos;Password=YOUR_PASSWORD;SslMode=None;AllowPublicKeyRetrieval=True"
> ```

**2. Install frontend dependencies:**

```bash
cd frontend
npm install
```

## Run (two terminals)

```bash
# Terminal 1 — API  →  http://localhost:5080  (Swagger at /swagger)
cd backend/src/HomeOs.Api
dotnet run
```

```bash
# Terminal 2 — Web  →  http://localhost:5173  (proxies /api and /hubs to the API)
cd frontend
npm run dev
```

Open <http://localhost:5173>. Register your own household, or sign in to the **seeded demo household**
(created automatically in Development, pre-filled with sample tasks):

> **demo@imel.ba** · **Demo1234!**

The dashboard's **System status** card should show **API: Online**; **Database: Online** once the MySQL
step above is done.

## Email

By default emails (invites, task-assigned) are **logged to the API console** — no setup needed. To send
real email, set `Email:Smtp:*` (via user-secrets in dev, env vars in prod). Providers + exact keys are in
[docs/DEPLOYMENT.md §3](docs/DEPLOYMENT.md). Use **port 587** (STARTTLS).

## Health & diagnostics

- `GET /health` — liveness (process up)
- `GET /health/ready` — readiness (database reachable)
- `GET /api/ping` — simple API check
