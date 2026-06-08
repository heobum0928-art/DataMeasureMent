---
phase: 22-image-dual-structure
plan: 01
subsystem: inspection
tags: [datum, ini-serialization, parambase, reflection, propertygrid, halcon]

# Dependency graph
requires:
  - phase: 11-datum-teaching-ui-roi
    provides: DatumConfig (ParamBase 상속 + SourceShotName 등 ImageSource 카테고리 필드)
  - phase: 12-datum-algorithm
    provides: DatumConfig.AlgorithmType + EnsurePerRoiDefaults 마이그레이션 패턴
  - phase: 17-datum-uxredesign
    provides: ICustomTypeDescriptor + Circle_RadialDirection sentinel "" → "Inward" fallback 패턴
provides:
  - DatumConfig.TeachingImagePath public string 속성 (기본값 "")
  - EnsurePerRoiDefaults() 의 TeachingImagePath null → "" 정규화 가드
  - INI [FIXTURE_DATUM_N] 섹션에 TeachingImagePath= 키 자동 직렬화 (ParamBase reflection)
  - PropertyGrid Datum|ImageSource 카테고리 자동 노출
affects: [22-02, 23-a-series-simul, future-teaching-image-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ParamBase reflection auto-serialization (신규 public string 필드 추가 시 Save/Load 코드 0줄)"
    - "EnsurePerRoiDefaults null 정규화 (Phase 17 D-02 패턴 확장 — `== null` 비교로 빈 문자열 보존)"
    - "디자인 결정 lock-in (b): InspectionImagePath = ShotConfig.SimulImagePath 의미적 재해석 (ShotConfig 무수정)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs

key-decisions:
  - "InspectionImagePath 전략 (b) — 기존 ShotConfig.SimulImagePath 를 의미상 '검사 이미지 경로'로 재해석. ShotConfig 무수정, Action_FAIMeasurement L111/L226 무수정 → zero-regression. 역할 분리는 코드 레벨에서 DatumConfig.TeachingImagePath (티칭) vs ShotConfig.SimulImagePath (검사) 의 별개 필드로 달성."
  - "UI 범위 = PropertyGrid 자동 노출만. [Category(\"Datum|ImageSource\")] 부착으로 기존 ImageSourceMode/SourceShotName 그룹에 자동 합류. Browse 버튼/OpenFileDialog 같은 풍부한 UI 는 Phase 22 범위 밖 — Phase 23 또는 후속 UI 작업 carry-over."
  - "Null guard 비교 연산자 `== null` 선택 (NOT `string.IsNullOrEmpty`) — 사용자가 의도적으로 클리어한 빈 문자열 값을 보존하기 위함. Circle_RadialDirection 가드 (Phase 17 D-02) 의 'sentinel \"\" → default' 의미론과 차별화."
  - "TeachingImagePath 의 실 소비는 Plan 22-01 범위 밖 — Phase 23 (재티칭 기준 이미지 참조) 또는 후속 UI 작업으로 carry-over. Plan 22-01 은 INI 영구 보존 인프라만 구축."

patterns-established:
  - "ParamBase reflection auto-serialization: public string 속성 1줄 선언 = INI Save/Load 자동 처리 (별도 caller 코드 0줄)"
  - "EnsurePerRoiDefaults null-only guard: 빈 문자열도 유효값인 경우 `if (X == null) X = \"\";` 패턴 (vs string.IsNullOrEmpty 의 sentinel default 패턴)"

requirements-completed: [IMG-01]

# Metrics
duration: 2min
completed: 2026-05-11
---

# Phase 22 Plan 01: DatumConfig TeachingImagePath 영구 보존 Summary

**DatumConfig 에 TeachingImagePath public string 속성 + EnsurePerRoiDefaults null 가드 추가 — ParamBase reflection 으로 INI 자동 직렬화, 검사 경로 (ShotConfig.SimulImagePath) 와 코드 레벨 역할 분리**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-05-11T06:23:40Z
- **Completed:** 2026-05-11T06:24:58Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- DatumConfig.cs 에 `public string TeachingImagePath { get; set; } = "";` 속성 추가 (L35) — [Category("Datum|ImageSource")] 부착으로 PropertyGrid 자동 노출
- DatumConfig.EnsurePerRoiDefaults() 에 `if (TeachingImagePath == null) TeachingImagePath = "";` 가드 추가 (L519) — INI 키 미존재 케이스 → 빈 문자열 정규화 (success criterion #4)
- ParamBase reflection (Save/Load String case) 가 신규 속성을 자동 처리 → InspectionRecipeManager L117-119 / L188-193 의 caller 코드 0줄 변경
- 두 변경 라인에 `//260511 hbk Phase 22 IMG-01` 마커 부착 (총 2회 매치 — grep 검증 통과)

## Task Commits

각 태스크는 원자적으로 커밋되었다:

1. **Task 1: DatumConfig 에 TeachingImagePath 속성 선언** — `dd9f706` (feat)
2. **Task 2: EnsurePerRoiDefaults 에 TeachingImagePath null 가드 추가** — `55c2fd4` (feat)

**Plan metadata:** (이 SUMMARY 커밋에서 부여) (docs)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — TeachingImagePath 속성 (L33-35) + EnsurePerRoiDefaults null 가드 (L519). 총 +5 lines (4 + 1), 다른 라인 무수정.

### 정확한 삽입 위치 (after-the-fact)

| 위치 | 라인 | 내용 |
|------|------|------|
| Task 1 주석 | L33 | `//260511 hbk Phase 22 IMG-01 — Datum 티칭 시 사용한 기준 이미지 경로. INI 직렬화는 ParamBase reflection ...` |
| Task 1 Category | L34 | `[Category("Datum|ImageSource")]` |
| Task 1 속성 | L35 | `public string TeachingImagePath { get; set; } = "";` |
| Task 2 가드 | L519 | `if (TeachingImagePath == null) TeachingImagePath = ""; //260511 hbk Phase 22 IMG-01 — INI 키 미존재 시 null 가드 ...` |

플래너의 예상 위치 (L31 직후 + L514 직후) 는 L31/L518 였으나 실제 baseline 의 SourceShotName 라인 = L31, Circle_RadialDirection 가드 라인 = L514 → L518 (Phase 17 D-02 이후 라인 시프트). Task 1 삽입으로 Task 2 의 Circle_RadialDirection 가드가 L518 로 1 라인 이동 → null 가드 최종 위치 L519.

## ParamBase Reflection 자동 직렬화 검증

**메커니즘:**
- `DatumConfig : ParamBase, ICustomTypeDescriptor` (DatumConfig.cs L15)
- ParamBase.Save (L324-367) — `prop.PropertyType.Name == "String"` 분기 → `saveFile[group][name] = (string)prop.GetValue(this);`
- ParamBase.Load (L369-429) — `case "String"` → `string sValue = loadFile[group][name].ToString(); prop.SetValue(this, sValue);`
- InspectionRecipeManager L117-119 (Datum Save) / L188-193 (Datum Load) 는 `[FIXTURE_DATUM_N]` 섹션 이름으로 Save/Load 호출 → 신규 키 `TeachingImagePath=` 자동 등장

**검증 방법 (Plan 22-02 UAT 시나리오 1 으로 인계):**
1. SIMUL 모드 실행 → DatumConfig 의 TeachingImagePath 에 임의 경로 설정 (예: `C:\test\teach.png`)
2. 레시피 저장 → 디스크 INI 의 `[FIXTURE_DATUM_0]` 섹션에 `TeachingImagePath=C:\test\teach.png` 키 자동 등장 확인
3. 어플 재시작 후 동일 레시피 로드 → DatumConfig.TeachingImagePath == "C:\test\teach.png" 확인
4. 기존 INI (TeachingImagePath 키 없음) 로드 → EnsurePerRoiDefaults() 호출 후 TeachingImagePath == "" 확인 (회귀 0)

## Decisions Made

### 디자인 결정 lock-in (planner 결정 그대로 채택)

**(1) InspectionImagePath 전략 = (b) 의미적 재해석**

- 기존 `ShotConfig.SimulImagePath` 를 그대로 "InspectionImagePath" 역할로 사용
- 이유: zero-regression. INI 키 그대로 유지, `Action_FAIMeasurement.cs` L111/L226 (Grab/DatumPhase 경로의 SimulImagePath 사용) 코드 변경 0
- Phase 22 의 "역할 분리" 는 **DatumConfig.TeachingImagePath (티칭) vs ShotConfig.SimulImagePath (검사)** 의 별개 필드로 코드 레벨에서 달성
- ShotConfig.cs 는 무수정 — 의미만 재정의

**(2) UI 범위 = PropertyGrid 자동 노출만**

- `[Category("Datum|ImageSource")]` 부착 → 기존 `ImageSourceMode`/`ReuseFromShotName`/`SourceShotName` 그룹에 자동 합류
- Browse 버튼/OpenFileDialog 같은 풍부한 UI 는 Phase 22 범위 밖
- XAML 변경 0

**(3) Null guard 연산자 선택 = `== null` (NOT `string.IsNullOrEmpty`)**

- 사용자가 의도적으로 클리어한 빈 문자열 값을 보존
- Circle_RadialDirection 가드 (Phase 17 D-02) 의 'sentinel "" → default' 의미론과 차별화
- 멱등성 보장: 비-null 값은 변경 0

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Carry-over

- **TeachingImagePath 의 실 소비**: Plan 22-01 은 신규 속성 + INI 직렬화 인프라만 제공. 실제 소비 (예: 셋업 단계에서 TeachingImagePath 의 이미지를 로드하여 ROI 재정의에 사용, 또는 재티칭 시 기준 이미지 참조) 는 **Phase 23 (A시리즈 Simul) 또는 후속 UI 작업** 으로 인계.
- **UI 풍부화**: Browse 버튼/OpenFileDialog 추가는 Phase 22 범위 밖.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 22-02 (build verification + UAT 시나리오) 진행 가능
- 신규 warning 0 확인 + INI 라운드트립 UAT 시나리오 1 실행은 Plan 22-02 의 Task 3/Task 4 범위
- TeachingImagePath 소비처 (Phase 23 또는 후속 UI) 는 carry-over

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — exists (modified)
- Commit `dd9f706` (Task 1) — present in git log
- Commit `55c2fd4` (Task 2) — present in git log
- Grep `public string TeachingImagePath { get; set; } = "";` → 1 match (L35) PASS
- Grep `if (TeachingImagePath == null) TeachingImagePath = "";` → 1 match (L519) PASS
- Grep `260511 hbk Phase 22 IMG-01` → 2 matches (L33, L519) PASS
- Post-commit deletion check (both commits) → no deletions

---
*Phase: 22-image-dual-structure*
*Plan: 01*
*Completed: 2026-05-11*
