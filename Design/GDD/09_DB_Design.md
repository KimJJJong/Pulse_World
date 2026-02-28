# 09. 데이터베이스 설계 (Database Design)

## 1. 개요 (Overview)
본 문서는 Pulse World 서버가 유저의 정보와 게임 상태를 영속적으로 보존하고, 실시간 매치메이킹 및 세션 상태를 빠르게 읽고 쓰기 위한 데이터베이스 아키텍처를 정의합니다.
성능과 데이터 무결성을 모두 확보하기 위해 **RDBMS(관계형 DB)**와 **In-Memory NoSQL(Redis)**을 혼합한 하이브리드 데이터베이스 구조를 사용합니다.

---

## 2. 데이터베이스 아키텍처 (DB Architecture)

현재 구축된 소스 구조(ApiDbContext.cs, RedisStore.cs)를 바탕으로 한 데이터 저장소 분리 정책입니다.

### 2-1. RDBMS (EF Core 기반)
*   **용도:** 절대 유실되어서는 안 되는 '영구적인(Persistent)' 유저 데이터 보관.
*   **주요 저장 데이터 (Master Data):**
    *   **계정 정보 (Account):** UID, 접속 암호화 토큰, 닉네임, 재화(골드, 맥류 가루).
    *   **인벤토리 (Inventory):** 보유 중인 장비(2_Equipments.csv 매핑 데이터), 소유한 맥석(Pulse Crystal) 정보.
    *   **성장 정보 (Progression):** 공명 레벨, 도면(Blueprint) 해금 여부, 스테이지 클리어 기록.
*   **특징:** API Server에서 Entity Framework Core(ApiDbContext)를 통해 관리되며, 트랜잭션(Transaction) 무결성을 최우선으로 합니다.

### 2-2. Redis (In-Memory NoSQL)
*   **용도:** 극도로 빠른 읽기/쓰기가 필요한 '휘발성(Ephemeral)' 데이터 및 서버 간 상태 공유.
*   **주요 저장 데이터 (Session & State):**
    *   **서버 레지스트리 (Registry):** 현재 떠 있는 Game Server들의 헬스체크 및 여유 CPU 상태.
    *   **유저 프레즌스 (Presence):** 특정 유저(UID)가 현재 어느 서버, 어느 방(Room)에 접속해 있는지에 대한 로컬리티 맵.
    *   **매치메이킹 대기열 (Waiting Room):** 파티를 구하는 유저들의 큐 테이블.
*   **특징:** Control Plane Server와 API Server가 공통으로 접근하여 분산 환경에서의 '단일 진실 공급원(SSOT)' 역할을 수행합니다.

---

## 3. 핵심 스키마 설계 (Entity Schema)

EF Core 기반 RDBMS에 들어갈 대표적인 플레이어 데이터 구조입니다. 

### 3-1. 유저 베이스 (PlayerEntity)
| 필드 (Column) | 타입 (Type) | 설명 (Description) |
|---|---|---|
| Uid (PK) | varchar(36) | 유저 고유 식별자 (GUID) |
| Nickname | varchar(20) | 인게임 닉네임 |
| ResonanceLevel | int | 타 RPG의 베이스 레벨에 해당하는 공명 레벨 |
| Gold | bigint | 보유 골드 (재화) |
| PulseDust | int | 맥류 가루 (재화) |
| LastLogin | datetime | 마지막 접속 시간 |

### 3-2. 장비 및 아이템 (InventoryEntity)
| 필드 (Column) | 타입 (Type) | 설명 (Description) |
|---|---|---|
| ItemUid (PK) | bigint | DB 발급 고유 아이템 일련번호 |
| OwnerUid (FK) | varchar(36) | 소유자 Uid |
| TemplateId | int | 2_Equipments.csv의 기준 ID (예: 100001) |
| EnhanceLevel | int | 장비 강화 수치 |
| IsBlueprint | bit(bool) | 해당 아이템이 완제품인지, 제작용 도면인지 구분 |

---

## 4. 데이터 플로우 및 캐싱 전략 (Data Flow & Caching)

실시간 데디케이티드 서버(Game Server) 특성상 RDBMS 부하를 줄이기 위한 캐싱(Caching) 전략입니다.

1.  **로그인 로드 (Login Load):** API Server 세션 연결 시 RDBMS에서 유저 정보를 끌어와 JWT와 함께 임시 메모리로 올립니다.
2.  **게임 룸 진입 (Enter Room):** Game Server(TCP)에 접속할 때, RDBMS를 직접 찌르지 않고 API 서버나 Redis에서 넘겨준 유저 스냅샷(캐시 데이터)을 기반으로 메모리 객체(Player)를 생성합니다.
3.  **인게임 저장 (Periodic Save):** 게임 플레이 도중 획득하는 재화나 장비는 메모리상에서만 증감시키고, 보스 클리어 직후 또는 플레이어가 마을(Town)을 떠날 때 비동기로 RDBMS에 한 번에 덤프(Bulk Update)합니다.

---

## 5. 향후 확장성 및 구조 개편 파이프라인 (Future Scalability)

게임 컴플렉시티 증가 및 운영 확장에 대비한 데이터베이스 장기 플랜입니다.

1.  **PVP 및 무한의 탑 리더보드 (Ranking DB):**
    *   향후 랭킹 시스템이 도입될 경우, RDBMS의 잦은 정렬 연산을 피하기 위해 **Redis의 Sorted Set (ZADD, ZRANGE)** 자료구조를 활용하여 실시간 전 서버 통합 랭킹보드를 구현합니다.
2.  **NoSQL 다큐먼트 저장소 도입 (Logging & Analytics):**
    *   유저의 모든 '맥석 교체 이력', '스킬 사용 빈도', '보스 사망 위치' 등 빅데이터 로그는 정형화된 RDBMS 대신 **MongoDB나 Elasticsearch** 같은 로그 수집용 NoSQL에 적재하여, 기획 및 밸런싱 분석 자료로 활용할 계획입니다.
3.  **데이터 샤딩 (Sharding):**
    *   유저 풀이 수백만 단위로 커질 경우, 지역적(아시아/북미) 분리가 아닌 Uid 해시값 기준으로 RDBMS 테이블을 쪼개는 수평적 샤딩을 분산 서버 아키텍처에 맞게 적용합니다.
