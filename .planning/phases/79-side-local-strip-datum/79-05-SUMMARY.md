---
phase: 79-side-local-strip-datum
plan: 05
subsystem: vision-measurement
tags: [uat, human-verification, halcon, edge-measurement, side-camera]

requires:
  - phase: 79-01
    provides: "국부 기준선 전체 경로(피팅·저장소·주입·전환), ComputeLocalRefLine/InjectedLocalRef/EvaluateJudgement API, LocalRefProbe"
  - phase: 79-02
    provides: "cycle.json RefSource·CSV 사용기준 열, 결과 그리드·리뷰어 '기준' 열"
  - phase: 79-03
    provides: "캔버스 기준 ROI 배선, 주황 오버레이"
  - phase: 79-04
    provides: "버전 1.7.50.0, phase 79 누적 회귀 감사 PASS"
provides:
  - "realab 결과(운영 코드 경로로 계산한 09-17 자재 A·B 7사이클 C13·C14 6점 국부/전역 A−B, 추천 티칭 값) — 79-HUMAN-UAT.md 에 반영"
  - "79-HUMAN-UAT.md U-1~U-8 사용자 실측 결과 기록 — U-1~U-6 PASS, U-7 대기, U-8 미실행"
  - "사용자 승인(\"ㅇㅋ\") — C13_P3(stripR)·C14_P3(stripR2) 국부 기준 채택 결정, P1·P2 는 켜지 않음"
affects: [80-review-ng-cycle-reload]

tech-stack:
  added: []
  patterns:
    - "realab probe: 레시피 스냅샷만 Load, 사진·cycle.json 은 읽기 전용, 결과는 스크래치 폴더 — 운영 데이터 무변경으로 A−B 효과를 사전 계산"
    - "추천 규칙은 A−B 값을 쓰지 않고 찾음 7/7 + 같은 자재 안 흔들림 최소로만 창을 고른다(T-79-20) — 같은 데이터로 고르고 평가하는 치우침 방지. 단, 이번 결과에서 C13_P1 은 그 규칙이 형제 창보다 나쁜 창(stripL2)을 추천하는 사례가 실제로 나와, 추천은 참고일 뿐 최종 선택은 A−B 표를 함께 보고 사람이 정해야 함을 확인"

key-files:
  created: []
  modified:
    - .planning/phases/79-side-local-strip-datum/79-HUMAN-UAT.md

key-decisions:
  - "C13_P3·C14_P3 만 국부 기준(Local Ref) 을 켠다 — 각각 추천 창 stripR·stripR2 그대로 사용. C13_P1·C14_P1(전역에서도 이미 편차 작음)·C13_P2·C14_P2(찾음 7/7 이나 국부에서 A−B 가 오히려 악화, A-79-A2) 는 켜지 않는다"
  - "새 NominalValue/공차는 이번 UAT 에서 결정하지 않는다 — 도면·고객 확인 후 별도 결정(코드는 자동으로 바꾸지 않음, O-79-05)"
  - "C14_P3 실측은 사용자가 'C13_P3 와 동일할 것' 으로 판단해 생략 — realab 예측(47.3→2.7µm)만 참고값으로 남김"
  - "A-79-E1(z1 사진 미표시)·A-79-E3(수동 RUN 즉시 반영) 두 개선 요청은 이번 phase 범위가 아닌 Phase 80 후보로 이월(커밋 08623973 에 이미 기록됨)"
  - "이 UAT 는 계획의 'Debug|x64 전용' 전제와 다르게, 라이선스 키 없는 이 PC 대신 사용자가 직접 빌드한 Release 1.7.50.0(운영 D:\\Data 배포 exe)으로 수행되었다 — 결과 자체의 신뢰도(실제 배포 경로로 확인됨)는 오히려 높으나, 운영 레시피 적용은 U-5 결정과 별개로 사용자가 최종 승인해야 한다(P-15)"

requirements-completed: [LSR-01, LSR-02, LSR-03, LSR-04, LSR-05, LSR-06]

coverage:
  - id: D1
    description: "realab probe 가 09-17 자재 A(4)·B(3) 7사이클, 창 6 × 에지 조합 8 을 조사해 운영 코드 경로(ComputeLocalRefLine→InjectedLocalRef→TryExecute→EvaluateJudgement) 그대로 C13·C14 6점의 국부/전역 A−B 와 추천 값을 계산했다"
    requirement: "LSR-06"
    verification:
      - kind: integration
        ref: "$H/realab.txt: realab cycles=7 A=4 B=3 exceptions=0, realab_best 6줄(전부 found=7/7), realab_meas 12줄, realab_pick 6줄, recipe_untouched=1 repo_clean=0"
        status: pass
    human_judgment: false
  - id: D2
    description: "79-HUMAN-UAT.md 에 추천 티칭 표·A−B 비교 표·U-1~U-8 절차가 실제 로그 문구·JSON 키·CSV 열 이름으로 작성되어 커밋되었다"
    requirement: "LSR-01,LSR-02,LSR-03,LSR-04,LSR-05"
    verification:
      - kind: integration
        ref: "커밋 42ac43f3, plan Task 2 acceptance_criteria 전부 통과(exists=1, U-1~U-8/A-79-E1~E4/A-79-A2/A-79-A7 각 ≥1, table_meas≥18, log_found/log_fallback/csvcol/jsonkey/debugonly/version/recipe_policy≥1)"
        status: pass
    human_judgment: false
  - id: D3
    description: "U-1(옵션 UI·티칭), U-2(실제 SIDE 사이클 국부 검출·로그·화면·기록), U-4(표시 회귀) 가 실기로 확인되어 PASS 로 기록되었다"
    verification: []
    human_judgment: true
    rationale: "탭 구성 사용성(A-79-E1), 실제 사이클 로그·화면·JSON 문구 육안 확인, 옛/새 리뷰어 표시 회귀는 사람이 화면을 보고 판단해야 하는 항목 — 자동화 불가"
  - id: D4
    description: "U-3 전역 기준선 자동 전환 3경우(ROI 삭제·평평한 곳 이동·Test Find 재사용 뒤 변경)에서 검사가 멈추지 않고 값이 옵션 끈 값과 같으며 전환 로그·표시가 남는다"
    verification: []
    human_judgment: true
    rationale: "Error 로그 원인 문구의 충분성 판단(A-79-E3)과 실제 화면 표시 확인은 사람 판단 항목"
  - id: D5
    description: "U-5(켤 측정·창 결정)·U-6(A−B 효과 확인, 기준값·공차 결정) — C13_P3·C14_P3 만 켜고 stripR/stripR2 사용, C13_P3 실측 48.7→8.4µm 개선 확인, 기준값·공차는 도면 확인 후로 보류"
    verification: []
    human_judgment: true
    rationale: "얼마나 개선되어야 충분한지, 어떤 측정을 켤지, 기준값을 얼마로 할지는 코드가 정할 수 없는 사용자 결정(A-79-E4, O-79-05)"
  - id: D6
    description: "U-7(TOP·BOTTOM, 장비 PC)·U-8(동시성, 선택) — 대기/미실행으로 남고, phase 승인은 이 두 항목의 완료를 전제로 하지 않는다"
    verification: []
    human_judgment: true
    rationale: "장비 PC 접근·동시 작업 재현은 사무실 PC 자동화로 대체할 수 없는 backstop 항목"

duration: 약 20분 (Task 1·2 는 이전 세션에 완료, 이 세션은 체크포인트 결과 반영·SUMMARY 작성)
completed: 2026-09-18
status: complete
---

# Phase 79 Plan 05: 핀 옆 띠 기준 UAT 결과 Summary

**realab(09-17 자재 A·B 7사이클, 운영 코드 경로)로 C13_P3 국부 A−B 가 47.9→9.5µm(예측)·48.7→8.4µm(실측)로 뚜렷이 줄었음을 확인하고, 사용자가 C13_P3·C14_P3 만 국부 기준을 켜기로 승인했다.**

## Performance

- **Duration:** 약 20분 (이 세션 — Task 1 realab, Task 2 UAT 문서 작성은 앞선 세션에 완료·커밋됨)
- **Completed:** 2026-09-18
- **Tasks:** 3 (Task 1 realab probe, Task 2 79-HUMAN-UAT.md 작성, Task 3 체크포인트 — 사용자 UAT 수행·승인)
- **Files modified:** 1 (`79-HUMAN-UAT.md`, 결과 기록 갱신)

## Accomplishments

- **Task 1 (realab, 이전 세션 완료):** `LocalRefProbe realab` 모드가 09-17 자재 A(4사이클)·B(3사이클) 7사이클의 z1 사진에서 창 6개 × 에지 조합 8개를 조사하고, C13·C14 6측정 × 창 2개(12행)의 국부/전역 A−B 를 운영 코드 경로 그대로 계산했다. 결과 원문(`$H/realab.txt`):
  ```
  realab cycles=7 A=4 B=3 exceptions=0
  realab_best window=stripL  combo=BtoT/LightToDark/10 found=7/7 meanScore=90.8
  realab_best window=stripL2 combo=BtoT/LightToDark/10 found=7/7 meanScore=28.4
  realab_best window=stripM  combo=TtoB/DarkToLight/10 found=7/7 meanScore=25.0
  realab_best window=stripM2 combo=TtoB/DarkToLight/10 found=7/7 meanScore=25.4
  realab_best window=stripR  combo=BtoT/LightToDark/10 found=7/7 meanScore=26.7
  realab_best window=stripR2 combo=BtoT/LightToDark/10 found=7/7 meanScore=25.4

  realab_meas meas=C13_P3 window=stripR  localAB_um=9.5  globalAB_um=47.9
  realab_meas meas=C13_P3 window=stripR2 localAB_um=-6.8 globalAB_um=47.9
  realab_meas meas=C14_P3 window=stripR  localAB_um=19.0 globalAB_um=47.3
  realab_meas meas=C14_P3 window=stripR2 localAB_um=2.7  globalAB_um=47.3
  realab_meas meas=C13_P1 window=stripL  localAB_um=3.3  globalAB_um=3.3
  realab_meas meas=C13_P1 window=stripL2 localAB_um=39.4 globalAB_um=3.3
  realab_meas meas=C14_P1 window=stripL  localAB_um=-0.8 globalAB_um=-0.5
  realab_meas meas=C13_P2 window=stripM  localAB_um=13.0 globalAB_um=-10.7 (악화)
  realab_meas meas=C14_P2 window=stripM  localAB_um=18.7 globalAB_um=-5.1 (악화)

  realab_pick meas=C13_P1 window=stripL2 row=6590.0 col=1850.0 len1=110.0 len2=90.0
  realab_pick meas=C14_P1 window=stripL  row=6590.0 col=1490.0 len1=110.0 len2=90.0
  realab_pick meas=C13_P2 window=stripM  row=6550.0 col=6990.0 len1=170.0 len2=90.0
  realab_pick meas=C14_P2 window=stripM  row=6550.0 col=6990.0 len1=170.0 len2=90.0
  realab_pick meas=C13_P3 window=stripR  row=6510.0 col=12690.0 len1=110.0 len2=90.0
  realab_pick meas=C14_P3 window=stripR2 row=6510.0 col=13040.0 len1=110.0 len2=90.0
  ```
  `recipe_untouched=1`, `repo_clean=0`(WPF_Example 무변경) — 운영 레시피·저장소를 건드리지 않고 계산됨.
- **Task 2 (79-HUMAN-UAT.md, 이전 세션 완료, 커밋 `42ac43f3`):** 위 결과를 추천 티칭 표·A−B 비교 표로 옮기고 U-1~U-8 절차·A-79-E1~E4 판단 칸을 실제 로그 문구·JSON 키·CSV 열 이름으로 작성했다.
- **Task 3 (이 세션 — 체크포인트 결과 반영, 커밋 `4e8fa5bf`):**
  - **U-1 PASS:** Local Ref 탭 3그룹·숫자 입력·재선택 시 ROI 상자·끌기/크기 조정 확인. 레시피 저장 후 `C13_P3` 섹션에만 13개 키 신규 생성, 다른 측정 섹션 무변경 확인. **A-79-E1 신규 발견:** 티칭 중 캔버스가 z3~z9 측정 사진을 보여주고 z1 기준점 사진(에지가 실제 피팅되는 사진)을 보여주지 않아 숫자만으로 맞춰야 함 — Phase 80 후보로 이월.
  - **U-2 PASS:** 수동 RUN(오프라인 사진, PLC TCP 아님) 3회 재현. Algorithm 로그 `[LocalRef] 기준선 찾음 — SIDE_1 · Side_Datum_1 · C13_P3: 중점 row=6476.79 col=12696.47 에지세기=65.7`(3회 row 6476.4~6477.1, 에지세기 65.7~69.1). cycle.json `RefSource=Local`, `C14_P3` 빈칸. 자재별 확인: A 기준점 origin/angle (6585.8, -0.380°) vs B (6608.2, -0.507°), 국부 띠 row A 6476.8 vs B 6490.0 — 자재마다 제 z1 사진·제 값 사용 확인(회귀 없음).
  - **U-3 PASS:** (a) `LocalRef_Length1=0`, (b) `LocalRef_Row=6000`(평평한 위치) 둘 다 값이 옵션 끈 값과 정확히 같음(2.2078), "기준"=`국부실패→전역`, 사이클 정상 완료. 두 경우 모두 Error 로그 원인이 `기준점을 찾은 뒤 기준 ROI 또는 에지 설정이 바뀜 — 기준점을 다시 찾으면 반영됨` 으로 찍힘(수동 RUN 이 이전 Test Find 기준점을 재사용하는 구조라 '설정 변경' 검사가 먼저 걸린 것 — 안전한 동작). **A-79-E3 요청:** 기준 ROI 변경을 Test Find 없이 수동 RUN 이 즉시 반영해 달라는 요청 — Phase 80 후보로 이월.
  - **U-4 PASS:** 옵션 꺼짐 → "기준" 칸 빈칸, 값이 옵션 추가 전과 동일.
  - **U-5 결정:** `C13_P3`(stripR)·`C14_P3`(stripR2) 만 켬. `C13_P1`/`C14_P1`(전역도 이미 작음)·`C13_P2`/`C14_P2`(찾음 7/7 이나 국부 A−B 악화) 는 켜지 않음.
  - **U-6 PASS(C13_P3):** 같은 z3/z1 사진 쌍(A1/B1) 실측 — 전역(옵션 끔) 48.7µm(A=2.2565, B=2.2078) → 국부(옵션 켬) 8.4µm(A=2.1811, B=2.1727), realab 예측(47.9→9.5)과 일치. C14_P3 는 "동일하겠지" 로 사용자가 판단해 실측 생략(realab 예측 47.3→2.7 만 참고). 새 기준값/공차는 도면·고객 확인 후로 보류.
  - **U-7:** 대기(장비 PC), **U-8:** 미실행(선택 항목).
  - **사용자 최종 승인:** "ㅇㅋ" — Phase 79 승인.

## Task Commits

Each task was committed atomically:

1. **Task 1: realab probe** — 저장소 파일 변경 없음(probe 는 스크래치 폴더, 커밋 대상 아님)
2. **Task 2: 79-HUMAN-UAT.md 작성** - `42ac43f3` (docs)
3. **Task 3: 체크포인트 결과 반영** - `4e8fa5bf` (docs)

**Plan metadata:** 이 SUMMARY 커밋(아래)이 별도 기록

## Files Created/Modified

- `.planning/phases/79-side-local-strip-datum/79-HUMAN-UAT.md` - U-1~U-8 결과·A-79-E1~E4 판단·U-5 결정 표·U-6 실측 표·최종 결론 절 추가

## Decisions Made

- C13_P3(stripR)·C14_P3(stripR2) 만 국부 기준을 켠다 — C13_P1/C14_P1/C13_P2/C14_P2 는 켜지 않는다(U-5)
- 새 NominalValue/공차는 이번 phase 에서 정하지 않고 도면·고객 확인 후 사용자가 별도 결정한다(U-6, O-79-05)
- C14_P3 실측은 이번 UAT 에서 생략(사용자 판단) — realab 예측값만 참고로 남긴다
- A-79-E1(z1 사진 미표시)·A-79-E3(수동 RUN 즉시 반영) 는 Phase 80 후보로 이월한다(이미 커밋 `08623973` 에 기록됨 — 이 SUMMARY 는 그 결정을 재확인만 함, 새로 추가하지 않음)

## Deviations from Plan

### 계획과 다르게 진행된 사항 (기록만, 코드 변경 아님)

**1. Release 빌드로 UAT 수행**
- **발견 시점:** Task 3 체크포인트 (사용자 응답)
- **내용:** 계획(79-HUMAN-UAT.md 공통 준비 1번)은 "Debug|x64 전용" 을 명시했으나(Release 는 `OutputPath` 가 `D:\Data\` 라 배포 exe 를 덮어쓰기 때문), 사용자 PC 에 라이선스 키가 없어 Debug 빌드가 실행되지 않았다. 사용자가 직접 15:48 에 Release 1.7.50.0 을 빌드해(운영 배포 exe 갱신) UAT 를 수행했다.
- **조치:** 코드나 레시피를 실행자가 고치지 않았으므로 Rule 1~3 대상이 아니다 — 사용자의 판단이자 행동이며, 실행자는 이를 UAT 문서·SUMMARY 에 사실대로 기록하는 것으로 마무리했다(위 header 문구, "알려진 한계" 14번).
- **영향:** UAT 결과 자체는 실제 운영 배포 경로로 검증되어 신뢰도가 낮아지지 않으나, 이 시점부터 운영 D:\Data 의 배포 exe 는 이미 1.7.50.0 이다. 운영 레시피(main.ini)에 U-5 결정(C13_P3/C14_P3 Local Ref 값)을 실제로 반영할지는 이 SUMMARY 범위 밖 — 사용자 최종 승인이 필요하다(P-15).

**총 편차:** 1건(계획 전제와 다른 빌드 종류로 UAT 수행, 코드/레시피 변경 없음). Rule 1~4 대상 아님(사용자 행동 기록).

## Issues Encountered

없음. U-1~U-6 모두 사용자 실측으로 확인되었고, U-7·U-8 은 애초에 backstop/선택 항목으로 설계되어 대기·미실행이 승인 조건이 아니다.

## User Setup Required

None - 외부 서비스 설정 불필요. 단, 후속으로 사용자가 해야 할 운영 준비 작업(이 phase 범위 밖):
- C14_P3 실측 확인
- C13_P3·C14_P3 새 NominalValue/공차 결정(도면·고객 확인 후)
- U-7(TOP·BOTTOM, 장비 PC)
- 운영 레시피(main.ini) 적용 여부 최종 결정 및 적용

## Next Phase Readiness

- Phase 79(핀 옆 띠 국부 기준) 전체가 사용자 승인으로 종료됨. C13_P3/C14_P3 국부 기준 채택, P1/P2 미채택.
- A-79-E1(z1 사진 미표시 개선)·A-79-E3(수동 RUN 즉시 반영)는 Phase 80 후보로 이미 이월됨(커밋 `08623973`) — 이번 SUMMARY 는 결정을 재확인만 하고 새로 추가하지 않는다.
- 배포(운영 레시피 적용)는 이 plan 범위 밖 — 사용자 별도 진행.

## Self-Check: PASSED

- `.planning/phases/79-side-local-strip-datum/79-HUMAN-UAT.md` 존재 확인(FOUND)
- 커밋 `42ac43f3`, `4e8fa5bf` 모두 `git log --oneline --all` 에서 확인됨(FOUND)

---
*Phase: 79-side-local-strip-datum*
*Completed: 2026-09-18*
