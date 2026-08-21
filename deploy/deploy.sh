#!/usr/bin/env bash
# Run ON THE VM, from inside the uploaded release/ folder.
# Loads the app image tars and (re)starts the production stack. On the very first run it
# also bootstraps the Let's Encrypt certificate (dummy cert -> up -> real cert -> reload),
# so the same single command works for the first deploy and every deploy after.
#
# Base images (postgres/rabbitmq/azurite/certbot) are pulled automatically by compose.
#
# Usage:
#   ./deploy.sh            # load image tars, then up (bootstraps TLS on first run)
#   ./deploy.sh --no-load  # skip loading tars (images already present), then up
set -euo pipefail
cd "$(dirname "$0")"

COMPOSE="docker compose -f docker-compose.prod.yml --env-file .env.prod"

if [ ! -f .env.prod ]; then
  echo "ERROR: .env.prod not found. Copy .env.prod.example to .env.prod and fill it in." >&2
  exit 1
fi

# shellcheck disable=SC1091
set -a; . ./.env.prod; set +a
: "${DOMAIN:?DOMAIN must be set in .env.prod}"
: "${LETSENCRYPT_EMAIL:?LETSENCRYPT_EMAIL must be set in .env.prod}"

if [ "${1:-}" != "--no-load" ]; then
  echo "==> Loading image tars..."
  for tar in images/*.tar; do
    echo "  - $tar"
    docker load -i "$tar"
  done
fi

echo "==> Pulling base images..."
$COMPOSE pull postgres rabbitmq rabbitmq-provisioner azurite certbot || true

# Does a real certificate already exist in the letsencrypt volume?
cert_exists() {
  $COMPOSE run --rm --no-deps --entrypoint sh certbot \
    -c "test -s /etc/letsencrypt/live/$DOMAIN/fullchain.pem" >/dev/null 2>&1
}

if cert_exists; then
  echo "==> Certificate for $DOMAIN present; starting the stack..."
  $COMPOSE up -d --remove-orphans
else
  echo "==> No certificate yet; bootstrapping Let's Encrypt for $DOMAIN"

  echo "  - writing a temporary self-signed cert so nginx can start on 443"
  $COMPOSE run --rm --no-deps --entrypoint sh certbot -c "
    mkdir -p /etc/letsencrypt/live/$DOMAIN &&
    openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
      -keyout /etc/letsencrypt/live/$DOMAIN/privkey.pem \
      -out    /etc/letsencrypt/live/$DOMAIN/fullchain.pem \
      -subj '/CN=$DOMAIN'"

  echo "  - starting the stack (proxy serves :80 for the ACME challenge)"
  $COMPOSE up -d --remove-orphans

  echo "  - requesting the real certificate from Let's Encrypt"
  $COMPOSE run --rm --no-deps --entrypoint sh certbot -c "
    rm -rf /etc/letsencrypt/live/$DOMAIN /etc/letsencrypt/archive/$DOMAIN /etc/letsencrypt/renewal/$DOMAIN.conf &&
    certbot certonly --webroot -w /var/www/certbot \
      -d $DOMAIN --email $LETSENCRYPT_EMAIL --agree-tos --no-eff-email --non-interactive"

  echo "  - reloading nginx with the real certificate"
  $COMPOSE exec proxy nginx -s reload
fi

echo "==> Current state:"
$COMPOSE ps
echo ""
echo "Done. App: https://$DOMAIN"
