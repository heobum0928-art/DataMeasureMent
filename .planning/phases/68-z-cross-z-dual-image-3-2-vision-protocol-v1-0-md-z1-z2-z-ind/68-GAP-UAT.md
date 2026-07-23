---
status: approved
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
source: [68-11-PLAN.md Task 2, 68-GAP-ANALYSIS.md, 68-VALIDATION.md]
started: 2026-07-23
updated: 2026-07-23
---

# Phase 68 Plan 11 Task 2 — Gap-Closure 통합 SIMUL UAT 결과

68-06~68-10(FIX-0/GAP-1/GAP-2/CROSS-1/CROSS-2/GAP-3) 통합 빌드로 SIMUL_MODE 사람 검증 수행.
68-05-PLAN.md/68-HUMAN-UAT.md는 미수정(지침 #10) — 결과는 이 신규 파일에만 기록.

**테스트 레시피**: `D:\Data\Recipe\FAI_1\main.ini`, `SHOT_E5`(BOTTOM) `FAI_E5`의 `E5_P2`
(`DualImageEdgeDistance`)에 `ZIndexA=1`/`ZIndexB=2` 부여(Config A: `SHOT_E5.ZIndex`=0=제3값,
68-HUMAN-UAT.md §4 그대로). 트리거 도구: `DebugManualZTrigger`(MainView "수동 Z 트리거" 패널).
테스트 종료 후 `main.ini`를 `main.ini.bak_gapuat`로부터 원복 완료(byte-diff 없음 확인).

---

## Tests

### 1. FIX-0/CROSS-1 — 완성 index 보정 반영
expected: z=1 트리거 시 `E5_P2` 미보고(role A 캡처만) → z=2 트리거 시 `E5_P2` 보고(완성 index), mm값이
identity 무보정이 아니라 z=1 검출 기반으로 산출됨.
관측: z=1 트리거 후 `E5_P2`/`E5_P1` 측정값·판정 모두 `—`(미보고). z=2 트리거 후 `E5_P2`=30.561mm(OK,
Nominal 30.578±0.05), 저장된 `cycle.json` 스냅샷으로도 z=1 스냅샷은 `LastHasResult=false`, z=2
스냅샷은 `LastHasResult=true`/`LastMeasuredValue=30.561...` 로 교차 확인.
result: **PASS**

### 2. GAP-1/GAP-2(무관 Shot 재-grab 0)
expected: z=1/z=2 트리거 시 `SHOT_E5` 외 BOTTOM의 다른 Shot(전부 own ZIndex=0)이 재-grab 되지 않음.
관측: 트리거 시각 구간의 Trace 로그(FitLine/Datum/ALIGN 전부 SHOT_E5·Bottom_Datum 관련)와 해당 구간에
새로 저장된 결과 이미지 파일명(전부 `_E5_` 포함, 다른 Shot 이름 0건) 두 경로로 교차 확인 — 무관 Shot 재-grab
0건.
result: **PASS**

### 3. GAP-2 Error 로그 억제 — ★신규 버그 발견 및 수정
expected: 크로스-Z 관련 비완성 index 트리거 시 "매칭 0건" 계열 스퓨리어스 Error 로그가 찍히지 않음(기존
GAP-2(f)가 크로스-Z **Datum**-only index만 억제).
관측(수정 전): z=1 트리거마다 `D:\Data\Error\2026-07-23_Error.log`에
`ERROR [WarnIfEmptyScope...] : [V1Cycle] BuildScopedResponse 빈 결과: ZIndex 매칭 0건 (Seq=BOTTOM, z=1,
last=2). 레시피 ZIndex 설정 확인 필요.` 가 매번 찍힘 — 원인: `WarnIfEmptyScope`의 억제 조건
(`IsDatumOnlyExecutionIndex`)이 크로스-Z **측정**(Measurement)의 비완성 capture role(예: `SHOT_E5`
own ZIndex=0, `ZIndexA=1`)은 커버하지 않음(Datum 케이스만 억제).
**수정**: `InspectionSequence.cs`에 `BuildCrossZMeasurementIndexSet`/`IsZIndexUsedByCrossZMeasurement`
신설(기존 `BuildCrossZDatumIndexSet` 대칭 구조, `AddFaiDeclaredZIndices` sub-헬퍼 재사용) —
`WarnIfEmptyScope`의 억제 조건을 `bDatumOnlyIndex || bCrossZMeasurementCaptureIndex`로 확장.
재빌드 후 z=1 재트리거로 실측 재확인 — Error 로그 0건 확인(라이트 경고 로그만 존재).
result: **FAIL → FIX 적용 후 PASS**(재검증 완료)

### 4. CROSS-2(한 사이클 P/F 정확히 1회)
expected: z=1(중간, 비완성)은 B(버퍼) 취급, z=2(완성)만 종합 P/F.
관측: `ComputeLastZIndex`가 `MaxCrossZCompletionZIndex`를 포함해 이 레시피에서 2를 반환함을 코드로 확인
(own ZIndex 최대값 0 vs 크로스-Z 완성 index 2 중 큰 값) → z=1: `bIsLastIndex=(1>=2)=false`→B,
z=2: `bIsLastIndex=(2>=2)=true`→P/F. `cycle.json` 스냅샷(`LastHasResult` 전이)과도 일치.
(참고: 트리거 시 찍힌 "Sequence BOTTOM Final Result: Pass" 로그는 V1 프로토콜 P/F/B와 무관한 별개의
시퀀스 액션-완료 로그로 확인 — 오탐.)
result: **PASS**

### 5. GAP-3(EnableCrossZDatumImmediateFail 기본 동작)
expected(68-11-PLAN.md 원문): 기본 OFF 확인.
관측: 68-10에서 사용자 체크포인트 결정("enable-after-agreement")으로 기본값이 이미 `true`로 flip됨
(커밋 `a110ef4`) — 68-11 계획서의 "기본 OFF" 문구는 그 결정 이전에 쓰인 stale 텍스트. 코드 기본값은
의도대로 `true`. 단, 로컬 `Setting.ini`(앱 실행 디렉터리)는 68-10 이전에 생성된 파일이라 여전히
`EnableCrossZDatumImmediateFail=False`로 영속화돼 있음 — 배포 시 기존 `Setting.ini`가 있는 PC는 새
기본값이 자동 반영 안 될 수 있음(배포 노트로 기록, 이번 UAT 범위 밖).
추가로, 이 플래그는 크로스-Z **Datum**(기준점) 전용이라 이번 테스트 레시피(`SHOT_E5`, 크로스-Z **측정**)로는
실동작을 재현할 수 없음 — Side 크로스-Z Datum 레시피 부재.
result: **BLOCKED**(이 환경에서 기능 검증 불가) — 코드 기본값 확인만 완료. Side 크로스-Z Datum 레시피 준비
후 별도 검증 필요.

### 6. 혼합 Shot 오염 (지침 #9) — ★신규 방지 기능 추가
expected: 크로스-Z owning Shot에 일반(비-크로스-Z) 측정이 섞이면 재현 확인 + 방지책 마련.
관측: `E5_P2`=크로스-Z(1,2), `E5_P1`=일반(-1,-1)로 재구성 후 z=1 트리거 → `E5_P1`(원래 own
ZIndex=0에서만 실행돼야 함)이 z=1에서도 보고됨(`cycle.json`: `LastHasResult=true`,
`LastMeasuredValue=30.566`) — 혼합 오염 실측 재현.
**사용자 결정**: 코드로 실행 로직을 바꾸는 대신(리스크 큼), **저장 시점 차단**으로 대응. 규칙은
"같은 Shot 안의 모든 측정은 (ZIndexA,ZIndexB) 짝이 전부 동일해야 함"으로 일반화(사용자 지적: 1,2와
3,2처럼 서로 다른 크로스-Z 짝 혼합도 금지 대상).
**수정**: `InspectionRecipeManager.cs`에 `FindMixedCrossZShots`/`ShotHasInconsistentCrossZPairs` 신설
(Shot 내 측정들의 (ZIndexA,ZIndexB) distinct pair 개수 > 1 이면 위반). `MainWindow.SaveRecipe`가 저장
직전 이를 호출해 위반 시 `CustomMessageBox`로 저장 자체를 차단(파일 미변경).
재빌드 후 실측 재확인: 위반 상태에서 저장 클릭 → `main.ini` mtime 불변(10:11:06, 이후 클릭 시각까지
불변) 확인 — 저장 차단 동작 확인.
result: **재현 확인 + 저장차단 기능 추가 후 PASS**

---

## Summary

total: 6
passed: 4 (FIX-0/CROSS-1, GAP-1/GAP-2 무관Shot, CROSS-2, 혼합Shot오염-차단기능)
fixed-during-uat: 2 (GAP-2 Error로그 억제 확장, 혼합Shot 저장차단 신설)
blocked: 1 (GAP-3 — 이 환경에 Side 크로스-Z Datum 레시피 없어 기능 검증 불가, 코드 기본값만 확인)
skipped: 0

## Code Changes Made During This UAT (out of Task 1's original no-source-change scope — user-directed)

1. `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`:
   - `BuildCrossZMeasurementIndexSet()`, `IsZIndexUsedByCrossZMeasurement()` 신설.
   - `WarnIfEmptyScope()` 억제 조건을 크로스-Z 측정 capture-only index까지 확장.
2. `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs`:
   - `FindMixedCrossZShots()`, `ShotHasInconsistentCrossZPairs()` 신설.
3. `WPF_Example/MainWindow.xaml.cs`:
   - `SaveRecipe()`에 저장 전 혼합 크로스-Z Shot 검사 + `CustomMessageBox` 차단 추가.

빌드: Debug/x64 msbuild, 0 errors, 신규 경고 0(기존 CS0618/CS0162만).

## Gaps / Carry-over

- GAP-3 기능 검증(Datum 즉시-F 실동작)은 Side 크로스-Z Datum 레시피가 준비되면 별도 UAT 필요.
- `Setting.ini` 스테일 이슈(EnableCrossZDatumImmediateFail 기존 로컬 설정파일이 새 기본값 미반영) —
  현장 배포 시 기존 `Setting.ini` 존재 PC 대상 마이그레이션/재확인 필요할 수 있음(별도 판단 필요, 이번
  범위 밖).
