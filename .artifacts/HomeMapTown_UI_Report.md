# Home -> Map -> Town UI/Feature Report

## Summary

Home -> Map -> Town 입장 흐름을 기존 Home1 Screen Space UI 구조 안에서 확장했다. Map에서 Town 가능한 지역을 선택하면 바로 씬 이동하지 않고, Town 입장 방식 선택 패널을 열어 기존 Town 찾기, 내 Town 생성, 키로 입장을 선택할 수 있다.

## Implemented

- Town 입장 선택 패널 추가
  - Find Existing Town, Create My Town, Join with Key, Quick Key Join 진입점을 구성했다.
  - 하위 화면 전환 시 상단 타이틀과 상태 문구가 참조 이미지 흐름에 맞게 바뀐다.
  - 패널, 목록, 입력창, 버튼은 기존 World Map 리소스 분위기에 맞춰 parchment/teal/gold 톤으로 구성했다.

- Existing Towns 기능 추가
  - 선택 지역의 공개 Town 목록 조회, 검색, 새로고침, 행 선택, 선택 Town 상세 보기, Join 버튼을 추가했다.
  - Steam lobby 정보는 서버 목록에 이미 존재하는 공개 Town에만 병합되도록 해 비공개 Steam lobby가 목록에 노출되지 않게 했다.

- Create My Town 기능 추가
  - Town 이름, 공개/비공개, 최대 인원 2/4/6/8 선택을 추가했다.
  - 생성 요청에 `isPublic`과 선택한 최대 인원을 전달한다.
  - Private Town은 목록에서 숨기고 키 입장으로만 접근하도록 서버와 클라이언트를 맞췄다.

- Join with Key 기능 추가
  - 키 입력, 키 검증, 결과 카드, Clear, Join Town 흐름을 추가했다.
  - 하이픈/공백/`townp2p:` 접두어가 있어도 키 조회가 가능하도록 정규화했다.

- Server API 확장
  - Town room 생성 요청/응답/Redis 저장 데이터에 `isPublic`을 추가했다.
  - 공개 목록 조회에서는 비공개 Town을 제외한다.

- Unity scene rebuild/verification 보강
  - Home1 Map 상세 패널 위치와 텍스트 영역을 재조정했다.
  - Town entry UI의 모든 serialized reference, row binding, max-player binding, input field, anchor 범위를 검증하는 Editor 검증 루틴을 추가했다.
  - 시각 확인용 Editor Canvas preview screenshot 메뉴를 추가했다.

## Changed Files

- `Client/Assets/3.Script/UI/Home/HomeMapRealmUI.cs`
- `Client/Assets/3.Script/Editor/Home1SceneSpaceUiBuilder.cs`
- `Client/Assets/0.MainProject/01_Net/Town/TownRoomApiClient.cs`
- `Server/ApiServer/2.Domain/Town/TownRoomService.cs`
- `Server/ApiServer/5.Presentation/Http/Controllers/TownRoomController.cs`
- `Client/Assets/0.MainProject/Scenes/Home 1.unity`
- `Client/Assets/0.MainProject/Scenes/Town/TownMap.unity`
- `Client/Assets/0.MainProject/Scenes/Town/Town_Forest.unity`

## Verification

- `dotnet build Client/Assembly-CSharp.csproj`: passed.
- `dotnet build Client/Assembly-CSharp-Editor.csproj`: passed.
- `dotnet build Server/ApiServer/ApiServer.csproj`: passed.
- Synaptic Unity asset refresh: passed, compilation OK.
- Synaptic menu `RhythmRPG/Editors/UI/Rebuild Home1 Scene Space UI`: passed.
- Synaptic menu `RhythmRPG/Editors/UI/Verify Home1 Scene Space Flow`: passed.
- Synaptic menu `RhythmRPG/Editors/UI/Ensure Town Home Overlay In Town Scenes`: passed.
- Synaptic menu `RhythmRPG/Editors/UI/Verify Town Home Overlay In Town Scenes`: passed.
- Unity console error analysis after verification and preview capture: 0 errors.

## Visual Check Artifacts

The following preview screenshots were generated and inspected:

- `Client/Assets/Screenshots/HomeMap_TownEntry_Choice_Preview.png`
- `Client/Assets/Screenshots/HomeMap_TownEntry_Existing_Preview.png`
- `Client/Assets/Screenshots/HomeMap_TownEntry_Create_Preview.png`
- `Client/Assets/Screenshots/HomeMap_TownEntry_Key_Preview.png`

The UI is not pixel-perfect to the reference image because there was no exact modal frame/card art asset for the provided mockups, but it uses the existing map/paper/frame/button resources and follows the same composition: World Map header, map art on the left, Town entry modal in the center, region detail panel on the right, and teal/gold action buttons.

## Notes

- Existing client build warnings remain in unrelated code and Unity package references. No new compile errors were introduced.
- A Synaptic Game View screenshot attempted earlier produced a black Play Mode capture because the capture path entered Play Mode and reset the preview state; the final visual verification uses the added Editor Canvas preview capture instead.
