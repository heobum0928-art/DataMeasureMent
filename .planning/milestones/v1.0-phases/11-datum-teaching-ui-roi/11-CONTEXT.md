# Phase 11: datum-teaching-ui-roi - Context

**Gathered:** 2026-04-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 6까지 완성된 Sequence > Datum + Shot > FAI > Measurement 트리 구조 위에서, `bugs.md`에 등록된 세 결함(WR-RT-01/03/04)을 공통 캔버스·ROI·티칭 인프라 위에서 한 번에 해소한다. 초점은 **"Datum을 실사용 가능한 UI로 완성하고 Circle ROI를 지원하여 RC-03 CircleDiameterMeasurement를 현실 운용 가능하게 만드는 것"** 이다.

구체적으로 Phase 11이 다루는 세 가지 결함:
1. **WR-RT-03 (🔴 Blocker):** `DatumFindingService.TryTeachDatum`에 UI 호출자가 0건이어서 Datum 티칭 워크플로가 전면 차단됨. 사용자는 PropertyGrid에 Line1/Line2 좌표·RefOrigin·RefAngle을 수동 숫자 입력해야 함(비현실적). Datum 노드 선택 시 이미지 로드조차 이루어지지 않음.
2. **WR-RT-01 (🟡 Warning):** RoiDefinition이 Rect/Polygon만 지원 → CircleDiameterMeasurement가 사용하는 Circle ROI를 캔버스에서 그릴 수 없음. RC-03 기능이 UI 상에서 실사용 불가.
3. **WR-RT-04 (🟡 Warning):** 티칭 워크플로 순서(Datum 티칭 → ROI 그리기 → 알고리즘/파라미터/테스트 → 저장)가 UI 상에서 안내되지 않음 → 사용자 임의 순서 진행 시 혼란/미저장 상태로 진행 가능.

**범위 외:**
- 5종 나머지 MeasurementBase 파생 클래스(PointToLine/PointToPoint/LineToLineAngle/LineToLineDistance 등)의 overlay 시각화 보강 → Phase 7 deferred 후속(별도 phase).
- DatumConfig 클래스 구조 자체의 대규모 변경(Line 2개 기반 Datum 패턴 → 원/3점 등 다른 패턴) → Phase 4 D-05 전제 유지.
- 조명 하드웨어 실제 제어 → Phase 6 D-12 유지.
- 데이터 마이그레이션(기존 Phase 1~5 INI 포맷) → Phase 6 D-22 유지.
- 티칭 Wizard UI(step-by-step 모달) → 기존 트리+PropertyGrid 중심 워크플로와 이질적이라 채택하지 않음.

</domain>

<decisions>
## Implementation Decisions

### Datum 티칭 UI 진입/버튼 구조 (WR-RT-03)
- **D-01:** 캔버스 상단 툴바에 **전용 `Teach Datum` 토글 버튼** 신설(기존 Rect/Polygon/Calibrate 버튼 옆). Datum 노드 선택 상태에서만 활성화, 다른 노드 선택 시 비활성화. Phase 4 D-12의 "기존 Rect ROI 재사용" 원안은 의도/실동작 혼동 가능성 때문에 폐기하고 전용 버튼으로 대체한다.
- **D-02:** Teach Datum 버튼 진입 후 시퀀스: ① Line1 ROI 드래그 지정(기존 Rect 드로잉 재사용) → ② `label_drawHint`에 "Line2 ROI를 지정하세요" 안내 → ③ Line2 ROI 드래그 지정 → ④ **두 번째 ROI 확정(MouseUp) 순간 자동으로 `DatumFindingService.TryTeachDatum` 호출**. 별도 Teach 실행 버튼 없음. 단계 수 최소화.
- **D-03:** 진행 상태는 `ECanvasMode`에 `TeachDatum` 값을 추가하여 관리(기존 `None/RectRoi/PolygonRoi/Calibration` 패턴 확장). 내부 부상태(`EDatumTeachStep { Line1, Line2, Done }`)로 현재 단계 구분.
- **D-04:** 재티칭 흐름: Datum이 이미 `IsConfigured=true`인 상태에서 Teach Datum 버튼을 다시 누르면 `CustomMessageBox` 확인("기존 Datum을 덮어씁니다. 계속?") → 승인 시 기존 Line1/Line2 ROI 필드를 0으로 초기화하고 Line1부터 다시 시작. 개별 Line만 수정하는 경로는 제공하지 않음(YAGNI).
- **D-05:** 티칭 성공 시 `DatumConfig.IsConfigured = true`는 `TryTeachDatum` 내부에서 이미 설정됨(기존 구현 유지). UI 측은 성공/실패 플래그만 받아 오버레이 갱신 트리거.

### Datum 티칭 이미지 소스 (WR-RT-03 연계)
- **D-06:** Datum 노드 선택 시 기존 FAI/Shot 선택과 동일하게 `button_grab.IsEnabled = true`, `button_loadImage.IsEnabled = true` 활성화하여 사용자가 이미지를 획득할 수 있게 한다. 현재 InspectionListView.xaml.cs:258에서 `ICameraParam` 검사로만 활성화하므로 Datum 노드(`ENodeType.Datum`) 케이스를 추가한다.
- **D-07:** Grab 실행 시 `DatumConfig.ImageSourceMode` 분기:
  - `Dedicated`(기본) → 해당 Fixture(Sequence)의 첫 번째 Shot과 같은 카메라로 SIMUL/실촬영 수행. Datum 전용 Z축 이동은 Phase 11 범위 외(Phase 6 D-07의 "Datum 전용 캡처 조건 별도 보유"는 이번 Phase에서 간소화 — Z/조명/노출은 `SourceShotName`에서 상속).
  - `ReuseFromShot` → 지정된 Shot의 **마지막 Grab 결과 이미지** 재사용(메모리 상 `ShotConfig` 이미지 필드). 신규 Grab 없음.
- **D-08:** **`DatumConfig.SourceShotName` string 필드 신설.** `Dedicated` 모드에서도 어느 Shot의 카메라/Z/조명을 상속할지 명시. PropertyGrid에서 드롭다운(같은 Sequence 내 Shot 이름 목록). `ReuseFromShot`에서는 이 Shot의 기존 이미지를 가져옴. 기본값은 Sequence의 첫 번째 Shot 이름. 기존 INI 하위호환: 미존재 시 자동으로 첫 Shot 이름 채움.
- **D-09:** SIMUL_MODE 이미지 경로는 **기존 Phase 5 Plan 01에서 도입된 `SimulImagePath`를 재사용**한다. `SourceShotName`이 가리키는 Shot의 `SimulImagePath`를 읽어 로드. DatumConfig에 별도 SimulImagePath 필드는 만들지 않는다(필드 중복 회피).
- **D-10:** `ImageSourceMode`가 `ReuseFromShot`인데 지정 Shot이 아직 Grab되지 않은 상태라면: label_drawHint에 "Shot '{name}'을 먼저 Grab하세요" 인라인 에러 표시, 티칭 모드 자동 종료.

### Datum 티칭 결과 피드백 (WR-RT-03 연계)
- **D-11:** TryTeachDatum 성공 시 캔버스 오버레이:
  - 검출된 **Line1 실제 직선**(노란색) 및 **Line2 실제 직선**(청록색)을 HalconDisplayService를 통해 렌더링. `TryFindLine` 내부에서 구한 (lr1,lc1)-(lr2,lc2) 쌍 2개를 DatumConfig의 휘발성 필드에 저장하여 UI에서 읽는다.
  - **교점 + 십자 마커**를 RefOriginRow/Col 위치에 그림(빨강, 기존 Datum 오버레이 색상 체계 유지).
  - 기존 `SetDatumOverlay(DatumConfig, bool)` 호출 경로는 유지하되, `MainResultViewerControl.RenderDatumOverlay`의 구현을 확장하여 위 두 항목을 그린다.
- **D-12:** TryTeachDatum 실패 시: `label_drawHint`에 빨간색 텍스트로 에러 메시지("Line1 검출 실패: insufficient edge points" 등, `out string error` 값 그대로 표시). ROI 사각형은 유지되어 사용자가 위치 조정 후 Teach Datum 버튼을 다시 누를 수 있게 한다. CustomMessageBox 모달은 사용하지 않는다(연속 튜닝 방해).
- **D-13:** 성공 후 INI 저장: **기존 Recipe Save 메뉴/버튼 필로우를 따른다.** TryTeachDatum true가 되어도 메모리 상 DatumConfig에만 반영되고, 사용자가 기존 Recipe Save를 실행해야 INI 영구 저장. FAI CRUD와 동일한 패턴으로 일관성 유지. Auto-save / dirty-flag / 종료 시 확인은 도입하지 않는다.

### Circle ROI 드로잉/저장 모델 (WR-RT-01)
- **D-14:** Circle ROI 드로잉 방식: **중심 클릭 + 드래그로 반지름 지정**. 첫 MouseDown=중심 고정, 이후 MouseMove=미리보기(반지름 계산), MouseUp=확정. 기존 `StartRectangleDrawing`과 대응되는 `StartCircleDrawing` 경로를 `HalconViewerControl`에 추가한다.
- **D-15:** `MainView.ECanvasMode`에 `CircleRoi` 값 추가. 캔버스 상단 툴바에 `btn_circleRoi` 토글 버튼 신설(`btn_rectRoi`/`btn_polygonRoi` 옆). FAI 선택 상태에서만 활성화(기존 Rect/Polygon과 동일 조건).
- **D-16:** `RoiDefinition`에 **`RoiShape Shape { Rect, Polygon, Circle }` enum과 `CenterRow/CenterCol/Radius` double 필드 신설**. 기존 `Row1/Col1/Row2/Col2`는 `Shape == Rect`에서만 의미, `PolygonPoints`는 `Shape == Polygon`, 신규 Center/Radius는 `Shape == Circle`. 기존 INI의 Rect만 있는 레시피는 `Shape` 미존재 시 기본값 Rect로 해석하여 하위 호환 유지.
- **D-17:** **CircleDiameterMeasurement 내부 필드(`Circle_Row/Col/Radius`)는 그대로 유지**. 캔버스 드로잉 결과를 `MainView.CommitCircleRoi()`에서 해당 Measurement의 `Circle_Row/Col/Radius`로 직접 기록한다(FAIConfig가 아니라 선택된 Measurement에 기록). `RoiDefinition`의 Circle 필드는 **캔버스 렌더링/편집의 중간 표현**으로만 사용하며, Measurement는 자체 필드로 Halcon 호출.
- **D-18:** Circle ROI 적용 범위는 **CircleDiameterMeasurement 한정**. PointToLine 등 다른 Measurement의 Roi는 Rect 유지(범위 확장 시 별도 phase). Datum Line ROI는 **Rect(Line ROI)만** 사용 — Phase 4/6의 "Line 2개 교점" 계약을 유지하고 원형 Datum 패턴은 deferred.
- **D-19:** `HalconDisplayService`의 ROI 렌더링 분기에 `Shape == Circle` 케이스 추가(Halcon `disp_circle`로 그림). `SetSelectedRoi` 하이라이트 로직도 Circle에서 동일 동작하도록 확장.

### 티칭 워크플로 순서 가이드 (WR-RT-04)
- **D-20:** **노드별 상태 배지 + 상태바 힌트** 방식. 하드 블로킹/마법사 UI 모두 채택하지 않는다.
  - Datum 노드 아이콘 색상: `IsConfigured==false` → 빨강(미티칭), `true` → 초록(티칭 완료).
  - FAI 노드 아이콘 색상: `ROI_Length1<=0 || ROI_Length2<=0` → 빨강(ROI 미설정), 설정 완료 → 초록. CircleDiameterMeasurement의 경우 `Circle_Radius<=0` 체크.
  - Measurement 노드: Tolerance 미설정 → 노랑(경고). 실행은 가능하되 수집 목적.
- **D-21:** MainWindow 하단 상태바에 **"다음 단계" 힌트** 표시. 선택된 노드 컨텍스트 기반:
  - Datum 노드 + 미티칭 → "Teach Datum 버튼으로 Line1/Line2를 지정하세요"
  - FAI 노드 + ROI 미설정 → "Rect/Polygon/Circle ROI 버튼으로 검사 영역을 지정하세요"
  - Measurement 노드 + Tolerance 미설정 → "공차 값을 입력하세요 (선택 사항)"
  - 모두 완료 → "Test FAI 또는 Run Inspection으로 측정을 실행하세요"
- **D-22:** **테스트 실행 전 검증 완화:** Datum 미설정(소속 Sequence에 유효한 DatumConfig가 없거나 DatumRef가 존재하지 않는 이름을 참조) + FAI.ROI 미설정 두 가지만 하드 검증. 둘 중 하나라도 부족하면 `CustomMessageBox`로 거부. **Tolerance 미설정은 검증하지 않는다** — 초기 튜닝 단계에서 값을 수집해야 하므로.
- **D-23:** **FAI 단독 테스트 버튼(`Test FAI`)** 신설. InspectionListView 하단에 추가. 동작:
  1. 선택된 FAI의 소속 Shot 이미지가 메모리에 있어야 함(없으면 "Grab 먼저" 에러).
  2. 소속 Sequence에서 DatumRef가 지정한 DatumConfig의 `CurrentTransform` 사용(미설정 시 Identity).
  3. 해당 FAI 내 모든 Measurement의 `TryExecute`를 순차 실행, 결과/오버레이를 현재 캔버스에 표시.
  4. Recipe Save 하지 않음(일회성 리허설).
- **D-24:** Test FAI 버튼은 Datum 티칭 UI와 동일 계열의 튜닝 도구로 배치(같은 툴바 영역). 전체 Sequence 실행 경로(Phase 5)는 변경 없음.

### 결정 로그
- **D-25:** 기존 Phase 4 D-12(Datum 노드 선택 + Rect ROI 버튼 재사용)는 Phase 11에서 **의도적으로 변경**. 사유: 같은 버튼이 노드 타입에 따라 FAI ROI vs Datum Line ROI로 동작 → 사용자 혼동 + 구현 시 모드 충돌. 대신 D-01의 전용 Teach Datum 버튼으로 대체.
- **D-26:** Phase 6 D-07의 "Datum Dedicated 모드는 별도 캡처 조건(Z/조명/노출)" 은 Phase 11에서 **간소화**. Z/조명/노출은 `SourceShotName`이 가리키는 Shot에서 상속(D-07/D-08). Datum 전용 캡처 조건 분리는 deferred.

### Claude's Discretion
- `label_drawHint` 색상/폰트 스타일(에러=빨강/성공=초록 권장)
- Datum 노드 상태 배지의 구체 아이콘 글리프 / XAML DataTrigger 구현 위치(NodeViewModel vs DataTemplate)
- Circle ROI 드로잉 중 미리보기 원 색상 (기존 Rect draft 색상 체계 재사용 권장)
- `RoiShape` enum 파일 위치(`WPF_Example/Halcon/Models/` 하위 신규 파일 vs `RoiDefinition.cs`에 내부 enum)
- `StartCircleDrawing` 이벤트 시그니처(`CircleDrawingCompleted` 신규 이벤트 필요 여부)
- Test FAI 버튼의 XAML 배치(InspectionListView 하단 vs 캔버스 툴바)
- Datum 검출 직선 오버레이의 구체 Halcon 렌더링(disp_line + disp_cross 조합 등)

### Folded Todos
- 없음 (해당 phase 맞춤형 todo 미수집)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 11 주요 수정 대상 (UI)
- `WPF_Example/UI/ContentItem/MainView.xaml` — 캔버스 상단 툴바 (btn_teachDatum / btn_circleRoi 신설 위치).
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `ECanvasMode` 확장(TeachDatum, CircleRoi), `_canvasMode` 분기 추가, `CommitCircleRoi`, `OnDatumTeachCompleted` 핸들러 신설.
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — `StartCircleDrawing`/`CommitActiveCircle`/`CircleDrawingCompleted` 신규 API 추가. Circle 드래그 상태 필드(`_circleDraftCenter`, `_circleDraftRadius`) 추가.
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — `RenderDatumOverlay` 구현 확장(검출 라인 2개 + 교점 십자), Circle 렌더링 경로 추가. 현재 SetDatumOverlay/ClearDatumOverlay는 L222-239.
- `WPF_Example/UI/ControlItem/InspectionListView.xaml` + `.xaml.cs` — Datum 노드 선택 시 Grab/Load 버튼 활성화(L258 주변 ICameraParam 분기 확장), Test FAI 버튼 신설, 노드 상태 배지 DataTemplate.
- `WPF_Example/UI/ControlItem/NodeViewModel.cs` / `Node.cs` — 상태 배지용 IsConfigured/HasRoi 등의 파생 bool 속성 추가 (또는 DataTemplate에서 Param 직접 바인딩).

### Phase 11 주요 수정 대상 (데이터 모델)
- `WPF_Example/Halcon/Models/RoiDefinition.cs` — `RoiShape Shape` enum + `CenterRow/CenterCol/Radius` 필드 신설. 기존 Rect/Polygon 필드는 유지.
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — `SourceShotName` string 필드 신설, 검출 라인 휘발성 필드(Line1/Line2 실제 lr/lc 2쌍)를 오버레이용으로 추가(또는 별도 DTO).
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` — `Circle_Row/Col/Radius` 필드 유지, 캔버스 편집 경로 노출을 위한 `IHalconTeachingProvider` 구현 검토.

### Phase 11 호출 확장 대상 (서비스)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — `TryTeachDatum` (L122) 인터페이스 유지. 성공 시 검출 라인 좌표를 out 파라미터 또는 DatumConfig 휘발성 필드로 돌려주는 경로 추가 검토 (현재는 RefOrigin/RefAngle만 저장, 라인 2개 픽셀 좌표는 버림).
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — Circle ROI 렌더링(`disp_circle`) 추가, Datum 검출 라인 + 십자 오버레이 렌더링 추가.
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — `TryFindCircle`는 존재(CircleDiameterMeasurement에서 사용 중), Phase 11에서 추가 변경 불필요.

### 재사용 (수정 없음 예상)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 런타임 실행 경로는 Phase 5/6/7에서 완성. Test FAI 버튼은 이 Action을 직접 호출하지 않고 Measurement.TryExecute를 임시 경로로 실행.
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — Sequence 레벨 DatumConfigs 리스트 관리(Phase 6). 변경 없음.
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — Recipe Save 경로 재사용(D-13).

### Upstream context (prior CONTEXT.md)
- `.planning/phases/02-teaching-calibration/02-CONTEXT.md` — D-10/D-11 (캔버스 직접 편집 + 툴바 버튼 패턴). Phase 11의 Circle/Teach Datum 버튼은 이 패턴 확장.
- `.planning/phases/04-datum/04-CONTEXT.md` — D-12/D-13/D-15 (Datum 티칭 원안). D-12는 Phase 11 D-25에서 명시적으로 폐기됨.
- `.planning/phases/06-rapid-city/06-CONTEXT.md` — D-07/D-08 (ImageSourceMode), D-24 (트리 구조), D-25 (Datum 승격).
- `.planning/phases/07-overlay-regression-fix/07-CONTEXT.md` — deferred 섹션(5종 Measurement overlay)은 Phase 11 **범위 외**로 유지.
- `.planning/bugs.md` — WR-RT-01/03/04 원본 결함 기술. Phase 11 완료 시 Fixed 표로 이동.

### 참고 문서 (간접)
- `260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB` — Datum B/C 정의, 현장 티칭 절차 참조.
- `CYG_Rapid_City_DFM-A8_0_Z_Stopper_V1_0` — Circle 측정이 요구되는 FAI 목록.

</canonical_refs>

<code_context>
## Existing Code Insights

### 현재 상태 (2026-04-23 시점)
- **Datum 티칭 UI: 전면 미구현.** `TryTeachDatum` (DatumFindingService.cs:122) 존재하지만 grep 결과 호출자 0건(바이너리 제외). Datum 노드 선택 시 `InspectionListView.xaml.cs:280-283`에서 `SetDatumOverlay`만 호출 → 이미지 없는 상태에서 ROI 사각형만 유령처럼 표시.
- **Grab/Load 버튼:** `InspectionListView.xaml.cs:258`에서 `itemParam is ICameraParam`일 때만 활성화. `DatumConfig`는 `ParamBase`만 상속, `ICameraParam` 미구현 → Datum 노드에서 Grab/Load 버튼이 비활성화. 이것이 "Datum 티칭 화면에 이미지 없음"의 직접 원인.
- **ROI 드로잉:** `MainView.ECanvasMode { None, RectRoi, PolygonRoi, Calibration }` 4가지만 존재(`MainView.xaml.cs:41`). Rect=`HalconViewerControl.StartRectangleDrawing`/`CommitActiveRectangle`, Polygon=`ImageLeftClicked`/`ImageRightClicked` 이벤트. Circle 경로 없음.
- **Circle 측정:** `CircleDiameterMeasurement` (Measurements/CircleDiameterMeasurement.cs)는 `Circle_Row/Col/Radius` 필드 보유, `VisionAlgorithmService.TryFindCircle` 호출. 동작은 가능하나 PropertyGrid 수동 입력만 지원 → WR-RT-01의 핵심 문제.
- **DatumConfig 상태:** `SourceShotName`/`ImageSourceMode`(=Dedicated/ReuseFromShot) 필드 존재(Phase 6). `ImageSourceMode` 기본값 Dedicated. `Line1_Row/Col/Phi/Length1/Length2` + `Line2_*` + `RefOriginRow/Col` + `RefAngleRad` + `IsConfigured` 완비. 런타임 휘발성 Transform도 존재.
- **MainResultViewerControl.SetDatumOverlay (L227):** 현재 구현은 `_datumConfig`/`_datumSelected` 플래그만 저장하고 `Render()` 호출 → `HalconDisplayService.Render` 내부에서 Datum ROI 사각형 2개 그리기. 검출 라인/교점 렌더링은 미구현.

### Phase 11 추가 후 흐름 (개념)
**Datum 티칭:**
1. 사용자가 Datum 노드 선택 → button_grab 활성화, Teach Datum 버튼 활성화
2. Grab 버튼 또는 LoadImage 버튼으로 이미지 획득(`SourceShotName` Shot의 카메라/SimulImagePath)
3. Teach Datum 버튼 클릭 → `ECanvasMode.TeachDatum`, `EDatumTeachStep.Line1`
4. Line1 ROI 드래그 → MouseUp에서 DatumConfig.Line1_* 기록, step=Line2
5. Line2 ROI 드래그 → MouseUp에서 DatumConfig.Line2_* 기록 + 즉시 TryTeachDatum 호출
6. 성공 → RefOrigin/RefAngle/IsConfigured 저장, 검출 라인 2개 + 교점 오버레이 렌더링, 상태바 "Datum 티칭 완료 — Recipe Save 권장"
7. 실패 → label_drawHint에 빨간 에러, ROI 사각형 유지, step=Done 상태로 사용자 재시도

**Circle ROI:**
1. 사용자가 FAI 또는 CircleDiameterMeasurement 노드 선택
2. Circle ROI 버튼 클릭 → `ECanvasMode.CircleRoi`
3. 캔버스 클릭(중심) → 드래그(반지름 실시간 미리보기) → MouseUp(확정)
4. CircleDiameterMeasurement.Circle_Row/Col/Radius 기록 + RoiDefinition(휘발성 편집 버퍼)의 Shape=Circle, Center/Radius 갱신
5. HalconDisplayService가 disp_circle로 렌더링

### 기존 ICameraParam 패턴 (Grab 버튼 활성화)
```csharp
// InspectionListView.xaml.cs:258
if (itemParam is ICameraParam) {
    button_grab.IsEnabled = true;
    button_loadImage.IsEnabled = true;
    button_light.IsEnabled = true;
}
```
DatumConfig가 ICameraParam을 구현하지 않아도 Datum 노드에서 Grab을 허용하려면:
- 옵션 A: DatumConfig가 ICameraParam 구현 → SourceShotName 기반 pass-through 속성 노출
- 옵션 B: 위 조건에 `|| itemParam is DatumConfig` 추가 → Grab 핸들러에서 `SourceShotName`으로 Shot 찾아 위임
옵션 B 권장(기존 데이터 모델 오염 최소).

### ParamBase 직렬화 제약 (유지)
- `RoiShape` enum은 `ParamBase.Save/Load`가 자동 지원(enum 타입 허용).
- `CenterRow/CenterCol/Radius` double 3개 추가는 INI에 자동 반영.
- 기존 INI의 Rect ROI는 `Shape` 필드 부재 → 기본값 Rect로 폴백.

### C# 7.2 / .NET 4.8 제약 유지
- nullable reference types, switch expressions, record 불가. enum + abstract + switch(문) 조합으로 처리.
- `HalconViewerControl`에서 마우스 좌표 → 이미지 좌표 변환은 기존 `ViewerPointerChangedEventArgs` 파이프라인 재사용.

### 빌드/검증
- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`
- SIMUL_MODE + 지정 SimulImagePath로 Datum 티칭 성공/실패 경로 육안 검증(Nyquist 자동화는 후속 Phase).
- 기존 Phase 6/7 INI 레시피가 Phase 11 배포 후에도 로드/실행 가능해야 함(RoiShape 미존재 시 Rect 폴백).

</code_context>

<specifics>
## Specific Ideas

### RoiShape enum 초안
```csharp
// RoiDefinition.cs 내부 또는 별도 파일
public enum RoiShape { Rect, Polygon, Circle }
```

### RoiDefinition 신규 필드 초안
```csharp
[DataMember] public RoiShape Shape { get; set; } = RoiShape.Rect;
[DataMember] public double CenterRow { get; set; }
[DataMember] public double CenterCol { get; set; }
[DataMember] public double Radius { get; set; }
```

### DatumConfig 신규 필드 초안
```csharp
// 기존 ImageSourceMode, Line1_*, Line2_* 유지
[DataMember] public string SourceShotName { get; set; } = "";  // 빈 문자열이면 Sequence 첫 Shot fallback

// 휘발성 (검출 직선 오버레이용)
[IgnoreDataMember] public double Line1Detected_RBegin, Line1Detected_CBegin, Line1Detected_REnd, Line1Detected_CEnd;
[IgnoreDataMember] public double Line2Detected_RBegin, Line2Detected_CBegin, Line2Detected_REnd, Line2Detected_CEnd;
[IgnoreDataMember] public bool LastTeachSucceeded;
```

### DatumFindingService.TryTeachDatum 시그니처 확장 검토
현재:
```csharp
public bool TryTeachDatum(HImage image, DatumConfig config, out string error)
```
Phase 11 개선(검출 라인 오버레이용):
```csharp
// 옵션 1: out 파라미터 추가
public bool TryTeachDatum(HImage image, DatumConfig config,
    out double l1rB, out double l1cB, out double l1rE, out double l1cE,
    out double l2rB, out double l2cB, out double l2rE, out double l2cE,
    out string error)

// 옵션 2 (권장): DatumConfig 휘발성 필드에 직접 기록
public bool TryTeachDatum(HImage image, DatumConfig config, out string error)
// 내부에서 config.Line1Detected_* / Line2Detected_* 세팅
```
옵션 2가 기존 호출부 호환성 유지에 유리.

### MainView.xaml 툴바 신규 버튼 초안
```xml
<ToggleButton x:Name="btn_teachDatum" Content="Teach Datum"
              IsEnabled="False" Click="TeachDatumButton_Click"/>
<ToggleButton x:Name="btn_circleRoi" Content="Circle ROI"
              IsEnabled="False" Click="CircleRoiButton_Click"/>
<Button x:Name="btn_testFai" Content="Test FAI"
        IsEnabled="False" Click="TestFaiButton_Click"/>
```

### 상태 배지 초안 (WPF DataTemplate 예시)
```xml
<!-- NodeViewModel에 Status 컬러 속성 추가 후 바인딩 -->
<Ellipse Width="8" Height="8" Fill="{Binding StatusColor}"/>
```
StatusColor = Red(미설정) / Yellow(부분) / Green(완료) / Transparent(비대상 노드).

### 예상 Plan 분할
- **Plan 01:** RoiDefinition 확장(Shape/Center/Radius) + HalconDisplayService Circle 렌더링 + HalconViewerControl Circle 드로잉 API. 순수 인프라, 빌드 통과가 완료 조건.
- **Plan 02:** Circle ROI 버튼 + MainView.ECanvasMode.CircleRoi + CircleDiameterMeasurement 필드 연동. SIMUL_MODE 육안 검증.
- **Plan 03:** DatumConfig.SourceShotName 필드 + Datum 노드 Grab 버튼 활성화 + Teach Datum 버튼 + Line1→Line2 순차 드로잉 + TryTeachDatum 자동 호출 + 검출 라인/교점 오버레이.
- **Plan 04:** WR-RT-04 워크플로 가이드 — 상태 배지 + 상태바 "다음 단계" 힌트 + Test FAI 버튼 + Datum/ROI 미설정 검증.

4개 plan 구성 권장. Plan 01+02(Circle 인프라)와 Plan 03+04(Datum UI + 가이드)가 두 묶음이며 Plan 03은 Plan 01에 의존하지 않음(병렬 가능).

</specifics>

<deferred>
## Deferred Ideas

- **Datum 전용 캡처 조건(Z/조명/노출) 분리** — Phase 11에서는 `SourceShotName`에서 상속(D-26). 독립 캡처 조건이 필요해지면 DatumConfig에 CaptureParam 신설.
- **Datum 티칭 Wizard UI** — step-by-step 모달 다이얼로그. 기존 트리+PropertyGrid 워크플로와 이질적이라 미채택.
- **개별 Line1/Line2 수정 경로** — 재티칭은 ROI 초기화 후 재시작으로 통일(D-04).
- **Tolerance 미설정 하드 검증** — 초기 튜닝 단계 불편 때문에 미채택(D-22).
- **하드 블로킹 순서 강제** — 사용자 피드백 유연성 저해로 미채택(D-20).
- **원형/3점 기반 Datum 패턴** — Phase 4 D-05 유지. Datum Line 2개 계약만 사용.
- **PointToLine/PointToPoint/LineToLineAngle/LineToLineDistance의 Circle ROI 적용** — Phase 11 범위 외. 필요 시 별도 phase.
- **MeasurementBase의 `IHalconTeachingProvider` 전면 구현** — Phase 11은 CircleDiameter 한정. 나머지 5종은 기존 PropertyGrid 수동 입력 유지.
- **Circle ROI 타원 확장 / 아크 ROI** — RoiShape enum 확장 가능성 열어두되 Phase 11 미구현.
- **Datum 오버레이 자동 갱신(검사 실행 시 검출 라인 표시)** — 티칭 성공 시점에만 오버레이 표시, 런타임 Inspection 결과 오버레이는 Phase 7 deferred에 포함.
- **Test FAI 결과 자동 저장/히스토리** — 일회성 리허설만. 히스토리는 v2 UI-07(트렌드 차트)에서.
- **Datum 재티칭 시 RefOrigin/RefAngle만 재계산(ROI 유지)** — 별도 UI 경로 추가 대신 Recipe JSON 편집이나 PropertyGrid 수동 수정으로 대체.

### Reviewed Todos (not folded)
- 없음 (Phase 11 cross_reference_todos 결과 해당 todo 미존재)

</deferred>

---

*Phase: 11-datum-teaching-ui-roi*
*Context gathered: 2026-04-23*
