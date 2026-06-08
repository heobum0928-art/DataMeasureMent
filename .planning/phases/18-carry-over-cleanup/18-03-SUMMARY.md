---
phase: 18-carry-over-cleanup
plan: "03"
subsystem: ui
tags: [wpf, modal, datum, teaching, error-message]

# Dependency graph
requires:
  - phase: 17
    provides: FormatTeachError(string err) 정의 + InvokeTryTeachDatum 완료 콜백 구조
provides:
  - FormatTeachError(DatumConfig datum, string err) — [DatumName] 접두사 포함 에러 메시지
affects: [Phase 19, Phase 24]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "에러 메시지 포매터에 도메인 객체(DatumConfig) 인자 전달 — null/empty 가드 포함 접두사 패턴"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "FormatTeachError 인자에 DatumConfig 추가 — datum.DatumName 기반 prefix, null/empty 가드로 안전성 보장"
  - "FormatFindError 미수정 — CO-06 범위 외, 별도 메서드 독립 유지"

patterns-established:
  - "에러 메시지 prefix 패턴: (datum != null && !string.IsNullOrEmpty(datum.DatumName)) ? \"[\" + datum.DatumName + \"] \" : \"\""

requirements-completed:
  - CO-06

# Metrics
duration: 8min
completed: 2026-05-05
---

# Phase 18 Plan 03: FormatTeachError DatumName 접두사 Summary

**FormatTeachError 시그니처를 DatumConfig 인자로 확장하여 티칭 실패 모달에 [DatumName] 접두사를 포함 — 다중 Datum 환경에서 실패 원인 즉시 식별 가능**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-05T03:55:00Z
- **Completed:** 2026-05-05T04:03:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- FormatTeachError(string err) → FormatTeachError(DatumConfig datum, string err) 시그니처 확장
- datum.DatumName 기반 "[DatumName] " 접두사 로직 추가 (null/empty 가드 포함)
- 유일한 호출 사이트(L1621)에서 _editingDatum 전달 — 기존 Phase 17 null 체크 재활용
- msbuild Debug/x64 경고 증가 없이 빌드 PASS

## Task Commits

1. **Task 1: FormatTeachError 시그니처 확장 + 호출 사이트 수정** - `064b6a8` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — FormatTeachError 메서드 본문 교체 + 호출 사이트 1곳 수정

## Decisions Made

- FormatTeachError에 DatumConfig 인자 추가 — 호출 시점에서 _editingDatum이 null 체크 완료되어 있어 안전하며, 메서드 내부에도 null 가드 추가로 이중 보호
- FormatFindError는 CO-06 범위 외 — 수정하지 않음

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- CO-06 완료. Phase 18 잔여: 18-04(CO-05 Test Find 버그), 18-05 통합 검증
- 18-04 실행 준비 완료

## Threat Flags

없음 — DatumName은 사용자가 직접 설정하는 값이며 내부 경로 노출 없음. null/empty 가드 적용됨 (T-18-03-01 accept 처리).

## Self-Check

- [x] `FormatTeachError` 정의 + 호출: 2건 이상 grep 확인 완료
- [x] `datum.DatumName` 사용: 2건 확인 완료
- [x] `DatumConfig datum, string err` 시그니처: 1건 확인 완료
- [x] `_editingDatum, error` 호출 사이트: 1건 확인 완료
- [x] 커밋 064b6a8 존재 확인
- [x] msbuild Debug/x64 PASS (DatumMeasurement.exe 생성 확인)

## Self-Check: PASSED

---
*Phase: 18-carry-over-cleanup*
*Completed: 2026-05-05*
