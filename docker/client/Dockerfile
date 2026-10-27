# Builds the React client and serves it with nginx. Build from the repository root:
#   docker build -f docker/client/Dockerfile .
# (docker compose does this for you: docker compose -f docker/docker-compose.yml up -d --build)

# ---- Stage 1: build -------------------------------------------------------------------------
# The Node image has npm and the compiler tools. It is only used to build and never shipped.
FROM node:24-alpine AS build
WORKDIR /app

# Install packages first. Docker caches this layer, so packages are downloaded again only when
# package.json or package-lock.json change, not on every code change. `npm ci` installs exactly what the lock file says.
COPY client/package.json client/package-lock.json ./
RUN npm ci

COPY client/ ./
RUN npm run build

# ---- Stage 2: run ---------------------------------------------------------------------------
# nginx-unprivileged runs as a non-root user and listens on port 8080, so the container needs no extra rights.
FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
COPY docker/client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
