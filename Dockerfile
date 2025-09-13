# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0.100 AS build
WORKDIR /src

ENV DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_CLI_HOME=/tmp \
    NUGET_XMLDOC_MODE=skip \
    DOTNET_DISABLE_PARALLEL=1 \
    NUGET_PACKAGES=/tmp/nuget

# 1) Sadece csproj önce (cache için)
COPY SwitchlyRedisWorker/SwitchlyRedisWorker.csproj SwitchlyRedisWorker/
RUN dotnet restore SwitchlyRedisWorker/SwitchlyRedisWorker.csproj \
    --disable-parallel --ignore-failed-sources \
    --source https://api.nuget.org/v3/index.json -v minimal

# 2) Tüm repo (lib dahil)
COPY . .

# 2.1) DLL gerçekten geldi mi? (YANLIŞSA BURADA PATLASIN)
RUN ls -la SwitchlyRedisWorker/lib/Switchly.Shared/net9.0/ && \
    test -f SwitchlyRedisWorker/lib/Switchly.Shared/net9.0/Switchly.Shared.dll

# 3) Build & Publish
RUN dotnet build SwitchlyRedisWorker/SwitchlyRedisWorker.csproj -c Release --no-restore \
    -p:RunAnalyzersDuringBuild=false -p:UseSharedCompilation=false -v minimal

RUN dotnet publish SwitchlyRedisWorker/SwitchlyRedisWorker.csproj -c Release --no-restore -o /app \
    -p:PublishReadyToRun=false -p:PublishSingleFile=false -p:UseAppHost=false \
    -p:RunAnalyzersDuringBuild=false -v minimal

# runtime...