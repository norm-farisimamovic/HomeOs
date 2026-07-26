# Deploying Home OS

**Architecture:** one Docker image serves **both** the API and the built PWA on the **same origin** (simplest, and cookie auth "just works" — no cross-site cookies). MySQL holds the data. Caddy terminates HTTPS with a free auto-renewing certificate. The app **creates its schema and applies migrations on boot**, so there's no manual DB step.

```text
                         ┌──────────── your VPS (Docker) ────────────┐
   browser ── HTTPS ──►  │  Caddy (TLS)  ──►  api (API + PWA)  ──►  MySQL  │
                         └────────────────────────────────────────────┘
```

Everything is prepared: root **`Dockerfile`** (builds SPA + API into one image), **`docker-compose.yml`** (api + db + caddy), **`deploy/Caddyfile`**, **`.env.example`**, **`fly.toml`** (free alternative), and **GitHub Actions** for CI + auto-deploy on push.

---

## Recommended: a €4–5/mo VPS (predictable, full control, auto-deploy)

Best value and simplest end-to-end. Total cost ≈ the VPS only.

### 0. Get the pieces
- **VPS:** Hetzner **CX22** (~€4.5/mo) or any Ubuntu 22.04+ box with 2 GB RAM. Note its IP.
- **Domain:** a cheap `.xyz`/`.com` (~€1–10/yr at Namecheap/Porkbun/Cloudflare), **or free** — a Cloudflare-proxied subdomain, or DuckDNS (`something.duckdns.org`, free). Point an **A record** for e.g. `homeos.yourdomain.com` → the VPS IP.
- (Optional, free) **AI assistant key** — Groq (`console.groq.com`) or Google Gemini (`aistudio.google.com`). Both have free tiers.
- (Optional) **SMTP** for real email — e.g. Brevo free tier.

### 1. Prepare the server
```bash
ssh root@YOUR_VPS_IP
apt update && apt install -y docker.io docker-compose-plugin git
git clone https://github.com/YOU/HomeOs.git ~/homeos
cd ~/homeos
cp .env.example .env
nano .env          # fill in APP_DOMAIN, DB passwords, (optional) SMTP + assistant key
```

### 2. First launch
```bash
docker compose up -d --build     # builds the image, starts db + api + caddy
docker compose logs -f api       # watch it migrate + seed, then "Now listening"
```
Open `https://homeos.yourdomain.com` — Caddy will have fetched HTTPS automatically. Sign in with the demo (`demo@imel.ba` / `Demo1234!`) or register your household.

### 3. Auto-deploy on every push
Add these repo secrets (GitHub → Settings → Secrets and variables → Actions):

| Secret | Value |
|---|---|
| `VPS_HOST` | your VPS IP/hostname |
| `VPS_USER` | SSH user (e.g. `root`) |
| `VPS_SSH_KEY` | a **private** SSH key whose public half is in the server's `~/.ssh/authorized_keys` |

Now `.github/workflows/deploy.yml` runs on every push to `main`: it SSHes in, `git pull`, `docker compose up -d --build`. `.github/workflows/ci.yml` builds + runs all tests first. **Push → live.**

---

## Free alternative: Fly.io

Free allowance, gives HTTPS + a `*.fly.dev` domain, deploys the root `Dockerfile`.
```bash
curl -L https://fly.io/install.sh | sh
fly launch --no-deploy          # uses fly.toml
# a small MySQL: `fly mysql create` (or an external free MySQL — Aiven free tier)
fly secrets set \
  ConnectionStrings__HomeOsDb="Server=...;Database=homeos;User Id=...;Password=...;SslMode=None;AllowPublicKeyRetrieval=True" \
  Cors__Origins__0="https://homeos.fly.dev" Frontend__BaseUrl="https://homeos.fly.dev" \
  Assistant__Provider="openai" Assistant__ApiKey="gsk_..." Assistant__BaseUrl="https://api.groq.com/openai/v1" Assistant__Model="llama-3.3-70b-versatile"
fly deploy
```
Auto-deploy on push: add `FLY_API_TOKEN` (from `fly tokens create deploy`) as a repo secret and swap the deploy job (see the note in `deploy.yml`).

---

## Configuration reference (environment variables)

.NET maps `Section:Key` → `Section__Key` (double underscore). All optional except the connection string.

| Variable | Purpose |
|---|---|
| `ConnectionStrings__HomeOsDb` | **Required.** MySQL connection string. |
| `Cors__Origins__0` | Allowed browser origin (your app URL). |
| `Frontend__BaseUrl` | Base URL used in email links. |
| `Email__From`, `Email__Smtp__Host/Port/User/Password/UseSsl` | SMTP (omit to disable outgoing mail). |
| `Assistant__Provider` | `openai` (Groq/Gemini/OpenRouter/Ollama) or `anthropic`. |
| `Assistant__ApiKey` / `Assistant__BaseUrl` / `Assistant__Model` | AI assistant (omit to disable; the box shows a "not set up" note). |
| `ASPNETCORE_ENVIRONMENT` | `Production` (HSTS on, Swagger off, demo seed off). Set in the image already. |

With docker-compose these come from `.env`; on Fly from `fly secrets`. **Never commit real secrets** — `.env` is git-ignored; use user-secrets locally.

---

## Security posture in production
- HTTPS + **HSTS** (Caddy/Fly force TLS; the app sends HSTS in Production).
- Security headers on every response (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, split CSP for API vs SPA).
- Rate limiting (per-IP; stricter on `/api/auth`), Identity lockout, httpOnly `SameSite=Lax` auth cookie over HTTPS.
- CORS locked to your origin. Migrations auto-applied; demo data only seeds in Development.

## Post-deploy smoke test
- `https://APP_DOMAIN/api/ping` → 200 JSON.
- `https://APP_DOMAIN/health/ready` → 200 (DB reachable).
- Load the app, register a household, create a task, confirm it persists after `docker compose restart api`.
- (If configured) send a test email + ask the assistant "what's coming up this week?".
