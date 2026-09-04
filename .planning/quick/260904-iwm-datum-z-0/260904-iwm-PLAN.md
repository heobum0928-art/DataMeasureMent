---
phase: quick-260904-iwm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/SystemHandler.cs
autonomous: false
requirements: [QUICK-260904-IWM]

must_haves:
  truths:
    - "시퀀스마다 '기준점(Datum) 촬영 = 새 사이클 시작' 인 z_index 를 다르게 가질 수 있다 (Top=0, Bottom=11, Side2=11 …)"
    - "Datum 노드 속성창(PropertyGrid)에 기준점 Z 번호 입력칸이 보이며, Datum 알고리즘 종류와 무관하게 4개 알고리즘 전부에서 보인다"
    - "아무 설정도 하지 않으면(-1 = 자동) 그 시퀀스가 소유한 Shot 의 ZIndex 최솟값이 자동으로 기준점이 된다"
    - "기존 레시피(INI 키 없음 → -1 자동, Shot 이 0 부터 시작)는 동작이 지금과 100% 같다 (회귀 0)"
    - "사용자가 직접 넣은 값은 자동값보다 우선하며, Shot 최솟값과 다르면 경고창이 뜨되 저장은 막히지 않는다"
    - "기준점 값은 레시피 {prefix}_DATUM_{d} 섹션에 자동 저장/로드되고, 비활성 시퀀스(다른 CameraRole)의 값도 기존 섹션-통째-보존 경로로 유지된다"
    - "Debug|x64 빌드 error CS 0"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "DatumZIndex 프로퍼티(+자동 sentinel) + 사용자 편집 시에만 뜨는 경고 + Load 키부재 가드"
      contains: "DatumZIndex"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "TryGetOwnedShotZIndexRange + GetDatumZIndex() 실효값 접근자 + 모든 판정 지점 치환"
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "StartV1Scoped 사이클 시작 판정 + GetPrepZIndex 폴백을 시퀀스 실효값으로 치환"
  key_links:
    - from: "DatumConfig.DatumZIndex"
      to: "InspectionSequence.GetDatumZIndex()"
      via: "소유 DatumConfigs 순회 → 지정값들의 최솟값"
      pattern: "DatumZIndex"
    - from: "DatumConfig 세터"
      to: "소유 InspectionSequence"
      via: "Owner as InspectionSequence (생성 경로가 AddDatum 단일)"
      pattern: "Owner as InspectionSequence"
    - from: "DatumConfig.Save/Load"
      to: "레시피 INI {prefix}_DATUM_{d} 섹션"
      via: "ParamBase 리플렉션 Int32 자동 직렬화 + Load 오버라이드 ContainsKey 가드"
      pattern: "ContainsKey\\(\"DatumZIndex\"\\)"
    - from: "SystemHandler.StartV1Scoped"
      to: "InspectionSequence.GetDatumZIndex()"
      via: "seq as InspectionSequence 캐스트 후 비교 대상 값만 치환"
      pattern: "GetDatumZIndex\\(\\)"
    - from: "InspectionSequence.AddResponseV1Cycle / OnStart / ShouldSkipMeasurementAfterDatumPhase"
      to: "GetDatumZIndex()"
      via: "const DATUM_Z_INDEX 제거 후 전 판정 지점이 같은 접근자를 소비"
      pattern: "GetDatumZIndex\\(\\)"
---

<objective>
시퀀스별 "기준점(Datum) 촬영 = 새 사이클 시작" 인 z_index 를 **Datum 노드 속성창에서 직접 넣는 설정값**으로 만든다.

Purpose: 제어(PLC)가 40개 버퍼 번호를 시퀀스마다 구간으로 나눠 배정한다(Top=0~4, Bottom=11~40, Side1=0~3, Side2=11~13 …). 각 구간의 **시작 번호가 그 시퀀스의 기준점 촬영이고 새 사이클 시작**이다. 그런데 프로그램은 지금 "z_index 0" 하나만 기준점으로 취급하도록 상수로 박혀 있어서, Bottom 처럼 11 부터 시작하는 시퀀스는 사이클이 영원히 시작되지 않는다. 값 매칭(어떤 번호가 오면 같은 ZIndex 의 Shot 을 찍는다)은 이미 되고 있고, $PREP z 도 시퀀스별로 기억된다. 남은 걸림돌은 "기준점 = 0 고정" 하나뿐이다.
Output: Datum 속성창에 기준점 Z 번호 입력칸 추가(저장은 기존 Datum 섹션에 자동) → 시퀀스 실효값 접근자 → 판정 지점 전면 치환.

**사용자 확정 결정:** 입력칸은 **Datum 노드 속성창(DatumConfig)** 에 둔다. 시퀀스 노드(InspectionMasterParam 프록시)는 쓰지 않는다.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md

@WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
@WPF_Example/Custom/SystemHandler.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
</context>

<analysis>

## 플래너 실측 확인 (전부 실제 파일에서 재검증함)

### "0 = 기준점" 이 박혀 있는 곳 — 전수 grep 결과가 이게 전부다

`grep -rn "DATUM_Z_INDEX\|DATUM_TEST_Z_INDEX" WPF_Example/ --include=*.cs` 및
`grep -rn "ZIndex == 0\|nCurZ == 0\|nZIndex == 0"` 로 확인. **아래 목록 밖에 숨은 0 비교는 없다.**

`WPF_Example/Custom/SystemHandler.cs`
- `:263` `private const int DATUM_TEST_Z_INDEX = 0;`
- `:302` `StartV1Scoped` — `nPrepZIndex == DATUM_TEST_Z_INDEX` → 사이클 시작 분기
- `:336` `inspDatumSeq.FindZeroIndexDatumTriggerActionIndices()` 호출
- `:44-60` `GetPrepZIndex(seqName)` — 기록 없음 → `0` 폴백 (2군데 return 0)

`WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- `:88` `private const int DATUM_Z_INDEX = 0;`
- `:453` `OnStart` — `GetExecutionZIndex() == DATUM_Z_INDEX` → `ClearDatumTransforms()` (새 사이클 캐시 비움)
- `:1561` `IsDatumOnlyExecutionIndex` 최상단 가드
- `:1663` `FindZeroIndexDatumTriggerActionIndices()` 정의
- `:1706` `ShouldSkipMeasurementAfterDatumPhase` 의 `bIsZeroIndex`
- `:1716` 같은 함수에서 `FindZeroIndex...()` 호출
- `:1806` `AddResponseV1Cycle` — `m_nCurrentZIndex == DATUM_Z_INDEX`
- `:1842` `HandleDatumIndexResponse` — `m_nCurrentZIndex = DATUM_Z_INDEX`
- `:1846` 같은 함수 `TryTurnOffLightsOnCycleEnd(..., "datum-index0", DATUM_Z_INDEX)` (nZIndex 는 **로그 전용**, `:1065-1080` 확인)
- `:1226-1227` `ResetCycleState` — `m_nCurrentZIndex = 0; m_nLastZIndex = 0;` (맨 0, 상수 아님)

### 근거를 남겨야 하는 판단 4건 (요구사항 3·6)

**(1) `ParseCurrentZIndex`(:1721) 의 0 정규화 — 바꾸지 않는다.**
이 0 은 "기준점" 이 아니라 **"요청 패킷이 없다(수동 RUN·일괄검사)"** 를 뜻한다. 근거:
- `:1386-1392` `IsProtocolDrivenCycle()` 주석이 명시 — "ParseCurrentZIndex 는 packet==null 일 때도 0 을 반환해(D-08 안전 폴백) 진짜 프로토콜 z=0 과 '프로토콜 자체가 없음'을 구별하지 못한다" → 그래서 별도 신호(IsProtocolDrivenCycle)를 둔 것이다.
- 만약 여기서 기준점 값(예 11)으로 정규화하면 `Action_FAIMeasurement.cs:1119-1121`, `:1719-1721` 의 크로스-Z 역할 판정(`nCurZ == datum.ZIndexA`)이 **수동 RUN 에서 갑자기 role A 캡처로 매칭**된다. 지금은 0 이라 절대 매칭되지 않고 저장본 재검출 경로로 빠진다. 이 사이트의 실제 생산 워크플로가 수동 지그 RUN 버튼이므로(메모리 `manual-jig-offline-inspect`) 변경 위험이 이득보다 크다.
- 따라서 값은 0 유지하되 **매직넘버만 이름 있는 const(`UNSET_CYCLE_Z_INDEX`)로 승격**하고, "이건 기준점이 아니라 '요청 없음' 이다" 를 주석으로 못박는다.

**(2) `IsDatumOnlyExecutionIndex`(:1561) 가드의 잔여 엣지 — 구조 변경 없이 주석으로 남긴다.**
기존 주석은 "수동 RUN 은 nZIndex 가 0 이라 이 가드가 이미 false 를 강제한다" 고 적혀 있다. 기준점이 11 이 되면 수동 RUN 의 0 은 이 가드를 통과한다. 그래도 안전한 이유: 그 다음 조건이 `IsZIndexUsedByCrossZDatum(0)` 인데, Bottom 처럼 11~40 구간을 쓰는 시퀀스의 크로스-Z Datum 이 ZIndexA/B=0 을 선언하는 일은 구간 배정상 없다 → false 로 빠진다.
`IsProtocolDrivenCycle()` 가드를 여기에 **추가하면 안 된다**: 이 함수는 `SystemHandler.StartV1Scoped:352` 에서도 호출되는데, 그 시점은 `StartSubset/StartAll` **이전**이라 `RequestPacket` 이 아직 이번 사이클 값으로 세팅되지 않았다(이전 사이클 잔값 또는 null) → 프로토콜 경로가 깨진다.

**(3) `bHasMeasurementShots = m_nLastZIndex > 0`(:1818) — 바꾸지 않는다.**
이 비교의 의미는 "기준점보다 큰가" 가 아니라 **"이 시퀀스가 소유한 Shot 이 하나라도 있는가"** 다(`ComputeLastZIndex` 는 소유 Shot 이 0 건이면 0 을 반환한다, `:636-660`). 기준점이 11 이어도 Bottom 은 최댓값 40 → `>0` 성립. 소유 Shot 0 건이면 여전히 0 → 기존 WR-01 가드(마지막 index 매칭 0건이면 F 강제)가 그대로 방어한다. 의미가 유지되므로 손대지 말 것. 다만 **주석에 "이 0 은 기준점이 아니라 '소유 Shot 없음' 센티널" 이라고 명시**한다.

**(4) 무변경 대상이 기준점≠0 에서도 계약을 유지하는 근거.**
- `ComputeLastZIndex`(:636) = 소유 Shot ZIndex **최댓값** + 크로스-Z 완성 index. 기준점(최솟값)과 독립 개념이라 영향 없음. **크로스-Z 확장을 최솟값 쪽에 대칭 적용하지 말 것** — 기준점은 "PLC 구간의 시작 번호" 라는 운영 정의이지 크로스-Z 완성 index 와 무관하다.
- `FindShotByZIndex` / `FindActionIndicesByZIndex`(:838) = 순수 값 매칭. 번호를 그대로 받아 같은 ZIndex Shot 을 실행하므로 기준점이 무엇이든 무관.
- `StartEmptyScope` 경로(SystemHandler `:355-372`) = "매칭 0건" 처리. 기준점 분기에 들어가지 않은 번호만 도달하므로 무관.
- 크로스-Z(ZIndexA/B) = 값 매칭. 무관.
- **"z==기준점 에서만 캐시 Clear" 치환의 정확성**: `:453` 의 분기는 `!IsProtocolDrivenCycle()` 의 else-if 다 → 프로토콜 사이클에서만 도달한다. 프로토콜에서 `GetExecutionZIndex()` 는 `$PREP` 가 넣어준 실제 버퍼 번호를 돌려주므로, 그 값이 그 시퀀스의 기준점과 같을 때가 곧 "구간 시작 = 새 부품" 이다. 나머지 번호는 같은 사이클의 연속 tick 이라 Clear 하면 안 된다 — **치환 전후로 이 불변식이 그대로 성립한다.** 기준점 0 인 시퀀스는 비교 대상 값이 그대로 0 이라 회귀도 0.

**(5) `FindZeroIndexDatumTriggerActionIndices` 이름 — 정정한다.**
"Zero" 가 의미상 어긋난다(z=0 전용이 아니라 "기준점 index 의 대표 트리거"). 호출부가 단 2곳(`SystemHandler.cs:336`, `InspectionSequence.cs:1716`)뿐이라 안전하다 → `FindDatumIndexTriggerActionIndices` 로 rename 하고 두 호출부 + 관련 주석(`SystemHandler.cs:258`, `InspectionSequence.cs:1657-1662`, `:1690-1698`)의 "z=0" 표현도 "기준점 index" 로 정정한다.

### 노출/저장 경로 — 사용자 결정("입력칸은 Datum 속성창") 기준으로 재조사함

**(A) Datum 속성창 노출은 프로퍼티 추가만으로 끝난다 — UI 파일 무수정.**
- Datum 노드 PropertyGrid 의 소스는 `DatumConfig` 자신이다. `DatumConfig` 는 `ParamBase` + `ICustomTypeDescriptor` 이며, 진입점 `BuildFilteredProperties`(`DatumConfig.cs:1170-1186`) → `DynamicPropertyHelper.FilterProperties`(`DynamicPropertyHelper.cs:20-48`) 는 `TypeDescriptor.GetProperties(obj, true)` **전체**를 가져와 `IsHiddenForAlgorithm(name)` 이 true 인 이름만 뺀다.
- 따라서 `[Category]` 가 붙은 public 프로퍼티를 하나 추가하고 **`IsHiddenForAlgorithm` 에 이름을 넣지 않으면 4개 알고리즘 전부에서 항상 보인다.** (`ZIndexA/ZIndexB` 는 `IsHiddenForAlgorithm` 의 TLI/CTH/VTH 3개 case 에서 명시적으로 hide 되어 DualImage 전용이 된 것 — 새 프로퍼티는 그 목록에 넣지 않는다.)
- `sourceNames` 화이트리스트는 `[Browsable(false)]` 인 ItemsSource 소스 전용 안전판이라 일반 프로퍼티와 무관하다.
- 이름 주의: hide 규칙이 접두사 매칭(`Line1_`, `Line2_`, `Circle`, `Vertical_`, `Horizontal_A_`, `Horizontal_B_`)이므로 새 이름이 이 접두사로 시작하면 안 된다. `DatumZIndex` 는 안전하다.

**(B) 저장/로드/보존 3경로 전부 자동 — `InspectionRecipeManager.cs` 무변경.**
- 저장: `SaveFixtureForSequence:97-100` 이 `seq.DatumConfigs[d].Save(saveFile, $"{sectionPrefix}_DATUM_{d}")` 를 호출하고, `ParamBase.Save` 가 Int32 public 프로퍼티를 리플렉션으로 자동 직렬화한다.
- 로드: `LoadFixtureForSequence:141-145` 의 `datum.Load(loadFile, datumSection)` 로 자동.
- 비활성 시퀀스(다른 CameraRole) 보존: `PreserveFixtureFromExisting:115-121` 이 `saveFile[datumSection] = existingFile[datumSection]` 로 **섹션을 통째로 복사**하므로 신규 키도 자동 보존된다.

**(C) 그러나 "키 부재 = -1(자동)" 은 자동으로 성립하지 않는다 — Load 오버라이드 가드가 필수다.**
- `ParamBase.Load:377-380` 은 `case "Int32": loadFile[group][name].ToInt();` 를 **인자 없이** 호출하고, `IniValue.ToInt(int valueIfInvalid = 0)`(`Ini.cs:179-185`)의 기본값이 0 이다. → **INI 에 키가 없으면 0 이 들어간다.**
- 이 기능에서 0 은 "z=0 을 기준점으로 명시 지정" 이라는 **유효값**이라 자동(-1)과 반드시 구별해야 한다. 구 레시피가 전부 0 으로 로드되면 Bottom 같은 시퀀스가 영영 사이클을 시작하지 못한다.
- 이것이 `ZIndexA/ZIndexB` 가 `DatumConfig.Load` 오버라이드(`:1249-1268`)에 `ContainsKey` 가드를 둔 것과 **정확히 같은 사유**다. `DatumConfig.cs:1153-1157` 주석이 이 대조("ZIndexA/ZIndexB 는 0 이 의미값이라 오버라이드가 필요했던 것과 대조")를 이미 명문화해 두었다.
- → 같은 Load 오버라이드에 `DatumZIndex` 가드 2줄(섹션 없음 분기 + ContainsKey 분기)을 추가한다.
- **주의:** 옛 계획이 근거로 삼았던 `.ToInt(기본값)` 경로는 `InspectionRecipeManager` 가 INI 를 **직접 호출**하는 자리에만 해당한다. 리플렉션 직렬화 경로에는 적용되지 않는다.

**(D) "사용자 편집일 때만 경고" 선례 — `_suppressMirrorWarning` 을 그대로 따른다.**
- 선언 `DatumConfig.cs:229`, 판독 `:269`(`WarnMirrorChanged` 최상단), 켜고 끄는 곳 `Load :1253-1260` 과 `CopyTo :1309-1314`. 리플렉션 SetValue(INI 로드)와 붙여넣기가 같은 세터를 때리기 때문에 두 경로에서 끈다.
- `MirrorX/MirrorY` 세터(`:238-262`)는 `if (_mirrorX == value) return;` 로 **같은 값 재저장 시 경고 반복까지** 막는다.
- 경고 표시는 `ReringProject.UI.CustomMessageBox.Show(title, message, MessageBoxImage.Warning, true, false)` — 마지막 인자 `isAutoClosing=false`(자동닫힘 끔). 내부가 `Dispatcher.BeginInvoke` 로 넘기므로 세터를 블로킹하지 않는다(`CustomMessageBox.cs:24-35`). **이중 마샬링 금지.**

**(E) 소유 시퀀스 접근 경로 — `Owner as InspectionSequence` 를 쓴다.**
- `DatumConfig` 생성 경로는 `InspectionSequence.AddDatum():2199-2208` 의 `new DatumConfig(this)` **단 하나**다(전 저장소 grep 확인. `MainView.xaml.cs:4206` 주석도 같은 사실을 기록). 레시피 로드도 `LoadFixtureForSequence` 가 `seq.AddDatum()` 후 `datum.Load(...)` 를 부르므로 예외 없다.
- `ParamBase.CopyPublicPropertiesTo:451` 이 `Owner` 를 복사 대상에서 제외하므로 붙여넣기로도 소유 관계가 깨지지 않는다.
- `OwnerName`(`ParamBase.cs:41-49`)은 문자열만 돌려주어 `SystemHandler.Handle.Sequences[name]`(문자열 인덱서, 미존재 시 null — `SequenceHandler.cs:138-150`) 를 한 번 더 타야 한다. **불필요하므로 쓰지 않는다.**
- `DatumConfig` 와 `InspectionSequence` 는 같은 `namespace ReringProject.Sequence` 라 별도 using 이 필요 없다.

</analysis>

<tasks>

<task type="auto">
  <name>Task 1: Datum 속성창에 기준점 Z 번호 + 시퀀스 실효값 접근자</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs, WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
**이 태스크에서는 판정 지점을 아직 건드리지 않는다.** `DATUM_Z_INDEX`(InspectionSequence:88) 는 그대로 두고 속성·저장·접근자만 추가한다 — 태스크 경계에서 항상 빌드가 통과해야 한다.
**`InspectionRecipeManager.cs` 는 한 줄도 고치지 않는다**(analysis (B)). **UI 3파일(`InspectionListView.xaml`, `.xaml.cs`, `InspectionListViewModel.cs`)도 손대지 않는다**(analysis (A)).

### 1-A. `DatumConfig.cs` — 억제 플래그 개명 (기계적, 7곳)

경고 억제 플래그가 이제 Mirror 전용이 아니게 되므로 `_suppressMirrorWarning` → `_suppressUserEditWarning` 으로 rename 한다. 컴파일러가 누락을 잡아주는 안전한 변경이며, 같은 시점에 켜고 꺼야 하는 플래그를 두 개로 늘리지 않기 위함이다.
- 선언 `:229`, 판독 `:269`(`WarnMirrorChanged` 최상단), `Load` `:1254`/`:1260`, `CopyTo` `:1309`/`:1314`. 새 경고 메서드에서 1회 더 읽으므로 최종 7곳.
- `:226-228` 주석의 "Mirror 세터" 표현을 "사용자 편집 대상 세터(Mirror / 기준점 Z 번호)" 로 넓힌다. `:1254` 인라인 주석도 같이 정정.
- **이 rename 외에 Mirror 관련 동작을 바꾸지 말 것.**

### 1-B. `DatumConfig.cs` — 기준점 Z 번호 프로퍼티

`ZIndexA/ZIndexB` 블록(`:212-222`) **아래**, `_suppressUserEditWarning` 선언 **위**에 넣는다(관련 코드 인접 배치).

1. sentinel 상수 — **매직넘버 금지**
   `public const int AUTO_DATUM_Z_INDEX = -1;`
   public 인 이유: `InspectionSequence` 가 같은 sentinel 로 "지정/자동" 을 판정해야 하며 값 복제를 금지하기 때문.

2. 백킹 필드 `private int _datumZIndex = AUTO_DATUM_Z_INDEX;`

3. 프로퍼티
   - `[Category("Datum|Cycle")]` — 새 그룹. **`IsHiddenForAlgorithm` 에는 절대 추가하지 말 것**(모든 알고리즘에서 보여야 한다, analysis (A)).
   - `[System.ComponentModel.Description("-1=자동(이 시퀀스 Shot 번호 중 가장 작은 값). 이 시퀀스의 새 사이클이 시작되는 Z 번호")]`
   - `public int DatumZIndex { get; set; }` — get 은 `_datumZIndex` 반환.
   - set 순서: (a) 지역 `int nNormalized` 에 값을 담되 **음수는 전부 `AUTO_DATUM_Z_INDEX` 로 정규화**(사용자가 -5 를 넣어도 "자동"). (b) `if (_datumZIndex == nNormalized) { return; }` — 같은 값 재저장 시 경고 반복 방지(Mirror 선례 `:239`/`:253`). (c) 대입. (d) `RaisePropertyChanged(nameof(DatumZIndex));` (e) `WarnDatumZIndexChanged();`

4. `private void WarnDatumZIndexChanged()` — `WarnMirrorChanged`(`:266-282`) 바로 아래, 같은 관용구로.
   가드 순서(전부 `if` + 중괄호, 삼항 금지):
   - `if (_suppressUserEditWarning) { return; }` — INI 로드/붙여넣기에서는 조용히.
   - `if (_datumZIndex == AUTO_DATUM_Z_INDEX) { return; }` — 자동이면 경고 없음.
   - `InspectionSequence owner = Owner as InspectionSequence;` → `if (owner == null) { return; }`
   - `int nMin; int nMax;` → `if (!owner.TryGetOwnedShotZIndexRange(out nMin, out nMax)) { return; }` — 소유 Shot 이 없으면 경고 없음.
   - `if (_datumZIndex == nMin) { return; }` — 시작 번호와 같으면 정상.
   - 그 외에만 `ReringProject.UI.CustomMessageBox.Show("기준점 Z 번호 확인", message, System.Windows.MessageBoxImage.Warning, true, false);`
   메시지는 **초보 작업자가 읽는 평이한 한국어**로, 숫자는 실제값을 넣어 조립한다. 취지:
   1) 이 시퀀스가 쓰는 촬영 번호는 {nMin} 부터 {nMax} 까지인데 방금 넣은 {값} 은 그 시작 번호({nMin}) 가 아니다.
   2) 기준점 번호는 "새 제품 하나가 시작되는 번호" 다. 시작 번호와 다르면 사이클이 제때 시작되지 않을 수 있다.
   3) 그래도 저장은 된다. 일부러 넣은 값이 아니면 칸에 -1 을 넣어 자동으로 되돌려라.
   **저장을 막지 말 것**(반환값을 쓰지 않는다).

5. **문자열/주석에 물음표 문자를 쓰지 말 것.** 삼항 금지 게이트(`grep -cE '\?[^?]*:'`)가 한글 문장의 물음표+콜론 조합에 오탐한다.

### 1-C. `DatumConfig.cs` — Load 오버라이드에 키부재 가드 (analysis (C), 필수)

기존 오버라이드(`:1249-1268`)에 `ZIndexA/ZIndexB` 와 **같은 자리, 같은 형태**로 2줄 추가:
- 섹션 자체가 없는 조기 return 분기(`:1262-1266`)에 `DatumZIndex = AUTO_DATUM_Z_INDEX;`
- `if (!sec.ContainsKey("ZIndexA")) ...` 옆에 `if (!sec.ContainsKey("DatumZIndex")) { DatumZIndex = AUTO_DATUM_Z_INDEX; }`
- 헤더 주석(`:1244-1248`)에 `DatumZIndex` 도 같은 사유(0 이 유효 지정값)로 가드가 필요함을 한 줄 덧붙인다.
- 이 대입들은 세터를 타지만 값이 `AUTO_DATUM_Z_INDEX` 라 `WarnDatumZIndexChanged` 가 두 번째 가드에서 즉시 빠져나온다 → 로드 중 경고창 없음. 이 근거를 주석에 남긴다.
- `_copyExclude`(`:1281-1300`) 에는 **추가하지 말 것** — 기준점 번호는 붙여넣기로 복사되는 게 맞다.

### 1-D. `InspectionSequence.cs` — 상수 2개

`:86-88` 의 기존 상수 블록(`DATUM_Z_INDEX` / `CROSS_Z_UNSET`) 옆에 추가한다. 이 태스크에서 `UNSET_CYCLE_Z_INDEX` 는 선언만 하고 소비는 Task 2 에서 한다(`const` 라 미사용도 경고 없음).
- `private const int MIN_VALID_Z_INDEX = 0;` — 유효한 가장 작은 z. 지정값 유효성 판정과 "소유 Shot 없음" 폴백에 둘 다 쓴다.
- `private const int UNSET_CYCLE_Z_INDEX = 0;` — "이번 tick 의 요청 자체가 없음". **기준점과 다른 개념이라는 것을 주석으로 못박을 것**(analysis 판단 (1)).

### 1-E. `InspectionSequence.cs` — 공개 API 2개

`ComputeLastZIndex`(:636) 바로 위에 붙여 "z_index 산출" 코드를 한곳에 모은다. 브레이스 스타일은 그 주변(Allman)을 따른다.

1. `public bool TryGetOwnedShotZIndexRange(out int nMin, out int nMax)`
   - `SystemHandler.Handle` **및** `SystemHandler.Handle.Sequences.RecipeManager` 를 각각 null 체크할 것 — 이 메서드는 PropertyGrid 세터(=앱 초기화 이후지만 레시피 미로드 가능)에서도 호출된다. `ComputeLastZIndex` 의 호출부와 달리 무가드 접근을 하면 안 된다.
   - `shot.OwnerSequenceName == Name` 인 Shot 만 순회 — **`ComputeLastZIndex:645-655` 와 완전히 같은 소유 판정식을 쓸 것**(레거시 빈 OwnerSequenceName 처리 일관성).
   - 소유 Shot 0건 또는 매니저 없음 → `nMin=0, nMax=0, return false`.
   - 크로스-Z 완성 index(`MaxCrossZCompletionZIndex`)는 **여기에 절대 섞지 말 것**(analysis (4)).

2. `public int GetDatumZIndex()`
   - **1순위:** 소유 `DatumConfigs` 를 순회해 `d.DatumZIndex >= MIN_VALID_Z_INDEX` 인 값들의 **최솟값**을 반환한다(하나라도 있으면). 0 도 정당한 지정값이다.
     - 한 시퀀스에 Datum 이 여러 개고 서로 다른 값이 들어간 경우도 최솟값을 쓴다 — 그 상황 자체는 1-B 의 세터 경고가 사용자에게 알린다.
     - `DatumConfigs` null 가드 필수.
   - **2순위:** `TryGetOwnedShotZIndexRange` 의 `nMin`.
   - **3순위:** `MIN_VALID_Z_INDEX`.
   - 캐시하지 말 것 — 레시피 교체/Shot 편집 후 스테일 값이 사이클 시작을 망가뜨리는 위험이 캐시 이득보다 크다. `ComputeLastZIndex` 가 이미 매 응답마다 같은 순회를 하고 있으므로 비용 근거도 동일하다. 이 근거를 주석으로 남긴다.
  </action>
  <verify>
    <automated><![CDATA[
set -e
cd /c/code/DataMeasurement

# 1) DatumConfig — 프로퍼티/sentinel/키부재 가드/경고
grep -q 'public const int AUTO_DATUM_Z_INDEX' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
grep -q 'public int DatumZIndex' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
grep -q 'Category("Datum|Cycle")' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
grep -q 'ContainsKey("DatumZIndex")' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
grep -q 'WarnDatumZIndexChanged' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
grep -q 'Owner as InspectionSequence' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs

# 모든 알고리즘에서 보여야 한다 — hide 목록 오염 금지
sed -n '/private static bool IsHiddenForAlgorithm/,/^        }$/p' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs > /tmp/iwm_hide.txt
test "$(grep -c 'DatumZIndex' /tmp/iwm_hide.txt)" = "0"
test "$(grep -c 'DatumZIndex' WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs)" = "0"

# 2) 억제 플래그 개명 완료 (구 이름 잔존 0, 신 이름 7곳 이상)
test "$(grep -rn '_suppressMirrorWarning' WPF_Example --include=*.cs | wc -l)" = "0"
test "$(grep -c '_suppressUserEditWarning' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs)" -ge 7

# 3) InspectionSequence — 접근자
grep -q 'public bool TryGetOwnedShotZIndexRange' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'public int GetDatumZIndex()' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'MIN_VALID_Z_INDEX' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs

# 4) 무변경 계약 — 레시피 매니저와 UI 는 손대지 않았다
test "$(git diff --name-only -- WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs | wc -l)" = "0"
test "$(git diff --name-only -- WPF_Example/UI | wc -l)" = "0"

# 5) 하드룰 — 추가 라인만 검사 (기존 라인의 hbk 주석은 대상 아님)
git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs \
                WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
  | grep '^+' | grep -v '^+++' > /tmp/iwm_t1.txt
test "$(grep -cF 'hbk' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cF '??' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cF '?.' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cE 'switch.*=>' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cE '\?[^?]*:' /tmp/iwm_t1.txt)" = "0"

# 6) 빌드 — error CS 0 (MSB3027 bin 복사 실패는 게이트 아님)
"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  2>&1 | tee /tmp/iwm_build1.log
test "$(grep -cE 'error CS' /tmp/iwm_build1.log)" = "0"
echo T1_OK
]]></automated>
    <manual>삼항 게이트가 히트하면 코드가 아니라 한글 주석/메시지의 물음표 + 콜론 조합일 수 있다. 육안 확인 후 **문구에서 물음표를 빼 0 을 만든다** — 게이트를 완화하거나 우회하지 말 것.</manual>
  </verify>
  <done>Datum 속성창용 `DatumZIndex` 프로퍼티가 `DatumConfig` 에 있고(모든 알고리즘에서 노출), INI 키가 없으면 -1(자동)로 로드되며, 사용자가 직접 바꿨을 때만 Shot 시작 번호와 다르면 경고창이 뜬다(저장은 막지 않음). `GetDatumZIndex()` 는 지정값들의 최솟값 → 소유 Shot ZIndex 최솟값 → 0 순으로 실효값을 돌려준다. `InspectionRecipeManager.cs` 와 UI 3파일은 변경 0. 판정 지점은 아직 변경 전이며 빌드 error CS 0.</done>
</task>

<task type="auto">
  <name>Task 2: 판정 지점 전면 치환 (상수 → 시퀀스 실효값)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/SystemHandler.cs</files>
  <action>
**원칙: 로직 구조는 그대로 두고 비교 대상 값만 바꾼다.** 특히 `StartV1Scoped` 는 과거 TOCTOU 경합을 잡은 예민한 지점이다 — `BeginCrossZImageCycle()` 호출 위치, `StartSubset`/`StartAll` 순서, `State==Idle` 사전체크 부재를 **절대 바꾸지 말 것**(`SystemHandler.cs:304-334` 의 긴 주석이 그 이유다).

실효값의 출처는 Task 1 이 만든 `InspectionSequence.GetDatumZIndex()` 이며, 그 값은 **Datum 속성창의 `DatumZIndex` 지정값(최솟값) → 소유 Shot ZIndex 최솟값 → 0** 순으로 파생된다. FIXTURE 키를 읽는 코드가 아니다 — 이 태스크에서 INI 를 직접 읽는 코드를 새로 만들지 말 것.

### 2-A. `InspectionSequence.cs`

1. `:88` `private const int DATUM_Z_INDEX = 0;` **삭제.** 이후 참조는 전부 `GetDatumZIndex()`.
2. `:453` `else if (GetExecutionZIndex() == DATUM_Z_INDEX)` → `GetDatumZIndex()` 와 비교. 조건은 이름 있는 bool 로 선추출할 것.
   - `:461-467` 주석의 "z==DATUM_Z_INDEX(0)", "z>=1" 표현을 "z == 이 시퀀스의 기준점", "그 외 번호" 로 정정한다(analysis (4) 의 불변식 근거를 한 줄 포함).
3. `:1561` `IsDatumOnlyExecutionIndex` 최상단 가드 → `nZIndex == GetDatumZIndex()`.
   - `:1556-1559` 주석에 analysis 판단 (2) 를 요약해 남긴다: 수동 RUN(요청 없음)의 0 이 기준점≠0 시퀀스에서 이 가드를 통과하지만 `IsZIndexUsedByCrossZDatum(0)` 이 false 라 안전하며, **여기에 `IsProtocolDrivenCycle()` 가드를 추가하면 `StartV1Scoped` 호출 시점에 RequestPacket 이 아직 이번 사이클 값이 아니어서 프로토콜 경로가 깨진다.**
4. `:1663` `FindZeroIndexDatumTriggerActionIndices` → **`FindDatumIndexTriggerActionIndices` 로 rename** (선언 + 호출부 `:1716` + `SystemHandler.cs:336`). `:1657-1662` 주석의 "z=0" 표현도 "기준점 index" 로 정정.
5. `:1706` `bool bIsZeroIndex = nZIndex == DATUM_Z_INDEX;` → `bool bIsDatumIndex = nZIndex == GetDatumZIndex();` (변수명도 정정). `:1690-1698` 주석의 z=0 표현 정정.
6. `:1806` `m_nCurrentZIndex == DATUM_Z_INDEX` → `GetDatumZIndex()`.
7. `:1818` `bHasMeasurementShots = m_nLastZIndex > 0` — **값·비교 모두 변경 금지.** 주석 한 줄만 추가: 이 0 은 기준점이 아니라 "이 시퀀스 소유 Shot 이 0건" 센티널이다(analysis (3)).
8. `:1842` `m_nCurrentZIndex = DATUM_Z_INDEX;` → `GetDatumZIndex()`. 같은 값을 두 번 계산하지 않도록 `HandleDatumIndexResponse` 진입부에서 지역변수 `int nDatumZ = GetDatumZIndex();` 로 한 번만 받아 쓴다.
9. `:1846` `TryTurnOffLightsOnCycleEnd(datumPacket, "datum-index0", DATUM_Z_INDEX)` → 태그를 `"datum-index"` 로, 값은 `nDatumZ`. (이 인자는 로그 전용임을 이미 확인함 — 동작 영향 없음)
10. `:1226-1227` `ResetCycleState` 의 맨 `0` 두 개 → `UNSET_CYCLE_Z_INDEX`. **값은 그대로 0 이다**(다음 tick 에 즉시 덮어써지는 중립값이며, `$RESET` 경로에서도 기준점으로 세팅할 이유가 없다). 이 근거를 주석으로 남긴다.
11. `:1721` `ParseCurrentZIndex` — 반환 `0` 3곳을 `UNSET_CYCLE_Z_INDEX` 로 승격. **동작 변경 금지**(analysis (1)). 주석에 "이 0 은 기준점이 아니라 '요청 패킷 없음' 이며, 진짜 프로토콜/수동 구분은 `IsProtocolDrivenCycle()` 이 담당한다" 를 명시.

### 2-B. `WPF_Example/Custom/SystemHandler.cs`

1. `:263` `DATUM_TEST_Z_INDEX` → `private const int NO_SEQUENCE_DATUM_Z_INDEX = 0;` 으로 **의미 변경 + 개명**. 뜻: "시퀀스를 해석할 수 없을 때의 폴백 기준점". `:255-262` 주석도 "z=0" → "그 시퀀스의 기준점 index" 로 정정.
2. 신규 private 헬퍼:
   `private int ResolveDatumZIndex(string szSeqName)`
   `Sequences[szSeqName]`(문자열 인덱서는 미존재 시 null 반환 — `SequenceHandler.cs:138-150` 확인함) → `as InspectionSequence` → null 이면 `NO_SEQUENCE_DATUM_Z_INDEX`, 아니면 `insp.GetDatumZIndex()`.
3. `:44-60` `GetPrepZIndex`:
   - 이름 없음 분기의 `return 0` → `return NO_SEQUENCE_DATUM_Z_INDEX;`
   - 기록 없음 폴백 → `ResolveDatumZIndex(szSeqName)` 반환. **`ResolveDatumZIndex` 는 `_prepZIndexLock` 밖에서 호출할 것**(락 안에서 시퀀스/레시피를 만지지 말 것 — 기존 락 범위 원칙).
   - 로그 문구를 "z_index=0 으로 폴백" → "그 시퀀스의 기준점 z_index={실효값} 으로 폴백" 으로 정정하고 실제 값을 찍는다.
   - `:43` 의 헤더 주석("0(=Datum 인덱스) 보수적 폴백")도 정정.
4. `:296-302` `StartV1Scoped`:
   - `InspectionSequence inspSeq = seq as InspectionSequence;` 캐스트를 함수 최상단으로 **hoist** 하고, `int nDatumZ;` 를 `inspSeq == null` 이면 `NO_SEQUENCE_DATUM_Z_INDEX`, 아니면 `inspSeq.GetDatumZIndex()` 로 구한다.
   - `bool bIsDatumZIndex = nPrepZIndex == nDatumZ;`
   - 기준점 분기 안의 지역변수 `inspDatumSeq` 는 hoist 한 `inspSeq` 로 대체한다. **`if (!bIsInspSeq) return seq.StartAll(packet);` 방어 폴백 2곳은 그대로 유지**(캐스트 실패 시 `nDatumZ==0` 이라 종전과 정확히 같은 동작이 된다).
   - `datumZeroIndices` 지역변수명 → `datumTriggerIndices`, 호출은 rename 된 `FindDatumIndexTriggerActionIndices()`.
   - `:255-262`, `:288-295`, `:304-334`, `:337-341` 주석의 "z=0" / "z>=1" 표현을 "기준점 index" / "그 외 번호" 로 정정하되 **TOCTOU·FIX-0·GAP-2·GAP-3 설명 본문은 지우지 말 것**(왜 이 구조인지의 유일한 기록이다).
  </action>
  <verify>
    <automated><![CDATA[
set -e
cd /c/code/DataMeasurement

# 1) 옛 상수/옛 이름이 완전히 사라졌는지 (주석 본문 포함)
test "$(grep -cE '\bDATUM_Z_INDEX\b' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs)" = "0"
test "$(grep -rnE '\bDATUM_TEST_Z_INDEX\b|FindZeroIndexDatumTriggerActionIndices' WPF_Example --include=*.cs | wc -l)" = "0"

# 2) 새 접근자가 판정 지점에서 실제로 소비되는지 (InspectionSequence 5곳 이상 + SystemHandler)
test "$(grep -c 'GetDatumZIndex()' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs)" -ge 5
grep -q 'GetDatumZIndex()' WPF_Example/Custom/SystemHandler.cs
grep -q 'FindDatumIndexTriggerActionIndices' WPF_Example/Custom/SystemHandler.cs
grep -q 'private int ResolveDatumZIndex' WPF_Example/Custom/SystemHandler.cs

# 3) 무변경 계약 — 이 3줄은 그대로 남아 있어야 한다
grep -q 'm_nLastZIndex > 0' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'inspSeq' WPF_Example/Custom/SystemHandler.cs
grep -q 'BeginCrossZImageCycle();' WPF_Example/Custom/SystemHandler.cs

# 4) 하드룰 — 추가 라인만
git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
                WPF_Example/Custom/SystemHandler.cs \
  | grep '^+' | grep -v '^+++' > /tmp/iwm_t2.txt
test "$(grep -cF 'hbk' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cF '??' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cF '?.' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cE 'switch.*=>' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cE '\?[^?]*:' /tmp/iwm_t2.txt)" = "0"

# 5) 범위 — 변경 파일 정확히 3개, UI/레시피매니저/csproj 무변경, 신규 소스파일 0
git diff --name-only -- WPF_Example | sort > /tmp/iwm_files.txt
test "$(wc -l < /tmp/iwm_files.txt)" = "3"
test "$(grep -c 'DatumMeasurement.csproj' /tmp/iwm_files.txt)" = "0"
test "$(grep -c 'InspectionRecipeManager.cs' /tmp/iwm_files.txt)" = "0"
test "$(git diff --name-only -- WPF_Example/UI | wc -l)" = "0"
test "$(git status --short | grep -cE '^\?\? .*\.(cs|xaml)$')" = "0"

# 6) 빌드
"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  2>&1 | tee /tmp/iwm_build2.log
test "$(grep -cE 'error CS' /tmp/iwm_build2.log)" = "0"
echo T2_OK
]]></automated>
  </verify>
  <done>`DATUM_Z_INDEX` / `DATUM_TEST_Z_INDEX` 상수가 사라지고 모든 기준점 판정이 `GetDatumZIndex()` 를 소비한다. `GetPrepZIndex` 폴백이 시퀀스 실효값을 돌려준다. `m_nLastZIndex > 0`, `ParseCurrentZIndex` 반환값(0), `StartV1Scoped` 의 `BeginCrossZImageCycle`/`StartSubset` 순서는 그대로다. 변경 파일 정확히 3개, 빌드 error CS 0.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 확인 (속성창 입력칸 + 기존 레시피 회귀 + Bottom 11 사이클 시작 + 경고)</name>
  <action>아래 <how-to-verify> 절차를 사용자에게 그대로 제시하고 응답을 기다린다. 코드 수정 없음.</action>
  <what-built>
    시퀀스마다 "기준점(= 그 시퀀스 구간의 시작 번호, 여기서 새 사이클이 시작됨)" 을 따로 정할 수 있게 했다.
    입력칸은 **Datum 속성창**(왼쪽 트리에서 Datum 항목을 클릭했을 때 오른쪽에 뜨는 설정창)의 `Datum|Cycle` 그룹에 있다.
    아무것도 설정하지 않으면(-1) **그 시퀀스가 가진 Shot 번호 중 가장 작은 번호**가 자동으로 기준점이 된다.
    그래서 Bottom 이 11~40 을 쓰면 11 이 자동으로 사이클 시작이 되고, 기존 레시피(0 부터 시작)는 예전과 완전히 똑같이 동작한다.
    직접 숫자를 넣으면 그 값이 우선하고, Shot 의 가장 작은 번호와 다르면 경고창이 뜨지만 저장은 막지 않는다.
  </what-built>
  <how-to-verify>
    `bin/x64/Debug/DatumMeasurement.exe` 실행.

    **1) 속성창에 칸이 보이는가 (이번 수정의 핵심)**
    - 왼쪽 트리에서 Datum 항목(예 `Datum_1`)을 클릭한다.
    - 오른쪽 설정창에 `Datum|Cycle` 그룹과 그 안의 기준점 Z 번호 칸이 보이는가.
    - Datum 알고리즘 종류를 바꿔가며(TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal / VerticalTwoHorizontalDualImage) **네 가지 전부에서 칸이 그대로 보이는지** 확인한다.
    - 처음 값이 `-1` 인가.

    **2) 기존 레시피 회귀 (가장 중요)**
    - 지금 쓰던 레시피를 그대로 열고 평소처럼 검사를 한 번 돌린다(PLC 또는 수동 지그 RUN 버튼).
    - 결과·응답이 **어제와 똑같은지** 확인한다. 뭔가 달라졌으면 즉시 실패로 보고할 것.
    - 레시피를 여는 동안 **경고창이 하나도 뜨지 않아야** 한다.

    **3) 저장 / 다시 불러오기**
    - 기준점 칸에 값을 넣고(예 Bottom 이면 11) 레시피를 저장한다.
    - 프로그램을 껐다 켜고 같은 레시피를 다시 연다 → **값이 그대로 남아 있는가.**
    - `main.ini` 를 메모장으로 열어 해당 Datum 섹션(예 `[FIXTURE_BOTTOM_DATUM_0]`)에 `DatumZIndex` 줄이 있는지 확인한다.
    - 현재 모드에 없는 시퀀스(예 Side 모드에서 Bottom)의 값도 저장 후 파일에 그대로 남아 있는가.

    **4) Bottom 11 로 실기 확인** (Bottom Shot 이 11~40 인 레시피 필요)
    - 제어 쪽에서 `$PREP:1,1,11@` 다음에 `$TEST` 를 보낸다.
    - 이때 **새 사이클로 시작되는지**(기준점 촬영 응답 `B`, 이전 부품 결과가 초기화됨) 확인한다.
    - 이어서 12, 13 … 을 보내고 마지막 번호(40)에서 종합 판정(P 또는 F)이 나오는지 확인한다.
    - 기준점 칸을 `-1`(자동)로 두고도 같은 동작이 되는지 한 번 더 확인한다.

    **5) 경고 확인**
    - 기준점 칸에 일부러 엉뚱한 값(예 Shot 이 11~40 인데 `40`)을 넣는다.
    - **경고창이 뜨는가.** 내용이 "시작 번호(11)가 아니다" 취지로 읽히는가.
    - 창을 닫은 뒤 **저장이 막히지 않는지** 확인한다.
    - 같은 값(40)을 한 번 더 넣어도 창이 다시 뜨지 않는가. (같은 값 재저장은 조용해야 정상)
    - 확인 후 값을 `-1` 로 되돌린다.
  </how-to-verify>
  <resume-signal>"approved" 또는 어긋난 항목(항목 번호 + 시퀀스/Datum 이름 + 보낸 번호 + 실제로 나온 응답)을 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| PLC/호스트 → TCP `$PREP`/`$TEST` z_index | 외부에서 들어온 정수 버퍼 번호가 "사이클 시작" 판정에 쓰인다 |
| 운영자 → Datum 속성창 기준점 입력 | 사람이 넣은 값이 사이클 시작 판정을 바꾼다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-IWM-01 | Denial of Service | `StartV1Scoped` 기준점 분기 | mitigate | 기준점을 잘못 지정하면 사이클이 시작되지 않는다 → 기본값을 "자동(-1, Shot 최솟값)" 으로 두어 오설정 자체가 발생하지 않게 하고, 수동 지정이 Shot 최솟값과 다르면 `WarnDatumZIndexChanged` 경고창 |
| T-IWM-02 | Repudiation | `GetPrepZIndex` 폴백 | mitigate | `$PREP` 누락 폴백 로그에 실제 사용한 기준점 값을 함께 찍어 사후 추적 가능하게 한다 |
| T-IWM-03 | Tampering | 레시피 INI `{prefix}_DATUM_{d}` 의 `DatumZIndex` 키 | accept | 로컬 파일이며 기존 Datum 키(ZIndexA/ZIndexB)와 동일한 신뢰 수준. 음수는 세터에서 "자동" 으로 정규화되고, 키 부재는 `Load` 오버라이드가 -1 로 강제하므로 잘못된 값이 판정에 흘러들지 않는다 |
| T-IWM-04 | Denial of Service | `DatumConfig` 세터 → `SystemHandler.Handle` 접근 | mitigate | 세터는 UI 스레드에서 임의 시점에 불릴 수 있다 → `TryGetOwnedShotZIndexRange` 가 `SystemHandler.Handle` 과 `RecipeManager` 를 각각 null 체크하고 실패 시 조용히 경고를 건너뛴다(예외로 PropertyGrid 편집을 깨뜨리지 않는다) |
| T-IWM-SC | Tampering | 패키지 설치 | n/a | 신규 패키지 설치 없음(npm/pip/cargo 미사용, 신규 파일 0개) |
</threat_model>

<verification>
1. Debug|x64 빌드 **error CS 0** (`MSB3027` bin 복사 실패는 게이트 아님, 실행 중인 프로세스를 강제 종료하지 말 것).
2. 하드룰 grep — **추가된 diff 라인 기준** 전항목 0: 삼항 / `??` / `?.` / `switch.*=>` / `hbk`.
3. 옛 상수/옛 이름 소멸: `DATUM_Z_INDEX`, `DATUM_TEST_Z_INDEX`, `FindZeroIndexDatumTriggerActionIndices`, `_suppressMirrorWarning` 전 저장소 grep 0건.
4. 무변경 계약 유지: `m_nLastZIndex > 0`, `ParseCurrentZIndex` 반환 0, `BeginCrossZImageCycle()` 호출 위치, `StartSubset/StartAll` 순서, `ComputeLastZIndex` 최댓값 로직, `IsHiddenForAlgorithm` 목록(새 이름 미추가).
5. 회귀 0 코드 경로 확인: INI 키 없음 → `Load` 오버라이드가 -1 → `GetDatumZIndex()` 가 Shot 최솟값(기존 레시피는 0) → 모든 판정 지점이 종전과 동일한 값을 비교한다.
6. 변경 파일 정확히 3개(`DatumConfig.cs`, `InspectionSequence.cs`, `Custom/SystemHandler.cs`), 신규 소스파일 0개, `InspectionRecipeManager.cs`·`WPF_Example/UI`·`DatumMeasurement.csproj` 미변경/미스테이징.
7. 스테이징은 수정 파일만 명시적으로 — `git add .` / `git add -A` **금지**.
</verification>

<success_criteria>
- Datum 노드 속성창에 기준점 Z 번호 입력칸이 있고, Datum 알고리즘 4종 전부에서 보인다.
- 기준점 인덱스가 설정값이 되고, 실효값 접근자(`GetDatumZIndex()`)를 **모든** 판정 지점이 소비한다.
- 미지정(-1) 시 자동으로 소유 Shot ZIndex 최솟값이 쓰이고, Shot 이 없으면 0 이다.
- 기존 레시피는 INI 키 부재 → -1 → 자동값 0 이라 동작이 종전과 100% 동일하고, 로드 중 경고창이 뜨지 않는다.
- 기준점 값이 레시피 `{prefix}_DATUM_{d}` 섹션에 저장/로드되고, 비활성 시퀀스의 값도 저장 시 보존된다(`InspectionRecipeManager` 무변경으로 달성).
- 사용자가 직접 값을 바꿨고 Shot 최솟값과 다르면 경고창이 뜨며, 경고가 저장을 막지 않고 같은 값 재저장 시 반복되지 않는다.
- 빌드 error CS 0, 하드룰 grep 전항목 0, 변경 파일 3개.
</success_criteria>

<output>
완료 후 `.planning/quick/260904-iwm-datum-z-0/260904-iwm-SUMMARY.md` 를 작성한다.
</output>
