# 00. 코딩 컨벤션 및 데이터 규약 (Coding Convention & Data Protocols)

> [!IMPORTANT]
> 본 문서는 **RhythmRPG** 프로젝트의 서버-클라이언트-기획 간 데이터 정합성을 유지하기 위한 **최상위 규약 문서**입니다.
> 새로운 오브젝트, 아이템, 몬스터 등을 기획 및 추가할 때 반드시 본 문서의 ID 규칙과 경로 스펙을 준수해야 합니다.

---

## 1. Entity 정의 및 스키마 (Entity Definitions)

**Entity**는 우리 프로젝트에서 고유 식별자(ID)를 가지며 게임 내 기능적, 시각적 실체를 갖는 모든 객체를 의미합니다.

### 1.1 서버-클라이언트 Entity 구조
현재 프로젝트는 **Single Source of Truth**를 지향하며, Entity ID 하나로 클라이언트 리소스와 서버의 데이터 테이블이 1:1 매칭됩니다.

*   **클라이언트 (Unity):** `EntityDefinitionSO` (ScriptableObject) 형태로 비주얼 데이터(Prefab, Animator)와 함께 관리됨. 
    `EntityIdDefine.cs`에서 정수형 ID 기반으로 로직 분기.
*   **서버 (C# Server):** `EntityDataManager.cs`를 통해 JSON 파일(`EntityData.json`)로부터 스펙(MaxHp, EntityType 등)을 로드하여 검증.

> [!TIP]
> **데이터 동기화:** 기획의 최상단 원본은 `Design/DataTables/` 내의 `.csv` 명세들이며, 이것이 곧 서버의 JSON과 클라이언트의 SO(ScriptableObject)로 매핑되어 사용됩니다.

---

## 2. 데이터 식별 규약 (Data ID Rules)

기존 알파벳 접두어(C001, M001 등) 방식에서 **정수형(Integer) ID 체계**로 마이그레이션된 현재의 스펙입니다.
클라이언트 코드의 `EntityIdDefine.cs` 클래스에 명시된 규칙과 정확히 일치하며 하드코딩된 대역폭을 따릅니다.

| Category (분류) | ID Range (시작) | ID Range (끝) | 클라이언트 분류 함수 | 비고 (Description) |
| :--- | :--- | :--- | :--- | :--- |
| **System/Reserved** | `0` | `9` | - | 시스템 예약 및 테스트용 더미 객체 |
| **Player (플레이어)** | `10` | `999` | `IsPlayer(id)` | 플레이어 캐릭터 모델 및 스킨 |
| **Monster (몬스터)** | `1,000` | `99,999` | `IsMonster(id)` | 일반 몬스터, 엘리트, 보스 |
| **Weapon (무기)** | `100,000` | `199,999` | `IsWeapon(id)` | 무기 아이템 및 무기 모델 (Active Skill Bind) |
| **Armor (머리)** | `200,000` | `209,999` | `IsHead(id)` | 투구, 모자 등 |
| **Armor (상하의)** | `210,000` | `229,999` | `IsBody(id)`, `IsPants(id)`| 갑옷 및 하의 장비 |
| **Armor (장갑/신발)**| `230,000` | `249,999` | `IsGloves(id)`, `IsShoes(id)`| 장갑 및 신발류 장비 |
| **Accessory (장신구)**| `300,000` | `399,999` | `IsAccessory(id)` | 반지, 목걸이 등 (250,000~299,999는 기타 Armor 여유분) |
| **Consumable (소비)**| `400,000` | `499,999` | `IsConsumable(id)` | 포션, 버프 주문서 등 소비성 아이템 |
| **Material/Etc (재료)**| `500,000` | `599,999` | `IsMaterial(id)` | 강화 재료, 퀘스트 아이템 및 기타 화폐 류 |
| **Player Skill (유저 스킬)**| `600,000` | `699,999` | `IsPlayerSkill(id)` | 무기/장신구 바인딩용 액티브 및 패시브 스킬 정의 |
| **Monster Skill (몹 스킬)**| `700,000` | `799,999` | `IsMonsterSkill(id)` | 몬스터 패턴 내장 스킬 (장판, 돌진, 타격 등 범용 엔티티) |
| **System/Trap (함정)**| `800,000` | `849,999` | `IsTrapSkill(id)` | 맵 낙뢰, 환경 데미지 장판 등 시스템 스킬 |

> [!WARNING]
> 표에 기재되지 않은 대역폭(예: 850,000 이상)은 현재 미할당 구역입니다. 유지보수를 위해 대역폭 확장이 필요할 시 활용합니다.

---

## 3. 리소스 셋팅 및 경로 규약 (Resource Paths)

`EntityID`를 부여받은 기획 데이터가 실제 인게임 리소스로 할당되기까지의 경로(Path) 규칙입니다. 
클라이언트의 팩토리(Factory) 로직은 이 경로를 기반으로 에셋을 로드합니다.

### 3.1 📂 Data Tables & Config (기획 원본)
- **위치:** `Design/DataTables/`
- **구조:** `1_Common_Items.csv`, `2_Equipments.csv`, `3_Drop_Groups.csv` 등 넘버링 및 카테고리화.
- **서버 JSON 적재 경로:** `Server/GameServer/Content/01.Game/Entity/Json/EntityData.json`

### 3.2 📂 Prefabs & Models (클라이언트 리소스)
클라이언트에서 `GetResourceFolderPath(id)`를 호출할 때 반환되는 폴더 기준. Addressables 나 Resources 폴더 내 아래 구조를 따릅니다.
*   `Entities/Player/` - 10 ~ 999 ID 대역의 모델/프리팹
*   `Entities/Monster/` - 1000 ~ 99999 ID 대역
*   `Entities/Weapon/` - 100000 대역
*   `Entities/Armor/` - 200000 대역
*   `Entities/Accessory/` - 300000 대역
*   `Entities/Consumable/` - 400000 대역
*   `Entities/Material/`, `Entities/Etc/` - 나머지 재료 및 아이템

### 3.3 📂 Audio (음원 및 SFX)
> [!NOTE]
> 🎧 **사운드 리소스 관리 프로세스 및 설정 방식은 추후 별도로 상세히 기획하여 추가될 예정입니다.**
> (현재 초기 작업 폴더인 `Design/AudioSamples_V2/` 외 인게임 적용 우선순위, 레이턴시 분류, Wwise/FMOD 연동 여부 등은 다음 단계에서 확립합니다.)

---

## 4. 명명 및 코딩 컨벤션 (Naming Conventions)

팀 내 혼선을 줄이고, 기획/개발 코드 매핑을 쉽게 하기 위한 기재입니다.

*   **클래스 (Class) 및 구조체 (Struct):** `PascalCase` 사용 (예: `PlayerController`, `EntityDataManager`)
*   **변수 (Variable) 및 함수 (Function):** `camelCase` 사용 (예: `calculateDamage()`, `isPlayer`)
*   **상수 (Constant) 및 Enum:** `UPPER_SNAKE_CASE` 사용 (예: `MAX_HP`, `PLAYER_MIN`)
*   **기타 인터페이스:** `I` 접두어 사용 (예: `IEntity`)

### 4.1 주석 및 협업 (Comments & Collaboration)
```csharp
/// <summary>
/// ID를 기반으로 리소스 로드 경로(폴더명)를 반환합니다.
/// 예: 10 -> "Entities/Player"
/// </summary>
public static string GetResourceFolderPath(int id) { ... }
```
- 함수/메소드 위에는 위와 같이 XML 주석 `/// <summary>` 을 달아 역할과 반환값을 기재.
- 미완성 로직이나 기획 데이터 누락분은 `// TODO: (담당자, 날짜) 내용` 형태로 기재하여 트래킹합니다.
