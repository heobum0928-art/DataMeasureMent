---
phase: quick-260819-gf1
plan: 01
subsystem: WPF_Example/Custom/Sequence/Inspection
tags: [refactor, ref-parameter, accumulator-object, fai-measurement]
requires: []
provides: [ShotMeasureAccumulator]
affects: [Action_FAIMeasurement.MeasureShotFaiList, Action_FAIMeasurement.ProcessOneMeasurement, Action_FAIMeasurement.FinalizeFaiTick]
tech-stack:
  added: []
  patterns: ["중첩 private class 를 통한 다중 ref 파라미터 통합 (필드 전용, 프로퍼티 금지 — CS0206 회피)"]
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "V9 파일전역 '{ get; set; } 0건' 체크는 FAIMeasurementContext.AllPass/MeasuredCount(무관한 기존 클래스, baseline 그대로)를 걸러내는 과도-광역 체크였다 — ShotMeasureAccumulator 자체 스코프 체크(0건, Task1 V7)로 대체 확인, 코드 수정 없음"
metrics:
  duration: "~35분"
  completed: 2026-08-19
---

# Phase quick-260819-gf1 Plan 01: ShotMeasureAccumulator ref 파라미터 통합 Summary

3개 메서드(`ProcessOneMeasurement` 4-ref, `MeasureShotFaiList` 4-ref, `FinalizeFaiTick` 3-ref)가 주고받던 `ref` 파라미터를 중첩 `private class ShotMeasureAccumulator`(순수 public 필드 6개) 참조 1개로 통합. `RunMeasure`·바깥 시그니처·4개 헬퍼는 전부 0줄 diff, 본문 변경은 두 곳 모두 기계 치환 규칙과 바이트 단위 동일함을 diff 로 직접 재확인.

## 무엇을 했는가

- `Action_FAIMeasurement.cs` L46-52(`ECrossZGate` enum) 다음, L54(`pMyContext` 필드) 앞에 `private class ShotMeasureAccumulator` 신설 — `AllPass`/`MeasuredCount`/`NMeasNg`/`ShotDisplayImageReplaced`(Shot 전체 수명) + `FaiAllPass`/`CrossZRoleImage`(FAI 1개 수명, 루프마다 리셋) 6개 순수 public 필드.
- `MeasureShotFaiList` 내부에서만 `acc` 를 만들어 바깥 ref 4개를 초기화 → 본문 전부 `acc.*` 로 갈아탐 → 메서드 끝에서 되쓰기 4줄로 바깥 ref 에 반영. 바깥 시그니처 5줄은 1글자도 안 바뀜.
- `ProcessOneMeasurement`: 시그니처의 ref 4개(`crossZRoleImage`/`faiAllPass`/`measuredCount`/`nMeasNg`) → `ShotMeasureAccumulator acc` 1개로 교체. 본문은 4규칙 기계 치환(`crossZRoleImage→acc.CrossZRoleImage`, `faiAllPass→acc.FaiAllPass`, `measuredCount→acc.MeasuredCount`, `nMeasNg→acc.NMeasNg`)만 적용, 정확히 16줄 변경.
- `FinalizeFaiTick`: 시그니처의 ref 3개(`crossZRoleImage`/`bShotDisplayImageReplaced`/`allPass`) → `acc` 1개로 교체(`bool faiAllPass` 값 파라미터는 그대로 유지). 본문 3규칙 기계 치환(`crossZRoleImage→acc.CrossZRoleImage`, `bShotDisplayImageReplaced→acc.ShotDisplayImageReplaced`, `allPass→acc.AllPass`, `faiAllPass` 는 제외) 적용, 정확히 8줄 변경(그중 1줄은 주석 — G-2 의 유일 예외로 `crossZRoleImage.Dispose()` 주석도 `acc.CrossZRoleImage.Dispose()` 로 함께 치환).
- 호출부 2곳(`ProcessOneMeasurement(...)`, `FinalizeFaiTick(...)`)을 `ref acc.필드` 나열 → `acc` 단일 인자로 정리.

## ①치환 규칙 기반 동치 diff 실제 출력

**Region A — `ProcessOneMeasurement` 본문에 4규칙(`crossZRoleImage→acc.CrossZRoleImage`, `faiAllPass→acc.FaiAllPass`, `measuredCount→acc.MeasuredCount`, `nMeasNg→acc.NMeasNg`) 적용 결과 vs 현재 코드:**
```
diff <(원본(4299401) 본문 | sed 4규칙) <(현재 본문)
[exit=0, 출력 없음]
```
자기검증: 4규칙이 원본 대비 실제로 건드리는 줄 수 = **16줄**(플래너 예측과 일치).

**Region B — `FinalizeFaiTick` 본문에 3규칙(`crossZRoleImage→acc.CrossZRoleImage`, `bShotDisplayImageReplaced→acc.ShotDisplayImageReplaced`, `allPass→acc.AllPass`, `faiAllPass` 제외) 적용 결과 vs 현재 코드:**
```
diff <(원본(4299401) 본문 | sed 3규칙) <(현재 본문)
[exit=0, 출력 없음]
```
자기검증: 3규칙이 원본 대비 실제로 건드리는 줄 수 = **8줄**(그중 1줄은 `crossZRoleImage.Dispose()` 주석 → `acc.CrossZRoleImage.Dispose()` 치환, G-2 의 유일 예외). `faiAllPass` 3건(파라미터 1 + 사용 2)은 규칙에서 제외돼 그대로 생존, `AggregateFaiResult(fai, faiAllPass, ...)` 호출 2건도 인자 무변경 확인.

두 diff 모두 **완전히 빈 출력**으로, "본문 코드가 기계적 문자열 치환 결과와 바이트 단위 동일하다"는 것을 실측으로 증명한다 — 판정 로직·분기 순서·로그 문자열·`switch` case 순서가 리팩토링 전후 1글자도 다르지 않다는 뜻.

## ②FAI 루프 리셋 2줄 위치 확인

`MeasureShotFaiList` 안:
```csharp
foreach (var fai in ShotParam.FAIList) {
    acc.FaiAllPass = true;                              // ← foreach fai 바로 다음 줄
    var faiOverlays = new List<EdgeInspectionOverlay>(); // per-FAI overlay 누적...
    //260729 hbk quick-fix(260729-hwb): ... (원본 3줄 주석 그대로)
    acc.CrossZRoleImage = null;                          // ← foreach meas 바로 앞줄
    foreach (var meas in fai.Measurements) {
        ProcessOneMeasurement(meas, parentSeq2, image, pixRes, acc, overlayAcc, faiOverlays, dctAlgoUsed);
    }
    FinalizeFaiTick(fai, acc.FaiAllPass, faiOverlays, sharedSrc, datumSnapshot, szSharedOriginPath, parentSeq2, acc);
}
```
두 리셋 줄은 원본과 같은 위치를 유지하고, 사이에 `faiOverlays` 선언 1줄 + 원본 hwb 주석 3줄이 그대로 끼어 있다(원본 구조 그대로, 억지로 붙이지 않음). 각 리셋 문자열은 파일 전체에서 1회씩만 등장(`grep -c` 로 유일성 확인).

## ③되쓰기 4줄 위치 근거

```csharp
            }               // using (var image = ...) 블록 닫힘
            //260819 hbk quick-260819-gf1: 되쓰기 — using 블록 바깥이라 image!=null / image==null 두 경로가
            //  전부 여기로 합류한다. 예외로 탈출하는 경우 되쓰기는 생략되지만 관측 불가하다 —
            //  RunMeasure 에는 try/catch 가 없어(실측) 호출부도 함께 unwind 되고,
            //  ref 로 읽는 지점(원래 RunMeasure 대입문) 이후 문장에 애초에 도달하지 못한다.
            allPass = acc.AllPass;
            measuredCount = acc.MeasuredCount;
            nMeasNg = acc.NMeasNg;
            bShotDisplayImageReplaced = acc.ShotDisplayImageReplaced;
        }                   // MeasureShotFaiList 메서드 닫힘
```
들여쓰기 12칸(메서드 본문 레벨) — `using` 블록(들여쓰기 16칸 이상) **바깥**에 위치해 `image!=null`/`image==null` 두 분기가 모두 이 지점에서 합류한다. `RunMeasure` 본문에 `try`/`catch` 가 0건이므로(실측), 이 메서드에서 예외가 나면 호출부까지 함께 unwind 되어 되쓰기 생략은 관측 불가능하다.

## ④ShotMeasureAccumulator 필드 선언 확인

```csharp
        private class ShotMeasureAccumulator {
            public bool AllPass;
            public int MeasuredCount;
            public int NMeasNg;
            public bool ShotDisplayImageReplaced;
            public bool FaiAllPass;
            public HImage CrossZRoleImage;
        }
```
6개 전부 `public <타입> <PascalCase이름>;` 순수 필드다(`{ get; set; }` 0건, 클래스 스코프 한정 확인). 프로퍼티였다면 `ref acc.CrossZRoleImage` 등 8곳의 `ref acc.필드` 전달이 CS0206 컴파일 에러로 즉시 잡혔을 것 — Task 1/2/3 빌드가 전부 error 0 으로 통과한 것 자체가 필드 선언의 증명이다.

## ⑤RunMeasure diff 0줄 확인

```bash
diff <(xm RunMeasure base.cs) <(xm RunMeasure 현재파일)
# 출력 없음 (exit=0)
```
`MeasureShotFaiList` 바깥 시그니처 5줄(`ref bool allPass, ref int measuredCount, ref int nMeasNg, ref bool bShotDisplayImageReplaced` 포함)도 baseline `4299401` 과 바이트 단위 동일 — `RunMeasure` 와의 호출 계약이 1글자도 바뀌지 않았다. `TakeCrossZRoleImageIfFirst`/`MarkCrossZHalfPending`/`MarkAllMeasurementsNoImage`/`AggregateFaiResult` 4개 헬퍼도 전문 0줄 diff.

## ⑥빌드 결과

| Task | OutputPath | error CS | warning CS | CS0206/0219/0168/0177/0165/0103/1027/1028 |
|---|---|---|---|---|
| Task 0 (baseline) | `gf1-t0` | 0 | 12 (WBASE) | 0 |
| Task 1 | `gf1-t1` | 0 | 12 | 0 |
| Task 2 | `gf1-t2` | 0 | 12 | 0 |
| Task 3 | `gf1-t3` | 0 | 12 | 0 |

3회 모두 Debug/x64 Rebuild — error 0, warning 이 착수 전 baseline(12 = CS0618×10 + CS0162×2, `reference_build_warning_baseline_12` 메모리와 일치)과 완전히 동일, 신규 CS0206(ref 프로퍼티) 등 0건.

## 커밋

- `5279c1b` refactor(260819-gf1): ShotMeasureAccumulator 신설 + MeasureShotFaiList 내부 누적객체화 (바깥 ref 시그니처·헬퍼 시그니처 무변경)
- `5d9bbb1` refactor(260819-gf1): ProcessOneMeasurement ref 4개를 ShotMeasureAccumulator 1개로 통합 (기계 치환, 동치 검증)
- `de53a81` refactor(260819-gf1): FinalizeFaiTick ref 3개를 ShotMeasureAccumulator 1개로 통합 (기계 치환, 동치 검증)

각 커밋은 `Action_FAIMeasurement.cs` **단 1개 파일**만 포함(`git show --name-only` 확인), `WPF_Example/DatumMeasurement.csproj` 오염은 3커밋 내내 unstaged 로 남음(`git log --name-only 4299401..HEAD | grep csproj` = 매치 0건).

## Deviations from Plan

### Auto-fixed Issues

없음 — plan 의 정확한 치환 규칙·삽입 위치를 그대로 따랐고, 코드 수정 없이 완료됨.

### 검증식 관련 참고사항 (코드 변경 아님)

Task 3 V9 의 파일전역 `grep -c '{ get; set; }' $F = 0` 체크가 실패했으나, 원인은 **무관한 기존 코드**였다:
`FAIMeasurementContext.AllPass`/`.MeasuredCount` (L18-19, `ShotMeasureAccumulator` 와 이름만 같고 완전히 다른 클래스, `ActionContext` 를 상속하는 기존 컨텍스트 클래스)가 baseline `4299401` 부터 이미 `{ get; set; }` 프로퍼티로 존재했다(`diff` 로 L1-30 무변경 확인). 이 파일에는 3개 task 어디도 손대지 않았다.
`ShotMeasureAccumulator` 자체 스코프로 좁힌 체크(Task 1 V7, `xm 'ShotMeasureAccumulator' | grep -c '{ get; set; }'` = 0)는 정확히 PASS 했다 — 이번 리팩토링이 도입한 새 클래스에는 프로퍼티가 0건이라는 실제 목표는 충족됐다. G-7("검증식 실패 시 코드를 고친다, 완화 금지")에 따라 코드 수정 여부를 검토했으나, `FAIMeasurementContext` 를 건드리는 것은 G-1("절대 금지" — 스코프 밖 파일/클래스 수정) 위반이자 무의미한 변경이라 코드는 그대로 두고 원인만 문서화함.

## Known Stubs

없음 — 이 plan 은 순수 데이터 전달 방식 리팩토링(ref → 참조형 필드)이며 신규 UI/데이터 소스 배선이 없다.

## Threat Flags

없음 — 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. 클래스 접근 제한자는 `private`(파일 내부 전용), 외부 노출 표면 무변화.

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
```

커밋 존재 확인:
```
FOUND: 5279c1b
FOUND: 5d9bbb1
FOUND: de53a81
```
