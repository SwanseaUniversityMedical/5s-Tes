FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:8.0.416 AS build

WORKDIR /src
COPY ["Agent/Agent.Web/Agent.Web.csproj", "Agent/Agent.Web/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Agent/Agent.Web/Agent.Web.csproj"

COPY ["Agent/Agent.Web/", "Agent/Agent.Web/"]
COPY ["Shared/FiveSafesTes.Core/", "Shared/FiveSafesTes.Core/"]
WORKDIR "/src/Agent/Agent.Web"
RUN dotnet publish "Agent.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained=false \
    /p:UseAppHost=false

FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:8.0 AS final
COPY rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt
RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates

RUN useradd -m -u 1001 -s /bin/bash app
USER app
ENV HOME=/home/app
ENV USER=app

EXPOSE 8080
EXPOSE 443

WORKDIR /app
COPY --chown=app:app --from=build /app/publish .
ENTRYPOINT ["dotnet", "Agent.Web.dll"]
