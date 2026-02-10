#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0.416 AS build
WORKDIR /src
COPY ["Submission/Submission.Web/Submission.Web.csproj", "Submission/Submission.Web/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Submission/Submission.Web/Submission.Web.csproj"
RUN dotnet restore "Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj"

COPY . .
WORKDIR "/src/Submission/Submission.Web"
RUN dotnet build "Submission.Web.csproj"   -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submission.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submission.Web.dll"]

COPY rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt

RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates
