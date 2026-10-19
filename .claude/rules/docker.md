# Docker

## Docker is the main way to develop

The API, the React client, SQL Server, and the observability dashboard run together with Docker Compose. `dotnet build` and `dotnet test` still run directly on your machine (.NET 10 SDK), because that's the fastest feedback loop for TDD.
**Why:** Everyone gets the same SQL Server version and setup with no local install. The way it runs locally matches the container that runs in Azure.

**Runs without asking**, and only with our compose file:

```bash
docker compose -f docker/docker-compose.yml up -d --build   # start or rebuild the stack in the background
docker compose -f docker/docker-compose.yml ps              # what's running
docker compose -f docker/docker-compose.yml logs api        # logs of one service
docker compose -f docker/docker-compose.yml down            # stop the stack (data is kept)
```

**Asks you:** any other Docker command (`docker build`, `docker run`, `exec`, other compose files).
**Blocked:** `down -v`, `docker volume rm`, `docker rm`/`rmi`, and `prune`, because they delete data such as your local database.

## Folder layout

```
docker/
  docker-compose.yml            All services wired together for local development
  api/
    Dockerfile                  Builds and runs Jobly.Api
  client/
    Dockerfile                  Builds the React app and serves it
    nginx.conf                  Web server configuration for the client
  sqlserver/
    (init scripts, if needed)   SQL Server setup for local development
  observability/
    (dashboard config)          Local dashboard for logs and traces (see logging.md)
.dockerignore                   At the repository root, because the build context is the root
```

Build an image from the repository root:

```bash
docker build -f docker/api/Dockerfile .
docker compose -f docker/docker-compose.yml up
```

## Rules

### One folder per service, each with its own Dockerfile and config.
**Why:** Everything needed to build and run a service sits together. Adding a service means adding a folder, without growing one huge file.

### The build context is the repository root, with a `.dockerignore`.
**Why:** The API image needs `src/` and the solution file, and the client image needs `client/`. The `.dockerignore` keeps `bin/`, `obj/`, `node_modules/`, `.git/`, and secrets out of the image, which keeps builds fast and images small and safe.

### Publish ports on `127.0.0.1` only (`"127.0.0.1:48217:8080"`), with unusual host ports set in `docker/.env`.
**Why:**
- Without `127.0.0.1`, Docker listens on every network interface, so other devices on the same network (office or café Wi-Fi) could reach SQL Server and the API.
- It also avoids a Docker Desktop IPv6 issue on Windows where `http://localhost` fails to load.
- Unusual ports (48217, 48233) avoid clashes with other software, and `.env` makes them easy to change.

### Use multi-stage builds.
Build with the SDK image, then copy the output into a small runtime image.
**Why:** The final image contains only what is needed to run. It's smaller, starts faster, and has less attack surface (no compiler, no source code).

### Run as a non-root user.
**Why:** If the app is ever compromised, the attacker doesn't get root inside the container.

### Pin image versions (for example `mcr.microsoft.com/dotnet/aspnet:10.0`), never `latest`.
**Why:** Builds are reproducible. The same Dockerfile produces the same image today and next month, and upgrades are deliberate.

### No secrets in Dockerfiles or images. Pass them as environment variables at runtime.
**Why:** Anyone with the image can read everything baked into it. The local SQL Server password goes in an untracked `.env` file read by compose, which is never committed.

### The same image runs locally, in GitHub Actions, and in Azure App Service.
**Why:** "Works on my machine" problems disappear when every environment runs the identical image and only configuration differs.
