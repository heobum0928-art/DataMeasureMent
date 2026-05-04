---
phase: 15
plan: 03
subsystem: Datum / Halcon Vision (Circle Polar Sampling)
tags: [datum, halcon, measurepos, edge-selection, circle, polar-sampling, signature-change]
requires:
  - VisionAlgorithmService.TryFindCircleByPolarSampling (Phase 14-04 폴라 샘플링 회전 의도 — rectPhi=thetaRad 보존 필수)
  - DatumConfig.Circle_EdgeSelection (Plan 15-01 산출 — sentinel "" + EnsurePerRoiDefaults fallback="First")
  - MeasurementAlgorithm.cs:178 (CANONICAL PascalCase → Halcon lower-case 변환 패턴)
  - Plan 15-02 (DatumFindingService.cs same-file 수정 완료 → wave 분리)
provides:
  - TryFindCircleByPolarSampling(string selection) — 명시 selection 파라미터 + sanity clamp + selectionLower 변환
  - MeasurePos(..., polarity, selectionLower, ...) — "all" 하드코딩 제거
  - Accumulation policy 분기 — All=eRows 전체 / First/Last=eRows[0] 단일점 (Phase 14-04 stepCount 보존)
  - DatumFindingService.TryTeachCircleTwoHorizontal Circle 호출 wiring (config.Circle_EdgeSelection 전파)
affects:
  - CircleTwoHorizontal (TryTeachCircleTwoHorizontal teach) — Circle 검출 stage 만 영향
  - 기타 알고리즘 (TwoLineIntersect, VerticalTwoHorizontal) 영향 0 — 본 plan 은 Circle 분기만 수정
tech-stack:
  added: []
  patterns:
    - "Phase 14-04 polar sampling 의도 보존 (rectPhi=thetaRad, stepCount 360°/stepDeg) — selection 인자화로 무영향"
    - "PascalCase storage (DatumConfig) → lower-case Halcon API (selectionLower) 변환을 호출자 진입부에서 1회 처리"
    - "Selection-aware accumulation: All=N points/step (전체), First/Last=1 point/step (legacy)"
    - "Single-call site wiring (DatumFindingService 1곳, smoke harness는 자체 좌표계산만 — TryFindCircleByPolarSampling 미호출)"
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - "selection 위치: polarity 다음 / datumTransform 앞 — 의미 단위 그룹 (edge 파라미터 묶음)"
  - "Sanity clamp default = 'First' — Phase 15-01 EnsurePerRoiDefaults fbSelection 기본값과 일치"
  - "Accumulation 분기: All만 eRows 전체 누적, First/Last는 [0] 인덱스 유지 — Phase 14-04 의 360° × N 의미 + Halcon 의 first/last 모드 의미가 본질적으로 1점 반환이라는 사실 모두 만족"
  - "Caller scan 결과 1 caller (DatumFindingService.TryTeachCircleTwoHorizontal) — RunPhiSmokeTest 는 좌표 계산식만 노출 (TryFindCircleByPolarSampling 미호출), 추가 wiring 불필요"
  - "Phase 14-04 D-13 rectPhi=thetaRad 핵심 회전식 변경 0 라인 — 본 plan 의 anti-goal 준수"
metrics:
  duration: "~5 min"
  completed: "2026-04-29"
  tasks: 2
  files_modified: 2
  lines_added: 20
  lines_removed: 5
  commits: 2
---

# Phase 15 Plan 03: VisionAlgorithmService TryFindCircleByPolarSampling Selection 정리 Summary

`TryFindCircleByPolarSampling` 의 MeasurePos 가 `"all"` 로 하드코딩 + `eRows[0]` 단일 인덱싱이라는 시멘틱 모순(요청은 all 인데 실제론 first 효과)을 정리. 시그니처에 `string selection` 파라미터 추가, PascalCase→lower 변환 후 MeasurePos 인자로 전달, 누적 정책을 selection 별 분기 (All=전체 / First/Last=단일점) 로 명시. Phase 14-04 의 `rectPhi = thetaRad` 회전 의도(반경 방향 측정, 화면 CCW 좌표)는 무변경 — 본 plan 은 selection 인자화에만 한정. DatumFindingService.TryTeachCircleTwoHorizontal 1 caller wiring 으로 사용자 PropertyGrid 의도(`Circle_EdgeSelection`)가 360° polar sampling 까지 일관 전파됨.

## What Changed

### Task 1: VisionAlgorithmService.TryFindCircleByPolarSampling 시그니처 + selection 인자화 (commit `dbde085`)

**Signature (line 214-222) — Before/After:**

```csharp
// Before
public bool TryFindCircleByPolarSampling(
    HImage image,
    double centerRow, double centerCol, double radius,
    double stepDeg, double rectL1Ratio, double rectL2Ratio,
    double sigma, int threshold, string polarity,
    HTuple datumTransform, ...)

// After
public bool TryFindCircleByPolarSampling(
    HImage image,
    double centerRow, double centerCol, double radius,
    double stepDeg, double rectL1Ratio, double rectL2Ratio,
    double sigma, int threshold, string polarity, string selection, //260429 hbk Phase 15
    HTuple datumTransform, ...)
```

**Sanity clamp + lower 변환 (line 239-243):**
```csharp
//260429 hbk Phase 15 — selection sanity clamp + PascalCase → Halcon lower-case 변환
if (string.IsNullOrEmpty(selection)) selection = "First";
string selectionLower =
    string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase) ? "last" :
    string.Equals(selection, "All",  StringComparison.OrdinalIgnoreCase) ? "all"  : "first";
```

**MeasurePos 호출 + selection-aware 누적 (line 295-313):**
```csharp
// Before: "all" 하드코딩 + eRows[0] 단일 인덱싱
HOperatorSet.MeasurePos(image, measureHandle, sigma, threshold, polarity, "all", ...);
HOperatorSet.TupleConcat(allRows, eRows[0], out allRows);  // 시멘틱 모순

// After: caller selection 반영 + 정책 분기
HOperatorSet.MeasurePos(image, measureHandle,
    sigma, threshold, polarity, selectionLower, //260429 hbk Phase 15
    out eRows, out eCols, out amp, out dist);

if (eRows.TupleLength() > 0 && eCols.TupleLength() > 0)
{
    //260429 hbk Phase 15 — selection 정책 분기
    if (string.Equals(selectionLower, "all", StringComparison.OrdinalIgnoreCase))
    {
        HOperatorSet.TupleConcat(allRows, eRows, out allRows);
        HOperatorSet.TupleConcat(allCols, eCols, out allCols);
    }
    else
    {
        // First/Last: Halcon 자체가 1점 반환 → eRows[0]
        HOperatorSet.TupleConcat(allRows, eRows[0], out allRows);
        HOperatorSet.TupleConcat(allCols, eCols[0], out allCols);
    }
}
```

**Phase 14-04 회전 의도 보존 (line 280-285) — 변경 0 라인:**
```csharp
double thetaRad = thetaDeg * Math.PI / 180.0;
double rectRow = cRow - radius * Math.Sin(thetaRad);  // 화면 CCW (sin 앞 minus)
double rectCol = cCol + radius * Math.Cos(thetaRad);
double rectPhi = thetaRad; // 반경 방향 = rect length1 축 — 보존
```

### Task 2: DatumFindingService.TryTeachCircleTwoHorizontal Circle_EdgeSelection wiring (commit `b8e3a60`)

```csharp
// line 342-350 — 인자 1개 추가 (polarity 다음, datumTransform=null 앞)
if (!visionSvc.TryFindCircleByPolarSampling(
        image,
        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
        config.Circle_PolarStepDeg, config.Circle_RectL1Ratio, config.Circle_RectL2Ratio,
        config.Circle_Sigma, config.Circle_EdgeThreshold, config.Circle_EdgePolarity,
        config.Circle_EdgeSelection, //260429 hbk Phase 15 — Circle_EdgeSelection 전파
        null, // teaching-phase identity transform (legacy 동일)
        out centerRow, out centerCol, out radius,
        out circleEdgeRows, out circleEdgeCols,
        out circleError))
```

## Caller Verification Table

| # | 호출 사이트 | 파일 / 라인 | 처리 | 추가 인자 |
|---|---|---|---|---|
| 1 | TryTeachCircleTwoHorizontal | `DatumFindingService.cs:342` | Task 2 wiring | `config.Circle_EdgeSelection` |
| — | RunPhiSmokeTest | `VisionAlgorithmService.cs:353` | **TryFindCircleByPolarSampling 미호출** — 자체 sin/cos 계산식만 trace 로그로 노출 (Phase 14 W1 부호 검증 harness, dormant). 수정 불필요. |

**전체 솔루션 grep 결과 (정의 라인 + 주석 제외):**
```bash
$ grep -rn "TryFindCircleByPolarSampling(" WPF_Example/ | grep -v "public bool TryFindCircleByPolarSampling"
WPF_Example/Halcon/Algorithms/DatumFindingService.cs:342:                if (!visionSvc.TryFindCircleByPolarSampling(
```

→ 단일 caller 확인. 본 plan 범위에서 모든 caller 가 `string selection` 인자 전달.

## Grep Evidence

```
$ grep -c "selectionLower" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
3   # (1) 변환 변수 정의 (2) MeasurePos 인자 (3) All 분기 비교

$ grep "MeasurePos.*\"all\"" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
(no matches)   # "all" 하드코딩 제거 확인

$ grep -c "rectPhi = thetaRad" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
1   # Phase 14-04 D-13 의도 보존 (line 285)

$ grep -c "string polarity, string selection" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
1   # 시그니처 변경 (line 218)

$ grep -c "config.Circle_EdgeSelection" WPF_Example/Halcon/Algorithms/DatumFindingService.cs
1   # Task 2 wiring (line 347)
```

## Build Verification

```
$ MSBuild.exe WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe
```

**0 NEW warnings on Phase 15 수정 범위 (VisionAlgorithmService.cs line 214-340 / DatumFindingService.cs line 342-350).**

Pre-existing carry-over warnings (Phase 15-02 SUMMARY 기록과 동일 — 수정 범위 외):
- `Halcon\Algorithms\VisionAlgorithmService.cs(64,22): warning CS0219` — `scanHorizontal` 미사용 (line 64, 본 plan 범위 외)
- `Device\Camera\VirtualCamera.cs(266,13): warning CS0162` — 별개 모듈
- `MSB3884: MinimumRecommendedRules.ruleset` — 프로젝트 메타 (코드 영향 0)

## Deviations from Plan

None — 2 task 모두 PLAN.md 명시대로 실행. Caller scan 결과 추가 wiring 불필요(`RunPhiSmokeTest` 가 TryFindCircleByPolarSampling 을 호출하지 않음 — 자체 sin/cos 식만 노출하는 dormant smoke harness) → Plan Task 1 의 "발견된 caller 가 DatumFindingService.cs 의 1 사이트만이라면 본 Task 에서는 추가 작업 없음" 분기 확정.

## TDD Gate Compliance

본 plan 은 `type: execute`, `tdd="false"` — TDD 게이트 적용 대상 아님. Circle 검출 회귀 검증은 Plan 15-04 UAT (실데이터 SIMUL_MODE) 에서 EdgeSelection First/Last/All 3 시나리오로 수행 예정.

## Compatibility Notes

- **Forward (사용자 의도 정합):** PropertyGrid 에서 Circle_EdgeSelection = "First"/"Last"/"All" 선택이 360° polar sampling 의 각 step MeasurePos 까지 도달. 기존엔 모든 step 이 "all" 모드로 호출되고 결과는 `eRows[0]` 으로 단일점만 누적되는 시멘틱 모순(첫 점 효과 = first 와 동등이지만 사용자 의도 미반영) 제거.
- **Backward:** Plan 15-01 의 `EnsurePerRoiDefaults` 가 INI 부재 시 `Circle_EdgeSelection = "First"` 로 채움 → 본 plan 진입 시 sanity clamp `if (string.IsNullOrEmpty(selection)) selection = "First";` 도 동일 기본값으로 fallback. 신규 INI 필드 미적용 환경의 동작은 기존 Phase 14-04 와 동등 (Halcon 이 first 모드에서 1점 반환 → `eRows[0]` 인덱스로 누적 = 기존 결과 byte-identical).
- **Behavior change scope:** CircleTwoHorizontal 알고리즘만 영향. TwoLineIntersect / VerticalTwoHorizontal 의 TryFindLine / TryExtractEdgePoints 경로는 무변경 (Plan 15-02 에서 별도 처리 완료).
- **Plan 14-04 회귀 가드:** `rectPhi = thetaRad` (line 285), 화면 CCW 좌표식 (line 282-284), 360° stepCount 루프 (line 277) 모두 변경 0 라인. SIMUL smoke (Plan 14-04 W1) PASS 가정 유지.

## Threat Flags

없음 — 본 plan 은 helper 시그니처 + 인자 wiring + 누적 정책 분기 only. 새로운 trust boundary / network surface / file access / 외부 입력 도입 없음. 보안 측면 영향 0.

## Self-Check: PASSED

**Files:**
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — FOUND, modified (Task 1 — +19 / -5 lines)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — FOUND, modified (Task 2 — +1 / -0 lines)

**Commits:**
- `dbde085` (Task 1 — TryFindCircleByPolarSampling 시그니처 + selection 인자화 + 누적 정책 분기) — FOUND in `git log`.
- `b8e3a60` (Task 2 — DatumFindingService Circle_EdgeSelection wiring + 통합 빌드 검증) — FOUND in `git log`.

**Build:** msbuild Debug/x64 PASS, 0 신규 warning on 수정 범위.

**Done criteria 검증:**
- [x] TryFindCircleByPolarSampling 시그니처에 `string selection` 추가 (polarity 다음)
- [x] sanity clamp `if (string.IsNullOrEmpty(selection)) selection = "First";`
- [x] PascalCase → lower 변환 (`selectionLower` 변수, MeasurementAlgorithm.cs:178 CANONICAL 패턴)
- [x] MeasurePos 가 `selectionLower` 사용 ("all" 하드코딩 제거 — grep 0 매치)
- [x] selection=="all" 분기에서 eRows 전체 누적, 그 외 [0] 단일점 누적 — Phase 14-04 stepCount 보존
- [x] `rectPhi = thetaRad` 회전 의도 보존 (변경 0 라인)
- [x] DatumFindingService.TryTeachCircleTwoHorizontal Circle 호출에 `config.Circle_EdgeSelection` 전달
- [x] 모든 caller scan 완료 — 1 caller 확정 (smoke harness 는 미호출)
- [x] msbuild Debug/x64 PASS, 신규 warning 0
