FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:8.0.416 AS build

WORKDIR /src
COPY ["Submission/Submission.Api/Submission.Api.csproj", "Submission/Submission.Api/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Submission/Submission.Api/Submission.Api.csproj"

COPY ["Submission/Submission.Api/", "Submission/Submission.Api/"]
COPY ["Shared/FiveSafesTes.Core/", "Shared/FiveSafesTes.Core/"]
WORKDIR "/src/Submission/Submission.Api"
RUN dotnet publish "Submission.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained=false \
    /p:UseAppHost=false

FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:8.0 AS final
USER root

# Install curl for healthchecks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt
RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates

USER app
EXPOSE 8080
EXPOSE 443
WORKDIR /app
COPY --chown=app:app --from=build /app/publish .
ENTRYPOINT ["dotnet", "Submission.Api.dll"]

