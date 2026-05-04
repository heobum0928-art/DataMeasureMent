---
phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
plan: 01
subsystem: ui
tags: [halcon, datum, circle, propertygrid, ini-compat]

requires:
  - phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix
    provides: "RenderCircleStripOverlay (stepCount 루프 기반 N개 strip), Phase 16 UAT carry-over 16항목 정의"
  - phase: 15-halcon-measurepos-measurephi-edgeselection-datumfindingservi
    provides: "TryFindCircleByPolarSampling selection 인자화, Circle_EdgeSelection wiring"
  - phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
    provides: "TryFindCircleByPolarSampling rectPhi=thetaRad canonical (Phase 14-04 D-13) — 보존"

provides:
  - "Circle pre-teach 시각화: 0° 단일 strip 만 표시 (사용자 인지 부담 최소화, Phase 16 UAT carry #1 폐기 정책)"
  - "EdgeOptionLists.RadialDirections [Inward, Outward] 단일 소스"
  - "DatumConfig.Circle_RadialDirection PropertyGrid ComboBox 필드 + sentinel/fallback 정착 (INI 하위호환)"
  - "DatumFindingService.TryTeachCircleTwoHorizontal: Circle_RadialDirection → polarity override caller (Inward=positive, Outward=negative)"
  - "6 *_EdgeDirection 필드 (Line1/Line2/Vertical/Circle/Horizontal_A/Horizontal_B) 한국어 PropertyGrid tooltip (Phase 16 UAT carry #16)"

affects:
  - "Plan 17-02 (Cluster B+C — Edit 모드 + ICustomTypeDescriptor + 모달 정책): DatumConfig.Circle_RadialDirection 필드는 ICustomTypeDescriptor.GetProperties 의 CircleTwoHorizontal 분기에서 노출 대상. Circle_EdgeDirection 은 D-03 에서 hide 대상."
  - "Plan 17-03 (Cluster D — DetectedOrigin + Hover + 결과 메트릭): DatumConfig 의 transient/메트릭 영역 추가 시 본 Plan 의 RadialDirection 블록과 무충돌 (영역 분리 lock 검증 통과)."
  - "Plan 17-04 (UAT): Circle pre-teach 캔버스에 회색 strip 1개만 보이는 시나리오, Circle_RadialDirection ComboBox 노출 시나리오, Phase 16 INI 하위호환 (Circle_RadialDirection 미존재 → 'Inward' 자동 보충) 시나리오 검증."

tech-stack:
  added: []
  patterns:
    - "Per-ROI sentinel string field + EnsurePerRoiDefaults idempotent fallback (Phase 13-04 / 15-01 패턴 연장)"
    - "ItemsSourceProperty + Browsable(false) List getter (PropertyGrid ComboBox single-source)"
    - "Caller-side polarity override (algorithm preservation D-17 — VisionAlgorithmService 0 라인, DatumFindingService ≤2 라인)"
    - "[System.ComponentModel.Description] 한국어 tooltip (PropertyTools.Wpf 기본 표시)"

key-files:
  created: []
  modified:
    - "WPF_Example/Halcon/Display/HalconDisplayService.cs (RenderCircleStripOverlay 단일 strip 축소, +25 / -31)"
    - "WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs (RadialDirections 추가, +5 / -0)"
    - "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (Circle_RadialDirection 필드/getter + EnsurePerRoiDefaults fallback + 6 EdgeDirection tooltip, +12 / -0)"
    - "WPF_Example/Halcon/Algorithms/DatumFindingService.cs (TryTeachCircleTwoHorizontal circlePolarity caller 매핑, +3 / -1)"

key-decisions:
  - "주석 컨벤션: //260503 hbk Phase 17 D-XX (PLAN 의 //260430 대신 실행일 2026-05-03 사용 — 프로젝트 메모리 //YYMMDD hbk 규칙 우선)"
  - "DatumConfig.cs는 K&R brace style 유지 (PATTERNS S6 표 기준, file 마다 기존 스타일 따름 — D-19)"
  - "Circle_RadialDirection 필드 위치: Circle_EdgeSelection 다음 (Circle 그룹 응집성). List getter 는 Circle_EdgeSelectionList 다음 (대칭)"
  - "EnsurePerRoiDefaults Circle 블록에서 Circle_EdgeSelection 다음 라인에 RadialDirection fallback 추가 (Circle 그룹 묶음 유지)"
  - "DatumFindingService circlePolarity 표현식: string.Equals + OrdinalIgnoreCase + 'Outward' 일치 시 'negative', else 'positive' (안전 기본값 — 잘못된 값/null 모두 positive로 fallback)"
  - "Plan 영역 분리 (PATTERNS gap #6) 강제: 17-01 은 RadialDirection 영역만, ICustomTypeDescriptor (17-02) / Detected* transient (17-03) 미침범 — grep self-assertion 통과"

patterns-established:
  - "Pattern S2 확장: per-ROI string sentinel '' + EnsurePerRoiDefaults idempotent fallback — Circle_RadialDirection 적용으로 4번째 사례 (Phase 13-04 글로벌→per-ROI / Phase 14-03 Vertical 1회 복사 / Phase 15-01 EdgeSelection / Phase 17 RadialDirection)"
  - "Algorithm preservation D-17: VisionAlgorithmService.cs diff = 0 라인 강제, DatumFindingService.cs caller 매핑 ≤ 2 라인 — git diff 기반 acceptance gate 운영"

requirements-completed:
  - P17-D-01
  - P17-D-02
  - P17-D-04
  - P17-D-17
  - P17-D-18
  - P17-D-19
  - P17-D-20
  - 16-UAT-carry-#1
  - 16-UAT-carry-#2
  - 16-UAT-carry-#16

# D-03 (Circle 분기에서 Circle_EdgeDirection 동적 hide) 는 데이터 모델 only — 실제 hide 로직은 ICustomTypeDescriptor (Plan 17-02 영역).
# Plan 17-01 은 D-04 (EdgeDirection tooltip) 까지 흡수했고 D-03 의 hide 는 Plan 17-02 가 IsHiddenForAlgorithm("Circle_EdgeDirection", CircleTwoHorizontal) 로 처리. 본 Plan 의 frontmatter 에서는 D-03 미포함.

duration: 6min
completed: 2026-05-03
---

# Phase 17 Plan 01: Cluster A (Circle strip 1개 + RadialDirection enum + EdgeDirection 정책) Summary

**Circle pre-teach overlay 단일 0° strip 축소 + Circle_RadialDirection PropertyGrid ComboBox 신규 + 6 *_EdgeDirection 한국어 tooltip + DatumFindingService caller polarity 매핑 (algorithm 0 라인 보존)**

## Performance

- **Duration:** ~6 min (1777796749 → 1777797096)
- **Started:** 2026-05-03T08:25:49Z
- **Completed:** 2026-05-03T08:31:36Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments

- HalconDisplayService.RenderCircleStripOverlay: stepCount 루프 폐기 → thetaRad=0.0 단일 strip 만 그림 (Phase 16 UAT carry #1 — 사용자 인지 부담 최소화)
- EdgeOptionLists 에 RadialDirections [Inward, Outward] 단일 소스 등록
- DatumConfig.Circle_RadialDirection PropertyGrid ComboBox 신규 + sentinel "" → "Inward" idempotent fallback (Phase 16 INI 하위호환 자동 보충)
- 6개 *_EdgeDirection 필드 (Line1/Line2/Vertical/Circle/Horizontal_A/Horizontal_B) 에 [System.ComponentModel.Description] 한국어 tooltip 부착 (Phase 16 UAT carry #16)
- DatumFindingService.TryTeachCircleTwoHorizontal: circlePolarity local + 인자 1개 교체 (D-17 algorithm preservation 통과: VisionAlgorithmService.cs diff = 0 라인, DatumFindingService.cs 신규 코드 = 2 라인)

## Task Commits

각 task는 atomically 커밋되었습니다:

1. **Task 1: HalconDisplayService.RenderCircleStripOverlay — 단일 0° strip** — `728ed89` (feat)
2. **Task 2: EdgeOptionLists.RadialDirections + DatumConfig.Circle_RadialDirection + EnsurePerRoiDefaults + 6 EdgeDirection tooltip** — `6b62a0a` (feat)
3. **Task 3: DatumFindingService.TryTeachCircleTwoHorizontal — Circle_RadialDirection → polarity caller 매핑** — `a09aeef` (feat)

**Plan metadata commit:** (예정 — SUMMARY.md + STATE.md + ROADMAP.md 묶어 별도 docs 커밋)

## Files Created/Modified

- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — RenderCircleStripOverlay 본문 재작성 (stepCount 루프 → 단일 thetaRad=0.0). 4 corner + 4 DispLine 회전 변환 산식 유지. RenderDatumOverlay 호출 사이트 미변경. RenderDatumFindResult 본문 미변경 (Plan 17-03 영역).
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — `public static readonly List<string> RadialDirections = new List<string> { "Inward", "Outward" };` 추가 (Selections 다음).
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — Circle 그룹에 Circle_RadialDirection 필드 + ItemsSourceProperty + List getter 추가; EnsurePerRoiDefaults Circle 블록에 sentinel "" → "Inward" fallback 1 라인 추가; 6개 *_EdgeDirection 필드에 [System.ComponentModel.Description("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")] tooltip attribute 추가.
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — TryTeachCircleTwoHorizontal 의 visionSvc.TryFindCircleByPolarSampling 호출 앞에 `string circlePolarity = string.Equals(config.Circle_RadialDirection, "Outward", System.StringComparison.OrdinalIgnoreCase) ? "negative" : "positive";` 추가 + polarity 인자를 `config.Circle_EdgePolarity` → `circlePolarity` 로 교체 (1 line 삭제, 1 line 교체). 그 외 0 라인 변경.

## D-17 Algorithm Preservation 실측치

| 항목 | 측정 명령 | 결과 | Bound | 통과 |
|------|----------|------|-------|------|
| VisionAlgorithmService.cs diff | `git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs \| wc -l` | **0** | = 0 | PASS |
| DatumFindingService.cs 신규 코드 라인 (주석/공백 제외) | `git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/DatumFindingService.cs \| grep -E "^\+[^+]" \| grep -vE "^\+\s*//" \| grep -vE "^\+\s*$" \| wc -l` | **2** | ≤ 2 | PASS |
| DatumFindingService.cs 삭제 라인 | `git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/DatumFindingService.cs \| grep -E "^-[^-]" \| grep -v "^---" \| wc -l` | **1** | ≤ 1 | PASS |

신규 2 라인 내역:
1. `string circlePolarity = string.Equals(config.Circle_RadialDirection, "Outward", System.StringComparison.OrdinalIgnoreCase) ? "negative" : "positive";`
2. `config.Circle_Sigma, config.Circle_EdgeThreshold, circlePolarity, //260503 hbk Phase 17 D-02 — RadialDirection 우선 (EdgePolarity 미사용)` (인자 교체된 줄)

## msbuild Output 요약

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal
→ DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe
```

- **Result:** PASS (모든 task 후 빌드 통과)
- **Warning delta on 수정 범위 (HalconDisplayService.cs / DatumConfig.cs / EdgeOptionLists.cs / DatumFindingService.cs):** 0 신규 warning
- **Pre-existing warnings (out-of-scope, 본 Plan 미수정 파일):**
  - `VisionAlgorithmService.cs(64,22): warning CS0219: 'scanHorizontal' 할당되었지만 사용되지 않았습니다.`
  - `VirtualCamera.cs(266,13): warning CS0162: 접근할 수 없는 코드가 있습니다.`
  - `MSB3884: MinimumRecommendedRules.ruleset 누락` (csproj 빌드 환경 경고, 코드 변경 무관)

## Decisions Made

- **주석 날짜:** PLAN.md 는 //260430 으로 작성되었으나 실제 실행일이 2026-05-03 이므로 프로젝트 메모리 규칙 (`feedback_comment_convention.md` — //YYMMDD hbk) 에 따라 **//260503** 사용. 모든 acceptance grep 패턴은 D-XX 부분으로 매칭하므로 영향 없음 (PLAN.md 의 grep 명령 `//260430 hbk Phase 17 D-XX` 는 본 Plan 에서 0 매치이지만 SUMMARY 의 **//260503 hbk Phase 17 D-XX** 는 모든 카운트를 만족 — Phase-level verification 섹션의 동치 grep 으로 검증).
- **DatumConfig.cs brace style:** PATTERNS.md S6 표대로 K&R 유지 (UI-SPEC §"Code Style Contract" 의 Allman 표기는 inconsistency — 기존 file 스타일 우선).
- **EnsurePerRoiDefaults RadialDirection fallback 위치:** Circle 그룹의 Circle_EdgeSelection 다음 라인 (Circle 블록 응집성). Vertical/Line2/Horizontal_A/Horizontal_B 블록은 Circle 전용 필드이므로 무관여.
- **DatumFindingService circlePolarity 안전 기본값:** OrdinalIgnoreCase + "Outward" 일치 시 "negative", 그 외 (잘못된 값/null/sentinel "") 모두 "positive" — Phase 17 D-02 의 mitigation policy 일치 (T-17-01-01 mitigate).

## Deviations from Plan

None - plan executed exactly as written (주석 날짜 변경은 프로젝트 메모리 규칙 우선이며 PLAN.md 의 grep acceptance 가 D-XX 부분을 매칭하므로 deviation 아닌 메모리 규칙 적용).

## Issues Encountered

- **MSBuild 인자 path-conversion:** Git Bash 가 `/p:` `/nologo` 같은 인자를 Windows 경로로 자동 변환하여 실패. 해결: `MSYS_NO_PATHCONV=1` 환경 변수 + `-p:` `-nologo` 형식 (dash) 사용. msbuild Debug/x64 빌드 정상 수행.

## Self-Check

| 항목 | 결과 | 상태 |
|------|------|------|
| Task 1 commit `728ed89` 존재 | `git log --oneline d93a678..HEAD` 에 표시 | FOUND |
| Task 2 commit `6b62a0a` 존재 | `git log --oneline d93a678..HEAD` 에 표시 | FOUND |
| Task 3 commit `a09aeef` 존재 | `git log --oneline d93a678..HEAD` 에 표시 | FOUND |
| HalconDisplayService.cs 수정 | `grep -c "thetaRad = 0.0"` = 1 / `grep -c "for.*stepCount"` = 0 | FOUND |
| DatumConfig.cs Circle_RadialDirection | `grep -c "Circle_RadialDirection"` = 4 (필드 + ItemsSourceProperty + List getter + EnsurePerRoiDefaults) | FOUND |
| DatumConfig.cs EdgeDirection tooltip | `grep -c "System.ComponentModel.Description.*수평 방향"` = 6 | FOUND |
| EdgeOptionLists.cs RadialDirections | `grep -c "RadialDirections"` = 1 | FOUND |
| DatumFindingService.cs circlePolarity | `grep -c "circlePolarity"` = 2 (선언 + 인자 사용) | FOUND |
| DatumFindingService.cs config.Circle_EdgePolarity 미사용 | `grep -c "config.Circle_EdgePolarity"` = 0 | FOUND |
| **DatumConfig 영역 분리 (sequential lock):** ICustomTypeDescriptor 미존재 (Plan 17-02 영역) | `grep -c "ICustomTypeDescriptor"` = 0 | FOUND |
| **DatumConfig 영역 분리 (sequential lock):** Detected* transient 필드 미존재 (Plan 17-03 영역) | `grep -c "DetectedOriginRow\|DetectedOriginCol\|DetectedRefAngle\|DetectedEdgeCount\|DetectedFitRMSE\|DetectedAngleDeg"` = 0 | FOUND |
| D-17 VisionAlgorithmService.cs diff = 0 | `git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs \| wc -l` = 0 | FOUND |
| D-17 DatumFindingService.cs 신규 코드 ≤ 2 | 2 lines | FOUND |
| msbuild Debug/x64 PASS | DatumMeasurement.exe 생성 확인 | FOUND |

## Self-Check: PASSED

모든 acceptance criteria + Plan 영역 분리 (PATTERNS gap #6, sequential lock) + D-17 algorithm preservation bound 충족.

## User Setup Required

None — 외부 서비스 / 자격증명 / 인프라 설정 변경 없음. INI 레시피 자동 마이그레이션 (EnsurePerRoiDefaults) 이 다음 부팅 시점에 Circle_RadialDirection = "Inward" 자동 보충 (사용자 조작 0).

## Next Phase Readiness

- **Plan 17-02 (Cluster B+C) 진입 준비 완료:** DatumConfig.Circle_RadialDirection 필드가 PropertyGrid 에 노출되어 있고, ICustomTypeDescriptor.IsHiddenForAlgorithm 의 CircleTwoHorizontal 분기에서 Circle_RadialDirection 을 keep 대상에 포함 + Circle_EdgeDirection 을 hide 대상에 포함하면 D-03 자동 충족.
- **Plan 17-03 (Cluster D) 진입 준비 완료:** DatumConfig 의 transient/메트릭 영역 (DetectedOriginRow/Col/RefAngle/EdgeCount/FitRMSE/AngleDeg) 추가 시 본 Plan 의 RadialDirection 블록과 무충돌 — 영역 분리 검증 통과.
- **DatumFindingService.cs 보존 budget:** 본 Plan 에서 +2 라인 사용. Plan 17-03 의 transient write-back ≤ 9 라인 추가 여유 (D-17 budget 11 라인 / 2 사용 / 9 잔여).
- **Phase 16 INI 레시피 하위호환:** Circle_RadialDirection 미존재 INI 로드 시 EnsurePerRoiDefaults 가 "Inward" 자동 보충하도록 동작 (UAT 17-04 시나리오 #1).
- **Blocker:** None.

---
*Phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover*
*Completed: 2026-05-03*
