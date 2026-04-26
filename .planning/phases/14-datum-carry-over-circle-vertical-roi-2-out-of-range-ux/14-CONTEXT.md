# Phase 14: Datum carry-over - Context

**Gathered:** 2026-04-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 13 Datum carry-over 5 sub-phase 의 **HOW** 결정. 5건 묶음:
1. Circle ROI 이동/resize 회귀 fix (14-01)
2. TwoLineIntersect 각도 out-of-range 게이트 (14-02)
3. Vertical 에지 파라미터 그룹 신설 (14-03)
4. Circle polar-sampling 신규 알고리즘 (14-04)
5. CircleTwoH/VerticalTwoH 정상화 + 결함 fix (14-05)

WHAT 은 SPEC.md 5 requirements + 15 acceptance criteria 로 이미 락. 본 CONTEXT 는 구현 결정만 다룸.
</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**5 requirements 가 locked.** See `14-SPEC.md` for full requirements, boundaries, and acceptance criteria.

Downstream agents MUST read `14-SPEC.md` before planning or implementing. Requirements are not duplicated here.

**In scope (from SPEC.md):**
- Circle ROI 이동 후 자동 재티칭 발동 wiring fix (14-01)
- Circle ROI Edit 모드 N/S/E/W 핸들 resize 동작 + 자동 재티칭 (14-01)
- TwoLineIntersect 두 라인 각도 게이트 + PropertyGrid `TwoLineAngleToleranceDeg` 필드 (14-02)
- DatumConfig Vertical 그룹 13 필드 신설 (5 geometry + 6 edge + 2 raw) + PropertyGrid Category + INI 마이그레이션 (14-03)
- DatumFindingService.TryTeachVerticalTwoHorizontal 의 Line1_* → Vertical_* 슬롯 교체 (14-03)
- MainView PublishDatumRoiCandidates / ApplyDatumRoiDelta / ClearDatumRoiFields 의 Datum.Vertical 분기 추가 (14-03)
- VisionAlgorithmService 신규 `TryFindCircleByPolarSampling` 메서드 + 3 PropertyGrid 파라미터 (14-04)
- DatumFindingService.TryTeachCircleTwoHorizontal 의 Circle 검출 호출을 신규 polar sampling 으로 교체 (14-05)
- btn_testFindDatum 으로 3 알고리즘 PASS 검증 + 발견된 결함 fix (14-05)

**Out of scope (from SPEC.md):**
- FAI 측정 경로 변경 — 독립 유지
- ROI Edit 모드 전반 재설계 (Polygon 이동 / Rect 회귀) — 별도 백로그
- `CircleDiameterMeasurement` legacy `TryFindCircle` 변경 — 시그니처 보존
- Halcon `MeasureCircle` 등 SDK 빌트인 사용 — 사용자 지정 회전 방식만
- Strategy 패턴 / 알고리즘 클래스 추상화 리팩터
- TCP / 시퀀스 / 외부 통신 영향
</spec_lock>

<decisions>
## Implementation Decisions

### A. Circle ROI Resize 핸들 (14-01)

- **D-01:** Circle ROI 의 Edit 모드 N/S/E/W 4 위치에 **시각적인 작은 사각형 마커**(visible square handle) 를 그린다. 사용자가 마커를 보고 잡을 수 있어야 함. 현재 `GetEditHandles` (`MainResultViewerControl.xaml.cs:365-372`) 는 핸들 좌표만 계산하고 그리지 않음 → 렌더 패스 추가 필요.
- **D-02:** 4 핸들 중 어느 것을 잡고 드래그해도 동일 로직으로 반경 변경 (drag 종점과 center 사이 거리 = 새 반경). N/S/E/W 구분은 시각 위치만, 동작은 동일.
- **D-03:** Resize 완료 시 자동 재티칭 발동 (Circle 이동 시 자동 재티칭과 동일 경로 — `InvokeTryTeachDatumForEdit` + `PublishDatumRoiCandidates` + `UpdateDatumRefCoordsLabel`).
- **D-04 (Claude's Discretion):** 코드 경로 — (1) 별도 `RoiResizeCompleted` 이벤트 신설 vs (2) 기존 `RoiMoveCompletedArgs` 에 `EditHandle?` 옵션 추가하여 단일 이벤트 확장 vs (3) Datum 전용 helper. **선호: (2) 단일 이벤트 확장** — 기존 13-03 `RoiMoveCompleted` 분기 패턴 재사용 가능, 신규 wiring 최소. 단 plan-phase researcher 가 `MainResultViewerControl` 의 drag 상태 머신과 충돌 가능성 점검 후 (1) 별도 이벤트로 변경 가능.

### B. INI 하위호환 정책 (14-03)

- **D-05:** Vertical 그룹 신설 시 `EnsurePerRoiDefaults` 마이그레이션은 **양쪽 다 채움** 정책: 기존 INI 의 `Line1_*` 값을 신규 `Vertical_*` sentinel(0/"") 일 때 1회 복사하고, **`Line1_*` 도 zero-out 하지 않고 그대로 유지**. 이유: (1) 회귀 위험 0, (2) 사용자가 알고리즘을 TwoLineIntersect 로 다시 바꿔도 Line1_* 값 즉시 사용 가능, (3) 데이터 손실 0.
- **D-06:** 마이그레이션은 `Vertical_EdgeThreshold==0` 등 sentinel 검출 기준, idempotent (`EnsurePerRoiDefaults` 패턴 13-04 연장). 한 번 마이그레이션 후 사용자가 Vertical_* 만 수정해도 Line1_* 는 그대로 (양쪽 동기화 안 함 — 사용자 입력이 권한).

### C. PropertyGrid 알고리즘별 가시성 (14-03)

- **D-07:** **알고리즘별 동적 숨김** 채택. AlgorithmType 에 따라:
  - `TwoLineIntersect` → Line1, Line2 그룹만 노출 (Circle / Vertical / Horizontal_A / Horizontal_B 숨김)
  - `CircleTwoHorizontal` → Circle, Horizontal_A, Horizontal_B 그룹만 노출
  - `VerticalTwoHorizontal` → Vertical, Horizontal_A, Horizontal_B 그룹만 노출
- **D-08 (Claude's Discretion):** 동적 가시성 구현 패턴 — PropertyTools 의 `[Browsable]` attribute 는 정적이라 런타임 토글이 자연스럽지 않음. 선택지: (1) `INotifyDataErrorInfo`/`Browsable` proxy property + AlgorithmType 의존, (2) PropertyGrid `SelectedObject` 를 알고리즘별 view-model wrapper 로 감쌈 (Line1/Line2 만 expose 하는 wrapper 등), (3) `BrowsableAttribute` 동적 적용 (TypeDescriptor.AddAttributes). plan-phase researcher 가 PropertyTools 3.1.0 의 실제 동작 확인 후 가장 단순한 방식 채택. 구현 까다로움이 예상보다 크면 fallback 으로 "그룹 이름에 [VTH]/[CTH] prefix" (시각 구분만) 채택 가능 — UX 정보량 감소 목적이 1차이므로 허용 가능한 트레이드오프.
- **D-09:** 가시성 토글은 PropertyGrid 만 영향. INI 저장/로드/내부 코드는 모든 그룹 항상 활성 (마이그레이션 안전).

### D. 14-05 진입 방식 (CircleTwoH/VerticalTwoH 정상화)

- **D-10:** **단순 verify 우선** — 14-03 (Vertical 그룹) + 14-04 (Circle polar) 통합 후 SIMUL_MODE 에서 btn_testFindDatum 으로 3 알고리즘 retest. PASS 면 14-05 종료. FAIL 일 때만 진단 로깅 + Phi/per-ROI 와이어링 audit 추가.
- **D-11:** 14-05 plan 작성 시 두 시나리오 분기 명시: (PASS path) "단순 verify + UAT 기록" / (FAIL path) "진단 로그 추가 → 결함 식별 → fix → 재 retest". plan-phase 가 default plan 은 PASS path 로 작성하되 FAIL contingency 메모.

### E. Circle Polar 좌표계 부호 컨벤션 (14-04)

- **D-12:** **화면 시점 CCW** 채택. 0°=화면 오른쪽(col+), 90°=화면 위쪽(row-), 180°=화면 왼쪽(col-), 270°=화면 아래(row+). 사용자 직관 일치 ("0°에서 반시계로 회전" = 화면상 위로).
- **D-13:** Halcon image 좌표 변환식:
  - rect 중심 row = `centerRow - radius * sin(theta_rad)`  (sin 앞 minus: 화면 위쪽 = row 감소)
  - rect 중심 col = `centerCol + radius * cos(theta_rad)`
  - rect phi (Halcon Rectangle2 의 회전각) = `theta_rad` (반경 방향 = rect length1 축; Halcon phi 는 col 양축 기준 시계 방향이지만 sin 부호로 보정)
  - **주의:** plan-phase researcher 는 Halcon Rectangle2 phi 의 정확한 정의(시계/반시계, 라디안/도) 를 SDK 문서로 재확인 후 D-13 부호식 검증 필수.

### Claude's Discretion

- D-04: Circle resize 코드 경로 (단일 이벤트 확장 선호, plan researcher 최종 결정)
- D-08: PropertyGrid 동적 가시성 구현 패턴 (3 후보 중 PropertyTools 실험 후 선택)
- D-13: Halcon Rectangle2 phi 부호 (SDK 문서 재검증)

### Folded Todos

없음 — 매칭된 todo 0건.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 14 Spec
- `.planning/phases/14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux/14-SPEC.md` — Locked requirements (5건) + 15 acceptance criteria. **MUST read before planning.**

### Phase 13 Carry-over Source
- `.planning/phases/13-datum-algorithm-extensibility/13-UAT.md` — Phase 13 UAT 결과, Phase 14 carry-over routing decision (commit `d9b5cc8`)
- `.planning/phases/13-datum-algorithm-extensibility/13-05-SUMMARY.md` — Datum 시각화 + Circle raw 점 carry-over 직접 원인
- `.planning/phases/13-datum-algorithm-extensibility/13-06-SUMMARY.md` — PropertyGrid 자동 재티칭 패턴 (`NotifyDatumParamMaybeChanged`)
- `.planning/phases/13-datum-algorithm-extensibility/13-CONTEXT.md` — Phase 13 결정 (D-01..D-PRP, additive only / 시그니처 보존 원칙)

### Project Constraints
- `.planning/PROJECT.md` — Tech stack, constraints (.NET 4.8 / C# 7.2 / Halcon 24.11 / WPF / additive only / SystemHandler singleton)
- `.planning/REQUIREMENTS.md` — ALG-05 (Datum hom_mat2d 변환) 회귀 없음 보장
- `CLAUDE.md` — Naming/comments/error handling/logging conventions

### Code Anchors (read before planning)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 5 ROI × 6 edge 필드 + EnsurePerRoiDefaults 패턴 (Vertical 그룹 추가의 모델)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — TryTeachVerticalTwoHorizontal `Line1_*` 재사용 (`:486-503`), TryTeachCircleTwoHorizontal Circle 검출 호출 (`:292-`), ValidateHorizontalVerticalAngles 패턴
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:120-200` — `TryFindCircle` (legacy, 시그니처 보존). 신규 `TryFindCircleByPolarSampling` 의 비교 기준
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs:303-372` — HitTestSelectedRoi + HitTestOneRoi (Circle 분기) + GetEditHandles (Circle N/S/E/W 핸들 좌표)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs:526-617` — HalconViewer_RoiMoveCompleted + HandleDatumRoiMove + ApplyDatumRoiDelta + ClearDatumRoiFields (Datum.Vertical 분기 추가의 모델)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs:697-728` — PublishDatumRoiCandidates 알고리즘 분기 (Datum.Vertical RoiId 추가의 모델)
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — RenderDatumOverlay LastTeachSucceeded 분기 (raw 점 추가 출력 모델)
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — AlgorithmType enum

### External Reference (사용자 명시)
- `C:\Info\Project\DatumMeasure` — Phase 13-04 strip-loop 패턴 원본 (참조 포팅 출처). 14-04 polar sampling 신규 개발 시 비교 참고만, 그대로 포팅하지 않음.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`EnsurePerRoiDefaults` 패턴** (`DatumConfig.cs:326-374`) — Vertical 그룹 신설 시 동일 패턴으로 sentinel 기반 idempotent 마이그레이션 가능. Line1_* → Vertical_* 복사 분기만 추가하면 됨.
- **`HitTestOneRoi`** (`MainResultViewerControl.xaml.cs:332-348`) — Rect/Circle 공용 helper, Circle 분기 정상. Edit 핸들 hit-test 추가 시 별도 helper 신설 가능.
- **`PublishDatumRoiCandidates`** (`MainView.xaml.cs:697-728`) — 알고리즘 분기 switch 패턴 정착. VerticalTwoHorizontal 케이스에 `Datum.Vertical` RoiId 추가만 필요.
- **`ApplyDatumRoiDelta` / `ClearDatumRoiFields`** (`MainView.xaml.cs:580-617`) — switch case 추가 패턴. Datum.Vertical 분기 한 줄씩 추가.
- **`NotifyDatumParamMaybeChanged`** (Phase 13-06+13-07) — PropertyGrid 변경 → 자동 재티칭 라우팅. Vertical_* 신규 필드도 동일 라우팅으로 자동 동작 (가드는 IsConfigured 만, Phase 13-07 fix 유지).
- **`Dispatcher.BeginInvoke(Background)` defer 패턴** (Phase 13-07 Fix A) — UI 스레드 블록 회피. Circle resize 후 자동 재티칭에도 동일 패턴 권장.

### Established Patterns
- **Additive only** (Phase 13 success criterion 6) — DatumConfig / DatumFindingService / MainView 공용 시그니처 변경 금지. Vertical 그룹은 신규 필드라 시그니처 무영향. 신규 메서드 (`TryFindCircleByPolarSampling`) 는 기존 `TryFindCircle` 보존하고 추가만.
- **`[Browsable(false)]` + Volatile 패턴** (Phase 13-05 D-VIZ-01) — runtime 전용 HTuple 필드는 PropertyGrid 비공개 + ParamBase reflection 자동 무시 → INI 영향 0. Vertical_DetectedEdgeRows/Cols 도 동일.
- **Allman vs K&R 스타일 file-local** — DatumConfig/DatumFindingService Allman, MainView K&R. 파일 내 일관 유지.
- **Sentinel 검출 기준** — `int==0` / `string==""` 으로 미설정 판단. 사용자 입력 0 와 sentinel 충돌 가능성은 EdgeThreshold/Sigma 등이 의미상 0 이 invalid 라 안전.

### Integration Points
- **DatumConfig 데이터 모델 확장** (`Custom/Sequence/Inspection/DatumConfig.cs`) — Vertical 그룹 13 필드 + Circle 그룹 3 신규 (Polar) + TwoLineAngleToleranceDeg 1 신규
- **DatumFindingService.TryTeachVerticalTwoHorizontal** — `config.Line1_*` 6 참조를 `config.Vertical_*` 로 교체 (단순 search/replace 수준)
- **DatumFindingService.TryTeachCircleTwoHorizontal** — Circle 검출 호출을 `TryFindCircle` → `TryFindCircleByPolarSampling` 로 교체
- **VisionAlgorithmService** — 신규 메서드 추가 (legacy `TryFindCircle` 그대로 유지)
- **MainResultViewerControl** — Circle resize 핸들 렌더 + hit-test + drag 상태 처리 추가
- **MainView** — `RoiMoveCompleted` 또는 신규 `RoiResizeCompleted` 핸들러 + Datum.Vertical 분기 4 곳 추가
- **HalconDisplayService** — RenderDatumOverlay Vertical raw 점 색상 매핑 추가 (예: cyan 재사용 또는 신규 색)
</code_context>

<specifics>
## Specific Ideas

- **Circle resize 핸들 시각** — 사용자 명시: "동서남북에다가 작은 사각형으로 보여줘 그걸 조절하면서 사이즈 조정". 사각형 마커 (visible square) 가 핵심 — 점 / 십자 가 아닌 사각형. 색/크기는 Claude's Discretion (예: 6×6 px 흰색/노란색).
- **PropertyGrid 가시성 의도** — "너무 정보가 많아" 가 동기. 동적 숨김 구현이 까다로워도 "정보량 감소" 가 1차 목표. fallback (prefix) 도 정보량 감소 목적은 달성.
- **Circle polar 알고리즘 개념** — 사용자 명시: "ROI 원을 그리고 그 원의 기준을 토대로 반지름까지 ... center 를 회전축으로 10도씩 혹은 1도씩 Option 을 두고, 사각형 ROI 생성하고 회전하면서 원을 구한 후 원피팅을 하여 center 를 구한다". 회전 로직만 신규, 사각형 ROI 별 에지 추출은 기존 `GenMeasureRectangle` + `MeasurePos` 재사용.
- **참조 코드** — `C:\Info\Project\DatumMeasure` 의 strip-loop 패턴은 Phase 13-04 에 포팅 사례 있음. 14-04 polar sampling 도 동일 출처에 유사 코드 있을 가능성 — researcher 가 grep 해보고 활용.
</specifics>

<deferred>
## Deferred Ideas

- **ROI Edit 모드 전반 재설계** (memory: project_roi_edit_mode_deferred) — Polygon 이동 / Rect 회귀 버그 / Edit 모드 진입 UX. Phase 14 는 Circle resize 한 케이스만 좁혀 처리. 전반 재설계는 별도 phase.
- **Strategy 패턴 / 알고리즘 클래스 추상화** — Phase 13 deferred 결정 그대로 유지. switch 디스패치 패턴 (DatumFindingService) 계속 사용.
- **Halcon `MeasureCircle` 등 SDK 빌트인** — Phase 14 는 사용자 지정 회전 방식만. SDK 빌트인 비교/벤치마크는 별도 spike 가능.
- **PropertyGrid 동적 가시성 fallback (prefix only)** — D-08 의 (3) 후보. 1차 동적 숨김 구현이 너무 까다로우면 그때 채택. fallback 채택 시 사용자 추가 승인 필요.
- **D-07 알고리즘별 동적 [Browsable] 숨김** — 사용자 승인 (2026-04-26) 으로 D-08 prefix fallback 채택. PropertyTools 3.1.0 런타임 토글 미시도. 향후 사용자가 prefix 만으로 부족하다고 판단하면 별도 phase 에서 TypeDescriptor.AddAttributes / SelectedObject wrapper 패턴으로 재시도 가능.
- **Vertical_* / Line1_* 양방향 동기화** — D-06 에서 안 함. 향후 사용자가 알고리즘 빈번히 전환하며 Vertical/Line1 일치 원하면 별도 quick task.
- **Reviewed Todos** — 매칭된 todo 0건이라 reviewed 도 없음.
</deferred>

---

*Phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux*
*Context gathered: 2026-04-26*
