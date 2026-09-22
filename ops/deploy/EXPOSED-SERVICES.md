# Exposed services (remote access)

Everything the production VM publishes on a port other than 80/443, why it's exposed, and how
to actually reach it. `docker-compose.prod.yml` and `ufw` (see `DEPLOYMENT.md` step 0) both have
to agree a port is open — this doc assumes both are already configured as described here; if a
service in this list stops responding, check `ufw status` on the VM before anything else.

Every `<UPPER_SNAKE_CASE>` token below is filled in live from your local `.env` when the
Exposed Services tab renders it — what you see there is the real value, not a placeholder. In
this file on disk (and in git) they stay as tokens, since `.env` itself is gitignored and never
committed.

**Quick reference:**
- Backend API Swagger — port `8080` — `http://<VM_HOST>:8080/swagger` — Bearer token (see below)
- Clinical Knowledge Swagger — port `8081` — `http://<VM_HOST>:8081/swagger` — Bearer / API key (see below)
- PostgreSQL — port `5432` — `<VM_HOST>:5432` — `medicalassistant` / `<POSTGRES_APP_PASSWORD>`
- RabbitMQ management UI — port `15672` — `http://<VM_HOST>:15672` — `<RABBITMQ_BOOTSTRAP_USER>` / `<RABBITMQ_BOOTSTRAP_PASSWORD>`
- Azurite blob (remote) — port `10000` — `http://<VM_HOST>:10000/<AZURITE_ACCOUNT_NAME>` — `<AZURITE_ACCOUNT_NAME>` / `<AZURITE_ACCOUNT_KEY>`

---

## Backend API Swagger — `:8080`

`http://<VM_HOST>:8080/swagger`

Swagger UI itself needs no login. Calling an endpoint through it does:
1. `POST /api/auth/login` (or whichever auth endpoint is current — check the Swagger doc itself)
   with a seeded doctor's credentials (see `SEED_DOCTOR_*` in `.env.prod`).
2. Copy the returned JWT.
3. Click **Authorize** in Swagger UI, enter `Bearer <token>`.

## Clinical Knowledge Swagger — `:8081`

`http://<VM_HOST>:8081/swagger`

Same idea as backend: Swagger UI loads without auth, calling endpoints needs
`<CLINICAL_KNOWLEDGE_API_KEY>` (or `<CLINICAL_KNOWLEDGE_ADMIN_API_KEY>` for admin-only routes),
passed the way that service's auth middleware expects (check its Swagger doc for the header
name). Hitting the bare root `/` without a token returns `401` — that's expected and not a sign
anything is broken; `/swagger` itself is what's unauthenticated.

## PostgreSQL — `:5432`

Connect with `psql`, pgAdmin, DBeaver, etc.:

```
Host:     <VM_HOST>
Port:     5432
User:     medicalassistant
Password: <POSTGRES_APP_PASSWORD>
Database: MedicalAssistantDb   (backend)
       or ai_med               (clinical-knowledge, pgvector)
```

Both databases live in the same Postgres container/instance — only the database name changes.

## RabbitMQ management UI — `:15672`

`http://<VM_HOST>:15672`, login with `<RABBITMQ_BOOTSTRAP_USER>` / `<RABBITMQ_BOOTSTRAP_PASSWORD>`.
This is the bootstrap/admin account; the backend, worker, and clinical-knowledge each connect
with their own scoped users instead — `<RABBITMQ_BACKEND_PASSWORD>` / `<RABBITMQ_WORKER_PASSWORD>`
/ `<RABBITMQ_CLINICAL_PASSWORD>` — which aren't meant for logging into the management UI, only
for reference if you ever need to debug a specific consumer's connection.

## Azurite blob storage — `:10000`

For remote inspection with Azure Storage Explorer or `az storage`, use this connection string —
same account/key as the containers use internally, but with the blob endpoint rewritten to the
VM's public address instead of the internal `azurite` hostname (that hostname only resolves
inside the Docker network):

```
DefaultEndpointsProtocol=http;AccountName=<AZURITE_ACCOUNT_NAME>;AccountKey=<AZURITE_ACCOUNT_KEY>;BlobEndpoint=http://<VM_HOST>:10000/<AZURITE_ACCOUNT_NAME>;
```

---

## Common gotchas

- Use plain `http://`, not `https://`, for these ports. Only nginx (`:443`) terminates TLS — `https://<VM_HOST>:8080/...` will fail since there's no TLS listener there to answer it.
- **Your own network may block it, not the server.** Some corporate/hotel/ISP networks only allow outbound 80/443/22. If a port that works fine for someone else (or from `curl` run directly on the VM) times out for you specifically, try a different network or a VPN before assuming the server is misconfigured.
- `ufw` has to explicitly allow each port, in addition to `docker-compose.prod.yml` publishing it — `docker ps` showing `0.0.0.0:PORT->PORT/tcp` is necessary but not sufficient. Check with `sudo ufw status verbose` on the VM.
- **Some hosting providers have a second, separate firewall** (a cloud/network security group) in front of the VM, independent of `ufw`. If `ufw` allows a port and the app answers on `localhost` from inside the VM, but it's still unreachable from the internet, check your provider's dashboard for a network-level firewall or security group rule blocking it.

## Security note

Every port in this table is currently open to **any** source IP (`ufw ... ALLOW IN Anywhere`). That's convenient for remote debugging on a single-VM setup but means these are real attack surface — especially Postgres and RabbitMQ, which hold or move real patient data. If this deployment moves past short-term development access, prefer scoping each rule to known IPs (`ufw allow from <your IP> to any port <port>`) or drop these ports and tunnel over SSH instead (`ssh -L 5432:localhost:5432 <user>@<VM_HOST>`).

