---
phase: quick-260814-f6i
plan: 01
subsystem: vision-inspection
tags: [halcon, datum, simul-mode, dualimage, fallback]

# Dependency graph
requires: []
provides:
  - "TryLoadStaticDualDatumImages 가 TeachingImagePath/_Vertical 부재 시 ShotParam.SimulImagePath 로 폴백"
affects: [datum-teaching, simul-mode-testing, protocol-tcp-roundtrip]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "1-image datum 폴백 헬퍼(LoadDatumImageFromPath)를 DualImage 경로에서도 재사용(신규 로직 발명 금지)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "새 폴백 로직을 작성하지 않고 기존 LoadDatumImageFromPath(datum, path, false) 헬퍼를 가로/세로 각각 재호출 — 두 축이 서로 다른 HImage 인스턴스를 갖도록 보장(이중 Dispose 방지)"
  - "폴백 발생 시 ELogType.Trace 로그로 흔적을 남겨 '티칭 이미지로 검출했다'는 오독을 방지(T-f6i-01 mitigate)"

patterns-established: []

requirements-completed: [QUICK-260814-F6I]

# Metrics
duration: ~15min
completed: 2026-08-14
---

# Quick 260814-f6i: DualImage Datum SimulImagePath 폴백 Summary

**DualImage Datum(VerticalTwoHorizontalDualImage)의 TryLoadStaticDualDatumImages 가 TeachingImagePath/_Vertical 미설정·부재 시 기존 1-image 경로가 쓰던 LoadDatumImageFromPath 헬퍼를 재사용해 ShotParam.SimulImagePath 로 폴백하도록 확장 — SIMUL $TEST(z_index=0) 프로토콜 왕복 검증을 이미지 부재로 즉시 막히지 않게 함**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-14T02:0x (세션 시작, 정확한 타임스탬프 미기록)
- **Completed:** 2026-08-14T02:08:01Z
- **Tasks:** 1/2 완료 (Task 2 는 인간 검증 대기 — 체크포인트)
- **Files modified:** 1

## Accomplishments
- `TryLoadStaticDualDatumImages` 가 가로/세로 각각 `LoadDatumImageFromPath(datum, path, false)` 를 호출해 `teachingPath → ShotParam.SimulImagePath → null+로그` 순서로 폴백하도록 재작성됨(기존 헬퍼 재사용, 신규 로직 없음)
- 폴백 발생 시 축 구분 `ELogType.Trace` 로그, 완전 실패 시 축 구분 `ELogType.Error` 로그 + 부분 성공분 `Dispose` 정리 + `false` 반환 유지
- `TryGrabOrLoadDualDatumImages` 상단 stale 주석(682~684줄)을 새 동작(빈 경로도 SimulImagePath 폴백으로 성립)에 맞게 갱신
- Debug/x64 빌드 성공, 경고는 baseline 12줄(CS0618×10 + CS0162×2)만 존재, `error CS`/`error MSB` 0건

## Task Commits

Each task was committed atomically:

1. **Task 1: TryLoadStaticDualDatumImages 에 SimulImagePath 폴백 추가 + stale 주석 갱신** - `0ae808c` (feat)

Task 2(체크포인트: `checkpoint:human-verify`, gate=`blocking`)는 코드 변경이 없으며 사용자의 실제 SIMUL 앱 실행 + TCP 왕복 검증 대기 중이므로 커밋 대상이 아님.

**Plan metadata:** 오케스트레이터가 후속 처리(본 SUMMARY 는 문서 커밋에서 별도 처리됨).

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `TryLoadStaticDualDatumImages` 폴백 로직 교체(+27/-16) + `TryGrabOrLoadDualDatumImages` 상단 stale 주석 갱신

## Decisions Made
- 새 폴백 코드를 직접 작성하지 않고 이미 1-image datum 경로에 구현되어 있던 `LoadDatumImageFromPath(datum, path, false)` 헬퍼를 가로/세로 각각 재호출 — 플랜 CONTEXT 의 "신규 폴백 로직 발명 금지" 지시를 그대로 따름. 이 헬퍼는 호출마다 `new HImage(...)` 로 독립 인스턴스를 생성하므로 호출부 `finally` 이중 `Dispose` 위험이 구조적으로 없음.
- 폴백이 조용히 성립하면 "정상 티칭 이미지로 검출됐다"고 오독될 위험이 있어(threat T-f6i-01), 폴백이 발생한 축마다 `ELogType.Trace` 로그를 추가함(플랜에 명시된 요구사항).

## Deviations from Plan

None - plan executed exactly as written. 코드 사전 확인(1단계) 결과 실제 파일이 플랜 CONTEXT 에 적힌 코드와 정확히 일치했으며, 2단계 교체 코드를 그대로 적용, 3단계 stale 주석도 플랜 제시 문구를 그대로 반영함.

## Issues Encountered
None. 초기 검증에서 `grep -c` 가 매치 0건일 때 exit code 1 을 반환해 `&&` 체인이 조기 종료되는 셸 동작이 있었으나, 이는 실행 스크립트 구성 문제였을 뿐 코드/검증 결과 자체와는 무관 — 각 grep 을 개별 실행해 10개 항목 전부 기대값과 일치함을 확인함.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Task 1 (코드) 완료, Task 2 (인간 검증) 는 블로킹 체크포인트로 대기 중입니다.**

Task 2 진행을 위해 사용자가 직접 수행해야 하는 절차 (PLAN.md `<how-to-verify>` 원문):

1. Top SIMUL 레시피를 로드하고, 대상 SHOT 의 검사이미지(SimulImagePath)가 실제로 등록되어 있는지 확인합니다. (비어 있으면 InspectionListView 에서 "검사이미지 Grab" 으로 먼저 한 장 확보해야 합니다 — 이 폴백은 그 이미지를 씁니다.)
2. 대상 Datum 의 AlgorithmType 이 `VerticalTwoHorizontalDualImage` 인지, 그리고 그 Datum 의 TeachingImagePath / TeachingImagePath_Vertical 이 비어 있는지 확인합니다.
3. 핸들러/목 클라이언트에서 `$TEST:site,Type,자재번호@` 형태의 z_index=0(Datum) 통신 테스트를 보냅니다.
4. 기대 결과: 로그에 "가로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다" 가 더 이상 나오지 않고, 대신 "가로축/세로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백" Trace 로그가 보이며, `$RESULT` 응답이 이미지 취득 실패로 즉시 F 가 아니라 끝까지 왕복해서 돌아옵니다.
5. 회귀 확인: 티칭 이미지가 정상 등록된 다른 Datum(1-image, DualImage 둘 다)으로 평소처럼 RUN 을 돌려 기존과 동일한 결과가 나오는지 확인합니다(폴백 Trace 로그가 뜨지 않아야 정상).

**Resume-signal:** "approved" 라고 회신하시거나, 다르게 동작한 부분을 알려 주세요.

---
*Phase: quick-260814-f6i*
*Completed: 2026-08-14 (Task 1만; Task 2 는 인간 검증 대기)*

## Self-Check: PASSED
- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND: .planning/quick/260814-f6i-simul-datum-test-dualimage-datum-teachin/260814-f6i-SUMMARY.md
- FOUND commit: 0ae808c
