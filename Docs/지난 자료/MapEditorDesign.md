# 인게임 맵 에디터 설계 (초안)

작성일: 2026-09-09
최종 갱신: 2026-09-18
상태: 1~6단계 전부 완료, 사용자가 Play 모드에서 체크리스트 검증까지 마침 — § "구현 순서" 참고.
추가로 §"맵 에디터 진입 흐름 개편" — 이제 씬 진입 시 편집 화면 대신 맵 선택 화면이 먼저 뜨고,
5단계에서 설명한 `SaveLoadModePanel`/`SaveLoadMode`/"Load 버튼" 관련 서술은 stale함(현재는
`SaveModePanel`, Load 버튼 없음 — 최신 내용은 새 섹션 참고).
**전체 시스템 안정성·유지보수성 리팩토링(2026-09-17, § "전체 시스템 리팩토링" 참고) 완료·검증
완료**: `ProgressManager`의 세이브 오염 버그·`FilterBlockBase`의 필터 고착 버그 등 실제 버그를
고쳤고, 사용자가 Play 모드 체크리스트 전항목 검증까지 마쳤다. `MapEditController.cs`의 8책임 분리는
회귀 위험 때문에 그 세션엔 보류했었다.
**MapEditController 책임 분리 Phase 1(2026-09-18, § "MapEditController 책임 분리 Phase 1" 참고)
완료·검증 완료**: 캔버스-순서 편집 UI + 탭 전환 2개 책임을 중첩 클래스로 분리, 사용자가 Play 모드
체크리스트까지 확인 완료.
**정답 순서 수동 편집 UI 제거(2026-09-18, § "정답 순서 수동 편집 UI 제거" 참고) 완료·검증 완료**:
사용자가 씬에서 이미 제거해 둔 수동 정답 순서 편집 UI(CanvasList/OrderList/"기물 추가")에 대응하는
죽은 코드를 정리(`CanvasOrderUI` 등). 정답 순서 데이터·자동 기록 로직은 그대로 유지. 사용자가 Play
모드 체크리스트까지 확인 완료.
**MapEditController 책임 분리 Phase 2(2026-09-18, § "MapEditController 책임 분리 Phase 2" 참고)
완료·검증 완료**: 저장/불러오기 + 플레이테스트 2개 책임을 중첩 클래스로 추가 분리
(`SaveLoadController`/`PlayTestController`), 사용자가 Play 모드 체크리스트까지 확인 완료.
**MapEditController 책임 분리 Phase 3(2026-09-18, § "MapEditController 책임 분리 Phase 3" 참고)
완료·검증 완료**: 남아있던 마지막 3개 책임(배치/제거, 팔레트 UI, 값 수정)을
`PlacementController`+`FixtureEditController`로 분리 — 이걸로 8책임 분리 전체가 완료됐다. 사용자가
Play 모드 체크리스트까지 확인 완료. 검증 중 발견된 버그 2건 + 기능 추가 1건은
§ "Phase 3 이후 버그 수정 및 기능 추가" 참고.
`MapEditor.unity` 씬에서 숫자 1~8 키로 핫바 슬롯을 선택하고, 카메라 조작은 유니티 Scene 뷰와
동일하게 마우스로(우클릭 회전/휠클릭 Pan/스크롤 Dolly) 하며, 좌클릭 설치·Ctrl+좌클릭 제거·
Shift+드래그 범위 설치/제거까지 지원한다. 파라미터가 필요한 기물을 선택하면 RGBInput/RGBSelect
패널이 뜨며, Confirm으로 값을 확정한다. 배치된 기물을 재선택해 값 수정("0"키 모드)도 가능하고,
정답 순서는 고정 2개(빨강/파랑)가 아니라 **캔버스(Canvas 기물)를 배치할 때마다 하나씩 자동으로
생기는 순서 목록**(최대 7개, 무지개 7색 마커) 방식으로 확장됐다 — 상세는 § "4단계: 기물 값 수정
모드 + 캔버스별 정답 순서 시스템 (2026-09-13)" 참고.

## 목적 / 대상

- 플레이어용 UGC(사용자 제작 콘텐츠) 기능. 유니티 에디터 없이 빌드된 게임 안에서 스테이지를 직접 제작.
- 만들 수 있는 기물 범위: 전체(필터 포함) — 컬러 필터, RGB 필터, 버킷, 캔버스, 팔레트, 컬러 체인저, 스택 체인저, 일반 벽 블록.
- 정답 순서(correctOrder1/2, 가이드 기능용)도 처음부터 편집 가능.

## 핵심 아이디어

기존 스테이지는 `.unity` 씬에 실제 GameObject로 저장되지만, 빌드된 게임 안에서는 새 씬 파일을 만들 수 없다.
그래서 배치 정보를 순수 데이터(JSON 직렬화 가능한 구조)로 저장하고, 플레이 시점에 그 데이터로 실제 기물
프리팹을 런타임에 Instantiate하는 방식을 쓴다. 진짜 컴포넌트(ColorCanvas, FilterBlockBase 등)로만
소환되면 LevelManager, StageGuideController, MapObjectMarkerHUD 같은 기존 게임 로직은 전혀 수정할
필요가 없다 — 이게 이 설계의 핵심 이점.

## 참고: 기존 개발자용 에디터(MazeGeneratorEditor)

`Assets/Scripts/Editor/MazeGeneratorEditor.cs` — Scene 뷰에서 마인크래프트식으로 블록을 편집하는
[개발자 전용] 커스텀 에디터. 클릭=설치/Ctrl+클릭=제거, Shift+드래그=사각형 범위 설치/제거, 라디오
버튼으로 기물 종류 선택 + 프리셋 값(R/G/B 등) 입력 후 설치. 좌표 규칙: x,z는 정수, y는 정수+0.5.
런타임 에디터는 이 조작 방식을 그대로 옮기되, Scene뷰/Handles 대신 실제 카메라 레이캐스트 +
런타임 UI로 구현한다.

## 데이터 구조

Dictionary는 JsonUtility가 직렬화하지 못하므로(SaveData.cs 기존 제약과 동일) 전부 List 기반으로 설계.

```csharp
[Serializable]
public class CustomStageData
{
    public string id;       // GUID — 나중에 서버 공유로 확장해도 충돌 안 나게
    public string title;
    public List<BlockEntry> blocks = new();       // 일반 벽 블록
    public List<FixtureEntry> fixtures = new();   // 필터 포함 전체 기물
    public List<CanvasOrderEntry> canvasOrders = new(); // 캔버스(Canvas 기물)마다 하나씩 자동 생성(최대 7개)
}

[Serializable]
public class BlockEntry { public int x, y, z; }

[Serializable]
public class CanvasOrderEntry
{
    public int canvasFixtureId;           // 이 순서가 속한 Canvas 기물의 id
    public List<int> orderFixtureIds = new(); // fixtures의 id를 참조 (리스트 인덱스 아님 — 편집 중 순서 변경에 안전)
}

[Serializable]
public class FixtureEntry
{
    public int id;           // 이 스테이지 내부 고유 id (correctOrder가 참조)
    public FixtureType type; // ColorFilter, RgbFilter, Bucket, Canvas, Palette, ColorChanger, StackChanger
    public int x, y, z;
    public int paramR, paramG, paramB;          // 컬러필터/팔레트/캔버스용
    public LightColor paramColorA, paramColorB; // RGB필터/버킷/스택체인저용
}
```

## 런타임 로더

지정된 `CustomStageData`를 받아서:
1. 일반 블록 → `wallBlockPrefab`이 비어있으면 `PrimitiveType.Cube`(에디터 기본값과 동일), 나중에
   전용 벽 프리팹이 생기면 그 필드에 넣기만 하면 자동으로 교체되도록 옵션 필드로 설계.
2. 기물 → 타입별 프리팹(MazeGenerator가 들고 있는 것과 동일한 7종) Instantiate, 파라미터 적용.
3. 빈 GameObject에 `MazeGenerator` 컴포넌트를 동적으로 붙이고, `data.canvasOrders`의 각 항목을
   실제 인스턴스 참조 리스트로 변환해 `MazeGenerator.correctOrders`(캔버스별 정답 순서 목록)에
   채움 → `StageGuideController`가 챕터 스테이지용 `correctOrder1/2`와 이 `correctOrders`를 하나로
   합쳐서 처리(§ "4단계" 참고, 가이드 시스템 하위 소비자는 변경 불필요).
4. `FilterBlockBase.RebuildAll()` 호출(필터 병합 메시 생성 — 개발자용 에디터/썸네일 캡처와 동일 처리).

## 인게임 에디터 UI (새 씬, 예: `MapEditor.unity`)

- 카메라 정면 레이캐스트로 블록 면 클릭 = 설치, 별도 입력(우클릭 등) = 제거. 기존 에디터의
  면 판정/그리드 스냅 로직(`TryGetTargetCell`)을 런타임 버전으로 그대로 이식.
- 화면 하단 기물 팔레트 UI: 블록/컬러필터/RGB필터/버킷/캔버스/팔레트/컬러체인저/스택체인저.
- 선택한 기물 타입에 맞는 파라미터 입력 UI(RGB 값 등 — 기존 에디터의 Preset 필드와 동일 개념).
- 이미 배치된 기물 재선택 → 값 수정 모드("0"키)에서 파라미터 재입력. 정답 순서는 캔버스를 배치할
  때마다 하나씩 자동 생기며(최대 7개), "기물 추가" 모드로 전환해 좌클릭한 기물을 선택된 캔버스의
  순서에 추가한다 — 상세는 § "4단계" 참고.

## 저장 / 공유

- 지금은 로컬 저장만: `SaveManager.Instance.SaveJson`(기존 범용 JSON API 재사용)으로 기기에 저장,
  "내 맵" 목록에서 불러오기/삭제/편집.
- 데이터 자체를 순수 JSON 직렬화 가능한 구조로만 만들어서, 나중에 내보내기/가져오기(문자열·파일)나
  서버 업로드로 확장하기 쉽게 열어둠. 저장 위치(로컬 파일 vs 서버)를 별도 계층으로 분리해서 로더/
  UI 코드는 안 건드리고 저장소만 교체 가능하게.

## 플레이 테스트

편집 중인 `CustomStageData`로 바로 플레이해볼 수 있는 "테스트" 버튼 — 런타임 로더를 그대로 재사용.

## 범위 밖

- 기존 챕터 스테이지(.unity 씬)는 JSON 방식으로 전환하지 않는다. 현재 수정 계획도 없고, 있어도
  소규모라 유니티 에디터에서 직접 고치는 쪽이 낫다고 판단(리스크 대비 이득 없음 — 상세 논의는
  대화 로그 참고). JSON 로더는 유저 제작 맵 전용.

## 구현 순서 (합의된 진행 계획)

1. ✅ `CustomStageData` + 관련 타입 정의 — 완료
2. ✅ 런타임 로더(데이터 → Instantiate → MazeGenerator 동적 부착 → RebuildAll) — 완료
3. ✅ 배치/제거 입력 + 기물 팔레트·파라미터 입력용 public API + 이벤트 배선 — 전부 완료(2026-09-12).
   Player 프리팹도 씬에 배치돼 있고 `InteractionController.enabled = false`로 이미 비활성화 확인됨.
4. ✅ 배치된 기물 재선택 → 파라미터 수정 + 캔버스별 정답 순서 추가·편집 UI — 완료(2026-09-13,
   § "4단계" 참고).
5. ✅ 저장/불러오기(로컬 JSON) + "내 맵" 목록 UI — 완료(2026-09-16, § "5단계" 참고).
6. ✅ 플레이 테스트 버튼(로더 재사용 대신 이미 배치된 오브젝트 재사용) — 완료(2026-09-17, § "6단계"
   참고). **단, 리팩토링 필요**(`MapEditController.cs` 비대화 — § "6단계" 도입부 참고).

각 단계는 순서대로 구현하고 중간중간 확인받으며 진행.

## 3단계 이벤트 배선 완료 내역 (2026-09-12)

문서에는 "HotBar 버튼 OnClick만 연결하면 됨"이라고 적혀 있었지만, 실제 씬을 열어보니 몇 가지가
문서와 달라서 사용자와 다시 협의해 아래처럼 확정·구현했다.

**HotBar는 버튼 클릭이 아니라 숫자 1~8 키로 선택한다** (마우스는 배치/제거용 좌/우클릭에 이미 쓰이고
있어서 핫바 선택까지 겹치지 않게 하려는 선택). HotBar의 8칸에는 애초에 `Button` 컴포넌트가 없었고,
라벨도 8칸 전부 `"1"`로 잘못 들어가 있었다 — 라벨을 1~8로 고치고, `Button`은 추가하지 않았다.

- **슬롯 매핑**: 1=기본 블록, 2=ColorFilter, 3=RgbFilter, 4=Bucket, 5=Canvas, 6=Palette,
  7=ColorChanger, 8=StackChanger (`FixtureType` enum 순서와 1:1).
- **선택 표시**: 선택된 슬롯의 `Image` 색을 `hotBarSelectedColor`(기본 노랑)로, 나머지는
  `hotBarNormalColor`(기본 흰색)로 바꿔서 하이라이트.
- **파라미터 패널은 "선택 시점"에 뜬다**(배치 시점이 아님) — 숫자키로 기물을 고르면 그 기물에 필요한
  패널(RGBInput 또는 RGBSelect)이 바로 뜨고, 필요 없는 기물(ColorChanger 등)은 아무 패널도 안 뜬다.
- **RGBInput은 입력창이 3개가 아니라 6자리("RRGGBB") 1개뿐** — 2자리씩 잘라 10진수 R/G/B로 해석
  (`SetPresetRGB`).
- **RGBSelect(Red/Green/Blue 토글 + Confirm)는 색 1개(RgbFilter/Bucket)와 2개(StackChanger의 A/B)
  선택에 재사용** — 토글은 정적 인자(0/1/2)로 `SetPendingColor`만 호출하고, 실제 A/B 반영은 Confirm
  클릭(`ConfirmColorSelect`)에서 처리. StackChanger는 첫 Confirm 후 패널을 유지한 채 두 번째 색을
  기다렸다가 반영.
- **값 유지**: `presetR/G/B`, `presetColorA/B`는 컨트롤러 인스턴스 필드라 아무 데서도 리셋하지 않음
  — 같은 기물을 여러 개 놓을 때 값을 새로 입력할 필요 없이 Confirm만 다시 누르면 직전 값 그대로
  적용된다.

**변경/추가된 코드**
- `Assets/Scripts/Player/InputManager.cs` — `ReadHotBarSlot(out int slot)` 추가(숫자 1~8, 메인
  자판/숫자패드 공용, 기존 `ReadDigit0()`과 같은 스타일).
- `Assets/Scripts/MapEditor/MapEditController.cs` — `rgbInputPanel`/`rgbSelectPanel`/
  `hotBarSlotImages`(8개) 필드 추가, `Update()`에서 `ReadHotBarSlot` 확인, `SelectBlockTool`/
  `SelectFixtureTool`이 패널 표시·하이라이트까지 담당, `SetPresetRGB`/`SetPendingColor`/
  `ConfirmRGBInput`/`ConfirmColorSelect` 추가.
- 씬(`MapEditor.unity`): HotBar 8칸 라벨 텍스트 수정, `hotBarSlotImages` 배열 연결, RGBInput/
  RGBSelect의 UnityEvent(OnValueChanged/OnClick)를 `UnityEditor.Events.UnityEventTools`로 배선
  (전용 Pipeline 명령이 없어 `eval_file`로 처리 — `GameObject.Find`는 비활성 오브젝트를 못 찾으므로
  `Transform.Find` 사용).
- 선택/패널 표시/StackChanger 2단계 색 선택 흐름은 `eval_file`로 자동 검증 완료(정상 동작 확인).

### 이벤트 배선 후 사용자 Play 테스트에서 발견된 버그 2건 수정 (2026-09-12)

자동화된 `eval_file` 검증은 실제 마우스/FPS 커서락/키보드 포커스 상호작용을 재현하지 못해서
아래 두 문제는 사용자가 직접 Play 모드에서 플레이해보고서야 발견됨. 둘 다 수정 완료, 사용자가
Play 모드에서 재확인하여 정상 동작 확인됨.

1. **패널이 떠도 마우스 커서가 안 보여서 클릭 불가**: `FirstPersonController`가 시작 시 커서를
   `CursorLockMode.Locked`/`visible=false`로 잠가둔 채라, RGBInput/RGBSelect 패널이 떠도 클릭할
   방법이 없었음. `MapEditController`에 `IsParamPanelOpen` 프로퍼티와 `UpdateCursorLock()`을 추가해
   패널이 하나라도 열려 있으면 커서를 풀고(`None`/`visible=true`), 전부 닫히면 다시 잠그도록 함
   (`PauseMenuController`가 쓰는 것과 동일한 기존 패턴 재사용). `SelectFixtureTool`/
   `HideParamPanels`에서 호출.
2. **RGB 입력창에 숫자를 타이핑하면 핫바 슬롯도 같이 바뀜**: `Update()`가 패널이 열려 있는지와
   무관하게 매 프레임 `ReadHotBarSlot`을 체크해서, RGBInput 입력창에 포커스가 있어도 숫자 키가
   핫바 전환으로도 처리됐음. `Update()`의 핫바 체크를 `!IsParamPanelOpen` 조건으로 감싸서 패널이
   열려 있는 동안(=텍스트 입력 중)은 핫바 전환을 무시하도록 수정.

**변경 파일**: `Assets/Scripts/MapEditor/MapEditController.cs`만 수정(`IsParamPanelOpen` private
프로퍼티 추가, `UpdateCursorLock()` 추가, `Update()`에 가드 추가). `InputManager.cs`나 씬 배선은
추가 변경 없음.

## 조작 방식을 유니티 Scene 뷰와 동일하게 전면 개편 + 배치 버그 3건 수정 (2026-09-12)

사용자가 "유니티 에디터처럼 자유 시점으로, 조작도 유니티 에디터처럼" 요청 — 기존 FPS 걷기 이동을
버리고, 마우스만으로 조작하는 방식으로 완전히 바꿨다. 이후 실사용 중 발견된 배치 관련 버그 3건도
같이 정리.

**조작키 (최종)**

| 입력 | 동작 |
|---|---|
| 우클릭 + 드래그 | 시점 회전 (누르는 동안 커서 잠김/숨김) |
| 휠 클릭 + 드래그 | 카메라 기준 평행 이동(Pan) |
| 휠 스크롤 | 카메라가 보는 방향으로 전/후진(Dolly) |
| 좌클릭 | 설치 |
| Ctrl + 좌클릭 | 제거 (기존엔 우클릭이었음) |
| Shift + 좌클릭 드래그 | 직사각형 범위 설치 (초록 미리보기) |
| Shift + Ctrl + 좌클릭 드래그 | 직사각형 범위 제거 (빨강 미리보기) |
| 숫자 1~8 | 핫바 슬롯 선택 (변경 없음) |

- 새 파일 `Assets/Scripts/MapEditor/EditorFlyCamera.cs` — `MapEditor.unity`의 Player 인스턴스에만
  붙는 전용 카메라 스크립트(공용 `Player.prefab`은 안 건드림). `FirstPersonController`와 같은
  "몸통 yaw + 카메라 pitch" 구조를 재사용하되 중력/충돌 없이 우클릭 홀드 중에만 회전한다.
  `MapEditor.unity`에서 `FirstPersonController`는 비활성화, `EditorFlyCamera` 추가.
- `InputManager.cs`에 `ReadRotateHeld`/`ReadPanHeld`/`ReadDolly`/`ReadRangeModifierHeld`/
  `ReadRemoveModifierHeld` 추가. `ReadInteract`/`ReadRemove`는 좌클릭 하나를 Ctrl 여부로 분기하도록
  통합.
- `MapEditController.cs`에 `MazeGeneratorEditor`(Scene 뷰 개발자 도구)의 Shift+드래그 직사각형
  범위 설치/제거 로직을 그대로 포팅(`DominantAxis`/`GetDragCenters`/`UpdateDragRect`/
  `CommitDragRect`). 미리보기는 런타임에 `Handles`가 없어서 풀링된 불투명 큐브(초록=설치,
  빨강=제거)로 대체. 커서 잠금 조건도 "패널 안 열림 && 우클릭 회전 중"으로 변경.

**배치 버그 3건 (실사용 중 발견)**

1. **필터 병합 메시가 클릭한 위치가 아니라 엉뚱한 곳에 뜸**: `FilterBlockBase.BuildGroup()`이
   메시 정점 좌표에 월드 좌표값을 그대로 굽고, 그 메시 오브젝트를 `mapObjectsRoot` 밑에 로컬
   원점으로 붙인다 — `mapObjectsRoot`가 월드 원점이어야만 맞는 구조. 그런데 `MapEditorController`
   GameObject 자체가 씬에서 원점이 아닌 곳(`(-4.84, -0.27, 1.73)`)에 있어서 `mapObjectsRoot`도
   같이 오프셋됐고, 병합 메시가 그만큼 어긋나 보였음(실제 필터 오브젝트·콜라이더는 항상 정확한
   위치였음 — 눈에 보이는 병합 메시만 문제). **해결: `MapEditorController` GameObject를 씬에서
   월드 원점 (0,0,0)으로 이동**(코드 변경 없이 씬 배치만으로 해결).
2. **캔버스가 플레이어 반대쪽을 보고 있음**: `ColorCanvas.LateUpdate()`가 표시면(로컬 -Z쪽)이
   아니라 +Z가 플레이어를 향하도록 `Quaternion.LookRotation(dir, ...)`을 쓰고 있었음. `-dir`로
   뒤집어서 수정([ColorCanvas.cs](../../Assets/Scripts/MapObjects/ColorCanvas.cs)) — 챕터 스테이지
   포함 모든 곳에 적용되는 공용 스크립트라 기존 챕터의 캔버스도 같이 정상화됨.
3. **기본 블록 외 기물(특히 필터)을 클릭할 수 없음 — 옆에 이어 놓기가 안 됨**:
   `TryGetTargetCell`/`TryRemove`의 레이캐스트가 `QueryTriggerInteraction.Ignore`였는데,
   `FilterBlockBase`는 플레이어 스택이 필터 조건과 일치하면 콜라이더를 트리거로 바꾼다(통과시켜야
   하니까). 기본값(0,0,0)으로 놓은 필터는 플레이어 초기 스택과 바로 일치해 트리거가 되고, 그러면
   편집용 레이캐스트가 아예 무시하고 지나가 클릭이 안 먹혔음. **해결**: 두 레이캐스트를
   `QueryTriggerInteraction.Collide`로 변경 — 편집 중에는 트리거 상태와 무관하게 항상 클릭 가능.

**시작용 블록을 정식 배치 데이터로 등록 (2026-09-12)**

바닥이 전혀 없으면 카메라 레이캐스트가 아무것도 못 맞혀 첫 배치 자체가 불가능한 문제(§ 별도 논의)
때문에, 사용자가 `MapEditor.unity`에 시작용 블록 하나("Block", 셀 `(0,-1,0)` = 월드 `(0,-0.5,0)`)를
직접 씬에 배치해뒀다. 이후 두 가지를 정리:

- 이 "Block"을 씬 루트에서 `/MapEditorController` 밑으로 재배치(`set_parent`, 위치는 그대로 유지)
  — 런타임에 배치되는 블록들과 같은 하이러키 영역에 있도록.
- **목적 확인**: 이 시작 블록도 다른 배치 블록과 완전히 동일하게 **제거 가능**해야 함. 이를 위해
  `MapEditController.Awake()`에 `RegisterPreplacedBlocks()`를 추가 — Awake 시점에 컨트롤러의
  기존 자식 오브젝트(방금 만든 `Maze`/`MapObjects` 본인은 제외)를 찾아 `Maze` 밑으로 옮기고,
  위치로부터 그리드 셀을 역산해 `cells`/`data.blocks`에 정식 등록한다. 그 결과 이 시작 블록도
  Ctrl+좌클릭으로 제거되고, 저장 데이터(`data.blocks`)에도 포함되며, 특정 오브젝트 이름에
  종속되지 않아 나중에 시작용 오브젝트를 더 추가해도 자동으로 같은 방식으로 등록된다.
  사용자가 Play 모드에서 직접 제거 확인 완료.

**스페이스바로 Confirm 버튼과 동일하게 확정 (2026-09-12)**

RGBInput/RGBSelect 패널의 "Confirm" 버튼을 마우스로 클릭하지 않고도 스페이스바로 확정할 수 있게
추가. `InputManager.ReadConfirm()`(스페이스바) 신설, `MapEditController.Update()`에서 패널이
열려 있을 때(`IsParamPanelOpen`) 스페이스바를 누르면 현재 활성 패널에 맞춰 `ConfirmRGBInput()`
또는 `ConfirmColorSelect()`를 그대로 호출(StackChanger의 2단계 색 선택도 기존 로직 그대로라 동일하게
동작). 마우스 포인터 위치와 무관하게 반응하도록 `IsPointerOverGameObject` 얼리 리턴보다 앞에 배치.
사용자가 Play 모드에서 RGBInput/RGBSelect(단일·2색) 전부 정상 동작 확인 완료.

**RGBSelect 토글 개수 검증 + 스택 체인저 3개 중 2개 선택 + 기물 재선택 시 패널 초기화 (2026-09-12)**

RGBSelect의 Red/Green/Blue 토글에는 `ToggleGroup`이 없어(`m_Group: null`) 몇 개를 켜든 유니티가
막아주지 않는다. `ConfirmColorSelect()`에서 `Toggle[] rgbSelectToggles`(씬 배선)의 `isOn` 상태를
직접 세어, RgbFilter/Bucket은 정확히 1개, StackChanger는 정확히 2개 선택돼 있어야만 확정하고 패널을
닫는다(개수가 안 맞으면 패널 유지). StackChanger는 기존에 "A 선택→Confirm→B 선택→Confirm" 2단계
(`colorPickStep`)였는데, 새 토글을 추가하지 않고 기존 3개 중 2개를 한 번에 선택하는 방식으로
바꿔서 1단계로 통합했다 — 배열 순서(Red=0,Green=1,Blue=2)상 먼저 켜진 인덱스가 아니라 배열에서
먼저 나오는 인덱스가 colorA, 나중 인덱스가 colorB.

같은 기물을 다시 선택(같은 핫바 슬롯 재입력)하면 패널 값이 초기화되도록 `SelectFixtureTool()`에
재선택 판정을 추가하고 `ResetPanelValues(FixtureType)`를 호출한다(RGBInput 계열은
`presetR/G/B=0`+`rgbInputField.text=""`, RGBSelect 계열은 토글 전부 `isOn=false`). 다른 기물을
거쳐 돌아오는 최초 복귀(재선택 아님)는 기존 "값 유지" 동작 그대로 보존된다. 이를 위해
`rgbInputField`(TMP_InputField) 필드를 신설해 `/UICanvas/RGBInput/RGB`에 배선했다.
사용자가 Play 모드에서 전부(토글 개수 검증, StackChanger 1단계 확정, 재선택 초기화, 값 유지 회귀
없음) 정상 동작 확인 완료.

**맵 에디터 HUD·1인칭 뷰모델 숨기기 + GameState를 프레임워크에서 프로젝트로 이관 (2026-09-12)**

맵 에디터 Play 모드에서 일반 플레이용 HUD(조준점·스택 표시)와 1인칭 뷰모델 붓이 계속 보이던 문제를
고쳤다. 두 컴포넌트(`HUDController`, `BrushViewmodel`)는 원래 `next != GameState.MainMenu`일
때만 보이는 구조였는데, `GameState`가 이 프로젝트가 아니라 별도 git 저장소인 프레임워크 패키지
(`com.nyapy.framework-core`)에 고정 `enum`으로 내장돼 있어서 새 상태를 넣으려면 프레임워크 자체를
고쳐야 했다.

이 기회에 "프로젝트가 상태를 자유롭게 추가/삭제할 수 있게" 프레임워크 구조를 바꿨다(실행 중 동적
등록은 불필요하다고 확인 — 컴파일 타임 enum으로 충분):
- 프레임워크 `Managers/GameManager.cs`(별도 저장소, `nyapy0811/FrameWorkCore.git`)를 구체 클래스
  → 제네릭 베이스 `GameManagerBase<TSelf, TState> : MonoSingleton<TSelf> where TState : struct, Enum`
  로 교체. `State`/`ChangeState`/`OnStateChanged`만 남기고, 상태 이름을 아는 이름 있는 편의 메서드
  (`StartGame/Pause/Resume/StageClear/BeginLoading/Quit`)는 전부 제거.
- 프로젝트 쪽에 새 파일 `Assets/Scripts/Core/GameState.cs`(진짜 C# enum: Boot/MainMenu/Loading/
  Playing/**MapEditor**/Paused/Cleared/Quitting)와 `Assets/Scripts/Core/GameManager.cs`
  (`GameManagerBase<GameManager, GameState>`를 구체화, 프레임워크에서 빠진 편의 메서드 전부 이관)를
  신설. 이름을 기존과 동일하게 유지해서 `MainMenuController`/`ClearScreenController`/
  `PauseMenuController`/`SceneRestarter`/`LevelManager`는 전혀 수정하지 않아도 그대로 컴파일됨.
- `GameStateListener.cs`/`HUDController.cs`는 `using Framework.Core;`가 더 이상 필요 없어 제거.
  `HUDController`/`BrushViewmodel`의 표시 조건에 `&& next != GameState.MapEditor` 추가.
  `MapEditController.Awake()` 맨 앞에 `GameManager.Instance.ChangeState(GameState.MapEditor);` 추가.

**부수 회귀 2건도 같은 자리에서 발견해 수정**:
1. `GameManager.Pause()`가 `State == Playing`일 때만 허용하고 `Resume()`이 무조건 `Playing`으로
   돌아가도록 하드코딩돼 있어서, 맵 에디터(`MapEditor` 상태)에서 ESC 일시정지가 아예 안 먹혔다.
   `Pause()`가 `Playing`/`MapEditor` 둘 다에서 허용되도록 하고, 일시정지 직전 상태를
   `stateBeforePause`에 저장해뒀다가 `Resume()`이 그 상태로 정확히 복귀하도록 수정
   (`PauseMenuController.Update()`의 ESC 판정에도 `GameState.MapEditor` 추가).
2. `Time.timeScale = 0`이어도 `Update()`는 계속 돌기 때문에, 일시정지 중에도 핫바 선택·설치·
   제거·드래그 범위 지정이 그대로 먹히고 있었다. `MapEditController.Update()` 맨 앞에
   `if (GameManager.Instance.State == GameState.Paused) return;` 가드를 추가해 일시정지 중에는
   커서 관리를 포함한 편집 입력 전체를 멈추고 `PauseMenuController`에 맡기도록 했다.

사용자가 Play 모드에서 HUD/뷰모델 숨김, 일시정지 진입/해제(맵 에디터로 정확히 복귀), 일시정지 중
편집 입력 차단, 일반 스테이지 회귀(메인 메뉴/Play/일시정지/클리어/재시작) 전부 정상 동작 확인 완료.

**단일 클릭 설치/제거 미리보기 + 기물도 블록 크기로 클릭 판정 (2026-09-12)**

기존 Shift+드래그 범위 설치/제거에만 있던 고스트 큐브 미리보기(초록=설치, 빨강=제거)를 일반 단일
클릭(좌클릭 설치/Ctrl+좌클릭 제거)에도 추가했다. `hoverPreview` 오브젝트 하나를 지연 생성해 매
프레임(`UpdateHoverPreview()`) 위치/재질을 갱신하며, `TryPlace`/`TryRemove`가 실제로 허용하는
조건과 동일하게 판정해서 클릭해도 아무 일 없는 상황(빈 곳 제거, 이미 막힌 칸 설치)에서는 미리보기도
뜨지 않는다. 일시정지, UI 위, 드래그 중에는 확실히 숨긴다.

이어서 "기물(필터·캔버스 등)도 블록처럼 클릭하기 쉽게 해달라"는 요청으로 클릭 판정 방식 자체를
바꿨다. 처음엔 기물에 보이지 않는 1x1x1 콜라이더를 붙이는 방법을 검토했으나,
`FilterBlockBase.OnTriggerEnter/Exit`가 플레이어의 필터 완전 통과를 감지해 `Player.ResetAll()`로
색 스택을 리셋하는데, 새 콜라이더가 같은 오브젝트에 붙으면 에디터에서 그 히트박스를 지나가기만
해도 이 콜백이 잘못 발동할 위험이 있어 기각했다. 대신 **콜라이더 없이 순수 계산**으로 처리한다:
- 새 헬퍼 `TryRaycastCells(ray, out hitCell, out normal, out distance)`가 `cells` 딕셔너리에 있는
  모든 칸에 대해 가상의 1x1x1 `Bounds`로 레이 교차를 검사해 가장 가까운 칸과 진입면을 반환한다.
  실제 모델 모양과 무관하게 항상 블록 하나 크기로 클릭된다.
- `TryGetTargetCell`(설치용)은 바닥/벽처럼 아직 배치되지 않은 표면을 위해 기존 물리 레이캐스트를
  유지하되, `TryRaycastCells` 결과와 "더 가까운 쪽"을 비교해서 쓴다.
- `TryGetRemoveTargetCell`(제거용)은 바닥 클릭이 의미 없으므로 `TryRaycastCells`만으로 충분해져
  훨씬 단순해졌다. 물리 레이캐스트로 콜라이더를 찾아 `PlacedTag`를 조회하던 기존 방식을 대체하면서,
  오직 그 용도로만 존재하던 `PlacedTag` 컴포넌트(`Assets/Scripts/MapEditor/PlacedTag.cs`)와
  `Register()`의 `AddComponent<PlacedTag>()` 호출도 제거했다.
- 필터의 `Col.isTrigger` 토글 상태와도 완전히 무관해져, 필터가 트리거 상태(통과 가능)일 때 클릭이
  씹히던 문제도 부수적으로 같이 해결됐다.

**버그 수정**: `Bounds.IntersectRay`는 레이 원점이 박스 안에 있으면 `dist=0`을 반환하는데,
`EditorFlyCamera`는 충돌 없는 자유 비행이라(특히 스크롤로 뒤로 Dolly할 때) 카메라가 블록 안/바로
옆으로 들어갈 수 있다. 이 상태를 그대로 두면 카메라 위치 기준의 임의 방향이 진입면으로 잘못 잡혀
플레이어 근처 허공에 초록 미리보기가 떴다. `TryRaycastCells`에서 각 칸을 검사하기 전에, 카메라가
그 칸의 1.2배 넉넉한 범위 안에 있으면 아예 그 칸을 판정에서 제외하도록 고쳐서 해결했다.

사용자가 Play 모드에서 얇은 기물 클릭/제거, 미리보기, 드래그 범위, 필터 트리거 상태, 뒤로 이동 시
허공 미리보기 버그까지 전부 정상 동작 확인 완료.

## 진행 상황 (1~2단계 완료 내역)

**새 파일**
- `Assets/Scripts/MapEditor/CustomStageData.cs` — `FixtureType` enum, `BlockEntry`, `FixtureEntry`,
  `CustomStageData` 클래스.
- `Assets/Scripts/MapEditor/CustomStagePrefabs.cs` — 기물 7종 + 벽 블록 프리팹을 들고 있는
  `ScriptableObject`(`ColorMaze/Custom Stage Prefabs` 메뉴로 생성). 현재 애셋은
  `Assets/Scripts/MapEditor/CustomStagePrefabs.cs`(스크립트) 기준으로 만들어졌고, 실제 프리팹 참조는
  `.meta`의 `MonoImporter.defaultReferences`에 7종 기물 프리팹이 이미 연결돼 있음(wallBlockPrefab은
  아직 비어있음 — 전용 벽 프리팹 생기면 채우면 됨).
- `Assets/Scripts/MapEditor/CustomStageLoader.cs` — `CustomStageData`를 Instantiate해서
  `MazeGenerator`를 동적으로 만들고 `correctOrder1/2`를 채운 뒤 `FilterBlockBase.RebuildAll()` +
  `SceneLoadCompleted` 발행까지 처리하는 정적 로더.

**기존 파일 수정 (Configure 메서드 추가 — 로더가 프리셋 값을 런타임에 넣을 때 사용)**
- `ColorFilterBlock.Configure(int r, int g, int b)`
- `RgbFilterBlock.Configure(LightColor target)`
- `Bucket.Configure(LightColor target)`
- `ColorPalette.Configure(int r, int g, int b)`
- `StackChanger.Configure(LightColor a, LightColor b)`
- `ColorCanvas.Configure(int r, int g, int b)`

**핵심 설계 포인트(재확인용)**: 로더 마지막에 `SceneLoadCompleted`를 발행하는 것만으로 LevelManager
(캔버스 스캔), StageGuideController(정답 리스트 로드), StackChanger/ColorChanger 미리보기,
FilterBlockBase 초기화가 전부 기존 로직 그대로 자동으로 맞물려 돎 — 별도 훅 불필요.

**남은 미확정 사항**: 플레이어 스폰 지점을 에디터에서 어떻게 지정할지(3~4단계에서 정할 것).
아직 별도 스폰 마커 기물 종류나 기본 스폰 규칙이 정해지지 않음.

**3단계 코드 (신규 파일)**
- `Assets/Scripts/MapEditor/PlacedTag.cs` — 배치된 오브젝트에 그리드 좌표를 붙여두는 꼬리표
  (제거 입력 시 레이캐스트로 맞은 오브젝트가 어느 칸인지 역추적하는 용도).
- `Assets/Scripts/MapEditor/MapEditController.cs` — 배치/제거 핵심 로직.
  - `Update()`: `EventSystem.current.IsPointerOverGameObject()`로 UI 클릭은 걸러내고,
    `InputManager.ReadInteract()`(좌클릭)=배치, `InputManager.ReadRemove()`(우클릭, 이번에 InputManager에
    신규 추가)=제거.
  - 배치 레이캐스트/그리드 스냅 규칙은 `MazeGeneratorEditor.TryGetTargetCell`과 동일(x,z=정수, y=정수+0.5).
  - 내부적으로 `CustomStageLoader.PlaceBlock`/`PlaceFixture`(이번에 public으로 전환, 반환값도
    정리)를 그대로 재사용해서 개별 배치 — 로더와 에디터 컨트롤러가 인스턴스화 로직을 공유함.
  - 배치/제거할 때마다 `CustomStageData`(내부에서 계속 들고 있는 `data` 필드, `Data` 프로퍼티로 공개)를
    같이 갱신 — 별도 "내보내기" 변환 단계 없이 바로 저장 가능한 상태 유지.
  - 팔레트/파라미터 UI 연결용 public 메서드: `SelectBlockTool()`, `SelectFixtureTool(int)`,
    `SetTitle(string)`, `SetPresetR/G/B(string)`, `SetPresetRGB(string)`(6자리 일괄),
    `SetPresetColorA/B(int)`, `SetPendingColor(int)`, `ConfirmRGBInput()`, `ConfirmColorSelect()`
    (마지막 4개는 2026-09-12 이벤트 배선 때 추가 — § "3단계 이벤트 배선 완료 내역" 참고).
- `Assets/Scripts/Player/InputManager.cs`에 `ReadRemove()`(우클릭) 추가.
- `Assets/Scripts/MapEditor/CustomStageLoader.cs`의 `PlaceBlock`/`PlaceFixture`를
  `private static` → `public static`로 전환(위 재사용을 위함). 동작 변경 없음.

**남은 에디터(유니티) 작업 — 3단계를 실제로 쓸 수 있게 하려면**
1. ✅ `MapEditor.unity` 새 씬 생성 + Build Settings에 등록 — 완료(2026-09-11 확인).
2. ✅ 카메라/이동: Player 프리팹 배치 확인됨, `InteractionController.enabled = false`로 이미
   비활성화되어 있음(2026-09-12 `eval`로 재확인).
3. ✅ 빈 오브젝트(`MapEditorController`)에 `MapEditController` 컴포넌트 추가, `prefabs` 필드에
   `CustomStagePrefabs.asset` 연결 완료(2026-09-11 확인).
4. ✅ HotBar 선택 → 숫자 1~8 키로 배선 완료(버튼 클릭 방식에서 변경, § "3단계 이벤트 배선 완료
   내역" 참고, 2026-09-12).
5. ✅ 파라미터 입력 UI(RGBInput 6자리 입력창, RGBSelect 토글+Confirm) → 전부 배선 완료, 색상
   A/B(스택체인저용) 2단계 선택 흐름까지 구현·검증 완료(2026-09-12).

## 4단계: 기물 값 수정 모드 + 캔버스별 정답 순서 시스템 (2026-09-13)

4단계 전체를 하위 단계(4-1/4-2/4-3)로 나눠서 진행했고, 사용자가 각 단계를 Play 모드에서 직접
테스트해 전부 정상 동작을 확인했다.

### 4-1. 값 수정 모드 뼈대

기물을 재선택해 파라미터를 고치는 기능을 만들려는데, Alt+클릭·휠클릭 등 조합키 방식은 이미 다른
기능(제거=Ctrl+좌클릭 등)과 겹치거나 매핑이 늘어질 수 있어 기각하고, 기존 핫바 도구 전환과 같은
패턴의 **전용 모드**로 결정했다: 숫자 "0"키로 "값 수정 모드"에 진입, 이 모드에서는 설치/제거/드래그가
전부 막히고 카메라 이동(회전/팬/돌리)만 가능하다.

- `MapEditController`에 `bool isEditMode`(4-3에서 `EditorMode` enum으로 대체됨) 추가,
  `SelectEditTool()`(0번 슬롯)에서 켜고 다른 도구 선택 시 꺼짐.
- `Update()`에서 `isEditMode`일 때 설치/제거 로직 전체를 건너뛰고 `TryEditFixtureAt()`만 실행,
  카메라 스크립트(`EditorFlyCamera`)는 별도 컴포넌트라 이 모드와 무관하게 계속 동작.

### 4-2. 기물 재선택 → 실시간 파라미터 편집

- `TryEditFixtureAt()`: 값 수정 모드에서 좌클릭한 칸의 기물을 찾아 `BeginEditFixture(FixtureEntry, GameObject)` 호출.
- `editingFixture`/`editingFixtureInstance` 필드로 "지금 편집 중인 기물"을 추적. 편집 시작 시
  기물 타입에 맞는 파라미터 패널(RGBInput/RGBSelect)을 띄우고 현재 값으로 초기화(`SetTogglesForEdit`).
- 기존 `SetPresetR/G/B`·`SetPresetColorA/B`·`ConfirmRGBInput`·`ConfirmColorSelect`에
  `editingFixture != null`일 때의 분기를 추가 — 새로 배치하는 게 아니라 기존 `FixtureEntry`의 값을
  덮어쓰고 `RefreshEditingVisual()`로 씬의 실제 오브젝트에도 즉시 반영(`CustomStageLoader.ApplyParams`를
  `public static`으로 전환해 재사용). Confirm을 누르면 `EndEdit()`으로 편집 상태 해제, 패널 닫힘.

### 4-3(개정). 캔버스별 정답 순서 시스템 — 게임 로직까지 확장

원래는 "정답 순서1/순서2"를 편집하는 UI만 만들 계획이었으나, 사용자가 직접 씬 UI를 만들어보며
요구사항이 구체화됐고, 최종적으로 **정답 순서 자체를 게임 로직 레벨에서 캔버스 1개당 순서 1개**로
확장하기로 했다(리스트 2개 고정 → N개, 최대 7개 — 마커 색을 무지개 7색으로 구분할 수 있는 한도).

**가장 중요하게 발견한 제약**: `Assets/Scenes/Chapter1~7/*.unity` 70개 기존 씬이 전부
`MazeGenerator.correctOrder1`/`correctOrder2` 필드를 인스펙터에 직접 값으로 채운 채 직렬화돼 있다
(그레이드/맵 에디터가 아니라 개발자가 예전부터 손으로 만든 정식 레벨). 이 필드의 이름·타입을
바꾸면 유니티 직렬화 매칭이 깨져 70개 레벨의 정답 순서가 조용히 날아간다. 그래서
**`correctOrder1`/`correctOrder2`는 한 글자도 건드리지 않고 그대로 둔 채, 맵 에디터 전용 새 필드
`correctOrders`를 추가하는 완전 additive 방식**으로 갔다. `CustomStageData`는 아직 저장 기능이
없어 디스크에 저장된 파일이 없으므로, 여기서는 기존 `correctOrder1FixtureIds`/
`correctOrder2FixtureIds`를 그냥 지우고 `canvasOrders` 구조로 교체했다(하위 호환 불필요).

**게임 로직 (`Assets/Scripts/Level/`, `Assets/Scripts/UI/`)**
- `MazeGenerator.cs`: 기존 `correctOrder1/2`는 그대로 두고, `correctOrders : List<List<MapObjectBase>>`
  필드를 새로 추가(맵 에디터 전용, 비어 있으면 무시).
- `StageGuideController.cs`: `list1/list2/index1/index2/Current1/Current2`를 리스트의 리스트
  기반(`lists`/`indices`, `ListCount`/`CurrentTarget(int i)`)으로 일반화. 씬 로드 시
  `correctOrders`가 있으면(맵 에디터 스테이지) 그걸 쓰고, 없으면(챕터 스테이지)
  `correctOrder1`/`correctOrder2` 두 개를 리스트로 감싸서 그대로 쓴다 — 두 시스템이 여기서만
  합류하고 하위 소비자는 소스 구분을 몰라도 된다.
- `StageGuideMarkerHUD.cs`: 고정 2개(빨강/파랑) 마커를 무지개 7색 배열로 교체, `markers[i]`를
  `guide.CurrentTarget(i)`로 갱신. 챕터 스테이지는 리스트가 최대 2개뿐이라 markers[2]~[6]은 항상
  비활성 상태 그대로라 시각적 회귀 없음.
- `MapObjectMarkerHUD.cs`: `pair.Key == guide.Current1 || pair.Key == guide.Current2` 비교를
  `guide.ListCount`를 순회하는 `IsCurrentGuideTarget()` 헬퍼로 일반화.

**데이터 모델 + 맵 에디터 로직 (`Assets/Scripts/MapEditor/`)**
- `CustomStageData.cs`: `CanvasOrderEntry`(`canvasFixtureId` + `orderFixtureIds`) 클래스 신설,
  `CustomStageData.canvasOrders : List<CanvasOrderEntry>`로 교체.
- `CustomStageLoader.cs`: `Load()`가 `data.canvasOrders`를 순회하며 각각을 `MazeGenerator.correctOrders`에
  채우도록 변경.
- `MapEditController.cs`:
  - **모드를 enum으로 통합**: `EditorMode { Place, ValueEdit, AddToOrder }` — "모드는 항상 정확히
    1개만 활성화"라는 요구사항을 타입 수준에서 보장(기존 `isEditMode` bool 대체, 이후 모드가
    늘어나도 이 구조 유지). `Update()`가 `switch (mode)`로 분기.
  - **캔버스 배치 제한 + 순서 자동 생성**: 캔버스를 배치할 때마다 `data.canvasOrders`에 새 항목을
    자동 추가(`CanvasOrderEntry { canvasFixtureId = entry.id }`), 이미 7개면 배치 자체를 막음.
    캔버스를 제거하면 그 캔버스의 순서 항목도 삭제하고, 다른 모든 캔버스 순서에서도 그 기물 id를
    제거(어떤 기물이 어느 캔버스 순서에 들어있었든 안전하게 정리).
  - **"기물 추가" 모드**(`EnterAddToOrderMode()`): 값 수정 모드와 동일하게 설치/제거/드래그를
    막고 카메라만 허용, 좌클릭한 기물을 **현재 선택된 캔버스 카드의 순서**에 즉시 추가
    (`TryAddFixtureAt()`, 파라미터 패널 없이 바로 추가). 다른 도구/모드로 전환하면 자동 해제.
  - `RefreshCanvasList()`/`RefreshOrderListUI()`: "숨긴 템플릿 복제" 패턴(템플릿은 항상
    `SetActive(false)`로 씬에 남겨두고, 데이터 개수만큼 `Object.Instantiate`)으로 캔버스 카드 목록·
    선택된 캔버스의 정답 순서 목록을 각각 동적으로 그림. 캔버스 카드 클릭(`SelectCanvasOrder`)
    시 하이라이트 갱신 + OrderList를 그 캔버스 순서로 전환.
- 새 파일 `CanvasCardUI.cs`(`NumberText`/`SelectButton`/`CardImage`), `OrderListItemUI.cs`
  (`OrderNumberText`/`ObjectNameText`/`UpButton`/`DownButton`/`RemoveButton`) — 둘 다 씬의 리스트
  항목 프리팹에 붙는 순수 필드 홀더.

**씬 UI (`MapEditor.unity`)**: 사용자가 `UICanvas/CorrectOrder` 패널(캔버스 카드 가로 스크롤
목록 `CanvasList`, 정답 순서 세로 스크롤 목록 `OrderList` + "기물 추가" 버튼)을 직접 만들었고,
`CanvasCardUI`/`OrderListItemUI` 컴포넌트 연결, "기물 추가" 버튼 → `EnterAddToOrderMode` 연결,
템플릿 카드 비활성화, `MapEditController`의 새 직렬화 필드 5개 연결은 Unity 라이브 에디터
연동(`unity-connect`)으로 처리했다.

**버그 수정 2건 (사용자가 UI 완성 후 Play 모드에서 발견)**
1. 정답 순서 UI 위에서 마우스 휠을 굴리면 카메라가 전후로 움직였다 — `EditorFlyCamera.cs`의
   Dolly 입력을 `EventSystem.current.IsPointerOverGameObject()`일 때 무시하도록 가드 추가.
2. UI 리스트가 스크롤되는 것처럼 조금 움직이다 바로 원위치로 튕겨 돌아왔다(1칸도 못 내려감) —
   원인은 `OrderList`/`CanvasList` 둘 다 `Content`에 `ContentSizeFitter`가 없어서, 스트레치
   앵커로 고정된 RectTransform이 자식 개수와 무관하게 항상 Viewport 크기와 똑같았던 것(=
   `ScrollRect` 입장에서 스크롤할 여백이 0). `OrderList/Content`에 `verticalFit=PreferredSize`,
   `CanvasList/Content`에 `horizontalFit=PreferredSize`인 `ContentSizeFitter`를 추가하고,
   `CanvasList`의 `ScrollRect`가 세로만 켜져 있던 것도 `horizontal=true, vertical=false`로
   고쳐서 해결(가로로 늘어서는 카드 목록이라 원래 가로 스크롤이 맞음).

**검증**: 캔버스 7개 + 기물 20개짜리 정답 순서를 리플렉션으로 임시 주입해 스크롤 동작을 먼저
확인(테스트 종료 후 Play 모드 종료로 데이터는 저장 없이 폐기)했고, 이어서 사용자가 실제 조작으로
캔버스 배치·7개 제한·카드 클릭 전환·"기물 추가" 모드(설치/제거 차단, 카메라 이동 허용)·모드
배타성·캔버스 제거 시 순서 정리·OrderList 위/아래/삭제 버튼·기존 챕터 스테이지(1-1)의 빨강/파랑
마커 회귀 여부까지 전부 Play 모드에서 직접 테스트해 정상 동작을 확인했다.

> **stale(2026-09-18)**: 6단계에서 정답 순서 자동 기록(플레이 테스트 중 상호작용 순서를 자동으로
> 저장)이 검증된 뒤, 사용자가 이 절에서 설명하는 수동 편집 UI(`CorrectOrder`/`CanvasList`/
> `OrderList`/"기물 추가" 버튼)를 씬에서 직접 제거했고, 그에 맞춰 관련 코드도 정리했다 — § "정답
> 순서 수동 편집 UI 제거" 참고. 이 절의 내용은 그 UI가 어떻게 만들어졌는지의 기록으로만 남긴다.

**부수 작업(같은 세션에서 별도로 진행, 맵 에디터와 직접 관련은 없지만 ColorCanvas를 건드림)**
- `ColorCanvas`가 `LateUpdate()`에서 Y축만 기준으로 플레이어 쪽을 바라보도록 회전.
- 기존 Chapter1~7 모든 씬의 캔버스 27개 인스턴스의 Y축 회전을 0으로 일괄 정리(스크립트로 처리,
  자식 오브젝트 회전은 건드리지 않음).

### 4-4. 값 입력 UI 통합 + 다중 선택 편집 (2026-09-14~16)

4단계 완료 이후 사용자가 배치 모드·값 수정 모드의 색 입력 UI를 직접 다시 만들면서, 그에 맞춰
배선을 다시 잡고 몇 가지 버그·기능을 추가로 정리했다.

**버튼 높이 토글 + Valueinput 프리팹 통합**
- 배치 모드의 기물 팔레트 버튼은 비활성 상태에선 높이 80(입력 패널 비활성), 그 기물이 선택된
  상태에선 높이 160(입력 패널 활성)으로 `VerticalLayoutGroup`이 자동 재배치하도록 변경
  (`ApplyPlacePanelStates`/`SetButtonExpanded`, `MapEditController.cs`) — 이전에 있던 "패널이
  다른 버튼에 가려 안 보임" 레이아웃 겹침 문제가 이 구조 변경으로 근본적으로 해결됨(패널 재배치
  꼼수 불필요).
- 배치 모드 6개 버튼 + 값 수정 모드(mode2Panel) 전부 색 입력 UI를 `Valueinput` 프리팹
  (`Assets/Prefebs/Valueinput.prefab`) 하나로 통일 — 안에 `RGBInput`(6자리 텍스트 입력)과
  `RGBSelect`(Red/Green/Blue 버튼)를 자식으로 두고, 기물 종류에 맞는 쪽만
  `SetPanelActive`로 켠다(`UsesRgbInput(FixtureType)`로 판정).
- **프리팹 원본 버그**: `Valueinput.prefab`의 `RGBSelect` 버튼들이 리네임 전 옛 메서드 이름
  `ClickPlaceColor`를 계속 가리켜 클릭이 씹혔다. 씬 인스턴스에만 `AddPersistentListener`로
  재배선했더니 `m_Target`만 오버라이드로 추적되고 `m_MethodName`은 추적 안 돼 프리팹 원본의 낡은
  값으로 계속 되돌아가는 게 원인 — `PrefabUtility.LoadPrefabContents`로 프리팹 원본 자체를 열어
  고치는 방식으로 확실히 해결(7개 인스턴스·21개 버튼 전부 자동 반영).

**색 선택 버튼 하이라이트(연하게 표시)**
- RGBSelect·StackChanger의 2색 선택 모두, 선택 안 된 색 버튼은 `Color.Lerp(base, Color.white, 0.5f)`로
  연하게 표시해 클릭 반영 여부를 시각적으로 보여준다(`UpdateColorButtonHighlight`). **기본 상태는
  항상 전부 연하게**(미선택) — 기존 기물을 값 수정 모드로 열어도 현재 값을 미리 하이라이트하지
  않고, 클릭해야만 그 색이 표시된다(사용자가 명시적으로 요청한 동작).

**RGBSelect/프리셋 관련 버그 3건 수정**
- 같은 색을 두 번 클릭하면 중복 선택되던 버그 → `RegisterColorClick`이 이미 선택된 색은 무시.
- 기물 종류를 바꿔도 이전 종류에서 고른 색 값이 그대로 이어지던 버그 → 전역 preset 필드를
  `Dictionary<FixtureType, PresetValues>`(`GetPreset(type)`)로 교체해 종류별로 완전히 분리.
- 필요한 색 개수를 다 고르지 않아도 재선택 시 기본값(빨강 등)으로 설치되던 버그 →
  `IsPlaceReady(FixtureType?)`가 `TryPlace`/`CommitDragRect`를 가드해서, 조건을 못 채우면 설치
  자체가 안 되도록 막음.

**값 수정 대상 마크 구분**
- 값 수정 모드에서 화면에 뜨는 마크 중 지금 편집 중인 기물의 마크만 다른 색
  (`markSelectedColor`, 기본 초록)으로 구분 표시(`UpdateMarkHighlight`). 나머지는 기존
  `markNormalColor`(기본 노랑) 그대로.

**다중 선택 편집(Shift/Ctrl)**
- 값 수정 모드에서 여러 기물을 한 번에 편집할 수 있게 `editingFixture`(단일) →
  `editingFixtures`/`editingFixtureInstances`(리스트)로 전환.
- **Shift+클릭**: 이미 선택된 기물이 있을 때, 클릭한 기물의 종류·현재 파라미터 값이 선택 그룹과
  완전히 같으면(`MatchesEditingSelection`) 그 기물 하나를 선택에 추가. 다르면 무시.
- **Ctrl+클릭**: Shift와 같은 종류·값 일치 조건으로, 클릭한 기물을 기준 삼아 상하좌우전후로
  맞닿은 채 조건을 만족하는 기물들을 이웃의 이웃까지 BFS로 연쇄 확장하며 한 번에 선택
  (`ExpandEditSelectionFrom`). 선택된 기물이 없는 상태에서 Ctrl+클릭하면 그 기물을 기준으로 새로
  시작한 뒤 바로 연쇄 확장한다.
- 선택된 기물 전체에 값 입력(RGBInput 텍스트, RGBSelect 클릭 — StackChanger의 2색 선택 포함)이
  동시에 반영되고(`SetPresetR/G/B`, `ClickColorButton`), 필터류(ColorFilter/RgbFilter)는
  `FilterBlockBase.RebuildAll()`도 그룹당 한 번만 호출.
- 사용자가 Play 모드에서 위 항목 전부(RGBSelect 클릭 반영, 색 하이라이트 기본/클릭 후 상태, 3가지
  버그 재현 안 됨, 마크 구분, Shift 다중 선택, Ctrl 연쇄 선택) 정상 동작 확인 완료.

**변경 파일**: `Assets/Scripts/MapEditor/MapEditController.cs`(대부분의 변경),
`Assets/Scripts/Player/InputManager.cs`(`ReadRangeModifierHeld`/`ReadRemoveModifierHeld` 문서 주석에
값 수정 모드 재사용 용도 추가), `Assets/Prefebs/Valueinput.prefab`(신규), `Assets/Scenes/MapEditor.unity`
(버튼 높이 기본값, `fixtureValuePanels`/`editRgbInputPanel`/`editRgbSelectPanel`/`editColorButtonImages`
등 배선).

## 5단계: 맵 저장/불러오기(로컬 JSON) (2026-09-16)

> **stale 안내**: 아래 UI 서술(`SaveLoadModePanel`/`SaveLoadMode` 탭/Load 버튼)은 이 시점 기준
> 기록이다. 이후 § "맵 에디터 진입 흐름 개편"에서 `SaveLoadModePanel`→`SaveModePanel`로 개명되고
> Load 버튼은 삭제됐다(맵 선택 화면이 그 역할을 대신함) — 현재 UI 구조는 그 섹션을 참고할 것.

`CustomStageData`(§ "데이터 구조" 참고)는 처음부터 `JsonUtility` 직렬화가 되도록 설계돼 있었지만,
지금까지 실제로 파일에 쓰거나 읽는 코드는 없었다. 프레임워크의 범용 `SaveManager.Instance.
SaveJson<T>/LoadJson<T>(fileName)`(`JsonUtility` 기반, `Application.persistentDataPath`에 저장)를
그대로 재사용해서 연결했다.

**UI**: 사용자가 만들어 둔 `SaveLoadModePanel`(Save/Save As/Load 버튼)과 `ModeButtonList`의 4번째
탭 `SaveLoadMode`에, 새로 만든 두 팝업(`UICanvas` 바로 밑, 어느 탭이든 항상 뜰 수 있게)을 연결했다:
- `SaveNamePopup` — 맵 제목 입력(확인/취소).
- `LoadListPopup` — 저장된 맵 목록(`CanvasList`/`OrderList`와 동일한 "숨긴 템플릿 복제" 패턴,
  `MyMapCardUI` 신설)을 스크롤로 보여주고 클릭하면 그 맵을 불러온다.

**동작 규칙**:
- 파일명은 `CustomMap_{data.id}.json`(GUID 기반, 충돌 없음). 목록은 `Directory.GetFiles`로
  `CustomMap_*.json`을 열거해 각각 제목만 읽어 카드로 표시(별도 인덱스 파일 없음).
- **"저장"**: 이미 이름이 있는(=처음 저장이 아닌) 맵이면 팝업 없이 바로 그 파일에 덮어쓴다. 아직
  이름이 없으면(첫 저장) 이름을 받아야 하므로 팝업이 뜬다.
- **"다른 이름으로 저장"**: 이미 이름이 있어도 항상 팝업이 뜨고, 확인하면 `data.id`를 새 GUID로
  바꿔 별도 파일로 저장한다 — 이후 "저장"은 그 새 파일을 대상으로 하고 원본은 건드리지 않는다.
- **"불러오기"**: 선택한 맵으로 현재 편집 상태(배치된 오브젝트, `cells`, `data`의 blocks/fixtures/
  canvasOrders, 편집 중 선택 등)를 완전히 교체한다(`LoadMap`). `data` 인스턴스 자체는 유지한 채
  내용만 갈아끼워서 `Data` 프로퍼티 참조가 깨지지 않게 했고, `nextFixtureId`는 불러온 기물 id
  최댓값+1로 재계산, `FilterBlockBase.RebuildAll()`은 필터 유무와 무관하게 항상 호출(기존
  `CustomStageLoader.Load()`와 동일 원칙).
- 값 수정/정답 순서 모드와 동일하게 `EditorMode.SaveLoad`를 추가해 이 탭에서는 카메라 이동만
  가능하고 월드 클릭(설치/제거/드래그)은 막힌다.

**버그 수정**: 새로 만든 `LoadListPopup`의 `Viewport` Image를 `OrderList/Viewport`와 동일한
`sprite=UIMask`로 맞췄지만 `Image.Type`을 `Sliced`로 지정하지 않아, 마스크용 스프라이트가 원래
모양(둥근 말풍선 형태)대로 늘어나 보이는 버그가 있었다(사용자가 Play 모드에서 발견 후 직접 수정).

**새 파일**: `Assets/Scripts/MapEditor/MyMapCardUI.cs`(`CanvasCardUI`와 동일한 패턴).

**검증**: 컴파일 정상, 새 필드(`mode4Panel`/`saveNamePopup`/`saveNameInputField`/`loadListPopup`/
`loadListContent`/`myMapCardTemplate`) 전부 배선 확인, 4개 버튼(Save/SaveAs/Load/SaveLoadMode 탭)의
onClick target/method 문자열 직접 확인. 사용자가 Play 모드에서 저장→재시작→불러오기 왕복까지
정상 동작 확인 완료.

## 6단계: 플레이 테스트 버튼 (2026-09-17)

**핵심 통찰**: 맵 에디터는 배치할 때마다 `CustomStageLoader.PlaceBlock`/`PlaceFixture`로 이미
**진짜 컴포넌트를 그 자리에 Instantiate**해서 `mazeRoot`/`mapObjectsRoot` 밑에 들고 있다(`cells`
딕셔너리로 추적). 그래서 플레이 테스트는 `CustomStageLoader.Load()`를 다시 불러 씬을 통째로 새로
만드는 게 아니라 — **이미 살아있는 그 오브젝트들을 그대로 플레이 가능한 상태로 전환**하기만 하면
된다(중복 생성·중복 콜라이더 걱정 없음). 부족했던 두 가지만 채우면 됐다: `MazeGenerator`
컴포넌트(정답 순서를 담는 데이터 홀더, 지연 `AddComponent`로 최초 1회만 부착) + 챕터 스테이지
로드 때와 동일하게 `LevelManager`/`StageGuideController`/필터 병합을 초기화하는 계기인
`SceneLoadCompleted` 이벤트 발행.

**UI**: 사용자가 이미 만들어 둔 5번째 탭 `PlayMode`(`ModeButtonList`) → `PlayModePanel`의
`PlayButton`(자리표시자 메서드로 연결돼 있던 것을 실제 메서드로 재배선)에 새로 만든
`PlayTestOverlay`(`UICanvas` 바로 밑, `BackToEditorButton` 하나만 있는 작은 배너, 기존
`PlayButton`을 복제해 스타일 통일, 기본 비활성)를 연결했다.

**동작 규칙**:
- **시작(`StartPlayTest`)**: `Time.timeScale = 1f`부터 명시적으로 복구(직전에 클리어 화면 등으로
  멈춰 있었을 가능성 방지)한 뒤, 선택/마크/미리보기 정리 후 `selectionPanel` 숨김 +
  `playTestOverlay` 표시. `FirstPersonController.Instance`(씬에 하나뿐인 Player 싱글톤) 기준으로
  `EditorFlyCamera` 비활성 + `FirstPersonController`/`InteractionController` 활성 + 커서 잠금,
  스폰 위치를 고정 좌표로 리셋. `ColorStacks.ResetAll()`로 색 스택 초기화. `cells`에 이미 배치된
  기물들로 `MazeGenerator.correctOrders`를 `data.canvasOrders` 기준으로 채우고(id→인스턴스 역참조
  딕셔너리 경유), 매 테스트마다 모든 `ClearObjectBase.ResetCompletion()`을 호출해 이전 테스트의
  완료 상태가 새 테스트에 남지 않게 한다. `FilterBlockBase.RebuildAll()` + `SceneLoadCompleted`
  발행 후 `GameManager.ChangeState(Playing)`.
  - **스폰 위치 이동 신뢰성**: 처음엔 `fpc.transform.SetPositionAndRotation(...)`으로 직접
    Transform만 옮겼는데, `CharacterController`가 붙어있는 오브젝트라 직전 테스트에서 남아있던
    낙하/이동 속도·지면 판정 상태와 부딪혀 순간이동이 씹히거나 튀는 문제가 있었다(사용자가 "플레이
    버튼 누르면 캐릭터를 시작지점으로 이동시키고 시작하게 해달라"고 요청해 발견). `FirstPersonController.
    Teleport(position, rotation)`를 새로 추가해 `CharacterController`를 잠깐 꺼서 옮긴 뒤 다시 켜고,
    남은 수평/수직 속도와 시점 pitch까지 초기화하도록 고쳤다.
- **종료(`StopPlayTest`)**: 위 전환을 역순으로 되돌리고(`Time.timeScale`도 복구),
  `EditorMode.MapEditor`로 상태 복귀. 에디터 자체의 프레임 로직(`Update()`)은 `isPlayTesting`
  플래그로 테스트 중엔 완전히 건너뛴다 — 플레이어 입력/일시정지는 기존
  `FirstPersonController`/`PauseMenuController`가 그대로 처리(추가 코드 불필요, HUD/BrushViewmodel도
  `GameState` 리스너라 자동으로 켜짐/꺼짐).
- **실제 클리어 감지 시 자동 복귀**: `GameState`에 `MapEditorPlayTest`를 새로 추가해, 플레이
  테스트 시작 시 `Playing`이 아니라 이 상태로 전환한다(`GameState.cs`). `GameManager.StageClear()`가
  원래부터 갖고 있던 `"State != Playing이면 무시"` 가드에 자연히 걸려, 플레이 테스트 중엔 실제
  `GameState.Cleared` 전환 자체가 일어나지 않는다. `ClearScreenController.OnStageCleared`도
  `GameManager.Instance.State != GameState.Playing`이면 클리어 화면을 아예 안 띄우도록 바꿔서,
  `MapEditController`를 전혀 몰라도 되게 분리했다. `MapEditController.OnStageClearedDuringPlayTest`는
  `StopPlayTest()` 호출로 에디터 상태 복구만 담당.
  - **시행착오**: 처음엔 플레이 테스트도 그냥 `GameState.Playing`을 재사용하고, `MapEditController`에
    `IsPlayTesting`(static bool)을 둬서 `ClearScreenController`가 그걸 직접 참조해 화면을 취소하는
    방식으로 구현했었다. 사용자가 Play 모드에서 테스트해보니 여전히 클리어 화면이 떴고("아직 클리어
    창이 떠"), 사용자가 "그냥 편집 모드 하위의 fsm 상태를 추가하는 게 편할 것 같다"고 제안해 위
    방식(전용 `GameState` 추가)으로 교체했다 — 서로 다른 클래스가 static 필드로 몰래 상태를
    주고받는 대신, 이미 있는 FSM에 진짜 상태를 하나 추가해 `GameManager.State`만 보면 판단할 수
    있게 정리한 것.
  - 이 상태 추가에 맞춰 `GameManager.Pause()`/`PauseMenuController`(ESC 일시정지 진입 조건, 재개 시
    커서 잠금 조건)에도 `MapEditorPlayTest`를 `Playing`/`MapEditor`와 동등하게 취급하도록 반영해,
    플레이 테스트 중 ESC 동작이 기존과 동일하게 유지되게 했다. HUD/BrushViewmodel은 원래
    `MainMenu`/`MapEditor`일 때만 숨기는 구조라 새 상태는 손댈 필요 없이 그대로 보인다.
- **정답 순서 자동 기록(수동 입력 시스템을 나중에 없애기 위한 사전 작업)**: 테스트 중
  `MapObjectUsed`(캔버스 자신의 완료 이벤트는 재료가 아니므로 제외)를 캔버스별 시퀀스에
  누적하다가, 그 캔버스의 `CanvasCompleted`가 뜨는 순간 지금까지 쌓인 시퀀스를 그 캔버스의
  `orderFixtureIds`로 덮어쓰고 비운다(다음 캔버스는 그 시점부터 새로 기록). 기존 수동 순서
  편집 UI(`CanvasList`/`OrderList`/"기물 추가" 모드)는 같은 데이터를 공유하므로 그대로 남겨뒀다
  — 자동 기록 후 수동 미세조정도 가능.
- **`clearVerified` 플래그**: 테스트로 실제 클리어하면 `data.clearVerified = true`. 이후 배치·
  제거·값 수정(`PlaceBlockAt`/`PlaceFixtureAt`/`RemoveCell`/`RefreshEditingVisual`, 즉
  `MarkEdited()` 호출 지점) 중 하나라도 일어나면 자동으로 `false`로 리셋 — 예전 클리어 기록이
  지금 내용과 안 맞을 수 있으므로. `LoadMap()`도 불러온 파일의 값을 그대로 이어받는다. 이번
  범위는 필드 추가와 세팅/리셋 로직까지만 — UI 표시(불러오기 목록 배지 등)는 나중 과제.
- 반복 테스트 안전성: 씬을 재로드하지 않고 같은 오브젝트를 재사용하므로, `ClearObjectBase.Completed`
  가 이전 테스트 결과로 영구히 남지 않도록 `ResetCompletion()`을 새로 추가해 매 `StartPlayTest()`마다
  호출한다.
- (범위 밖) 테스트 중 ESC로 일시정지 메뉴를 열고 "다시하기"/"메인메뉴"를 누르면 씬이 재로드/전환
  되며 저장 안 한 편집 내용이 사라질 수 있음 — 이번엔 손대지 않음.

**변경 파일**: `Assets/Scripts/Core/GameState.cs`(`MapEditorPlayTest` 상태 추가),
`Assets/Scripts/MapEditor/MapEditController.cs`(대부분의 변경 — `EditorMode.PlayView`,
`ShowPlayTab`/`StartPlayTest`/`StopPlayTest`/`RestoreConsumedFixtures`/세 이벤트 핸들러/`MarkEdited`,
`Update()`의 ESC 처리),
`Assets/Scripts/MapEditor/CustomStageData.cs`(`clearVerified` 필드),
`Assets/Scripts/MapObjects/ClearObjectBase.cs`(`ResetCompletion()`),
`Assets/Scripts/UI/ClearScreenController.cs`(`OnStageCleared`가 `GameManager.State`를 직접 확인하도록
변경), `Assets/Scripts/Player/FirstPersonController.cs`(`Teleport()` — CharacterController를 잠깐
꺼서 옮겨 안전하게 텔레포트 + 잔여 속도/pitch 초기화), `Assets/Scenes/MapEditor.unity`
(`PlayTestOverlay` 신설, `PlayMode`/`PlayButton` 재배선, `mode5Panel`/`selectionPanel`/
`playTestOverlay`/`modeTabImages[4]` 배선, `BackToEditorButton` 문구를 "ESC to Editor"로 수정).

**검증**: 컴파일 정상. `mode5Panel`/`selectionPanel`/`playTestOverlay`/`modeTabImages`(5칸) 전부
배선 확인, `PlayMode` 탭 → `ShowPlayTab`, `PlayButton` → `StartPlayTest`, `BackToEditorButton` →
`StopPlayTest` onClick target/method 문자열 직접 확인.

**추가 버그 수정 2건(사용자가 Play 모드에서 발견, 2026-09-17)**
1. **"Back to Editor" 버튼이 무반응**: 플레이 테스트 중엔 FPS 시점 조작을 위해
   `Cursor.lockState = Locked`로 마우스 커서를 잠가둔다 — 이 상태에서는 커서가 화면에 보이지도,
   클릭 가능한 위치로 움직이지도 않으므로 `PlayTestOverlay`의 버튼 자체를 클릭할 방법이 없었다
   (설계 단계에서 놓친 부분). 일시정지 메뉴를 거치는 방식 대신, `MapEditController.Update()`가
   플레이 테스트 중 ESC 입력을 직접 감지해 바로 `StopPlayTest()`를 호출하도록 고쳤다(일시정지
   메뉴는 아예 거치지 않음 — "다시하기"/"메인메뉴" 같은, 편집 중인 맵을 날릴 수 있는 버튼들과
   섞이지 않게). 버튼 자체는 그대로 두되(혹시 모를 다른 입력 방식 대비), 문구를 "ESC to
   Editor"로 바꿔 사용법을 안내한다.
2. **클리어 후 에디터로 복귀 시 사라진 기물이 안 돌아옴**: 버킷/팔레트/컬러 체인저/스택 체인저
   (`ConsumableObjectBase` 계열)는 실제로 사용되면 `Consume()`이 오브젝트를 `Destroy()`로 완전히
   파괴한다 — 캔버스처럼 완료 상태만 잠그는 게 아니라 진짜로 사라져서, `ResetCompletion()` 방식으로는
   되살릴 수 없었다. `data.fixtures`엔 원본 데이터가 그대로 남아있으므로,
   `StopPlayTest()`에서 `cells`를 훑어 `GameObject`가 파괴된(Fixture는 있는데 GameObject가 null인)
   칸만 골라 `CustomStageLoader.PlaceFixture`로 다시 만들어 넣는
   `RestoreConsumedFixtures()`를 추가했다.

**Play 모드 체크리스트(사용자 직접 확인 필요)**:
1. "테스트" 탭 진입 시 카메라만 움직이고 `PlayModePanel`에 시작 버튼만 보이는지.
2. 시작 버튼 클릭 → 팔레트 UI가 전부 사라지고 "Playtesting - ESC to Editor" 배너만 뜨는지, 자유
   시점 카메라가 아니라 실제 1인칭 조작(마우스 커서 잠김)으로 바뀌는지, HUD/브러시 뷰모델이 다시
   보이는지.
3. 배치해 둔 필터·캔버스·팔레트 등이 정상적으로 상호작용되는지(색 스택이 빈 상태로 시작하는지).
   버킷/팔레트/컬러 체인저/스택 체인저처럼 사용하면 사라지는 기물도 정상 동작하는지.
4. ESC 키 → 팔레트 UI 복귀, 자유 시점 카메라 복귀, 테스트 탭에 그대로 남아있는지. 방금 테스트
   중 사용해서 사라졌던 소모성 기물(버킷 등)이 원래 자리에 그대로 복원돼 있는지.
5. 시작→종료→다시 시작을 반복해도 기물이 중복 생성되거나 필터 메시가 깨지지 않는지.
6. 실제로 정답 순서를 전부 맞춰 클리어 → 자동으로 에디터로 돌아가는지, 시간이 멈춘 채로 남지
   않는지, 그 과정에서 사용됐던 소모성 기물도 4번과 마찬가지로 복원돼 있는지.
7. 캔버스가 여러 개인 맵에서 순서대로(또는 뒤섞어) 클리어해보고, "정답 순서" 탭(OrderMode)에서
   각 캔버스 순서 목록이 실제 상호작용 순서대로 자동으로 채워져 있는지(수동 입력 없이) 확인.
   캔버스 자신은 그 목록에 들어가지 않는지도 확인.
8. 클리어 후 저장한 JSON 파일을 텍스트 에디터로 열어 `clearVerified`가 `true`인지, 이후 기물을
   하나라도 놓거나 지우거나 값을 바꾸면 즉시 `false`로 바뀌는지 확인.
9. 같은 캔버스를 테스트에서 두 번 연속 클리어(테스트→종료→다시 테스트→다시 클리어)해도 처음부터
   이미 클리어된 것처럼 보이지 않고 정상적으로 다시 플레이 가능한지.
10. (참고, 이번엔 손 안 댐) 테스트 중 ESC로 일시정지 메뉴를 열고 "다시하기"/"메인메뉴"를 누르면
    저장 안 한 편집 내용이 사라질 수 있음 — 중요한 맵은 테스트 전에 저장 권장.

## 맵 에디터 진입 흐름 개편: 맵 선택 화면 먼저 보여주기 (2026-09-17)

지금까지는 맵 에디터에 들어가면 바로 빈 편집 화면(Place 탭)이 떴고, 저장/불러오기는 편집 중
"Save Load" 탭에서만 가능했다. 사용자가 씬 UI를 먼저 스스로 손봤다: `SaveLoadModePanel` →
**`SaveModePanel`**로 개명하고 `LoadButton`을 완전히 삭제(이제 편집 중엔 저장만 가능, 불러오기는
없음), 기존 `LoadListPopup`의 `Content`에 **`NewMap`** 카드를 하나 더 만들어 목록 맨 앞에 항상
보이도록 배치해뒀다(클릭 동작은 아직 안 붙어 있었음). 이걸 이어받아 `LoadListPopup`을 "저장된 맵
불러오기 팝업"에서 "맵 에디터 진입 시 맨 처음 뜨는 맵 선택 화면"으로 승격시켰다.

**동작 규칙**:
- `MapEditController.Awake()`가 더 이상 `ShowPlaceTab()`을 바로 부르지 않는다 — 대신
  `selectionPanel`(편집 UI 전체)을 비활성화하고 `OpenLoadPopup()`으로 맵 선택 화면(저장된 맵
  목록 + `NewMap` 카드)을 먼저 띄운다.
- **저장된 맵 카드 클릭**: 기존 `LoadMap()` 로직 그대로(변경 없음), 마지막에
  `CloseLoadPopup(); ShowPlaceTab();`이었던 걸 새 헬퍼 `ShowEditorUI()`(`selectionPanel` 활성화 +
  `ShowPlaceTab()`)로 교체해 편집 화면을 확실히 보여주게 했다.
- **`NewMap` 카드 클릭(`OnNewMapButtonSelected`)**: 기존 `SaveNamePopup`(이름 입력 팝업)을 그대로
  재사용해 이름을 받는다. 확인을 누르면(`ConfirmSaveName`에 새로 추가한 분기) **그 자리에서
  파일을 저장하지 않고** `data.title`만 세팅한 채 바로 `ShowEditorUI()`로 편집 화면에 들어간다 —
  사용자가 명시적으로 확인한 사항("저장은 나중에"). `data`는 `Awake()`가 이미 `new
  CustomStageData()` 기본 상태 + 새 GUID로 준비해뒀으므로 "새 맵 만들기"는 사실상 이름만 정하는
  일이다. 이후 편집 중 "저장"을 처음 누르면(이미 제목이 있으므로) 팝업 없이 그 이름으로 파일이
  그때 처음 생긴다(`OpenSavePopup`의 기존 "제목 있으면 팝업 없이 저장" 로직이 그대로 적용됨,
  코드 추가 불필요).
  - `ConfirmSaveName()`이 이제 세 가지 경우(그냥 저장/다른 이름으로 저장/새 맵 만들기)를 구분해야
    해서 `pendingSaveAsNew` 옆에 `pendingNewMap` 플래그를 추가했다. 취소 후 다른 경로로 재진입해도
    플래그가 새지 않도록, 세 진입점(`OpenSavePopup`/`OpenSaveAsPopup`/`OnNewMapButtonSelected`)이
    전부 두 플래그 값을 매번 명시적으로 세팅한다.
- **맵 선택 화면의 `BackButton`**: 기존엔 `CloseLoadPopup`(팝업만 닫고 편집으로 복귀)이었는데,
  이제 애초에 아직 편집할 맵을 고르지 않은 상태이므로 "취소"가 아니라 "나가기"가 맞다 — 새 메서드
  `OnMapSelectBackButton()`으로 재배선했다. 로직은 `ClearScreenController.OnMainMenuButton`/
  `PauseMenuController.OnQuitButton`과 동일한 표준 패턴
  (`GameAudio.PlayButtonClick → Time.timeScale=1f → GameManager.ChangeState(MainMenu) →
  SceneLoader.Load("MainMenu")`).
- 씬의 기본 활성 상태도 런타임과 일치하도록 `SelectionPanel`을 기본 비활성, `LoadListPopup`을
  기본 활성으로 저장해뒀다(이전엔 `LoadListPopup`이 편집 중 실수로 켜진 채 저장돼 있었음).

**변경 파일**: `Assets/Scripts/MapEditor/MapEditController.cs`(`Awake()`, `pendingNewMap`,
`OnNewMapButtonSelected`, `ConfirmSaveName`의 새 맵 분기, `ShowEditorUI()`,
`OnMapSelectBackButton()`), `Assets/Scenes/MapEditor.unity`(`LoadListPopup/Panel/BackButton`,
`.../NewMap`의 onClick 재배선, `SelectionPanel`/`LoadListPopup` 기본 활성 상태).

**참고**: 4번째 탭 GameObject는 `SaveMode`로 개명됐지만 그 onClick은 여전히 `ShowSaveLoadTab`
(이름 불일치, 동작엔 문제없어 이번엔 안 건드림 — 나중에 손대면 같이 정리).

**검증**: 컴파일 정상. `BackButton`/`NewMap.SelectButton`의 onClick target/method 문자열,
`SelectionPanel`/`LoadListPopup` 기본 활성 상태 직접 확인, 씬 저장 완료.

**Play 모드 체크리스트(사용자 직접 확인 필요)**:
1. 맵 에디터 씬 진입 시 편집 화면이 아니라 맵 선택 리스트가 먼저 뜨는지.
2. "New Map" 클릭 → 이름 입력 → 확인 → 그 즉시 파일이 생기지 않고 바로 Place 탭 편집 화면으로
   들어가는지.
3. 그 상태에서 뭔가 배치하고 "Save"를 처음 누르면(이미 이름이 있으므로) 팝업 없이 바로 저장되고,
   실제로 `CustomMap_{id}.json` 파일이 그때 처음 생기는지.
4. 저장된 맵 카드를 클릭하면 정상적으로 불러와 편집 화면으로 들어가는지(기존 동작 그대로).
5. 맵 선택 화면에서 "Back"을 누르면 편집으로 안 돌아가고 메인메뉴로 나가는지.
6. 메인메뉴에서 다시 맵 에디터로 들어가면, 방금 저장한 맵이 목록에 보이고 "New Map" 카드는
   여전히 하나만 맨 앞에 남아있는지(중복 생성 없음).

### 추가 수정 3건(사용자가 Play 모드에서 발견/요청, 2026-09-17)

1. **New Map 확인 후에도 맵 리스트가 안 사라짐**: `OnNewMapButtonSelected()`가 이름 입력 팝업
   (`SaveNamePopup`)만 열고 `LoadListPopup`은 그대로 켜둔 채였다 — 이름 입력 팝업이 다이얼로그
   형태로 그 위에 뜨는 것뿐이라 뒤의 맵 리스트가 계속 보였다. `OnNewMapButtonSelected()`에서
   `OpenSaveNamePopup()` 전에 `CloseLoadPopup()`을 먼저 호출하도록 고쳤다. 이름 입력을 취소하면
   다시 맵 리스트로 돌아가야 하므로, `CancelSavePopup()`에 `pendingNewMap`이 켜져 있으면
   `OpenLoadPopup()`을 다시 호출하는 분기를 추가했다.
2. **맵 에디터에서 일시정지 메뉴가 뜨던 것을 제거**: `PauseMenuController.Update()`의 ESC 처리
   조건에서 `GameState.MapEditor`를 뺐다 — 이제 맵을 편집하는 동안 ESC를 눌러도 아무 일도 안
   일어난다(플레이 테스트 중의 ESC 처리는 `MapEditController.Update()`가 별도로 직접 담당하므로
   영향 없음).
3. **맵 에디터에서 나가는 유일한 경로 = Save 탭의 "Save And Exit" 버튼**: 일시정지 메뉴를 거쳐
   나가던 길이 막혔으므로, 사용자가 미리 만들어 둔(자리표시자로 `OpenSavePopup`에 연결돼 있던)
   `SaveModePanel`의 `SaveAndExitButton`을 실제 기능에 연결했다. 새 메서드
   `OnSaveAndExitButton()`: 이미 이름이 있으면 즉시 저장 후 메인메뉴로 나가고, 아직 이름이 없으면
   (첫 저장) 이름 입력 팝업을 띄운 뒤 확인 시점에 저장하고 나간다 — 이를 위해 `pendingExitAfterSave`
   플래그를 추가하고, 기존 세 진입점(`OpenSavePopup`/`OpenSaveAsPopup`/`OnNewMapButtonSelected`)도
   전부 이 플래그를 명시적으로 `false`로 리셋하도록 맞춰서 이전 시도의 상태가 새지 않게 했다.
   메인메뉴 이동 로직은 `OnMapSelectBackButton`과 완전히 같아서 공용 `ExitToMainMenu()` 헬퍼로
   묶었다.

**변경 파일**: `Assets/Scripts/MapEditor/MapEditController.cs`(`pendingExitAfterSave`,
`OnSaveAndExitButton`, `ExitToMainMenu`, `OnNewMapButtonSelected`/`CancelSavePopup`/`ConfirmSaveName`
수정), `Assets/Scripts/UI/PauseMenuController.cs`(`Update()`에서 `MapEditor` 조건 제거),
`Assets/Scenes/MapEditor.unity`(`SaveAndExitButton` onClick을 `OnSaveAndExitButton`으로 재배선 —
버튼 자체는 사용자가 이미 만들어둔 것).

**검증**: 컴파일 정상. `SaveButton`/`SaveAsButton`/`SaveAndExitButton`/`BackButton`/
`NewMap.SelectButton`의 onClick target/method 문자열 전부 재확인, 씬 저장 완료.

**Play 모드 재확인 필요**:
1. New Map 클릭 → 이름 입력 화면만 보이고 맵 리스트는 안 보이는지, 취소하면 맵 리스트로
   돌아가는지.
2. 맵을 편집하는 중(플레이 테스트 아님) ESC를 눌러도 아무 반응이 없는지.
3. Save 탭의 "Save And Exit" 클릭 → (이름이 이미 있으면) 바로 저장되고 메인메뉴로 나가는지,
   (이름이 없으면) 이름 입력 후 확인하면 저장되고 메인메뉴로 나가는지.

## 전체 시스템 리팩토링: 안정성·유지보수성 (2026-09-17)

### 배경

"전체 시스템 스캔해서 리팩토링 하자, 중점은 안정성과 유지 보수의 편리성" 요청에 따라 Core/Framework
연동·Level/UI·MapEditor·Player/MapObjects 전 영역을 조사했다(Explore 에이전트 3개 병렬 조사 → Plan
에이전트 설계 → 핵심 파일 직접 재검증). 위 상태줄의 "6단계 리팩토링 필요" 메모가 계기 중 하나였다.
동작을 바꾸지 않는 선에서 격리된 버그 수정과 구조 정리 위주로 진행했다(Stage A/B/D). `MapEditController`
8책임 분리(Stage C)는 회귀 위험 때문에 이번엔 보류.

### 최우선 발견 — ProgressManager 세이브 오염 버그

재검증 과정에서 문서화되지 않았던 실제 버그를 새로 찾았다. `ProgressManager.OnStageCleared`가
`GameManager.State`를 확인하지 않고 `StageCleared` 이벤트만 구독하고 있어서, **맵 에디터 플레이
테스트로 클리어할 때마다** 활성 씬 이름("MapEditor")이 그대로 `SaveManager.Instance.Current.
MarkStageCleared("MapEditor")`로 호출되고 즉시 `Save()`까지 실행됐다. 즉 진짜 세이브 파일의
`clearedStages`에 `"MapEditor"`라는 가짜 항목이 이미 쌓였을 가능성이 있다(6단계 작업 중 플레이
테스트 클리어를 여러 번 확인했으므로). `ClearScreenController.OnStageCleared`는 6단계 작업 때 이미
`GameManager.State != Playing` 가드를 넣어 화면 표시는 막았지만, `ProgressManager`는 별도 구독자라
그 가드의 보호를 못 받고 있었다 — 화면만 안 뜰 뿐 세이브는 계속 오염되고 있었던 것.
**수정**: `ProgressManager.OnStageCleared` 맨 앞에 동일한 가드 추가.
**부수 확인 필요**: 세이브 파일(`Application.persistentDataPath` 하위 JSON)의 `clearedStages`에
`"MapEditor"`가 이미 들어있는지 직접 확인 권장 — 있다면 수동으로 지워야 한다.

### Stage A — 안전하고 격리된 버그 수정 (적용 완료)

- **ProgressManager 세이브 오염 가드**(위 항목).
- **StageGuideController 가이드 상태 누수**: `OnSceneLoaded`에 `GameManager.State != Playing`이면
  건너뛰는 가드 추가. `Deactivate()`는 `SceneRestarter`를 거친 재시작에서만 호출되므로, 가이드를
  켠 채로 메인메뉴를 거쳐 맵 에디터에 들어가면(SceneRestarter를 안 거침) `GuideActive`가 켜진 채로
  남아 맵 에디터/플레이 테스트의 `SceneLoadCompleted`에도 반응해 엉뚱한(또는 빈) `MazeGenerator`를
  추적할 수 있었다. (참고: `SceneLoadCompleted`는 map-editor 관련 두 곳 말고도 Framework.Core의
  `SceneLoader.LoadAsync`가 모든 씬 전환마다 발행한다 — 처음엔 이걸 놓쳐서 "실제 스테이지에서도 가이드가
  전혀 안 될 것"이라고 오판했었다.)
- **FilterBlockBase 필터 고착 버그**: `Refresh()`가 `Col.isTrigger`를 true→false로 내리는 순간
  플레이어가 그 콜라이더에 물리적으로 겹쳐 있으면 Unity가 `OnTriggerExit`를 다시는 안 불러서, 모든
  필터가 공유하는 `playerFilterDepth` 정적 카운터가 영구 고착되고 이후 **다른 모든 필터**의 통과
  판정까지 막히는 버그를 확인했다.
  **시행착오**: 처음엔 인스턴스별 `playerCounted` 플래그 + `Refresh()`에서 `Col.bounds.Contains()`로
  겹침을 확인해 카운터를 미리 정리하는 방식으로 고쳤다. Play 모드로 재현·검증하는 과정에서(필터에
  걸친 채로 색을 얻으면 필터가 단단해지며 밀려나는 것까지 확인) 정상 작동을 확인했지만, 사용자가 더
  근본적인 방식을 제안: **애초에 필터 안에 있는 동안은 상호작용 자체를 막아서 색이 못 바뀌게 하면
  이 상황 자체가 생기지 않는다.** 이 편이 "겹침을 감지해 뒷수습하는" 것보다 원인을 원천 차단하는
  더 단순한 해결책이라 판단해 채택 — `playerCounted`/`Refresh()`의 겹침 정리 로직은 전부 되돌리고,
  대신 `FilterBlockBase`에 `public static bool PlayerInsideFilter => playerFilterDepth > 0;`를
  추가해 [InteractionController.cs](Assets/Scripts/Player/InteractionController.cs)의
  `TryInteract()` 호출 직전에 이 값을 확인해 필터 안에 있으면 상호작용 자체를 막도록 변경했다.
  (색 변경형 기물은 전부 `ConsumableObjectBase`의 조준+클릭 상호작용으로만 발동하고 걸어서 닿는
  것으로는 발동하지 않으므로, 이 한 지점만 막으면 근본 원인이 완전히 차단된다.)
- **MapEditController 배열 경계 체크**: `PlacePanelFor`/`UpdateObjectButtonHighlight`/
  `UpdateModeTabHighlight`에 인덱스·null 가드 추가.
- **ConsumableObjectBase**: `GameAudio.Instance` null-조건부 접근으로 방어(Bootstrap 매니저 초기화
  실패 시나리오와 연동).
- **FirstPersonController.OnEnable() 속도 리셋**: `enabled`를 다시 켤 때 남은 낙하/이동 속도·pitch를
  리셋하는 방어 로직 추가(현재 유일한 호출부인 `StartPlayTest()`는 이미 `Teleport()`를 먼저 불러
  실제로는 무해하지만, 앞으로 다른 경로가 생겨도 안전하도록).
- **Bootstrap try/catch + 실행 순서**: `Awake()`의 싱글톤 초기화를 각각 try/catch로 감싸 하나가
  예외를 던져도 나머지는 계속 초기화되게 했고, `[DefaultExecutionOrder(-1000)]`을 추가했다.
- **CustomStageLoader 데이터 유실 경고**: `PlaceFixture`가 프리팹 누락으로 `null`을 반환하는 경로에
  `Debug.LogWarning` 추가.

### Stage B — 구조적 정리 (적용 완료, 동작 변경 없음)

- `pendingSaveAsNew`/`pendingNewMap`/`pendingExitAfterSave` 3개 bool을 `enum SaveIntent { None,
  SaveAsNew, NewMap, ExitAfterSave }` 하나로 통합 — 상호 배타성을 타입으로 보장.
- `editingFixtures.Clear(); editingFixtureInstances.Clear();` 반복 패턴(10곳)을
  `ResetEditingSelection()` 헬퍼로 통합.
- 드래그 배치/제거 중 필터가 섞여 있으면 칸마다 `FilterBlockBase.RebuildAll()`(씬 전체 필터 메시
  재생성)이 반복 호출되던 것을, `PlaceFixtureAt`/`RemoveCell`에 `suppressFilterRebuild` 옵션을 추가해
  드래그가 끝난 뒤 한 번만 호출하도록 지연.
- 기물 등록 공통 헬퍼 추출·메인메뉴 종료 패턴(`ClearScreenController`/`PauseMenuController`/
  `MapEditController` 3곳 중복) 통합은 검토 결과 깔끔한 추출이 어렵거나 여러 클래스에 걸친 변경이라
  이번 "안전한 격리 수정" 범위에서는 보류했다(억지 추상화 방지).

### Stage C — MapEditController 책임 분리 → 보류

`MapEditController.cs`는 약 1700줄, 8가지 책임(배치/제거, 팔레트·파라미터 UI, 값 수정, 정답 순서
편집, 탭 전환, 저장/불러오기+맵 선택, 플레이테스트, 부트스트래핑)이 섞여 있다. 지금 정상 동작 중이고
이번 세션에서 만든 저장 포맷/씬 배선과의 회귀 위험이 커서 이번엔 손대지 않았다. **다음 리팩토링
세션 제안**: 플레이테스트 로직(`StartPlayTest`/`StopPlayTest`/`OnMapObjectUsedDuringPlayTest`/
`OnCanvasCompletedDuringPlayTest`/`OnStageClearedDuringPlayTest`/`RestoreConsumedFixtures`/
`MarkEdited`)을 `MapEditPlayTester`라는 별도 컴포넌트로 분리하는 것부터 시작 — 기존 씬의 UnityEvent
배선(`PlayButton`→`StartPlayTest` 등)은 `MapEditController`에 한 줄짜리 forwarder 메서드를 남겨두면
씬을 다시 배선할 필요가 없다.

### Stage D — 죽은 코드/문서 정리 (적용 완료)

- `AcquireObjectBase`(획득형 기물 베이스, 실제 상속/사용처 없음 확인) — 프로젝트 규칙에 따라 삭제
  대신 `!!!!AcquireObjectBase.cs`로 이름 변경(내용 그대로 보존, 필요 시 git 히스토리에서도 복원 가능).
- § "5단계" 앞에 `SaveLoadModePanel`/Load 버튼 서술이 stale하다는 안내 인용구 추가.

### Framework.Core 패키지 이슈 — 핸드오프만, 이번 세션에서 수정 안 함

`SaveManager`(로드/저장 try/catch 없음)·`EventBus`(구독자 하나가 예외를 던지면 그 뒤 구독자 전부
스킵)·`MonoSingleton`(Awake 순서 미보장) 세 가지 안정성 이슈를 확인했지만, 전부 외부 Git 패키지
(`Packages/manifest.json`의 `FrameWorkCore` 저장소, `Library/PackageCache` 하위)에 있어 이 저장소에서
고쳐도 저장되지 않는다. 사용자 결정에 따라 이번 세션은 패키지를 건드리지 않고,
`Docs/FrameworkCoreFixRequest.md`에 "프레임워크 담당" 세션으로 전달할 구체적 요청서만 작성했다.

**후속(같은 날)**: 사용자가 FrameWorkCore 저장소에 실제로 패치를 적용, `unity command
package_resolve`로 재해석 후 세 가지 수정 사항(SaveManager try/catch화, EventBus 구독자별 예외
격리, MonoSingleton의 `Dependencies` 선언 + 초기화 순서 경고 장치) 전부 반영된 것을 직접 확인했다
(자세한 내용은 `Docs/FrameworkCoreFixRequest.md` 상단 확인 기록 참고). 컴파일 정상.

### 검증

- `unity command recompile` 정상 확인(Stage A/B/D 전체 반영 후, FilterBlockBase 재구현 후에도 재확인).

**Play 모드 체크리스트 — 사용자 확인 완료(2026-09-17, 전부 정상 작동)**:
1. ~~맵 에디터에서 플레이 테스트로 클리어 → 에디터 복귀는 기존과 동일하게 동작하는지, 그리고 실제
   세이브 파일에 `"MapEditor"`가 `clearedStages`로 더 이상 추가되지 않는지 확인.~~ **확인 완료.**
   실제로 세이브 파일(`C:\Users\<유저>\AppData\LocalLow\ColorMaze\ColorMaze\save.json` — companyName이
   `nyapy`로 바뀌기 전 값으로 계속 저장되고 있었음, 에디터 재시작 후에도 동일)에 `"MapEditor"`가
   이미 오염돼 있던 걸 실제로 확인·제거했다. 단, 이미 켜져 있던 Play 세션이 그 파일을 메모리에 들고
   있어서 파일만 지워도 다음 저장 때 되살아나는 걸 한 번 더 겪었다 — `SaveManager.Instance.Current`
   에서도 같이 지워야 했다(Play 모드를 완전히 정지했다 다시 켜면 자동으로 해결됨).
2. ~~필터 안에 플레이어가 겹쳐 있는 상태에서 트리거 상태가 바뀌어도 이후 다른 필터들이 고착 없이
   정상적으로 반응하는지.~~ **확인 완료.** 단, 수정 방식이 바뀌었다 — 위 "FilterBlockBase 필터
   고착 버그" 항목의 시행착오 참고(겹침 감지 방식 → 상호작용 차단 방식).
3. ~~6단계·진입 흐름 개편 기존 기능 회귀 여부(맵 선택→New Map→편집→저장→플레이 테스트→저장 및
   나가기).~~ **확인 완료.**
4. ~~필터가 섞인 범위 Shift+드래그 설치/제거 시 메시 정상 갱신(성능 최적화 확인).~~ **확인 완료.**
5. ~~게임을 새로 켰을 때 정상적으로 메인메뉴로 부팅되는지(Bootstrap 변경 확인).~~ **확인 완료.**

## MapEditController 책임 분리 Phase 1: 캔버스-순서 UI + 탭 전환 (2026-09-18)

### 배경

Stage C(8책임 분리)는 위 "전체 시스템 리팩토링" 세션에서 회귀 위험 때문에 보류됐었다. "MapEditController
8책임 분리 계획해두자" 요청에 따라 Explore 에이전트로 현재 구조(책임별 필드/메서드 인벤토리, 책임 간
교차 참조, 씬 바인딩 표, 공유 필드 소유권)를 전수 조사하고 Plan 에이전트로 구체적 분리 방안을 설계한
뒤, 다음 세션에서 그 계획대로 실제 구현했다(계획 파일: `.claude/plans/partitioned-imagining-gizmo.md`의
"MapEditController 책임 분리 — Phase 1" 섹션 참고).

기존에 이 문서(위 Stage C 항목)는 "플레이테스트부터 분리"를 제안했었지만, 조사 결과 플레이테스트가
`cells`/`data.canvasOrders`를 직접 조작하는 등 오히려 더 위험한 축에 속한다는 게 확인돼 — 대신
**캔버스-순서 편집 UI + 탭 전환**부터 먼저 분리하는 것으로 순서를 바꿨다. 씬 전체(`MapEditor.unity` +
`Valueinput.prefab`)의 `m_MethodName` 바인딩을 grep으로 전수 확인한 결과, 이 두 책임은 씬에 전혀
바인딩되지 않거나(캔버스-순서 관련 메서드는 전부 런타임 델리게이트) 순수 forwarder로만 남는(탭 전환
5개 메서드) 성격이라 회귀 위험이 가장 낮았다.

### 적용 내용

`MapEditController`를 `partial class`로 바꾸고, 공유("God") 필드(`data`, `cells`, `mode` 등)와 모든
`[SerializeField]` 인스펙터 필드는 그대로 둔 채, 두 책임을 **중첩 클래스**로 분리했다 — 각각 `owner`
백레퍼런스로 outer의 private 멤버에 접근한다(C# 중첩 클래스는 인스턴스 참조로 enclosing 타입의 private
멤버에도 접근 가능 — 접근 제한자 변경 없음, 인스펙터 참조도 안 깨짐):

- [MapEditController.CanvasOrderUI.cs](Assets/Scripts/MapEditor/MapEditController.CanvasOrderUI.cs)
  (신규) — `EnterAddToOrderMode`/`AddFixtureToOrder`/`SelectedOrder`/`SelectCanvasOrder`/
  `RefreshCanvasList`/`UpdateCanvasCardHighlight`/`RefreshOrderListUI` 메서드와
  `selectedCanvasFixtureId`/`canvasCardInstances` 필드를 이동. `RemoveCell`/`LoadMap`이
  `selectedCanvasFixtureId`에 직접 접근하던 걸 대체할 `OnCanvasRemoved(int)`/`ResetSelection()`을
  새로 추가.
- [MapEditController.TabController.cs](Assets/Scripts/MapEditor/MapEditController.TabController.cs)
  (신규) — `ShowPlaceTab`/`ShowValueEditTab`/`ShowOrderTab`/`ShowSaveLoadTab`/`ShowPlayTab`/
  `SetActivePanel`/`UpdateModeTabHighlight`를 이동. 5개 Show*Tab은 `MapEditController`에 한 줄짜리
  forwarder(`public void ShowPlaceTab() => tabController.ShowPlaceTab();` 등)로 남겨 씬의 UnityEvent
  바인딩은 전혀 안 건드렸다.

`canvasOrderUI`/`tabController` 두 필드는 `Awake()` 맨 앞에서 생성한다(`owner`로 `this`를 넘겨야 하는데
인스턴스 필드 이니셜라이저에서는 `this`를 쓸 수 없어(CS0027) `readonly` 필드 이니셜라이저 방식은 포기하고
`Awake()`에서 생성하는 방식으로 변경).

**범위 밖(다음 세션)**: 나머지 6개 책임(배치/제거, 팔레트·값 수정 UI, 저장/불러오기, 플레이테스트,
부트스트래핑)은 그대로 `MapEditController`에 남아있다 — 계획 파일의 "범위 밖" 절 참고.

### 검증

- `unity command recompile` 정상 확인(에러 0건).
- 계획에 정리해둔 8개 호출부 수정(`Awake`/`PlaceFixtureAt`/`RemoveCell`×2/`LoadMap`×2/
  `OnCanvasCompletedDuringPlayTest`) + 탭 전환 5개 forwarder 전부 grep으로 재확인.
- 씬/프리팹 파일은 이번 변경에서 전혀 열지 않음(수정 자체가 없음).

**Play 모드 체크리스트(사용자 확인 필요)**:
1. 맵 에디터 진입 → 맵 선택 화면 정상 표시.
2. New Map → 이름 입력 → Place 탭 진입.
3. Mode1~4 탭 전환이 정상 동작(TabController forwarder 확인).
4. Canvas 기물 배치 → 캔버스 카드 생성, "기물 추가" 모드로 순서 추가/Up/Down/Remove 정상 동작.
5. 배치된 Canvas 기물 제거 → 카드/순서 즉시 사라짐, 선택 중이었으면 OrderList도 비워짐.
6. 저장된 맵 불러오기 → 블록/기물/캔버스 카드/순서 목록 전부 정상 복원(가장 중요한 회귀 확인 지점).
7. 플레이 테스트로 캔버스 클리어 → 자동 기록된 순서가 정상 반영되고 OrderList UI가 실시간 갱신됨.
8. 저장 및 나가기 정상 동작.

**사용자 확인 완료(2026-09-18)**: 위 체크리스트 전항목 정상 작동.

## 정답 순서 수동 편집 UI 제거 (2026-09-18)

### 배경

위 Phase 1 체크리스트 검증이 끝난 뒤, 사용자가 "정답 순서 패널 없애서 관련 코드 정리해도 돼"라고
요청. 6단계에서 정답 순서 자동 기록(플레이 테스트 중 상호작용 순서를 캔버스 클리어 시점에 자동
저장)이 이미 충분히 검증됐으므로, 그 데이터를 수동으로 조회·편집하던 UI(`CorrectOrder`/
`CanvasList`/`OrderList`/"기물 추가" 버튼, § "4단계" 참고)는 더 이상 필요 없어졌다.

라이브 Unity 연동으로 `MapEditController`의 직렬화 필드를 직접 조회해보니, `orderListContent`/
`orderItemTemplate`/`canvasListContent`/`canvasCardTemplate`/`addToOrderButton`/`mode3Panel`이
전부 이미 `null`이었다 — 사용자가 씬에서 해당 UI를 이미 직접 제거해둔 상태였고, 이번 작업은 그에
맞춰 **더 이상 아무것도 채우지 못하는 코드만 정리**하는 것이었다(씬 파일은 이번 세션에서 전혀
수정하지 않음).

### 적용 내용

`data.canvasOrders`/`CanvasOrderEntry`(캔버스별 정답 순서 데이터 자체)와 플레이 테스트의 자동 기록
로직(`OnMapObjectUsedDuringPlayTest`/`OnCanvasCompletedDuringPlayTest`)은 게임플레이에 필요한
핵심 데이터라 전혀 건드리지 않았다. 제거한 건 그 데이터를 **에디터에서 수동으로 조회·편집하던
UI 코드**뿐이다.

- [MapEditController.CanvasOrderUI.cs](Assets/Scripts/MapEditor/MapEditController.CanvasOrderUI.cs)
  → [!!!!MapEditController.CanvasOrderUI.cs](Assets/Scripts/MapEditor/!!!!MapEditController.CanvasOrderUI.cs)로
  이름 변경. 이 클래스의 모든 메서드가 이제 없는 UI 참조에 의존하므로(예: `orderListContent`가
  삭제됨) 컴파일에서 빠지도록 전체를 주석 처리해뒀다 — 검토 후 파일째로 삭제할 것.
- [!!!!CanvasCardUI.cs](Assets/Scripts/MapEditor/!!!!CanvasCardUI.cs),
  [!!!!OrderListItemUI.cs](Assets/Scripts/MapEditor/!!!!OrderListItemUI.cs) — 위 UI 전용 필드
  홀더 컴포넌트, 다른 사용처 없음 확인 후 이름 변경(내용은 그대로 — `AcquireObjectBase` 때와 동일한
  방식).
- [MapEditController.cs](Assets/Scripts/MapEditor/MapEditController.cs): `orderListContent`/
  `orderItemTemplate`/`canvasListContent`/`canvasCardTemplate`/`addToOrderButton`/`mode3Panel`/
  `canvasOrderUI` 필드 제거. `EditorMode` enum에서 `AddToOrder`/`OrderView` 제거(`Place, ValueEdit,
  SaveLoad, PlayView` 4개만 남음). `Update()`의 모드별 분기, `LateUpdate()`의 가드 조건에서 두 모드
  케이스 제거. `PlaceFixtureAt`/`RemoveCell`/`LoadMap`/`OnCanvasCompletedDuringPlayTest`에서
  `canvasOrderUI.*` 호출부만 제거하고 데이터 조작 로직(`data.canvasOrders` 추가/삭제/복사)은 그대로
  유지. `ShowOrderTab()` forwarder 제거.
- [MapEditController.TabController.cs](Assets/Scripts/MapEditor/MapEditController.TabController.cs):
  `ShowOrderTab()` 메서드와 `SetActivePanel`의 `mode3Panel` 처리 제거. `modeTabImages` 배열(씬에 5칸
  중 가운데 1칸이 이미 비어있는 상태 — Mode3 버튼 자체가 씬에 없었음)과 `UpdateModeTabHighlight`의
  인덱스 인자(SaveLoad=3, Play=4)는 지금도 정상 동작하므로 손대지 않았다(깨지지 않은 걸 굳이
  리넘버링하지 않음).

### 검증

- `unity command recompile` 정상 확인(에러 0건).
- grep으로 `EditorMode.AddToOrder`/`OrderView`/`canvasOrderUI`/`ShowOrderTab`/`mode3Panel`/
  `orderListContent` 등 관련 식별자가 살아있는 코드에 하나도 안 남았는지 재확인.
- 라이브 Unity 연동으로 씬의 `MapEditController` 직렬화 필드를 다시 조회해 이번 변경으로 씬 파일이
  전혀 바뀌지 않았음을 확인.

**Play 모드 체크리스트(사용자 확인 필요)**: 이번 변경은 이미 비어있던 필드를 참조하던 죽은 코드
제거라 기능적으로는 무영향이어야 하지만, 혹시 모르니 아래만 재확인 권장.
1. Mode1(배치)/Mode2(값 수정)/Mode4(저장)/Mode5(플레이) 탭 전환이 여전히 정상 동작.
2. Canvas 기물 배치·제거가 정상 동작(카드 UI는 없지만 `data.canvasOrders` 추가/삭제 자체는 계속 됨).
3. 플레이 테스트로 캔버스 클리어 → 정답 순서 자동 기록·저장이 여전히 정상 동작(6단계 체크리스트와
   동일).

**사용자 확인 완료(2026-09-18)**: 위 체크리스트 전항목 정상 작동.

## MapEditController 책임 분리 Phase 2: 저장/불러오기 + 플레이테스트 (2026-09-18)

### 배경

Phase 1과 위 정답 순서 UI 제거까지 전부 Play 모드 검증이 끝난 뒤 "Phase 2 계획 시작하자" 요청에 따라
진행. Explore 에이전트로 현재(Phase 1 이후) 상태를 재조사하고 Plan 에이전트로 분리안을 설계한 뒤,
이번엔 계획 승인 즉시 같은 세션에서 구현까지 진행했다(계획 파일:
`.claude/plans/partitioned-imagining-gizmo.md`의 "MapEditController 책임 분리 — Phase 2" 섹션 참고).

남은 6개 책임 중 `Cat6`(부트스트래핑, `Awake`/`OnEnable`/`OnDisable`은 Unity 매직 메서드라 분리 불가)과
`Cat1`+`Cat2`+`Cat3`(배치/제거·팔레트 UI·값 수정 — `ClickColorButton` 등에서 메서드 본문 자체가 얽혀
있어 가장 위험한 "핵심 3인방")는 이번에도 손대지 않고, `Cat4`(저장/불러오기)와 `Cat5`(플레이테스트)만
Phase 1과 동일한 위험도(서로 메서드 본문을 공유하지도 호출하지도 않음)로 판단해 이번 범위로 확정했다.

### 적용 내용

Phase 1과 동일한 nested partial class + `owner` 백레퍼런스 패턴을 그대로 반복 적용:

- [MapEditController.SaveLoadController.cs](Assets/Scripts/MapEditor/MapEditController.SaveLoadController.cs)
  (신규) — 저장/불러오기 + 맵 선택 전담. `myMapCardInstances`/`SaveIntent` enum/`saveIntent`/
  `SaveFilePrefix`/`SaveFileName` 필드와 `OpenSavePopup`/`OpenSaveAsPopup`/`OnSaveAndExitButton`/
  `OnNewMapButtonSelected`/`OpenSaveNamePopup`/`ConfirmSaveName`/`SaveCurrentMap`/`CancelSavePopup`/
  `OpenLoadPopup`/`CloseLoadPopup`/`RefreshLoadList`/`LoadMap`/`ShowEditorUI`/`OnMapSelectBackButton`/
  `ExitToMainMenu` 메서드를 이동. `SetTitle(string)`은 파일 위치가 Cat2/3 구역이고 씬 바인딩도 없는
  내부 전용 메서드라 Phase 1의 `CanvasCount` 사례와 동일하게 이번 범위 밖으로 남기고
  `owner.SetTitle(title)`로 계속 호출.
- [MapEditController.PlayTestController.cs](Assets/Scripts/MapEditor/MapEditController.PlayTestController.cs)
  (신규) — 플레이 테스트 전담. `isPlayTesting`(공개 `IsPlayTesting` 프로퍼티로 노출)/`playTestMaze`/
  `PlayerSpawnPosition`/`playTestUsedSequence`/`playTestFixtureIdByInstance` 필드와 `StartPlayTest`/
  `StopPlayTest`/`RestoreConsumedFixtures`/`OnMapObjectUsedDuringPlayTest`/
  `OnCanvasCompletedDuringPlayTest`/`OnStageClearedDuringPlayTest` 메서드를 이동.
- [MapEditController.cs](Assets/Scripts/MapEditor/MapEditController.cs): `saveLoadController`/
  `playTestController` 필드를 `Awake()` 맨 앞에서 생성(Phase 1과 동일하게 CS0027 회피 위해 필드
  이니셜라이저가 아닌 `Awake()` 본문에서 생성). `[SerializeField]` 필드(`saveNamePopup` 등,
  `selectionPanel`/`playTestOverlay`)는 그대로 outer에 남기고 `owner.`로 읽는다. `OnEnable`/
  `OnDisable`의 `EventBus` 구독 대상을 `playTestController.OnXxx`로 교체. 씬 바인딩이 있는 9개
  메서드(`OpenSavePopup`/`OpenSaveAsPopup`/`OnSaveAndExitButton`/`OnNewMapButtonSelected`/
  `ConfirmSaveName`/`CancelSavePopup`/`OnMapSelectBackButton`/`StartPlayTest`/`StopPlayTest`)는
  한 줄짜리 forwarder로 남겨 씬 파일은 전혀 안 건드렸다. `Update()`의 `isPlayTesting`/`StopPlayTest()`
  참조(계획엔 명시 안 됐던 호출부 — 직접 grep으로 확인 후 추가 수정)를
  `playTestController.IsPlayTesting`/`playTestController.StopPlayTest()`로 교체. `MarkEdited()`는
  Cat1/Cat3가 광범위하게 호출하는 범용 유틸이라 그대로 outer에 남김.

**범위 밖(다음 세션)**: `Cat1`+`Cat2`+`Cat3` "핵심 3인방", `RegisterPreplacedBlocks()`(위치는 Cat6,
본문은 Cat1), `SetTitle()`(위치는 Cat2/3, 기능은 Cat4) — 계획 파일의 "범위 밖" 절 참고.

### 검증

- `unity command recompile` 정상 확인(에러 0건) — Step 2A(SaveLoadController) 완료 직후, Step 2B
  (PlayTestController) 완료 직후 각각 별도로 확인.
- 씬 바인딩 9개(`m_MethodName: StartPlayTest`/`StopPlayTest` 등)를 grep으로 재확인 — 이번 변경으로
  씬 파일 자체는 전혀 열지 않음.

**Play 모드 체크리스트(사용자 확인 필요)**:
1. 맵 에디터 진입 → 맵 선택 화면 정상 표시, New Map → 이름 입력 → Place 탭 진입.
2. 블록·기물(Canvas 포함) 배치 → Save(첫 저장은 이름 팝업) → Save As로 별도 이름 저장.
3. 메인메뉴로 나갔다 다시 들어가거나 씬 재로드 후, 저장된 맵 불러오기 → 블록/기물/캔버스 순서
   데이터 전부 정상 복원(가장 중요한 회귀 확인 지점 — `LoadMap`).
4. 값 수정 탭에서 기물 마크 클릭이 여전히 정상 동작(회귀 확인).
5. 플레이 테스트 시작 → 플레이어 스폰/커서 잠금/1인칭 조작 정상.
6. 소모성 기물(Bucket/Palette/ColorChanger/StackChanger) 하나 사용 → 파괴 확인.
7. "에디터로 돌아가기"로 테스트 종료 → 소모됐던 기물이 복구되는지(`RestoreConsumedFixtures`) 확인.
8. 두 번째 플레이 테스트를 ESC(일시정지 경유)로 종료해도 동일하게 복구되는지 확인.
9. 정답 순서를 전부 맞춰 실제 클리어 → 자동으로 에디터 복귀, `data.clearVerified`가 true로
   세팅되는지(저장 후 파일 확인 또는 이어서 편집 시 false로 리셋되는지로 간접 확인).
10. "저장 및 나가기" 정상 동작(이미 이름이 있으므로 팝업 없이 바로 저장).

**사용자 확인 완료(2026-09-18)**: 위 체크리스트 전항목 정상 작동.

## MapEditController 책임 분리 Phase 3: 배치/제거 + 팔레트 UI + 값 수정 (2026-09-18)

### 배경

Phase 1·2가 전부 Play 모드 검증까지 끝난 뒤 "Cat1+Cat2+Cat3 분리도 계획 세우자" 요청으로 진행된
마지막 Phase. 이 셋은 앞선 두 Phase에서부터 "핵심 3인방"으로 지목돼 있었다 — Explore 에이전트 2개
(Cat1 / Cat2+Cat3)를 병렬로 띄워 재조사한 결과, Cat1(배치/제거, `cells` 소유)은 씬/프리팹에 바인딩된
메서드가 하나도 없어 독립적으로 분리 가능했지만, Cat2(팔레트 UI)와 Cat3(값 수정)는 `SetPresetR/G/B`/
`ClickColorButton`이 `editingFixtures.Count > 0` 하나로 배치/수정 두 흐름을 같은 메서드 몸체 안에서
분기 처리하고 `HideParamPanels`/`IsParamPanelOpen`도 두 카테고리 UI 상태를 무조건 같이 참조해 완전히
분리 불가능함이 재확인됐다 — 로직 중복 없이는 쪼갤 수 없으므로 **Cat2+Cat3를 하나의 컨트롤러로 묶어서
분리**했다. Cat1 → Cat2+Cat3 순서로 각각 컴파일 게이트를 두고 진행(Phase 2의 실행 패턴과 동일).

### 적용 내용

- [MapEditController.PlacementController.cs](Assets/Scripts/MapEditor/MapEditController.PlacementController.cs)
  (신규, Cat1) — 배치/제거 + 호버 미리보기 + Shift 드래그 범위 설치/제거 전담. `hoverPreview`/드래그
  상태 필드(`dragging`은 `PlayTestController`가 직접 쓰므로 `public` 필드로 승격) 및
  `RegisterPreplacedBlocks`/`TryPlace`/`TryRemove`/`PlaceBlockAt`/`PlaceFixtureAt`/`RemoveCell`/
  호버·드래그 관련 메서드 전부를 이동. `cells`/`data`/`nextFixtureId`/`mazeRoot`/`mapObjectsRoot`/
  `Register()`/`MarkEdited()`/`IsFilter()`는 이번에도 outer의 "God 필드"로 남겨 `owner.`로 읽는다
  (`IsFilter`는 `static`이라 `owner.` 없이 바로 호출).
- [MapEditController.FixtureEditController.cs](Assets/Scripts/MapEditor/MapEditController.FixtureEditController.cs)
  (신규, Cat2+Cat3 통합) — 기물 팔레트(배치 파라미터) UI + 값 수정 모드 전담. `currentFixtureType`
  (→ `public CurrentFixtureType` 프로퍼티), `PresetValues`(→ `public class`로 승격 — `GetPreset`이
  `public`이 되면서 반환 타입도 함께 공개해야 하는 CS0053 제약 때문), `presets`/`editingFixtures`/
  `editingFixtureInstances`/`editColorClicks`/`marks` 등 비직렬화 상태와, `SetPresetR/G/B`/
  `ClickColorButton`/`HideParamPanels`/`IsParamPanelOpen`(엔탱글먼트 메서드, 쪼개지 않고 통째로 이동)을
  비롯한 Cat2/Cat3 메서드 전부를 이동. `LateUpdate()`의 마크 화면투영 로직은 `UpdateMarkProjections()`
  라는 평범한 메서드로 옮기고(중첩 클래스에 "LateUpdate"라는 이름을 그대로 두면 Unity 콜백처럼 오인될
  수 있어 개명), outer의 실제 `LateUpdate()`는 한 줄 위임으로 교체. 13개 `[SerializeField]` 필드
  (`fixtureValuePanels`/`editRgbInputPanel`/`markNormalColor` 등)와 `FixtureValuePanel` 클래스는
  이번에도 전부 outer에 그대로 두고 `owner.`로 읽는다 — 이미 컴파일 통과한 `TabController.
  UpdateModeTabHighlight`가 `owner.tabNormalColor`를 직접 참조하고 있어서, 만약 옮기면 이번 범위 밖인
  그 파일까지 불필요하게 손대야 한다는 게 설계 검토 중 확인됐다.
- [MapEditController.cs](Assets/Scripts/MapEditor/MapEditController.cs): `placementController`/
  `fixtureEditController` 필드를 `Awake()` 맨 앞에서 생성. `Update()`의 배치/제거/드래그/호버 호출을
  전부 `placementController.*`로, `editingFixtures.Count > 0 && ... EndEdit()`을
  `fixtureEditController.HasEditSelection && ... fixtureEditController.EndEdit()`으로 교체.
  `UpdateCursorLock()`의 `IsParamPanelOpen` 참조를 `fixtureEditController.IsParamPanelOpen`으로 교체
  (이 메서드 자체는 Cat1/Cat3 내용이 전혀 없는 순수 Cat6 로직이라 outer에 그대로 둠). 씬/프리팹
  바인딩이 있는 4개 메서드(`SelectBlockTool`/`SelectFixtureTool`/`SetPresetRGB`/`ClickColorButton`)는
  한 줄짜리 forwarder로 남기고, 씬 바인딩이 없던 `SelectEditTool()`은 forwarder 없이 완전히 삭제
  (유일한 호출자인 `TabController.cs`가 새 컨트롤러를 직접 부르도록 수정).
- 기존 3개 파일의 호출부 수정(전부 컴파일 오류로 확인 후 수정, 기능 변경 없음): `TabController.cs`
  (`ShowValueEditTab`/`ShowSaveLoadTab`/`ShowPlayTab` 안의 `owner.SelectEditTool()`/
  `owner.ResetEditingSelection()`/`owner.HideParamPanels()`/`owner.ClearMarks()` 6곳을
  `owner.fixtureEditController.*`로), `SaveLoadController.cs`(`LoadMap`의
  `owner.ResetEditingSelection()` 1곳), `PlayTestController.cs`(`StartPlayTest`의
  `owner.HideHoverPreview()`/`owner.ClearDragPreview()`/`owner.dragging = false`를
  `owner.placementController.*`로, `owner.ResetEditingSelection()`/`owner.HideParamPanels()`/
  `owner.ClearMarks()`를 `owner.fixtureEditController.*`로).

이걸로 원래 8개였던 책임(배치/제거, 팔레트 UI, 값 수정, 정답 순서 편집[Phase 1에서 제거됨], 탭 전환,
저장/불러오기, 플레이테스트, 부트스트래핑) 중 `Awake`/`OnEnable`/`OnDisable`/`Update`/`LateUpdate`
(Unity 매직 메서드라 물리적으로 outer에 남아야 하는 최소한의 부트스트래핑 껍데기)를 제외한 전부가
중첩 컨트롤러로 분리 완료됐다.

### 검증

- `unity command recompile` 정상 확인(에러 0건) — PlacementController 완료 직후, FixtureEditController
  완료 직후 각각 별도로 확인. 중간에 컴파일 에러(CS1061, 호출부 수정 누락)가 여러 건 났었으나 전부
  outer/3개 사이드 파일의 호출부를 새 컨트롤러 경유로 고쳐서 해결.
- grep으로 `currentFixtureType`/`IsPlaceReady`/`GetPreset`/`ResetEditingSelection`/`HideParamPanels`/
  `ClearMarks`/`SelectEditTool` 참조가 전부 `owner.`/`fixtureEditController.`로 정상 한정됐는지 재확인
  (이미 죽은 코드인 `!!!!MapEditController.CanvasOrderUI.cs`의 주석 처리된 참조는 컴파일에서 제외되므로
  무관).

**Play 모드 체크리스트(사용자 확인 필요, 이번이 8책임 분리 전체의 마지막 검증)**:

*배치/제거(Cat1)*:
1. 일반 블록 배치, 7종 기물 각각 배치 → 기본 파라미터로 정상 생성.
2. Ctrl+좌클릭으로 블록/기물 제거.
3. 호버 프리뷰(초록/빨강)가 UI 위·일시정지 중·카메라 전용 모드에선 숨겨지는지.
4. Shift+드래그 설치/제거(색 미선택 RGBSelect류는 드래그 설치 안 됨, 필터 포함 시 리빌드 1회만).
5. Canvas 8개째 배치 거부(7개 제한).

*팔레트/배치 파라미터(Cat2)*:
6. 각 기물 버튼 선택 시 패널 확장 + 올바른 서브패널(RGBInput/RGBSelect) 표시.
7. 재선택 시 프리셋 초기화.
8. RGBInput 6자리 입력 → 배치 프리셋에 정확히 반영.
9. RGBSelect 색상 버튼(단일/StackChanger 2개 조합) → 배치된 기물 파라미터 정확한지.
10. 기물 종류 전환 시 프리셋이 서로 안 새는지.

*값 수정 모드(Cat3)*:
11. Mode2 탭 진입 → 마크 표시(ColorChanger 제외).
12. 수정자 없는 클릭/Shift+클릭(동일 값만 추가)/Ctrl+클릭(6방향 BFS 연쇄 선택) 각각 정상 동작.
13. 선택 중 값 변경 → 즉시 월드 반영 + 필터면 리빌드.
14. Space로 EndEdit → 선택 해제·패널 닫힘·마크 하이라이트 원복.
15. 카메라 이동에 따라 마크가 화면 안/밖으로 정상 표시(`UpdateMarkProjections`).

*엔탱글먼트 교차 회귀*:
16. `SetPresetR/G/B`·`ClickColorButton`을 배치 흐름/수정 흐름 양쪽 다 테스트.
17. 배치 패널/편집 패널 각각 열림 상태에서 커서 잠금 규칙 정상(`IsParamPanelOpen`/`UpdateCursorLock`).

*저장/불러오기·플레이테스트 회귀*:
18. 값 수정 선택 중 다른 맵 불러오기 → 선택이 완전히 초기화되는지.
19. 드래그/호버 중 플레이 테스트 시작 → 고스트 안 남고 상태 리셋.
20. 값 수정 선택·마크·패널이 열린 채로 플레이 테스트 시작·종료 → 전부 정리되는지.
21. 소모성 기물 사용 후 플레이 테스트 종료 → 정상 복구.

---

## Phase 3 이후 버그 수정 및 기능 추가 (2026-09-18)

Phase 3 Play 모드 체크리스트 검증 중 사용자가 실제로 발견한 문제 2건과, 그 과정에서 나온 사용성
개선 요청 1건.

### 버그 1 — RGBSelect류 기물 재선택 시 프리셋이 초기화 안 됨

**증상**: RGB 필터에서 색을 고른 뒤 같은 RGB 필터 버튼을 다시 선택(재선택)해도 골라둔 색이 그대로
남아있었다(체크리스트 7번).

**근본 원인**: [MapEditController.FixtureEditController.cs](Assets/Scripts/MapEditor/MapEditController.FixtureEditController.cs)의
`ResetPanelValues(FixtureType type)`가 RGBInput류(ColorFilter/Canvas/Palette)인지 RGBSelect류
(RgbFilter/Bucket/StackChanger)인지를 `panel.rgbField != null`로 판별하고 있었는데, 이 조건은
신뢰할 수 없었다 — 모든 기물 버튼의 `Valueinput`이 구조상 `RGBInput`/`RGBSelect` 서브오브젝트를
둘 다 갖고 있고, `fixtureValuePanels` 인스펙터 배열의 `rgbField` 참조가 RGBSelect류 항목에도
전부 연결돼 있었다(씬 파일 직접 확인). 그 결과 RGBSelect류도 항상 "RGBInput류" 분기를 타서
`preset.colorClicks`가 절대 안 지워지고, 화면에 안 보이는 RGBInput 텍스트만 지워지는 무의미한
동작을 하고 있었다. `SetButtonExpanded`/`BeginEditFixture` 등 다른 곳은 이미 올바르게
`UsesRgbInput(type)`(기물 타입 자체)로 분기하고 있었으므로, `ResetPanelValues`만 이 패턴을
안 따르고 있었던 것.

**수정**: `ResetPanelValues`의 분기 조건을 `panel.rgbField != null` → `UsesRgbInput(type)`으로
교체(다른 메서드들과 동일한 패턴으로 통일). 사용자가 재테스트해 정상 작동 확인.

### 버그 2 — RGB 입력창에 스페이스바 등 아무 문자나 입력됨

**증상**: RGBInput 텍스트 입력창(ColorFilter/Canvas/Palette 배치 패널 + 값 수정 모드 공용)에
숫자가 아닌 스페이스바를 눌러도 그대로 공백 문자가 입력됐다.

**근본 원인**: C# 코드가 아니라 `Assets/Prefebs/Valueinput.prefab`의 `TMP_InputField` 설정
문제였다 — `m_ContentType: Standard`(0), `m_CharacterValidation: None`(0)이라 애초에 어떤 문자
입력도 제한이 없었다. 이 프리팹 하나를 ColorFilter/Canvas/Palette 배치 패널 3곳 + 값 수정 모드
입력창 1곳, 총 4곳이 프리팹 인스턴스로 공유하고 있어 전부 동일하게 영향받고 있었다.

**수정**: 라이브 Unity 연동으로 프리팹의 `TMP_InputField.ContentType`을 `IntegerNumber`로
변경(`m_CharacterValidation`도 `Integer`로 자동 설정됨) 후 프리팹에 Apply — 코드 수정 없이 4곳
전부 한 번에 숫자 외 입력이 원천 차단됐다. 사용자가 재테스트해 정상 작동 확인.

### 기능 추가 — 배치 모드에서도 스페이스바로 값 입력 패널 닫기

**요청 배경**: 값 수정 모드는 이미 스페이스바(`InputManager.ReadConfirm()`)로 편집을 종료할 수
있었지만(`EndEdit()`), 배치 모드에는 대응하는 동작이 없어 패널을 닫을 방법이 스페이스바로는
없었다. 사용자에게 확인한 결과, 배치 모드에서는 **패널만 닫고 선택된 기물 종류는 유지**(연속
배치를 막지 않기 위해)하는 쪽으로 결정.

**구현**: [MapEditController.cs](Assets/Scripts/MapEditor/MapEditController.cs)의 `Update()`에서
스페이스바 처리를 분기 확장 — 값 수정 모드 선택이 있으면 기존대로 `EndEdit()`, 없고 배치 모드
패널이 열려 있으면(`fixtureEditController.IsParamPanelOpen`) `HideParamPanels()`만 호출(기물
종류 선택은 그대로 유지). 사용자가 재테스트해 정상 작동 확인.

### 검증

`unity command recompile` 매 수정 후 에러 0건 확인. 세 가지 전부 사용자가 Play 모드에서 직접
테스트해 정상 작동 확인 완료.
