# Quick Task 260805-f3w: 검사 흐름 로그 신설 + 반복 스팸 로그 정리 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning

<domain>
## Task Boundary

두 가지를 함께 한다 (같이 해야 의미가 있음 — 시끄러운 걸 걷어내야 중요한 게 보인다):

1. **초보자용 검사 흐름 로그 신설** — 이 시스템을 처음 보는 사람도 "지금 무슨 단계를 하고 있고, 무엇이 나왔는지"를 로그만 읽고 이해할 수 있어야 한다. 한글, 평문, 전문용어 최소화.
2. **반복 스팸 로그 정리** — 사이클당 수십~수백 줄씩 쏟아지는 중복 로그를 요약/삭제한다.

</domain>

<decisions>
## Implementation Decisions

### D-F3W-01: "흐름" 로그 카테고리 신설 (LOCKED)
- `ELogType` 에 신규 카테고리(예: `Flow`)를 추가하고 `SystemHandler.cs:88-93` 의 `Logging.SetLog(...)` 등록 블록에 같은 패턴으로 등록한다. 기존 `LogViewChildWindow(ELogType type)` 인프라가 카테고리별 창을 이미 지원하므로 별도 UI 작업 없이 분리 조회가 가능하다.
- 흐름 로그는 이 신규 카테고리로만 나간다. 기존 `Trace` 는 기술 진단용으로 그대로 둔다 — 두 성격을 섞지 않는다.
- `ELogType.Image = 5` 는 현재 SetLog 등록이 안 되어 있다(미사용). 신규 enum 값은 기존 정수값을 바꾸지 않도록 **뒤에 추가**한다(로그 파일/설정 하위호환).

### D-F3W-02-REV: 흐름 로그 상세 수준 = 압축(시퀀스+전체 tact+결과값만) — 아래가 D-F3W-02 를 대체한다 (2026-08-05, "그룹 A" 요청으로 조정, LOCKED)

**원래 D-F3W-02(1단계/2단계/3단계 전환마다 + 측정 항목 하나하나 Nominal/Tol/합불까지 전부 찍는 상세판)는 폐기한다.** 대신 사이클 1회당 로그를 다음 3가지만 압축해서 남긴다:

1. **시퀀스명** — 어느 시퀀스(Top/Side/Bottom)의 사이클인지.
2. **전체 tact** — 사이클 시작부터 끝까지 걸린 시간(초).
3. **최종 결과값** — 종합판정(OK/NG) + 판정치. "판정치"는 최소한 기존에 설계했던 `FlowLog.CycleEnd`(종합판정/측정개수/NG개수/소요시간)의 골격 그대로면 충분 — 측정 항목별 세부(Nominal/Tol/기준치 나열)까지는 다시 넣지 않는다.

목표 형식 예시(압축판, 문구/기호는 구현 재량):
```
■ [SIDE] 사이클 종료 — 종합판정 NG (측정 30개 중 6개 벗어남, 소요 4.2초)
```

- 검사 단계 전환(기준선 찾기/촬영/측정 등) 로그는 **이번 범위에서 넣지 않는다** — Task 2(원래 계획의 단계별 로그 삽입)는 이 압축 결정에 맞게 축소하거나, 사이클 시작/종료 요약 로그(원래 Task 3)만으로 충분하면 Task 2 자체를 생략해도 된다. 플래너가 판단해서 재구성할 것.
- 전문용어(measurePhi, strip, transform, XLD 등)는 흐름 로그에 여전히 쓰지 않는다 — 이 원칙은 유지.
- **참고**: 이 압축 결정으로 이전 plan-checker 가 지적했던 "측정명(MeasurementName) fallback 시 `==null` 대신 `string.IsNullOrEmpty` 써야 하는 버그"(Task 2 의 측정 항목별 라인에서만 발생하던 이슈)가 범위에서 아예 빠질 수도 있다 — 플래너는 새 설계에 측정명을 항목별로 나열하는 부분이 실제로 남는지 먼저 확인하고, 남는다면 반드시 `string.IsNullOrEmpty` 로 고쳐서 반영할 것.

### D-F3W-03: 스팸 정리 — 범위 축소됨 (아래가 최신 LOCKED 결정, 2026-08-05 사용자 확정)

**이전 결정("전체 정리", 12건 전부)은 폐기됐다.** 사용자가 "필요한 것들만" / "중요한건 시퀀스야"로 범위를 좁혀 확정: **S1과 S11만 이번 quick task 범위에 포함한다.** 나머지(S2/S3+S4/S6+S7+S8/S9/S10)는 **이번 범위에서 명시적으로 제외** — ROI당 1~2줄 수준이라 흐름 로그를 가리지 않는다는 판단이며, 필요해지면 별도 작업으로 처리한다. S12는 여전히 절대 삭제 금지.

**단, S2는 예외 — S1과 묶어서 같이 처리한다.** S1 수정으로 ROI 요약줄에 `failedStrips`(실패한 strip 개수) 카운트가 새로 추가되는데, 이는 아래 표의 S2 조건("요약줄에 failedStrips 카운트가 생기면 예외 메시지는 첫 1건만 남기고 dedup")을 그대로 발동시킨다. 이건 별도 스코프가 아니라 S1 구현의 직접적인 결과이므로, S1을 구현하는 김에 이 dedup 가드(같은 ROI에서 첫 예외 메시지 1건만 남기고 이후는 조용히 카운트만 증가)도 같이 넣는다 — 코드 몇 줄이면 되고, 안 넣으면 S1 자신이 만든 조건을 스스로 어기는 모순이 생긴다.

원래 전수 조사(에이전트 조사 완료) 결과 표는 아래에 참고용으로 남겨둔다 — S1/S11(+S2 dedup 가드) 외 나머지는 이번 범위 밖이라는 점만 재확인.

| 건 | 위치 | 조치 |
|----|------|------|
| S1 | `DatumFindingService.cs:2182` (strip 루프 최내곽, 사이클당 40~240줄) | **ROI 단위 요약 1줄로 축약.** `VisionAlgorithmService.cs:125-184` 의 okStrips/noEdgeStrips/failedStrips 집계 패턴을 이식해 `DatumFindingService.cs:1810-1813` / `2066-2069` 의 기존 accumulated 요약줄에 병합. dir/sel/measurePhi 는 ROI당 1회만(같은 ROI 내 상수라 strip 반복 무의미), `edges=N` 은 집계로 보존 |
| S2 | `DatumFindingService.cs:2202` (strip catch) | 유지. 단 요약줄에 failedStrips 카운트가 생기면 예외 메시지는 첫 1건만 남기고 dedup |
| S3+S4 | `VisionAlgorithmService.cs:182`, `214` | 동일 스코프 연속 2줄 → 1줄 병합 |
| S5 | `VisionAlgorithmService.cs:189` | 유지 (조건부 Error, 정상 시 무출력 — 이상적 형태) |
| S6+S7+S8 | `DatumFindingService.cs:1677`/`1811`/`1820` 및 쌍(`1950`/`2067`/`2076`) | 3줄 → 1줄 병합 (셋 다 ROI 1회·동일 스코프) |
| S9 | `DatumFindingService.cs:1762`, `2018` | 임계 초과 시만 출력(예: tact > 50ms). 정상 시 조용 |
| S10 | `FAIEdgeMeasurementService.cs:76` | Shot 단위 1줄로 축약 (rotAngleDeg 는 한 Shot 내 모든 FAI 동일 transform → 측정마다 반복 무의미) |
| S11 | `InspectionSequence.cs:1859`, `1869` (`[ALIGN-DIAG-LIVE]`) | **삭제** — 임시 진단 스캐폴딩(modelPath 덤프) |
| S12 | `InspectionSequence.cs:1850`, `1879`, `1936` (`[ALIGN] datumDetectRotDeg vs patternThetaDeg`) | **절대 유지.** 실제로 measurePhi 부호 버그를 잡은 확증 로그(`.planning\quick\260618-o2m-*-SUMMARY.md:63-69`, 커밋 a719073). Datum당 1~2줄로 양도 적음 |
| — | `Action_FAIMeasurement.cs:461` (`[FAI CrossZ IMG]`) | 크로스-Z 추적용 임시 로그. 유지하되 흐름 로그와 중복되면 Trace 로만 남긴다 |

### Claude's Discretion
- 신규 enum 멤버 이름(`Flow` / `Sequence` / `흐름` 대응 영문명), 로그 접두 기호(▶/■/→ 등), 들여쓰기 방식.
- 흐름 로그를 심는 정확한 코드 지점(`Action_FAIMeasurement` 의 EStep 전환부 vs `InspectionSequence` 상위) — 단, "한 사이클이 처음부터 끝까지 순서대로 읽히는" 결과가 나와야 한다.
- 요약줄 포맷/필드 구성, tact 임계값.

</decisions>

<specifics>
## Specific Ideas

- 로그 인프라: `WPF_Example\Utility\Logging.cs` (`SetLog` 137행, `PrintLog` 275-297행 — 필터는 `IsTerminated` + `LogList.ContainsKey` 뿐, 레벨/verbosity 개념 없음), `WPF_Example\Setting\SystemSetting.cs:18-26` (`ELogType` enum), `WPF_Example\SystemHandler.cs:88-93` (SetLog 등록 블록), `WPF_Example\UI\Log\LogViewChildWindow.cs:22` (`LogViewChildWindow(ELogType type)` — 카테고리별 창).
- 검사 흐름의 뼈대: `Action_FAIMeasurement.cs` 의 EStep 상태머신(Init/DatumPhase/Grab/Measure/End), `InspectionSequence.cs`.
- 측정 결과 필드: `MeasurementResultDto` / 측정 객체의 `LastMeasuredValue`, `LastJudgement`, `LastHasResult`, `LastSkipReason`, `NominalValue`, `TolerancePlus`, `ToleranceMinus`.
- `SkipReason` 상수(`Custom\Sequence\Inspection\SkipReason.cs`): DATUM_FAIL, ALIGN_FAIL, NO_IMAGE, DATUM_REF_MISSING, ZINDEX_MISCONFIGURED, CROSS_Z_INCOMPLETE — 흐름 로그에선 이걸 쉬운 한국어 문장으로 매핑해야 한다.
- 측정 알고리즘 클래스(`Custom\Sequence\Inspection\Measurements\*.cs`)에는 `Logging` 호출이 0건 — 스팸원 아님, 건드릴 필요 없음.
- `Action_FAIMeasurement.cs` 의 기존 Logging 호출 32건은 전부 실패/skip 경로(Error)라 정상 사이클엔 조용함 — 삭제 대상 아님.

</specifics>

<canonical_refs>
## Canonical References

- 스팸 전수 조사 결과는 위 D-F3W-03 표에 이미 반영됨(에이전트 조사, 파일:라인 검증 완료). 재조사 불필요.
- `VisionAlgorithmService.cs:125-184` — strip 결과 집계(okStrips/noEdgeStrips/failedStrips → 요약 1줄) 참조 구현. S1 축약 시 이 패턴을 그대로 따른다.

</canonical_refs>
