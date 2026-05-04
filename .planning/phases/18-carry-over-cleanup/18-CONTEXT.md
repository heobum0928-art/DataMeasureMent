# Phase 18: Carry-over 정리 - Context

**Gathered:** 2026-05-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 17 partial sign-off에서 이월된 5건의 소형 결함(CO-01/03/04/05/06)을 완전히 해소하여 v1.1 개발 기반을 깨끗하게 만든다.
신규 기능 추가 없음 — 기존 동작 결함 수정 및 사양 명문화만.

</domain>

<decisions>
## Implementation Decisions

### CO-01: Circle_RadialDirection PropertyGrid ItemsSource

- **D-01:** `EdgeOptionLists.RadialDirections = {"Inward","Outward"}` 리스트 자체는 정확하다.
- **D-02:** 버그 원인은 `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` 바인딩이 ICustomTypeDescriptor 필터링 후 PropertyTools.Wpf에서 `EdgeOptionLists.Directions` (LtoR/RtoL/TtoB/BtoT 4개)로 잘못 해석되는 경로에 있다. 연구자가 root cause를 탐색하여 수정한다.
- **D-03:** INI 하위호환(EnsurePerRoiDefaults에서 sentinel "" → "Inward" fallback) 유지.

### CO-03: btn_teachDatum 호환성 가드 Spec 정리

- **D-04:** 코드 변경 없음 — 현재 `IsConfigured` 게이팅(hotfix#3)이 올바른 동작이다.
  - `IsConfigured=false` (새 Datum) → wizard(StartDatumTeachStep) 우선, ROI 없어도 티칭 진행 가능
  - `IsConfigured=true` (재티칭) → ValidateRoiPresence 모달 가드 실행
- **D-05:** 18-UAT.md Test 10을 재작성하여 현재 IsConfigured 게이팅 동작을 spec으로 명문화한다.
- **D-06:** Acceptance에 `grep -c "ValidateRoiPresence" MainView.xaml.cs` 자동 검증 명령 추가 — CO-03 "자동 검증" 성공 기준 충족.

### CO-04: ROI 재그리기 우클릭 메뉴

- **D-07:** 노출 조건: `_canvasMode == ECanvasMode.TeachDatum` (btn_teachDatum ON) + 우클릭 위치에 ROI hit-test 통과한 ROI가 있을 때만 메뉴 항목 표시. ROI 없으면 항목 숨김(Visibility.Collapsed).
- **D-08:** 레이블: `"ROI 다시 그리기"` (한국어).
- **D-09:** 동작: hit-test 통과한 ROI의 Length1/Length2/Radius를 0으로 리셋 — 기존 `ClearDatumRoiFields` 패턴 재사용. 자동 그리기 모드 진입 없음.
- **D-10:** 적용 대상: Datum ROI만 (FAI ROI는 좌클릭+드래그 재그리기로 이미 가능).
- **D-11:** 구현 위치: `HalconViewerControl.xaml` ViewerContextMenu에 항목 추가 + `MainView.xaml.cs` ViewerRightClicked 핸들러에서 hit-test 후 Visibility 제어.

### CO-05: 검출 Strip 색상 분기

- **D-12:** 적용 범위: Circle polar strip만 (CTH 알고리즘 전용). Horizontal A/B line strip은 이번 Phase 제외.
- **D-13:** 데이터 전달: `DatumConfig.CircleStripSuccesses bool[]` transient 필드 신설.
  - 속성 데코레이션: `[Browsable(false)]`, `[JsonIgnore]`, `[System.Text.Json.Serialization.JsonIgnore]` — INI/JSON 직렬화 제외.
  - Phase 17 DetectedOriginRow/Col transient 패턴(`//260503 hbk Phase 17 D-13`) 동일 방식.
- **D-14:** 갱신 시점: `TryTeachCircleTwoHorizontal` 완료 시에만 갱신. Test Find(TryFindDatum) 시에는 갱신 없음.
- **D-15:** 색상 분기: `RenderCircleStripOverlay`에서 `CircleStripSuccesses[i] == true` → `"green"` / `false` → `"red"` / 배열 null 또는 인덱스 범위 초과 → 기존 `"gray"` fallback.
- **D-16:** bool[] 크기 = stepCount (TryFindCircleByPolarSampling의 실제 strip 수).

### CO-06: FormatTeachError ROI Label 보존

- **D-17:** `FormatTeachError(string err)` 시그니처를 `FormatTeachError(DatumConfig datum, string err)`로 확장.
- **D-18:** 에러 메시지 앞에 `"[{datum.Name}] "` 접두사 추가 — 예: `"[Datum 2] 검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요."`.
- **D-19:** 기존 호출 사이트(`CustomMessageBox.Show("티칭 실패", FormatTeachError(error))`)에 datum 인자 추가.

### Claude's Discretion

- CO-01 버그의 정확한 root cause(PropertyTools 내부 바인딩 경로 vs ICustomTypeDescriptor 상호작용)는 연구자/실행자가 코드 탐색으로 결정.
- CO-04 hit-test 로직 구현 방식(기존 `HitTestSelectedRoi` 재사용 vs 별도 캔버스 좌표 변환) — 플래너가 결정.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 17 결과 (Carry-over 원천)
- `.planning/milestones/v1.0-phases/17-datum-ux-circle-strip-1-test-find-detectedorigin-hover/17-UAT.md` — Phase 17 FAIL/SKIP 항목 상세 (Test 2, 8, 10 결과 및 notes)

### 요구사항 정의
- `.planning/REQUIREMENTS.md` §Phase 17 Carry-over — CO-01/CO-03/CO-04/CO-05/CO-06 정의
- `.planning/ROADMAP.md` §Phase 18 — 성공 기준 5개

### v1.1 추가 범위 (Phase 19.5 신규 Phase 참조용)
- `.planning/v1.1-MILESTONE_add.md` — ArcEdgeDistance/CompoundAngle/LineConstructDistance 알고리즘 3종 + GR&R 범위 정의

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DatumConfig.cs` L239-249: `Circle_RadialDirection` + `[ItemsSourceProperty]` + `Circle_RadialDirectionList` — CO-01 디버깅 대상
- `DatumConfig.cs` L545-554: `GetProperties(Attribute[])` ICustomTypeDescriptor — CO-01 바인딩 경로
- `EdgeOptionLists.cs` L25-27: `RadialDirections = {"Inward","Outward"}` — 정확함, 수정 대상 아님
- `DatumConfig.cs` L596-599 (EnsurePerRoiDefaults): Circle_RadialDirection sentinel 처리 — 유지
- `MainView.xaml.cs` L717-738: `ValidateRoiPresence` — CO-03 spec 기준 코드 (변경 없음)
- `MainView.xaml.cs` L741-749: `FormatTeachError` — CO-06 시그니처 확장 대상
- `MainView.xaml.cs` L779-791: ROI 이동 재티칭 경로 (ClearDatumRoiFields 패턴 참고)
- `HalconViewerControl.xaml` L17-22: `ViewerContextMenu` (현재 Zoom/Fit 3개) — CO-04 항목 추가 위치
- `HalconViewerControl.xaml.cs` L84: `ViewerRightClicked` event — CO-04 MainView 핸들러 연결점
- `HalconDisplayService.cs` L457-515: `RenderCircleStripOverlay` — CO-05 색상 분기 위치

### Established Patterns
- Transient 필드 패턴: `[Browsable(false)] + [JsonIgnore] + 0/null 초기값` — Phase 17 D-13 `DetectedOriginRow` 동일 패턴을 `CircleStripSuccesses bool[]`에 적용
- 우클릭 메뉴 hit-test: Phase 17 `HitTestSelectedRoi()`가 `_isEditMode` 게이팅으로 이미 존재 — CO-04에서 `_canvasMode == TeachDatum` 게이팅 추가
- `//260505 hbk Phase 18` 주석 컨벤션 — 모든 변경 라인에 필수

### Integration Points
- `DatumFindingService.TryTeachCircleTwoHorizontal` → `CircleStripSuccesses bool[]` 기록 → `RenderCircleStripOverlay` 색상 소비
- `HalconViewerControl.ViewerRightClicked` → `MainView` 핸들러 → hit-test → "ROI 다시 그리기" 항목 Visibility 제어

</code_context>

<specifics>
## Specific Ideas

- CO-04 메뉴 레이블 확정: `"ROI 다시 그리기"` (Phase 17 UAT 원안 "Re-draw this ROI"의 한국어 버전)
- CO-05 색상: green = `"green"` / red = `"red"` (HalconDisplayService 기존 색상명 문자열 방식 유지)
- CO-06 에러 메시지 형식: `"[{datum.Name}] {기존 메시지}"` — datum 이름이 앞에 오는 간결한 형식

</specifics>

<deferred>
## Deferred Ideas

### 알고리즘 3종 신규 Phase (v1.1 로드맵 삽입 예정)
- **ArcEdgeDistance** (G1~G12, 12개 FAI 항목) — 이론 교점 → 스캔 → 실제 Edge
- **CompoundAngle** (E2) — Circle+Line 조합, 반원 대응
- **LineConstructDistance** (E5) — 가상 기준선 생성 후 거리 측정
- 배치: Phase 19 이후 신규 Phase (19.5번)로 `/gsd-insert-phase` 사용
- 전제 파일 필요: `CODE-RULES.md`, `.planning/phases/06-rapid-city/06-algorithms-extension.md` (아직 미생성)
- 참조: `.planning/v1.1-MILESTONE_add.md`

### GR&R 엑셀 (AIAG 표준)
- v1.1-MILESTONE_add.md의 전체 GR&R (AIAG MSA 4th, %GR&R/%EV/%AV) — Phase 25 OUT-03과 범위 겹침 여부 Phase 25 discuss 시 확인 필요

### Manual + Verify 워크플로우 (Phase 17 UAT carry-over #7)
- wizard 강제 단계 대신 자유 그리기 + Test 사이클 — v1.1 백로그

</deferred>

---

*Phase: 18-carry-over-cleanup*
*Context gathered: 2026-05-05*
