# Production deployment (single Ubuntu VM, docker compose, nginx + Let's Encrypt)

This deploys the whole stack to one Ubuntu VM behind an nginx reverse proxy that terminates
TLS for your domain. Images are built on your machine, saved to `.tar`, uploaded with
FileZilla (SFTP), and loaded + started on the VM — no registry, no build tools on the server.

```
Internet ──443──▶ proxy (nginx)  ── / ───────▶ SPA (baked into the proxy image)
                    │             ── /api ────▶ backend-api:8080
                    │             ── /hubs ───▶ backend-api:8080  (SignalR WebSocket)
                    └── internal docker network ──▶ clinical-knowledge, postgres(+pgvector),
                                                    rabbitmq, azurite, transcription-worker
```
Only the proxy publishes ports (80/443). Everything else is internal to the compose network.

---

## 0. One-time VM preparation

On the VM (Ubuntu, as root or with sudo):

```bash
# Docker Engine + compose plugin
curl -fsSL https://get.docker.com | sh
# Firewall: allow SSH + web
ufw allow 22 && ufw allow 80 && ufw allow 443 && ufw --force enable
# A place for the release
mkdir -p /opt/medicalassistant
```

**DNS:** point your domain's **A record** at the VM's public IP. `dig +short your.domain`
must return the VM IP before you request a certificate, or Let's Encrypt validation fails.

---

## 1. Build + package the release (on your machine)

Requires Docker Desktop running.

```bash
./deploy/package-release.ps1                 # tag "latest"
# or a versioned build:
./deploy/package-release.ps1 -Tag 2026-08-20
```

This builds the 5 app images (backend-api, backend-migrations, clinical-knowledge,
transcription-worker, proxy), `docker save`s them into `release/images/*.tar`, and copies
`docker-compose.prod.yml`, `db-init/`, `rabbitmq/`, the deploy scripts, and
`.env.prod.example` into `release/`.

---

## 2. Upload with FileZilla

Connect: **SFTP**, host = VM IP, port **22**, your VM user. Upload the **entire `release/`
folder** into `/opt/medicalassistant` on the VM. You should end up with
`/opt/medicalassistant/release/` containing `images/`, `docker-compose.prod.yml`, `db-init/`,
`rabbitmq/`, `deploy.sh`, `init-letsencrypt.sh`, `.env.prod.example`.

> Re-deploys only need to re-upload `release/images/` (and the compose file if it changed) —
> the config folders rarely change.

---

## 3. Configure secrets (on the VM, first time only)

```bash
cd /opt/medicalassistant/release
cp .env.prod.example .env.prod
nano .env.prod          # set DOMAIN, LETSENCRYPT_EMAIL, all passwords, OPENAI_API_KEY, etc.
chmod +x deploy.sh init-letsencrypt.sh
```

Generate strong secrets, e.g. `openssl rand -base64 36` for each password and `JWT_KEY`.

---

## 4. Deploy

```bash
./deploy.sh
```

On the **first** run this automatically bootstraps TLS (temporary self-signed cert → bring the
stack up → obtain the real Let's Encrypt cert over the `:80` ACME challenge → reload nginx),
then prints `App: https://your.domain`. On every later run it just loads the new images and
recreates changed containers.

Migrations apply themselves: `backend-migrations` runs once against `MedicalAssistantDb`, and
`clinical-knowledge` migrates `ai_med` on startup.

---

## 5. Updating a running deployment

```bash
# on your machine
./deploy/package-release.ps1 -Tag 2026-08-21
# FileZilla: upload release/images/ (and docker-compose.prod.yml if changed), set IMAGE_TAG in .env.prod
# on the VM
cd /opt/medicalassistant/release && ./deploy.sh
```

Compose recreates only the services whose image changed; postgres/rabbitmq/azurite and their
data volumes are untouched.

---

## Operations

- **Logs:** `docker compose -f docker-compose.prod.yml --env-file .env.prod logs -f backend-api`
- **Status:** `docker compose -f docker-compose.prod.yml --env-file .env.prod ps`
- **Stop:** `... down` (add `-v` ONLY if you intend to delete data volumes — destructive).
- **Cert renewal:** the `certbot` service auto-renews and the proxy reloads every 6h; nothing to do.
- **Force a new cert** (domain change / bad cert): `./init-letsencrypt.sh`.
- **Backups:** back up the `pgdata` volume (the databases) and, if you keep blobs local, the
  `azurite_data` volume:
  `docker run --rm -v medicalassistant-prod_pgdata:/v -v $PWD:/b alpine tar czf /b/pgdata-$(date +%F).tgz -C /v .`

## Notes / gotchas

- **Same-origin:** the SPA is built with `VITE_API_BASE_URL=/api`, so the browser calls
  `https://your.domain/api` and the hub at `https://your.domain/hubs/chat` — no CORS. If you
  ever serve the API on a different host, rebuild the proxy image with a different
  `VITE_API_BASE_URL` build arg (it is baked at build time).
- **Uploads:** nginx allows up to `client_max_body_size 512m` for consultation audio/PDF.
- **Blob storage:** Azurite runs in-container with a volume (fine for one VM). For durability
  across rebuilds/hosts, set `AZURITE_CONNECTION_STRING` to a real Azure Storage account.
- **Secrets:** `.env.prod` lives only on the VM — never commit it.
