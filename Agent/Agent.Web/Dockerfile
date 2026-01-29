#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

ENV ASPNETCORE_ENVIRONMENT=Development

WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0.416 AS build
WORKDIR /src
COPY ["Agent/Agent.Web/Agent.Web.csproj", "Agent/Agent.Web/"]
COPY ["Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj", "Shared/FiveSafesTes.Core/"]
RUN dotnet restore "Agent/Agent.Web/Agent.Web.csproj"
RUN dotnet restore "Shared/FiveSafesTes.Core/FiveSafesTes.Core.csproj"

COPY . .
WORKDIR "/src/Agent.Web"
RUN dotnet build "Agent.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Agent.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Agent.Web.dll"]

COPY Agent/rootca.pem /usr/local/share/ca-certificates/chi-ansible.crt

RUN chmod 644 /usr/local/share/ca-certificates/*.crt && \
	update-ca-certificates
