FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

ENV ASPNETCORE_ENVIRONMENT=Development

WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0.416 AS build
WORKDIR /src
COPY ["Agent/Agent.Api/Agent.Api.csproj", "Agent/Agent.Api/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Agent/Agent.Api/Agent.Api.csproj"
RUN dotnet restore "Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj"

COPY . .
WORKDIR "/src/Agent/Agent.Api"
RUN dotnet build "Agent.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Agent.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Agent.Api.dll"]

COPY rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt

RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates
