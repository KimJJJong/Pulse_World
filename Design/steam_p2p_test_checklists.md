# Steam P2P 실제 테스트 체크리스트

작성일: 2026-04-30  
대상 프로젝트: RhythmRPG  
대상 범위: Steam 로그인, Steam Lobby 바인딩, Steam P2P 전투, Host 재선출

## 먼저 알아둘 점

이번 체크리스트는 지금 코드에 추가된 `Steam / P2P Debug` HUD를 기준으로 작성했다.

- HUD 토글 키: `F8`
- HUD 목적:
  - Steam이 실제로 초기화됐는지
  - Steam 로그인인지 Guest fallback인지
  - Steam Lobby가 바인딩/조인됐는지
  - 전투에서 Steam P2P transport가 선택됐는지
  - 아직 1인 테스트라서 "정상인데 peer가 없는 상태"인지

혼자 테스트할 때는 특히 HUD의 `Solo Check` 줄을 먼저 보면 된다.

## 공통 합격 기준

아래 4개가 보이면, 최소한 Steam 연동의 바닥은 정상이라고 봐도 된다.

- `Steam > Initialized: YES`
- `Auth > Mode`가 `Steam`, 또는 `GuestFallback`이어도 사유가 명확함
- `Waiting Room > Probe`에 `n ms` 값이 보임
- `Solo Check`가 "Steam is ready..."류 메시지로 바뀜

## 1인 사전 확인 체크리스트

2PC가 없어도 지금 바로 확인 가능한 항목이다.

### 실행 전

- [ ] Steam Client 실행
- [ ] 테스트 계정 로그인 완료
- [ ] 클라이언트 실행
- [ ] `F8`로 `Steam / P2P Debug` HUD 확인

### 합격 기준

- [ ] `Steam > Enabled: YES`
- [ ] `Steam > Initialized: YES`
- [ ] `Steam > AppId: 480` 또는 현재 테스트 AppID
- [ ] `Steam > SteamId64`가 비어 있지 않음
- [ ] `Steam > LastError: -` 또는 치명적이지 않은 메시지

### 로그인 경로 확인

- [ ] 로그인 후 `Auth > Mode` 확인
- [ ] `Steam`이면 Steam 인증까지 붙은 상태
- [ ] `GuestFallback`이면 현재 Steam ticket 또는 PublisherKey 미설정으로 Guest로 떨어진 상태

### 방 진입 전 확인

- [ ] 방 UI를 열고 방 생성 또는 입장
- [ ] `Waiting Room > RoomId` 표시 확인
- [ ] `Waiting Room > Probe`에 RTT가 표시되는지 확인
- [ ] `Waiting Room > LobbyStatus`가 `Creating`, `Created`, `Joining`, `Joined`, `Bound` 흐름 중 어디까지 갔는지 확인

### 1인 테스트 해석

혼자서 아래처럼 보이면 **실패가 아니라 정상 대기 상태**다.

- `Waiting Room > Members: 1`
- `Solo Check: Steam is ready. With one client...`
- 전투 진입 후 Host면 `State: Hosting (0 peer)`

즉, 이 상태는 "Steam이 안 붙은 것"이 아니라 "붙을 상대가 아직 없는 것"이다.

## 로컬 환경 체크리스트

대상:

- ApiServer, Town Server, Game Server를 로컬에서 실행
- DB/Redis/ControlPlane은 Docker
- 테스트 클라이언트는 2PC 권장

### 설정

- [ ] `Client/Assets/0.MainProject/Resources/AppConfig.asset`의 `BaseUrl`을 로컬 ApiServer로 설정
- [ ] 권장값: `http://127.0.0.1:5290`
- [ ] `EnableSteam = true`
- [ ] `PreferSteamLogin = true`
- [ ] `PreferSteamP2PInGame = true`
- [ ] `steam_appid.txt` 확인

### 인프라

- [ ] `docker compose up -d db redis controlplane`
- [ ] ApiServer 실행
- [ ] Town Server 실행
- [ ] Game Server 실행

### 로컬 smoke

- [ ] 클라이언트 A 로그인
- [ ] HUD에서 `Steam > Initialized: YES`
- [ ] HUD에서 `Auth > Mode` 확인
- [ ] 방 생성
- [ ] `Waiting Room > Probe` RTT 확인
- [ ] `Waiting Room > LobbyStatus`가 `Created` 또는 `Joined`로 전개되는지 확인

### 로컬 2PC 본 테스트

- [ ] PC A: 방 생성
- [ ] PC B: 같은 방 입장
- [ ] 양쪽 모두 `Waiting Room > Members`가 `2` 이상
- [ ] 양쪽 모두 `Waiting Room > SteamLobbyId` 동일
- [ ] Start
- [ ] Host 클라이언트 HUD: `Transport: SteamP2P`
- [ ] Host 클라이언트 HUD: `State: Hosting (1 peer)` 이상
- [ ] Guest 클라이언트 HUD: `ToHost: Connected`
- [ ] Guest 클라이언트 HUD: `Ping` 값이 `last n ms / avg n ms` 형식으로 갱신

### 로컬 Host 재선출

- [ ] 전투 중 현재 Host 프로세스 종료
- [ ] 남은 클라이언트 HUD에서 `ManifestEpoch` 또는 `HostEpoch` 변경 확인
- [ ] 남은 클라이언트 HUD에서 `Host` 정보가 새 값으로 바뀌는지 확인
- [ ] Guest였다가 Host가 된 쪽이면 `State: Hosting (...)`
- [ ] 재연결된 Guest면 `ToHost: Connected`

## 원격 환경 체크리스트

대상:

- ApiServer/GameServer가 원격 서버 또는 DDNS 주소
- 클라이언트는 외부 네트워크에서 접속

### 원격 배포 전

- [ ] 원격 `ApiServer` 접근 가능
- [ ] 원격 `Town/GameServer` 접근 가능
- [ ] `SystemApiKey` 일치
- [ ] `Steam.PublisherKey` 설정 여부 확인

### 원격 클라이언트 설정

- [ ] `BaseUrl`을 원격 주소로 설정
- [ ] 두 PC 모두 서로 다른 인터넷 환경이면 더 좋음
- [ ] Windows Firewall 또는 백신이 Steam/클라이언트 트래픽을 막지 않는지 확인

### 원격 smoke

- [ ] 클라이언트 A에서 HUD `Steam > Initialized: YES`
- [ ] `Auth > Mode` 확인
- [ ] 방 생성 후 `Waiting Room > Probe` RTT 확인
- [ ] 원격 환경이라면 로컬보다 RTT가 높아도 probe 값이 찍히면 1차 통과

### 원격 2PC 본 테스트

- [ ] 두 클라이언트가 같은 방에 모임
- [ ] `SteamLobbyId` 동일
- [ ] Start
- [ ] Host: `State: Hosting (1 peer)` 이상
- [ ] Guest: `ToHost: Connected`
- [ ] Guest: `Ping` 값 확인
- [ ] 2~3분 플레이 동안 `TransportError`가 계속 비어 있는지 확인

### 원격 환경에서 특별히 볼 것

- [ ] `Ping` 평균값이 지나치게 높지 않은지
- [ ] `TransportError`가 주기적으로 쌓이지 않는지
- [ ] Host 변경 후에도 `Connected`가 복구되는지

## HUD 해석표

### Steam 섹션

- `Initialized: YES`
  - Steam SDK 초기화 성공
- `Initialized: NO`
  - Steam Client 미실행, AppID 문제, 실행 권한 문제 가능성

### Auth 섹션

- `Mode: Steam`
  - Steam ticket 발급 및 `/auth/login/steam` 성공
- `Mode: GuestFallback`
  - Steam 경로를 시도했지만 Guest 로그인으로 대체
- `Mode: Guest`
  - 설정상 Steam 로그인 미사용 또는 초기화 전

### Waiting Room 섹션

- `Probe: n ms`
  - waiting room RTT probe 성공
- `LobbyStatus: Created ...`
  - 방장이 Steam Lobby 생성 완료
- `LobbyStatus: Joined ...`
  - Steam Lobby 참가 완료

### Match / Transport 섹션

- `Transport: SteamP2P`
  - 실시간 전투가 Steam transport를 사용 중
- `State: Hosting (0 peer)`
  - Host는 준비됐지만 아직 연결된 다른 클라이언트 없음
- `ToHost: Connected`
  - Guest가 Host에 실제 연결됨
- `Ping: last/avg`
  - Guest 기준 Host RTT 측정 중

## 실패로 볼 상황

- [ ] `Steam > Initialized: NO`가 계속 유지
- [ ] `Waiting Room > Probe`가 끝까지 `Idle/Connecting/Waiting Pong`에서 멈춤
- [ ] 두 클라이언트가 같은 방인데 `SteamLobbyId`가 다름
- [ ] 전투 시작 후 `Transport`가 기대와 다르게 `ServerRelay`로 남음
- [ ] Guest에서 `ToHost: Not Connected`가 계속 유지
- [ ] `TransportError`가 반복적으로 누적

## 추천 순서

1. 1인으로 Steam init / Auth / Probe 확인
2. 1인으로 방 생성 후 LobbyStatus 확인
3. 2PC 로컬 테스트
4. 2PC 원격 테스트
5. Host 재선출 테스트
