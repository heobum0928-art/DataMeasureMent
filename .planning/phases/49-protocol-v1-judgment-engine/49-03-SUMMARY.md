---
phase: 49-protocol-v1-judgment-engine
plan: 03
subsystem: infra
tags: [tcp, encoding, refactor, control-protocol-v1]

# Dependency graph
requires:
  - phase: 48-protocol-v1-test-result-site-material
    provides: "VisionServer 생성자 ApplyEncoding(Utf8) 호출처 + 48-REVIEW.md CR-01(CO-48-01) 가이드"
provides:
  - "TcpServer.EncodingType instance 필드 (static 제거)"
  - "TcpServer.ApplyEncoding instance 메서드 (static 제거)"
  - "다중 인스턴스 전역 인코딩 오염 시한폭탄 구조적 제거 (CO-48-01 종결)"
affects: [49, 50, control-protocol]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "중첩 클래스(ConnectedClient)에서 부모 instance private 멤버 접근 = Parent.Field 한정"

key-files:
  created: []
  modified:
    - WPF_Example/TcpServer/TcpServer.cs

key-decisions:
  - "EncodingType/ApplyEncoding/eEncoding 기존 명명 보존 (D-10) — 호출처 호환 + 회귀 0 우선, 이름 변경 회피"
  - "Header/Trailer static 필드는 CO-48-01 범위 밖 (기록만, Phase 48 이전부터 존재)"

patterns-established:
  - "static 전역 가변 상태 → instance 전환 시 중첩 클래스 소비처는 Parent.Field 로 한정"

requirements-completed: [PROTO-05]

# Metrics
duration: 5min
completed: 2026-06-23
---

# Phase 49 Plan 03: EncodingType/ApplyEncoding 인스턴스화 (CO-48-01/D-09) Summary

**TcpServer.EncodingType static 필드 + ApplyEncoding static 메서드를 instance 로 전환하여 다중 인스턴스 전역 인코딩 오염 시한폭탄을 제거 (인코딩 동작 회귀 0)**

## Performance

- **Duration:** 5 min
- **Started:** 2026-06-23T09:35:00Z
- **Completed:** 2026-06-23T09:40:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- `TcpServer.EncodingType` static 프로퍼티 → instance 프로퍼티 (라인 78)
- `ApplyEncoding` `protected static` → `protected` instance 메서드 (라인 83)
- `ConvertMessage(string)` / `ConvertMessage(byte[])` 의 `switch (EncodingType)` → `switch (Parent.EncodingType)` 2곳 한정 (중첩 클래스 ConnectedClient 가 부모 instance 필드 경유 접근)
- VisionServer.cs 무변경 확인 — 생성자(instance 컨텍스트) 내 `ApplyEncoding(MessageEncodingType.Utf8)` 호출이 instance 메서드로 그대로 유효
- 빌드 PASS (0 errors, 신규 warning 0 — baseline 동일)

## Task Commits

1. **Task 1: EncodingType/ApplyEncoding 인스턴스화 + ConvertMessage 접근 한정** - `c12f4d3` (refactor)

**Plan metadata:** (final docs commit)

## Files Created/Modified
- `WPF_Example/TcpServer/TcpServer.cs` - EncodingType/ApplyEncoding static→instance + ConvertMessage 2곳 Parent.EncodingType 한정

## Decisions Made
- EncodingType/ApplyEncoding/eEncoding 기존 명명을 보존 (D-10). 회귀 0 우선 정책상 이름 변경은 위험 — static 키워드 제거 + 접근 한정만 수행.
- Header/Trailer static 필드는 동일 문제이나 CO-48-01 범위 밖 (Phase 48 이전부터 존재) — 코드 주석으로 기록만 하고 미변경.

## Deviations from Plan

None - plan executed exactly as written. (예상했던 접근성 오류 없이 컴파일 PASS — C# 중첩 클래스가 외곽 클래스 private 멤버 접근 허용, Rule 1 즉시 수정 불필요.)

## Issues Encountered
None.

## Carry-over / STATE 갱신
- **CO-48-01 종결:** Phase 48 review CR-01 (EncodingType static→instance) 흡수 완료. STATE/carry-over 목록에서 CO-48-01 closed 로 표기 권장.
- **잔존 기록 (범위 밖):** TcpServer.cs Header/Trailer static 필드 — 동일한 전역 가변 상태 문제이나 본 plan 범위 밖. 향후 정리 후보로 CONTEXT deferred 에 기록됨.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- CO-48-01 구조적 결함 제거 완료. 제어 스택(VisionResponsePacket/InspectionSequence 판정 엔진) 작업의 토대 안전화.
- **human_needed (선택):** UseProtocolV1=true 로 실제 $TEST/$RESULT 송수신 시 한글/UTF-8 메시지 인코딩이 Phase 48 baseline 과 동일한지 UAT 확증 (코드 경로상 회귀 0 — VisionServer 생성자 분기 무변경).

## Self-Check: PASSED

- FOUND: `.planning/phases/49-protocol-v1-judgment-engine/49-03-SUMMARY.md`
- FOUND: commit `c12f4d3`

---
*Phase: 49-protocol-v1-judgment-engine*
*Completed: 2026-06-23*
