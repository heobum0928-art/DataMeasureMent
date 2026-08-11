---
phase: quick-260810-olh
plan: 01
subsystem: api
tags: [tcp-protocol, align-calib, plc-integration, vision-server]

# Dependency graph
requires: []
provides:
  - "$ALIGN_CALIB TCP 응답이 START/STEP/END/ABORT 모든 명령에서 항상 N(스텝번호) 필드를 포함"
  - "실패(NG) 시 N=97 을 BuildAlignCalibMessage 단일 지점에서 중앙 결정하는 패턴"
affects: [tcp-server, plc-integration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "응답 직렬화 시 성공/실패에 따른 필드값 분기는 Build*Message 메서드 한 곳에서 packet.IsPass 기준으로 중앙 결정 (여러 실패 반환 지점에서 개별로 챙기지 않도록)"

key-files:
  created: []
  modified:
    - WPF_Example/TcpServer/VisionResponsePacket.cs
    - WPF_Example/Custom/SystemHandler.cs

key-decisions:
  - "NG=97 규칙을 ProcessAlignCalib 의 실패 반환 지점(5곳)마다 개별 세팅하지 않고 BuildAlignCalibMessage 한 곳에서 packet.IsPass 기준으로 중앙 결정 — 향후 실패 경로 추가/변경 시에도 자동 적용되어 누락 불가능 (사용자 확정 설계)"

requirements-completed: [ALIGN-CALIB-STEPNO-01]

# Metrics
duration: 15min
completed: 2026-08-10
---

# Quick 260810-olh: ALIGN_CALIB 응답 N(스텝번호) 필드 전 명령 통일 Summary

**`$ALIGN_CALIB` TCP 응답의 N(스텝번호) 필드를 STEP 전용에서 START/STEP/END/ABORT 전체로 확장, 실패(NG) 시 명령 종류 무관 항상 97을 출력하도록 BuildAlignCalibMessage 한 곳에서 중앙 결정**

## Performance

- **Duration:** 약 15분
- **Completed:** 2026-08-10
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- `VisionResponsePacket.cs`: `ALIGN_CALIB_NG_STEP_NO = 97` 상수 추가, `BuildAlignCalibMessage` 를 명령 종류(CmdStr) 분기 없이 항상 N 필드를 출력하도록 재작성(성공=`packet.StepNo`, 실패=상수 97)
- `Custom/SystemHandler.cs`: `ProcessAlignCalib` 의 START/END/ABORT 성공 분기에 각각 `resultPacket.StepNo = 0` / `= 99` / `= 98` 명시적 세팅 추가
- STEP 분기(기존 `StepNo = PickerCal.StepCount`)와 알 수 없는 CmdStr 폴백 경로는 계획대로 무수정
- Debug/x64 빌드를 스크래치 OutDir(`C:\gsd-olh-scratch\`)로 실행해 실행 중인 `DatumMeasurement.exe` 잠금 충돌 없이 빌드 성공 확인 (신규 `error CS` 0건)

## Task Commits

1. **Task 1: ALIGN_CALIB 응답에 N(현재 스텝 번호) 필드 항상 포함 + 실패 시 97 통일** - `4f1ddd7` (feat)

_단일 태스크 플랜 — 플랜 메타데이터 커밋은 별도로 아래 최종 커밋에서 처리._

## Files Created/Modified
- `WPF_Example/TcpServer/VisionResponsePacket.cs` - `ALIGN_CALIB_NG_STEP_NO` 상수 추가, `BuildAlignCalibMessage` 를 명령 종류 무관 항상 N 필드 출력으로 재작성 (실패 시 97 은 여기 한 곳에서 결정)
- `WPF_Example/Custom/SystemHandler.cs` - `ProcessAlignCalib` START/END/ABORT 성공 분기에 `resultPacket.StepNo = 0/99/98` 한 줄씩 추가

## Decisions Made
- NG=97 규칙을 `ProcessAlignCalib` 의 실패 반환 지점 5곳(START/STEP/END/ABORT 각 실패 + 알 수 없는 CmdStr 폴백)에서 개별로 세팅하지 않고, `BuildAlignCalibMessage` 한 곳에서 `packet.IsPass` 를 보고 중앙 결정하도록 설계 — 실패 경로가 앞으로 추가/변경돼도 자동으로 97 이 나가 빠뜨릴 위험이 구조적으로 없음 (제어팀/PLC 요청, 사용자 최종 승인)

## Deviations from Plan

None - 플랜에 명시된 정확한 교체 코드를 그대로 적용. `<interfaces>` 섹션 1~5의 현재 코드 상태가 실제 파일과 완전히 일치함을 확인 후 진행.

**참고 (deviation 아님, 검증 스크립트 자체의 사소한 불일치):** 플랜의 verify 게이트2 주석은 "`ALIGN_CALIB_NG_STEP_NO` grep 카운트가 총 2건(선언 1 + 사용 1)이어야 한다"고 적혀 있었으나, 실제로는 3건이 나옴. 원인은 플랜이 명시한 `BuildAlignCalibMessage` 교체 코드의 설명 주석 자체에 "`ALIGN_CALIB_NG_STEP_NO(97)`" 이라는 문구가 포함되어 있기 때문(주석 1 + 선언 1 + 실사용 1 = 3). 플랜의 정확한 교체 코드를 한 글자도 바꾸지 않고 그대로 적용한 결과이므로 수정하지 않았고, `<done>` 기준에도 "총 2건"은 포함되어 있지 않아 완료 조건에는 영향 없음.

## Issues Encountered
- 초기 빌드 시도에서 스크래치 OutDir 경로를 백슬래시(`C:\gsd-olh-scratch\bin\`)로 전달했더니 Git Bash 의 경로 이스케이프 처리로 MSBuild 가 `error MSB4184`(경로 결합 실패)를 냄. 슬래시(`C:/gsd-olh-scratch/bin/`)로 바꿔서 재시도해 해결. 실제 코드 변경과 무관한 빌드 명령 환경 이슈.

## User Setup Required
None - 외부 서비스 설정 불필요.

## Next Phase Readiness
- 정적 검증(상수/메서드 교체/StepNo 세팅/파일 범위/빌드) 전부 통과.
- 실기 검증(PLC 통신 환경에서 START/STEP/END/ABORT 각 성공 응답 및 임의 실패 유도 시 NG 응답 형태 확인)은 이 플랜 범위 밖 — 사용자가 추후 PLC 연결 후 직접 확인 예정.

---
*Phase: quick-260810-olh*
*Completed: 2026-08-10*

## Self-Check: PASSED
- FOUND: WPF_Example/TcpServer/VisionResponsePacket.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: .planning/quick/260810-olh-align-calib-stepno-all-commands/260810-olh-SUMMARY.md
- FOUND commit: 4f1ddd7
