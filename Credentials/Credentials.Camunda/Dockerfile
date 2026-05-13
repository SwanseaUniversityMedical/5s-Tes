FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:8.0.416 AS build

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

FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:8.0 AS final
USER root
RUN apt-get update && apt-get install -y wget curl && \
    wget https://dl.min.io/client/mc/release/linux-amd64/mc && \
    chmod +x mc && \
    mv mc /usr/local/bin/mc && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

RUN useradd -m -u 1001 -s /bin/bash app
USER app
ENV HOME=/home/app
ENV USER=app

EXPOSE 8080
EXPOSE 8081

WORKDIR /app
COPY --chown=app:app --from=build /app/publish .
ENTRYPOINT ["dotnet", "Credentials.Camunda.dll"]
