# Phase 13: datum-algorithm-extensibility — Context (재정의)

**Gathered:** 2026-04-23 (최초), **Rewritten:** 2026-04-24
**Status:** Ready for planning

## Scope Change History

원 Phase 13 scope(Strategy 패턴 리팩터 + CircleAndLine 알고리즘 추가)는 Phase 12(datum-circle-vertical-horizontal-intersection)에서 **이미 달성**됨:
- Phase 12-01: EDatumAlgorithm enum + DatumConfig.AlgorithmType (string 직렬화 + enum helper)
- Phase 12-02: DatumFindingService 3-way switch 디스패치 + CircleTwoHorizontal + VerticalTwoHorizontal
- Phase 12-03: MainView GetFirstStep/GetNextStep 가변 단계 흐름 + InspectionListView Datum 노드 활성화 + HalconDisplayService 알고리즘별 오버레이

따라서 Phase 13는 **Phase 12에서 이월된 UX 빈틈 3건**으로 재정의한다. Strategy 패턴 리팩터(DatumAlgorithmBase 추상화)는 현재 알고리즘 3개 switch 디스패치 가독성이 충분하므로 YAGNI 기준으로 Deferred.

<domain>
## Phase Boundary

Phase 12의 Datum 티칭 3-way UI 위에서 발견된 **사용성 빈틈 3건**을 메운다:

1. **Gap-1 — TeachDatum ROI 사후 이동/편집**: 티칭 완료된 ROI를 마우스 드래그로 재조정하면 즉시 재티칭이 실행되어 오버레이(십자/원)가 새 위치를 반영해야 한다.
2. **Gap-4 — 런타임 TryFindDatum 테스트 UI**: 티칭된 Datum을 다른 이미지에 적용(TryFindDatum)하여 실제 런타임 동작을 검증할 수 있는 UI가 필요하다.
3. **Req 5d — 방향 정합성 검사**: CircleTwoHorizontal/VerticalTwoHorizontal 티칭 시 수평선/수직선 각도가 말이 되지 않으면 티칭 실패로 판정한다.

**구체적으로 Phase 13가 추가하는 것:**
1. MainResultViewerControl의 RoiMoveCompleted 이벤트를 TeachDatum 컨텍스트에서도 처리 → DatumConfig ROI 필드 write-back + 자동 TryTeachDatum 재호출
2. `btn_testFindDatum` 버튼 + LoadImage 다이얼로그 선택지 + 결과 오버레이(검출 RefOrigin 십자 + 좌표 숫자)
3. DatumFindingService.TryTeachCircleTwoHorizontal + TryTeachVerticalTwoHorizontal에 phi 각도 검증 게이트 추가

**범위 외 (Deferred):**
- DatumAlgorithmBase 추상 클래스 추출(Strategy 리팩터) — 현재 3개 알고리즘 switch 디스패치로 충분, 4번째 알고리즘 실제 요구 시 별도 phase
- 런타임 TryFindDatum 실패 시 단계별(원 검출/에지 피팅) 디버그 오버레이 — Phase 13에서는 에러 메시지만
- CircleTwoHorizontal 방향 임계각 사용자 설정 필드 — 고정값으로 시작, 튜닝 필요해지면 DatumConfig에 추가
- TemplateMatch / 3점 Datum 등 4번째 이상 알고리즘

</domain>

<decisions>
## Implementation Decisions

### Gap-1: TeachDatum ROI 사후 이동/편집 (D-01 ~ D-04)

- **D-01 (1-a): 편집 진입 트리거 = hit-test 기반 자동 선택** — Quick 260423-o53에서 이미 MainResultViewerControl에 구현된 `RoiMoveCompleted` 이벤트 패턴을 그대로 사용. 사용자가 그려진 ROI를 클릭하면 hit-test로 선택, 드래그하면 이동. 별도 "편집 모드" 진입 UI 없음.

- **D-02 (1-b): 편집 대상 범위 = 티칭 완료 후 ROI 전체 (AlgorithmType별로 Rect/Circle 모두)** — TeachDatum 모드 중이 아니어도 이동 가능. DatumConfig가 현재 선택된 Datum 노드에 해당하면 hit-test가 해당 노드의 AlgorithmType별 ROI 목록을 대상으로 시행:
  - TwoLineIntersect: Line1 rect, Line2 rect
  - CircleTwoHorizontal: CircleROI circle, Horizontal_A rect, Horizontal_B rect
  - VerticalTwoHorizontal: Line1 rect(=수직), Horizontal_A rect, Horizontal_B rect

- **D-03 (1-c): 이동 후 자동 재티칭 = 즉시 실행** — ROI 드래그 MouseUp 시점에 `DatumFindingService.TryTeachDatum`을 **자동 재호출**. 실패 시 기존 `LastTeachSucceeded = false` 경로로 라벨+오버레이 갱신. 성공 시 새 RefOrigin/십자/검출 원이 즉시 반영. 여러 ROI 연속 조정 시에도 매 이동마다 재계산 (계산 부담보다 UX 일관성 우선 — 사용자 선택).

- **D-04 (1-d): 기존 코드 재사용 = MainResultViewerControl 패턴 이식, 베이스 클래스 추출 없음** — halconViewer(= MainResultViewerControl 인스턴스)에 이미 있는 `RoiMoveCompleted` 이벤트를 MainView.xaml.cs에서 listen. Datum 컨텍스트 판단(현재 선택된 노드가 DatumConfig인지)은 MainView의 기존 `_editingDatum` 참조로 처리. MainResultViewerControl 측 hit-test가 Datum ROI 목록을 어떻게 알지는 MainView에서 selection-aware 힌트(예: `SetSelectedRoiCandidates(List<RoiHit>)` 같은 API) 추가 또는 hit-test 로직 확장 — 방식은 planner 재량(D-02 범위와 충돌 없도록).

### Gap-4: 런타임 TryFindDatum 테스트 UI (D-05 ~ D-08)

- **D-05 (4-a): 진입점 = `btn_testFindDatum` 버튼 추가** — MainView canvas 툴바의 `btn_teachDatum` 옆에 배치. Datum 노드가 선택되고 `IsConfigured && LastTeachSucceeded`일 때만 활성화(hit-test처럼 세부는 planner 재량). 라벨: "Datum Find 테스트".

- **D-06 (4-b): 테스트 이미지 소스 = 현재 Grab 이미지 + LoadImage 다이얼로그 둘 다** — `btn_testFindDatum` 클릭 시 2단계:
  1. 현재 halconViewer에 이미지가 있는지 확인
  2. 사용자 선택: `(a) 현재 이미지로 테스트` / `(b) 다른 파일 선택…` — Ookii.Dialogs.Wpf OpenFileDialog로 png/bmp/jpg 필터. 로드 후 `HImage` 생성(기존 LoadImage 경로 재사용).
  선택 UX 구현 방식(다이얼로그 vs 콤보박스 vs 키보드 단축키)은 planner 재량.

- **D-07 (4-c): 결과 표시 = 오버레이 십자 + 좌표 숫자 둘 다** — 성공 시:
  - 검출된 RefOrigin에 빨간 20px 십자 렌더 (기존 teach 성공 오버레이와 시각적으로 구분되도록 색상/굵기 달리 — planner 재량, 예: 주황색 또는 점선)
  - label_testFindResult (신규) 또는 메인 상태 라벨에 숫자 표시: `"TryFind OK — RefOrigin = (Row: 123.4, Col: 567.8), Angle: -0.023 rad"`
  - 실패 시: 빨간 텍스트 에러 메시지 (D-08 참조)

- **D-08 (4-d): 실패 시 = 에러 메시지만** — TryFindDatum이 false 반환 시 `label_testFindResult`에 빨간색으로 `TryFindDatum`의 out error 문자열을 그대로 표시. 원 검출 실패/에지 피팅 실패 등 단계별 디버그 오버레이는 Deferred. 에러 문자열은 TryFindDatum 내부 Halcon try/catch가 이미 구분된 메시지를 뱉도록 구현되어 있어야 함(없으면 planner가 보강).

### Req 5d: 방향 정합성 검사 (D-09 ~ D-12)

- **D-09 (5-a): 판정 기준 = 두 축 체크 (수평 허용각 + 수직 교차각)** — CircleTwoHorizontal/VerticalTwoHorizontal의 TryTeach 말미에 두 조건 모두 검사:
  1. **수평 ROI 결과(Line2Detected_* concat line) phi ∈ [-15°, +15°]** — 수평에서 15도 이상 벗어나면 fail
  2. **수평선 phi와 수직 가상선(CircleTwoHorizontal의 수직 X=center, VerticalTwoHorizontal의 Line1Detected_*) 사이 각도 ∈ [85°, 95°]** — 직각에서 5도 이상 벗어나면 fail
  두 조건 중 하나라도 실패하면 `LastTeachSucceeded = false` + error 반환.

- **D-10 (5-b): 임계 각도 = 고정값 (소스 상수)** — `MIN_HORIZONTAL_EDGES` 상수처럼 `DatumFindingService.cs` 내부 `const double HORIZONTAL_TOLERANCE_DEG = 15.0; const double PERPENDICULAR_TOLERANCE_DEG = 5.0;` 선언. 향후 사용자 튜닝 필요해지면 DatumConfig 필드화(Deferred).

- **D-11 (5-c): 실패 시 처리 = 티칭 실패 (기존 에러 패턴)** — 기존 `"Horizontal line fit failed: …"` 같은 에러 메시지 패턴 따라 `"Horizontal line orientation out of range: phi=XX.X° (expected ±15°)"` / `"Horizontal/Vertical perpendicularity violated: angle=XX.X° (expected 90°±5°)"` 형식. TryTeach false 반환 + 기존 실패 UI 경로(빨간색 라벨) 그대로.

- **D-12 (5-d): 검사 적용 범위 = CircleTwoHorizontal + VerticalTwoHorizontal** — 두 알고리즘 모두 수평 ROI 2개를 사용하므로 동일 검증 로직 적용. 한 private helper(예: `ValidateHorizontalVerticalAngles(double horizPhi, double vertPhi, out string error)`)로 추출해서 중복 제거. TwoLineIntersect는 기존대로 각도 검증 없음(무효).

### 공통 (D-13)

- **D-13: Phase 12 기 구현과의 관계 유지** — Phase 13 수정은 모두 **추가(additive)**:
  - DatumConfig 공개 시그니처 변경 없음
  - DatumFindingService public 메서드 시그니처 변경 없음 (TryTeachDatum/TryFindDatum)
  - MainView의 기존 btn_teachDatum 흐름 변경 없음
  - HalconDisplayService.RenderDatumOverlay 기존 출력 변경 없음
  Gap-1/Gap-4는 새 이벤트 핸들러/버튼 추가, Req 5d는 TryTeach 내부 validation 게이트 추가로만 영향.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 13 주요 수정 대상

**기존 파일 (확장 대상):**
- `WPF_Example/UI/ContentItem/MainView.xaml` — `btn_testFindDatum` ToggleButton/Button 추가 (canvas 툴바), `label_testFindResult` (옵션) 추가
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — RoiMoveCompleted 핸들러에 TeachDatum 분기(D-01..D-04), btn_testFindDatum_Click 핸들러 + LoadImage 다이얼로그 경로(D-05..D-08)
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — hit-test가 Datum ROI 후보도 인식하도록 확장 (MainView가 selection hint를 주입하는 API 또는 판단 로직 분기 — D-02/D-04)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — TryTeachCircleTwoHorizontal / TryTeachVerticalTwoHorizontal 말미에 ValidateHorizontalVerticalAngles private helper 호출 추가(D-09..D-12). TryFindDatum은 기존 구현 유지(Phase 12 SPEC Out-of-scope).
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — Gap-4 테스트 Find 결과 오버레이 렌더 메서드 추가 (RenderDatumOverlay와 구분되는 RenderDatumFindResult 또는 기존 메서드 파라미터 확장 — planner 재량)

**신규 파일 (선택적):**
- planner 판단에 따라 hit-test 헬퍼/테스트 find 결과 DTO를 별도 파일로 둘 수 있음. 필수 아님.

### Phase 13 재사용 (수정 없음)

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — Phase 12 스키마 유지 (AlgorithmType/CircleROI_*/Horizontal_A_*/Horizontal_B_*/Line*Detected_*/IsConfigured/LastTeachSucceeded). Phase 13는 필드 추가 없음.
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — Phase 12 enum 유지.
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — Phase 12 Datum 노드 활성화 유지. Phase 13는 추가 변경 없음 예상(필요 시 btn_testFindDatum 활성화 게이트 조정 수준).

### Upstream context

- `.planning/phases/12-datum-circle-vertical-horizontal-intersection/12-SPEC.md` — Req 5d 원문 (방향 정합성).
- `.planning/phases/12-datum-circle-vertical-horizontal-intersection/12-01-SUMMARY.md` / `12-02-SUMMARY.md` / `12-03-SUMMARY.md` — Phase 12 확정 구현 내역.
- `.planning/phases/11-datum-teaching-ui-roi/11-CONTEXT.md` — Datum 티칭 UI 기반 설계.
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` L40/L113/L116/L287/L917/L942 — RoiMoveCompleted + CircleDrawingCompleted + hit-test 패턴 (Quick 260423-o53).

</canonical_refs>

<code_context>
## Existing Code Insights

### Phase 12 완료 시점 사실(Phase 13 시작 시점)

- `DatumFindingService.TryTeachDatum(HImage, DatumConfig, out string)` — switch(config.AlgorithmTypeEnum) 디스패치. TwoLineIntersect는 private TryTeachTwoLineIntersect 위임, Circle/VerticalTwoHorizontal는 공용 수평 ROI concat (GenContourPolygonXld×2 → ConcatObj → FitLineContourXld tukey) 패턴. MIN_HORIZONTAL_EDGES=10.
- `MainView.xaml.cs` — `ECanvasMode.TeachDatum`, `EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }`, `GetFirstStep`/`GetNextStep` switch. `HalconViewer_DatumRectCompleted` / `HalconViewer_DatumCircleCompleted` 가 DatumConfig 필드 write-back 담당.
- `halconViewer` (MainView.xaml:131) = `MainResultViewerControl`. StartRectangleDrawing, StartCircleDrawing, RoiMoveCompleted, CircleDrawingCompleted 모두 존재. MouseDown hit-test 후 MouseMove 드래그 → MouseUp 시 delta 계산 → RoiMoveCompleted(fxArgs) raise.
- `MainResultViewerControl.RoiMoveCompleted` 이벤트(L116)는 현재 **FAIConfig 전제**로 MainView에서 처리(Quick 260423-o53). Phase 13에서는 DatumConfig 컨텍스트(`_editingDatum != null`) 분기를 추가.
- `HalconDisplayService.RenderDatumOverlay` — TwoLineIntersect/CircleTwoHorizontal/VerticalTwoHorizontal 분기별 오버레이 + yellow ROI 라벨(L1/L2/Circle/H-A/H-B/Vert) 완비. Phase 13는 Find 결과 오버레이만 신설.

### Req 5d 검증 로직 참고

`DatumFindingService.TryTeachCircleTwoHorizontal` 말미에 이미 수평 2-ROI concat line(Line2Detected_* 에 write-back됨)과 수직 가상선(CircleCenter_Col 기반 X=const 수직)이 계산되어 있음. Line1Detected_RBegin/CBegin/REnd/CEnd (VerticalTwoHorizontal에서는 수직 ROI 검출 결과)와 수평 concat line의 phi 차이로 각도 검증 가능. 기존 에러 문자열 패턴 예: `"Horizontal line fit failed: insufficient edges (0)"`.

### Gap-4 LoadImage 경로 참고

기존 MainView에 LoadImage 버튼/경로가 있을 경우 재사용 권장. `Ookii.Dialogs.Wpf.VistaOpenFileDialog` 또는 표준 `OpenFileDialog` 둘 다 프로젝트에서 사용 중(UI/Device/DeviceSelector.xaml.cs 참고). HImage 로드는 `HOperatorSet.ReadImage`. 성공 시 halconViewer.ShowHalconImage(또는 동등 메서드)로 표시 후 TryFindDatum 호출.

### C# 7.2 / .NET 4.8 제약 (Phase 12와 동일)

- nullable reference types, switch expressions, record 불가
- out parameter, Tuple, ValueTuple 사용 가능
- 각도 계산: Math.Atan2, Math.Abs, Math.PI. 라디안↔도 변환 상수 추출 권장.

### 빌드/검증

- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`
- SIMUL_MODE 시나리오:
  - Gap-1: TwoLineIntersect 티칭 후 Line1 rect 드래그 이동 → 빨간 십자 즉시 따라 움직이는지 / CircleTwoHorizontal 티칭 후 CircleROI 드래그 → yellow 검출 원 + 십자 이동
  - Gap-4: Teach 완료 후 btn_testFindDatum → 현재 이미지로 Find 성공 시 주황 십자 + 좌표 라벨 표시 / LoadImage로 다른 이미지 열어 재검증
  - Req 5d: CircleTwoHorizontal 티칭 시 H-A/H-B를 일부러 비스듬히(20°+) 그리면 "orientation out of range" 에러 발생 / 수직 가상선과 엇나가는 수평선으로 perpendicularity 실패 재현

### 주석 convention

- `//260424 hbk Phase 13 D-XX — <intent>` 모든 신규/수정 라인

</code_context>

<specifics>
## Specific Ideas (planner용 스케치)

### Gap-1 핸들러 skeleton

```csharp
// MainView.xaml.cs — 기존 RoiMoveCompleted 핸들러에 Datum 분기 추가
private void HalconViewer_RoiMoveCompleted(object sender, RoiMoveCompletedArgs e)
{
    //260424 hbk Phase 13 D-01..D-04 — TeachDatum 컨텍스트 ROI 이동 처리
    if (_editingDatum != null)
    {
        // e.RoiKey (예: "Datum.Line1" / "Datum.Circle" / "Datum.HorizontalA" 등)로 어느 ROI인지 판정
        // DatumConfig 해당 필드에 delta 반영 (Circle이면 CenterRow/Col, Rect면 Row/Col/Phi/Length)
        ApplyDatumRoiDelta(_editingDatum, e);

        // D-03: 자동 재티칭
        InvokeTryTeachDatum();  // 기존 Phase 12 경로 재사용
        return;
    }
    // 기존 FAI 경로
    HalconViewer_RoiMoveCompleted_Fai(sender, e);
}
```

### Gap-4 btn_testFindDatum 핸들러 skeleton

```csharp
// MainView.xaml.cs — 신규 핸들러
private void BtnTestFindDatum_Click(object sender, RoutedEventArgs e)
{
    //260424 hbk Phase 13 D-05..D-08 — 런타임 TryFindDatum 테스트
    var datum = _editingDatum ?? InspectionListView.SelectedDatum;
    if (datum == null || !datum.IsConfigured || !datum.LastTeachSucceeded)
    {
        ShowWarning("Datum 티칭이 완료된 후 테스트 가능합니다.");
        return;
    }

    HImage testImage = AskTestImageSource();  // 현재 halconViewer 이미지 또는 OpenFileDialog
    if (testImage == null) return;

    var service = new DatumFindingService();
    if (service.TryFindDatum(testImage, datum, out string error))
    {
        // D-07: 주황 십자 + 좌표 라벨
        RenderDatumFindResult(testImage, datum);
        label_testFindResult.Text =
            $"TryFind OK — RefOrigin=({datum.RefOriginRow:F1}, {datum.RefOriginCol:F1}), " +
            $"Angle={datum.RefAngleRad:F3} rad";
        label_testFindResult.Foreground = Brushes.LimeGreen;
    }
    else
    {
        label_testFindResult.Text = $"TryFind FAIL — {error}";
        label_testFindResult.Foreground = Brushes.Red;
    }
}
```

### Req 5d 검증 helper skeleton

```csharp
// DatumFindingService.cs — 내부 helper
private const double HORIZONTAL_TOLERANCE_DEG = 15.0;
private const double PERPENDICULAR_TOLERANCE_DEG = 5.0;

private static bool ValidateHorizontalVerticalAngles(
    double horizPhiRad, double vertPhiRad, out string error)
{
    //260424 hbk Phase 13 D-09..D-12 — 방향 정합성 게이트
    double horizDeg = Math.Abs(horizPhiRad * 180.0 / Math.PI);
    if (horizDeg > 90.0) horizDeg = 180.0 - horizDeg;  // [0, 90] normalize
    if (horizDeg > HORIZONTAL_TOLERANCE_DEG)
    {
        error = $"Horizontal line orientation out of range: " +
                $"{(horizPhiRad*180.0/Math.PI):F1}° (expected ±{HORIZONTAL_TOLERANCE_DEG}°)";
        return false;
    }
    double deltaDeg = Math.Abs((horizPhiRad - vertPhiRad) * 180.0 / Math.PI);
    while (deltaDeg >= 180.0) deltaDeg -= 180.0;
    double perpErr = Math.Abs(deltaDeg - 90.0);
    if (perpErr > PERPENDICULAR_TOLERANCE_DEG)
    {
        error = $"Horizontal/Vertical perpendicularity violated: " +
                $"delta={deltaDeg:F1}° (expected 90°±{PERPENDICULAR_TOLERANCE_DEG}°)";
        return false;
    }
    error = "";
    return true;
}
```

### 예상 Plan 분할

- **Plan 01 — Req 5d 방향 정합성 검사** (가장 작고 국소적, 순수 알고리즘 레이어)
  ValidateHorizontalVerticalAngles 추가 + CircleTwoHorizontal/VerticalTwoHorizontal 호출부 연결. 빌드 + SIMUL_MODE 비스듬한 ROI 실패 재현이 완료 조건.

- **Plan 02 — Gap-4 런타임 TryFindDatum 테스트 UI** (UI + LoadImage 다이얼로그)
  btn_testFindDatum 추가, AskTestImageSource, HalconDisplayService.RenderDatumFindResult, label_testFindResult. SIMUL_MODE 현재 이미지/파일 로드 2경로 검증이 완료 조건.

- **Plan 03 — Gap-1 TeachDatum ROI 이동 + 자동 재티칭** (가장 복잡, MainResultViewerControl hit-test 확장)
  RoiMoveCompleted Datum 분기 + MainResultViewerControl selection-aware hit-test + ApplyDatumRoiDelta + InvokeTryTeachDatum 자동 실행. SIMUL_MODE 3 algorithm × ROI 종류별 이동 회귀 없음 확인이 완료 조건.

Plan 01 → Plan 02 → Plan 03 순서(각 Plan 독립, 직렬 실행). Plan 02/03 병렬도 가능하나 Plan 03의 RoiMoveCompleted 핸들러가 Plan 02의 btn_testFindDatum 핸들러와 동일 파일(MainView.xaml.cs)을 건드려서 병합 충돌 우려 → 직렬 권장.

</specifics>

<deferred>
## Deferred Ideas

- **DatumAlgorithmBase Strategy 리팩터** — 현재 3개 switch 디스패치 가독성 충분. 4번째 알고리즘(TemplateMatch 등) 실제 요구 시 별도 phase.
- **런타임 TryFindDatum 실패 시 단계별 디버그 오버레이** — 원 검출 실패/에지 피팅 실패 각각 시각화. Phase 13는 에러 메시지만.
- **방향 정합성 임계각 사용자 설정** — HORIZONTAL_TOLERANCE_DEG/PERPENDICULAR_TOLERANCE_DEG를 DatumConfig 필드화. 튜닝 필요해지면 추가.
- **TeachDatum ROI 이동 시 여러 ROI 배치 편집** — 현재 설계는 ROI 하나 이동 = 즉시 재티칭. 다중 선택 이동은 Phase 13에서 미지원.
- **btn_testFindDatum의 연속 배치 테스트(여러 이미지 폴더 배치)** — 단일 이미지 테스트만. 배치 검증은 별도 도구 phase.
- **TemplateMatch / 3점 기반 Datum** — 4번째 이상 알고리즘.

</deferred>

---

*Phase: 13-datum-algorithm-extensibility (재정의: Datum UX 이월 3건)*
*Context rewritten: 2026-04-24*
