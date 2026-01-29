FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ControlPlaneServer/ControlPlaneServer.csproj", "ControlPlaneServer/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "ControlPlaneServer/ControlPlaneServer.csproj"
COPY . .
WORKDIR "/src/ControlPlaneServer"
RUN dotnet build "ControlPlaneServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ControlPlaneServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# No ASPNETCORE_URLS needed as it's set in Program.cs, but good practice to allow override
ENTRYPOINT ["dotnet", "ControlPlaneServer.dll"]
