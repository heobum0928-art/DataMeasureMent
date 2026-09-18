---
phase: 79-side-local-strip-datum
plan: 01
subsystem: vision-measurement
tags: [halcon, edge-measurement, fai, datum, local-reference-line, ini-recipe, csharp7.2]

requires: []
provides:
  - "옵션 켠 EdgeToLineDistance 1개의 국부 기준선 전체 경로: 기준점 검출 성공 지점(TryRunSingleDatum / 5인자 TryComposeAlign)에서 가로 사진 피팅 → 사이클 저장소(_localRefLines) → 측정 직전 주입(InjectLocalRef) → TryExecute 가 기준선 원점으로 사용 → 거리(mm) + LastRefSource"
  - "국부 기준을 못 쓰는 5가지 원인(DatumRef 없음/기준점 원점 미주입/이번 사이클 미계산/기준 ROI 미티칭 또는 에지 못 찾음/기준점 검출 뒤 설정 바뀜)을 값 불변 + 원인 로그 1줄로 전환하는 TryResolveLocalRef"
  - "옛 레시피(Local Ref 키 없음) 하위호환 Load override — 에지 설정 7개만 선언 기본값 복원"
  - "Local Ref 속성 13개(옵션 1 + ROI 5 + 에지 7) 와 LocalRefLineResult/BuildLocalRefSettingsKey"
  - "저장소 밖 probe 2종(OffRegressProbe, LocalRefProbe) — 편집 전/후 exe 비트 비교, 합성 11사례, ini 3사례, 실사진 z1 stripL/stripR 검출 가능성"
affects: [79-02-record-display, 79-03-roi-wiring-overlay-color, 79-04-version-audit, 79-05-realab-uat]

tech-stack:
  added: []
  patterns:
    - "기준점 검출 성공 지점(return true 직전) 1줄 훅으로 부가 계산을 끼워 넣는 패턴 — 검출 결과·반환값을 건드리지 않고 훅 안 예외는 삼켜 Error 로그만 남긴다"
    - "사이클 저장소는 사전(Dictionary) 연산만 락 안에서 하고 HALCON 피팅은 락 밖에서 수행 — 결과 객체는 만든 뒤 고치지 않는다(immutable record)"
    - "SettingsKey(문자열 스냅샷)로 '주입 시점 설정이 계산 시점과 같은가'를 판정해 stale 값 사용을 막는 패턴"
    - "옛 레시피 하위호환: 기본값이 0/false 가 아닌 키만 Load override 에서 개별 복원, 0/false 가 곧 '꺼짐'인 옵션·좌표는 복원 불필요"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "P-1: 국부 기준선 계산 훅은 InspectionSequence 기준점 검출 성공 지점 2곳(TryRunSingleDatum, 5인자 TryComposeAlign)의 return true 직전 1줄 — RESEARCH 권고(Action_FAIMeasurement RunDatum*Detection)는 캐시 재사용 Shot(:315)을 놓쳐 채택하지 않음"
  - "P-2: 대상 측정은 이 시퀀스가 소유한 Shot 전체 중 EdgeToLineDistance + DatumRef 일치 + IsLocalRefEnabled — 캐시 재사용 Shot 포함"
  - "P-3: 결과 저장은 InspectionSequence._localRefLines(참조 동등 키), _datumStateLock 안 사전 연산만, ClearDatumTransforms 에서 함께 Clear, 재검출 시 지우고 새로 씀. 결과 객체는 불변"
  - "P-4: Action_FAIMeasurement.InjectDatumOrigin 바로 다음 InjectLocalRef — 측정당 tick 1번, Z 범위 처리 전"
  - "P-5 (O-79-02 b): 위치만 국부(기준선 원점 = 국부 피팅선 중점), 각도는 전역 유지 — 회귀 면을 줄이려 12줄 치환만"
  - "P-6: TryExecute 입구(LastFitScore=0.0 다음)에서 bUseLocalRef·LastRefSource 확정 — 실패 return 보다 먼저라 Z 후보마다 같은 값"
  - "P-7 (D-79-06): 전환 5조건(NO_DATUM/NO_DATUM_ORIGIN/NOT_COMPUTED/미티칭·에지못찾음/STALE)을 TryResolveLocalRef 가 순서대로 가드 절로 판정, 값은 옵션 꺼짐과 비트 동일"
  - "P-8: 국부 전용 에지 기본값 = 측정 기본값과 같되 Selection 만 Strongest(창 안 이중 에지 잡음 방지)"
  - "P-9: EdgeToLineDistanceMeasurement.Load override — 기본값이 0/false 가 아닌 7개(Threshold/Sigma/SampleCount/Trim/Polarity/Direction/Selection)만 키 없을 때 선언 기본값 복원"
  - "P-13 (D-79-05/D-79-08): 국부 피팅은 별도 EdgeStrengthScore 를 쓰고 측정의 Z 선택 점수(LastFitScore)를 건드리지 않음 — TryExecute 안에는 기준 ROI 피팅이 없음(재확인)"

requirements-completed: [LSR-01, LSR-02, LSR-03, LSR-05]

coverage:
  - id: D1
    description: "옵션 켠 EdgeToLineDistance 1개 — 기준점 가로 사진 피팅 → 사이클 저장소 → 측정 직전 주입 → 국부 기준선 거리(5.0) + LastRefSource='Local', 옵션 꺼짐(5.6)과 비트 동일"
    requirement: "LSR-02"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe synthetic — case=off_global, case=on_local, case=on_null_injected, case=axis_x (Y축·X축 모두 확인)"
        status: pass
    human_judgment: false
  - id: D2
    description: "기준 ROI 실패 5가지가 값 불변 + 원인 로그 1줄로 전역 기준선 전환(강제 NG·검사 중단 없음)"
    requirement: "LSR-03"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe synthetic — case=on_not_found, case=pin_fail_refsource, case=fallback_not_taught, case=settings_key"
        status: pass
    human_judgment: false
  - id: D3
    description: "옛 레시피(Local Ref 키 없음)는 옵션 꺼짐 + 에지 설정 선언 기본값으로 읽힘, 저장 후 재로드 round-trip 일치"
    requirement: "LSR-01"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe ini — case=ini_old_defaults, case=ini_roundtrip, case=ini_real_c13 (main-snapshot.ini C13_P1)"
        status: pass
    human_judgment: false
  - id: D4
    description: "편집 전 exe 와 새 exe 의 옵션 꺼짐 측정 결과(합성 6 + main.ini EdgeToLineDistance 96섹션 × 실사진 2장 = 198경우)가 바이트 단위로 같음"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "OffRegressProbe.exe(편집 전 exe 로 컴파일) — regress-base.txt vs regress-new-01a.txt(Task1) / regress-new-01b.txt(Task2) cmp -s"
        status: pass
    human_judgment: false
  - id: D5
    description: "실제 09-17 자재 A z1 가로 사진에서 핀 옆 띠 stripL·stripR 이 에지 설정 8조합 중 1개 이상에서 검출됨(설계 전제 성립 확인)"
    requirement: "LSR-02"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe real1 D:/Data/Result/Image/260917/1738/original/datum_SIDE_1_Side_Datum_1_H_173855718.jpg — stripL anyfound=1 stripR anyfound=1 exceptions=0"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-09-18
status: complete
---

# Phase 79 Plan 01: 국부(핀 옆 띠) 기준선 — 기준 ROI 피팅·전환·하위호환 Summary

**SIDE EdgeToLineDistance 측정에 국부 기준(핀 옆 띠) 옵션을 추가 — 기준점 가로 사진에서 기준 ROI 를 피팅해 사이클 저장소에 두고 측정 직전 주입, 실패 5가지는 원인 로그와 함께 전역 기준선으로 조용히 전환하며 옵션 꺼짐·옛 레시피는 바이트 단위로 기존과 동일하다.**

## Performance

- **Duration:** 약 35분 (Task 1 tracer + 체크포인트 승인 + Task 2, 편집 전 기준 빌드 시각 13:30 → LocalRefProbe 최종 컴파일 14:00, KST)
- **Completed:** 2026-09-18T05:01:52Z
- **Tasks:** 2 (Task 1 tracer, Task 2 전환·하위호환)
- **Files modified:** 4 (EdgeToLineDistanceMeasurement.cs, MeasurementBase.cs, InspectionSequence.cs, Action_FAIMeasurement.cs)

## Accomplishments

- 옵션 켠 EdgeToLineDistance 1개의 국부 기준선 전체 경로 완성: 기준점 검출 성공 지점(InspectionSequence.TryRunSingleDatum / 5인자 TryComposeAlign) → ComputeLocalRefLine 피팅 → `_localRefLines` 사이클 저장소 → Action_FAIMeasurement.InjectLocalRef 주입 → TryExecute 가 국부 기준선 중점을 원점으로 써서 거리(mm) 산출, `LastRefSource='Local'`
- 국부를 못 쓰는 5가지(DatumRef 없음 / 기준점 원점 미주입 / 이번 사이클 미계산 / 기준 ROI 미티칭·에지 못 찾음 / 검출 뒤 설정 바뀜)를 `TryResolveLocalRef` 로 순서대로 판정해 값은 옵션 꺼짐과 비트 동일하게 유지하고 `LastRefSource='LocalFallback'` + 원인 로그(`[LocalRef] 전역 기준선으로 전환 — ...`) 1줄만 남김
- `BuildLocalRefSettingsKey()` 로 기준점 검출 시점과 주입 시점의 기준 ROI·에지 설정·DatumRef 가 같은지 스냅샷 비교(stale 감지)
- `EdgeToLineDistanceMeasurement.Load` override 로 옛 레시피(Local Ref 키 없음)는 에지 설정 7개(Threshold/Sigma/SampleCount/Trim/Polarity/Direction/Selection)만 선언 기본값으로 복원, 옵션·ROI 좌표는 0/false 가 곧 꺼짐이라 복원 불필요
- 옵션 꺼짐 측정·옛 레시피의 값이 편집 전 exe 와 바이트 단위로 동일함을 OffRegressProbe(합성 6 + 실 레시피 EdgeToLineDistance 96섹션 × 실사진 2장 = 198경우)로 확인
- 실제 09-17 자재 A z1 가로 사진에서 핀 옆 띠(stripL·stripR) 가 8가지 에지 설정 조합 중 다수에서 검출됨을 확인해 국부 기준선 설계 전제 성립을 검증

## Task Commits

Each task was committed atomically:

1. **Task 1 (tracer): 옵션 켠 EdgeToLineDistance 1개 — 기준점 가로 사진에서 기준 ROI 피팅 → 사이클 저장소 → 측정 직전 주입 → 국부 기준선까지 거리 + LastRefSource, 옵션 꺼짐 비트 동일** - `f5948431` (feat)
2. **Task 2: 자동 전환 원인·로그(D-79-06) + 기준점 검출 뒤 설정 바뀜 감지 + 옛 레시피 기본값(Load override)** - `44f82975` (feat)

**편집 전 기준(base-79-01):** `0e4de45071e007ce40f7e4fbdfdfcdee0b7a1b56` (docs(phase-79): STATE — 계획 완료, 실행 대기)

_TDD 없음 — 두 태스크 모두 probe(합성/실사진/ini) 기반 automated verify._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` - Local Ref 속성 13개, `LocalRefLineResult`(+ `SettingsKey`), `ComputeLocalRefLine`, `InjectedLocalRef`, TryExecute 기준선 원점 교체(12줄), FAI-RefLine 오버레이, `BuildLocalRefSettingsKey`, `Load` override + `IsKeyMissing`. `git diff -w` 기준 삭제=12(전부 DatumOrigin→dAxisOrigin/200.0→AXIS_HALF_LENGTH_PX 치환)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` - `REF_SOURCE_LOCAL`/`REF_SOURCE_FALLBACK`, `LastRefSource`(ClearResult 에서 null). 삭제=0
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `_localRefLines` 사이클 저장소, `ComputeLocalRefLinesForDatum` 훅(2곳 return true 직전), `TryGetLocalRefLine`, `CollectLocalRefConsumers` 계열. 삭제=0
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `InjectLocalRef`, `TryResolveLocalRef`(D-79-06 전환 원인 5가지). 삭제=0

## Decisions Made

계획 단계 결정(phase 전체 — 79-02~05 도 이 표를 따른다), P-1~P-15:

| # | 결정 | 근거 요약 |
|---|---|---|
| P-1 | 훅 위치 = InspectionSequence 기준점 검출 성공 지점 2곳(TryRunSingleDatum, 5인자 TryComposeAlign)의 return true 직전 | ProcessOneDatum 이 비-크로스-Z 기준점을 캐시로 건너뛰어(:315) RESEARCH 권고 위치는 뒤 Shot 을 놓침; 검출 성공 지점 1곳이면 SIDE·TOP·BOTTOM·Test Find 가 같은 코드를 탐(D-79-09) |
| P-2 | 대상 측정 = 시퀀스 소유 Shot 전체 × EdgeToLineDistance × DatumRef 일치 × IsLocalRefEnabled | 캐시 재사용 Shot 포함, 옵션 끈 측정은 피팅 0회 |
| P-3 | 저장 = `_localRefLines`(참조 동등 키), `_datumStateLock` 사전 연산만, ClearDatumTransforms 동시 Clear, 재검출 시 지우고 새로 씀 | `_datumTransforms` 와 같은 수명. 필드+TryExecute 리셋 방식은 Z 후보 두 번째 실행부터 기준선을 잃어 채택 안 함 |
| P-4 | 전달 = InjectDatumOrigin 바로 다음 InjectLocalRef, 측정당 tick 1번, Z 범위 처리 전 | IDatumOriginConsumer 주입과 같은 방식 |
| P-5 | 기울기 = 위치만 국부(원점=중점), 각도는 전역 유지 (O-79-02 b) | 12줄 치환만으로 끝나 회귀 면 최소화; 국부 기울기(a) 는 A-79-A1 로 유보 |
| P-6 | 사용 기준 결정 시점 = TryExecute 입구(LastFitScore=0.0 다음) | Z 후보 실패해도 LastRefSource 가 채택 후보와 같게 유지 |
| P-7 | 전환 조건 5가지·원인(D-79-06) | 계산 시점 로그와 주입 시점 로그를 분리 |
| P-8 | 국부 전용 에지 기본값(Threshold10/Sigma1.0/Sample20/Trim10%/DarkToLight/TtoB/**Strongest**) | Selection 만 Strongest 로 창 안 이중 에지 잡음 완화 |
| P-9 | Load override — 0/false 아닌 7개 키만 선언 기본값 복원 | RESEARCH Pattern 4(IniValue.ToString 은 키 없으면 null) |
| P-10~P-15 | 기록(cycle.json/CSV)·표시(그리드/주황 선)·ROI 티칭 배선·Z 선택 무관·기준값 자동 변경 안 함·버전 1.7.50.0 | 79-02~05 로 이어짐, 이 plan 범위 밖 |

**Flagged assumptions (자동 해소 금지 — 사람 판단 대기):**
- A-79-E1 (LSR-01): Local Ref 탭 구성·첫 기준 ROI 숫자 입력 방식이 운영자에게 쓸 만한지 — 79-05 U-1
- A-79-E2 (LSR-02): z1 가로 사진 띠 위치를 측정 사진 핀과 같은 좌표로 쓰는 전제가 실제 사이클에서 성립하는지 — U-2·U-6 (전역도 이미 이렇게 씀)
- A-79-E3 (LSR-03): 전환 로그·표시 문구가 원인 추적에 충분한지 — U-3
- A-79-E4 (LSR-06): 자재 A-B 편차가 전역보다 뚜렷이 줄어드는지(수 µm 목표) — U-6 사용자 판단, 기준 ROI 창 선택에 좌우됨
- A-79-A1: 기울기는 전역 유지(P-5 b). 국부 기울기(a) 가 더 나은지 미검증
- A-79-A2: z1 사진 핀·띠 위치가 이번 조사로 확정되지 않음(RESEARCH A5) — 79-05 realab 재확인
- A-79-A3: 사무실 PC 에 TOP·BOTTOM 사진 없음 — 공용 코드 경로라 구조는 같지만 실측은 장비 PC(U-7 backstop)
- A-79-A4: 옵션 꺼진 측정의 `기준` 칸 = 빈칸(D-79-07 "전역"을 빈칸으로 해석) — U-4 확인
- A-79-A5: 첫 기준 ROI 는 속성창 숫자 입력(CommitRectRoi 미변경) — 불편하면 후속 quick
- A-79-A6: Test Find 재사용 수동 RUN 은 그 사진의 국부 기준선을 씀, 이후 ROI 변경 시 전환 — U-3
- A-79-A7: 저장 캡처 사진의 국부 기준선은 주황이 아니라 기존 '그 외' 색(파랑) — 79-03 이 OverlayCaptureRenderer 미변경, U-4 확인

## Deviations from Plan

None - 계획대로 실행. Task 1 tracer 는 체크포인트에서 "approved" 승인 후 그대로 진행.

## Issues Encountered

- 없음. Task 2 빌드(msbuild_exit=0), probe 컴파일·실행, OffRegressProbe 비트 비교 모두 1회에 통과.
- (환경 메모, 오케스트레이터 인계사항과 동일) Git Bash 에서 probe 실행 시 출력 파일 인자는 `C:/...` 형식(Windows 경로)으로 넘겨야 한다 — MSYS `/c/...` 경로를 주면 OffRegressProbe 가 예외를 던진다. 이번 실행은 전부 `$W`(Windows 표기) 변수를 사용해 문제 없었다.

## Probe 출력 원문

### synthetic (11사례, `synthetic_fail=0`)

```
case=off_global ok=1 value=5.605 refsource=- refline=0 result=PASS
case=on_local reffound=1 midRow=1699.50 ok=1 value=5 refsource=Local refline=1 result=PASS
case=on_null_injected ok=1 bitEqual=1 refsource=LocalFallback refline=0 result=PASS
case=on_not_found reffound=0 referr=insufficient edge points (0) across 20 strips ok=1 bitEqual=1 refsource=LocalFallback result=PASS
case=pin_fail_refsource ok=0 refsource=Local result=PASS
case=two_meas_one_on aRefSource=Local bOk=1 bBitEqual=1 bRefSource=- result=PASS
case=axis_x off=-5.605 on=-5 delta=0.605 reffound=1 refsource=Local result=PASS
case=fallback_not_taught reffound=0 referr=기준 ROI 가 티칭되지 않음 (LocalRef_Length1/Length2 가 0) ok=1 bitEqual=1 refsource=LocalFallback result=PASS
case=settings_key keymatch=1 rowdiff=1 polaritydiff=1 datumrefdiff=1 result=PASS
case=copyto copyok=1 enabledSame=1 rowSame=1 injectedNull=1 lastRefSourceNull=1 result=PASS
case=clearresult lastRefSourceAfterClear=- result=PASS
synthetic_fail=0
```
(`fallback_not_taught` 의 `referr` 콘솔 표시는 codepage 문제로 이 문서에서 원문 한글로 보정했다 — 프로그램 내부 문자열 비교는 UTF-16 그대로라 영향 없음)

### ini (3사례, `ini_fail=0`)

```
case=ini_old_defaults enabled=0 roiZero=1 threshold=10 sigma=1 sample=20 trim=10 polarity=DarkToLight direction=TtoB selection=Strongest result=PASS
case=ini_roundtrip enabled=1 roi=1 edge=1 result=PASS
case=ini_real_c13 hassection=1 enabled=0 pointRow=5673.6175710438 edgeDir=BtoT localThreshold=10 result=PASS
ini_fail=0
```
(`ini_real_c13` 대상 섹션 = `main-snapshot.ini` 의 `[SHOT_5_FAI_0_MEAS_0]`, `MeasurementName=C13_P1` 첫 섹션 — 같은 이름이 `[SHOT_23_FAI_0_MEAS_0]` 에도 있으나 줄 번호 기준 첫 섹션만 확인)

### real1 (z1 사진, 8조합 × stripL/stripR = 16줄)

```
real1 window=stripL combo=TtoB/DarkToLight/10 found=1 midRow=6593.97 score=88.8
real1 window=stripL combo=TtoB/DarkToLight/5 found=1 midRow=6593.97 score=88.8
real1 window=stripL combo=TtoB/LightToDark/10 found=1 midRow=6523.23 score=18.2
real1 window=stripL combo=TtoB/LightToDark/5 found=1 midRow=6547.34 score=21.1
real1 window=stripL combo=BtoT/DarkToLight/10 found=1 midRow=6523.23 score=18.2
real1 window=stripL combo=BtoT/DarkToLight/5 found=1 midRow=6547.34 score=21.1
real1 window=stripL combo=BtoT/LightToDark/10 found=1 midRow=6593.97 score=89.2
real1 window=stripL combo=BtoT/LightToDark/5 found=1 midRow=6593.97 score=89.2
real1 window=stripR combo=TtoB/DarkToLight/10 found=1 midRow=6479.10 score=26.3
real1 window=stripR combo=TtoB/DarkToLight/5 found=1 midRow=6479.10 score=26.3
real1 window=stripR combo=TtoB/LightToDark/10 found=1 midRow=6438.75 score=10.1
real1 window=stripR combo=TtoB/LightToDark/5 found=1 midRow=6438.87 score=10.5
real1 window=stripR combo=BtoT/DarkToLight/10 found=1 midRow=6438.75 score=10.1
real1 window=stripR combo=BtoT/DarkToLight/5 found=1 midRow=6438.87 score=10.5
real1 window=stripR combo=BtoT/LightToDark/10 found=1 midRow=6479.10 score=26.3
real1 window=stripR combo=BtoT/LightToDark/5 found=1 midRow=6479.10 score=26.3
real1 stripL anyfound=1 stripR anyfound=1 exceptions=0
```

**79-05 realab 출발점(가장 강한 조합):** stripL = `TtoB/LightToDark/10` 또는 `/5`(score 89.2, midRow 6593.97 부근에서 `TtoB/DarkToLight` 와 사실상 동일값), stripR = `TtoB/DarkToLight/10` 또는 `/5`(score 26.3, midRow 6479.10). stripL 점수(88.8~89.2)가 stripR(10.1~26.3)보다 뚜렷이 높다 — 79-05 U-2 에서 실제 사이클로 재확인 필요(A-79-E2).

### regress (OffRegressProbe, 편집 전 exe 기준)

- `regress-base.txt` total=198 (합성 6 + main-snapshot.ini `TypeName=EdgeToLineDistance` 96섹션 × 실사진 2장)
- Task 1 후: `regress-new-01a.txt` vs `regress-base.txt` → `regress_diff=0`
- Task 2 후: `regress-new-01b.txt` vs `regress-base.txt` → `regress_diff=0` (Load override 를 거쳐도 옵션 꺼짐 결과 비트 동일)
- `base_repeat_diff=0`(편집 전 exe 2회 실행 결과 동일 — 비교 기준 결정적), `warn-base.txt` 5줄(경고 기준선, 79-04 가 새 경고 0 비교에 사용)

## New Symbols (Artifacts 표 79-01 행 대조)

| 심볼 | 파일 | 확인 |
|---|---|---|
| `LocalRefLineResult`(DatumName/Found/Row1/Col1/Row2/Col2/EdgeScore/Error/SettingsKey/MidRow/MidCol) | EdgeToLineDistanceMeasurement.cs | O |
| 레시피 속성 13개(IsLocalRefEnabled, LocalRef_Row/Col/Phi/Length1/Length2, LocalRefEdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection/EdgeSelection) | EdgeToLineDistanceMeasurement.cs | O (`props=13`) |
| `InjectedLocalRef` | EdgeToLineDistanceMeasurement.cs | O |
| `ComputeLocalRefLine`, `IsInjectedLocalRefUsable`, `ResolveRefSourceCode`, `BuildLocalRefSettingsKey`, `Load` override, `IsKeyMissing` | EdgeToLineDistanceMeasurement.cs | O |
| const `LOCAL_REF_LOG_TAG`/`LOCAL_REF_OVERLAY_ROI_ID`/`LOCAL_REF_ROI_SUBKEY`/`LOCAL_REF_ERR_*`/`LOCAL_REF_REASON_*`/`AXIS_HALF_LENGTH_PX`/`LOCAL_REF_DEFAULT_*`/`SETTINGS_KEY_*` | EdgeToLineDistanceMeasurement.cs | O |
| 오버레이 `FAI-RefLine` | EdgeToLineDistanceMeasurement.cs | O |
| `MeasurementBase.REF_SOURCE_LOCAL`/`REF_SOURCE_FALLBACK`/`LastRefSource` | MeasurementBase.cs | O |
| `InspectionSequence._localRefLines`, `TryGetLocalRefLine`, `ComputeLocalRefLinesForDatum`, `CollectLocalRefConsumers`, `AppendShotLocalRefConsumers`, `IsLocalRefConsumer`, `RemoveLocalRefLinesOfDatum`, `StoreLocalRefLine`, `LogLocalRefLine` | InspectionSequence.cs | O |
| `Action_FAIMeasurement.InjectLocalRef`, `TryResolveLocalRef` | Action_FAIMeasurement.cs | O |
| 로그 줄 5종([LocalRef] 기준선 찾음/기준 ROI 실패/N개 계산/전역 기준선으로 전환/예외) | InspectionSequence.cs, Action_FAIMeasurement.cs | O |

`REF_SOURCE_LOCAL_TEXT`/`REF_SOURCE_FALLBACK_TEXT`/`FormatRefSource`/`ParseRefSource`, cycle.json/CSV 기록, XAML 열, ROI 배선, 표시 색은 79-02~03 소관(이 plan 범위 밖).

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 79-02(기록·표시)는 `MeasurementBase.LastRefSource`(Local/LocalFallback/null)를 그대로 소비하면 됨 — 코드 값 확정
- 79-03(ROI 배선·주황 선)은 `LOCAL_REF_ROI_SUBKEY`("LocalRef")와 `LOCAL_REF_OVERLAY_ROI_ID`("FAI-RefLine")를 키로 사용
- 79-05 realab 은 위 real1 표의 stripL=TtoB/LightToDark(또는 DarkToLight)/10, stripR=TtoB/DarkToLight/10 조합에서 출발해 실제 기준 ROI 티칭 값을 확정
- A-79-E1~E4, A-79-A1~A7 전부 미해소 상태로 79-05 UAT 로 이월 — 이 plan 은 코드 경로·비트 동일·검출 가능성만 증명했고 사람 판단은 아직 없음

---
*Phase: 79-side-local-strip-datum*
*Completed: 2026-09-18*
