FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:10.0.300 AS build

WORKDIR /src
COPY ["Credentials/Credentials.Camunda/Credentials.Camunda.csproj", "Credentials/Credentials.Camunda/"]
COPY ["Credentials/Credentials.Models/Credentials.Models.csproj", "Credentials/Credentials.Models/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Credentials/Credentials.Camunda/Credentials.Camunda.csproj"

COPY ["Credentials/", "Credentials/"]
COPY ["Shared/FiveSafesTes.Core/", "Shared/FiveSafesTes.Core/"]
WORKDIR "/src/Credentials/Credentials.Camunda"
RUN dotnet publish "Credentials.Camunda.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained=false \
    /p:UseAppHost=false

FROM --platform=${BUILDPLATFORM} alpine:3.20 AS rc-download
ARG TARGETARCH
ARG RC_VERSION=v0.1.35
RUN apk add --no-cache wget && \
    case "${TARGETARCH}" in \
      amd64) RC_SHA256=f852392837e2b56c4785ea7f4e4a0e3f58a5df19fe317eb80bcc1bfaa41a2893 ;; \
      arm64) RC_SHA256=3d8e125f878f295dedeb40a03a85c311588205601f5076fbc8b341fe40b41b1b ;; \
      *) echo "unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac && \
    wget -O rc.tar.gz "https://github.com/rustfs/cli/releases/download/${RC_VERSION}/rustfs-cli-linux-${TARGETARCH}-${RC_VERSION}.tar.gz" && \
    echo "${RC_SHA256}  rc.tar.gz" | sha256sum -c - && \
    tar -xzf rc.tar.gz rc && \
    mkdir /out && \
    install -m 0755 rc /out/rc && \
    ln -s rc /out/mc

FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:10.0 AS final
COPY --from=rc-download /out/ /usr/local/bin/

USER app
EXPOSE 8080
EXPOSE 8081
WORKDIR /app
COPY --chown=app:app --from=build /app/publish .
ENTRYPOINT ["dotnet", "Credentials.Camunda.dll"]
