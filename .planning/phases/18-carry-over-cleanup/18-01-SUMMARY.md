---
phase: 18-carry-over-cleanup
plan: 01
subsystem: ui
tags: [wpf, propertygrid, propertytools, ICustomTypeDescriptor, DatumConfig, halcon]

# Dependency graph
requires:
  - phase: 17
    provides: "DatumConfig ICustomTypeDescriptor + Circle_RadialDirectionList 정의 (Phase 17 D-02)"
provides:
  - "DatumConfig.GetProperties(Attribute[]) allNoFilter whitelist — [Browsable(false)] List<> 소스 프로퍼티 강제 포함"
  - "Circle_RadialDirection PropertyGrid 콤보박스 Inward/Outward 2항목 정상 표시"
affects: [19-propertygrid-generalize]

# Tech tracking
tech-stack:
  added: []
  patterns: ["ICustomTypeDescriptor.GetProperties: allNoFilter+sourceNames whitelist 패턴으로 Browsable(false) 소스 프로퍼티 강제 포함"]

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs

key-decisions:
  - "allNoFilter + sourceNames HashSet whitelist 패턴 — attributes 필터 없이 재조회 후 List<> 소스 프로퍼티 명시 추가 (keep.Exists 중복 방지)"

patterns-established:
  - "ItemsSource 소스 List<> 프로퍼티 whitelist 패턴: TypeDescriptor.GetProperties(this, true) 재조회 → HashSet<string> sourceNames 교차 → keep에 추가 (중복 skip)"

requirements-completed: [CO-01]

# Metrics
duration: 10min
completed: 2026-05-05
---

# Phase 18 Plan 01: Carry-over Cleanup — DatumConfig ItemsSource Whitelist Summary

**DatumConfig.GetProperties(Attribute[]) allNoFilter+sourceNames whitelist로 Circle_RadialDirection 콤보박스 Inward/Outward 2항목 표시 버그 수정**

## Performance

- **Duration:** 10 min
- **Started:** 2026-05-05T00:00:00Z
- **Completed:** 2026-05-05T00:10:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- `GetProperties(Attribute[])` 에 `allNoFilter` + `sourceNames` whitelist 블록 추가 — `[Browsable(false)]` List<> 소스 프로퍼티가 BrowsableAttribute.Yes 필터로 제외되는 근본 원인 수정
- `Circle_RadialDirectionList` 가 keep 컬렉션에 명시적으로 포함됨 — PropertyTools.Wpf 이름 조회 성공, fallback(Directions 4항목) 해제
- 기존 20개 List<> 소스 프로퍼티 전체 whitelist 등록 (AlgorithmTypeList + 각 ROI별 Direction/Polarity/Selection/RadialDirection)
- Debug/x64 빌드 PASS, 신규 경고 0 (기존 경고 3개 MSB3884/CS0162/CS0219 은 이번 변경 무관)

## Task Commits

1. **Task 1: GetProperties whitelist — ItemsSource 소스 프로퍼티 강제 포함** - `77fd87e` (fix)

**Plan metadata:** 별도 docs 커밋 포함 예정

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — GetProperties(Attribute[]) L545: allNoFilter + sourceNames whitelist 블록 추가 (22 lines)

## Decisions Made
- `keep.Exists(k => k.Name == pd.Name)` 중복 검사 — allNoFilter 재조회 시 이미 IsHiddenForAlgorithm을 통과한 항목이 중복 추가될 가능성 방지
- `Vertical_Edge*List` 3개도 whitelist에 포함 — 플랜 샘플 코드에는 없었으나 실제 파일에 존재하는 프로퍼티이므로 완전성 보장

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing] Vertical_EdgeDirectionList/PolarityList/SelectionList whitelist 누락 보완**
- **Found during:** Task 1 (파일 읽기 후 실제 프로퍼티 목록 확인)
- **Issue:** 플랜 샘플 코드에 Vertical_* List 프로퍼티 3개가 누락됨. 실제 파일(L157-161)에는 존재.
- **Fix:** sourceNames HashSet에 `nameof(Vertical_EdgeDirectionList)`, `nameof(Vertical_EdgePolarityList)`, `nameof(Vertical_EdgeSelectionList)` 추가
- **Files modified:** DatumConfig.cs
- **Verification:** nameof() 컴파일 타임 검증 — 빌드 PASS
- **Committed in:** 77fd87e (Task 1 커밋에 포함)

---

**Total deviations:** 1 auto-fixed (1 missing critical — whitelist 완전성)
**Impact on plan:** VTH 알고리즘 Vertical EdgeDirection/Polarity/Selection 콤보박스도 동일 버그 영향권이었음. 완전 수정.

## Issues Encountered

None — 파일 읽기 후 실제 프로퍼티 이름 확인 절차로 플랜 누락 항목을 사전에 식별하여 한 번에 처리.

## Known Stubs

None — ItemsSource 소스 목록이 EdgeOptionLists.RadialDirections (실제 데이터)에 직접 연결됨.

## Threat Flags

없음 — GetProperties는 내부 PropertyGrid 바인딩 전용 메서드. 외부 입력 없음 (T-18-01-01 accept 그대로).

## Self-Check

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — FOUND (수정 완료)
- commit `77fd87e` — FOUND (git log 확인)
- `allNoFilter` 출현 2회 — PASS
- `Circle_RadialDirectionList` 출현 4회 (≥2) — PASS
- `sourceNames` 출현 2회 — PASS
- msbuild Debug/x64 Build succeeded — PASS

## Next Phase Readiness
- Phase 18-02 (CO-03) 실행 가능
- CTH Datum PropertyGrid Circle_RadialDirection 드롭다운 런타임 UAT는 Phase 18 완료 후 수행 (CO-01 acceptance)

---
*Phase: 18-carry-over-cleanup*
*Completed: 2026-05-05*
