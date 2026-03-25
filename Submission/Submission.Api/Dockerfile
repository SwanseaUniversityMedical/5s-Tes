FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

ENV ASPNETCORE_ENVIRONMENT=Development

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0.416 AS build
WORKDIR /src
COPY ["Submission/Submission.Api/Submission.Api.csproj", "Submission/Submission.Api/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Submission/Submission.Api/Submission.Api.csproj"
RUN dotnet restore "Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj"

COPY . .

WORKDIR "/src/Submission/Submission.Api"
RUN dotnet build "Submission.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submission.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submission.Api.dll"]

COPY rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt

RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates
