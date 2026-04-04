# ?? 펄스 월드: 범용 틱(Tick) 표준 아키텍처 가이드라인

이 문서는 "리듬 RPG" 엔진 개발에 있어 서버와 클라이언트가 참조해야 할 유일무이한 **시간의 절대 기준(Absolute Time Standard)**을 명세합니다. 이 규격에 벗어나는 모든 초(Seconds), 프레임(deltaTime) 변수는 버그 및 동기화 실패의 원인이 되므로 일절 금지됩니다.

---

## 1. 최상위 규칙: PPQ 480 (1 박자 = 480 틱)

이 게임의 모든 기능(사운드, 공격, 버프, 이펙트 유지, 쿨타임)에서 사용되는 최소 시간 단위는 **Tick(틱)**입니다. 

> [!IMPORTANT]
> **표준 규격 (PPQ=480)**
> * `1 박자 (정박, 1 Beat)` = **480 Ticks**
> * `0.5 박자 (8분음표)` = **240 Ticks**
> * `0.25 박자 (16분음표)` = **120 Ticks**
> * `3연음 (1/3 박자)` = **160 Ticks**

### 1-1. 초(Seconds)나 밀리초(Ms)의 사용 금지
* `Task.Delay(100)`, `Time.deltaTime`, `Life = 1.25f` 등 물리적 시간에 의존하는 코드는 클라이언트 시뮬레이션(에디터 전용, 비동기 디버그)을 제외한 **핵심 게임플레이 로직에서 사용이 엄격히 금지**됩니다. 
* 모든 물리적 타이머는 `RhythmClient.GetCurrentBeatIndex()` 배열 또는 `RhythmSystem.GetCurrentTick()` 등의 절대 위치값을 빼서 계산해야 합니다.

---

## 2. 서버 적용 가이드 (Server Protocol & DB)

서버에서 송수신되는 스킬, 아이템, 버프 데이터는 `Beats(int)`를 쓰던 방식을 전면 개편하여, 보다 세밀한 쪼개기가 가능한 **`Ticks(int)`**로 교체합니다.

> [!CAUTION]
> **[구 규격 - 사용 불가]**  
> `DurationBeats = 1` (무조건 정수 단위의 박자만 처리 가능, 엇박 적용 불가)  
> `ActionWindowMs = 200` (음악이 느려지든 빨라지든 판정 시간 200ms 고정 - 불합리함)

> [!TIP]
> **[신 규격 - 표준안]**  
> `DurationTicks = 480` (직관적 대조: 1박자는 480틱. 여기서 반으로 가르고 1/3로 자를 수 있음)  
> `ActionWindowTicks = 120` (음악이 느려지면 120틱이 차지하는 초 단위 시간이 늘어나 유저에게 관대한 판정 부여)

### 2-1. 데이터 구조체 변화 (Struct / JSON)
```csharp
// [기존 패킷]
public int DurationBeats; // 스킬 동작 시간

// [신규 패킷]
public int DurationTicks; // 스킬 동작 시간
public List<int> HitDelayTicks; // 쌍검/활처럼 다단히트가 필요한 무기의 타격 발생 시간들 (예: [0, 240, 480])
```

---

## 3. 클라이언트 적용 가이드 (Client Unity)

클라이언트는 서버에서 내려준 **Tick 데이터**를 모조리 가져와 화면과 사운드를 렌더링하는 거대한 '음향 스피커'이자 '모니터' 역할을 수행합니다. 모든 시각화 및 청각화는 BPM의 영향을 자동 계산하여 재생되어야 합니다.

### 3-1. 절대 시간 계산 공식 (Tick -> Milliseconds)
실제 유니티 엔진이나 FMOD 사운드 카드에 예약을 걸 때(DSP setDelay 등)는 어쩔 수 없이 초(Sec/Ms)단위 변환이 필요합니다. 이때 아래 공식을 유일한 기준으로 사용합니다.

```csharp
// 서버에서 받아온 정보를 동적 BPM으로 환산하는 단축 공식
long beatDurationMs = RhythmClient.Instance.GetBeatDurationMs(); 
long msDuration = (long)((TargetTicks / 480.0) * beatDurationMs);

// 사용 예시 (애니메이션, FMOD)
FMOD_Delay(msDuration);
Visual_AnimSpeed = BaseAnimLength / msDuration;
```

### 3-2. 애니메이션 (Animation / VFX) 속도 스케일링
`BoardView`나 `EntityVisual`에서 재생되는 코루틴 비율 등은 `%`(Float)나 고정된 `Time.time`을 기준으로 끊을 수 없습니다. 타겟 틱까지 얼마나 남았는지를 기반으로 역으로 현재 재생률(`Speed`)을 보정해야 합니다. (이 부분은 향후 ActionSequencer에서 전담)
