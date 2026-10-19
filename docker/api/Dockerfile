# Builds and runs Jobly.Api. Build from the repository root:
#   docker build -f docker/api/Dockerfile .
# (docker compose does this for you: docker compose -f docker/docker-compose.yml up -d --build)

# ---- Stage 1: build -------------------------------------------------------------------------
# The SDK image has the compiler. It is only used to build and never shipped.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first and restore packages. Docker caches this layer, so packages are
# downloaded again only when a .csproj or the package versions change, not on every code change.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Jobly.Domain/Jobly.Domain.csproj src/Jobly.Domain/
COPY src/Jobly.Application/Jobly.Application.csproj src/Jobly.Application/
COPY src/Jobly.Infrastructure/Jobly.Infrastructure.csproj src/Jobly.Infrastructure/
COPY src/Jobly.Api/Jobly.Api.csproj src/Jobly.Api/
RUN dotnet restore src/Jobly.Api/Jobly.Api.csproj

# Now copy the source code and publish a Release build.
COPY src/ src/
RUN dotnet publish src/Jobly.Api/Jobly.Api.csproj --configuration Release --output /app/publish --no-restore

# ---- Stage 2: run ---------------------------------------------------------------------------
# The ASP.NET runtime image is much smaller and has no compiler or source code: less to attack.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The image's built-in non-root user, so a compromised app does not get root inside the container.
USER $APP_UID

# .NET images listen on port 8080 by default. docker-compose.yml maps it to an unusual port on your machine.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Jobly.Api.dll"]
