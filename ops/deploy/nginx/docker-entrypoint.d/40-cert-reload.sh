#!/bin/sh
# Reload nginx every 6h so a renewed Let's Encrypt certificate is picked up without a
# redeploy. Runs from the base image's docker-entrypoint.d (before nginx is exec'd), so the
# loop's first iteration sleeps 6h — the master process is well up by then. The base image's
# own 20-envsubst-on-templates.sh has already rendered app.conf from the template by this point.
( while :; do sleep 6h; nginx -s reload 2>/dev/null || true; done ) &
