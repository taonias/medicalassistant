#!/usr/bin/env bash
# Force a fresh Let's Encrypt certificate for $DOMAIN (e.g. after changing the domain or
# to recover from a bad/staging cert). Normal deploys do NOT need this — deploy.sh issues
# the certificate automatically on the first run. This deletes the current cert and lets
# the next deploy.sh re-bootstrap it.
set -euo pipefail
cd "$(dirname "$0")"

COMPOSE="docker compose -f docker-compose.prod.yml --env-file .env.prod"
# shellcheck disable=SC1091
set -a; . ./.env.prod; set +a
: "${DOMAIN:?DOMAIN must be set in .env.prod}"

echo "==> Removing existing certificate material for $DOMAIN"
$COMPOSE run --rm --no-deps --entrypoint sh certbot -c "
  rm -rf /etc/letsencrypt/live/$DOMAIN /etc/letsencrypt/archive/$DOMAIN /etc/letsencrypt/renewal/$DOMAIN.conf"

echo "==> Re-running deploy to bootstrap a fresh certificate"
./deploy.sh --no-load
