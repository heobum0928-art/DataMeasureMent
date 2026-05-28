# Plan 36-04 SUMMARY — UAT + Phase 36 PARTIAL sign-off

**Status:** PARTIAL signed_off (2026-05-28)

## What was done

- Task 1 (auto regression + UAT scaffold): 완료
  - Guard 4파일 변경 0 (ParamBase/InspectionListView/InspectionSequence/VisionResponsePacket)
  - msbuild Debug/x64 Rebuild PASS, 신규 warning 0 (total 14 baseline)
  - 36-UAT.md 7-Test 시나리오 작성 (commit 9fca966)
- Task 2 (사용자 실측 UAT): PARTIAL
  - UAT 도중 시각화 결함 root cause 발견·수정 (아래 hotfix 참조)
  - Test 2~7 사용자 시각 확인 일부 잔여 → CO-36-05

## UAT hotfixes (전부 commit, guard 4파일 변경 0 유지)

| CO | 내용 | commit | 비고 |
|----|------|--------|------|
| CO-36-01 | 수직도 PERPENDICULAR_TOLERANCE_DEG 5°→10° 임시 완화 | 14d9bf1 | 실측 fixture 83.5° 가 5° 한계에 막힘. 사용자 필드화는 open |
| CO-36-02/03 | OFF-SCREEN/markScale/이미지크기 기반 시각화 시도 | 7e0edc3, ee9395d, 6d23039 → 제거 36a4d28 | 오진. teach 오버레이가 고정크기로 정상 표시되므로 동일 방식이 옳았음 |
| CO-36-04 | **ROOT CAUSE**: `"purple"` 무효 HALCON 색상 → SetColor 예외 → catch swallow → 십자 전체 미표시 | fec1e02 | `"slate blue"` 로 해소. 사용자 발견 |
| (구조) | RenderDatumFindResult 를 LastTeachSucceeded 블록 밖으로 (검출 십자가 teach 상태에 묶여있던 결함) | df71e5c | 검출 십자는 자체 LastFindSucceeded 게이트만 따름 |

## Carry-over (open)

- **CO-36-01** — PERPENDICULAR_TOLERANCE_DEG 하드코딩(10°) → DatumConfig 사용자 필드화
- **CO-36-05** — Test 2/3/4/6/7 사용자 시각 UAT 미수행 (slate blue 빌드 이후 확인)
- **CO-36-06** — Side 4-datum × DualImage(8장) + 측정 별도 이미지 구조 미지원 → **신규 phase 필요**
- **CO-36-07** — TryRunDatumPhase 다중 datum 전부-성공 강제 + DualImage 판단 DatumConfigs[0] 한정 + 단일 이미지로 DualImage 검사 → CO-36-06 phase 에서 흡수

## Verification

- 가드 4파일 변경 0: PASS
- msbuild Debug/x64: PASS (exit 0, warnings 14, 신규 0)
- Plan 36-01/02/03 코드 머지 완료, 빌드 통합 PASS
- 시각화 root cause 해소 (slate blue)
