

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["mangamesh-peer/src/MangaMesh.Peer.ClientApi/MangaMesh.Peer.ClientApi.csproj", "mangamesh-peer/src/MangaMesh.Peer.ClientApi/"]
COPY ["mangamesh-peer/src/MangaMesh.Peer.Core/MangaMesh.Peer.Core.csproj", "mangamesh-peer/src/MangaMesh.Peer.Core/"]
COPY ["mangamesh-peer/src/MangaMesh.Shared/MangaMesh.Shared.csproj", "mangamesh-peer/src/MangaMesh.Shared/"]
RUN dotnet restore "mangamesh-peer/src/MangaMesh.Peer.ClientApi/MangaMesh.Peer.ClientApi.csproj"
COPY . .
WORKDIR "/src/mangamesh-peer/src/MangaMesh.Peer.ClientApi"
RUN dotnet build "MangaMesh.Peer.ClientApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MangaMesh.Peer.ClientApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "MangaMesh.Peer.ClientApi.dll"]
