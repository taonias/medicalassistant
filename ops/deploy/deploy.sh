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

# Refuse to deploy a release whose manifest doesn't match what .env.prod asks for — catches a
# packaged-for-the-wrong-tag mistake before it reaches the running stack. No jq dependency:
# the manifest format is ours and fixed, so a plain regex extraction is reliable enough.
if [ -f manifest.json ]; then
  manifest_tag=$(grep -o '"tag"[[:space:]]*:[[:space:]]*"[^"]*"' manifest.json | sed -E 's/.*"([^"]*)"$/\1/')
  manifest_prefix=$(grep -o '"imagePrefix"[[:space:]]*:[[:space:]]*"[^"]*"' manifest.json | sed -E 's/.*"([^"]*)"$/\1/')
  expected_tag="${IMAGE_TAG:-latest}"
  expected_prefix="${IMAGE_PREFIX:-medicalassistant}"
  if [ "$manifest_tag" != "$expected_tag" ] || [ "$manifest_prefix" != "$expected_prefix" ]; then
    echo "ERROR: manifest.json (tag=$manifest_tag, imagePrefix=$manifest_prefix) does not match" >&2
    echo "       .env.prod (IMAGE_TAG=$expected_tag, IMAGE_PREFIX=$expected_prefix)." >&2
    echo "       This release bundle doesn't match what .env.prod says to run — refusing to deploy." >&2
    exit 1
  fi
fi

if [ "${1:-}" != "--no-load" ]; then
  echo "==> Loading image tars..."
  for tar in images/*.tar; do
    echo "  - $tar"
    docker load -i "$tar"
  done
fi

echo "==> Pulling base images..."
$COMPOSE pull postgres rabbitmq rabbitmq-provisioner azurite certbot || true

# Does a REAL (Let's Encrypt, not self-signed dummy) certificate exist in the volume?
# The bootstrap writes a temporary self-signed cert so nginx can start; that has
# issuer == subject. A real cert's issuer (Let's Encrypt) differs from its subject, so
# comparing them distinguishes the two and prevents a leftover dummy from blocking issuance.
cert_is_real() {
  $COMPOSE run --rm --no-deps --entrypoint sh certbot -c '
    f=/etc/letsencrypt/live/'"$DOMAIN"'/fullchain.pem
    [ -s "$f" ] || exit 1
    iss=$(openssl x509 -in "$f" -noout -issuer | sed "s/^issuer=//")
    sub=$(openssl x509 -in "$f" -noout -subject | sed "s/^subject=//")
    [ "$iss" != "$sub" ]
  ' >/dev/null 2>&1
}

if cert_is_real; then
  echo "==> Certificate for $DOMAIN present; starting the stack..."
  $COMPOSE up -d --remove-orphans
else
  echo "==> No real certificate yet (missing or self-signed dummy); bootstrapping Let's Encrypt for $DOMAIN"

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
