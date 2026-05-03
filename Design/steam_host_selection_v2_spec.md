# Steam Host Selection V2 Hybrid Spec

작성일: 2026-05-03  
대상 프로젝트: RhythmRPG  
문서 목적: 사용자가 제안한 Relay 절감 전략을 현재 RhythmRPG의 `서버 조율 + Host-authoritative Steam P2P + ServerRelay fallback` 구조에 맞게 재해석하고, 현재 구현과 후속 개선 방향을 `v2` 기준으로 정렬한다.

## 한 줄 결론

RhythmRPG에서 Relay를 줄이는 가장 현실적인 방법은 `모든 peer를 mesh로 묶는 것`이 아니라, **가장 많은 peer와 안정적으로 Steam 경로를 맺을 수 있는 Host를 뽑고, 각 peer별로 Steam 또는 ServerRelay fallback을 선택하는 것**이다.

즉, 이 프로젝트의 `v2` 핵심은 아래 한 문장이다.

- `전투 topology는 star를 유지하고, Host selection과 peer reachability 측정을 더 똑똑하게 만들어 Relay 비율을 줄인다.`

## 왜 V2인가

현재 코드 기준으로는 이미 다음 요소가 들어가 있다.

- Waiting Room에서 `HostSelectionReport` 수집
- peer 간 `measuredSteamPairs` 보고
- `MatchManifest`에 `HostUid`, `HostCandidateOrder`, `HostSelectionMode` 고정
- `P2PRelayRoom`의 failover가 candidate order 재사용
- runtime metric version이 `host-selection-v2-hybrid`

즉, 설계 문서 이름만 `v1`일 뿐, 실제 구현은 이미 `v2 hybrid` 방향으로 움직이고 있다.  
이번 문서는 새로운 이론을 추가하는 문서가 아니라, **현재 프로젝트에 맞는 운영 기준을 하나로 정리하는 최신 spec**이다.

## 현재 프로젝트의 전제

이 문서는 아래 전제를 바꾸지 않는다.

1. 전투 authority는 `Host Client`가 가진다.
2. 실시간 전투 패킷 흐름은 `Guest -> Host -> authoritative result`인 star topology를 유지한다.
3. `GameServer`는 완전히 사라지지 않고, 입장/세션/Host 변경 신호/결과 경계에 남는다.
4. `Steam P2P`는 1순위 실시간 경로이고, `ServerRelayComposite`는 fallback이다.
5. Relay는 실패가 아니라 비용이 큰 degraded path다.

## 사용자가 제안한 10개 항목과의 적합성

| 제안 | 적합성 | V2 적용 방식 | 비고 |
| --- | --- | --- | --- |
| Host 선정이 절반을 결정 | 매우 높음 | 그대로 채택 | 이 프로젝트는 Host-authoritative 전투라 Host 품질 영향이 매우 큼 |
| Direct 가능성 점수 사전 계산 | 높음 | `peer reachability score`로 재정의 | raw NAT 대신 `measuredSteamPairs + Steam readiness + route hint`를 사용 |
| Partial Mesh 전략 | 낮음 | 전투 topology에는 미채택 | 전투 authority가 Host에 있으므로 guest-guest mesh는 구조 충돌 |
| 연결 재시도 전략 | 높음 | 짧은 다중 재시도로 채택 | 현재 reconnect cadence는 존재하지만 더 빠른 start-window retry가 필요 |
| 포트 다양화 | 낮음 | OS UDP 포트 roulette는 미채택 | Steam Networking Sockets는 app-level port cycling과 잘 맞지 않음 |
| 지역 기반 매칭 | 중간~높음 | matchmaker/control-plane 후속 과제로 채택 | Host selection 자체보다 room composition 단계가 더 적합 |
| Relay 유저 분리 | 중간 | `host normal eligibility` 제한으로 채택 | 참가 자체를 막지 않고 Host 우선순위에서 밀어냄 |
| Steam Networking 옵션 튜닝 | 높음 | `DetailedStatus`, `FakeIP` 검토 | 현재 stack 안에서 확장 가능 |
| Telemetry | 매우 높음 | 필수 채택 | 개선 여부를 측정하려면 반드시 필요 |
| 3단계 구조 | 매우 높음 | 프로젝트 맞춤형 3단계로 채택 | full mesh가 아니라 `measure -> freeze -> failover` 구조로 변환 |

## V2의 핵심 해석

사용자가 말한 `Direct 연결 가능한 구조를 최대한 많이 만드는 것`은 RhythmRPG에서는 아래처럼 해석해야 한다.

1. `Host가 될 후보`는 가능한 많은 peer에게 Steam 경로를 안정적으로 제공해야 한다.
2. 하지만 `전투 중 guest-guest direct mesh`를 늘리는 것은 목표가 아니다.
3. 이 프로젝트에서 중요한 것은 `peer 간 full mesh gameplay`가 아니라 `peer -> host reachability`다.
4. 따라서 V2는 `Direct score`보다 더 넓은 개념인 `Reachability score`를 쓴다.

여기서 Reachability는 아래를 뜻한다.

- 측정된 Steam pair 연결 성공 여부
- 해당 pair의 RTT / connection quality
- stale하지 않은 최근 측정이 있는지
- Steam 경로가 없을 경우 ServerRelay fallback으로라도 수용 가능한지

## 왜 NAT / 공인 IP / 포트 매핑을 하드 규칙으로 두지 않는가

일반적인 UDP hole punching 설계에서는 `Open NAT`, `공인 IP`, `포트 매핑 가능`이 매우 중요하다.  
하지만 RhythmRPG의 현재 transport는 `Facepunch + Steam Networking Sockets + SDR` 위에 올라가 있으므로, 현재 코드가 직접 보는 관측값은 다음이다.

- Steam usable 여부
- Steam pair probe의 측정 결과
- Steam transport runtime quick status
- GameServer RTT / jitter / loss

즉, 이 프로젝트에서 `NAT 타입`은 현재 구조상 1차 입력이 아니라 **관측 불가능하거나 추상화된 상태**에 가깝다.  
V2는 NAT 이론 대신 `실측 가능한 결과`를 우선한다.

## V2 Path Taxonomy

V2는 pair 경로를 아래 4개로 다룬다.

| Path Type | 의미 | 현재 구현 가능 여부 |
| --- | --- | --- |
| `SteamMeasuredReachable` | Waiting Room pair probe나 runtime에서 Steam 경로가 실제 측정됨 | 가능 |
| `SteamProxyEstimated` | Steam usable은 맞지만 직접 pair 측정이 없어서 proxy RTT로 추정 | 가능 |
| `ServerRelayComposite` | Steam 경로를 못 쓰거나 pair 측정 실패, 대신 GameServer RTT 합성으로 추정 | 가능 |
| `Unavailable` | pair 품질을 추정할 수 없음 | 가능 |

추가로 future route hint는 아래처럼 다룬다.

| Future Hint | 의미 | 상태 |
| --- | --- | --- |
| `DirectLikely` | NAT traversal 등으로 direct route 가능성이 높음 | 후속 |
| `SDRLikely` | Steam Datagram Relay 경유 가능성이 높음 | 후속 |

현재 코드에는 direct vs SDR을 명시적으로 저장하는 필드가 없다.  
따라서 V2의 1차 목표는 `Direct를 억지로 판정하는 것`이 아니라, **실제 측정 기반 reachability를 정확히 반영하는 것**이다.

## V2의 3단계 구조

### 1. Waiting Room Measure

대기방에서는 full mesh에 가까운 `pair probe`를 돌리되, 목적은 gameplay mesh가 아니라 `Host selection input` 수집이다.

- 각 클라이언트는 `HostSelectionReport`를 3초 주기로 전송
- Steam usable 조건이면 peer별 `measuredSteamPairs`를 수집
- frame time, jitter, loss, server RTT도 함께 보고

이 단계의 목표는 `누가 direct host로 잘 붙느냐`를 NAT로 추측하는 것이 아니라, `누가 실제로 많은 peer와 안정적인 Steam pair를 만들 수 있느냐`를 수집하는 것이다.

### 2. Match Start Freeze

게임 시작 직전 서버는 아래를 한 번에 고정한다.

- `PreferredHostUid`
- `HostCandidateOrder[]`
- `HostSelectionMode`
- `HostSelectionMetricVersion`
- `HostSelectionScore`
- `MatchManifest`

중요한 것은 `게임 시작 전에 누가 Host인지 먼저 고정`한다는 점이다.  
전투가 시작된 뒤에 mesh를 다시 짜는 것이 아니라, **manifest를 통해 deterministic하게 freeze**한다.

### 3. Runtime Failover

전투 중 Host가 끊기면 `P2PRelayRoom`은 waiting room에서 계산된 `HostCandidateOrder`를 그대로 재사용한다.

- 현재 연결된 preferred candidate가 있으면 승격
- 없으면 다음 connected candidate로 failover
- 그래도 없으면 seat / actor 순 fallback

즉, V2는 `mesh routing`이 아니라 `ordered failover`를 핵심 구조로 삼는다.

## V2에서 채택하는 Host 선정 원칙

1. `측정된 Steam reachability`가 `추정된 proxy RTT`보다 우선한다.
2. `ServerRelayComposite`는 허용하지만 Host 선정에서는 분명한 감점 대상이다.
3. `Relay를 자주 요구하는 후보`는 normal mode Host에서 밀려야 한다.
4. `Owner`는 사회적 owner일 뿐이며, network quality가 비슷할 때만 tie-break에 사용한다.
5. room 전체가 degraded면 deterministic한 emergency fallback은 반드시 남겨둔다.

## V2 Candidate Inputs

V2에서 Host 후보 점수에 반영해야 하는 입력은 아래다.

| 입력 | 설명 |
| --- | --- |
| `MeasuredSteamReachablePairCount` | 실제 pair probe로 확인된 Steam 연결 수 |
| `MeasuredSteamReachabilityRatio` | 기대 peer 수 대비 실제 Steam 연결 비율 |
| `ProxySteamPairCount` | 실측이 아닌 proxy estimate로 처리한 pair 수 |
| `ServerRelayPairCount` | Server relay composite로만 처리 가능한 pair 수 |
| `AveragePairCost` | 후보 기준 평균 경로 비용 |
| `WorstPairCost` | 후보 기준 최악 경로 비용 |
| `HostCapacityPenalty` | frame time 기반 authority 수행 여유 |
| `SelectionReportFreshness` | 최근 보고 데이터의 freshness |

## V2 Hard Filters

아래 조건은 normal mode Host eligibility 기준이다.

1. `Ready == true`
2. `HostSelectionReport`가 freshness window 안에 있음
3. `p95FrameMs <= 33 ms`
4. 최소 peer coverage 충족
5. Steam room인데 `steamReady == false`인 후보는 normal mode에서 제외
6. 다른 후보가 충분한 measured reachability를 가지고 있는데, 특정 후보가 `ServerRelayComposite` 위주라면 normal mode에서 제외

단, 모든 후보가 degraded라면 `EmergencyFallback`는 유지한다.

## V2 점수 함수

V2는 기존 V1 계열 점수 위에 `reachability`와 `relay-heavy penalty`를 더 강하게 반영한다.

```text
CandidateCost =
    0.45 * AveragePairCost +
    0.20 * WorstPairCost +
    0.15 * RelayFallbackRatioPenalty +
    0.10 * ProxyEstimatePenalty +
    0.10 * HostCapacityPenalty
```

보조 정의:

```text
RelayFallbackRatioPenalty = ServerRelayPairCount / ExpectedPeerCount
ProxyEstimatePenalty      = ProxySteamPairCount / ExpectedPeerCount
```

tie-break 순서는 아래를 권장한다.

1. `RelayFallbackRatioPenalty`가 더 낮은 후보
2. `MeasuredSteamReachablePairCount`가 더 높은 후보
3. `WorstPairCost`가 더 낮은 후보
4. `Owner`
5. `uid` 사전순

## Direct Score를 이 프로젝트식으로 바꾸면

사용자가 말한 `Direct 가능성 점수`는 RhythmRPG에서는 아래처럼 바꾸는 것이 더 맞다.

### 원래 의미

- raw NAT / hole punching 성공 가능성

### RhythmRPG V2 의미

- `measuredSteamPairs`가 최근에 실제로 잡혔는가
- 잡혔다면 ping / quality가 수용 가능한가
- 잡히지 않았을 때 proxy estimate 비중이 얼마나 큰가
- 결국 Host가 되었을 때 room 전체가 relay-heavy가 되는가

즉, V2에서의 Direct score는 **`raw NAT 점수`가 아니라 `실측 Steam reachability 점수`**다.

## Partial Mesh를 왜 전투 topology에 넣지 않는가

사용자 제안 중 가장 크게 프로젝트와 어긋나는 부분은 `Partial Mesh 전략`이다.

RhythmRPG 전투는 다음 성질을 가진다.

- Host가 authoritative action / beat result를 확정
- Guest는 입력을 보내고 결과를 받는 구조
- 판정 일관성과 리듬 타이밍 정합성이 중요

이 구조에서 guest-guest gameplay mesh를 추가하면 아래 문제가 생긴다.

- authoritative source가 늘어나 정합성이 깨짐
- packet responsibility가 중복됨
- Host migration 복잡도가 급증함
- debug와 replay reason tracing이 어려워짐

따라서 V2는 `전투 mesh`를 채택하지 않는다.  
대신 아래만 채택한다.

- Waiting Room 측정 단계에서의 peer reachability mesh
- Host star topology 위에서의 per-peer Steam / ServerRelay fallback
- 추후 voice, emote, cosmetic sync 같은 비권위 채널에 한해 side-channel 검토

## 연결 재시도 전략은 어떻게 바꿀 것인가

이 항목은 프로젝트와 잘 맞는다.

현재 pair probe와 Steam guest reconnect는 존재하지만, start-time window 기준으로는 retry cadence가 다소 느리다.  
V2 권장안은 아래다.

1. match start 직후 Steam guest connect 1차 시도
2. `150 ms`, `300 ms`, `600 ms` 짧은 재시도
3. 그래도 gameplay-ready가 아니면 `ServerRelay` fallback 유지
4. background에서는 Steam recovery를 계속 관측 가능

중요한 점은 `처음 실패했다고 즉시 전체 match를 relay mode로 강등하지 않는 것`이다.

## 포트 다양화는 어떻게 볼 것인가

일반 UDP hole punching 시스템에서는 유효할 수 있다.  
하지만 현재 Steam stack에서는 `27015`, `27016`, `27017` 같은 OS-level port roulette를 앱에서 직접 돌리는 방식은 맞지 않는다.

V2에서 대신 보는 것은 아래다.

- Steam virtual port 분리 여부
- gameplay와 probe의 channel/virtual port 역할 구분
- reconnect timing
- FakeIP / route telemetry

즉, `포트 다양화`는 이 프로젝트에서는 `OS UDP port 다양화`가 아니라 `Steam-level channel/virtual port observability`로 해석한다.

## 지역 기반 매칭은 어디에 넣어야 하는가

이 항목은 Host selection calculator보다 matchmaker / room composition 단계가 더 적합하다.

적용 지점:

- `ControlPlane / room allocation`
- 친구방이 아닌 quick-match
- Steam ping location 또는 region bucket 사용

주의점:

- 친구 초대 기반 lobby에서는 강한 지역 제한보다 `예상 품질 경고`가 더 현실적이다.
- 지역 기반 매칭은 Host selection을 대체하지 않고, `나쁜 조합의 방 자체를 덜 만들기 위한 사전 필터`다.

## Relay 유저 분리 전략은 어떻게 적용하는가

이 항목도 그대로 강제하기보다는 프로젝트형으로 완화해서 적용해야 한다.

V2 규칙:

1. relay-heavy 유저도 room 참가 자체는 허용
2. 하지만 `normal Host eligibility`에서는 제외 가능
3. room 전체가 degraded일 때만 emergency fallback 후보로 복귀

즉, V2는 `relay 유저를 배제`하는 것이 아니라, **`relay-heavy 유저를 기본 Host 후보에서 밀어내고, room 전체 degraded일 때만 살린다`**가 맞다.

## Steam 옵션 튜닝 V2

현재 프로젝트에 바로 의미 있는 항목은 아래다.

1. `Connection.DetailedStatus()`를 로그/telemetry에 저장
2. direct vs SDR route hint를 future classifier로 활용
3. `FakeIP`는 future opt-in 과제로 검토
4. Steam usable이면 `Steam transport preferred` 유지
5. `SteamEligibleButRelaySelected`가 언제 발생하는지 원인 분류

이 항목은 새 네트워크 스택을 도입하지 않고도 현재 코드 위에서 확장 가능하다.

## V2 Telemetry

이 문서 기준으로 반드시 수집해야 할 운영 지표는 아래다.

| 항목 | 설명 |
| --- | --- |
| `room_size` | 방 인원 |
| `steam_ready_count` | Steam usable 참가자 수 |
| `measured_steam_pair_count` | 실측된 Steam pair 수 |
| `measured_steam_reachability_ratio` | 실측 Steam 도달 비율 |
| `proxy_steam_pair_ratio` | proxy estimate 비율 |
| `server_relay_pair_ratio` | server relay composite 비율 |
| `selected_host_uid` | 최종 Host |
| `host_selection_mode` | `FullMeasured`, `HybridMeasured`, `EmergencyFallback` 등 |
| `host_selection_score` | 최종 점수 |
| `candidate_order` | failover 포함 후보 순서 |
| `host_failover_reason` | preferred 승격 / failover / no_connected_candidate |
| `route_hint_direct_count` | future route classifier 결과 |
| `route_hint_sdr_count` | future route classifier 결과 |
| `region_bucket` | 매칭 지역 그룹 |

## V2 Network Debug 상세 보고

V2는 telemetry만 쌓는 것으로 끝나면 안 된다.  
실제 개발과 QA 단계에서는 **현재 방이 왜 이런 상태가 되었는지, 왜 특정 peer가 Steam 대신 Relay를 쓰는지, 왜 Host가 바뀌었는지**를 클라이언트 런타임에서 바로 읽을 수 있어야 한다.

즉, V2의 필수 요구사항은 아래다.

- `운영용 telemetry`와 별도로 `개발/QA용 Network Debug 상세 보고`를 제공한다.
- 단순 숫자 나열이 아니라 `상태 + 근거 + reason code`를 함께 보여준다.
- “현재 정상/비정상 여부”보다 “왜 이렇게 결정되었는가”를 읽을 수 있어야 한다.

### 목표

Network Debug는 아래 질문에 즉시 답할 수 있어야 한다.

1. 왜 이 유저가 Host가 되었는가
2. 왜 나는 Host가 아니었는가
3. 왜 이 peer는 Steam measured pair가 아니라 proxy estimate로 계산되었는가
4. 왜 이 peer는 ServerRelayComposite로 내려갔는가
5. 지금 전투 경로가 Steam인지 ServerRelay인지
6. 지금 연결이 direct-like인지 SDR-like인지, 아니면 아직 분류 불가인지
7. Host가 왜 바뀌었는가
8. 지금 문제의 원인이 `Steam 미초기화`, `측정 stale`, `coverage 부족`, `frame budget 초과`, `retry 중` 중 무엇인가

### 표시 위치

V2 기준 상세 보고는 아래 두 곳에 노출하는 것을 권장한다.

1. `Waiting Room / Room Debug`
   게임 시작 전 Host selection과 peer reachability 상태 확인
2. `In-Game / Network Debug`
   게임 시작 후 실제 transport 상태, fallback, failover 확인

### 필수 섹션

#### 1. Room Summary

방 전체 상태를 한 화면에서 요약해야 한다.

- `RoomId`
- `MatchId`
- `Room Size`
- `UseP2PRelay`
- `HostSelectionMode`
- `HostSelectionMetricVersion`
- `HostSelectionEpoch`
- `PreferredHostUid`
- `CurrentHostActorId`
- `HostCandidateOrder`

#### 2. Host Selection Summary

Host 선출 결과와 근거를 한 줄로 읽을 수 있어야 한다.

- `Selected Host`
- `Selection Score`
- `Selection Mode`
- `MeasuredSteamReachabilityRatio`
- `ServerRelayPairRatio`
- `ProxyEstimateRatio`
- `HostCapacityPenalty`
- `Primary Selection Reason`

`Primary Selection Reason` 예시:

- `BestMeasuredReachability`
- `LowerRelayFallbackRatio`
- `LowerWorstPairCost`
- `OwnerTieBreak`
- `EmergencyFallback`

#### 3. Per-Candidate Breakdown

각 Host 후보별 점수와 탈락 사유를 보여줘야 한다.

- `Uid`
- `Eligible`
- `CandidateCost`
- `AveragePairCost`
- `WorstPairCost`
- `AveragePairRttMs`
- `WorstPairRttMs`
- `MeasuredSteamPairCount`
- `ProxySteamPairCount`
- `ServerRelayPairCount`
- `UnavailablePairCount`
- `HostCapacityPenalty`
- `P95FrameMs`
- `DisqualifiedReasons[]`

핵심은 `탈락했는지`만이 아니라 `왜 탈락했는지`를 바로 보여주는 것이다.

대표 reason code:

- `NotReady`
- `MissingState`
- `SelectionReportStale`
- `InsufficientPeerCoverage`
- `FrameBudgetExceeded`
- `SteamNotReady`
- `RelayHeavyCandidate`

#### 4. Per-Peer Reachability Matrix

이 섹션은 V2에서 가장 중요하다.  
각 `Peer -> Candidate Host` 조합이 어떤 방식으로 평가되었는지 보여줘야 한다.

각 pair에 대해 아래를 표시한다.

- `PeerUid`
- `CandidateUid`
- `PathType`
- `Measured / Proxy / Relay`
- `AvgRttMs`
- `JitterMs`
- `LossPct`
- `ConnectionQualityLocal`
- `ConnectionQualityRemote`
- `ReportedAtMs`
- `Fresh / Stale`
- `PairReason`

`PairReason` 예시:

- `MeasuredSteamPair`
- `MeasuredSteamPairRemoteOnly`
- `ProxyEstimatedFromServerRtt`
- `SteamUnavailableFallbackToRelay`
- `MissingPeerMetrics`

#### 5. Runtime Transport Status

게임 시작 후 실제로 어떤 transport가 살아 있는지 보여줘야 한다.

- `TransportName`
- `NetworkStateSummary`
- `NetworkFlowSummary`
- `TransportDebugStatus`
- `IsHostLocal`
- `IsSteamTransport`
- `IsServerRelayTransport`
- `IsSteamTransportConnectedToHost`
- `SteamConnectedPeerCount`
- `TransportPairPingMs`
- `TransportPendingReliableBytes`
- `TransportPendingUnreliableBytes`
- `TransportSentUnackedReliableBytes`
- `TransportConnectionQualityLocal`
- `TransportConnectionQualityRemote`
- `TransportLastError`

#### 6. Retry / Fallback Timeline

연결 실패 직후 왜 fallback이 일어났는지 시간순으로 읽을 수 있어야 한다.

- `InitialConnectAttemptAtMs`
- `RetryCount`
- `LastRetryAtMs`
- `RetryBackoffStage`
- `SteamConnectSucceededAtMs`
- `FallbackActivatedAtMs`
- `FallbackReason`
- `RecoveryObservedAtMs`

`FallbackReason` 예시:

- `SteamConnectTimeout`
- `SteamNotInitialized`
- `HostSteamIdMissing`
- `GameplayReadyDeadlineExceeded`
- `TransportClosed`

#### 7. Host Migration / Failover Status

Host 변경은 반드시 별도 섹션이 필요하다.

- `CurrentHostActorId`
- `CurrentHostUid`
- `PreviousHostUid`
- `HostEpoch`
- `FailoverIndex`
- `FailoverReason`
- `CandidateOrderCursor`
- `ConnectedCandidates`

`FailoverReason` 예시:

- `promote_preferred`
- `preferred`
- `failover`
- `await_preferred_initial`
- `no_connected_candidate`

#### 8. Route Hint / Directness Hint

V2 future scope로 direct vs SDR 판별 힌트가 들어오면, 이것도 디버그에 표시해야 한다.

- `RouteHint`
- `DirectLikely / SDRLikely / Unknown`
- `DetailedStatusSnippet`
- `FakeIPAssigned`
- `RemoteAddressKnown`

중요한 점은 이 값이 `결정적 진실`이 아니라 `진단 힌트`라는 점을 UI에 명확히 표시하는 것이다.

### 반드시 필요한 UX 원칙

1. 숫자만 보여주지 말고 `문장형 reason`을 함께 보여준다.
2. stale 데이터를 fresh 데이터처럼 보이게 하면 안 된다.
3. 현재 선택 결과뿐 아니라 `탈락한 후보의 탈락 사유`도 읽을 수 있어야 한다.
4. Waiting Room 상태와 In-Game 상태를 분리해서 본다.
5. 한 화면에서 `현재 상태`, `결정 근거`, `최근 이벤트`가 함께 보여야 한다.

### 권장 로그 문장

디버그 UI와 로그에는 아래 수준의 문장이 남는 것이 좋다.

- `Host selected: u_owner because measured reachability 3/3, relay ratio 0.00, worst pair RTT 46 ms`
- `Guest u_b fell back to ServerRelay because Steam pair measurement missing and gameplay-ready deadline exceeded`
- `Host failover: u_owner -> u_guest_a because previous host disconnected and next preferred candidate was connected`
- `Candidate u_guest_b disqualified because SelectionReportStale + FrameBudgetExceeded`

### 현재 코드 기준 노출 대상 파일

| 영역 | 현재 파일 | 역할 |
| --- | --- | --- |
| Waiting Room Host selection debug | `Client/Assets/0.MainProject/01_Net/Room/UI/RoomUiController.cs` | 시작 전 상태 요약 |
| In-game network debug summary | `Client/Assets/3.Script/NetWork/GameServer/Z.ForNetWork/PingManager.cs` | 런타임 네트워크 상태 요약 |
| Host / transport debug window | `Client/Assets/0.MainProject/04_Runtime/Game/Managers/P2PHostLogWindow.cs` | Host selection과 transport 세부 표시 |
| Runtime transport state source | `Client/Assets/0.MainProject/04_Runtime/Game/Managers/P2PRelayClientBridge.cs` | 실제 transport / role / RTT / queue 상태 |
| Steam runtime state source | `Client/Assets/0.MainProject/04_Runtime/Game/Managers/SteamP2PClientTransport.cs` | Steam 연결 상세 상태 |

## 현재 코드 기준 적용 파일

| 영역 | 현재 파일 | V2 변경 방향 |
| --- | --- | --- |
| Host selection 계산 | `Server/ApiServer/2.Domain/WaitingRoom/HostSelectionV1Calculator.cs` | 파일명은 임시 유지 가능, 로직은 V2 기준으로 보정 |
| 대기방 transport 상태 저장 | `Server/ApiServer/2.Domain/WaitingRoom/WaitingRoomService.cs` | route hint / ratio 입력 확장 |
| WS DTO 계약 | `Server/ApiServer/5.Presentation/WebSockets/RoomWebSocketHandler.cs` | telemetry payload 전파 |
| 클라이언트 report 송신 | `Client/Assets/0.MainProject/01_Net/Room/RoomWsClient.cs` | retry / telemetry 확장 |
| pair probe | `Client/Assets/0.MainProject/01_Net/Room/RoomSteamPairProbeService.cs` | measured pair + detail hint 강화 |
| Steam transport | `Client/Assets/0.MainProject/04_Runtime/Game/Managers/SteamP2PClientTransport.cs` | `DetailedStatus`, FakeIP 검토, fast retry |
| manifest 확정 | `Server/ApiServer/2.Domain/GameMatch/GameMatchService.cs` | V2 score와 order 고정 |
| runtime failover | `Server/GameServer/Rooms/P2PRelayRoom.cs` | waiting room order 재사용 유지 |
| network debug UI | `Client/Assets/0.MainProject/04_Runtime/Game/Managers/P2PHostLogWindow.cs` | candidate / pair / fallback 상세 보고 추가 |
| runtime network summary | `Client/Assets/3.Script/NetWork/GameServer/Z.ForNetWork/PingManager.cs` | 현재 상태와 reason 요약 추가 |

## 구현 우선순위

### 1단계: 문서와 버전 정렬

- V2 spec를 기준 문서로 고정
- V1 문서는 historical reference로 유지
- `host-selection-v2-hybrid`와 문서 이름 불일치 해소

### 2단계: Network Debug 상세 보고 추가

- Waiting Room debug에 selection summary / candidate breakdown 추가
- In-game debug에 runtime transport / fallback / failover 추가
- pair reachability matrix 표시
- reason code와 문장형 설명 동시 표시

### 3단계: 관측성 강화

- `DetailedStatus()` route hint 수집
- host selection telemetry 수집
- relay-heavy room 비율 측정

### 4단계: 점수식 개선

- `MeasuredSteamReachabilityRatio` 반영
- `RelayFallbackRatioPenalty` 강화
- relay-heavy 후보의 normal mode 제외

### 5단계: 시작 직후 연결 튜닝

- 빠른 재시도
- immediate hard fallback 지연
- background recovery 관측

### 6단계: 매칭 단계 최적화

- quick match region bucket
- Steam ping location 활용 검토
- degraded room 구성 비율 감소

## 최종 결론

사용자가 제안한 방향의 핵심 취지는 이 프로젝트에도 매우 잘 맞는다.  
다만 RhythmRPG는 `generic UDP mesh game`이 아니라 `server-coordinated, host-authoritative Steam hybrid game`이므로, 아래처럼 바꿔 받아들여야 한다.

1. `Direct/NAT 이론`보다 `실측 Steam reachability`를 우선한다.
2. `Partial mesh gameplay`는 버리고 `star topology + better host selection`을 택한다.
3. `Relay 유저 배제`가 아니라 `relay-heavy 후보의 Host 우선순위 하향`으로 처리한다.
4. `포트 다양화`보다 `Steam route telemetry + retry tuning`이 더 중요하다.
5. region matching은 Host selection이 아니라 room composition의 후속 과제로 둔다.

즉, RhythmRPG의 V2 핵심은 아래 한 줄로 정리된다.

- `Relay를 없애는 것이 목표가 아니라, Host-star 구조 안에서 Steam reachability가 가장 좋은 후보를 뽑아 Relay가 전체 room 품질을 지배하지 못하게 만드는 것`
