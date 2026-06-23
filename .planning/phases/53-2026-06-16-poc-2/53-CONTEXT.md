# Phase 53: 픽셀 캘리브레이션 (체커보드) - Context

**Gathered:** 2026-06-23
**Status:** Ready for planning

<domain>
## Phase Boundary

별도 캘리브레이션 창에서 체커보드(흑백 격자) 이미지를 입력받아 **격자 코너를 검출 → 코너 간격 기반 mm/px(픽셀 분해능)를 산출 → `ShotConfig.PixelResolution`에 반영**한다.

**In scope:** 격자 코너 검출, mm/px 산출, 외곽 왜곡 검증(수치+경고), 사용자 확인 후 PixelResolution 반영, 이미지 로드 + 라이브 촬상 입력.

**Out of scope (의도적):** 렌즈 왜곡 보정(undistort/이미지 와핑), 풀 카메라 캘리브레이션(set_calibration_data/CalibrateCameras), HALCON 전용 caltab 점판. 텔레센트릭 전제. 외곽 왜곡이 유의미하게 검출되면 **별도 Phase로 undistort 승격**(이번 phase 아님).
</domain>

<decisions>
## Implementation Decisions

### 격자 검출 & 피치 입력
- **D-01:** 격자 한 칸 실제 크기(mm)는 **사용자 수동 입력**, 직전 입력값을 기본값으로 기억. (체커보드 규격은 사용자만 앎)
- **D-07 (carried, LOCK):** **풀 caltab 캘리브 안 씀.** 일반 흑백 체커보드 코너 검출 → 인접 코너 간격(px) → `knownMm / pixelDist = mm/px` 직접 산출. HALCON 전용 caltab 점판/`set_calibration_data` 불필요.
- 구체적 코너 검출 연산자(HALCON `find_rectangular_pattern` / `points_foerstner` / `edges_sub_pix`+교점 등)는 **research/planner 재량** — "코너점 검출 후 격자 간격 통계" 개념만 lock.

### mm/px 산출 & 외곽 왜곡 검증
- **D-02:** mm/px는 **단일 평균값**(가로·세로 간격 평균) → `PixelResolution` 반영. 텔레센트릭 등방 가정. X·Y 분리값은 **리포트로 참고 표시만**(적용은 단일).
- **D-05:** 외곽 왜곡 검증 = **수치 + 임계 경고**. 격자 간격 평균±편차, **중앙↔외곽 간격 편차%** 표시 + 임계(예: 1%) 초과 시 경고 라벨. 이게 "추후 undistort 승격" 판정 게이트.
- **D-08 (carried, LOCK):** 왜곡 보정은 안 함. 측정이 FOV 외곽 위주라 잔여왜곡 우려 있으나, **검증→데이터로 판정** 후 필요시 별도 phase.

### PixelResolution 적용
- **D-03:** 산출값을 **활성 시퀀스 전체 shot**에 일괄 반영(동일 카메라/렌즈 가정). 기존 2점 캘리브(`ApplyCalibrationResult`)의 "선택 FAI shot 1개"보다 범위 확장.
- **D-06:** **사용자 확인 후 반영.** 산출값+왜곡 리포트를 먼저 보여주고 **[적용] 버튼**으로 반영(되돌리기 어려운 설정 덮어쓰기라 안전장치 필수). 자동 반영 아님.
- Phase 42 단일소스 계약 유지: 측정 소비는 `ShotConfig.PixelResolution`. (FAI `PixelResolutionX/Y`는 INI 호환 보존용, 기존 패턴 따름)

### 입력 모드 & 창
- **D-04:** **이미지 로드 + 라이브 촬상 둘 다** 제공(ROADMAP). 단 `SIMUL_MODE`에선 이미지 로드만 동작, 실 HW에서 라이브 정지→촬상. 별도 Window UX.

### Claude's Discretion
- 코너 검출 HALCON 연산자 선택 및 격자 정렬/이상치 절사 방식 (research)
- 캘리브 창 레이아웃/위젯 세부 배치
- 검출 실패·부분검출 시 가드/에러 메시지
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 정의
- `.planning/ROADMAP.md` §"Phase 53: 픽셀 캘리브레이션 (체커보드)" — Goal/Scope/UAT (CAL-01)

### 설계 배경 (메모리, 필독)
- 메모리 `project_calibration_telecentric` — 텔레센트릭이라 mm/px만, 레퍼런스 QCellInspector CCalibration(CCTV 와핑) 복사 금지. **범위 LOCK 2026-06-23**: 코너 검출 직접 산출 + 외곽 왜곡 검증 게이트.
- 메모리 `project_phase42_progress` — `ShotConfig.PixelResolution` 런타임 단일소스 (이 phase 산출값의 소비처)
- 메모리 `project_correction_factor` — per-shot CorrectionFactor(캘리브 위 곱셈 보정 레이어). PixelResolution과 곱해짐 → 산출값 해석 시 주의.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — 기존 **2점 수동 캘리브** 전체 경로: `HalconViewer_CalibrationMouseDown` → `FinishCalibration`(mm/px = realMm/pixelDistance) → `ApplyCalibrationResult(mmPerPixel)` → `shot.PixelResolution = mmPerPixel` + FAI `PixelResolutionX/Y`. **반영 로직 재사용 가능**, 단 "선택 FAI shot 1개" → "활성 시퀀스 전체 shot"으로 확장 필요(D-03).
- `HalconViewerControl.LoadImage(...)` — 이미지 로드/표시 (이미지 로드 입력 모드).
- `ShotConfig.PixelResolution` (Phase 42 단일소스) — 산출값 반영 타겟.

### Established Patterns
- 별도 창: 기존 `TeachingWindow`/`ReviewerWindow`(`WPF_Example/UI/Dialog`, `UI/Reviewer`) WPF Window 패턴 따름.
- `SIMUL_MODE` 분기: 라이브 촬상은 실 HW 경로, SIMUL은 이미지 로드 폴백.

### Integration Points
- 캘리브 창 → 산출값 → 활성 시퀀스(`SequenceHandler`/`recipeManager.Shots`)의 shot들 PixelResolution 일괄 set → 레시피 저장.
</code_context>

<specifics>
## Specific Ideas

- 사용자 우려: **측정이 주로 FOV 외곽** → 텔레센트릭 잔여왜곡이 최대인 지점과 겹침. 그래서 외곽 왜곡 검증(D-05)이 단순 부가기능이 아니라 **핵심 판정 게이트**.
- 현재 체커보드 실측 이미지 없음 → **구현 먼저**, 정확도·왜곡 UAT는 실측 이미지 확보 후 (인터넷 다운로드 체커보드로 검출 동작까지만 1차 확인 가능).
</specifics>

<deferred>
## Deferred Ideas

- **렌즈 왜곡 보정(undistort)** — 외곽 왜곡 검증(D-05)에서 임계 초과가 실측되면 별도 Phase로 승격. HALCON 텔레센트릭 모델 `gen_cam_par_area_scan_telecentric_division` 확장 후보. 이번 phase 아님.
- X·Y 분리 적용(이방성 보정) — 현재는 단일 평균(D-02). 필요시 후속.
</deferred>

---

*Phase: 53-pixel-calibration-checkerboard*
*Context gathered: 2026-06-23*
