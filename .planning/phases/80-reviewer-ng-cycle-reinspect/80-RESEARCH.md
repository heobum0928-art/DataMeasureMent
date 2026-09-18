# Phase 80: 리뷰어 NG 사이클 사진 한 번에 불러와 파라미터 수정·재검사 - Research

**Researched:** 2026-09-18
**Domain:** WPF 리뷰어(비모달 Window) ↔ 메인 검사 화면(MainView/InspectionListView) 간 사진·상태 이관, 저장 사이클(cycle.json) 재구성, HALCON Datum/Local-Ref 재계산, 레시피 저장 보호(스냅샷/복원)
**Confidence:** HIGH — 전 항목을 실제 코드(파일:라인) 대조로 확인했다. 외부 라이브러리·문서 조사는 없음(신규 패키지 0, 전부 기존 코드 재사용/확장). Phase 79(국부 기준선)는 이미 완료·배포(1.7.50.0)되어 있어 "가정"이 아니라 "현재 동작하는 코드"로 확인했다.

## Summary

Phase 80 은 5개의 독립적이지만 연결된 변경을 요구한다: (1) 리뷰어 NG 행 → 메인 화면 사진 일괄 주입 + 파라미터 편집 + 재검사, (2) 리뷰어 사이클 전체 보기 오버레이 겹침 버그 수정, (3) 수동 RUN 시 Local Ref ROI 를 고친 뒤 stale 판정으로 전역 전환되던 것을 현재 기준점 사진에서 재계산하도록 수정, (4) Local Ref ROI "시험 찾기" 버튼 추가. 다섯 항목 모두 **신규 알고리즘이나 신규 NuGet 패키지가 필요 없다** — 기존 `RepeatRunService`/`SavedCycleRerunPlanner`(반복검사 큐), `InspectionSequence`(Datum 검출·Local Ref 사전계산), `EdgeToLineDistanceMeasurement`(Local Ref 소비), `MainView`/`InspectionListView`(트리·ROI 배선), `ReviewerWindow`(사이클 로드) 의 기존 메커니즘을 정확히 재사용·확장하는 문제다.

핵심 발견은 "부품(자재) 단위로 사진을 모으는" 로직이 이미 `SavedCycleRerunPlanner`(RepeatRunService.cs) 에 완성돼 있고, `SavedCycleRerunPart`/`SavedCycleRerunPlan` 이 이미 `public` 클래스라는 점이다. 다만 진입점(`BuildPlan`)은 "날짜 범위 전체를 스캔해 유효한 부품만 남기고 나머지는 통계로 버리는" 반복검사용 API라서, "선택한 딱 한 사이클이 속한 부품 하나만, 기준점 사진이 없어도 버리지 말고 돌려달라"는 리뷰어의 요구와 맞지 않는다. 해결책은 **`SavedCycleRerunPlanner` 안에 신규 `public static` 메서드 1개를 추가**해, 그 메서드 안에서 이미 있는 `private static` 형제 메서드(`GroupIntoParts`/`FillPartDatumPhotos`/`FillPartShotPhotos`/`FillPartDualPhotos`/`FillPartZRangePhotos`)를 그대로 호출하는 것이다 — 같은 클래스 내부 호출이라 **접근제어자 변경이 전혀 필요 없다.** `ValidatePart`(제외 판정)는 호출하지 않아 기준점 사진이 없는 부품도 살아서 반환되고, 그 판단(D-80-09 알림)은 리뷰어 쪽 신규 서비스가 한다. 이 설계는 RepeatRunService 의 기존 동작(회귀 0)에 어떤 영향도 주지 않는다.

두 번째 핵심 발견은 Local Ref stale 문제(함께 처리 2번)의 근본 원인이다: `InspectionSequence.HandleRunStartResetResults`(:499) 는 `_bManualDatumHeld` 가 true 이면 `ClearDatumTransforms()` 를 부르지 않고, `Action_FAIMeasurement.ProcessOneDatum`(:318) 은 `HasCachedDatumTransform` 이 true 이면 **이미지를 다시 grab/load 하지 않고 DatumPhase 전체를 skip** 한다. 즉 "Test Find 로 기준점을 잡아 두고 수동 RUN 만 반복"하는 흐름에서는 RUN 시점에 기준점 가로 사진 자체를 손에 넣을 방법이 없다(Phase 79 리서치가 이미 확인한 "이미지는 검출 직후 Dispose" 제약과 같은 계열의 문제). 해결책은 stale 판정 시점(`Action_FAIMeasurement.TryResolveLocalRef`:1975)에서 곧바로 `GrabOrLoadDatumImage(datum)` 를 한 번 더 불러(이미 있는 헬퍼, alignment 재검출 없이 이미지만 재취득) 그 자리에서 `etld.ComputeLocalRefLine(imgH, cachedTransform)` 를 즉석 재계산하는 것이다. PLC 자동 사이클에서는 사이클 중 설정을 바꿀 방법이 없어 이 stale 경로가 원천적으로 발동하지 않으므로 "PLC 자동 검사는 영향 없음"(D-80-CONTEXT) 은 자연히 만족되지만, 안전망으로 `!parentSeq2.IsProtocolDrivenCycle()` 가드를 명시적으로 추가하는 것을 권장한다.

**Primary recommendation:** `SavedCycleRerunPlanner.BuildPartForSingleCycle(선택된 CycleResultDto, seq, recipeManager)` 신규 메서드로 부품을 재구성하고, 새 `ReviewerReinspectService`(정적 서비스 + 최소 VM) 가 그 결과를 `RepeatRunService.BuildOverrideSnapshot`/`RestoreOverridePathsOnly` 와 같은 필드 집합(SimulImagePath·TeachingImagePath[_Vertical]·TeachingImagePath_Horizontal/_Vertical·RerunZRangeImagePaths·OfflineInspectMode)에 적용한다. 해제는 `MainWindow.Window_Closing`(:452)·`Custom/SystemHandler.cs ProcessTest 진입 직전(:120)`·`SequenceHandler.OnRecipeChanged`(단일 이벤트, 수동/TCP 레시피 변경 공용) 3곳에 훅을 건다 — 이 3곳은 이미 `RepeatRunService.IsSavedCycleRerunActive`/`RestoreActiveSavedCycleOverridesForShutdown`/`ForceOfflineInspectModeOffForAutoTest` 가 정확히 같은 문제(다른 기능)로 선점하고 있어 나란히 추가하면 된다.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| 부품(자재) 사진 그룹핑 | Sequence(`SavedCycleRerunPlanner`, 신규 메서드) | Backend(`CycleResultSerializer.Load`) | 이미 존재하는 "부품 재구성" 알고리즘의 유일한 소스 — D-80-07 이 명시적으로 이 로직 재사용을 요구 |
| 레시피 사진경로 스냅샷/주입/복원 | Sequence(신규 `ReviewerReinspectService`) | — | `ShotConfig`/`DatumConfig`/`DualImageEdgeDistanceMeasurement` 필드를 직접 쓰는 계층은 Sequence 뿐 |
| 자동 해제 트리거(PLC/레시피변경/종료) | Backend(`Custom/SystemHandler.cs`, `SequenceHandler.OnRecipeChanged`) | Frontend(`MainWindow.Window_Closing`) | 기존 3개 훅과 동일 계층 — 새 계층 만들지 않음 |
| 자동 Test Find(다이얼로그 우회) | Frontend(`MainView.xaml.cs`, 신규 헬퍼) | Sequence(`InspectionSequence.TryComposeAlign`/`TryRunSingleDatum`, `DatumFindingService.TryFindDatum`) | 기존 `BtnTestFindDatum_Click` 과 같은 계층, 다이얼로그만 제거 |
| Local Ref stale 재계산 | Sequence(`Action_FAIMeasurement.TryResolveLocalRef`) | Backend(`EdgeToLineDistanceMeasurement.ComputeLocalRefLine`) | 이미지 재취득·설정 키 비교가 모두 이 계층의 기존 헬퍼로 가능 |
| 리뷰어 오버레이 표시 규칙 수정 | Frontend(`ReviewerWindow.xaml.cs DisplayCycle`) | — | 순수 화면 렌더링 문제, 판정/기록 데이터는 무변경 |
| Local Ref ROI 시험 찾기 | Frontend(`MainView.xaml.cs`, 신규 버튼) | Sequence(`EdgeToLineDistanceMeasurement.ComputeLocalRefLine`) | 기존 `BtnTestFindDatum_Click` 과 같은 계층 패턴 재사용 |
| Shot+FAI 트리 노드 선택 | Frontend(`InspectionListView.xaml.cs`, 신규 헬퍼) | — | `NodeViewModel.IsSelected`/`IsExpanded` 가 이미 존재하는 유일한 선택 채널 |

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-80-00:** UI 는 단순하고 쉽게. 신규 UI 는 ① 리뷰어 버튼 1개 ② 메인 화면 상태 줄 1줄(해제 버튼 포함) ③ 기준 ROI 시험 찾기 버튼 1개 뿐. 새 창·새 설정 항목·추가 확인창 금지. 문구는 쉬운 한국어 한 줄.
- **D-80-01:** 리뷰어 버튼 "이 사진으로 파라미터 수정" 1개, NG 원인 설명(원인·근거·확인할 일) 바로 아래, 크게. 더블클릭·우클릭 메뉴 없음.
- **D-80-02:** 행 미선택 또는 불러올 수 없는 행이면 버튼 비활성 + 이유 한 줄 표시.
- **D-80-03:** 누르면 확인창 없이 즉시 불러온다(운영 레시피는 D-80-09 로 보호되므로 안전).
- **D-80-04:** 불러온 뒤 리뷰어 창은 열어 둔 채 메인 화면을 앞으로. 다른 NG 행을 이어서 고를 수 있음.
- **D-80-05:** 메인 화면에서 NG 난 Shot + 그 측정(FAI)을 선택해 파라미터를 바로 보여준다.
- **D-80-06:** Test Find 까지만 자동, RUN 은 사용자가 직접. 자동 Test Find 는 `AskTestImageSource` 대화상자를 거치지 않는다.
- **D-80-07:** 같은 자재의 모든 Shot 사진 + 기준점 사진(가로·세로)을 불러온다. "같은 자재" 묶음은 `SavedCycleRerunPlanner.GroupIntoParts`(시간순, 기준점 z 틱에서 새 자재 시작)와 같은 규칙.
- **D-80-08:** Z 범위 Shot 은 z 후보 사진 전부를 불러와 Z 자동 선택까지 재현. 후보 사진이 없으면 리뷰어에서 선택된 z 한 장 + 상태 줄 안내.
- **D-80-09:** 짝이 맞는 기준점 사진이 없으면(수동 검사 기록 또는 파일 없음) 알림 다이얼로그 후 Shot 사진만 불러온다. 기준점 사진은 현재 것을 그대로, 자동 Test Find 는 하지 않는다.
- **D-80-10:** 불러온 상태에서 레시피 저장 시 파라미터는 저장, 사진 경로(SimulImagePath·TeachingImagePath·TeachingImagePath_Vertical·RerunZRangeImagePaths 등)는 불러오기 전 원래 값으로 저장된다. 저장을 막지 않는다.
- **D-80-11:** 원복 시점: ① 상태 줄 [해제] 버튼 ② PLC 자동 검사 수신 시 자동 해제 ③ 프로그램 종료·레시피 변경 시 자동 해제. 해제해도 파라미터는 유지. 다른 NG 행을 또 불러오면 앞의 불러오기를 대체(원래 값은 처음 것 유지).
- **D-80-12:** 불러온 동안 메인 화면에 눈에 띄는 상태 줄: "리뷰어 사진 사용 중 — 날짜 시간 자재번호" + [해제] 버튼. 로그에도 남김.
- **D-80-13:** 재검사 필요한 `OfflineInspectMode` 는 불러오기 동안 켜고 해제 시 원래 값으로(반복검사와 같은 방식).
- **D-80-14:** 불러온 사진이 JPG 면 상태 줄에 짧게 안내 "JPG 사진 — 실제 검사값과 조금 다를 수 있음". 팝업 없음.
- **D-80-15:** 기준 ROI 시험 찾기를 이번 phase 에 포함. Local Ref 티칭 시 기준점 가로 사진(z1)을 배경으로 기준 ROI 상자를 보여주고, 버튼 1개로 그 자리에서 띠 에지 라인을 찾아 선으로 보여준다. D-79-05 와 같은 사진·같은 찾기 로직.
- **D-80-16:** CLAUDE.md 🔒 가독성 규칙 필수. 삼항/`??`/`?.`/switch식 금지, 중괄호 필수, 헝가리언 접두사, 매직넘버 const, C# 7.2, HImage/HObject/HTuple finally Dispose, 날짜 주석 금지. 검증 grep 5종 0.
- **D-80-17:** MVVM. 불러오기·짝맞추기·스냅샷/복원·상태줄 문구는 서비스+ViewModel. `ReviewerWindow.xaml.cs`·`MainView.xaml.cs` 는 배선만. 버튼 활성/이유·상태줄 문자열은 VM 에서 바인딩.

### Claude's Discretion

- 버튼·상태줄의 정확한 문구·크기·색 (D-80-00 원칙 안에서)
- 리뷰어 → 메인 화면 연결 방식(Owner 경유 등). 새 로직은 서비스/VM, 배선만 code-behind.
- 스냅샷/복원 구현을 `RepeatRunService` 와 공유할지 새 서비스로 뺄지 → **본 리서치 결론: 새 서비스(`ReviewerReinspectService`)로 분리, `SavedCycleRerunPlanner` 에 조회 진입점만 신규 추가**(아래 Pattern 1 참고).
- Local Ref 재계산 트리거 방식 → **본 리서치 결론: stale 판정 시 `GrabOrLoadDatumImage` 재호출 + `ComputeLocalRefLine` 즉석 재계산**(아래 Pattern 4).

### Deferred Ideas (OUT OF SCOPE)

- 재검사 결과와 리뷰어 원래 값의 자동 비교 표시 — 필요하면 다음 phase.
- `2026-05-28-datum-angle-param-ui-cleanup.md` — 무관(점수 0.2).
- (함께 처리 목록 밖) TOP 측정 ROI Edit 후 이동 안 되는 버그 — `/gsd-debug` 별도 트랙(STATE.md 2026-09-18 기록).
</user_constraints>

## Project Constraints (from CLAUDE.md)

- C# 7.2 고정. 삼항 `?:` / 이항 `??`·`??=` / null 조건 `?.`·`?[]` / C# 8 `switch` 식 전부 금지 — `if/else`, 명시적 null 체크, 전통 `switch`(+`break`)만.
- 한 줄 분기도 중괄호 생략 금지. 3개 이상 `&&`/`||` 조건은 이름 있는 bool 로 선추출.
- 헝가리언 접두사(`b`/`n`/`sz`/`d`/`hv`), 매직넘버 금지(named const).
- 새 UI 로직은 ViewModel/서비스. `ReviewerWindow.xaml.cs`·`MainView.xaml.cs` 는 이번에 손대는 지점(버튼 배선, 상태줄 바인딩)에만 최소 추가 — 기존 비대한 code-behind 에 새 로직 추가 금지.
- 날짜 주석 신규 금지(`// 날짜 hbk` 형식) — `// Phase 80: ...` 형식만.
- `HImage`/`HObject`/`HTuple` 반드시 Dispose(`finally`). 특히 Local Ref 재계산용으로 재취득하는 `GrabOrLoadDatumImage` 결과와 auto Test Find 우회 경로에서 직접 `new HImage(path)` 로 로드하는 이미지 모두 `finally` Dispose 대상.
- STATE/ROADMAP 은 수동 편집(`phase.add` 오산정 버그 회피, STATE.md 기존 관행).

<phase_requirements>
## Phase Requirements

이 phase 는 REQUIREMENTS.md 에 매핑된 REQ-ID 가 없다(로드맵 확정 항목, "TBD — none mapped"). `80-CONTEXT.md` 의 D-80-00~17 결정이 곧 요구사항 계약이다. 아래는 결정 → 리서치 지원 섹션 매핑이다.

| 결정 ID | 내용 요약 | 리서치 지원 |
|---|---|---|
| D-80-01/02/03/04/05 | 리뷰어 버튼·흐름·메인 선택 | Pattern 5(트리 선택), Pattern 2(주입) |
| D-80-06 | 자동 Test Find | Pattern 3 |
| D-80-07/08/09 | 사진 범위·부품 그룹핑·기준점 없음 처리 | Pattern 1 |
| D-80-10/11/12/13 | 레시피 보호·해제·상태줄·OfflineInspectMode | Pattern 2 |
| D-80-14 | JPG 안내 | Pattern 2 (상태줄 조립) |
| D-80-15 | Local Ref 시험 찾기 | Pattern 7 |
| D-80-16/17 | 코드 규칙·MVVM | 전체 Pattern, Don't Hand-Roll |
| (함께 처리 1) 오버레이 겹침 버그 | Pattern 6 |
| (함께 처리 2) Local Ref stale 재계산 | Pattern 4 |
</phase_requirements>

## Standard Stack

신규 NuGet 패키지 없음. 기존 `halcondotnet`(HALCON 24.11), `Newtonsoft.Json`(cycle.json 역직렬화, 이미 사용 중), `PropertyTools.Wpf`(PropertyGrid, 이미 사용 중) 만 재사용한다.

**Installation:** 없음.

## Package Legitimacy Audit

N/A — 이 phase 는 외부 패키지를 설치하지 않는다. 전부 기존 코드 확장.

## Architecture Patterns

### System Architecture Diagram

```
[ReviewerWindow] NG 행 선택 (dataGrid_measurements.SelectedItem = ReviewMeasurementRow)
   │  _currentCycle(CycleResultDto), _selectedRow 는 이미 code-behind 필드로 보유
   ▼
[버튼 "이 사진으로 파라미터 수정"] Click
   │  Button_ApplyRowToMain_Click(code-behind, 1줄) → ReviewerReinspectService.LoadForRow(_currentCycle, _selectedRow, ...)
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ ReviewerReinspectService.LoadForRow (신규, Sequence 계층)              │
│  1) 대상 InspectionSequence 해석 (row.OwnerShot.OwnerSequenceName)     │
│  2) SavedCycleRerunPlanner.BuildPartForSingleCycle(cycle, seq, mgr)    │
│     └─ null 또는 DatumPhotoPaths 불완전 → Branch B(D-80-09)            │
│     └─ 완전 → Branch A(D-80-07/08)                                    │
│  3) (최초 1회만) BuildOverrideSnapshot 로 원본 경로 스냅샷 저장          │
│  4) part.ShotPhotoPaths/DatumPhotoPaths/DualPhotos/ZRangePhotoPaths    │
│     를 live ShotConfig/DatumConfig/DualImageEdgeDistanceMeasurement    │
│     인스턴스에 주입 (RepeatRunService.ApplySavedCyclePart 와 동일 필드) │
│  5) OfflineInspectMode 강제 ON(스냅샷에 원래값 보존)                    │
│  6) 상태 플래그 ON + 상태줄 문자열 조립(자재번호·시각·JPG 안내)          │
└─────────────────────────────────────────────────────────────────────┘
   ▼
[MainWindow] Activate() + InspectionListView.SelectShotAndMeasurement(liveShot, liveMeas) (신규 헬퍼)
   │  트리 IsSelected/IsExpanded 설정 → 기존 SelectionChanged 경로로 PropertyGrid/캔버스 갱신(무변경)
   ▼
[MainView] (Branch A 한정) 자동 Test Find — AskTestImageSource 우회 버전 호출
   │  DualImage Datum: 기존 BtnTestFindDatum_Click 의 DualImage 분기 그대로(다이얼로그 이미 없음)
   │  1-image Datum: TeachingImagePath 파일을 직접 new HImage() 로 로드(다이얼로그 생략)
   ▼
[사용자] 파라미터 수정 → RUN(수동, 기존 그대로) → InspectionSequence.HasCachedDatumTransform 분기
   │  Local Ref ROI 를 고쳤다면 → Action_FAIMeasurement.TryResolveLocalRef stale 감지 → 즉석 재계산(Pattern 4)
   ▼
[레시피 저장] MainWindow.SaveRecipe → (신규) 저장 직전 원본 경로로 스왑 → SaveRecipe.Save() → 저장 후 리뷰어 경로 재적용
   ▼
[해제] [해제]버튼 / PLC $TEST 수신(Custom/SystemHandler.cs:120 부근) / Window_Closing / OnRecipeChanged
   → ReviewerReinspectService.Release() → RestoreOverridePathsOnly + OfflineInspectMode 원복 + 상태줄 숨김
```

### Recommended Project Structure

```
WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
  └── SavedCycleRerunPlanner.BuildPartForSingleCycle(...)  [신규 public static, 기존 private 형제 재사용]

WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs  [신규 파일 — Phase 79 는 "신규 .cs 금지"였으나
  80-CONTEXT.md 는 해당 제약을 재확정하지 않음. 신규 파일 여부는 discretion, 아래 Pitfall 참고]
  ├── LoadForRow(CycleResultDto cycle, ReviewMeasurementRow row, out string szDisableReason) : bool
  ├── Release(string szReason)
  ├── IsActive { get; }  (상태줄 바인딩용)
  ├── StatusText { get; } (D-80-12 문구 조립)
  └── 내부: Snapshot(단일, RepeatRunService.BuildOverrideSnapshot 필드셋과 동일 구조)

WPF_Example/UI/Reviewer/ReviewerWindow.xaml
  └── border_ngCause StackPanel 에 Button "이 사진으로 파라미터 수정" 추가 (txt_ngCausePanel 바로 아래)
WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
  └── Button_ApplyRowToMain_Click(1~2줄, 서비스 호출 + MainWindow.Activate())
  └── DisplayCycle 오버레이 합산 로직 수정(Pattern 6)

WPF_Example/UI/ContentItem/MainView.xaml.cs
  └── 상태줄 UI 엘리먼트 + [해제] 버튼 배선(바인딩만)
  └── 자동 Test Find 우회 헬퍼(다이얼로그 없이 호출, BtnTestFindDatum_Click 로직 재사용/추출)
  └── Local Ref "시험 찾기" 버튼 배선

WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
  └── SelectShotAndMeasurement(ShotConfig, MeasurementBase) 신규 헬퍼(트리 재귀 탐색 + IsSelected/IsExpanded)

WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  └── TryResolveLocalRef 의 bStale 분기에 즉석 재계산 추가(Pattern 4)

WPF_Example/MainWindow.xaml.cs
  └── SaveRecipe 에 "사진경로 스왑 후 저장" 훅, Window_Closing 에 해제 훅

WPF_Example/Custom/SystemHandler.cs
  └── MainRun 의 VisionRequestType.Test 분기(:120 부근)에 해제 훅

WPF_Example/Sequence/SequenceHandler.cs (변경 없음, 이벤트만 구독)
  └── OnRecipeChanged 구독 지점 추가(MainWindow.OnLoadRecipe 옆, 또는 서비스 자체 구독)
```

### Pattern 1: 단일 사이클 → 부품(자재) 재구성 (`SavedCycleRerunPlanner` 확장)

**What:** `SavedCycleRerunPlanner.BuildPlan`(이미 `public static`, RepeatRunService.cs:1185)은 날짜 범위를 스캔해 "완전한" 부품만 남기고 나머지는 제외 카운트로 버린다(`ValidatePart`:1710). 리뷰어는 정확히 하나의 사이클이 속한 부품을 원하고, 기준점 사진이 불완전해도(D-80-09) 부품 자체는 받아야 한다. 그래서 `BuildPlan` 을 호출하지 말고, 같은 클래스 안에 신규 `public static` 메서드를 추가해 **`ValidatePart` 를 건너뛰고** 원시 부품을 반환한다.

**When to use:** 리뷰어 버튼 클릭 시, 선택된 `CycleResultDto`(ReviewerWindow 의 `_currentCycle`)로부터.

**핵심 근거(코드 확인):**
- `SavedCycleRerunPart`/`SavedCycleRerunPlan` 은 이미 `public class`(RepeatRunService.cs:1101/1123), `DatumPhotoPaths`/`ShotPhotoPaths`/`DualPhotos`/`ZRangePhotoPaths` 도 전부 `public` 프로퍼티.
- `GroupIntoParts`(:1326)는 "기준점 z 틱(`dto.ZIndex == seq.GetDatumZIndex()`)에서 새 부품 시작, 다음 기준점 틱 전까지 누적" — D-80-07 이 요구하는 바로 그 규칙.
- `CollectAutoTicks`(:1247)는 날짜 폴더(`ResultSavePath\yyyyMMdd`)의 `*_cycle` 하위폴더를 전부 스캔해 `IsEligibleAutoTick`(:1285 — `dto.IsProtocolDriven && dto.ZIndex>=0 && RecipeName 일치 && 이 시퀀스 소유 Shot 포함`)로 거른다. **수동(RUN) 사이클은 `IsProtocolDriven=false` 라 이 스캔에서 원천적으로 빠진다** — 이것이 D-80-09 "수동 검사 기록" 케이스가 실제로 벌어지는 코드 경로다.
- `FillPartDatumPhotos`(:1375)/`FillPartShotPhotos`(:1477)/`FillPartDualPhotos`(:1558)/`FillPartZRangePhotos`(:1401) 는 전부 `private static` 이지만 **같은 클래스 안에서 호출하는 신규 메서드에는 접근제어자 문제가 없다.**

**구현 스케치:**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs (SavedCycleRerunPlanner, 신규 메서드)
// 선택된 사이클 하나가 속한 부품을 그대로 돌려준다. ValidatePart(제외 판정)를 타지 않으므로
//  기준점 사진이 없어도(D-80-09) 부품 자체는 반환된다 — "없음" 판단은 호출부(리뷰어 서비스)가 한다.
public static SavedCycleRerunPart BuildPartForSingleCycle(CycleResultDto selectedCycle, InspectionSequence seq, InspectionRecipeManager recipeManager)
{
    bool bMissingArgs = selectedCycle == null || seq == null || recipeManager == null;
    if (bMissingArgs) { return null; }
    if (!selectedCycle.IsProtocolDriven) { return null; } // 수동 기록 — 부품 개념 자체가 없음(D-80-09 Branch B)

    StatisticsTimeRange range = new StatisticsTimeRange(); // FirstDate=LastDate=선택 사이클 날짜
    range.FirstDate = selectedCycle.InspectionTime.Date;
    range.LastDate = selectedCycle.InspectionTime.Date;

    string szRecipeName = selectedCycle.RecipeName;
    List<CycleResultDto> lstTicks = CollectAutoTicks(range, szRecipeName, seq);
    lstTicks.Sort(CompareByInspectionTime);

    SavedCycleRerunPlan dummyPlan = new SavedCycleRerunPlan(); // GroupIntoParts 가 제외 카운트를 씀 — 여기선 버림
    List<SavedCycleRerunPart> lstParts = GroupIntoParts(lstTicks, seq, dummyPlan);

    string szTargetKey = NgCauseHistory.ResolveCycleKey(selectedCycle); // CycleFolderPath 우선, 없으면 시각
    foreach (var part in lstParts) {
        foreach (var tick in part.Ticks) {
            if (NgCauseHistory.ResolveCycleKey(tick) == szTargetKey) {
                FillPartDatumPhotos(part);
                FillPartShotPhotos(part);
                FillPartDualPhotos(part, seq, recipeManager);
                FillPartZRangePhotos(part);
                return part;
            }
        }
    }
    return null; // 같은 날짜 폴더에서 이 사이클을 포함한 부품을 못 찾음(이례적 — 로그만 남기고 D-80-09 로 폴백)
}
```
**필요한 필드 접근 근거:** `NgCauseHistory.ResolveCycleKey`(이미 `public static`, CycleResultDto.cs:737)가 "CycleFolderPath 우선, 없으면 InspectionTime.Ticks" 로 사이클을 식별하는 기존 단일 소스라 그대로 재사용 가능.

**부품을 찾았지만 기준점 사진이 불완전한 경우(D-80-09 Branch B-2):** `part.DatumPhotoPaths` 가 이 시퀀스의 필요 역할 키(`ComputeRequiredDatumRoleKeys`, private static, :1682 — 필요하면 같은 클래스 안에서 재사용)를 전부 채우지 못했으면 데이텀 사진 없음으로 간주하고 Shot/ZRange 는 그대로 적용, Test Find 는 생략, 알림만 띄운다.

### Pattern 2: 스냅샷/주입/복원 (신규 `ReviewerReinspectService`)

**What:** RepeatRunService 의 `SavedCycleOverrideSnapshot`(:442)·`BuildOverrideSnapshot`(:455)·`RestoreOverridePathsOnly`(:495) 와 **정확히 같은 필드 집합**을 스냅샷/복원한다. RepeatRunService 는 "부품 큐를 순회하며 자동으로 여러 번 갈아치우는" 시나리오라 `Dictionary<ShotConfig,string>` 매핑을 쓰지만, 리뷰어는 "버튼 한 번 = 부품 하나"이므로 구조는 같지만 순환 로직(TriggerNextSavedCyclePart 류)은 필요 없다 — 훨씬 단순하다.

**스냅샷 대상 필드(RepeatRunService.BuildOverrideSnapshot:455-491 과 동일):**
- `ShotConfig.SimulImagePath` (이 시퀀스 소유 Shot 전체, `InspectionSequence.IsShotOwnedBySequence` 로 판정)
- `DatumConfig.TeachingImagePath` / `TeachingImagePath_Vertical` (이 시퀀스의 `DatumConfigs` 전체)
- `DualImageEdgeDistanceMeasurement.TeachingImagePath_Horizontal` / `TeachingImagePath_Vertical` (소유 Shot 의 FAI 순회)
- `ShotConfig.RerunZRangeImagePaths` (복원 시 `null` — RepeatRunService:519 와 동일 규약)
- `SystemSetting.Handle.OfflineInspectMode` (D-80-13, `OfflineSetByRerun`-류 플래그로 "우리가 켰을 때만 끈다")
- (신규, RepeatRunService 에는 없던 것) 부품의 `IndexNumber`·`StartTime`·JPG 여부 → 상태줄 조립용, 레시피에 쓰지 않으므로 스냅샷 불필요.

**최초 로드 시점에만 스냅샷:** D-80-11 "다른 NG 행을 또 불러오면 앞의 불러오기를 대체(원래 값은 처음 것 유지)" — 이미 활성 상태(`IsActive==true`)면 스냅샷을 다시 뜨지 말고 기존 스냅샷 위에 `RestoreOverridePathsOnly` 후 새 부품 값으로 재주입해야 한다(RepeatRunService.`ApplySavedCyclePart`:708-765 의 "먼저 전부 원복 후 이번 부품 값으로 덮어쓰기" 패턴을 그대로 재사용 — 부품 간 사진 잔류 방지 규칙과 동일).

**주입 순서(RepeatRunService.ApplySavedCyclePart:708-765 이식):**
1. `RestoreOverridePathsOnly(snapshot)` (이미 활성 중이면)
2. Shot 전체에 `part.ShotPhotoPaths` 적용 (있는 것만, D-80-07)
3. Shot 전체에 `part.ZRangePhotoPaths` 로 `RerunZRangeImagePaths` 재구성 (D-80-08, 없으면 `null` 유지 → `LoadOfflineZRangeCandidates` 가 자동으로 "1장 폴백" 경로를 탄다 — Action_FAIMeasurement.cs:2335 이하 기존 폴백 로직 그대로, 별도 처리 불필요)
4. Datum 전체에 `part.DatumPhotoPaths` 적용 (D-80-07, 있을 때만 — 없으면 Datum 은 원본 경로 그대로 유지: D-80-09 요구사항 그대로 자연히 만족됨, 별도 분기 불필요)
5. `part.DualPhotos` 순회해 `FindOwnedDualMeasurement`(private, :784 — 같은 서비스에 없으므로 새 서비스가 독립적으로 재구현하거나, `internal` 로 승격해 재사용. 로직 자체는 15줄 남짓이라 재구현이 더 낮은 리스크)

**상태줄 조립(D-80-12/14):** `"리뷰어 사진 사용 중 — " + part.StartTime.ToString("MM-dd HH:mm") + " 자재 " + part.IndexNumber + (JPG 여부 안내)`. JPG 여부는 `Path.GetExtension(임의 대표 경로).Equals(".jpg", OrdinalIgnoreCase)` 로 판정(Setting.OriginImageFormat 조회보다 안전 — 실제 로드한 파일의 진짜 포맷을 본다, CaptureImageSaveService.ResolveOriginImageExtension:411 참고).

### Pattern 3: 자동 Test Find — 다이얼로그 우회

**What:** `MainView.BtnTestFindDatum_Click`(:4475)은 이미 두 경로를 갖는다: DualImage Datum(`VerticalTwoHorizontalDualImage`, :4501)은 `datum.TeachingImagePath`/`TeachingImagePath_Vertical` 을 직접 `new HImage(path)` 로 읽어 **다이얼로그가 없다** — D-80-06 을 이미 만족한다. 1-image Datum(`CircleTwoHorizontal`, :4545)만 `AskTestImageSource()`(:4636, YesNoCancel 대화상자)를 거친다.

**How:** 신규 헬퍼 `RunTestFindForReviewer(DatumConfig datum)` 를 추가해, 1-image 분기에서 `AskTestImageSource()` 호출을 `new HImage(datum.TeachingImagePath)` 직접 로드로 바꾼 버전을 만든다(Dual 분기는 기존 코드 그대로 재사용). Test Find 성공 시 기존과 동일하게 `heldSeq.HoldManualDatum(datum.DatumName)`(:4590) 을 호출해 다음 수동 RUN 이 이 결과를 재사용하게 한다.

**비패턴(Line-fit) Datum 의 캐시 공백(연구 포커스 4번 응답):** `BtnTestFindDatum_Click` 의 else 분기(:4560)는 `svc.TryFindDatum(...)` 을 **직접** 호출한다 — 이는 `InspectionSequence._datumTransforms`/`_localRefLines` 캐시에 쓰지 않는다(`TryComposeAlign`/`TryRunSingleDatum` 만 그 캐시에 쓴다, InspectionSequence.cs:3600/3603). 결과적으로 비패턴 Datum 은 auto Test Find 후에도 `HasCachedDatumTransform`이 false 로 남아, 이어지는 수동 RUN 의 `ProcessOneDatum`(:318)이 캐시-skip 을 타지 않고 **다시 정상적으로 검출**한다. 이 재검출은 리뷰어가 이미 주입한 "같은" 사진(TeachingImagePath)을 다시 읽는 것이므로 **결과가 달라지지 않는다** — 버그가 아니라 약간의 중복 계산이다. 다만 일관성을 위해 auto Test Find 헬퍼는 `svc.TryFindDatum` 대신 `seq.TryRunSingleDatum(datum, img, null, out error)` 를 호출하도록 만들 것을 권장한다 — 이렇게 하면 (a) `_datumTransforms` 캐시가 채워지고, (b) `ComputeLocalRefLinesForDatum`(InspectionSequence.cs:3645)이 함께 실행되어 **Local Ref 옵션이 켜진 측정도 auto Test Find 시점에 바로 국부 기준선을 갖게 되어**, 뒤이은 수동 RUN 이 캐시를 skip 하더라도(`_bManualDatumHeld` 경로) Local Ref 값이 이미 준비돼 있다. 이 대체가 기존 `BtnTestFindDatum_Click` 자체 버튼의 동작을 바꾸지 않도록, 새 헬퍼에서만 이 경로를 쓰고 기존 버튼 코드는 그대로 둔다(회귀 0).

**Dispose 규약:** `new HImage(path)` 로 직접 로드한 이미지는 `try/finally` 로 즉시 Dispose(1-image, DualImage 분기 모두 기존 `BtnTestFindDatum_Click` 코드가 이미 이 패턴 — :4540/4542, :4514/4517 참고, 새 헬퍼도 그대로 따른다).

### Pattern 4: Local Ref stale 시 수동 RUN 재계산

**What:** `Action_FAIMeasurement.TryResolveLocalRef`(:1947-1983)의 `bStale` 분기(:1975-1979)는 현재 무조건 전역 폴백이다. D-80 요구는 "설정이 바뀌었으면 현재 기준점 사진에서 다시 계산"이다.

**왜 이미지가 없는가(근본 원인, 코드 확인):** `Action_FAIMeasurement.ProcessOneDatum`(:318) `if (!bTakesCrossZPath && parentSeq.HasCachedDatumTransform(datum.DatumName)) { nDatumCached++; return; }` — 캐시가 있으면 이미지 grab/load 자체를 안 한다. `InspectionSequence.HandleRunStartResetResults`(:499) 는 `_bManualDatumHeld==true` 일 때 `ClearDatumTransforms()` 를 생략해 캐시를 살려둔다(Test Find 로 잡아둔 기준점을 다음 RUN 이 재사용하는 것이 원래 의도, :1407-1410 주석). 따라서 "Test Find 로 기준점을 잡아두고 ROI 만 고쳐서 RUN" 흐름에서는 RUN 시점에 기준점 이미지를 손에 넣는 코드 경로가 아예 없다.

**How:** stale 로 판정된 그 순간, `GrabOrLoadDatumImage(datum)`(:1080, 이미 있는 헬퍼 — SIMUL_MODE/OfflineInspectMode 면 파일 로드, 아니면 라이브 grab)을 한 번 더 호출해 이미지를 재취득하고, `etld.ComputeLocalRefLine(imgH, cachedTransform)` 을 즉석으로 다시 실행한다. `cachedTransform` 은 `parentSeq2.TryGetDatumTransform(etld.DatumRef, out transform)`(:3610, 이미 있음)으로 얻는다 — **정렬(align)을 다시 하지 않고 이미 캐시된 transform 을 그대로 쓴다**(요구사항이 "ROI/에지 설정만 바뀜"이므로 정렬 재검출은 불필요·과함).

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:1975 부근 수정 스케치
bool bStale = result.SettingsKey != etld.BuildLocalRefSettingsKey();
if (bStale) {
    bool bManualCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle(); // 안전망 — PLC 자동은 절대 이 경로 안 탐(D-80 "PLC 자동 영향 없음")
    if (bManualCycle) {
        LocalRefLineResult recomputed = TryRecomputeStaleLocalRef(etld, parentSeq2); // 신규 private 헬퍼
        if (recomputed != null && recomputed.Found) {
            etld.InjectedLocalRef = recomputed;
            szReason = null;
            return true; // 재계산 성공 — 전역 폴백 없이 국부 기준 사용
        }
    }
    szReason = EdgeToLineDistanceMeasurement.LOCAL_REF_REASON_STALE;
    return false; // 재계산도 실패(또는 PLC 자동) — 기존과 동일하게 전역 폴백
}
```

**`TryRecomputeStaleLocalRef` 헬퍼 설계:**
```csharp
private LocalRefLineResult TryRecomputeStaleLocalRef(EdgeToLineDistanceMeasurement etld, InspectionSequence parentSeq2) {
    DatumConfig datum = FindDatumByName(parentSeq2, etld.DatumRef); // InjectDatumOrigin 과 같은 조회 패턴(:1897-1903)
    if (datum == null) { return null; }
    HTuple transform;
    if (!parentSeq2.TryGetDatumTransform(etld.DatumRef, out transform)) { return null; }
    HImage img = null;
    try {
        img = GrabOrLoadDatumImage(datum); // 기존 헬퍼 재사용 — 새 grab 경로 없음
        if (img == null) { return null; }
        return etld.ComputeLocalRefLine(img, transform); // 기존 헬퍼(Phase 79) 그대로
    } finally {
        SafeDisposeImage(img); // 기존 헬퍼(:여러 곳에서 이미 사용)
    }
}
```

**성능/스레드 우려:** 리뷰어 재검사 흐름은 D-80-13 에 따라 `OfflineInspectMode` 가 강제 ON 이므로 `GrabOrLoadDatumImage` 는 **파일 읽기**만 한다(라이브 카메라 grab 아님) — 시퀀스 스레드를 블로킹할 정도의 지연은 없다. 실카메라로 이 경로를 타는 경우(리뷰어 흐름 밖, 순수 수동 지그 사용자가 실시간으로 ROI 를 고치는 경우)는 라이브 grab 1회가 추가되는데, 이는 사용자가 명시적으로 "RUN" 을 누른 시점에만 일어나고 Test Find 처럼 매 프레임 반복되지 않으므로 허용 가능한 비용이다.

### Pattern 5: 트리에서 Shot+FAI(측정) 노드 프로그램적 선택

**What:** `InspectionListView.SetSelectionChange(string seqName)`(:840)는 시퀀스(최상위) 노드만 선택한다. Shot/FAI/Measurement 레벨 선택은 지금까지 사용자의 트리 클릭(`TreeListBox_SelectionChanged`, :878 이하)으로만 일어났다 — 프로그램적으로 하위 노드를 선택하는 기존 헬퍼가 없다.

**How:** `NodeViewModel`(NodeViewModel.cs)은 `Children`(ObservableCollection, LoadChildren 지연로딩), `Param`(object, 실제 ShotConfig/FAIConfig/MeasurementBase/DatumConfig 참조) 을 갖는다. 리뷰어가 넘겨준 DTO(`ReviewMeasurementRow.OwnerShot.ShotName`, `Source.MeasurementName`)로는 **live 객체가 아니므로**, 먼저 `SystemHandler.Handle.Sequences.RecipeManager.Shots` 를 이름으로 순회해 live `ShotConfig`/`MeasurementBase` 를 찾는다(이 이름 매칭은 `RepeatRunService.FindOwnedDualMeasurement`:784-822 가 이미 쓰는 패턴 — `MeasurementName ?? TypeName` 키 비교). 그 다음 트리를 재귀 탐색해 `item.Param == liveShot`(참조 동등)인 `NodeViewModel` 을 찾아 `IsExpanded=true`(조상 전부) + `IsSelected=true`(최종 노드) 를 설정한다 — `SetSelectionChange`(:845-851)가 이미 이 패턴(`item.IsSelected = true`)을 쓰고 있어 TreeListBox 가 `IsSelected` 바인딩만으로 실제 선택을 반영함을 확인했다.

```csharp
// Source: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (신규 헬퍼, SetSelectionChange:840 옆에 추가)
public void SelectShotAndMeasurement(ShotConfig liveShot, MeasurementBase liveMeas) {
    if (treeListBox_sequence.Items.Count == 0) { return; }
    NodeViewModel root = treeListBox_sequence.Items[0] as NodeViewModel;
    if (root == null) { return; }
    root.IsExpanded = true;
    NodeViewModel found = FindNodeByParam(root, liveMeas); // liveMeas 우선, 없으면 liveShot
    if (found == null) { found = FindNodeByParam(root, liveShot); }
    if (found == null) { return; }
    ExpandAncestors(found);
    found.IsSelected = true;
    treeListBox_sequence.ScrollIntoView(found);
}

private NodeViewModel FindNodeByParam(NodeViewModel node, object targetParam) {
    if (targetParam == null) { return null; }
    if (ReferenceEquals(node.Param, targetParam)) { return node; } // NodeViewModel.Param 프로퍼티 존재 확인 필요(코드 조사: Node/Param 매핑, NodeViewModel.cs 전체 재확인 권장)
    foreach (NodeViewModel child in node.Children) {
        NodeViewModel result = FindNodeByParam(child, targetParam);
        if (result != null) { return result; }
    }
    return null;
}
```

**⚠ 확인 필요(계획 단계 재조사 권장):** `NodeViewModel.cs` 앞부분(1-60줄)만 이번 조사에서 열람했다. `Param` 프로퍼티의 정확한 노출 형태(직접 프로퍼티인지 `Node.Param` 위임인지)와 `IsExpanded`/`IsSelected` 의 실제 선언부는 계획 단계에서 파일 전체(특히 60줄 이후)를 반드시 재확인해야 한다 — `treeListBox_sequence.SelectedItem is NodeViewModel` 캐스팅이 코드 전역에서 빈번히 쓰이는 것으로 보아 존재는 확실하나 정확한 타입/접근자는 미확인.

**재진입 주의(TreeListBox 가상화):** `TreeListBox_SelectionChanged` 핸들러 자체의 기존 주석(:880-885)이 "컨테이너 생성 도중 동기 재선택 시 크래시('Cannot call StartAt when content generation is in progress')" 를 경고한다. `SelectShotAndMeasurement` 를 리뷰어 클릭 직후 곧바로 호출하면 트리가 막 리빌드/렌더링 중일 수 있으므로 — 기존 패턴처럼 `Dispatcher.BeginInvoke(Background, ...)` 로 한 틱 늦춰 호출하는 것을 권장한다.

### Pattern 6: 리뷰어 오버레이 겹침 버그 수정

**What:** `ReviewerWindow.DisplayCycle`(:136-196)의 "사이클 전체 보기" 는 `ResolveCycleImagePath(cycle)`(:176, 측정 결과가 있는 **첫 Shot** 의 사진 1장만 로드)로 이미지를 표시하면서, overlay 는 `cycle.Shots` 전체를 `SelectMany` 해 **모든 Shot 의 모든 FAI overlay** 를 합쳐 그린다(:183-192). 즉 화면엔 Shot A 의 사진 1장만 있는데, Shot B·C·D 의 측정선까지 같은 사진 위에 겹쳐 그려진다 — 이것이 "여러 Shot 의 검출 선이 사진 한 장에 겹쳐 그려지는 버그"다.

**How:** `ResolveCycleImagePath` 가 사진을 고른 것과 **같은 Shot** 만 overlay 대상으로 필터링한다. `ReviewerImagePathResolver.ResolveCycleImagePath`(ReviewMeasurementRow.cs:241-269)는 이미 내부적으로 `FindMeasuredOriginInShot`(:272-292)로 "측정 결과가 있는 첫 FAI 를 가진 Shot" 을 순회하지만 **그 Shot 자체를 반환하지 않고 경로 문자열만 반환**한다 — 시그니처를 확장하거나(오버로드 추가, out ShotResultDto), `DisplayCycle` 쪽에서 별도로 "그 경로를 낸 Shot" 을 다시 찾아야 한다. 더 간단한 방법: `ResolveCycleImagePath` 대신 **그 경로를 만든 Shot 객체를 함께 반환하는 새 오버로드**를 추가한다(기존 오버로드는 무변경 — 회귀 0).

```csharp
// Source: WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs (ReviewerImagePathResolver, 신규 오버로드)
public static string ResolveCycleImagePath(CycleResultDto cycle, out ShotResultDto ownerShot) {
    ownerShot = null;
    if (cycle == null || cycle.Shots == null) { return null; }
    foreach (var shot in cycle.Shots) {
        string szOrigin = FindMeasuredOriginInShot(shot); // 기존 private 메서드 그대로(같은 클래스)
        if (!string.IsNullOrEmpty(szOrigin)) { ownerShot = shot; return szOrigin; }
    }
    ShotResultDto firstShot = cycle.Shots.Count > 0 ? cycle.Shots[0] : null;
    if (firstShot != null && IsExistingFile(firstShot.ResultImagePath)) { ownerShot = firstShot; return firstShot.ResultImagePath; }
    return null;
}
```
`DisplayCycle`(:176, :183)을 이 오버로드로 바꾸고, overlay 합산 대상을 `cycle.Shots` 전체가 아니라 `ownerShot.FAIs` 만으로 좁힌다:
```csharp
// Source: WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs:176-193 수정 스케치
ShotResultDto ownerShot;
string szCycleImagePath = ReviewerImagePathResolver.ResolveCycleImagePath(cycle, out ownerShot);
if (!string.IsNullOrEmpty(szCycleImagePath)) { halconViewer.LoadImage(szCycleImagePath); }

List<EdgeInspectionOverlay> allOverlays;
if (ownerShot != null && ownerShot.FAIs != null) {
    allOverlays = ownerShot.FAIs.Where(f => f.LastOverlays != null).SelectMany(f => f.LastOverlays).ToList();
} else {
    allOverlays = new List<EdgeInspectionOverlay>(); // 표시할 사진 자체가 없으면 overlay 도 없음(기존보다 더 안전)
}
halconViewer.SetInspectionOverlays(allOverlays);
```
이 수정은 D-80 의 다른 어떤 기능과도 무관하며 독립적으로 검증 가능하다(리뷰어에서 여러 Shot 을 가진 사이클을 열어 "전체 보기" 클릭 → 화면의 사진에 실제로 찍힌 Shot 의 선만 보이는지 확인).

### Pattern 7: Local Ref ROI "시험 찾기" (D-80-15)

**What:** `MainView`는 이미 `LocalRef_*` ROI 를 편집하는 배선(`BuildPointRoiDefinitions`:428/526-534, `ApplyPointRoiMoveDelta`:898/918-919, `ApplyPointRoiResize`:1015-1018)을 갖고 있다(Phase 79). 지금은 이 ROI 를 그리는 배경 이미지가 "현재 캔버스에 떠 있는 이미지"(측정 사진일 수도, z1 사진일 수도)에 의존한다 — Phase 79 UAT 에서 "측정 사진에서는 에지가 안 보임"이 불편으로 지적됐다(80-CONTEXT.md 배경).

**How:** 새 버튼 "기준 ROI 시험 찾기"는 (1) 선택된 측정의 `DatumRef` 로 해당 `DatumConfig` 를 찾고, (2) 그 Datum 의 `TeachingImagePath`(z1 가로 사진, D-79-05 규약)를 캔버스에 강제로 로드(`halconViewer.LoadImage(datum.TeachingImagePath)` — `MainView.DisplayDatumImage`(InspectionListView.xaml.cs:926 에서 이미 쓰는 헬퍼) 재사용 가능), (3) `EdgeToLineDistanceMeasurement.ComputeLocalRefLine(imgLoaded, transform)` 을 즉석 호출해 결과를 오버레이로 그린다. `transform` 은 티칭 단계라 아직 사이클 캐시가 없을 수 있으므로 identity(`HOperatorSet.HomMat2dIdentity`)를 써도 무방하다 — 이미 티칭된 ROI 좌표는 원본 좌표계이고(O-79-09, Phase 79 리서치 확인), 시험 찾기는 "이 ROI 로 지금 이 사진에서 에지가 잡히는지"만 미리보기하는 용도이기 때문이다.

```csharp
// Source: WPF_Example/UI/ContentItem/MainView.xaml.cs (신규 버튼 핸들러 스케치, BtnTestFindDatum_Click:4475 패턴 참고)
private void BtnTestFindLocalRef_Click(object sender, RoutedEventArgs e) {
    var etld = mParentWindow.inspectionList.SelectedParam as EdgeToLineDistanceMeasurement;
    if (etld == null) { CustomMessageBox.Show("기준 ROI 시험 찾기", "EdgeToLineDistance 측정을 선택하세요."); return; }
    DatumConfig datum = FindDatumByName(etld.DatumRef); // 기존 조회 패턴 재사용(여러 곳에 유사 코드 존재)
    if (datum == null || string.IsNullOrEmpty(datum.TeachingImagePath) || !File.Exists(datum.TeachingImagePath)) {
        CustomMessageBox.Show("기준 ROI 시험 찾기", "이 측정의 기준점에 가로 사진(z1)이 티칭되지 않았습니다.");
        return;
    }
    HImage img = null;
    try {
        img = new HImage(datum.TeachingImagePath);
        halconViewer.LoadImage(datum.TeachingImagePath);
        HTuple identity;
        HOperatorSet.HomMat2dIdentity(out identity);
        LocalRefLineResult result = etld.ComputeLocalRefLine(img, identity);
        if (result.Found) {
            // 기존 overlay 렌더 경로(halconViewer.SetInspectionOverlays 또는 전용 헬퍼)로 선 표시
        } else {
            CustomMessageBox.Show("기준 ROI 시험 찾기", "띠 에지를 찾지 못했습니다: " + result.Error);
        }
    } finally {
        if (img != null) { try { img.Dispose(); } catch { } }
    }
}
```

**주의:** `datumTransform` 을 identity 로 넘기면 `ComputeLocalRefLine` 내부의 `TryFitLine` 이 ROI 좌표를 "그대로"(변환 없이) 사용한다 — 이는 **티칭 사진(z1) 자체에서 ROI 를 찾는" 용도라 정확히 맞다(실제 런타임에서도 같은 사진에서 처음 검출할 때는 아직 transform 이 identity 에 가깝다, TryComposeAlign 흐름과 동일 전제). 계획 단계에서 `TryFitLine` 의 transform 파라미터 의미(ROI 를 이동시키는 보정값인지, 단순 identity 를 넣어도 안전한지)를 한 번 더 코드로 재확인할 것.

### Anti-Patterns to Avoid

- **`SavedCycleRerunPlanner.BuildPlan` 을 그대로 호출해 리뷰어 부품을 찾는 것:** `ValidatePart` 가 기준점 사진 없는 부품을 통째로 버리므로 D-80-09 분기를 만들 데이터 자체가 사라진다. 반드시 `ValidatePart` 를 타지 않는 신규 진입점을 써야 한다(Pattern 1).
- **리뷰어 override 를 `RepeatRunService` 인스턴스에 얹는 것:** `RepeatRunService` 는 "부품 큐를 순회하는 실행기"이고 `IsSavedCycleRerunActive` 전역 게이트, `TriggerNextSavedCyclePart` 폴링 루프 등 리뷰어가 필요 없는 상태machine을 딸려온다. 독립된 정적 서비스로 분리하는 것이 D-80-17 MVVM 원칙과도 맞다.
- **Local Ref stale 재계산을 PLC 자동 사이클에서도 허용하는 것:** 이론상 발동하지 않더라도(사이클 중 설정 변경 불가), 명시적 `IsProtocolDrivenCycle()` 가드 없이 두면 향후 다른 변경이 이 안전 불변식을 조용히 깨뜨릴 수 있다.
- **`BtnTestFindDatum_Click` 자체를 리팩토링해서 재사용하는 것:** 기존 버튼의 다이얼로그 동작(수동 지그 사용자용)은 D-80 범위 밖이다. 새 헬퍼로 분리하고 기존 버튼 코드는 절대 수정하지 않는다(회귀 0 원칙, RepeatRunService 를 건드리지 않은 것과 동일한 이유).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 자재 단위로 사진 묶기 | 새 그룹핑 알고리즘 | `SavedCycleRerunPlanner.GroupIntoParts`(+형제 Fill 메서드) | 이미 실전 검증된 로직(Phase 77 D-77-06, quick-260911-fia) — 시간순+기준점z 경계 규칙을 새로 만들면 두 갈래 구현이 미묘하게 달라질 위험 |
| 사진 경로 스냅샷/복원 | 새 Dictionary 패턴 | `RepeatRunService.BuildOverrideSnapshot`/`RestoreOverridePathsOnly` 의 필드 집합을 그대로 이식 | 이미 어떤 필드를 스냅샷해야 하는지(Shot/Datum/DualMeas/ZRange) 실전에서 확정된 목록 |
| Shot+FAI 이름 → live 객체 조회 | 새 조회 헬퍼 | `RepeatRunService.FindOwnedDualMeasurement` 패턴(`MeasurementName ?? TypeName` 키) | 기존 코드가 이미 이 정확한 매칭 규칙을 씀 |
| "지금 라이브 촬영 중인가" 판정 | 새 조건식 | `Action_FAIMeasurement.IsLiveCaptureMode()`(private static, 이미 SIMUL_MODE 조건부 컴파일까지 감쌈) | 조건부 컴파일 분산은 "한쪽 빌드만 조용히 달라지는 사고" 위험(기존 주석 경고) |
| Datum 원점/각도 주입 | 새 주입 로직 | `Action_FAIMeasurement.InjectDatumOrigin`/`InjectLocalRef` | `IDatumOriginConsumer` 계약이 이미 확립돼 있음 |

**Key insight:** Phase 80 은 "새 기능"이 아니라 "이미 있는 3개의 기존 메커니즘(반복검사 부품 그룹핑, Datum 원점 주입, Test Find)을 리뷰어라는 새 진입점에서 재사용"하는 문제다. 새로 설계할 것은 오직 (a) 단일-부품 조회 진입점 (b) 스냅샷 소유 서비스 (c) 트리 프로그램적 선택 헬퍼 3가지뿐이다.

## Common Pitfalls

### Pitfall 1: ZRangeImages 는 DatumImages 와 저장 게이트가 다르다
**What goes wrong:** 계획 단계에서 "기준점 사진과 Z 후보 사진 모두 PLC 자동 검사 때만 저장된다"고 가정하면 틀린 코드가 나온다.
**Why it happens:** `ArchiveDatumImageForCycle`(Action_FAIMeasurement.cs:1587-1608)의 저장 게이트는 `IsLiveCaptureMode() && parentSeq.IsProtocolDrivenCycle()` 이지만, `SaveZRangeCandidateImageIfEnabled`(:2126-2154)의 게이트는 `SystemSetting.Handle.SaveZRangeCandidateImages && IsLiveCaptureMode()` **뿐**이다 — `IsProtocolDrivenCycle()` 조건이 없다. 즉 **수동 RUN(실카메라, OfflineInspectMode 꺼짐, 저장 체크박스 켜짐)도 Z 후보 사진을 저장할 수 있다.**
**How to avoid:** D-80-09 분기(Branch B, 기준점 사진 없음)에서도 `part`(또는 선택된 사이클 자신)의 `ZRangeImages` 는 존재할 수 있으므로 무조건 버리지 말고 있으면 쓴다. 없으면 D-80-08 의 "선택된 z 한 장 + 안내"로 폴백한다.
**Warning signs:** 수동 RUN 으로 만들어진 사이클을 리뷰어로 불러왔는데 Z 범위 Shot 의 Z 자동선택이 재현되는 경우(있으면 정상, 계획에서 이 가능성을 놓치면 "왜 되는지 모르는" 코드가 됨).

### Pitfall 2: `HasCachedDatumTransform` 이 true 면 DatumPhase 가 이미지 자체를 안 만진다
**What goes wrong:** "수동 RUN 시 Local Ref 를 재계산하자"를 `ComputeLocalRefLinesForDatum`(DatumPhase 훅)에만 손대서 고치려 하면, `_bManualDatumHeld` 경로에서는 그 훅이 **호출조차 되지 않는다**(ProcessOneDatum:318 이 캐시-skip 으로 DatumPhase 전체를 건너뜀).
**Why it happens:** 캐시-skip 최적화(quick-260807)는 "같은 사이클 안에서 이미 검출 성공한 기준점을 또 검출하지 않는다"는 성능 목적으로 만들어졌고, Local Ref 재계산 요구(Phase 80)는 그 최적화가 존재하기 전에는 없던 새로운 요구다.
**How to avoid:** 재계산은 `TryResolveLocalRef`(측정 직전 주입 지점, DatumPhase 밖)에서 트리거해야 한다(Pattern 4). DatumPhase 훅을 건드리지 않는다.
**Warning signs:** 코드를 고쳤는데도 stale 로그가 계속 뜨면, 그 코드가 DatumPhase 안에 있어서 애초에 실행되지 않았다는 뜻이다.

### Pitfall 3: `OnRecipeChanged` 발화 시점엔 이미 새 레시피가 로드돼 있다
**What goes wrong:** `SequenceHandler.LoadRecipe`(:163)가 `OnRecipeChanged` 를 **레시피 교체 완료 후** 발화하므로, 이 이벤트를 받고 나서 "스냅샷에 저장해 둔 옛 `ShotConfig` 인스턴스"에 값을 되돌리는 것은 이미 늦다 — 그 인스턴스들은 `RecipeManager.Shots` 에서 이미 빠져나갔다(새 레시피가 완전히 새 인스턴스 집합으로 교체됨).
**Why it happens:** RepeatRunService 의 `FindSavedCycleAbortReason`(:613-640)은 이 문제를 폴링 루프에서 "레시피가 바뀌었으면 중단"으로 사전에 감지하지만, 리뷰어 override 는 폴링 루프가 없으므로 이벤트 기반으로만 감지 가능하다.
**How to avoid:** `OnRecipeChanged` 핸들러에서는 (a) 옛 인스턴스 경로 복원은 **시도해도 되지만 실패해도 무해**(이미 버려질 객체), (b) 반드시 해야 하는 것은 `IsActive` 플래그를 끄고 상태줄을 숨기고 `OfflineInspectMode` 복원(이건 전역 Setting 이라 레시피와 무관하게 유효)이다.
**Warning signs:** 레시피를 바꾼 뒤에도 메인 화면 상태줄이 "리뷰어 사진 사용 중"으로 남아있으면 이 핸들러가 안 불렸거나 순서가 잘못됐다는 신호.

### Pitfall 4: SaveRecipe 의 "경로만 원복" 타이밍
**What goes wrong:** D-80-10 을 "저장 직전에 스왑, 저장 후 재적용"이 아니라 "저장 자체를 막고 별도 파일에 쓰기"처럼 구현하면, `SequenceHandler.SaveRecipe`(Sequence/SequenceHandler.cs:170)의 기존 직렬화 경로(INI 리플렉션, `ParamBase.Save`)를 건드려야 해서 위험이 커진다.
**How to avoid:** `MainWindow.SaveRecipe`(:292)에서 `mSystemHandler.Sequences.SaveRecipe(...)`(:314) 호출 **직전에** `RestoreOverridePathsOnly`(파라미터는 이미 live 객체에 있으므로 영향 없음, 사진 경로만 원본으로) → 저장 → **직후에** 저장된 부품 값 재주입. 이렇게 하면 `SequenceHandler.SaveRecipe` 자체는 단 한 줄도 안 건드린다(회귀 0).
**Warning signs:** 저장된 INI 파일을 열어 `SimulImagePath`/`TeachingImagePath` 값이 리뷰어가 주입한 임시 경로로 되어 있으면 이 타이밍이 잘못됐다는 뜻(D-80-10 정면 위반).

### Pitfall 5: 트리 재귀 탐색을 트리 리빌드 중에 실행
**What goes wrong:** `SelectShotAndMeasurement` 를 리뷰어 버튼 클릭의 동기 흐름 안에서 즉시 호출하면, 마침 `NodeViewModel.LoadChildren`(지연 로딩) 이 진행 중일 때 `TreeListBox_SelectionChanged` 의 기존 경고(:880-885, "Cannot call StartAt when content generation is in progress")와 같은 계열의 크래시가 재현될 수 있다.
**How to avoid:** `Dispatcher.BeginInvoke(Background, ...)` 로 한 틱 지연(기존 코드가 이미 이 패턴을 씀).

## Code Examples

### 부품(자재) 그룹핑 — 기존 규칙 원문
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs:1326-1354 (SavedCycleRerunPlanner.GroupIntoParts)
private static List<SavedCycleRerunPart> GroupIntoParts(List<CycleResultDto> lstTicks, InspectionSequence seq, SavedCycleRerunPlan plan) {
    List<SavedCycleRerunPart> lstParts = new List<SavedCycleRerunPart>();
    int nDatumZ = seq.GetDatumZIndex();
    SavedCycleRerunPart currentPart = null;
    int nPreDatumTickCount = 0;
    foreach (var dto in lstTicks) {
        bool bIsDatumTick = dto.ZIndex == nDatumZ;
        if (bIsDatumTick) {
            currentPart = new SavedCycleRerunPart();
            currentPart.StartTime = dto.InspectionTime;
            currentPart.IndexNumber = dto.IndexNumber;
            lstParts.Add(currentPart);
        }
        if (currentPart == null) { nPreDatumTickCount++; continue; }
        currentPart.Ticks.Add(dto);
    }
    if (nPreDatumTickCount > 0) { RecordExclusion(plan, REASON_NO_DATUM_TICK, nPreDatumTickCount + "건"); }
    return lstParts;
}
```

### 자동 해제 훅 3곳 — 기존 선례(RepeatRunService/OfflineInspectMode)
```csharp
// Source: WPF_Example/MainWindow.xaml.cs:452-471 (Window_Closing) — 프로그램 종료 훅 선례
private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
    // ...
    RepeatRunService.RestoreActiveSavedCycleOverridesForShutdown(); // ← 리뷰어도 이 옆에 ReviewerReinspectService.Release("프로그램 종료") 추가
    mSystemHandler.Release();
}
```
```csharp
// Source: WPF_Example/Custom/SystemHandler.cs:117-127 (MainRun, VisionRequestType.Test) — PLC 자동 검사 도착 훅 선례
case VisionRequestType.Test:
    if (Setting.AutoLogoutWhenRecvTest && Login.IsLogin) { Login.LogOut(); }
    ForceOfflineInspectModeOffForAutoTest(); // ← 리뷰어도 이 옆에 ReviewerReinspectService.Release("PLC 자동 검사 수신") 추가
    if (!ProcessTest(packet.AsTest())) { /* ... */ }
    break;
```
```csharp
// Source: WPF_Example/Sequence/SequenceHandler.cs:163 (LoadRecipe 완료) — 레시피 변경 단일 이벤트(수동/TCP 공용)
OnRecipeChanged?.Invoke(this, new RecipeChangedEventArgs(name)); // ← ReviewerReinspectService 가 자체 구독해 Release("레시피 변경")
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| 리뷰어에서 NG 확인 후 기준점 사진을 사용자가 직접 폴더(예: 17:38/17:43)에서 찾아 수동으로 로드 | 버튼 1개로 같은 자재의 Shot+기준점+Z후보 사진 전부 자동 주입, 파라미터 수정 후 RUN 만 | Phase 80 | 사용자가 반복하던 수동 파일 탐색을 제거(80-CONTEXT.md "사용자가 전에 1738·1743 폴더에서 기준점 사진 짝을 손으로 찾았던 일을 프로그램이 대신") |
| Local Ref stale → 항상 전역 폴백 | 수동 RUN 한정 즉석 재계산, 실패 시에만 폴백 | Phase 80 | ROI 미세조정 반복 작업(Phase 79 UAT 워크플로) 속도 개선 |

**Deprecated/outdated:** 없음 — 기존 경로(옵션 꺼짐, PLC 자동, 반복검사) 전부 무변경 유지.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `NodeViewModel.Param` 이 정확히 이 이름의 public 프로퍼티로 존재하고 `ReferenceEquals` 비교가 유효하다 | Pattern 5 | 실제 필드명/구조가 다르면 트리 탐색 헬퍼를 다시 작성해야 함 — 계획 착수 전 `NodeViewModel.cs` 전체(60줄 이후) 재확인 필수 |
| A2 | `ComputeLocalRefLine` 에 identity transform 을 넘겨도 "시험 찾기"용 미리보기로 안전하다(정식 사이클 계산과 좌표계 불일치 없음) | Pattern 7 | `TryFitLine` 이 transform 을 단순 이동이 아니라 다른 방식으로 쓰면 미리보기 결과가 실제 런타임 값과 달라 보일 수 있음 — `VisionAlgorithmService.TryFitLine` 의 transform 파라미터 의미를 계획/구현 단계에서 한 번 더 정독 필요 |
| A3 | `SelectedZText`/`RefSourceText` 표시 로직처럼, 리뷰어에서 불러온 임시 상태가 UI 스레드 바인딩만으로 충분히 즉시 반영된다(별도 강제 리프레시 불필요) | Pattern 2 | PropertyGrid/트리 캐시가 stale 하게 남으면 사용자가 "값이 안 바뀐다"고 오인할 수 있음 — `InspectionListView` 의 기존 `RefreshParamEditor()`/force-rebind 패턴(:939-947) 재사용 여부를 계획에서 명시할 것 |

## Open Questions (RESOLVED)

> 2026-09-18 plan 단계에서 모두 해결: Q1 → D-80-18(신규 .cs 허용, csproj 등록), Q2 → D-80-19(필요 사진 하나라도 없으면 없음과 같게, 80-03 IsDatumPhotoSetComplete), Q3 → 트리 선택은 측정 노드까지(D-80-05), 시험 찾기는 측정의 DatumRef 를 내부 조회(80-02 LocalRefTestFindService).

1. **[RESOLVED → D-80-18]** **`ReviewerReinspectService` 를 신규 .cs 파일로 만들지, 기존 파일(예: `RepeatRunService.cs` 또는 `Action_FAIMeasurement.cs`)에 얹을지.**
   - What we know: Phase 79 는 "신규 .cs 파일 금지"를 명시적 제약으로 걸었지만(csproj `<Compile Include>` 스테이징 회피 목적), 80-CONTEXT.md 는 이 제약을 재확정하지 않았다.
   - What's unclear: 이 제약이 프로젝트 전반의 암묵적 관행인지, Phase 79 한정 리스크 회피였는지.
   - Recommendation: 계획 단계에서 사용자에게 1줄로 확인(신규 .cs 허용 여부). 허용되면 `ReviewerReinspectService.cs` 신규 파일이 가장 깨끗하다(기존 파일 비대화 방지, CLAUDE.md MVVM 원칙과도 부합).

2. **[RESOLVED → D-80-19]** **D-80-09 의 "짝이 맞는 기준점 사진이 없음" 알림 다이얼로그 문구와, Branch A/B 판정에 쓸 정확한 "완전성" 기준.**
   - What we know: `part.DatumPhotoPaths` 가 `ComputeRequiredDatumRoleKeys(seq)`(RepeatRunService.cs:1682, private static — 같은 클래스 내 재사용 가능)의 필요 키를 전부 채우면 완전.
   - What's unclear: DualImage Datum 중 한쪽(가로만 있고 세로 없음) 같은 부분 결손 시 D-80-09 를 "완전 폴백"으로 볼지 "있는 것만 적용"으로 볼지 CONTEXT.md 에 명시가 없다.
   - Recommendation: 계획 단계에서 discuss 하거나, 보수적으로 "필요 역할 키 중 하나라도 없으면 전체 폴백"(D-80-09 원문 "짝이 맞는"의 자연스러운 해석)으로 확정.

3. **[RESOLVED → D-80-05 / 80-02]** **`SelectShotAndMeasurement` 가 DatumConfig 노드(Shot/FAI 가 아니라 Datum 자체)를 직접 선택해야 하는 경우가 있는가.**
   - What we know: D-80-05 는 "NG 난 Shot + 그 측정(FAI)" 선택만 요구한다.
   - What's unclear: Local Ref 옵션이 켜진 측정을 열었을 때 사용자가 곧바로 "기준 ROI 시험 찾기"를 누르려면 그 측정의 `DatumRef` 를 알아야 하는데, 트리 선택 자체는 Datum 노드로 이동하지 않아도 되는지.
   - Recommendation: Pattern 7 의 버튼 핸들러가 `SelectedParam`(측정)에서 `DatumRef` 를 읽어 Datum 을 내부적으로만 조회하면 되므로, 트리 선택 자체는 D-80-05 그대로(측정 노드까지)로 충분 — 별도 조치 불필요, 계획에 명시만 할 것.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음(프로젝트 전역 정책 — xUnit/NUnit/MSTest 미도입) |
| Config file | 없음 |
| Quick run command | `"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` [VERIFIED: Phase 77/78/79 다수 실사용, 79-RESEARCH.md 인용] |
| Full suite command | 동일(빌드=검증 전부) + CLAUDE.md grep 5종 |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-80-07/08 | 부품 그룹핑이 같은 자재 사진을 정확히 모음 | 정적 검증 + 실측 폴더 대조 | `BuildPartForSingleCycle` 결과의 `ShotPhotoPaths`/`DatumPhotoPaths` 키 개수를 실제 `D:\Data\Result\<날짜>\` 폴더 스캔 결과와 수동 대조 | ❌ 신규(단위테스트 없음 — 수동 대조 스크립트로 대체) |
| D-80-09 | 기준점 사진 없을 때 알림 + Shot만 적용 | 수동 UAT | 수동 검사 기록(IsProtocolDriven=false)을 리뷰어에서 열어 버튼 클릭 → 알림 확인 | ❌ 신규 |
| D-80-10 | 저장 후 사진경로 원복 | 정적 grep + 실측 | 저장된 INI 를 열어 `SimulImagePath` 등이 원본 값인지 grep | ❌ 신규 |
| D-80-11 | 3개 해제 훅 | 로그 확인 | `[해제]` 버튼/PLC $TEST 수신/레시피변경/종료 4가지 각각 로그 1줄 발생 확인 | ❌ 신규 |
| Local Ref stale 재계산 | Test Find 후 ROI 이동 → RUN → 재계산 로그 | 실측 + 로그 | `[LocalRef]` 로그에 "재계산 성공" 문구, PLC 자동 사이클에서는 발생 0건 | ❌ 신규 |
| 오버레이 겹침 버그 | 여러 Shot 사이클 전체보기 | 육안 | 리뷰어에서 다중-Shot 사이클 열어 전체보기 클릭 → 표시된 사진의 Shot 선만 보임 | ❌ 신규 |

### Sampling Rate
- **Per task commit:** Quick run command(빌드) + CLAUDE.md grep 5종(신규/수정 파일)
- **Per wave merge:** 빌드 + 수동 UAT 시나리오(위 표) 순차 실행
- **Phase gate:** 전체 UAT 시나리오 통과 + 회귀 0(옵션 꺼짐/PLC 자동/반복검사 기존 흐름 무변경) 확인 후 `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `NodeViewModel.cs` 전체(60줄 이후) 재확인 — `Param`/`IsExpanded`/`IsSelected` 정확한 시그니처(A1)
- [ ] `VisionAlgorithmService.TryFitLine` 의 transform 파라미터 의미 재확인(A2)
- [ ] `D:\Data\Result\` 아래 실제 수동(IsProtocolDriven=false) 사이클 폴더 1개 확보 — D-80-09 시나리오 재현용 테스트 데이터
- [ ] 신규 .cs 파일 허용 여부 사용자 확인(Open Question 1)

## Security Domain

이 phase 는 `security_enforcement` 설정이 명시적으로 꺼져 있지 않으므로 섹션을 포함하되, 이 애플리케이션은 인터넷 비연결 산업용 장비 PC 단독 실행 WPF 앱이라 대부분의 ASVS 카테고리가 해당 없음이다.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | 기존 `LoginManager`(로컬 PIN, 이 phase 무변경) |
| V3 Session Management | no | 해당 없음(단일 프로세스, 네트워크 세션 없음) |
| V4 Access Control | no | 해당 없음 |
| V5 Input Validation | 부분 | 리뷰어가 주입하는 사진 경로는 로컬 디스크에 이미 저장된 `cycle.json` 의 경로 문자열(사용자가 직접 입력하는 값이 아님) — 다만 `CaptureImageSaveService.SanitizeFilePart` 가 이미 파일명 생성 시 traversal 문자를 차단하므로, 리뷰어가 "읽기"만 하는 이 경로들도 기존에 안전하게 생성된 값이다. 신규 검증 불필요, 단 `File.Exists` 가드는 기존 관례(`ReviewerImagePathResolver.IsUsableImageFile`)대로 유지. |
| V6 Cryptography | no | 해당 없음 |

### Known Threat Patterns for {stack}

이 phase 의 변경 범위(WPF UI, 로컬 파일 경로 주입, TCP 핸들러의 훅 1줄 추가)에는 STRIDE 상 신규 위협 표면이 없다 — TCP 프로토콜 자체(파싱/직렬화)는 무변경이고, 훅은 기존 이벤트/함수 호출 지점에 로컬 상태 정리 코드만 추가한다.

## Sources

### Primary (HIGH confidence — 코드 직접 확인)
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` — 전체 스냅샷/복원/부품그룹핑 메커니즘(SavedCycleOverrideSnapshot:442, BuildOverrideSnapshot:455, RestoreOverridePathsOnly:495, ApplySavedCyclePart:708, GroupIntoParts:1326, FillPartDatumPhotos:1375, FillPartShotPhotos:1477, FillPartDualPhotos:1558, ValidatePart:1710, SavedCycleRerunPart/Plan:1101/1123)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — HandleRunStartResetResults:448(캐시 skip 조건), HoldManualDatum:1413, TryComposeAlign:3418, TryRunSingleDatum:3559, TryGetDatumTransform:3610, HasCachedDatumTransform:3625, ComputeLocalRefLinesForDatum:3645
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — ProcessOneDatum:295(캐시 skip 실체), InjectLocalRef:1928, TryResolveLocalRef:1947, GrabOrLoadDatumImage:1080, ArchiveDatumImageForCycle:1587, SaveZRangeCandidateImageIfEnabled:2126, IsLiveCaptureMode:1496
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` — ComputeLocalRefLine:494, BuildLocalRefSettingsKey:572, LOCAL_REF_REASON_STALE:34, IsInjectedLocalRefUsable:458
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml`/`.xaml.cs` — DisplayCycle:136, 오버레이 합산:183, border_ngCause 패널:226
- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` — ReviewerImagePathResolver 전체, ResolveCycleImagePath:241
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — BtnTestFindDatum_Click:4475, AskTestImageSource:4636, LoadAndDisplay:1605, LocalRef ROI 배선(BuildPointRoiDefinitions:428 등)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — SetSelectionChange:840, TreeListBox_SelectionChanged 재진입 경고:880
- `WPF_Example/UI/ControlItem/NodeViewModel.cs` — Children/LoadChildren:27-48(1-60줄만 확인)
- `WPF_Example/MainWindow.xaml.cs` — SaveRecipe:292, PopupView(Reviewer):401, Window_Closing:452
- `WPF_Example/Custom/SystemHandler.cs` — MainRun:73, ForceOfflineInspectModeOffForAutoTest:296, ProcessTest:319
- `WPF_Example/Sequence/SequenceHandler.cs` — OnRecipeChanged 이벤트:46, 발화 지점:163
- `WPF_Example/Utility/CaptureImageSaveService.cs` — ResolveOriginImageExtension:411, BuildDatumFileName:399, BuildZRangeCandidateFileName:429
- `.planning/phases/79-side-local-strip-datum/79-RESEARCH.md`/`79-CONTEXT.md` — Phase 79 확정 설계(이미 배포된 상태로 확인)
- `.planning/STATE.md` (tail) — Phase 79 완료 기록(1.7.50.0), Phase 80 컨텍스트 수집 완료 기록

### Secondary (MEDIUM confidence)
- 없음(전부 1차 코드 확인)

### Tertiary (LOW confidence)
- `NodeViewModel.cs` 의 `Param`/`IsSelected`/`IsExpanded` 정확한 선언(60줄 이후 미열람) — Assumptions Log A1

## Metadata

**Confidence breakdown:**
- 부품 그룹핑/스냅샷 재사용 설계: HIGH — 기존 코드가 이미 거의 동일한 문제를 풀어놓았고 전부 `public` 접근 가능
- Local Ref stale 재계산: HIGH(원인) / MEDIUM(해결 코드 스케치는 미실행 검증) — 근본 원인(캐시 skip)은 코드로 확정, 재계산 헬퍼는 계획 단계에서 실제 컴파일 검증 필요
- 트리 프로그램적 선택: MEDIUM — 패턴은 확실하나 `NodeViewModel` 세부 시그니처 미확인(Assumptions A1)
- 오버레이 버그 수정: HIGH — 근본 원인과 수정 코드 모두 명확

**Research date:** 2026-09-18
**Valid until:** 계획·실행 착수 시까지. Phase 79 이후 Local Ref/Datum 관련 추가 quick-fix 가 있었다면(git log 재확인) 이 리서치의 InspectionSequence.cs 라인 번호가 밀렸을 수 있음 — 계획 단계에서 파일을 다시 열어 라인 번호를 재확인할 것.
