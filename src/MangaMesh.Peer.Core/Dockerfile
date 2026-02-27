# Base stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["mangamesh-peer/src/MangaMesh.Peer.Core/MangaMesh.Peer.Core.csproj", "mangamesh-peer/src/MangaMesh.Peer.Core/"]
COPY ["mangamesh-peer/src/MangaMesh.Shared/MangaMesh.Shared.csproj", "mangamesh-peer/src/MangaMesh.Shared/"]

RUN dotnet restore "mangamesh-peer/src/MangaMesh.Peer.Core/MangaMesh.Peer.Core.csproj"

COPY . .
WORKDIR "/src/mangamesh-peer/src/MangaMesh.Peer.Core"
RUN dotnet build "MangaMesh.Peer.Core.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MangaMesh.Peer.Core.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MangaMesh.Peer.Core.dll"]
