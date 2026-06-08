---
phase: 39-inspection-workflow-e2e-2026-05-29
plan: 03
type: summary
status: complete
date: 2026-05-29
files_modified:
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/UI/ControlItem/NodeViewModel.cs
requirements_addressed: [WF-01, WF-02]
depends_on: [39-01]
---

# Plan 39-03 SUMMARY — UI overlay 'DETECT FAIL' + INPC

## Output

Wave 2 UI overlay / 1 commit / 3 파일.

### 신규 인터페이스 3종

| 위치 | 멤버 | 시그니처 | 용도 |
|------|------|----------|------|
| DatumConfig | `_lastFindSucceeded` | `private bool` | LastFindSucceeded backing field (INPC 패턴) |
| DatumConfig | `HasDetectFail` | `public bool { get; }` computed | `IsConfigured && !_lastFindSucceeded` — UI XAML binding 진입점 |
| NodeViewModel | (case) `nameof(DatumConfig.LastFindSucceeded)` | switch case | `RaisePropertyChanged("HasDetectFail")` + return |

### 동작 흐름

1. **DatumPhase 실패 → DatumConfig 상태 갱신** (Phase 37 D-37-03 기존 경로):
   - `InspectionSequence.TryRunDatumPhase` / `TryRunSingleDatum` 가 `datum.LastFindSucceeded = false` 세팅 (변경 0)

2. **INPC 자동 발화** (본 plan 추가):
   - `LastFindSucceeded` setter (backing field) → `RaisePropertyChanged(nameof(LastFindSucceeded))` + `RaisePropertyChanged(nameof(HasDetectFail))`
   - idempotent 가드: 동일 값 set 시 early return (무동작)

3. **NodeViewModel 핸들러 분기** (본 plan 추가):
   - `OnParamPropertyChanged(e.PropertyName == "LastFindSucceeded")` → `RaisePropertyChanged("HasDetectFail")` → return (Node.Name 갱신 미진입)
   - 기존 4 case (DatumName/ShotName/FAIName/MeasurementName, CO-31-01) 본문 보존

4. **HALCON overlay 'DETECT FAIL' 라벨** (본 plan 추가):
   - `HalconDisplayService.RenderDatumOverlay` 의 `RenderDatumFindResult` 호출 직후, 닫는 `}` 이전 분기 삽입
   - 조건: `datum.IsConfigured && !datum.LastFindSucceeded`
   - 위치: RefOriginRow - 40, RefOriginCol + 5
   - 색상: `"red"` 표준명 (memory feedback 준수)
   - try/catch swallow (기존 RenderDatumOverlay 컨벤션)

## Memory feedback 준수 증거

| Memory 항목 | 적용 위치 | 검증 |
|-------------|-----------|------|
| feedback_response_language (한국어) | 모든 사용자 응답 + 주석 | ✓ |
| feedback_halcon_setcolor_invalid_names | HOperatorSet.SetColor(window, "red") 만 사용 | grep `"light red/green/blue"` = 0 ✓ |
| feedback_comment_convention (//YYMMDD hbk) | 모든 신규 라인 끝 //260529 hbk Phase 39 WF-02 D-04 | grep DatumConfig.cs = 13건 ✓ |

## 회귀 가드

| 항목 | 위치 | 검증 |
|------|------|------|
| RenderDatumOverlay 200+ 줄 본문 | HalconDisplayService.cs:683-885 | 변경 0 |
| RenderDatumFindResult 메서드 | HalconDisplayService.cs:307+ | 변경 0 (Phase 17 D-13) |
| Phase 36 hotfix CO-36-03 호출 | HalconDisplayService.cs L889 | 보존 |
| DrawRoiLabelAt analog | HalconDisplayService.cs:912-938 | 변경 0 |
| DatumConfig 100+ 다른 필드 | L17-411 + L414+ | 변경 0 |
| NodeViewModel 기존 4 case (CO-31-01) | L213-227 | 본문 보존 |
| NodeViewModel ctor + INPC 구독 | L196-206 | 변경 0 |

## Build verification

| Files | msbuild |
|-------|---------|
| 3 파일 (HalconDisplayService.cs + DatumConfig.cs + NodeViewModel.cs) | PASS (errors 0, warnings 베이스라인) |

## Plan 04 UAT 검증 포인트

Test 3 (검출실패 시나리오):
- SIMUL 시나리오로 1 datum 검출 실패 유도 (TeachingImagePath 잘못된 경로 또는 ROI 검출 불가 영역)
- **시각 확인:** Datum_2 위치에 적색 'DETECT FAIL' 라벨 표시
- **데이터 진입점 확인:** `datum.HasDetectFail == true` (debug breakpoint 또는 XAML DataTemplate 후속 plan 의 적색 dot)
- **회귀 가드:** Datum_1 정상 십자 표시, 정상 datum 기반 FAI overlay `-OK`/`-NG` suffix 정상

XAML DataTemplate 적색 dot 추가 여부는 Plan 04 UAT 결과 따라 후속 plan / hotfix 결정 (본 plan 은 데이터 진입점까지만 책임).
