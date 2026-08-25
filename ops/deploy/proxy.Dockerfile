# Edge reverse proxy for the production stack.
#   stage 1 (spa)   : builds the React app with VITE_API_BASE_URL=/api (same-origin)
#   stage 2 (final) : nginx that serves the built SPA AND reverse-proxies /api + /hubs
#                     to backend-api, terminating TLS. It is the only public service.
#
# Build context is the repo root:
#   docker build -f ops/deploy/proxy.Dockerfile -t medicalassistant/proxy:latest .

FROM node:20-alpine AS spa
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
# Same-origin in production: the app calls /api and connects the hub at /hubs/chat,
# both served by this proxy, so no CORS and no hardcoded host. Baked at build time.
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine AS final
# The base image's entrypoint runs envsubst over /etc/nginx/templates/*.template at startup
# (writing to /etc/nginx/conf.d/) whenever the command starts with "nginx" — which the default
# CMD does, so we do NOT override the command here. Only ${DOMAIN} is substituted (the base
# script substitutes all env vars; nginx's own $host/$uri are safe because they aren't env vars).
# The reload loop is added as a standard docker-entrypoint.d hook instead of a command override.
RUN rm -f /etc/nginx/conf.d/default.conf
# Restrict envsubst to DOMAIN only — belt-and-suspenders so nginx runtime vars are never touched.
ENV NGINX_ENVSUBST_FILTER=DOMAIN
COPY --from=spa /app/dist /usr/share/nginx/html
COPY ops/deploy/nginx/templates /etc/nginx/templates
COPY ops/deploy/nginx/docker-entrypoint.d/40-cert-reload.sh /docker-entrypoint.d/40-cert-reload.sh
RUN chmod +x /docker-entrypoint.d/40-cert-reload.sh
EXPOSE 80 443
