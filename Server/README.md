# RhythmRPG Server

이 디렉터리는 Pulse World / RhythmRPG의 현재 .NET 서버 스택입니다.

예전 서버 README 구조는 더 이상 현재 기준이 아닙니다. 현재 서버는 HTTP API, gRPC ControlPlane, role 기반 TCP GameServer, Redis 상태 저장, PostgreSQL 영구 저장, JSON 콘텐츠 로딩으로 구성됩니다.

## 프로젝트

```text
Server/
├── ApiServer/             HTTP API, 인증, 룸, WebSocket room hub
├── ControlPlaneServer/    gRPC 서버 레지스트리, 할당, 티켓, 프레즌스
├── GameServer/            TCP 서버, Town/Game role module
├── GameServer.Tests/      net9.0 테스트 프로젝트
├── PacketGenerator/       패킷 코드 생성기
├── ServerCore/            소켓/네트워크 코어
├── Shared/                공유 계약/프로토콜 코드
├── WebGenerater/          웹/코드 생성 보조 도구
├── migrations/            DB migration 스크립트
├── data/                  Docker volume 데이터
└── docker-compose.yml     로컬 컨테이너 오케스트레이션
```

`Server.sln`에 포함된 프로젝트:

```text
ApiServer
ControlPlaneServer
GameServer
GameServer.Tests
PacketGenerator
ServerCore
Shared
WebGenerater
```

## 런타임 구성

```text
ApiServer
  - HTTP controllers
  - /hub/room WebSocket endpoint
  - PostgreSQL persistence
  - Redis lobby/session/room state
  - ControlPlaneServer gRPC client

ControlPlaneServer
  - 5001 포트 gRPC
  - Redis 기반 server registry
  - ticket allocation
  - room, transition, presence coordination

GameServer
  - 하나의 실행 파일을 --role로 분기
  - Town role: 마을/세션 릴레이, Town map state
  - Game role: 전투/gameplay session state
  - TCP listener, ControlPlane heartbeat, ApiServer internal calls
```

## 요구사항

- 일반 서버 빌드: .NET 8 SDK
- `GameServer.Tests`: .NET 9 SDK
- 로컬 스택: Docker Desktop

## Docker 실행

`docker-compose.yml`과 같은 위치에 `.env`를 만듭니다.

```env
POSTGRES_USER=app_user
POSTGRES_PASSWORD=change_me
POSTGRES_DB=lobbydb
REDIS_PASSWORD=change_me

CONTROLPLANE_PORT=5001
API_PORT=5000
SERVER_PUBLIC_HOST=127.0.0.1
TOWN_PUBLIC_PORT=13221
GAME_PUBLIC_PORT=13222
```

실행:

```powershell
docker compose up -d --build
docker compose ps
```

기본 포트:

| Service | Host port | 용도 |
| --- | ---: | --- |
| `db` | `5432` | PostgreSQL |
| `redis` | `6380` | Redis, container port `6379` |
| `controlplane` | `5001` | HTTP/2 gRPC |
| `apiserver` | `5000` | HTTP API와 WebSocket |
| `townserver` | `13221` | TCP Town role |
| `gameserver` | `13222` | TCP Game role |

로그:

```powershell
docker compose logs -f apiserver controlplane townserver gameserver
```

중지:

```powershell
docker compose down
```

## 로컬 개발 실행

저장소 루트에서 실행합니다.

```powershell
dotnet build Server/Server.sln
dotnet run --project Server/ControlPlaneServer/ControlPlaneServer.csproj
dotnet run --project Server/ApiServer/ApiServer.csproj --launch-profile http
dotnet run --project Server/GameServer/GameServer.csproj -- --role Town
dotnet run --project Server/GameServer/GameServer.csproj -- --role Game
```

주요 로컬 포트:

- `ControlPlaneServer`: `5001`
- `ApiServer` launch profile `http`: `5290`
- `GameServer --role Town`: `13221`
- `GameServer --role Game`: `13222`
- Docker Compose Redis host port: `6380`
- Docker Compose PostgreSQL host port: `5432`

주의: `GameServer/appsettings.Town.json`, `GameServer/appsettings.Game.json`에는 현재 워크스페이스 기준 절대 콘텐츠 경로가 들어 있습니다. Docker 실행은 이 값을 override합니다. 다른 경로에서 로컬 실행할 경우 해당 경로를 수정하거나 `Server__Content__Game__BaseDir`, `Server__Content__Town__BaseDir` 같은 환경변수 override를 사용합니다.

## 콘텐츠 구조

```text
GameServer/Content/
├── 01.Game/       Game role 맵, 스테이지, 사운드, 스킬, 패턴, 엔티티
├── 02.Town/       Town role 맵 콘텐츠
├── Data/Json/     공용 아이템/장비/드롭 데이터
├── Map/           공용/레거시 맵 런타임 코드와 JSON
├── Pattern/       공용/레거시 패턴 로더 데이터
└── Skill/         공용/레거시 스킬 로더 데이터
```

## 테스트

```powershell
dotnet test Server/GameServer.Tests/GameServer.Tests.csproj
```

테스트 프로젝트가 `net9.0` 대상이므로 .NET 9 SDK가 필요합니다.
