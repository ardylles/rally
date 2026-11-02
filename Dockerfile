# syntax=docker/dockerfile:1
FROM node:24-bookworm-slim AS web-build
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.303 AS api-build
WORKDIR /src
COPY global.json NuGet.Config ./
COPY server/Rally.Api/Rally.Api.csproj server/Rally.Api/
RUN dotnet restore server/Rally.Api/Rally.Api.csproj
COPY server/Rally.Api/ server/Rally.Api/
COPY --from=web-build /src/server/Rally.Api/wwwroot/ server/Rally.Api/wwwroot/
RUN dotnet publish server/Rally.Api/Rally.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/var/lib/rally/keys
# A named volume preserves sign-in keys between container replacements.
RUN mkdir -p /var/lib/rally/keys && chown -R app:app /var/lib/rally
COPY --from=api-build /out/ ./
USER app
EXPOSE 8080
# Render sets PORT; Docker Compose uses the default 8080.
CMD ["sh", "-c", "exec dotnet Rally.Api.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
