---
phase: 67-stat-01-2026-07-07
plan: 01
subsystem: database
tags: [csv, statistics, cycle-result, settings, hangarian-io]

# Dependency graph
requires: []
provides:
  - "StatisticsSavePath 설정 프로퍼티 (SystemSetting, [DirectoryPath][AutoUpdateText][Category(\"Path|Statistics\")])"
  - "MeasurementHistoryCsvWriter.Append(CycleResultDto) — 측정항목당 1행 일자별 CSV append 정적 서비스"
  - "CycleResultSerializer.SaveAsync 훅 — v2.6/v1.0/수동 3경로 자동 커버"
affects: [67-02, 67-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "static class + static lock 을 이용한 파일 I/O 경합 방지 (RepeatMeasurementStats 와 동일 계열)"
    - "RFC4180 CSV 이스케이프 헬퍼(Esc) — 콤마/따옴표/개행 감지 + 이중 따옴표"
    - "독립 try/catch 훅 — 부가 기능 실패가 핵심 경로(JSON 저장)에 영향 없도록 격리(D-04)"

key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
  modified:
    - WPF_Example/Setting/SystemSetting.cs
    - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
    - WPF_Example/DatumMeasurement.csproj

key-decisions:
  - "StatisticsSavePath 는 LogDeleteDay 뒤 · [Category(\"Path|MapData\")] 앞에 배치하고 자체 [Category(\"Path|Statistics\")] 부여 — SystemSetting.Load()/Save() 의 group 상속 로직상 뒤따르는 MapData 그룹이 즉시 리셋하므로 그룹 누출 0"
  - "CSV 스키마 = 고정 14컬럼(검사일시~OverallCycleResult), 일자별 1파일(yyyyMMdd.csv), OverallJudgement 는 P/F/N 3-state 로 매핑"
  - "MeasurementResultDto.LastSkipReason/LastHasResult/LastJudgement → DATUM_FAIL/NO_IMAGE/NO_RESULT/OK/NG 5분기 — RepeatMeasurementStats.AddSample 정책과 100% 일치시켜 향후 로더/집계 일관성 확보"
  - "CSV injection 프리픽스(=/+/-/@) 이스케이프는 미적용 — 내부 기계 생성 데이터로 저위험 판단, 왕복 파싱 정확성 우선(threat_model T-67-02 accept)"

patterns-established:
  - "SaveAsync 단일 훅 패턴: v2.6/v1.0/수동 3개 저장 경로가 모두 CycleResultSerializer.SaveAsync 로 수렴하므로, 신규 부가 파이프라인은 이 한 지점만 훅하면 전 경로 자동 커버"

requirements-completed: [STAT-01]

# Metrics
duration: 15min
completed: 2026-07-07
---

# Phase 67 Plan 01: 양산 이력 통계 분석 — 수집 계층 Summary

**검사 완료(SaveAsync) 시 StatisticsSavePath\yyyyMMdd.csv 에 측정 항목당 1행을 RFC4180 이스케이프 + static lock 으로 append 하는 CSV 이력 수집 계층 (D-01~D-05 잠금결정 그대로 구현)**

## Performance

- **Duration:** 약 15분
- **Started:** 2026-07-07T01:09:00Z
- **Completed:** 2026-07-07T01:17:04Z
- **Tasks:** 4/4
- **Files modified:** 4 (1 신규 + 3 수정)

## Accomplishments
- `SystemSetting.StatisticsSavePath` 설정 프로퍼티 추가 — 설정창(PropertyGrid)에 노출, 기존 경로 회귀 0
- `MeasurementHistoryCsvWriter.Append(CycleResultDto)` 신규 정적 서비스 — Shot→FAI→Measurement 3중 루프 평탄화, 14컬럼 고정 스키마, RFC4180 이스케이프, static lock 으로 경합 방지
- `CycleResultSerializer.SaveAsync` 의 background Task 에 CSV append 를 **독립 try/catch** 로 훅 — JSON 저장 성공/실패와 무관, v2.6/v1.0/수동 3경로 모두 자동 커버
- msbuild Debug/x64 빌드 PASS (error 0, 기존 경고만 존재), 변경 파일 4개 전부 추가(diff `+`)만 존재 — 회귀 0

## Task Commits

Each task was committed atomically:

1. **Task 1: SystemSetting 에 StatisticsSavePath 추가 (D-01)** - `92a45b0` (feat)
2. **Task 2: MeasurementHistoryCsvWriter 신규 생성 (D-02/D-03/D-05)** - `8c26a0e` (feat)
3. **Task 3: CycleResultSerializer.SaveAsync 훅 삽입 (D-04)** - `a6c0044` (feat)
4. **Task 4: csproj 등록 + 빌드 검증** - `f16e390` (chore)

**Plan metadata:** (다음 커밋에서 STATE/ROADMAP 과 함께 기록됨)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs` - 신규. 측정항목당 1행 CSV append, 14컬럼 스키마, RFC4180 이스케이프, static lock
- `WPF_Example/Setting/SystemSetting.cs` - StatisticsSavePath 프로퍼티 추가 (Path|Statistics 그룹)
- `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` - SaveAsync 에 독립 try/catch CSV 훅 추가
- `WPF_Example/DatumMeasurement.csproj` - MeasurementHistoryCsvWriter.cs Compile Include 등록

## Decisions Made
- 플랜에 명시된 D-01~D-05 잠금 결정을 그대로 구현. 별도 아키텍처 판단 불필요 (Rule 4 트리거 없음).
- CSV injection 프리픽스 미적용은 계획된 threat_model accept 항목 그대로 유지.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- Bash 도구(Git Bash)에서 `/p:` MSBuild 스위치가 경로로 오인되어 첫 두 번의 빌드 시도가 실패(MSB1008/MSB1001). `MSYS2_ARG_CONV_EXCL="*"` 환경변수로 우회하여 세 번째 시도에서 정상 빌드 성공(회귀 없음, 빌드 스크립트 자체 문제였음).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 02(조회 계층)가 소비할 CSV 스키마(14컬럼, yyyyMMdd.csv, P/F/N 판정) 확정 및 실제 파일 생성 경로(StatisticsSavePath) 준비 완료.
- Plan 03(UI)에서 StatisticsSavePath 설정 노출을 그대로 재사용 가능.
- 실 데이터 누적 확인(육안 UAT)은 이 Plan 범위 밖 — Plan 02/03 진행 중 또는 phase 종료 시 검사 1회 실행으로 CSV 파일 생성 확인 권장.

---
*Phase: 67-stat-01-2026-07-07*
*Completed: 2026-07-07*

## Self-Check: PASSED

All created/modified files and all 4 task commit hashes (92a45b0, 8c26a0e, a6c0044, f16e390) verified present.
