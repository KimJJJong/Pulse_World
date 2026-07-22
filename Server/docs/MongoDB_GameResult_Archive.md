# MongoDB 게임 결과 아카이브

## 1. 도입 목적

MongoDB는 계정, 재화, 인벤토리를 대체하지 않습니다. 이 데이터는 계속 PostgreSQL 트랜잭션으로 관리합니다.

MongoDB의 책임은 다음과 같습니다.

- 매치 결과의 장기 보존
- 플레이어 및 스테이지별 전적 조회
- 판정, 네트워크, 스킬 사용량처럼 구조가 자주 바뀌는 텔레메트리 보관
- 중복 제출 및 서로 다른 결과 재제출 탐지
- 밸런싱 분석과 운영 조사에 필요한 원본 증거 제공
MongoDB를 선택한 이유가 "검증 엔진"이기 때문은 아닙니다. 입력값·플레이 시간 검증은 GameServer와 ApiServer가 수행하고, MongoDB는 검증이 끝난 결과와 조사 가능한 텔레메트리 원본을 유연한 문서로 보존합니다. `_id` unique 제약과 payload hash는 저장 무결성을 보조합니다. 일반 애플리케이션 로그 전체를 이 컬렉션에 넣지는 않습니다.


Redis는 방, 세션, 티켓, 빠른 중복 검사와 보상 집계 projection을 담당합니다. MongoDB는 만료되지 않는 분석 원본을 담당합니다.

## 2. 전체 구조

```mermaid
flowchart LR
    GS["GameServer"] -->|"POST /api/game/result"| API["ApiServer"]
    API -->|"immutable insert"| MONGO[("MongoDB\ngame_results")]
    API -->|"7일 캐시 + pending outbox"| REDIS[("Redis")]
    REDIS -->|"재시도 대상"| WORKER["GameResultArchiveReconciler"]
    WORKER -->|"idempotent insert"| MONGO
    API -->|"계정/재화/인벤토리"| PG[("PostgreSQL")]
```

저장 순서는 다음과 같습니다.

1. GameServer가 고유한 `MatchId`와 결과를 ApiServer에 제출합니다.
2. ApiServer가 같은 `MatchId`의 payload fingerprint를 계산합니다.
3. Redis Lua 스크립트가 결과 본문(`SET NX`, 7일)과 Mongo pending 등록(`SADD`)을 원자적으로 처리합니다.
4. 이미 같은 `MatchId`가 있으면 fingerprint를 비교하여 동일 재전송은 허용하고 다른 결과는 HTTP 409로 거부합니다.
5. 승리한 payload를 MongoDB에 immutable insert합니다.
6. Mongo 저장과 모든 플레이어의 Redis projection이 모두 성공한 뒤에만 pending 항목을 제거합니다.
7. MongoDB가 일시적으로 꺼져 있으면 pending을 유지하고 `PendingRetry`를 반환합니다.
8. 백그라운드 reconciler가 기본 15초마다 `SSCAN`으로 제한된 batch만 읽어 Mongo 저장과 누락된 projection을 함께 재시도합니다.
9. Redis 보상 ledger는 `game_reward_ledger_v2:*` 키와 30일 rolling sorted set 두 개(전체/클리어)를 Lua로 정리하므로 재시도에 안전하고 활성 사용자도 무한히 커지지 않습니다. 기존 무기한 누적 ledger와 키를 분리해 배포 시 오래된 count가 섞이지 않습니다.

## 3. MatchId가 필요한 이유

`RoomId`는 같은 파티가 여러 판에 재사용할 수 있습니다. 따라서 MongoDB의 `_id`와 멱등키는 `RoomId`가 아니라 매번 새로 생성되는 `MatchId`입니다.

- `MatchId`: 한 번의 게임 실행을 식별하는 영구 고유키
- `RoomId`: 같은 파티 또는 대기방을 묶어 조회하기 위한 논리키
- `RelayId`: GameServer 내부 routing key이며 `p2p:` 접두사가 붙을 수 있음

`MatchId`는 필수입니다. 정상 경로에서는 manifest의 실제 `MatchId`를 사용하며, manifest를 불러오지 못한 GameServer 방도 방 인스턴스마다 별도의 고유 fallback ID를 한 번 생성해 고정합니다. 재사용 가능한 `RoomId`를 `_id`로 사용하지 않습니다.

## 4. MongoDB 문서 구조

컬렉션 이름은 기본적으로 `game_results`입니다.

```json
{
  "_id": "room-17:1784628000000",
  "schemaVersion": 1,
  "payloadHash": "sha256-v1:...",
  "roomId": "room-17",
  "mapId": "Game_Forest_01",
  "hostUid": "user-1",
  "hostActorId": 1,
  "isClear": true,
  "reportedPlayTimeMs": 181000,
  "verifiedPlayTimeMs": 179800,
  "totalDamage": 95000,
  "playerUids": ["user-1", "user-2"],
  "submittedAtMs": 1784628180000,
  "storedAtMs": 1784628180100,
  "archivedAtUtc": "2026-07-21T10:03:00Z",
  "telemetry": {
    "rhythm": {
      "perfect": 120,
      "good": 18,
      "miss": 3
    },
    "network": {
      "averageRttMs": 72,
      "transport": "steam-relay"
    }
  }
}
```

`telemetry`는 선택 필드입니다. 중첩 객체와 배열을 그대로 받을 수 있으므로 새로운 분석 필드를 추가할 때마다 PostgreSQL migration을 만들 필요가 없습니다. 중요한 공통 필드는 검색과 검증을 위해 문서 최상위의 고정 필드로 유지합니다.

`payloadHash`에는 서버가 확정한 결과 필드, `SubmittedAtMs`, 정렬된 플레이어 목록과 canonical telemetry가 들어갑니다. ApiServer의 `StoredAtMs`와 Mongo의 `ArchivedAtUtc`만 재시도마다 달라질 수 있으므로 hash에서 제외합니다.

## 5. 인덱스

ApiServer의 백그라운드 서비스가 시작 시 다음 인덱스를 idempotent하게 생성합니다.

| 이름 | 키 | 용도 |
| --- | --- | --- |
| Mongo 기본 `_id_` | `_id = MatchId` unique | 중복 방지와 단건 조회 |
| `ix_player_stored_at` | `playerUids + storedAtMs desc` | 플레이어 최근 전적 |
| `ix_map_clear_stored_at` | `mapId + isClear + storedAtMs desc` | 맵별 클리어 분석 |
| `ix_room_stored_at` | `roomId + storedAtMs desc` | 같은 방의 여러 매치 조회 |

게임 결과는 운영 증거이므로 TTL 인덱스를 만들지 않았습니다. 보존 기간이 짧은 세부 이벤트를 추가한다면 별도 `gameplay_events` 컬렉션과 BSON Date 타입의 `expireAtUtc` TTL 인덱스를 사용합니다.

## 6. 로컬 실행

### 환경 파일 준비

```powershell
cd Server
Copy-Item .env.example .env
```

`.env`에서 다음 값을 변경합니다.

```dotenv
MONGO_ROOT_USERNAME=root_admin
MONGO_ROOT_PASSWORD=change-me-mongo-root-password
MONGO_APP_DATABASE=rhythm_analytics
MONGO_APP_USERNAME=rhythm_api
MONGO_APP_PASSWORD=change-me-mongo-app-password
MONGO_PORT=27017
SYSTEM_API_KEY=replace-this-with-a-random-secret-at-least-32-characters
```

로컬 connection string에 들어가는 비밀번호는 URI-safe 문자만 사용하거나 URL encoding 해야 합니다. 운영에서는 root 계정이 아니라 `rhythm_api`처럼 대상 DB에만 `readWrite` 권한이 있는 계정을 사용합니다.

### 실행 및 상태 확인

```powershell
docker compose up -d --build
docker compose ps
docker compose logs -f mongodb apiserver
```

MongoDB와 Redis 포트는 안전을 위해 `127.0.0.1`에만 노출됩니다. Compose 내부 ApiServer는 각각 `mongodb:27017`, `redis:6379`로 연결합니다.

Redis는 Mongo outbox 유실 가능성을 낮추기 위해 AOF(`appendfsync everysec`)와 named volume을 사용합니다. `.env`의 `SYSTEM_API_KEY`가 32자 미만이거나 placeholder이면 ApiServer와 GameServer는 시작을 거부합니다.

## 7. 결과 저장 API

서버 간 요청이므로 `X-Server-Secret` 헤더가 필요합니다.

```powershell
$headers = @{
    "X-Server-Secret" = "your-random-system-api-key-at-least-32-chars"
}
$nowMs = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

$body = @{
    MatchId = "room-17:$nowMs"
    RoomId = "room-17"
    MapId = "Game_Forest_01"
    HostUid = "user-1"
    HostActorId = 1
    IsClear = $true
    ReportedPlayTimeMs = 181000
    VerifiedPlayTimeMs = 179800
    TotalDamage = 95000
    PlayerUids = @("user-1", "user-2")
    SubmittedAtMs = $nowMs
    Telemetry = @{
        rhythm = @{
            perfect = 120
            good = 18
            miss = 3
        }
        network = @{
            averageRttMs = 72
            transport = "steam-relay"
        }
    }
} | ConvertTo-Json -Depth 8

Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:5000/api/game/result" `
    -Headers $headers `
    -ContentType "application/json" `
    -Body $body
```

응답의 `ArchiveStatus` 의미는 다음과 같습니다.

| 값 | 의미 |
| --- | --- |
| `Stored` | MongoDB에 새 문서 저장 완료 |
| `AlreadyExists` | 동일 결과의 안전한 재전송 |
| `PendingRetry` | MongoDB 장애로 Redis outbox에서 재시도 예정 |
| `Disabled` | Mongo 기능이 설정에서 꺼짐 |

동일 `MatchId`로 피해량 등 의미 있는 내용이 다른 payload를 보내면 HTTP 409가 반환됩니다. `MatchId`, `RoomId` 등 식별자는 최대 256자, 플레이어는 최대 64명, 직렬화된 전체 결과는 최대 1 MB입니다. `SubmittedAtMs`는 양수 Unix millisecond 값이면서 ApiServer 현재 시각의 ±5분 범위여야 하므로 GameServer와 ApiServer의 시스템 시각을 동기화해야 합니다.

## 8. 조회 API

시스템 secret 또는 사용자 Bearer token이 필요합니다.

```text
GET /api/game/result/{matchId}
GET /api/game/result/history?uid={uid}&mapId={optionalMapId}&limit=20
```

일반 사용자는 자신이 참가한 매치와 자신의 history만 읽을 수 있습니다. 시스템 요청은 운영 도구를 위해 모든 사용자를 조회할 수 있습니다. `limit`은 설정의 `MaxHistoryLimit`까지 제한됩니다.

## 9. mongosh로 직접 확인

```powershell
docker compose exec mongodb mongosh `
    --username rhythm_api `
    --password change-me-mongo-app-password `
    --authenticationDatabase rhythm_analytics `
    rhythm_analytics
```

접속 후:

```javascript
db.game_results.find().sort({ storedAtMs: -1 }).limit(5)
db.game_results.find({ playerUids: "user-1" }).sort({ storedAtMs: -1 })
db.game_results.findOne({ _id: "room-17:1784628000000" })
db.game_results.getIndexes()
```

## 10. 설정값

`ApiServer/appsettings.json`의 `Mongo` 섹션을 사용합니다.

| 설정 | 기본값 | 설명 |
| --- | --- | --- |
| `Enabled` | `false` | 직접 `dotnet run` 시 안전한 기본값. Compose에서 `true`로 override |
| `ConnectionString` | `mongodb://localhost:27017` | MongoDB 접속 문자열 |
| `DatabaseName` | `rhythm_analytics` | 분석 DB |
| `GameResultsCollection` | `game_results` | 결과 컬렉션 |
| `ConnectTimeoutSeconds` | `5` | 새 연결 수립 제한 시간 |
| `ServerSelectionTimeoutSeconds` | `5` | 사용할 Mongo 노드를 찾는 제한 시간 |
| `OperationTimeoutSeconds` | `5` | 결과 insert와 조회의 전체 제한 시간 |
| `WriteConcernTimeoutSeconds` | `5` | majority 확인 제한 시간 |
| `ReconcileIntervalSeconds` | `15` | pending 재처리 간격 |
| `ReconcileBatchSize` | `100` | 한 주기에 처리할 최대 결과 수 |
| `MaxHistoryLimit` | `100` | history API 최대 문서 수 |

Compose 밖에서 ApiServer만 실행하려면 환경변수로 활성화합니다.

```powershell
$env:Mongo__Enabled = "true"
$env:SystemApiKey = "your-random-system-api-key-at-least-32-chars"
$env:Mongo__ConnectionString = "mongodb://rhythm_api:password@localhost:27017/rhythm_analytics?authSource=rhythm_analytics"
dotnet run --project ApiServer/ApiServer.csproj
```

## 11. 장애 및 복구

- MongoDB만 장애: API는 Redis에 결과와 pending 항목을 원자적으로 보관하고 `PendingRetry`를 반환합니다.
- MongoDB 복구: reconciler가 pending 결과와 누락된 Redis projection을 제한된 batch로 복구한 뒤 pending에서 제거합니다.
- HTTP 응답 유실: GameServer는 최초 승인된 결과 스냅샷을 고정하므로 같은 `MatchId`와 완전히 같은 payload로 다시 보냅니다. 방마다 하나의 전송만 실행하며 최대 5회 지수 백오프로 재시도합니다. 모두 실패하면 오류를 남기고 플레이어가 영구 정지하지 않도록 Town 복귀를 진행합니다.
- 만료, 손상 또는 payload 충돌: 항목을 `game_results:archive_dead_letter` set으로 이동하고, 가능한 원문은 별도 dead-letter payload 키에 30일 보존한 뒤 원래 Redis 결과를 제거합니다.
- Redis projection 도중 장애: pending이 남고 reconciler가 다시 실행합니다. 30일 rolling sorted set이 같은 `MatchId`를 멱등 처리하며 오래된 항목과 count를 함께 제거합니다.
- MongoDB와 Redis 동시 장애: 요청은 실패합니다.
- MongoDB 장애가 7일을 넘김: Redis 본문이 만료될 수 있습니다. 운영 환경에서는 replica set/Atlas와 durable message broker 또는 PostgreSQL outbox를 추가해야 합니다.

초기화 스크립트는 Mongo volume이 비어 있을 때 한 번만 실행됩니다. `.env`에서 사용자나 비밀번호를 바꿔도 기존 volume의 계정은 자동 변경되지 않습니다. 기존 데이터를 유지해야 하면 mongosh로 사용자를 수정하십시오. 로컬 데이터를 모두 지워도 되는 경우에만 `docker compose down -v`를 사용합니다.

## 12. 운영 주의사항

- 운영 MongoDB는 단일 Docker 컨테이너가 아니라 replica set 또는 MongoDB Atlas를 사용합니다.
- TLS와 secret manager를 사용하고 connection string을 로그로 출력하지 않습니다.
- 앱 계정에는 분석 DB의 `readWrite`만 부여합니다.
- `telemetry`에 access token, IP 원문, 이메일과 같은 민감정보를 넣지 않습니다.
- 스키마 변경 시 기존 필드를 덮어쓰기보다 `schemaVersion`을 올리고 읽기 호환성을 유지합니다.
- Mongo 결과만 보고 재화나 아이템을 직접 지급하지 않습니다. 지급은 계속 PostgreSQL 트랜잭션에서 수행합니다.

공식 참고 자료:

- [MongoDB C#/.NET Driver](https://www.mongodb.com/docs/drivers/csharp/current/)
- [MongoDB Indexes](https://www.mongodb.com/docs/manual/indexes/)
- [MongoDB Docker Official Image](https://hub.docker.com/_/mongo)

