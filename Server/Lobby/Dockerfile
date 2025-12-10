# -----------------------------
# Build Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy Project File (optimization cach)
COPY *.csproj .
RUN dotnet restore

# Copy hole Project
COPY . .
RUN dotnet publish -c Release -o /app

# -----------------------------
# Runtime Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy build artifact
COPY --from=build /app ./

# Docker Env Setting File
COPY appsettings.Docker.json ./appsettings.Docker.json

#  keys direction
# => appsettings.json stay use relative path
COPY Infrastructure/Security/keys ./Infrastructure/Security/keys

# migrations folder mount
VOLUME /app/migrations

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "Lobby.dll"]
