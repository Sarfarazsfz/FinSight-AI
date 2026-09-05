# ============================================================
# FinSight API — multi-stage Docker build
#
# Build context:  backend/
# Railway root:   backend
# Port:           8080
#
# All secrets (JWT, DB, AI keys, CORS) must be supplied as
# runtime environment variables — nothing sensitive is baked
# into this image.
# ============================================================

# ── Stage 1: build ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files first so the package-restore layer is
# cached independently of source changes.
COPY FinSight.Api/FinSight.Api.csproj                         FinSight.Api/
COPY FinSight.Application/FinSight.Application.csproj         FinSight.Application/
COPY FinSight.Domain/FinSight.Domain.csproj                   FinSight.Domain/
COPY FinSight.Infrastructure/FinSight.Infrastructure.csproj   FinSight.Infrastructure/

# Restore all packages before copying source (improves cache hit rate).
RUN dotnet restore FinSight.Api/FinSight.Api.csproj

# Copy the rest of the source (excludes whatever .dockerignore omits).
# FinSight.DataGenerator and FinSight.Tests are not referenced by
# FinSight.Api and are excluded via .dockerignore.
COPY FinSight.Api/           FinSight.Api/
COPY FinSight.Application/   FinSight.Application/
COPY FinSight.Domain/        FinSight.Domain/
COPY FinSight.Infrastructure/ FinSight.Infrastructure/

# Publish release build.
RUN dotnet publish FinSight.Api/FinSight.Api.csproj \
      -c Release \
      --no-restore \
      -o /app/publish

# ── Stage 2: runtime ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Copy only the published output — no SDK, no source, no tests.
COPY --from=build /app/publish .

# Bind to all interfaces on port 8080.
# All secrets must be supplied as environment variables at runtime.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "FinSight.Api.dll"]
