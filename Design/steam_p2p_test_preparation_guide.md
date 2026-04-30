# Steam P2P 테스트 준비 가이드

작성일: 2026-04-30  
대상 프로젝트: RhythmRPG  
대상 범위: `Facepunch.Steamworks` 기반 Steam 로그인, Steam Lobby 바인딩, Steam P2P 전투, Host 재선출

## 목적

이 문서는 **현재 코드 기준**으로 Steam 환경 테스트를 시작하기 전에 필요한 준비 사항을 한 번에 정리한 실행 가이드다.  
특히 다음 4가지를 빠르게 검증할 수 있도록 구성한다.

1. Steam 초기화와 Steam ID 획득
2. 기존 자체 방 목록 UI와 Steam Lobby 바인딩
3. 전투 시작 후 `SteamNetworkingSockets` 경유 P2P 전송
4. Host 이탈 시 재선출과 재연결

추가로 현재 클라이언트에는 `F8`로 토글 가능한 `Steam / P2P Debug` HUD가 들어가 있다.  
혼자 테스트할 때도 아래를 바로 구분할 수 있다.

- Steam이 아예 초기화되지 않은 상태
- Steam은 정상인데 Guest fallback으로 로그인된 상태
- Steam Lobby는 생성/조인됐지만 아직 peer가 없는 상태
- 전투에서 Steam P2P transport가 선택됐지만 아직 host 연결 전인 상태

## 현재 구현 기준 요약

현재 구현은 아래 구조를 테스트 대상으로 본다.

- 방 목록과 게임 시작 전 조율은 **기존 ApiServer/WaitingRoom**이 담당
- Steam Lobby는 **백그라운드 바인딩** 용도
- 전투 시작 시 ApiServer가 `MatchManifest`를 생성
- 실시간 전투 패킷은 `CreateRelaySocket` / `ConnectRelay` 기반 **Steam P2P** 사용
- Host 선정은 **게임 시작 전 waiting room RTT probe 결과** 기준
- Host 이탈 시 재선출은 **manifest에 저장된 Host 우선순위** 순서 기준

중요한 점:

- 지금의 "가장 네트워크 상태가 좋은 사람"은 **실시간 계속 측정값**이 아니라, **게임 시작 직전 probe RTT 기준**이다.
- 실시간 게임 서버 비용을 줄이는 목적은 이미 반영되어 있지만, 결과 검증의 `guest ack/hash -> 보상 보류` 단계는 아직 다음 작업이다.

## 테스트 모드

테스트는 아래 두 모드로 나눠서 준비하는 것이 좋다.

### 1. 최소 스모크 테스트

목표:

- Steam 초기화
- Steam Lobby 생성/참가
- 전투 시작 후 Steam P2P 연결
- Host 재선출

특징:

- `ApiServer`의 `Steam.PublisherKey`가 비어 있어도 가능
- 이 경우 Steam 로그인 검증이 실패하면 클라이언트는 **Guest 로그인으로 fallback**한다
- 즉, **Steam transport**는 보되, **Steam 계정 인증 API**는 보지 않는 테스트다

### 2. 전체 통합 테스트

목표:

- 위 스모크 테스트 전부
- `/auth/login/steam` 실제 검증
- Steam ticket 기반 로그인과 계정 매핑 확인

추가 필요:

- Steamworks Publisher Key
- 테스트에 사용할 실제 AppID 또는 Steam Playtest AppID

## 사전 준비 체크리스트

### 인원 / 장비

- Steam 계정 2개
- PC 2대
- 각 PC에 Steam Desktop Client 설치
- 각 PC에서 서로 다른 Steam 계정 로그인

권장:

- Host 후보 PC 1대는 유선 LAN
- Guest PC 1대는 유선 또는 안정적인 Wi-Fi

### 소프트웨어

- Unity Editor
- Visual Studio 2022 또는 `dotnet` SDK
- Docker Desktop
- Steam Client

### 네트워크

- 두 PC 모두 인터넷 연결 가능
- Windows Firewall에서 `Steam.exe`, Unity Editor, 클라이언트 빌드 exe의 **아웃바운드 차단이 없을 것**
- 포트 포워딩은 필수가 아니다. 현재 구현은 Steam P2P relay 경로를 사용한다

근거:

- Steam `CreateListenSocketP2P` / `ConnectP2P` 연결은 Valve 네트워크를 통해 relay될 수 있다: [ISteamNetworkingSockets](https://partner.steamgames.com/doc/api/ISteamnetworkingSockets)

## Steam 관련 준비

### 현재 임시 AppID

현재 테스트 가정은 아래 값이다.

- Steam AppID: `480`

현재 코드 반영 위치:

- [Client/steam_appid.txt](D:/Git/Server/RhythmRPG/RhythmRPG/Client/steam_appid.txt)
- [Client/Assets/0.MainProject/Resources/AppConfig.asset](D:/Git/Server/RhythmRPG/RhythmRPG/Client/Assets/0.MainProject/Resources/AppConfig.asset)
- [Server/ApiServer/appsettings.json](D:/Git/Server/RhythmRPG/RhythmRPG/Server/ApiServer/appsettings.json)

주의:

- `Facepunch.Steamworks` 쪽 `SteamClient.Init`은 AppID가 유효하지 않거나, Steam이 꺼져 있거나, 해당 앱 실행 권한이 없으면 예외가 날 수 있다: [Installing For Unity](https://wiki.facepunch.com/steamworks/Installing_For_Unity)
- 따라서 `480`은 **임시 내부 테스트용 가정**으로만 보고, 외부 QA나 장기 테스트로 넘어갈 때는 실제 AppID 또는 Steam Playtest AppID로 교체하는 것을 권장한다

### 외부 테스트 권장 방식

Steam 공식 문서 기준으로 외부 테스트는 **Steam Playtest**가 가장 자연스럽다. Steam 문서에 따르면 Steam Playtest AppID는 본편과 같은 Steamworks 기술 기능을 사용할 수 있다: [Testing On Steam](https://partner.steamgames.com/doc/store/testing)

즉, 2인 내부 확인이 끝나면 다음 순서는:

1. 실제 메인 AppID 확보
2. Steam Playtest AppID 생성
3. `480` 대신 Playtest AppID로 치환

## 서버 / 클라이언트 설정 체크

### 1. Client AppConfig

현재 실제 에셋 값:

- `BaseUrl`: `http://jjongserver.ddns.net:80`
- `EnableSteam`: `true`
- `SteamAppId`: `480`
- `PreferSteamP2PInGame`: `true`
- `PreferSteamLogin`: `true`

파일:

- [AppConfig.asset](D:/Git/Server/RhythmRPG/RhythmRPG/Client/Assets/0.MainProject/Resources/AppConfig.asset)

로컬 테스트를 할 경우 권장 변경값:

```text
BaseUrl = http://127.0.0.1:5290
EnableSteam = true
SteamAppId = 480
PreferSteamP2PInGame = true
PreferSteamLogin = true
```

설명:

- `5290`은 ApiServer 개발 프로필 포트다
- 지금처럼 DDNS 주소를 유지하면 로컬 ApiServer 로그와 맞춰 보기 어려워진다

### 2. ApiServer Steam 설정

파일:

- [Server/ApiServer/appsettings.json](D:/Git/Server/RhythmRPG/RhythmRPG/Server/ApiServer/appsettings.json)

중요 항목:

```json
"Steam": {
  "Enabled": true,
  "AppId": "480",
  "PublisherKey": "",
  "WebApiBaseUrl": "https://partner.steam-api.com"
}
```

의미:

- `PublisherKey`가 비어 있으면 `/auth/login/steam`은 실제 검증을 통과할 수 없다
- 이 상태에서는 클라이언트가 Steam 로그인 시도를 하더라도 결국 Guest 로그인으로 fallback할 수 있다

전체 통합 테스트를 하려면:

```text
Steam:PublisherKey = 실제 Publisher Key
Steam:AppId = 실제 테스트 AppID 또는 Playtest AppID
```

권장:

- 실키는 `appsettings.json`에 직접 박지 말고 사용자 비밀 저장소나 환경 변수로 주입

### 3. GameServer -> ApiServer 연결

파일:

- [Server/GameServer/appsettings.json](D:/Git/Server/RhythmRPG/RhythmRPG/Server/GameServer/appsettings.json)

확인 항목:

```json
"ApiServer": {
  "BaseUrl": "http://localhost:5290",
  "SystemApiKey": "rhythmrpg-server-secret-key-1234"
}
```

이 값은 아래와 일치해야 한다.

- [Server/ApiServer/appsettings.json](D:/Git/Server/RhythmRPG/RhythmRPG/Server/ApiServer/appsettings.json)의 `SystemApiKey`

불일치하면:

- GameServer가 `MatchManifest` 조회 실패
- 결과 저장 실패
- Host preference 반영 실패

### 4. Steam AppID 파일

Standalone 빌드 테스트에서는 실행 파일 옆에 `steam_appid.txt`가 있어야 한다.

현재 반영 파일:

- [Client/steam_appid.txt](D:/Git/Server/RhythmRPG/RhythmRPG/Client/steam_appid.txt)

## 로컬 실행 권장 구성

가장 덜 헷갈리는 조합은 아래다.

### 권장 구성

- Docker:
  - PostgreSQL
  - Redis
  - ControlPlane
- 로컬 실행:
  - ApiServer
  - Town GameServer
  - Game GameServer
  - Unity Client 2대

이 구성이 좋은 이유:

- Redis / DB / ControlPlane 포트를 고정하기 쉽다
- ApiServer / GameServer 로그를 IDE에서 바로 보기 쉽다
- Steam 관련 예외와 게임 패킷 로그를 한쪽에서 집중 확인 가능하다

## 인프라 준비 절차

### 1. `.env` 준비

파일 위치:

- `D:/Git/Server/RhythmRPG/RhythmRPG/Server/.env`

예시:

```env
POSTGRES_USER=app_user
POSTGRES_PASSWORD=Strong_App_Pw!
POSTGRES_DB=lobbydb
REDIS_PASSWORD=whdals0119
CONTROLPLANE_PORT=5001
API_PORT=5000
TOWN_PUBLIC_PORT=13221
GAME_PUBLIC_PORT=13222
SERVER_PUBLIC_HOST=127.0.0.1
```

주의:

- 실제 비밀번호는 로컬 전용 값으로 바꾸는 것을 권장
- 이 파일은 테스트 PC마다 맞춰 둔다

### 2. Docker 서비스 실행

작업 폴더:

```powershell
cd D:\Git\Server\RhythmRPG\RhythmRPG\Server
```

실행:

```powershell
docker compose up -d db redis controlplane
```

필요 시 전체 인프라를 Docker로도 가능하지만, 현재 테스트 준비는 위 3개만 먼저 띄우는 구성이 가장 단순하다.

## 애플리케이션 실행 절차

### 1. ApiServer 실행

```powershell
dotnet run --project D:\Git\Server\RhythmRPG\RhythmRPG\Server\ApiServer\ApiServer.csproj --launch-profile http
```

기대 포트:

- `http://0.0.0.0:5290`

### 2. Town Server 실행

```powershell
dotnet run --project D:\Git\Server\RhythmRPG\RhythmRPG\Server\GameServer\GameServer.csproj -- --role Town
```

기대 포트:

- `13221`

### 3. Game Server 실행

```powershell
dotnet run --project D:\Git\Server\RhythmRPG\RhythmRPG\Server\GameServer\GameServer.csproj -- --role Game
```

기대 포트:

- `13222`

### 4. Unity / Standalone 클라이언트 실행

권장 순서:

1. PC A에서 Steam 계정 A 로그인
2. PC B에서 Steam 계정 B 로그인
3. 각 PC에서 클라이언트 실행
4. 둘 다 같은 `ClientVersion`인지 확인

주의:

- 한 PC에서 Steam 계정 2개를 동시에 안정적으로 돌리는 방식은 이번 테스트 준비 범위에서 제외
- 이번 준비는 **2 PC / 2 계정**을 기본으로 한다

## 테스트 전 최종 확인표

### 공통

- [ ] 두 PC 모두 Steam Client 로그인 완료
- [ ] 두 PC 모두 서로 다른 Steam 계정 사용
- [ ] `AppConfig.asset`의 `BaseUrl`이 테스트 대상 ApiServer 주소를 가리킴
- [ ] `EnableSteam = true`
- [ ] `SteamAppId = 480` 또는 실제 테스트 AppID
- [ ] `PreferSteamP2PInGame = true`
- [ ] `steam_appid.txt` 존재

### 서버

- [ ] Redis 실행 중
- [ ] PostgreSQL 실행 중
- [ ] ControlPlane 실행 중 (`5001`)
- [ ] ApiServer 실행 중 (`5290`)
- [ ] Town Server 실행 중 (`13221`)
- [ ] Game Server 실행 중 (`13222`)

### Steam 인증 전체 테스트 전용

- [ ] `ApiServer`의 `Steam:PublisherKey` 설정 완료
- [ ] 테스트 AppID와 `PublisherKey`가 같은 Steamworks 앱에 속함

## 권장 테스트 시나리오

### 1. Steam 초기화 스모크

목표:

- 두 클라이언트 모두 Steam 초기화 성공

성공 기준:

- 클라이언트 로그에 `[Steam] Initialized`
- `SteamId64`와 `Name`이 출력됨

### 2. 로그인 경로 확인

목표:

- Steam 로그인 또는 Guest fallback 여부 확인

성공 기준:

- 전체 통합 테스트: `/auth/login/steam` 성공
- 최소 테스트: Steam ticket 발급 시도 후 Guest fallback이어도 진행 가능

### 3. 방 생성 / Steam Lobby 바인딩

목표:

- 기존 방 UI로 방 생성
- Steam Lobby ID가 바인딩됨

성공 기준:

- 방장 클라이언트에서 Steam Lobby 생성
- 참가자 클라이언트에서 해당 대기방 참가
- Room WS에 `SteamLobbyBound` 수신

### 4. MatchManifest 생성

목표:

- 게임 시작 시 manifest 생성 및 두 클라이언트 수신

성공 기준:

- ApiServer 로그에 `[GameMatch] Stored manifest`
- 두 클라이언트 모두 `GameStart` 수신
- `hostUid`, `hostSteamId64`, `hostEpoch` 값 확인 가능

### 5. Steam P2P 전투 연결

목표:

- 실시간 전투 패킷이 GameServer relay가 아니라 Steam transport를 통과

성공 기준:

- Host 로그에 `[SteamP2P] Host relay socket ready`
- Guest 로그에 `[SteamP2P] Connecting to host=...`
- 입력/판정이 전투 중 정상 동기화

### 6. Host 재선출

목표:

- 현재 Host 프로세스를 강제 종료하거나 네트워크를 끊었을 때 새 Host로 전환

성공 기준:

- GameServer 로그에 `Host changed relayId=...`
- 클라이언트 로그에 `Steam host changed actor=...`
- 남은 클라이언트가 새 Host 기준으로 전투 계속 진행 또는 최소 재연결 시도

## 로그 수집 포인트

### Client

중요 로그 태그:

- `[Steam]`
- `[SteamP2P]`
- `[P2PRelayClientBridge]`

볼 것:

- Steam 초기화 성공 여부
- Steam Web API ticket 생성 여부
- Host socket 생성 여부
- Guest connect / reconnect 여부
- Host 변경 반영 여부

### ApiServer

중요 로그:

- `WS Join Success`
- `[GameMatch] Stored manifest`
- Steam auth 실패 / 성공

### GameServer

중요 로그:

- `Manifest loaded roomId=...`
- `Host preference updated`
- `Host changed relayId=...`

## 현재 테스트에서 주의할 점

### 1. `PublisherKey`가 비어 있으면 Steam 로그인 검증은 완전하지 않다

즉, transport가 붙어도 **인증 테스트가 끝난 것은 아니다**.

### 2. `AppConfig.asset`는 현재 원격 DDNS를 가리키고 있다

로컬 스택 테스트를 하려면 **반드시** 로컬 ApiServer 주소로 바꾸는 것이 좋다.

### 3. Host 품질 평가는 "전투 시작 전" 값이다

현재는 게임 시작 전에 수집한 RTT 기준 Host 우선순위를 사용한다.  
전투 도중 모든 피어의 상태를 다시 측정해서 즉시 최적 Host를 고르는 단계는 아직 아니다.

### 4. 결과 검증 보류 정책은 아직 후속 작업이 남아 있다

지금은 Steam P2P transport와 Host 재선출 검증이 우선이다.  
보상 불일치 시 `보류` 정책을 완전히 닫으려면 Guest ack/hash 연동이 추가로 필요하다.

## 추천 진행 순서

1. `480` + Guest fallback으로 스모크 테스트
2. Host 재선출 테스트
3. `PublisherKey` 연결 후 Steam 로그인 통합 테스트
4. 실제 AppID 또는 Steam Playtest AppID로 교체
5. 외부 QA 또는 친구 초대 테스트 확장

## 공식 참고 문서

- Steam 내부/외부 테스트 가이드: [Testing On Steam](https://partner.steamgames.com/doc/store/testing)
- Steam Playtest 개요: [Steam Playtest](https://partner.steamgames.com/doc/features/playtest)
- Steam P2P 소켓 API: [ISteamNetworkingSockets](https://partner.steamgames.com/doc/api/ISteamnetworkingSockets)
- Steam Lobby / Matchmaking API: [ISteamMatchmaking](https://partner.steamgames.com/doc/api/ISteamMatchmaking)
- Steam Web API ticket 검증: [ISteamUserAuth / AuthenticateUserTicket](https://partner.steamgames.com/doc/webapi/isteamuserauth?l=english)
- Facepunch Unity 설치 가이드: [Installing For Unity](https://wiki.facepunch.com/steamworks/Installing_For_Unity)
