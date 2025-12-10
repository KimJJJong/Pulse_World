# -----------------------------
# Build Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 전체 솔루션 복사 (Server, ServerCore, Shared 모두 포함)
COPY . .

# 키가 Server/keys 안에 있으므로 그대로 복사됨 (별도 COPY 필요 없음)
# 만약 COPY가 필요하다면 아래 코드지만, 보통 COPY . . 로 이미 복사됨:
# COPY Server/keys ./Server/keys

# restore 실행 (Server.csproj 가 게임서버 프로젝트)
RUN dotnet restore GameServer/GameServer.csproj

# publish
RUN dotnet publish GameServer/GameServer.csproj -c Release -o /app/publish


# -----------------------------
# Runtime Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# publish 결과물 복사
COPY --from=build /app/publish ./


# Docker 환경용 설정 파일 복사
COPY GameServer/appsettings.Docker.json ./appsettings.Docker.json

# 공개키도 Server/keys 안에 있으므로 그대로 복사
COPY GameServer/keys ./keys

# TCP 포트 오픈
EXPOSE 13221

ENTRYPOINT ["dotnet", "GameServer.dll"]
