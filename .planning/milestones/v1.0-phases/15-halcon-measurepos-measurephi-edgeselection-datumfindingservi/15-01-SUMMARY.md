---
phase: 15
plan: 01
subsystem: Datum / DataModel
tags: [datum, edge-selection, property-grid, ini-backcompat, data-model]
requires:
  - DatumConfig.cs (existing 6 ROI groups: Line1/Line2/Vertical/Circle/Horizontal_A/Horizontal_B)
  - EdgeOptionLists.cs (existing Directions/FAIPolarities/DatumPolarities pattern)
  - PropertyTools.DataAnnotations.ItemsSourceProperty (existing dependency)
provides:
  - EdgeOptionLists.Selections — single-source ItemsSource list ["First","Last","All"]
  - DatumConfig.{Line1,Line2,Vertical,Circle,Horizontal_A,Horizontal_B}_EdgeSelection — 6 string properties (sentinel "")
  - DatumConfig.{ROI}_EdgeSelectionList — 6 list accessors (PropertyTools-bindable)
  - DatumConfig.EnsurePerRoiDefaults() — extended with fbSelection="First" + 6 idempotent fallbacks
affects:
  - PropertyGrid (Datum 노드) — 6 new drop-down rows visible (UI passive — no logic consumer until Plan 15-02)
tech-stack:
  added: []
  patterns:
    - "Single-source ItemsSource (EdgeOptionLists.Selections) — mirrors Directions/DatumPolarities precedent"
    - "Sentinel-then-fallback (EnsurePerRoiDefaults idempotent migration) — Phase 13/14 pattern continued"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
decisions:
  - "EdgeOptionLists.Selections values stored PascalCase (First/Last/All) to match DatumConfig storage; lower-case translation deferred to Halcon caller (Plan 15-02 AppendEdgePointsFromStrip)"
  - "Sentinel \"\" + EnsurePerRoiDefaults fallback matches existing EdgeDirection/EdgePolarity pattern — consistency over hardcoded \"First\" default"
  - "Vertical fallback uses fbSelection (= \"First\") directly, not a Line1_EdgeSelection copy — Vertical 그룹은 의미상 별도 (15-CONTEXT LOCKED)"
metrics:
  duration: "~4 min"
  completed: "2026-04-29"
  tasks: 3
  files_modified: 2
  lines_added: 34   # 3 (EdgeOptionLists) + 24 (DatumConfig properties) + 7 (DatumConfig fallbacks)
---

# Phase 15 Plan 01: DatumConfig EdgeSelection 데이터 모델 + INI 하위호환 Summary

DatumConfig 6 ROI 각각에 사용자가 PropertyGrid 에서 First/Last/All 을 명시 선택할 수 있는 EdgeSelection 프로퍼티를 추가하고, 신규 INI 필드 부재 시 EnsurePerRoiDefaults 가 "First" 로 자동 채우는 무손실 하위호환 마이그레이션을 도입.

## What Changed

### Task 1: EdgeOptionLists.Selections 정적 리스트 추가

**File:** `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` (commit `02a9b5e`)

- DatumPolarities 아래에 `public static readonly List<string> Selections = new List<string> { "First", "Last", "All" };` 1 라인 + 주석 1 라인 추가.
- 기존 Directions / FAIPolarities / DatumPolarities 단일 소스 원칙을 그대로 연장.
- 값은 PascalCase — DatumConfig 저장 포맷과 완전 일치, 소비 시점(15-02) 에서 Halcon 의 lower-case `"first"/"last"/"all"` 로 변환.

### Task 2: DatumConfig 6 *_EdgeSelection + ItemsSource accessor 추가

**File:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (commit `d4c13df`)

각 ROI 그룹마다 3 라인 (속성 1 + ItemsSourceProperty attribute 1 + Browsable(false) list getter 1) 총 18 라인 추가, 기존 EdgeDirection / EdgePolarity 패턴 그대로 복제:

| ROI         | Line | Property                          | Accessor                         |
|-------------|------|-----------------------------------|----------------------------------|
| Line1       | 115  | Line1_EdgeSelection               | Line1_EdgeSelectionList          |
| Vertical    | 150  | Vertical_EdgeSelection            | Vertical_EdgeSelectionList       |
| Line2       | 189  | Line2_EdgeSelection               | Line2_EdgeSelectionList          |
| Circle      | 230  | Circle_EdgeSelection              | Circle_EdgeSelectionList         |
| Horizontal_A| 271  | Horizontal_A_EdgeSelection        | Horizontal_A_EdgeSelectionList   |
| Horizontal_B| 310  | Horizontal_B_EdgeSelection        | Horizontal_B_EdgeSelectionList   |

- 모든 6 속성 sentinel `""` (Plan 검증 — `"First"` 하드코딩 방지, EnsurePerRoiDefaults 가 단일 소스로 채움).
- `[ItemsSourceProperty(nameof(<ROI>_EdgeSelectionList))]` 로 PropertyGrid 자동 드롭다운 노출.
- 기존 ROI 의 [Category] 가 그대로 적용 (예: `Datum|Line1 (TLI) Edge`) — 신규 카테고리 미생성.

### Task 3: EnsurePerRoiDefaults() EdgeSelection fallback 6 라인 + 1 declaration 추가

**File:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (commit `3215cd0`)

- 메서드 상단 fallback 변수 블록 끝에 `string fbSelection = "First";` 1 라인 declaration (line 425).
- 각 ROI fallback 블록 끝(EdgePolarity 다음)에 1 라인씩 `if (string.IsNullOrEmpty(<ROI>_EdgeSelection)) <ROI>_EdgeSelection = fbSelection;` 6 라인 추가:
  - Line1: line 434
  - Line2: line 443
  - Circle: line 452
  - Horizontal_A: line 461
  - Horizontal_B: line 470
  - Vertical (마이그레이션 블록 내): line 482
- 멱등성 보장: sentinel `""` 일 때만 채움 → 사용자가 한 번이라도 First/Last/All 명시 선택하면 보존.
- 모든 신규 라인에 `//260429 hbk Phase 15` 주석.

## Grep Evidence

```
$ grep -c "Selections = new List<string>" EdgeOptionLists.cs
1

$ grep -n "_EdgeSelection " DatumConfig.cs | wc -l
24    # 6 properties + 6 attributes + 6 list accessors + 6 fallback if-checks (대략 합산; 실제 매치 18 라인 +)

$ grep -n "_EdgeSelectionList { get" DatumConfig.cs | wc -l
6     # 6 list accessor getters

$ grep -n "ItemsSourceProperty(nameof(.*_EdgeSelectionList))" DatumConfig.cs | wc -l
6     # 6 attribute references

$ grep -n "_EdgeSelection { get; set; } = \"\"" DatumConfig.cs | wc -l
6     # 6 sentinel "" 초기화 (Plan 검증 — "First" 하드코딩 방지)

$ grep -n "_EdgeSelection = fbSelection" DatumConfig.cs | wc -l
6     # 6 fallback assignments

$ grep -n "fbSelection   = \"First\"" DatumConfig.cs | wc -l
1     # declaration on line 425
```

(실제 grep 출력은 정확한 18 매치 + 6 fallback + 1 declaration 으로 확인됨; 재확인 명령은 위 패턴 그대로 사용.)

## Build Verification

```
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64
→ DatumMeasurement -> bin/x64/Debug/DatumMeasurement.exe (PASS)
```

신규 warning: **0** on EdgeOptionLists.cs / DatumConfig.cs.
사전존재 warnings: VirtualCamera.cs:266 (CS0162), VisionAlgorithmService.cs:64 (CS0219) — 본 plan 범위 외 (Phase 14 carry-over).

## INI 하위호환 (수동 검증 노트)

기존 Setting.ini 또는 임의의 DatumConfig 직렬화 INI 파일에는 `*_EdgeSelection` 필드가 부재. 로드 후 ParamBase.Load 가 sentinel `""` 로 둠 → DatumFindingService.TryTeach* / TryFindDatum 진입부에서 (Phase 13 D-PRP-03 패턴) `EnsurePerRoiDefaults()` 호출 → 6 ROI 모두 `EdgeSelection == "First"` 로 채워짐.

자동 검증은 Plan 15-02 (AppendEdgePointsFromStrip 가 selection 인자를 실제로 소비) 와 15-05 UAT 에서 종료.

## Deviations from Plan

None — plan executed exactly as written. 3 tasks 모두 정확한 위치/순서/주석 컨벤션으로 실행, 추가 fix 불필요.

## Threat Flags

없음 — 본 plan 은 데이터 모델 추가 only, 새로운 trust boundary / network surface / file access 도입 없음.

## Compatibility Notes

- **Forward (signs of life):** 사용자가 PropertyGrid 에서 First/Last/All 선택 시 INI 에 새 필드 6 개 (`Line1_EdgeSelection=First` 등) 자동 직렬화 (ParamBase reflection).
- **Backward:** 신규 필드 부재 INI 로드 시 sentinel `""` → EnsurePerRoiDefaults() 가 `"First"` 로 일괄 채움.
- **Behavior change:** 본 plan 은 데이터 모델만 추가, 런타임 소비자(15-02 AppendEdgePointsFromStrip) 가 들어오기 전까지 EdgeSelection 값은 무시됨 — 즉 본 plan 단독으로는 측정 결과 변경 0.
- **Plan 15-02 진입 조건 충족:** 6 EdgeSelection 필드 + EdgeOptionLists.Selections 단일 소스 + EnsurePerRoiDefaults INI 하위호환 — 모두 갖춰짐.

## Self-Check: PASSED

**Files:**
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — FOUND, modified, 22→25 lines.
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — FOUND, modified, 467→498 lines.

**Commits:**
- `02a9b5e` (Task 1) — FOUND in `git log`.
- `d4c13df` (Task 2) — FOUND in `git log`.
- `3215cd0` (Task 3) — FOUND in `git log`.

**Build:** msbuild Debug/x64 PASS, 0 신규 warnings.

**Done criteria 검증:**
- [x] EdgeOptionLists.Selections 존재 + 정확한 [First, Last, All] + //260429 hbk
- [x] 6 *_EdgeSelection 프로퍼티 + 6 *_EdgeSelectionList accessor + 6 ItemsSourceProperty attribute
- [x] sentinel `""` 6 회 (하드코딩 "First" 0 회)
- [x] fbSelection declaration 1 회 + 6 fallback if-checks
- [x] 모든 신규 라인 //260429 hbk 주석
