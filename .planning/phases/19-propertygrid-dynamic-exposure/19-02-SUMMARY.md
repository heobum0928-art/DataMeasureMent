---
phase: 19-propertygrid-dynamic-exposure
plan: 02
subsystem: ui
tags: [propertytools, propertygrid, icustomtypedescriptor, faiconfig, dynamic-exposure]

requires:
  - phase: 19
    plan: 01
    provides: DynamicPropertyHelper.FilterProperties 정적 헬퍼 (sourceNames null-safe + hideFunc null-guard)
  - phase: 17
    provides: DatumConfig ICustomTypeDescriptor + IsHiddenForAlgorithm 패턴 (D-09)
  - phase: 18
    provides: allNoFilter+sourceNames whitelist 패턴 (CO-01)
provides:
  - FAIConfig ICustomTypeDescriptor 동적 PropertyGrid (EdgeMeasureType 별 6+2 hide)
  - FAIConfig.EdgeMeasureType string 필드 (INI 직렬화, 기본값 "EdgePairDistance")
  - FAIConfig.EdgeMeasureTypeList ([Browsable(false)] MeasurementFactory.GetTypeNames 단일 소스)
  - FAIConfig.IsHiddenForEdgeMeasureType private static (CircleDiameter 분기 8개 hide 조건)
affects: []

tech-stack:
  added: []
  patterns:
    - "FAIConfig PropertyGrid 동적 노출 — DynamicPropertyHelper.FilterProperties 위임 (Wave 1 헬퍼 재사용)"
    - "EdgeMeasureType + EdgeMeasureTypeList 드롭다운 — DatumConfig.AlgorithmType 패턴 그대로 복제"
    - "IsHiddenForEdgeMeasureType — DatumConfig.IsHiddenForAlgorithm 패턴 (string 비교 기반, switch 대신 단일 if-block)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs

key-decisions:
  - "Task 1+Task 2 단일 atomic commit (Rule 3 - blocking issue): Task 1 단독 적용 시 ICustomTypeDescriptor 미구현 멤버 CS0535 컴파일 에러 → 빌드 무결성 우선으로 두 task 묶어 commit"
  - "EdgeMeasureType 위치: 기존 [Category(\"Edge|Measurement\")] 어트리뷰트를 EdgeMeasureType 으로 이동 — Category 그룹의 첫 번째 표시 항목으로 노출 (PLAN action 명시 패턴)"
  - "EdgeMeasureTypeList getter — MeasurementFactory.GetTypeNames() 단일 소스 호출 (하드코딩 금지, MEMORY.md feedback rule)"
  - "sourceNames 화이트리스트 3개 (EdgeMeasureTypeList + EdgeDirectionList + EdgePolarityList) — Phase 18 CO-01 패턴 적용, [Browsable(false)] 소스 강제 포함"
  - "K&R 브레이스 스타일 — FAIConfig.cs 기존 스타일 일관 유지 (DatumConfig + DynamicPropertyHelper 와 동일)"

requirements-completed: [QUAL-03, CO-02]

duration: 4min
completed: 2026-05-07
---

# Phase 19 Plan 02: FAIConfig ICustomTypeDescriptor + EdgeMeasureType Dynamic Dropdown Summary

**FAIConfig 에 ICustomTypeDescriptor 구현 + EdgeMeasureType 드롭다운 추가 — DynamicPropertyHelper.FilterProperties 위임으로 CircleDiameter 선택 시 에지 파라미터 6종(+2 List)을 PropertyGrid 에서 숨김. INI 직렬화 비간섭 + DatumConfig 회귀 0 (CO-02 재충족).**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-07T23:43:26Z
- **Completed:** 2026-05-07T23:46:44Z
- **Tasks:** 2 (단일 atomic commit 으로 통합)
- **Files modified:** 1

## Accomplishments

- `FAIConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor` 인터페이스 확장
- `EdgeMeasureType` string 필드 추가 — 기본값 `"EdgePairDistance"`, `[Category("Edge|Measurement")]`, `[ItemsSourceProperty(nameof(EdgeMeasureTypeList))]`, ParamBase 자동 INI 직렬화
- `EdgeMeasureTypeList` 게터 — `[PropertyTools.DataAnnotations.Browsable(false)]`, `MeasurementFactory.GetTypeNames()` 단일 소스 (하드코딩 금지)
- `IsHiddenForEdgeMeasureType(string, string)` private static — CircleDiameter 분기에서 `EdgeDirection`/`EdgeDirectionList`/`EdgePolarity`/`EdgePolarityList`/`EdgeSelection`/`EdgeSampleCount`/`EdgeTrimCount`/`Sigma` 8개 hide 조건
- `GetProperties(Attribute[])` 구현 — `DynamicPropertyHelper.FilterProperties(this, attributes, name => IsHiddenForEdgeMeasureType(name, EdgeMeasureType), sourceNames)` 단일 위임 호출
- `GetProperties()` 무인자 오버로드 — `TypeDescriptor.GetProperties(this, true)` 위임 (System.ComponentModel 우회 사용처 보호)
- ICustomTypeDescriptor stub 10개 모두 구현 (`GetAttributes`, `GetClassName`, `GetComponentName`, `GetConverter`, `GetDefaultEvent`, `GetDefaultProperty`, `GetEditor`, `GetEvents` × 2, `GetPropertyOwner`)
- sourceNames 화이트리스트 3개 (`EdgeMeasureTypeList`, `EdgeDirectionList`, `EdgePolarityList`) — Phase 18 CO-01 [Browsable(false)] 소스 강제 포함 패턴
- 모든 신규 라인에 `//260507 hbk Phase 19 QUAL-03` 주석 부착 (MEMORY.md feedback rule 준수)
- msbuild Debug/x64 PASS — errors 0, 신규 warning 0 (pre-existing 2건은 무관: VirtualCamera.cs CS0162, VisionAlgorithmService.cs CS0219)
- DatumConfig.cs 무수정 — Phase 17 Test 8 (TLI/CTH/VTH 동적 노출) 코드 동일성으로 회귀 0 (CO-02 충족)

## Task Commits

Task 1 + Task 2 가 동일 파일(FAIConfig.cs) 의 상호 의존 변경이며 Task 1 단독 적용 시 빌드가 깨지므로(ICustomTypeDescriptor 미구현 멤버 CS0535) 단일 atomic commit 으로 통합:

1. **Task 1 + Task 2 통합: FAIConfig ICustomTypeDescriptor + EdgeMeasureType + 동적 hide** — `1046cd7` (feat)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` (수정, +58/-2 줄)
  - line 10: 클래스 선언 변경 — `: ParamBase` → `: ParamBase, System.ComponentModel.ICustomTypeDescriptor`
  - lines 47-60: `EdgeMeasureType` + `EdgeMeasureTypeList` 신규 필드 (Edge|Measurement Category 첫 번째 항목)
  - lines 200-244: ICustomTypeDescriptor 구현 블록 (`GetProperties` × 2 + stub 10개 + `IsHiddenForEdgeMeasureType` 메서드)

## Decisions Made

- **Task 1+Task 2 단일 commit 통합 (Rule 3 auto-fix - blocking issue):** PLAN 은 Task 1 (클래스 선언 + 필드) 와 Task 2 (인터페이스 메서드 구현) 를 별도 commit 으로 명시했으나, Task 1 단독 적용 시 `ICustomTypeDescriptor` 인터페이스 멤버 미구현으로 CS0535 빌드 에러 발생 — 빌드 무결성 우선 원칙 (`task_commit_protocol` "post-commit deletion check" 와 같은 가드 정신) 으로 두 task 의 변경을 한 atomic commit 으로 묶음. PLAN 의 acceptance criteria 와 done 조건은 단일 commit 결과로도 모두 충족.
- **EdgeMeasureType 위치 — 기존 `[Category("Edge|Measurement")]` 어트리뷰트 이동:** PLAN action note 에 명시된 대로 기존 `EdgeThreshold` 위의 `[Category("Edge|Measurement")]` 를 EdgeMeasureType 으로 이동 (EdgeMeasureType 이 같은 Category 첫 번째 항목으로 표시). EdgeThreshold 의 Category 어트리뷰트는 자연스럽게 부모 Category 그룹에 귀속.
- **sourceNames null-safe 미사용:** Wave 1 에서 sourceNames null 안전성을 추가했으나, FAIConfig 의 GetProperties 호출 경로는 항상 non-null HashSet 전달 — null 가드 발동 없음. Wave 1 안전 장치는 미래 헬퍼 사용처에서만 의의.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking Issue] Task 1+Task 2 단일 commit 으로 통합**
- **Found during:** Task 1 commit 직전 빌드 무결성 검토
- **Issue:** Task 1 만 적용 시 클래스 선언이 `: ICustomTypeDescriptor` 를 갖지만 인터페이스 멤버(GetProperties × 2, GetAttributes, GetPropertyOwner 등 12개) 미구현 → CS0535 컴파일 에러 → Task 1 commit 직후 빌드 깨짐 → atomic commit 정신 위반.
- **Fix:** Task 1 (클래스 선언 + 필드) 와 Task 2 (인터페이스 메서드 구현) 의 변경을 한 atomic commit (`1046cd7`) 으로 통합. PLAN 의 acceptance criteria 는 통합 commit 결과로도 모두 충족 (인터페이스 선언 + 필드 + 구현 동시 만족).
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs`
- **Verification:** msbuild Debug/x64 PASS (errors=0, 신규 warning=0). 모든 acceptance criteria grep 통과.

---

**Total deviations:** 1 auto-fixed (Rule 3 - blocking issue)
**Impact on plan:** PLAN 의 변경 내용/패턴/검증 기준 모두 그대로 적용. commit 분할 방식만 단일화 — 빌드 무결성 우선.

## Issues Encountered

- 없음. 빌드 1회에 PASS.

## TDD Gate Compliance

이 plan 은 `type: execute` (TDD 게이트 비대상). 회귀는 정적 grep + msbuild + DatumConfig 무수정 보장으로 검증.

## Verification Results

PLAN 의 verification 6단계 모두 통과:

1. ✓ `Select-String -Pattern "ICustomTypeDescriptor"` → FAIConfig.cs 4건 (선언 + 주석 등)
2. ✓ `Select-String -Pattern "EdgeMeasureType"` → FAIConfig.cs 11건 (필드 + List + IsHidden 매개변수 + 호출 + 주석)
3. ✓ `Select-String -Pattern "IsHiddenForEdgeMeasureType"` → FAIConfig.cs 2건 (정의 + 람다 호출)
4. ✓ `Select-String -Pattern "DynamicPropertyHelper\.FilterProperties"` → FAIConfig.cs 1건 (단일 위임 호출)
5. ✓ DatumConfig.cs 회귀 — `DynamicPropertyHelper.FilterProperties` 1건(line 570), `IsHiddenForAlgorithm` 3건(주석+람다+정의) 모두 보존, 코드 무수정으로 Phase 17 Test 8 동작 회귀 0 보장
6. ✓ msbuild Debug/x64 → `DatumMeasurement.exe` 산출 성공, errors 0, 신규 warning 0 (pre-existing 2 only)

추가 검증:

- ✓ K&R 브레이스 스타일 — FAIConfig.cs 기존 스타일 일관 유지 (`{` 선언 줄 끝, `private static bool IsHiddenForEdgeMeasureType(string name, string edgeMeasureType) {` 와 같이)
- ✓ 모든 신규 라인에 `//260507 hbk Phase 19 QUAL-03` 주석 (MEMORY.md feedback rule 준수)
- ✓ C# 7.2 호환 — nullable refs / switch expressions / record 미사용
- ✓ MeasurementFactory.GetTypeNames() 단일 소스 호출 (드롭다운 옵션 하드코딩 금지)
- ✓ ParamBase INI 직렬화 비간섭 — GetType().GetProperties() Reflection 경로 (ParamBase.cs L75/L325/L370 확인)

## CO-02 Phase 17 Test 8 재검증 가이드 (UAT 사용자 확인용)

DatumConfig.cs 코드는 이번 plan 에서 무수정. Phase 17-02 의 동작이 그대로 유지된다. 사용자가 PropertyGrid 동작 확인이 필요한 경우:

1. **TLI 알고리즘 (TwoLineIntersect):**
   - Datum 노드 선택 → AlgorithmType = "TwoLineIntersect"
   - PropertyGrid 노출: `Line1_*`, `Line2_*` (ROI + Edge 그룹)
   - PropertyGrid 숨김: `Circle_*`, `CircleROI_*`, `CircleCenter_*`, `CircleDetected_*`, `Vertical_*`, `Horizontal_A_*`, `Horizontal_B_*`

2. **CTH 알고리즘 (CircleTwoHorizontal):**
   - PropertyGrid 노출: `Circle_*` (`Circle_RadialDirection` 포함, `Circle_EdgeDirection` 제외 — Phase 17 D-03), `Horizontal_A_*`, `Horizontal_B_*`
   - PropertyGrid 숨김: `Line1_*`, `Line2_*`, `Line1Detected_*`, `Line2Detected_*`, `Vertical_*`

3. **VTH 알고리즘 (VerticalTwoHorizontal):**
   - PropertyGrid 노출: `Vertical_*`, `Horizontal_A_*`, `Horizontal_B_*`
   - PropertyGrid 숨김: `Line1_*`, `Line2_*`, `Line1Detected_*`, `Line2Detected_*`, `Circle_*`, `CircleROI_*`, `CircleCenter_*`, `CircleDetected_*`

4. **Circle_RadialDirection 드롭다운:** Inward/Outward 두 값만 표시 (Phase 18 CO-01 회귀 없음)

코드 동일성 보장으로 자동 PASS — 사용자 UAT 는 선택 사항이며 동작 회귀는 Phase 19-01 SUMMARY (commit `4342cdc` 검증) 에서 이미 입증됨.

## FAIConfig EdgeMeasureType UAT 가이드 (신규 동작)

사용자가 신규 PropertyGrid 동작 확인이 필요한 경우:

1. **EdgePairDistance (기본):**
   - FAI 노드 선택 → EdgeMeasureType 드롭다운에서 "EdgePairDistance" (기본값)
   - PropertyGrid 노출: `Sigma`, `EdgeThreshold`, `EdgeDirection`, `EdgeSelection`, `EdgeSampleCount`, `EdgeTrimCount`, `EdgePolarity`, ROI/Calibration 모두 표시
   - 드롭다운 옵션: `EdgePairDistance`, `PointToLineDistance`, `PointToPointDistance`, `LineToLineAngle`, `CircleDiameter`, `LineToLineDistance` (6종, MeasurementFactory.GetTypeNames 그대로)

2. **CircleDiameter 선택 시:**
   - PropertyGrid 노출: `EdgeMeasureType`, `EdgeThreshold`, ROI 그룹, Calibration 그룹
   - PropertyGrid 숨김: `Sigma`, `EdgeDirection`, `EdgeDirectionList`, `EdgePolarity`, `EdgePolarityList`, `EdgeSelection`, `EdgeSampleCount`, `EdgeTrimCount` (6 필드 + 2 List = 8개 hide)

3. **다른 타입 (PointToLineDistance, PointToPointDistance, LineToLineAngle, LineToLineDistance):**
   - PropertyGrid 노출: EdgePairDistance 와 동일 (모두 표시, 숨김 없음)

4. **INI 직렬화 검증:**
   - 레시피 저장 → INI 파일에 `EdgeMeasureType=CircleDiameter` 같은 키 기록 확인 (ParamBase.Save string switch)
   - 새 레시피 로드 → 기본값 `"EdgePairDistance"` 가 유지됨 (INI 키 미존재 시 ParamBase.Load 가 property 기본값 보존)

## User Setup Required

없음 — 외부 서비스 설정 불필요. INI 하위 호환 유지 (EdgeMeasureType 키 미존재 시 자동 기본값 fallback).

## Next Phase Readiness

Phase 19 완료. 다음 Phase 20 (코드 스타일 정리: QUAL-02, QUAL-04) 또는 v1.1 후속 phase 진행 가능.

Phase 19 산출:
- DynamicPropertyHelper.FilterProperties 헬퍼 (Wave 1, plan 01)
- DatumConfig 헬퍼 위임 (Wave 1, plan 01)
- FAIConfig ICustomTypeDescriptor + EdgeMeasureType 동적 노출 (Wave 2, plan 02)

미래 모델 클래스에 동일 패턴 확장 시 DynamicPropertyHelper.FilterProperties 단일 헬퍼 재사용 가능 (CONTEXT.md deferred: v1.2 MeasurementBase 파생 클래스별 PropertyGrid 필터).

## Self-Check: PASSED

**Files claimed modified — all verified:**
- ✓ `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` (FOUND, +58/-2 줄)

**Commits claimed — all verified in git log:**
- ✓ `1046cd7` (Task 1+2 통합: feat FAIConfig ICustomTypeDescriptor + EdgeMeasureType)

**Acceptance criteria — all met:**
- ✓ `public class FAIConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor {` (line 11)
- ✓ `public string EdgeMeasureType { get; set; } = "EdgePairDistance";` (line 54)
- ✓ `[ItemsSourceProperty(nameof(EdgeMeasureTypeList))]` (line 53)
- ✓ `public List<string> EdgeMeasureTypeList` getter w/ `MeasurementFactory.GetTypeNames()` (lines 58-60)
- ✓ `[PropertyTools.DataAnnotations.Browsable(false)]` on EdgeMeasureTypeList (line 57)
- ✓ `GetProperties(System.Attribute[] attributes)` 메서드 (line 204)
- ✓ `GetProperties()` 무인자 오버로드 (line 212)
- ✓ `IsHiddenForEdgeMeasureType` private static 메서드 (line 230)
- ✓ ICustomTypeDescriptor stub 10개 모두 구현 (lines 216-225, GetAttributes ~ GetPropertyOwner)
- ✓ sourceNames HashSet 3개 (EdgeMeasureTypeList + EdgeDirectionList + EdgePolarityList)
- ✓ DynamicPropertyHelper.FilterProperties 호출 (line 210)
- ✓ msbuild Debug/x64 PASS, 신규 warning 0
- ✓ 모든 신규 라인 `//260507 hbk Phase 19 QUAL-03` 주석 부착

---

*Phase: 19-propertygrid-dynamic-exposure*
*Plan: 02*
*Completed: 2026-05-07*
