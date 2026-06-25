# 전투 데미지 스케줄링 보고서

## 목적

Warning 지역 회피/진입 판정이 간헐적으로 틀어지던 원인은, 한 비트 안에서 다음 두 처리가 섞였기 때문이다.

1. 연출 시작과 Warning 예약
2. 이동 확정과 데미지 판정

이 둘이 같은 기준 시간으로 처리되면 다음 문제가 생긴다.

- Warning 위에 있다가 다음 턴에 다른 칸으로 피했는데도 데미지가 들어간다.
- 안전 지역에 있다가 Warning 지역으로 이동했는데도 데미지가 안 들어간다.

이번 수정의 목표는 `데미지는 항상 inputJudgeWindow 종료 후, 그 비트의 최종 위치 기준으로만 판정`되도록 순서를 고정하는 것이다.

## 최종 확정 순서

호스트 전투 로직은 이제 두 단계로 분리된다.

또한 실제 스케줄러 실행 시점은 `P2PHostController.LateUpdate()`로 고정한다.  
이유는 입력 콜백, `MainThreadDispatcher.Update()`, `NetworkManager.Update()`가 같은 프레임의 입력/패킷을 먼저 큐에 넣고,
그 다음 프레임 끝에서 호스트가 `currentBeat → resolvedBeat` 순서를 처리해야
원격 서버/릴레이 환경에서도 이동 입력이 한 프레임 늦게 밀려 `late input`으로 버려지지 않기 때문이다.

### 1. currentBeat 단계

비트가 시작되면 즉시 처리한다.

- 공격/스킬 시작 브로드캐스트
- ClientSkillRunner의 Warning 표시
- InputLock, 사운드, 연출 예약
- AI가 이번 비트에 사용할 행동 등록

이 단계에서는 **데미지를 확정하지 않는다.**

### 2. resolvedBeat 단계

`currentServerTime - judgeWindowMs` 기준으로 `resolvedBeat`를 계산한다.  
즉, inputJudgeWindow가 끝난 비트만 확정 처리한다.

같은 resolvedBeat 안에서는 아래 순서만 허용한다.

1. 해당 비트의 이동 입력 처리
2. 해당 비트의 스킬 이벤트 처리
3. 스킬 이벤트 내부 우선순위 적용

스킬 이벤트 우선순위는 다음과 같다.

1. `Move`
2. `Warning`
3. `InputLock`
4. `Damage`
5. `Sound`
6. `Wait`

핵심은 `Damage`가 항상 `Move` 뒤에 실행된다는 점이다.  
그래서 데미지 판정은 그 비트의 최종 위치를 본다.

## 이번 수정 사항

수정 파일:

- `Client/Assets/0.MainProject/04_Runtime/Game/Managers/P2PHostController.cs`

핵심 변경:

- `currentBeat`와 `resolvedBeat`를 다시 분리했다.
- 호스트 스케줄러 실행 시점을 `Update()`에서 `LateUpdate()`로 옮겼다.
- 비트 시작 시점에는 공격/스킬 시작만 처리하고, 데미지 확정은 하지 않도록 되돌렸다.
- 이동 입력은 `resolvedBeat`에서만 처리되도록 고정했다.
- 이미 확정이 끝난 비트로 늦게 들어온 입력은 폐기하도록 정리했다.
- 스킬 등록 직후 `ProcessSkillsAtBeat(currentBeat)`를 호출하던 경로를 제거했다.
  이 호출이 Warning 회피/진입 판정을 흔들던 직접 원인이었다.

## 보장되는 판정

이제 다음 두 케이스는 같은 규칙으로 처리된다.

### 케이스 A

- Warning 지역에 서 있음
- 다음 턴 inputJudgeWindow 안에서 다른 칸으로 이동 성공
- 같은 턴 데미지 판정 시점에는 이미 최종 위치가 안전 칸

결과:

- 데미지를 받지 않아야 한다.

### 케이스 B

- 안전 지역에 서 있음
- 다음 턴 inputJudgeWindow 안에서 Warning 지역으로 이동 성공
- 같은 턴 데미지 판정 시점에는 이미 최종 위치가 Warning 칸

결과:

- 데미지를 받아야 한다.

## 이후 작업 시 주의사항

이 부분은 이후 전투 작업에서 다시 합치면 안 된다.

- `currentBeat` 단계와 `resolvedBeat` 단계를 하나로 합치지 말 것
- 호스트 스케줄러를 다시 `Update()` 초반으로 당기지 말 것
- 스킬 등록 직후 즉시 `ProcessSkillsAtBeat(currentBeat)`를 다시 호출하지 말 것
- 이동 처리보다 데미지 처리가 먼저 오지 않게 할 것
- judge window가 닫히기 전 비트를 확정 비트로 취급하지 말 것

위 규칙이 깨지면 Warning 회피/진입 데미지 판정이 다시 흔들릴 가능성이 높다.
