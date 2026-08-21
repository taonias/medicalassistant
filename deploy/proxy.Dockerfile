# Edge reverse proxy for the production stack.
#   stage 1 (spa)   : builds the React app with VITE_API_BASE_URL=/api (same-origin)
#   stage 2 (final) : nginx that serves the built SPA AND reverse-proxies /api + /hubs
#                     to backend-api, terminating TLS. It is the only public service.
#
# Build context is the repo root:
#   docker build -f deploy/proxy.Dockerfile -t medicalassistant/proxy:latest .

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
# The base image runs envsubst over /etc/nginx/templates/*.template at startup,
# writing the result to /etc/nginx/conf.d/. We inject ${DOMAIN} that way.
RUN rm -f /etc/nginx/conf.d/default.conf
COPY --from=spa /app/dist /usr/share/nginx/html
COPY deploy/nginx/templates /etc/nginx/templates
EXPOSE 80 443
