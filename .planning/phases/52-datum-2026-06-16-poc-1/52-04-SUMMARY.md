---
phase: 52-datum-2026-06-16-poc-1
plan: 04
type: execute
status: partial
requirements: [LEVEL-01]
created: 2026-06-17
key_files:
  created:
    - .planning/phases/52-datum-2026-06-16-poc-1/52-UAT.md
  modified:
    - .planning/ROADMAP.md
    - .planning/REQUIREMENTS.md
carry_over:
  - CO-52-01
---

# Plan 52-04 Summary — SIMUL UAT + Sign-off (PARTIAL)

## 결과: PARTIAL (NOT signed_off)

- **Task 1 (auto):** 통합 빌드 검증 + 52-UAT.md 5 Test 골격 작성 — ✅ 완료 (commit `6b84946`)
  - msbuild Debug/x64 Rebuild **PASS**, 에러 0, 경고 6 (전부 pre-existing baseline), Phase 52 파일 신규 경고 0.
- **Checkpoint (human-verify):** 사용자 SIMUL 육안 검증 — ❌ Test 2(핵심) FAIL.
- **Task 2 (sign-off):** 부분 완료 — UAT 결과 기록 + ROADMAP/REQUIREMENTS PARTIAL 갱신 + carry-over 등재. **SIGNED_OFF 미수행** (UAT 미충족).

## UAT 결과 (2026-06-17)

| Test | 항목 | 결과 |
|------|------|------|
| 1 | off 회귀 | NOT_TESTED (사용자 미보고; 기본 off 경로) |
| 2 | 레벨링 동작 + 회전 이미지 Datum 검출 (D-02 핵심) | **FAIL** |
| 3 | 회전 방향 부호 | BLOCKED |
| 4 | 무회전 폴백 | BLOCKED |
| 5 | INI 영속 | BLOCKED |

## FAIL 근본 원인 — UI 부재 (코드 결함 아님)

사용자 보고: "이미지가 돌아가는 것처럼 안 보여 동작 여부를 알 수 없고, 각도/레벨링을 설정할 데가 없다."

조사 결과 (grep 확인):
- `LevelingEnabled` — 백엔드 3개 파일(Action_FAIMeasurement / InspectionRecipeManager / InspectionSequence)에만 존재, **XAML/UI 바인딩 0건**.
- `IsLevelingReference` — UI(xaml/xaml.cs) 바인딩 **0건**.

→ 사용자가 레벨링을 **켜거나 기준 Datum 을 지정할 UI 가 없어** 기능 진입 자체가 불가. 결과 화면에 회전 적용 시각화도 없음. 백엔드(데이터모델·각도산출·회전·측정경로)는 빌드 검증·코드리뷰 클린이나 사용자 관점 검증 불가.

## 코드 리뷰 (Phase 52, standard depth)

- 0 critical, 2 warning, 3 info. 두 핵심 불변식(부호/회전중심 일관성, INI 하위호환) 모두 정상.
- **WR-01** (회전 swap Dispose 비가드) + **WR-02** (SIMUL 각도 소스 불일치) → 둘 다 수정 적용 (commit `8e89f89`), 빌드 재검증 PASS.
- IN-01/02/03 (info) 미수정 (advisory).

## Carry-over

- **CO-52-01 — 레벨링 활성화/기준지정 UI + 결과 회전 시각화 부재** → 신규 Phase 52.1 후보:
  1. FIXTURE 설정에 `LevelingEnabled` 토글 UI
  2. Datum 노드/PropertyGrid 에 `IsLevelingReference` 선택 UI
  3. 결과 화면 회전 전/후 시각화(또는 적용 각도 표시)
  - 이후 Test 2~5 재검증 (부호/방향, 무회전 폴백, INI 영속 + CameraRole 회귀 가드 포함).

## Commits

- `6b84946` docs(52-04): 통합 빌드 검증 + 52-UAT.md 5 Test 골격
- `46ebe49` docs(52): code review report
- `8e89f89` fix(52): WR-01 Dispose 가드 + WR-02 SIMUL 진단로그
- (this) docs(52-04): UAT 결과 기록 (PARTIAL) + ROADMAP/REQUIREMENTS + carry-over CO-52-01

## Self-Check: PASSED (with documented carry-over)
