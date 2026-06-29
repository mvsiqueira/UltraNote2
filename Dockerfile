# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only csproj first for restore-layer caching (API references Core).
COPY UltraNote.Core/UltraNote.Core.csproj UltraNote.Core/
COPY UltraNote.Api/UltraNote.Api.csproj UltraNote.Api/
RUN dotnet restore UltraNote.Api/UltraNote.Api.csproj

# Copy the rest and publish.
COPY UltraNote.Core/ UltraNote.Core/
COPY UltraNote.Api/ UltraNote.Api/
RUN dotnet publish UltraNote.Api/UltraNote.Api.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Listens on 8080 (internal only — the QNAP cloudflared reaches it on the docker network).
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Database + attachments live on mounted volumes, never inside the image.
ENV ConnectionStrings__Db="Data Source=/data/ultranote.db"
ENV Storage__AssetsPath="/assets"

EXPOSE 8080

# Runs as root by default so it can write to QNAP bind-mounted volumes without
# host-side chown gymnastics. The container is not published to the host — only
# the internal docker network (cloudflared) can reach it. See DEPLOY-QNAP.md.

ENTRYPOINT ["dotnet", "UltraNote.Api.dll"]
