FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Server/ApiServer/ApiServer.csproj", "Server/ApiServer/"]
COPY ["Server/Shared/Shared.csproj", "Server/Shared/"]
RUN dotnet restore "Server/ApiServer/ApiServer.csproj"
COPY ["Server/", "Server/"]
WORKDIR "/src/Server/ApiServer"
RUN dotnet build "ApiServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApiServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "ApiServer.dll"]
