# Phase 15: HALCON MeasurePos 정합성 — measurePhi 명시 매핑 + EdgeSelection 명시 처리 - Context

**Gathered:** 2026-04-29
**Status:** Ready for planning
**Source:** Mid-session conversation (260429-c2e quick task UAT 노출 → 구조 결함 식별)

<domain>
## Phase Boundary

`DatumFindingService` 의 strip-loop 패턴(`AppendEdgePointsFromStrip`)과 `VisionAlgorithmService.TryFindCircleByPolarSampling` 의 MeasurePos 호출에서 **두 가지 구조적 누락**을 잡는다:

1. **EdgeDirection → measurePhi 명시 매핑 누락** — 현재 `SmallestRectangle2` 의 자동 도출 phi(`rp`)에 의존, 사용자의 BtoT vs TtoB 부호 의도를 구분 못 함 → MeasurePos polarity 의미가 뒤집혀 실 데이터에서 0 edges 반환.
2. **MeasurePos selection 하드코딩** — `"all"` 로 모든 후보 반환, `EdgeSelection` 필드가 `DatumConfig` 에 자체 부재.

이 둘을 **참조 올바른 구현 3건과 통일** (MeasurementAlgorithm.cs:130-178 / FAIEdgeMeasurementService.cs:87-102, 365 / VisionAlgorithmService.cs:63-72). Datum 6 ROI 전부 (Line1, Line2, Vertical, Circle, Horizontal_A, Horizontal_B) + 3 알고리즘(TwoLineIntersect / CTH / VTH) 영향.

Out of scope:
- HALCON 라이브러리 외부 변경
- Phase 14 의 polar sampling 회전 로직 자체(`rectPhi = thetaRad`)는 그대로 유지 — 정합성 명시(selection)만 정리.
- TwoLineIntersect 동작 자체 재설계는 안 함 — 같은 helper 를 쓰므로 선택적으로 영향받지만 기존 의도 유지.

</domain>

<decisions>
## Implementation Decisions (LOCKED)

### DatumConfig 확장 (6 ROI EdgeSelection 추가)
- 6개 EdgeSelection 프로퍼티 신규 추가: `Line1_EdgeSelection`, `Line2_EdgeSelection`, `Vertical_EdgeSelection`, `Circle_EdgeSelection`, `Horizontal_A_EdgeSelection`, `Horizontal_B_EdgeSelection`.
- 타입: `string`, 기본값 `"First"`. `[ItemsSourceProperty(nameof(*_EdgeSelectionList))]` 로 PropertyGrid 드롭다운 노출.
- ItemsSource list = `EdgeOptionLists.Selections` (신규 정적 리스트 또는 기존 헬퍼 확장) — 후보값 `["First", "Last", "All"]`.
- INI 하위호환: `EnsurePerRoiDefaults()` 에 `if (string.IsNullOrEmpty(*_EdgeSelection)) *_EdgeSelection = "First"` 6 라인 추가.
- 카테고리 prefix 라벨은 기존 EdgeDirection / EdgePolarity 패턴 그대로 유지.

### AppendEdgePointsFromStrip 확장
- 시그니처에 `string direction`, `string selection` 두 파라미터 추가 (기존 sigma/threshold/polarity 옆).
- 함수 안에서 `direction` → `measurePhi` 직접 매핑 (참조: MeasurementAlgorithm.cs:130-178):
  ```csharp
  double measurePhi;
  if (direction == "TtoB")      measurePhi = -Math.PI / 2.0;
  else if (direction == "BtoT") measurePhi = +Math.PI / 2.0;
  else if (direction == "RtoL") measurePhi = Math.PI;
  else                          measurePhi = 0.0;  // LtoR (default)
  ```
- `GenMeasureRectangle2(rr, rc, measurePhi, rh, rw, ...)` — 기존 `rp` 자동 도출 사용 중단.
- `MeasurePos(..., polarity, selection, ...)` — `"all"` 하드코딩 제거, 인자화.
- 기존 주석 (`SmallestRectangle2 가 strip Phi 자동 도출 → manual effectivePhi 불필요`) 제거 — 이게 잘못된 가정의 정당화 근원.

### TryFindLine / TryExtractEdgePoints 시그니처 확장
- 두 함수 모두 `selection` 파라미터 추가, AppendEdgePointsFromStrip 으로 그대로 전달.
- 이미 받고 있던 `direction` 도 함께 전달 (이전엔 받기만 하고 strip-loop 에 전달 안 함).
- `roiPhi` 파라미터는 일단 그대로 유지 (현재 미사용이지만 ROI 회전 보정 미래 확장용 — 지우지 않음).

### Circle (TryFindCircleByPolarSampling) 정리
- `MeasurePos(..., polarity, "all", ...)` + `eRows[0]` 인덱싱 패턴 → `MeasurePos(..., polarity, "first", ...)` + 결과 사용 단순화.
- `rectPhi = thetaRad` 회전 로직은 **그대로 유지** (Circle 의 polar sampling 의도 = 반경 방향 측정 + 360° sweep).
- `selection` 파라미터를 메서드 시그니처에 추가하여 caller(DatumFindingService.TryTeachCircleTwoHorizontal) 가 `Circle_EdgeSelection` 전달.

### 호출부 6 wiring 업데이트
- `DatumFindingService` 의 모든 TryFindLine / TryExtractEdgePoints 호출에 selection 추가:
  - Line 196-205 (TwoLineIntersect Line1)
  - Line 219-227 (TwoLineIntersect Line2)
  - Line 367-380 (CTH Horizontal_A)
  - Line 386-393 (CTH Horizontal_B)
  - Line 517-526 (VTH Vertical)
  - Line 540-552 (VTH Horizontal_A)
  - Line 561-573 (VTH Horizontal_B)
- 각각 `config.<ROI>_EdgeSelection` 인자 전달.
- TryTeachCircleTwoHorizontal 의 Circle 검출 호출에 `Circle_EdgeSelection` 전달.

### UAT 시나리오 (실데이터 회귀)
- TwoLineIntersect, CTH, VTH 3 알고리즘 각각 실 데이터로 EdgeDirection 4방향(LtoR/RtoL/TtoB/BtoT) 시나리오 + ROI 이동 후 재티칭 + #1405 quick task UAT 4건 흡수.
- SIMUL 회귀: SIMUL 데이터로도 3 알고리즘 PASS 확인 — Phase 14-05 SIMUL UAT 동일 시나리오 재실행.

### 코드 컨벤션
- 모든 수정 라인에 `//260429 hbk <reason>` 주석 (CLAUDE.md feedback 메모리).
- C# 7.2 한정, .NET Framework 4.8 (CLAUDE.md 제약).

### Claude's Discretion
- ItemsSourceProperty 패턴 — 기존 EdgeDirection/EdgePolarity 가 어떻게 되어 있는지 확인 후 같은 스타일로.
- EdgeOptionLists.Selections 정적 리스트의 위치 (별도 helper 또는 EdgeOptionLists 안에 정의).
- AppendEdgePointsFromStrip 의 Trace 로그 보강 여부 — 기존 로그(line 896-900)에 selection/measurePhi 추가하면 디버깅 편의 향상.
- 빈 catch 블록(line 1014-1017) 진단 로그 추가 — 별개 개선이지만 한 PR 안에 묶을지는 planner 판단.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 올바른 measurePhi 매핑 패턴 (참조 구현)
- `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs:130-178` — TwoLineInspect direction → measurePhi 삼항식 + EdgeSelection (First/Last/All) 처리
- `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs:82-106, 360-365` — direction → measurePhi 4-way 매핑 + roiPhi 회전 보정 + selection (first/last)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:63-90` — direction → measurePhi 4-way 매핑 + LightToDark/DarkToLight polarity 변환

### 수정 대상 파일
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — 메인 수정 대상
  - line 196-205, 219-227 (TwoLineIntersect)
  - line 367-380, 386-393 (CTH HorizontalA/B)
  - line 517-526, 540-552, 561-573 (VTH Vertical/HorizontalA/B)
  - line 717-858 (TryFindLine)
  - line 860-973 (TryExtractEdgePoints)
  - line 975-1023 (AppendEdgePointsFromStrip — 핵심 helper)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — TryFindCircleByPolarSampling line 214-340 selection 정리
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 6 ROI EdgeSelection 프로퍼티 추가, EnsurePerRoiDefaults() 확장

### 메모리 / 컨벤션
- `feedback_halcon_measurepos_must_haves.md` (이번 세션 신규) — measurePhi + EdgeSelection 체크리스트
- `feedback_comment_convention.md` — //YYMMDD hbk 주석 필수
- CLAUDE.md — C# 7.2 제약, K&R/Allman 스타일은 파일별 일관성 유지

### 선행 phase 컨텍스트
- Phase 14 SUMMARY (특히 14-04/14-05 — Circle polar sampling, btn_testFindDatum 통합 UAT)
- Quick 260429-c2e — #1405 IntersectionLl fix (실 UAT 4건 미수행 carry-over)

</canonical_refs>

<specifics>
## Specific Ideas

### 분할 단위 권고 (planner 입력)
1. **Plan 15-01**: DatumConfig 6 EdgeSelection 프로퍼티 + EdgeOptionLists.Selections + EnsurePerRoiDefaults INI 하위호환.
2. **Plan 15-02**: AppendEdgePointsFromStrip 시그니처 확장 + measurePhi 매핑 + selection 인자화 + Trace 로그 보강 (선택).
3. **Plan 15-03**: TryFindLine / TryExtractEdgePoints 시그니처 확장 + 호출부 7곳 wiring.
4. **Plan 15-04**: VisionAlgorithmService.TryFindCircleByPolarSampling selection 정리 + DatumFindingService.TryTeachCircleTwoHorizontal 호출 업데이트.
5. **Plan 15-05**: UAT — 3 알고리즘 × EdgeDirection 4방향 + #1405 carry-over 4건 + SIMUL 회귀.

### 의존성 / Wave 권고
- 15-01 → 15-02 → 15-03 (시그니처 변경 사슬, 순차 wave)
- 15-04 는 15-01 만 의존 (Circle 만 영향, parallel with 15-02/03 가능)
- 15-05 UAT 는 15-02/03/04 모두 완료 후

### 스킵 가능 / 옵션
- 빈 catch 진단 로그 강화는 선택 — quick fix 로 묶거나 별도 개선 phase.

</specifics>

<deferred>
## Deferred Ideas

- ROI 회전(Phi ≠ 0) 시 bounding box 재계산 — 현재 `Phi=0 저장 규약` 가정. ROI 회전 기능이 들어오면 그때 별도 phase.
- Datum 외 측정(MeasurementAlgorithm.cs / FAIEdgeMeasurementService.cs) 의 measurePhi/EdgeSelection — 이미 올바른 구현. 회귀 영향 없음 — 손대지 않음.
- TwoLineAngleToleranceDeg 같은 이미 완료된 게이트 로직 — Phase 14-02 에서 처리 끝. 영향 없음.

</deferred>

---

*Phase: 15-halcon-measurepos-measurephi-edgeselection-datumfindingservi*
*Context gathered: 2026-04-29 via mid-session conversation derivation*
