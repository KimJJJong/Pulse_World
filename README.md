# Pulse World / RhythmRPG

`RhythmRPG`는 **Pulse World**의 Unity 클라이언트와 .NET 서버를 함께 관리하는 저장소입니다. 현재 `main`은 최신 재구성 기준 브랜치이며, 리듬 액션 RPG의 마을, 룸, 전투 세션, Steam/P2P 흐름을 통합하는 방향으로 정리되어 있습니다.

## 현재 기준

- 클라이언트 제품명: `Pulse World`
- Unity 버전: `6000.3.12f1`
- 렌더 파이프라인: URP `17.3.0`
- 서버 런타임: 대부분 `.NET 8`
- 서버 테스트: `GameServer.Tests`는 `.NET 9` 대상
- 로컬 인프라: PostgreSQL 15, Redis 7, Docker Compose
- 현재 주요 범위: 숲 마을, 숲 튜토리얼/첫 스텝 게임 맵, 리듬 전투, 룸 플로우, Steam/P2P 전송, 서버 릴레이 fallback

## 전체 구조

```text
Unity Client
  -> ApiServer HTTP controllers
  -> ApiServer WebSocket /hub/room
  -> GameServer TCP, Town role 또는 Game role

ApiServer
  -> PostgreSQL: 계정/플레이어 영구 데이터
  -> Redis: 로비, 룸, 토큰, 세션 상태
  -> ControlPlane gRPC: 서버 할당과 티켓 발급

ControlPlaneServer
  -> Redis 기반 서버 레지스트리, 티켓, 할당, 룸/프레즌스 상태

GameServer
  -> Role 기반 런타임: Town 또는 Game
  -> 실시간 클라이언트 패킷용 TCP listener
  -> ControlPlane 등록/heartbeat
  -> ApiServer 내부 API 호출
  -> Role별 JSON 콘텐츠 로딩
```

## 디렉터리 구조

```text
RhythmRPG/
├── Client/                    Unity 클라이언트 프로젝트
│   ├── Assets/0.MainProject/  메인 게임 스크립트, 씬, 프리팹, 리소스
│   ├── Packages/              Unity 패키지 manifest
│   └── ProjectSettings/       Unity 프로젝트 설정
├── Server/                    .NET 서버 솔루션
│   ├── ApiServer/             HTTP API, 인증, 룸, WebSocket room hub
│   ├── ControlPlaneServer/    gRPC 서버 레지스트리, 할당, 티켓, 프레즌스
│   ├── GameServer/            TCP 게임/마을 서버, role module 기반
│   ├── GameServer.Tests/      net9.0 테스트 프로젝트
│   ├── PacketGenerator/       패킷 코드 생성기
│   ├── ServerCore/            소켓/네트워크 코어
│   ├── Shared/                공유 계약/프로토콜 코드
│   ├── WebGenerater/          웹/코드 생성 보조 도구
│   └── docker-compose.yml     로컬 컨테이너 스택
├── Design/                    GDD, Steam P2P, 아키텍처, 기획 문서
├── Resource/                  외부/보조 리소스
└── Tools/                     유틸리티 도구
```

## 요구사항

- Unity Hub + Unity Editor `6000.3.12f1`
- Visual Studio 2022 또는 Rider
- 서버 빌드용 .NET 8 SDK
- `GameServer.Tests` 실행 시 .NET 9 SDK
- 로컬 인프라 실행용 Docker Desktop
- Steam/P2P 테스트용 Steam 데스크톱 클라이언트

## 서버 빠른 실행

`Server/.env` 파일을 생성합니다.

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

Docker 전체 스택 실행:

```powershell
cd Server
docker compose up -d --build
docker compose ps
```

기본 Docker 포트:

| Service | Host port | 설명 |
| --- | ---: | --- |
| `db` | `5432` | PostgreSQL 15 |
| `redis` | `6380` | 컨테이너 내부 Redis 포트는 `6379` |
| `controlplane` | `5001` | HTTP/2 gRPC |
| `apiserver` | `5000` | HTTP API와 `/hub/room` WebSocket |
| `townserver` | `13221` | `--role Town` GameServer |
| `gameserver` | `13222` | `--role Game` GameServer |

로그 확인:

```powershell
cd Server
docker compose logs -f apiserver controlplane townserver gameserver
```

## 서버 로컬 개발

저장소 루트에서 실행합니다.

```powershell
dotnet build Server/Server.sln
dotnet run --project Server/ControlPlaneServer/ControlPlaneServer.csproj
dotnet run --project Server/ApiServer/ApiServer.csproj --launch-profile http
dotnet run --project Server/GameServer/GameServer.csproj -- --role Town
dotnet run --project Server/GameServer/GameServer.csproj -- --role Game
```

로컬 실행 주의사항:

- `ApiServer`의 `http` launch profile은 `http://0.0.0.0:5290`에서 실행됩니다.
- Docker의 `ApiServer`는 host port `5000`에서 실행됩니다.
- `GameServer`는 `--role Town` 또는 `--role Game` 인자에 따라 `appsettings.Town.json`, `appsettings.Game.json`을 추가 로드합니다.
- Docker는 콘텐츠 경로를 환경변수로 override합니다. Docker가 아닌 로컬 실행에서 저장소 경로가 달라졌다면 GameServer 콘텐츠 base path를 수정하거나 환경변수 override를 사용해야 합니다.

## 클라이언트 빠른 실행

Unity `6000.3.12f1`로 `Client/` 폴더를 엽니다.

현재 빌드 씬은 `Client/ProjectSettings/EditorBuildSettings.asset`에 아래 순서로 등록되어 있습니다.

```text
Assets/0.MainProject/Scenes/Bootstrap.unity
Assets/0.MainProject/Scenes/LoadingScene.unity
Assets/0.MainProject/Scenes/HeadphonesRecommended.unity
Assets/0.MainProject/Scenes/Login.unity
Assets/0.MainProject/Scenes/WorldMap.unity
Assets/0.MainProject/Scenes/Town/TownMap.unity
Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity
Assets/0.MainProject/Scenes/Town/Town_Forest.unity
Assets/0.MainProject/Scenes/Game/Game_Forest_First_Step.unity
```

진입 씬은 `Bootstrap.unity`입니다. `AppBootstrap`이 `AppConfig`를 읽고, 저장된 토큰 상태에 따라 헤드폰 안내, 로그인, 월드맵 흐름으로 이동합니다.

클라이언트 API 설정:

- `Client/Assets/0.MainProject/Resources/AppConfig.asset`는 현재 `http://jjongserver.ddns.net:80`을 가리킵니다.
- `Client/Assets/0.MainProject/Resources/AppConfig_Local.asset`는 `http://127.0.0.1:5290`을 가리킵니다.
- 현재 `Bootstrap` 씬은 `AppConfig.asset`를 참조합니다. 로컬 서버 개발 시 씬에서 local config를 할당하거나 `AppConfig.asset`의 BaseUrl을 임시로 바꿔야 합니다.
- Docker API만 사용할 경우 클라이언트 BaseUrl은 `http://127.0.0.1:5000`으로 맞춥니다.

## 게임/네트워크 메모

- 로그인은 게스트와 Steam 지향 흐름을 지원합니다.
- `ApiServer`는 계정/세션 API, 룸 API, `/hub/room` WebSocket 업데이트를 담당합니다.
- `ControlPlaneServer`는 Town/Game 서버 티켓 발급과 할당을 담당합니다.
- `GameServer`는 하나의 실행 파일을 role별로 나누어 사용합니다.
  - `Town`: 마을/월드 릴레이, 룸 프레즌스, 낮은 주기 tick
  - `Game`: 리듬 전투 세션, 맵/스테이지/사운드/스킬 콘텐츠, 높은 주기 tick
- Steam P2P 전송과 host selection 흐름이 포함되어 있습니다.
- GameServer P2P relay packet route를 통한 서버 릴레이 fallback도 남아 있습니다.
- 현재 콘텐츠는 `Server/GameServer/Content/01.Game`, `02.Town`, 공유 데이터 폴더의 JSON을 기준으로 로딩됩니다.

## 테스트

서버 테스트:

```powershell
dotnet test Server/GameServer.Tests/GameServer.Tests.csproj
```

테스트 프로젝트가 `net9.0` 대상이므로 .NET 9 SDK가 필요합니다.

## 관련 문서

- `Server/README.md`: 서버 전용 실행 문서
- `Design/GDD/`: 게임 디자인 문서
- `Design/steam_p2p_test_preparation_guide.md`: Steam/P2P 준비 가이드
- `Design/steam_p2p_test_checklists.md`: Steam/P2P 검증 체크리스트
- `Design/steam_multiplayer_release_architecture.md`: Steam 멀티플레이어 아키텍처 메모
