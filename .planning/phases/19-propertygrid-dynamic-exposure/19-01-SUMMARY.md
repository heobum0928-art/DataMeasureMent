---
phase: 19-propertygrid-dynamic-exposure
plan: 01
subsystem: ui
tags: [propertytools, propertygrid, icustomtypedescriptor, refactor, datumconfig]

requires:
  - phase: 17
    provides: DatumConfig ICustomTypeDescriptor + IsHiddenForAlgorithm 알고리즘별 hide 패턴 (D-09)
  - phase: 18
    provides: allNoFilter+sourceNames whitelist 패턴 (CO-01) — Browsable(false) 소스 강제 포함
provides:
  - DynamicPropertyHelper.FilterProperties 정적 헬퍼 (DatumConfig/FAIConfig 공유 가능)
  - DatumConfig.GetProperties(Attribute[]) 단일 위임 호출로 축약 — 31줄 → 16줄
affects: [19-02-FAIConfig-icustomtypedescriptor]

tech-stack:
  added: []
  patterns:
    - "DynamicPropertyHelper.FilterProperties 공통 헬퍼 — 동적 PropertyGrid 필터링 (hideFunc + sourceNames)"

key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/DatumMeasurement.csproj

key-decisions:
  - "FilterProperties 시그니처: (object obj, Attribute[] attrs, Func<string,bool> hideFunc, HashSet<string> sourceNames) — 4-인자 간결성 우선"
  - "T-19-02 mitigation 채택: hideFunc null이면 ArgumentNullException — fail-fast로 디버깅 비용 절감"
  - "sourceNames null 안전 추가 (Rule 2): null이면 화이트리스트 단계 skip — 호출자 부담 감소"
  - "DatumConfig.GetProperties(Attribute[]) 위 기존 Phase 17 D-09 주석 블록 보존 — 의도적 설계 컨텍스트 유지"

patterns-established:
  - "동적 PropertyGrid 필터: ICustomTypeDescriptor.GetProperties(Attribute[]) → DynamicPropertyHelper.FilterProperties(this, attrs, name => HideRule(name, state), sourceNames)"
  - "Phase 18 CO-01 whitelist: [ItemsSourceProperty(nameof(XxxList))] 의 List 소스는 [Browsable(false)]이므로 sourceNames HashSet에 nameof(XxxList) 명시 등록 필수"
  - "INI 직렬화 비간섭: ParamBase.Save/Load는 GetType().GetProperties() 사용 → ICustomTypeDescriptor 영향 0"

requirements-completed: [QUAL-03, CO-02]

duration: 3min
completed: 2026-05-07
---

# Phase 19 Plan 01: DynamicPropertyHelper + DatumConfig Refactoring Summary

**DynamicPropertyHelper.FilterProperties 정적 헬퍼 추출 + DatumConfig.GetProperties 31줄 본문을 16줄 헬퍼 호출 단일 위임으로 축약 (Phase 17 D-09 + Phase 18 CO-01 동작 회귀 0).**

## Performance

- **Duration:** 3 min
- **Started:** 2026-05-07T23:37:04Z
- **Completed:** 2026-05-07T23:39:38Z
- **Tasks:** 2
- **Files modified:** 3 (1 new, 2 modified)

## Accomplishments

- `DynamicPropertyHelper.FilterProperties(object, Attribute[], Func<string,bool>, HashSet<string>)` 정적 헬퍼 신규 작성 — Phase 17 D-09 알고리즘별 hide 필터 + Phase 18 CO-01 [Browsable(false)] 소스 화이트리스트 패턴 통합
- `DatumConfig.GetProperties(Attribute[])` 본문(L555-585, 31줄)을 `DynamicPropertyHelper.FilterProperties` 단일 호출(L556-571, 16줄)로 교체 — 람다 콜백으로 `IsHiddenForAlgorithm` 위임
- `DatumMeasurement.csproj` Compile ItemGroup에 `DynamicPropertyHelper.cs` 등록 (MeasurementFactory.cs 다음 줄, 백슬래시 경로)
- T-19-02 위협 모델 mitigation: `hideFunc` null 가드 (`ArgumentNullException`)
- msbuild Debug/x64 PASS — 신규 error/warning 0건 (기존 pre-existing warning 3건은 무관)
- Phase 19 Wave 2 (FAIConfig ICustomTypeDescriptor) 의 헬퍼 기반 마련

## Task Commits

각 태스크는 원자적으로 커밋되었다:

1. **Task 1: DynamicPropertyHelper.cs 신규 생성 + csproj Compile 등록** — `224332d` (feat)
2. **Task 2: DatumConfig.GetProperties 리팩토링 (헬퍼 호출로 교체)** — `4342cdc` (refactor)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs` (신규, 47줄) — `public static class DynamicPropertyHelper` + `FilterProperties` 정적 메서드. ParamBase INI 경로 비간섭 명시 주석.
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (수정, +5/-20 줄) — `GetProperties(Attribute[])` 본문 헬퍼 호출로 교체. `IsHiddenForAlgorithm`/`GetProperties()`/10개 stub/주석 블록 모두 보존.
- `WPF_Example/DatumMeasurement.csproj` (수정, +1 줄) — `<Compile Include="Custom\Sequence\Inspection\DynamicPropertyHelper.cs" />` 등록.

## Decisions Made

- **FilterProperties 시그니처 4-인자 채택:** 위협 모델 `T-19-02` (DoS - hideFunc null) mitigation 요구로 `if (hideFunc == null) throw new ArgumentNullException`을 메서드 본문에 추가. CONTEXT.md decisions의 4-인자 시그니처 그대로 유지.
- **`sourceNames` null 안전성 추가 (Rule 2 - missing critical):** PLAN에는 null 검사 없었으나, `sourceNames.Contains` 호출 직전 null이면 NullReferenceException → 화이트리스트 단계 skip 패턴으로 호출자 편의성 확보. 기존 DatumConfig 호출 경로 동작 영향 없음 (항상 non-null HashSet 전달).
- **DatumConfig 위 Phase 17 D-09 주석 블록 보존:** PLAN의 교체 후 코드 블록은 기존 L551-554 주석 3줄을 명시 표기하지 않았으나, 의도적 설계 컨텍스트(PropertyTools.Wpf 호출 경로 + INI reflection 비간섭 + GetProperties() 무인자 안전 장치)를 미래 변경자가 잃지 않도록 그대로 유지하고 `//260507 hbk Phase 19 QUAL-03:` 주석을 그 아래 추가.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] DynamicPropertyHelper.FilterProperties 본문에 hideFunc null 가드 추가**
- **Found during:** Task 1 (DynamicPropertyHelper.cs 작성)
- **Issue:** PLAN action 코드 블록에는 null 가드가 없었으나, threat_model T-19-02가 `if (hideFunc == null) throw new ArgumentNullException("hideFunc");` mitigation을 명시 — 미적용 시 위협 모델 위반.
- **Fix:** `FilterProperties` 본문 첫 줄에 `if (hideFunc == null) throw new System.ArgumentNullException("hideFunc");` 추가 (line 26).
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs`
- **Verification:** 코드 grep으로 라인 존재 확인. C# 7.2 호환 (System.ArgumentNullException).
- **Committed in:** `224332d` (Task 1 commit)

**2. [Rule 2 - Missing Critical] sourceNames null 안전 처리 추가**
- **Found during:** Task 1 (DynamicPropertyHelper.cs 작성)
- **Issue:** PLAN action 코드 블록은 sourceNames에 직접 `.Contains` 호출 — null 전달 시 NullReferenceException. 미래 호출자가 화이트리스트 미사용 케이스에서 null 전달 가능성 존재.
- **Fix:** allNoFilter 루프를 `if (sourceNames != null) { ... }` 블록으로 감싸 null이면 skip.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs`
- **Verification:** DatumConfig 호출 경로(항상 non-null HashSet 전달)에서 동작 동일성 확인. 빌드 PASS.
- **Committed in:** `224332d` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 missing critical via Rule 2)
**Impact on plan:** 두 auto-fix 모두 위협 모델 mitigation 또는 헬퍼 안전성 확립을 위한 것. PLAN의 핵심 동작(FilterProperties 시그니처, 내부 로직, 반환값)은 변경 없음. Wave 2(FAIConfig)에서 sourceNames null 안전성을 활용 가능.

## Issues Encountered

- 없음. 빌드 1회에 PASS, 회귀 검증 grep 5종 모두 통과.

## TDD Gate Compliance

이 plan은 `type: execute` (TDD 게이트 비대상). 회귀는 정적 grep + msbuild로 검증.

## Verification Results

PLAN의 verification 5단계 모두 통과:

1. ✓ `Test-Path "WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs"` → 파일 존재
2. ✓ csproj Compile 등록 — line 218에 `<Compile Include="Custom\Sequence\Inspection\DynamicPropertyHelper.cs" />`
3. ✓ DatumConfig 헬퍼 호출 — line 570에 `DynamicPropertyHelper.FilterProperties(this, attributes, name => IsHiddenForAlgorithm(name, alg), sourceNames)`
4. ✓ IsHiddenForAlgorithm 보존 — 3건 (주석 + 람다 콜백 + static 정의)
5. ✓ msbuild Debug/x64 → `DatumMeasurement.exe` 산출 성공, 신규 error/warning 0

회귀 검증 추가 grep:
- ✓ `var all = System.ComponentModel.TypeDescriptor` → DatumConfig.cs에 0건 (헬퍼로 이전 완료)
- ✓ `var keep = new List` → DatumConfig.cs에 0건 (헬퍼로 이전 완료)
- ✓ `GetProperties()` 무인자 오버로드 → line 572 보존
- ✓ 10개 ICustomTypeDescriptor stub → lines 576-585 모두 보존

## User Setup Required

없음 — 외부 서비스 설정 불필요.

## Next Phase Readiness

Wave 2 준비 완료:

- `DynamicPropertyHelper.FilterProperties` 헬퍼가 빌드 경로에 등록되어 호출 가능
- DatumConfig 회귀 0 — Phase 17 Test 8 (TLI/CTH/VTH 동적 노출) 동작 보존
- FAIConfig ICustomTypeDescriptor 구현 시 같은 헬퍼 재사용 가능 (sourceNames null-safe도 활용 가능)

다음 plan (`19-02-PLAN.md`):
- FAIConfig.EdgeMeasureType 신규 string 필드 + EdgeMeasureTypeList ItemsSource
- FAIConfig : ParamBase, ICustomTypeDescriptor 인터페이스 확장
- IsHiddenForEdgeMeasureType("CircleDiameter") → EdgeDirection/Polarity/Selection/SampleCount/TrimCount/Sigma 숨김
- DynamicPropertyHelper.FilterProperties 호출로 Wave 1 동일 패턴 적용

## Self-Check: PASSED

**Files claimed created/modified — all verified:**
- ✓ `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs` (FOUND)
- ✓ `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (FOUND, modified)
- ✓ `WPF_Example/DatumMeasurement.csproj` (FOUND, modified)

**Commits claimed — all verified in git log:**
- ✓ `224332d` (Task 1: feat DynamicPropertyHelper + csproj)
- ✓ `4342cdc` (Task 2: refactor DatumConfig.GetProperties delegation)

---

*Phase: 19-propertygrid-dynamic-exposure*
*Plan: 01*
*Completed: 2026-05-07*
