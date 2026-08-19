---
phase: quick-260819-hyk
plan: 01
subsystem: inspection
tags: [csharp, refactor, halcon, cross-z, extract-method]

requires:
  - phase: quick-260818-ukh, quick-260818-vih, quick-260818-ruh
    provides: "동일 파일 내 앞선 순수-이동 Extract Method 3건 (LogAndTallyAlgorithm/MeasureShotFaiList/RenderDatumOverlay 구역 등)"
provides:
  - "ProcessOneMeasurement 131줄 → 본문 20줄(span 27) 로 축소"
  - "TryGateMeasurement(bool) — Datum 검출실패 게이트 + DatumRef 참조깨짐 게이트"
  - "EvaluateCrossZGate(bool + out 2) — 크로스-Z 5-case 판정 전체, 6경로 bool 매핑"
  - "RecordMeasurementResult(void) — 판정/로그/오버레이/카운터 마무리 (순수 이동)"
affects: [action-faimeasurement, cross-z-measurement]

tech-stack:
  added: []
  patterns: ["Extract Method with control-flow translation (return;/break; → return bool;), verified via byte-identical diff against mechanically-generated expected files"]

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "return; -> return false; (4경로) / break; -> return true; (1경로) / if-블록-밖 fall-through -> return true; (1경로) — 6경로 전수 PIN1~PIN6 앵커 검증으로 확정"
  - "case 본문·기존 상세주석 8종은 1바이트도 손대지 않고 그대로 이동 — 신규 doc주석은 각 메서드 본문 위 8칸에만 추가"

requirements-completed: [HYK-01, HYK-02, HYK-03]

duration: 12min
completed: 2026-08-19
---

# Quick 260819-hyk: ProcessOneMeasurement 3분할 Summary

**`Action_FAIMeasurement.ProcessOneMeasurement`(131줄)를 `TryGateMeasurement`/`EvaluateCrossZGate`/`RecordMeasurementResult` 3개로 분리, 6가지 종료 경로(`return;`/`break;` → `return false;`/`return true;`)를 PIN1~PIN6 앵커 검증과 4구간 바이트동치 diff로 회귀 0 확인**

## Performance

- **Duration:** 약 12분
- **Started:** 2026-08-19T04:28:00Z (Task 0 baseline)
- **Completed:** 2026-08-19T04:39:51Z (Task 3 커밋)
- **Tasks:** 4 (Task 0 baseline 읽기전용 + Task 1/2/3 각 1커밋)
- **Files modified:** 1

## Accomplishments
- `ProcessOneMeasurement` 착수 전 span 130줄 → 최종 span 27줄(시그니처 6 + 본문 20 + `}` 1)
- `TryGateMeasurement`(span 23) / `EvaluateCrossZGate`(span 74) / `RecordMeasurementResult`(span 24) 3개 메서드 신설
- 크로스-Z 6경로 bool 매핑 전수 검증 통과 — 가장 위험한 지점(BothReady=`break;` → `return true;`)이 코드·빌드·검증 3중으로 확인됨
- 기존 상세 주석 8종 전부 생존(삭제 0건), `default:` 라벨 미도입 원칙 유지
- 3개 Task 각각 독립 커밋, `Action_FAIMeasurement.cs` 1파일만 변경

## Task Commits

Each task was committed atomically:

1. **Task 1: TryGateMeasurement 추출 (게이트 2개, return→return false)** - `a80cc77` (refactor)
2. **Task 2: EvaluateCrossZGate 추출 (6경로 bool 매핑)** - `7f5e8ba` (refactor)
3. **Task 3: RecordMeasurementResult 추출 (순수 이동) + 최종 골격 확정** - `526765d` (refactor)

_Task 0(baseline)은 읽기전용이라 커밋 없음 — 스크래치에 `base.cs` 스냅샷 + `exp0a/exp0b/exp1~exp4.txt` 6종 기대파일 생성._

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `ProcessOneMeasurement`을 3개 private 메서드로 분할(구조 변경, 판정/로그/저장 로직 무변경)

## ① 6-경로 매핑표 — 실제 코드 인용으로 재확인

착수 전 `ProcessOneMeasurement` 안에서 크로스-Z 구간을 빠져나가는 6개 경로가, 신설된 `EvaluateCrossZGate`(HEAD `526765d`) 안에서 다음과 같이 매핑되었다. 아래는 커밋된 파일에서 그대로 발췌한 실제 `switch` 블록이다:

```csharp
                switch (eGate)
                {
                    case ECrossZGate.Misconfigured:
                        MarkMeasurementZIndexMisconfigured(meas);
                        acc.FaiAllPass = false;
                        acc.MeasuredCount++;
                        return false;                                    // ① 설정오류 — 측정 실행 안 함
                    case ECrossZGate.NotMyTick:
                        if (bNonProtocolCycle)
                        {
                            MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2);
                            acc.FaiAllPass = false;
                            acc.MeasuredCount++;
                        }
                        return false; // 프로토콜: 이 tick 은 이 측정과 무관 — 상태변화 없음(안전망, 무변경)
                                                                           // ② if 블록 밖, 양 갈래 공통 도달
                    case ECrossZGate.CaptureFailed:
                        meas.ClearResult();
                        meas.LastSkipReason = SkipReason.NO_IMAGE;
                        meas.LastJudgement = false;
                        acc.FaiAllPass = false;
                        acc.MeasuredCount++;
                        return false;                                    // ③ 캡처 실패 — 측정 실행 안 함
                    case ECrossZGate.HalfPending:
                        TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage);
                        MarkCrossZHalfPending(meas, parentSeq2, bNonProtocolCycle, ref acc.FaiAllPass, ref acc.MeasuredCount);
                        return false;                                    // ④ 짝 미완성 — 측정 실행 안 함
                    case ECrossZGate.BothReady:
                        TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage);
                        return true; // 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)
                                                                           // 🔴⑤ 원본은 break; 였던 자리 — 뒤집히면
                                                                           //     완성된 크로스-Z 측정이 조용히 실행 안 됨
                }
```

메서드 맨 끝, `if (bHasAnyZIndex)` 블록 **밖**의 ⑥ 일반 측정 경로:

```csharp
            }
            return true; // 크로스-Z 가 아닌 일반 측정 — 원본에서 if 블록을 건너뛰던 경로와 동치
        }
```

**PIN1~PIN6 실측 결과 (모두 PASS):**

| PIN | 경로 | 검증 방식 | 결과 |
|---|---|---|---|
| PIN1 | Misconfigured | `case` +4줄 = `return false;` (정확일치) | OK |
| PIN2 | NotMyTick | `if(bNonProtocolCycle)` 블록 +6줄=`}` 다음 줄 `return false; // 프로토콜:...` | OK |
| PIN3 | CaptureFailed | `case` +6줄 = `return false;` | OK |
| PIN4 | HalfPending | `case` +3줄 = `return false;` | OK |
| PIN5(🔴최고위험) | BothReady | `case` +2줄 = `return true; //...`, 같은 줄에 `return false;` 없음, +3줄 = switch 닫는 `}` | OK |
| PIN6 | `!bHasAnyZIndex` | `if` 블록 닫는 `}` 다음 줄 = `return true;`, 그 다음 줄 = 메서드 종료 `}` | OK |

**총량:** `return false;` = 4건(①②③④), `return true;` = 2건(⑤⑥), `break;` = 0건, 알몸 `return;` = 0건, `default:` 라벨(엄격패턴 `^[[:space:]]*default:`) = 0건. `case ECrossZGate.` 5개, 순서(Misconfigured→NotMyTick→CaptureFailed→HalfPending→BothReady) 보존.

## ② case 본문 바이트 동일 diff — 실제 출력

Task 0에서 HEAD `a57e744` 스냅샷으로부터 기계적으로 생성한 4개 기대파일(`exp1~exp4.txt`)과, 최종 커밋된 파일에서 동일 좌표를 잘라낸 구간을 `diff`한 실측 결과(전부 빈 출력 = 완전 동일):

```
=== exp1 diff (TryGateMeasurement 본문 17줄) ===
exit=0
=== exp2 diff (EvaluateCrossZGate 본문 68줄) ===
exit=0
=== exp3 diff (POM 중간 실행부 15줄) ===
exit=0
=== exp4 diff (RecordMeasurementResult 본문 18줄) ===
exit=0
```

4구간 전부 `diff` 출력 0줄 — case 본문의 로그 호출·`acc.FaiAllPass`/`acc.MeasuredCount` 대입·`ref acc.CrossZRoleImage` 등 `ref` 인자·꼬리주석이 원본과 1바이트도 다르지 않음이 기계적으로 증명됨.

## ③ RecordMeasurementResult 순수 이동 확인

원본 HEAD L709–726(18줄, `LogAndTallyAlgorithm(...)` ~ `acc.MeasuredCount++;`)에는 애초에 제어흐름 문장(`return`/`break`/`continue`)이 0개였다. 최종 커밋 파일에서 `RecordMeasurementResult` 메서드 전체(span 24줄)를 대상으로 제어흐름 문장 개수를 재실측:

```
=== RecordMeasurementResult 제어흐름 문장 개수(순수이동 증명) ===
0
```

`void` 반환형이며 `return` 문 자체가 없음 — 판정(`meas.EvaluateJudgement`)·실패 로그(`Logging.PrintLog`)·오버레이 누적(`ApplyOverlaySuffixAndAccumulate`)·카운터(`acc.MeasuredCount++`)가 그대로 옮겨진 순수 이동임이 확인됨.

## ④ ProcessOneMeasurement 최종 골격

착수 전 130줄 → 최종 span 27줄(시그니처 6 + 본문 20 + `}` 1). 실제 커밋된 코드 전문:

```csharp
        private void ProcessOneMeasurement(MeasurementBase meas, InspectionSequence parentSeq2,
                                     HImage image, double pixRes,
                                     ShotMeasureAccumulator acc,
                                     List<EdgeInspectionOverlay> overlayAcc,
                                     List<EdgeInspectionOverlay> faiOverlays,
                                     Dictionary<string, int> dctAlgoUsed) {
            if (!TryGateMeasurement(meas, parentSeq2, acc)) return;
            DualImageEdgeDistanceMeasurement dualMeasForGate;
            bool bHasAnyZIndex;
            if (!EvaluateCrossZGate(meas, parentSeq2, acc, out dualMeasForGate, out bHasAnyZIndex)) return;
            HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef); //260702 hbk Extract Method(Task1)
            InjectDatumOrigin(meas, parentSeq2); //260702 hbk Extract Method(Task1)
            double resultValue;
            string measError;
            List<EdgeInspectionOverlay> measOverlays;
            bool ok;
            var swMeasureExec = Stopwatch.StartNew(); //260818 hbk 알고리즘 로그용 측정 실행시간
            if (bHasAnyZIndex)
            {
                ok = TryExecuteCrossZMeasurement(dualMeasForGate, parentSeq2, transform, pixRes, out resultValue, out measError, out measOverlays); //260722 hbk Phase 68 D-02a: 완성 index 크로스-Z 실행
            }
            else
            {
                ok = TryExecuteMeasurement(meas, image, transform, pixRes, out resultValue, out measError, out measOverlays); //260702 hbk Extract Method(Task1)
            }
            RecordMeasurementResult(meas, bHasAnyZIndex, ok, resultValue, measError, measOverlays, overlayAcc, faiOverlays, dctAlgoUsed, swMeasureExec, acc);
        }
```

이 골격은 "게이트 2개 → transform 계산/주입 → 실행 분기(크로스-Z/일반) → 마무리" 로 한눈에 읽힌다. `MeasureShotFaiList`의 호출부 1줄(`ProcessOneMeasurement(meas, parentSeq2, image, pixRes, acc, overlayAcc, faiOverlays, dctAlgoUsed);`)도 무변경.

## ⑤ 빌드 결과

단일 Debug|x64 빌드(손대는 구간 전처리 지시문 0건 확인됨). MSBuild `-t:Rebuild`로 매 Task 후 실행:

| 시점 | error CS | warning CS | 신규 진단(CS0161/CS0177/CS0165/CS0206/CS0219/CS0168/CS0103) |
|---|---|---|---|
| t0 (baseline) | 0 | 12 | - |
| t1 (Task 1 후) | 0 | 12 | 0 |
| t2 (Task 2 후) | 0 | 12 | 0 |
| t3 (Task 3 후) | 0 | 12 | 0 |

warning 12건은 착수 전 baseline과 완전히 동일(CS0618×10 + CS0162×2, 이 파일과 무관한 기존 baseline). `EvaluateCrossZGate`의 `out DualImageEdgeDistanceMeasurement dualMeasForGate` / `out bool bHasAnyZIndex`는 본문 첫 2줄에서 무조건 대입되므로 6개 반환 경로 전부 확정 대입 — `CS0177`/`CS0165`/`CS0161` 0건. `ref acc.CrossZRoleImage`/`ref acc.FaiAllPass`/`ref acc.MeasuredCount`도 `ShotMeasureAccumulator`가 여전히 필드이므로 `CS0206` 0건.

## Decisions Made
- `return;`(알몸) 4곳은 전부 `return false;`로, `break;` 1곳만 `return true;`로 변환 — plan의 6-경로 매핑표를 그대로 따름(별도 판단 불필요, 이미 완전히 결정된 사양)
- 신규 3개 메서드 배치 위치는 각 원본 코드 바로 다음 자리(레이아웃 고정) 그대로 따름

## Deviations from Plan

None — plan 실행 그대로. 플랜에 명시된 순서(Task 0 baseline → Task 1 → Task 2 → Task 3)와 앵커/좌표/기대값이 실제 코드와 전부 일치했고, 예외 처리나 추가 수정이 필요한 지점이 없었다.

## Known Stubs

None.

## Threat Flags

None — 이 작업은 순수 내부 리팩토링으로 신규 네트워크/인증/파일접근/스키마 표면을 도입하지 않는다.

## Issues Encountered
None.

## User Setup Required
None - 외부 서비스 설정 불필요.

## Next Phase Readiness
`ProcessOneMeasurement`이 20줄 본문으로 축소되어 가독성이 크게 개선됨. 크로스-Z 게이트/일반 게이트/마무리부가 독립 메서드로 분리되어 향후 크로스-Z 로직 확장(예: 6번째 `ECrossZGate` 멤버 추가) 시 `EvaluateCrossZGate` 한 곳만 수정하면 되는 구조가 확보됨. 판정 로직·검사 흐름·저장 결과는 4구간 바이트동치 diff와 6경로 PIN 검증으로 회귀 0이 기계적으로 증명됨.

---
*Phase: quick-260819-hyk*
*Completed: 2026-08-19*

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
- FOUND: `.planning/quick/260819-hyk-processonemeasurement-trygatemeasurement/260819-hyk-SUMMARY.md`
- FOUND commit `a80cc77` (Task 1)
- FOUND commit `7f5e8ba` (Task 2)
- FOUND commit `526765d` (Task 3)
