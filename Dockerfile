# syntax=docker/dockerfile:1

# =============================================================================================
# One Dockerfile, two images.
#
# The API and the Razor frontend are separate processes that scale and fail separately, so they
# are separate images — but they share a solution, a props file and a NuGet feed, and keeping
# two Dockerfiles in step by hand is how the two quietly end up on different base images.
#
#   docker build --target webapi -t pms-webapi .
#   docker build --target web    -t pms-web    .
#
# BuildKit only executes the stages a target actually needs, so building `webapi` never
# publishes the frontend and vice versa. docker-compose.yml passes `target:` for each service.
#
# WHY THE FRONTEND RESTORES ALMOST NOTHING
# ----------------------------------------
# PMS.Web has no project references at all — it talks to the API over HTTP and holds no model
# types in common. That is a deliberate property of the architecture and it pays off here: its
# restore layer is one .csproj, so an edit anywhere in Core or Infrastructure does not
# invalidate the frontend's dependency cache.
# =============================================================================================


# ── Base runtime ────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime

# curl is here for HEALTHCHECK and nothing else. The aspnet image ships neither curl nor wget,
# and a container that cannot answer "are you up?" is one Compose will report as running while
# it 500s.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Kestrel binds this; nothing here terminates TLS. See docker-compose.yml for why the stack
# runs plain HTTP and what a real deployment would put in front of it.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# No useradd here: the .NET 8 runtime images already ship a non-root `app` user (uid 1654), and
# creating one is an error rather than a no-op. The stages below only have to select it.

EXPOSE 8080


# ── Restore ─────────────────────────────────────────────────────────────────────────────────
#
# Project files only, before the source. An edit to a .cs file then reuses the restore layer;
# copying everything first would re-download the package graph on every code change.
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS restore
WORKDIR /src

# Central package management and the feed list govern every project below, so they come first.
COPY Directory.Build.props Directory.Packages.props NuGet.config ./

COPY src/Core/PMS.SharedKernel/PMS.SharedKernel.csproj           src/Core/PMS.SharedKernel/
COPY src/Core/PMS.Domain/PMS.Domain.csproj                       src/Core/PMS.Domain/
COPY src/Core/PMS.Application/PMS.Application.csproj             src/Core/PMS.Application/
COPY src/Infrastructure/PMS.Infrastructure/PMS.Infrastructure.csproj \
     src/Infrastructure/PMS.Infrastructure/
COPY src/Infrastructure/PMS.Persistence/PMS.Persistence.csproj   src/Infrastructure/PMS.Persistence/
COPY src/Aspire/PMS.ServiceDefaults/PMS.ServiceDefaults.csproj   src/Aspire/PMS.ServiceDefaults/
COPY src/Presentation/PMS.WebApi/PMS.WebApi.csproj               src/Presentation/PMS.WebApi/
COPY src/Presentation/PMS.Web/PMS.Web.csproj                     src/Presentation/PMS.Web/

# Restored per entry point rather than across the solution: the test and tools projects are not
# in either image, and restoring them here would put their packages in every layer below.
RUN dotnet restore src/Presentation/PMS.WebApi/PMS.WebApi.csproj \
    && dotnet restore src/Presentation/PMS.Web/PMS.Web.csproj


# ── Source ──────────────────────────────────────────────────────────────────────────────────
FROM restore AS source
WORKDIR /src
COPY src/ src/


# ── Publish ─────────────────────────────────────────────────────────────────────────────────
#
# Two stages rather than one that publishes both, so `--target web` does not compile the whole
# Core and Infrastructure graph the frontend never references.
#
# --no-restore because the layer above did it. UseAppHost=false drops the native launcher: the
# entrypoint is `dotnet X.dll`, and the apphost is several megabytes of nothing.
FROM source AS publish-webapi
RUN dotnet publish src/Presentation/PMS.WebApi/PMS.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /publish \
    -p:UseAppHost=false

FROM source AS publish-web
RUN dotnet publish src/Presentation/PMS.Web/PMS.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /publish \
    -p:UseAppHost=false


# ── The API ─────────────────────────────────────────────────────────────────────────────────
FROM runtime AS webapi
WORKDIR /app
COPY --from=publish-webapi --chown=app:app /publish ./

# FileStorage:BasePath is relative, so uploads land here. Created owned by app because the
# process cannot create it under a root-owned /app.
RUN mkdir -p /app/uploads && chown app:app /app/uploads
USER app

# /health/live is the liveness probe the API already exposes — it asserts the process answers
# and nothing else. /health would drag the database into the container's own health, which
# would make the API report unhealthy for something that is not its fault.
HEALTHCHECK --interval=15s --timeout=3s --start-period=40s --retries=5 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "PMS.WebApi.dll"]


# ── The Razor frontend ──────────────────────────────────────────────────────────────────────
FROM runtime AS web
WORKDIR /app
COPY --from=publish-web --chown=app:app /publish ./
USER app

# The frontend has no health endpoint, so this uses the sign-in page: it is anonymous, it
# renders the layout, and a 200 from it means Razor compiled and the static files resolved.
# It deliberately does NOT depend on the API being up — the frontend degrades rather than
# failing when the API is unreachable, and a healthcheck that said otherwise would restart a
# container that is behaving correctly.
HEALTHCHECK --interval=15s --timeout=3s --start-period=40s --retries=5 \
    CMD curl --fail --silent http://127.0.0.1:8080/login || exit 1

ENTRYPOINT ["dotnet", "PMS.Web.dll"]
