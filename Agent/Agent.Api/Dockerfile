FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:8.0.416 AS build
WORKDIR /src
COPY ["Agent/Agent.Api/Agent.Api.csproj", "Agent/Agent.Api/"]
COPY ["Credentials/Credentials.Models/Credentials.Models.csproj", "Credentials/Credentials.Models/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Agent/Agent.Api/Agent.Api.csproj"

COPY ["Agent/Agent.Api/", "Agent/Agent.Api/"]
COPY ["Credentials/Credentials.Models/", "Credentials/Credentials.Models/"]
COPY ["Shared/FiveSafesTes.Core/", "Shared/FiveSafesTes.Core/"]
WORKDIR "/src/Agent/Agent.Api"
RUN dotnet publish "Agent.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained=false \
    /p:UseAppHost=false

FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:8.0 AS final
USER root
COPY rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt
RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates

USER app
EXPOSE 8080
EXPOSE 443
WORKDIR /app
COPY --chown=app:app --from=build /app/publish .
ENTRYPOINT ["dotnet", "Agent.Api.dll"]
