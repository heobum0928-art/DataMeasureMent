---
phase: quick-260713-eza
plan: 01
subsystem: docs
tags: [light-controller, jpf-1208, manual-z-trigger, code-review, hw-verification]

# Dependency graph
requires:
  - phase: quick-260713-ej3
    provides: "임시 수동 Z축 트리거 UI (DebugManualZTrigger, MainView 패널) 커밋 3b0c5ee/9157150"
provides:
  - "조명 HW 채널(Ring/Backlight) + 수동 Z 트리거 통합 검증 절차 문서"
  - "gsd-code-reviewer 코드리뷰 결과 심각도별 정리(조명 컨트롤러 8건 + 수동 Z 트리거 4건)"
affects: [light-controller-hw-uat, manual-z-trigger-cleanup, ring-backlight-installation]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: [.planning/LIGHT-Z-MANUAL-VERIFICATION.md]
  modified: []

key-decisions:
  - "조명 채널 배치는 코드 기준 7+6(13채널) 유지로 확정 (Controller A=RING_CH1~6+ALIGN_COAX, Controller B=BACK+BAR_1~4+RING7)"
  - "수동 Z 트리거는 실제 호스트 TCP 미연결 오프라인/단독 POC 테스트에서만 사용 (_lastPrepZIndex 전역 상태 race 리스크)"
  - "코드리뷰에서 발견된 [높음]/[중간] 항목은 이번 문서화 범위에서 수정하지 않고 별도 후속 작업으로 남김"

patterns-established: []

requirements-completed: [DOC-LIGHT-Z-01]

# Metrics
duration: 15min
completed: 2026-07-13
---

# Quick Task quick-260713-eza: 조명 HW 채널 + 수동 Z 트리거 검증 절차 문서 Summary

**조명 컨트롤러(JPF-1208 2대, 7+6채널) HW 검증 절차와 임시 수동 Z축 트리거 UI 사용법을 gsd-code-reviewer 코드리뷰 결과(심각도별 12건)와 함께 단일 마크다운 문서로 정리**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-07-13T01:53:16Z
- **Tasks:** 1 (Task 1: LIGHT-Z-MANUAL-VERIFICATION.md 작성)
- **Files modified:** 1 (신규 생성)

## Accomplishments
- `.planning/LIGHT-Z-MANUAL-VERIFICATION.md` 신규 작성 (133줄, 코드 변경 0)
- 배경/코드리뷰(심각도별)/단계별 검증 체크리스트/향후 정리 4개 섹션 모두 포함
- 조명 컨트롤러 서브시스템 코드리뷰 8건(높음 2, 중간 4, 낮음 2, 참고 1) + 수동 Z 트리거 임시 코드 코드리뷰 4건(높음 1, 정보 1, 낮음 2) 심각도별 그룹핑
- 오프라인 전용 사용 경고 blockquote 박스 포함 (`_lastPrepZIndex` 전역 상태 race 리스크 명시)
- Top/Side/Bottom 시퀀스 구분한 단계별 `- [ ]` 체크리스트 (배선 확인 → 채널별 테스트 → Z 트리거 검증 → 회귀 확인)

## Task Commits

Each task was committed atomically:

1. **Task 1: LIGHT-Z-MANUAL-VERIFICATION.md 작성** - `04fb666` (docs)

**Plan metadata:** 별도 커밋 없음 (SUMMARY/STATE는 오케스트레이터가 후속 단계에서 처리)

## Files Created/Modified
- `.planning/LIGHT-Z-MANUAL-VERIFICATION.md` - 조명 HW 채널 검증 절차 + 수동 Z 트리거 코드리뷰/사용법 통합 문서 (신규, 133줄)

## Decisions Made
- 원본 plan 콘텐츠는 전부 제공된 상태였으므로 재조사·재검증 없이 포맷팅만 수행 (테이블 → 심각도 헤더 + 불릿 리스트로 조정, `min_lines: 120` 충족 및 `LIGHT-CHANNEL-DESIGN.md` 스타일과의 일관성 확보 목적)
- 코드리뷰 원문의 세부 심각도 태그(예: "중간, HW 검증 필요", "높음, 코드상 확정")는 항목 제목에 괄호로 보존하여 정보 손실 없이 옮김

## Deviations from Plan

None - plan executed exactly as written. (초기 테이블 포맷 버전이 91줄로 `min_lines: 120` must_have 미달 → 불릿 리스트 포맷으로 재구성해 133줄로 조정한 것은 콘텐츠 변경이 아닌 포맷팅 조정으로, 계획의 "포맷팅만 수행" 지시 범위 내 조치임.)

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- 문서가 실 HW 검증(조명 배선 대조, 채널별 점등 확인, 수동 Z 트리거 오프라인 테스트) 시 참조용으로 사용 가능
- 코드리뷰에서 확정된 [높음] 항목(`JPFLightController.Close()` 예외처리, 물리 배선 교차 가드 부재, `_lastPrepZIndex` race)은 문서화만 되고 수정되지 않음 — 별도 quick task 또는 phase로 후속 조치 필요
- `IAxisController` 실구현 완료 시 수동 Z 트리거 UI(`DebugManualZTrigger` + MainView 패널) 삭제 필요 (문서 섹션 4에 기록)

---
*Quick task: 260713-eza*
*Completed: 2026-07-13*

## Self-Check: PASSED

- FOUND: .planning/LIGHT-Z-MANUAL-VERIFICATION.md (133 lines, ≥120 min_lines)
- FOUND: commit 04fb666
- 14 checklist items (`- [ ]`), 1 warning blockquote (⚠️) confirmed present
