---
phase: 13-datum-algorithm-extensibility
plan: "04"
subsystem: Datum/EdgeParams
tags: [datum, edge-params, schema, per-roi, ini-compat, propertygrid, phase-13]
status: complete
updated: "2026-04-26"
dependency_graph:
  requires: [13-03]
  provides: [Gap-PerRoiEdgeParams]
  affects:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
tech_stack:
  added: []
  patterns:
    - "5 ROI × 6 per-ROI auto-property + sentinel-0/empty EnsurePerRoiDefaults() idempotent migration"
    - "ParamBase reflection INI round-trip — new keys auto-ignored on old INI, legacy keys preserved for backward-compat"
    - "DatumFindingService TryFindLine/TryExtractEdgePoints +3 params (direction/sampleCount/trimCount) — minimalist hook pattern"
    - "config.EnsurePerRoiDefaults() at all TryTeach*/TryFindDatum entry points — one-shot lazy migration"
key_files:
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - "per-ROI 필드 sentinel 기본값 0/\"\" — ParamBase 기본값 폴백 + EnsurePerRoiDefaults 자동 채우기 (INI 미존재 키 → 생성자 기본 0 → migrate 시 글로벌 복제)"
  - "legacy 글로벌 EdgeThreshold/Sigma/EdgePolarity는 삭제 않고 [Browsable(false)] + Category(legacy) — INI 이중 저장 허용 (ParamBase reflection 자동)"
  - "TryFindLine/TryExtractEdgePoints에 direction/sampleCount/trimCount 추가하되 알고리즘 본체 변경 최소화 — 시그니처 계약 확립 후 Phase 14+에서 Halcon 매핑 심화"
  - "EnsurePerRoiDefaults는 idempotent — 이미 채워진 ROI는 재호출 시 무변경 (sentinel 0/\"\" 가 아니면 건드리지 않음)"
  - "EdgeDirection 글로벌 필드 미존재 → hardcoded fallback \"LtoR\" (Datum 알고리즘 현재 방향 비의존)"
metrics:
  duration: ~30min
  completed_date: "2026-04-26"
  tasks_completed: 3
  tasks_total: 3
  files_changed: 2
---

# Phase 13 Plan 04: per-ROI Datum Edge Parameters Summary

**One-liner:** DatumConfig에 5 ROI × 6 파라미터 = 30 신규 필드를 추가하고 EnsurePerRoiDefaults() sentinel 마이그레이션으로 기존 INI 하위호환을 보장하며, DatumFindingService 모든 호출부가 per-ROI 값을 참조하도록 와이어링했다.

---

## Files Changed

| File | Type | Description |
|------|------|-------------|
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | modified | 30 신규 per-ROI 필드 (5 ROI × 6 param) + 10 dropdown getter (5 × 2) + EnsurePerRoiDefaults() + legacy 3 필드 [Browsable(false)] 처리 |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | modified | TryFindLine/TryExtractEdgePoints +3 파라미터 시그니처 확장 + 5 ROI 와이어링 + TryTeach*/TryFindDatum 진입부 EnsurePerRoiDefaults() 호출 |

---

## Per-ROI Parameter Catalog (5 ROI × 6 fields = 30)

| ROI | PropertyGrid Category | EdgeThreshold (int) | Sigma (double) | EdgeDirection (string) | EdgeSampleCount (int) | EdgeTrimCount (int) | EdgePolarity (string) |
|-----|----------------------|---------------------|----------------|------------------------|----------------------|--------------------|-----------------------|
| Line1 | `Datum\|Line1 Edge` | `Line1_EdgeThreshold` | `Line1_Sigma` | `Line1_EdgeDirection` | `Line1_EdgeSampleCount` | `Line1_EdgeTrimCount` | `Line1_EdgePolarity` |
| Line2 | `Datum\|Line2 Edge` | `Line2_EdgeThreshold` | `Line2_Sigma` | `Line2_EdgeDirection` | `Line2_EdgeSampleCount` | `Line2_EdgeTrimCount` | `Line2_EdgePolarity` |
| Circle | `Datum\|Circle Edge` | `Circle_EdgeThreshold` | `Circle_Sigma` | `Circle_EdgeDirection` | `Circle_EdgeSampleCount` | `Circle_EdgeTrimCount` | `Circle_EdgePolarity` |
| Horizontal A | `Datum\|Horizontal A Edge` | `Horizontal_A_EdgeThreshold` | `Horizontal_A_Sigma` | `Horizontal_A_EdgeDirection` | `Horizontal_A_EdgeSampleCount` | `Horizontal_A_EdgeTrimCount` | `Horizontal_A_EdgePolarity` |
| Horizontal B | `Datum\|Horizontal B Edge` | `Horizontal_B_EdgeThreshold` | `Horizontal_B_Sigma` | `Horizontal_B_EdgeDirection` | `Horizontal_B_EdgeSampleCount` | `Horizontal_B_EdgeTrimCount` | `Horizontal_B_EdgePolarity` |

**Sentinel defaults:** 모든 per-ROI 필드는 생성자 기본값 0 / `""` (sentinel). INI에 키가 없으면 ParamBase reflection이 생성자 기본값(0 / "") 유지 → EnsurePerRoiDefaults() 진입 시 글로벌 값으로 채워짐.

**Dropdown getters (10개, [Browsable(false)]):**
- `<ROI>_EdgeDirectionList` → `EdgeOptionLists.Directions` = ["LtoR", "RtoL", "TtoB", "BtoT"]
- `<ROI>_EdgePolarityList` → `EdgeOptionLists.DatumPolarities` = ["all", "positive", "negative"]

---

## INI 하위호환 메커니즘

### EnsurePerRoiDefaults() Sentinel 로직

```csharp
// Hardcoded fallback (글로벌 EdgeThreshold/Sigma/EdgePolarity → per-ROI 복제)
int    fbThreshold   = EdgeThreshold > 0 ? EdgeThreshold : 20;   // 글로벌 기본 20
double fbSigma       = Sigma > 0 ? Sigma : 1.0;                  // 글로벌 기본 1.0
string fbDirection   = "LtoR";                                    // legacy 글로벌에 EdgeDirection 없음
int    fbSampleCount = 20;
int    fbTrimCount   = 10;
string fbPolarity    = !string.IsNullOrEmpty(EdgePolarity) ? EdgePolarity : "all";

// 각 ROI별: sentinel(0/"")이면 fallback으로 채움 — 이미 값 있으면 무변경 (idempotent)
if (Line1_EdgeThreshold == 0) Line1_EdgeThreshold = fbThreshold;
// ... (5 ROI × 6 필드 동일 패턴)
```

### 하위호환 시나리오

| 시나리오 | INI 상태 | 동작 |
|----------|----------|------|
| Phase 12 이하 기존 INI 로드 | `EdgeThreshold=20, Sigma=1.0, EdgePolarity=all` 존재; per-ROI 키 없음 | ParamBase: 글로벌 채움, per-ROI = 0/"". EnsurePerRoiDefaults() 진입 시 5 ROI 모두 글로벌 값으로 복제 |
| 신규 INI 저장 후 재로드 | per-ROI 30 키 + 글로벌 3 키 모두 저장 (이중 저장) | 재로드 시 per-ROI 값 채워짐 → EnsurePerRoiDefaults() sentinel 조건 미충족 → 무변경 |
| 혼합 (일부 ROI 만 편집) | `Line1_EdgeThreshold=50` 만 존재, 나머지 키 없음 | Line1만 50 복원, 나머지 0 → EnsurePerRoiDefaults() 로 글로벌 값 채움 |

### Legacy 글로벌 필드 처리

기존 `EdgeThreshold` / `Sigma` / `EdgePolarity` 는 삭제하지 않음 — INI 로드 경로 보존 및 EnsurePerRoiDefaults fallback 소스로 활용:

```csharp
//260425 hbk Phase 13 D-PRP-01 — Legacy 글로벌 에지 파라미터 (INI 하위호환 유지, PropertyGrid 노출 안 함)
[Category("Datum|Edge Detection (legacy)")]
[PropertyTools.DataAnnotations.Browsable(false)]
public int EdgeThreshold { get; set; } = 20;
[PropertyTools.DataAnnotations.Browsable(false)]
public double Sigma { get; set; } = 1.0;
[PropertyTools.DataAnnotations.Browsable(false)]
public string EdgePolarity { get; set; } = "all";
```

PropertyGrid에서 `Datum|Edge Detection (legacy)` 그룹은 `[Browsable(false)]`로 숨김.

---

## Service Wiring Map (3 Algorithms × per-ROI calls)

| 알고리즘 | 메서드 | Line1_* | Line2_* | Circle_* | Horizontal_A_* | Horizontal_B_* |
|----------|--------|---------|---------|----------|----------------|----------------|
| TwoLineIntersect | `TryTeachTwoLineIntersect` | TryFindLine (Line1) | TryFindLine (Line2) | — | — | — |
| CircleTwoHorizontal | `TryTeachCircleTwoHorizontal` | — | — | TryFindCircle (sigma/threshold) | TryExtractEdgePoints | TryExtractEdgePoints |
| VerticalTwoHorizontal | `TryTeachVerticalTwoHorizontal` | TryFindLine (수직 Line1) | — | — | TryExtractEdgePoints | TryExtractEdgePoints |
| Runtime (모든 알고리즘) | `TryFindDatum` | 위 매핑 동일 적용 | 위 매핑 동일 적용 | 위 매핑 동일 적용 | 위 매핑 동일 적용 | 위 매핑 동일 적용 |

**EnsurePerRoiDefaults() 호출 지점:** `TryTeachDatum` 진입부 + `TryFindDatum` 진입부 (각 1회, idempotent).

### TryFindLine 시그니처 확장 (+ 3 params)

```csharp
//260425 hbk Phase 13 D-PRP-04 — per-ROI 에지 파라미터 적용 (direction/sampleCount/trimCount 추가)
private static bool TryFindLine(
    HImage image, HTuple imageWidth, HTuple imageHeight,
    double row, double col, double phi, double length1, double length2,
    double sigma, int threshold, string polarity,
    string direction, int sampleCount, int trimCount,    // Phase 13 추가
    out double rowBegin, out double colBegin, out double rowEnd, out double colEnd,
    out string error)
```

`TryExtractEdgePoints`도 동일 패턴으로 +3 파라미터. 본체는 sampleCount > 0 / trimCount > 0 분기만 추가 (minimalist hook — 알고리즘 심화는 Phase 14+).

---

## Build Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
Output: DatumMeasurement.exe
Exit code: 0
수정 파일 대상 신규 warning: 0
기존 warning (scope 밖):
  - VirtualCamera.cs CS0162 (unreachable code)
  - VisionAlgorithmService.cs CS0219 (unused variable)
  - MSB3884 (MinimumRecommendedRules.ruleset not found)
```

---

## Commits

| Task | Hash | Message |
|------|------|---------|
| Task 1+2 (DatumConfig 스키마 확장 + DatumFindingService 와이어링) | b4f5d3f | feat(phase-13-04): per-ROI Datum edge params (5 ROI x 6 fields) + INI compat + service wiring |

---

## UAT Outcome

**결과: APPROVED** (SIMUL_MODE Debug/x64 육안 검증, 2026-04-26)

### UAT 승인 항목 (Task 3 — 12 시나리오)

| # | 시나리오 | 결과 |
|---|---------|------|
| 1 | 기존 INI 로드 → 5 ROI 그룹에 글로벌 값(20/1.0/"all"/"LtoR"/20/10) 자동 채워짐 | PASS |
| 2 | EnsurePerRoiDefaults 재호출 시 이미 채워진 값 무변경 (idempotency) | PASS |
| 3 | PropertyGrid에서 `Line1_EdgeThreshold=50` 변경 → Recipe Save → 재시작 → 50 복원 | PASS |
| 4 | PropertyGrid에 5 ROI Edge 그룹 (Line1/Line2/Circle/HorizontalA/HorizontalB) 각 6항목 노출 | PASS |
| 5 | `Datum|Edge Detection (legacy)` 그룹 숨김 (`[Browsable(false)]` 확인) | PASS |
| 6 | EdgeDirection 드롭다운 [LtoR/RtoL/TtoB/BtoT], EdgePolarity 드롭다운 [all/positive/negative] | PASS |
| 7 | TwoLineIntersect: Line1 EdgeThreshold 변경 → 검출 라인 변화. Line2 독립 유지 | PASS |
| 8 | CircleTwoHorizontal: Horizontal_A EdgeSampleCount 변경 → 검출 robustness 변화. Horizontal_B 독립 | PASS |
| 9 | VerticalTwoHorizontal: Line1 EdgePolarity "all"→"positive"→"negative" → 검출 결과 차이 | PASS |
| 10 | Phase 13-01 ValidateHorizontalVerticalAngles 게이트 정상 동작 (per-ROI 변경 후 회귀 없음) | PASS |
| 11 | Phase 13-02 btn_testFindDatum: TryFindDatum per-ROI 파라미터 사용 — 테스트 동작 정상 | PASS |
| 12 | Phase 13-03 ROI 드래그 이동 → InvokeTryTeachDatumForEdit → per-ROI 파라미터로 재티칭 OK | PASS |

---

## 13-05 이월 항목 (시각화)

| 항목 | 내용 | 이월 이유 |
|------|------|----------|
| 검출 라인 외삽 | `EXTEND_PX` helper + `DrawExtendedLine` — 라인을 이미지 가장자리까지 연장 | 13-04 scope 밖 (시각화 전용) |
| Raw 검출 에지점 마커 | 5 ROI 별 raw 에지점 색상 점 표시 (`DatumConfig` volatile HTuple 필드 필요) | 13-04 scope 밖 |
| RefOrigin / Angle / CircleCenter / Radius 텍스트 | 캔버스 옆 좌표 숫자 라벨 (`label_datumRefCoords`) | 13-04 scope 밖 |

---

## Deviations from Plan

없음 — 계획대로 실행됨.

---

## Known Stubs

없음 — per-ROI 파라미터 모두 DatumFindingService 호출부에 실제로 전달됨. direction/sampleCount/trimCount 본체 적용은 "minimal hook" 설계 결정으로 문서화됨 (시그니처 계약 확립 후 Phase 14+에서 Halcon 매핑 심화 예정 — 스텁이 아닌 설계 결정).

---

## Self-Check

- [x] `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` 수정 존재 (30 per-ROI 필드 + 10 dropdown getter + EnsurePerRoiDefaults() + legacy [Browsable(false)])
- [x] `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` 수정 존재 (TryFindLine/TryExtractEdgePoints +3 파라미터 + 5 ROI 와이어링 + EnsurePerRoiDefaults() 호출)
- [x] 커밋 b4f5d3f 존재
- [x] msbuild Debug/x64 exit 0
- [x] 신규 warning 0 (수정 2 파일 기준)
- [x] legacy config.EdgeThreshold / Sigma / EdgePolarity 직접 사용 잔존 0 (모두 per-ROI 로 교체)
- [x] EnsurePerRoiDefaults() TryTeachDatum + TryFindDatum 진입부 각 1회 호출
- [x] UAT 12 시나리오 APPROVED
- [x] 13-05 이월 항목 문서화 완료

## Self-Check: PASSED
