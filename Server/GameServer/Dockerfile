FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 13221

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Server/GameServer/GameServer.csproj", "Server/GameServer/"]
COPY ["Server/ServerCore/ServerCore.csproj", "Server/ServerCore/"]
COPY ["Server/Shared/Shared.csproj", "Server/Shared/"]
RUN dotnet restore "Server/GameServer/GameServer.csproj"
COPY ["Server/", "Server/"]
COPY ["Client/Assets/SharedContent/", "Client/Assets/SharedContent/"]
WORKDIR "/src/Server/GameServer"
RUN dotnet build "GameServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GameServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameServer.dll"]
