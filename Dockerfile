# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0.100 AS build
WORKDIR /src

# Daha stabil ve düşük RAM için env'ler
ENV DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_CLI_HOME=/tmp \
    NUGET_XMLDOC_MODE=skip \
    DOTNET_DISABLE_PARALLEL=1 \
    NUGET_PACKAGES=/tmp/nuget


# 1) Restore (sadece Worker projesi)
COPY SwitchlyRedisWorker/SwitchlyRedisWorker.csproj SwitchlyRedisWorker/
COPY lib/ lib/

RUN dotnet restore SwitchlyRedisWorker/SwitchlyRedisWorker.csproj \
    --disable-parallel --ignore-failed-sources \
    --source https://api.nuget.org/v3/index.json -v minimal 

COPY . .

# (3) Build & Publish
RUN dotnet build SwitchlyRedisWorker/SwitchlyRedisWorker.csproj -c Release --no-restore \
    -p:RunAnalyzersDuringBuild=false -p:UseSharedCompilation=false -v minimal

RUN dotnet publish SwitchlyRedisWorker/SwitchlyRedisWorker.csproj -c Release --no-restore -o /app \
    -p:PublishReadyToRun=false -p:PublishSingleFile=false -p:UseAppHost=false \
    -p:RunAnalyzersDuringBuild=false -v minimal

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
# Worker HTTP sunmaz; port açmaya gerek yok
ENTRYPOINT ["dotnet","SwitchlyRedisWorker.dll"]
