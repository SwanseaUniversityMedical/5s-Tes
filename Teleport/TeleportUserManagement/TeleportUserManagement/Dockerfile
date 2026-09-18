FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:10.0.300 AS build

WORKDIR /src
COPY ["Teleport/TeleportUserManagement/TeleportUserManagement/TeleportUserManagement.csproj", "Teleport/TeleportUserManagement/TeleportUserManagement/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Teleport/TeleportUserManagement/TeleportUserManagement/TeleportUserManagement.csproj"

COPY ["Teleport/TeleportUserManagement/TeleportUserManagement/", "Teleport/TeleportUserManagement/TeleportUserManagement/"]
COPY ["Shared/FiveSafesTes.Core/", "Shared/FiveSafesTes.Core/"]
WORKDIR "/src/Teleport/TeleportUserManagement/TeleportUserManagement"
RUN dotnet publish "TeleportUserManagement.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained=false \
    /p:UseAppHost=false

FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root

# The AD's LDAPS certificate chains to this CA.
COPY rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt
RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates

USER app
EXPOSE 8080
WORKDIR /app
COPY --chown=app:app --from=build /app/publish .
ENTRYPOINT ["dotnet", "TeleportUserManagement.dll"]
