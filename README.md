# RhythmRPG

**RhythmRPG**는 리듬 게임의 다이내믹한 템포와 RPG의 성장 및 전략 요소를 결합한 멀티플레이어 온라인 게임 프로젝트입니다.  
Unity 기반의 클라이언트와 .NET 8 기반의 분산 서버 아키텍처로 구성되어 있으며, 실시간 리듬 노트 동기화와 대규모(였으면? 좋겠는) 멀티플레이어 환경(마을) 및 인스턴스 던전(게임 세션)을 지원합니다.

---

## 🏗️ 아키텍처 (Architecture)

이 프로젝트는 클라이언트-서버 모델을 따르며, 서버는 확장성과 유지보수성을 고려하여 여러 역할로 분리되어 있습니다.

### Client
- **Unity Engine**: 리듬 액션 및 RPG 게임 로직 구현.
- **Network**: 
  - RestAPI (Http) 인증 및 데이터 조회.
  - TCP/IP 소켓 통신을 통한 실시간 게임 플레이.

### Server
서버는 **.NET 8** 환경에서 구동되며, Docker Compose를 통해 통합 관리됩니다.

1. **ApiServer**
   - 계정 인증, 유저 데이터 관리, 게임 결과 저장 등 웹 API 서비스 제공.
   - PostgreSQL DB 및 Redis와 통신.

2. **ControlPlane**
   - 서버들의 상태를 관리하고 로드 밸런싱 및 동적 할당을 담당하는 중앙 제어 노드.
   - gRPC를 사용하여 타 서버들과 통신.

3. **GameServer** (Role 기반 실행)
   - **Town Server**: 유저들이 모이는 로비/마을 역할. 채팅 및 파티 매칭.
   - **Game Server**: 실제 리듬 배틀이 이루어지는 인스턴스 세션. 정밀한 판정 및 동기화 처리.

4. **Infrastructure**
   - **PostgreSQL**: 영구 데이터 저장소 (계정, 아이템 등).
   - **Redis**: 실시간 세션 관리, 캐싱, Pub/Sub.

---

## 🛠️ 기술 스택 (Tech Stack)

### Client
- **Engine**: Unity 2022/6 (Universal Render Pipeline)
- **Language**: C#
- **Patterns**: MVP/MVC 아키텍처, UniRx (Reactive Extensions)

### Server
- **Framework**: .NET 8, ASP.NET Core
- **Database**: PostgreSQL 15
- **Cache**: Redis 7
- **Communication**: 
  - gRPC (Server-to-Server)
  - TCP/IP Socket (Client-Server, Custom Protocol)
- **Deployment**: Docker, Docker Compose

---

## 📂 디렉토리 구조 (Directory Structure)

```
RhythmRPG/
├── Client/                 # Unity 클라이언트 프로젝트
│   ├── Assets/             # 게임 리소스 및 스크립트
│   └── Packages/           # 유니티 패키지 의존성
│
├── Server/                 # 서버 솔루션 (.NET 8)
│   ├── ApiServer/          # 웹 API 서버 엔트리포인트
│   ├── ControlPlaneServer/ # 서버 오케스트레이션 및 관리
│   ├── GameServer/         # 게임 로직 및 소켓 서버 (Town/Game 모드)
│   ├── ServerCore/         # 네트워크 엔진 및 코어 라이브러리
│   ├── PacketGenerator/    # 패킷 직렬화 코드 자동 생성기
│   ├── Shared/             # 클라/서버 공유 라이브러리 (Protocol, Data)
│   ├── data/               # DB 데이터 볼륨 (Docker)
│   └── docker-compose.yml  # 로컬 개발 환경 구성
│
└── README.md
```

---

## 🚀 시작하기 (Getting Started)

### 사전 요구사항 (Prerequisites)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (.NET 8 SDK 포함)
- [Unity Hub & Editor](https://unity.com/) 
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 서버 환경 설정 (Server Setup)

1. **환경 변수 설정**: `Server` 폴더 내에 `.env` 파일을 생성하고 다음 내용을 설정하세요.
    ```env
    POSTGRES_USER=myuser
    POSTGRES_PASSWORD=mypassword
    POSTGRES_DB=rhythmrpg
    REDIS_PASSWORD=myredispassword
    ```

2. **인프라 실행 (Docker)**:
    ```bash
    cd Server
    docker-compose up -d
    ```
    *PostgreSQL, Redis, ControlPlane 등이 실행됩니다.*

3. **서버 실행 (로컬 개발 시)**:
    - Visual Studio에서 `Server.sln`을 엽니다.
    - `PacketGenerator`를 먼저 빌드하여 패킷 코드를 생성합니다.
    - `ApiServer`, `GameServer` (속성에서 `--role Town` 또는 `--role Game` 인자 설정) 프로젝트를 시작합니다.

### 클라이언트 실행 (Client Setup)

1. Unity Hub에서 `Client` 폴더를 프로젝트로 추가하여 엽니다.
2. `Assets/Scenes/TitleScene` (또는 진입 씬)을 엽니다.
3. Play 버튼을 눌러 실행합니다.

---

## ✨ 주요 기능 (Features)

- **리듬 액션**: 음악 비트에 맞춘 정확한 판정 시스템 (BeatActionManager).
- **상태 동기화**: 서버 주도의 틱(Tick-Beat) 기반 상태 동기화로 공정한 멀티플레이 환경 제공.
- **월드 시스템**: 다수의 플레이어가 상호작용 가능한 마을(Town) 구현.
- **매치메이킹 & 룸**: ControlPlane을 통한 게임 세션 할당 및 파티 입장.

---
