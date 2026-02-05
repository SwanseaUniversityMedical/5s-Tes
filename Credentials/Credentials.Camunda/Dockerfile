#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

RUN apt-get update && apt-get install -y wget curl && \
    wget https://dl.min.io/client/mc/release/linux-amd64/mc && \
    chmod +x mc && \
    mv mc /usr/local/bin/mc && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

USER app

FROM mcr.microsoft.com/dotnet/sdk:8.0.416 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Credentials/Credentials.Camunda/Credentials.Camunda.csproj", "Credentials/Credentials.Camunda/"]
RUN dotnet restore "Credentials/Credentials.Camunda/Credentials.Camunda.csproj"
COPY . .
WORKDIR "/src/Credentials/Credentials.Camunda"
RUN dotnet build "Credentials.Camunda.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Credentials.Camunda.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Credentials.Camunda.dll"]
