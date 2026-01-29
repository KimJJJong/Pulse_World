FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 13221

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["GameServer/GameServer.csproj", "GameServer/"]
COPY ["ServerCore/ServerCore.csproj", "ServerCore/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "GameServer/GameServer.csproj"
COPY . .
WORKDIR "/src/GameServer"
RUN dotnet build "GameServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GameServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameServer.dll"]
