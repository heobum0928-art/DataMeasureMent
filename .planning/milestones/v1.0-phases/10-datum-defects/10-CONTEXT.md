# Phase 10: Datum 정확성 결함 수정 - Context

**Gathered:** 2026-04-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 4 code review (`.planning/phases/04-datum/04-REVIEW.md`)에서 제기된 3건(WR-01, WR-03, WR-05)의 Datum 정확성 관련 경고를 코드 수준에서 해소한다. 새로운 기능 추가 없음 — 기존 Datum/FAI 파이프라인의 수치적 정확성 결함을 수정하는 tech-debt phase.

</domain>

<decisions>
## Implementation Decisions

### WR-01: IntersectionLl 평행선 가드
- **D-01:** `isOverlapping.I == 1`만 검사하는 기존 가드는 "동일선(collinear)" 케이스만 걸러냄. 평행선은 `isOverlapping==0` + 교점 좌표가 `±Infinity`로 반환되므로, `double.IsInfinity || double.IsNaN` 범위 검사를 추가한다.
- **D-02:** 수정 범위 = 동일 버그 패턴이 있는 **3곳 모두** 일괄 수정.
  - `WPF_Example/Halcon/Algorithms/DatumFindingService.cs:75` (TryFindDatum)
  - `WPF_Example/Halcon/Algorithms/DatumFindingService.cs:163` (TryTeachDatum)
  - `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:274` (IntersectLines 정적 유틸)
- **D-03:** 수정 방식은 각 호출 지점에 인라인 가드 삽입(04-REVIEW.md WR-01 fix snippet 그대로). 공통 헬퍼 추출은 이번 phase 범위 밖 (scope creep).
- **D-04:** 실패 시 `error` 메시지는 "Lines are collinear (identical)" / "Lines are parallel, intersection is at infinity"로 구분해 로그에서 원인 추적이 가능하도록 한다. `IntersectLines`(`bool` 반환, `out` 좌표)는 기존 시그니처를 유지하고 메시지 없이 `false`만 반환.

### WR-03: hom_mat2d 회전 추출 인덱스
- **D-05:** `FAIEdgeMeasurementService.cs:67`의 회전각 추출을 `Math.Atan2(-transform[1].D, transform[0].D)` → `Math.Atan2(transform[3].D, transform[0].D)`로 변경.
- **D-06:** Halcon `hom_mat2d` 레이아웃 `[h00,h01,h02,h10,h11,h12,h20,h21,h22]`을 주석으로 명시. 병진 성분이 섞인 합성 행렬에서도 인덱스 0(`h00=cos θ`), 3(`h10=sin θ`)은 순수 회전 성분이므로 번역(translation)에 무관하게 복원된다.

### WR-05: LastFindSucceeded 리셋 (이미 해결됨)
- **D-07:** Phase 6에서 `Action_FAIMeasurement` → `InspectionSequence.TryRunDatumPhase`(`InspectionSequence.cs:144-171`)로 Datum 실행 로직이 리팩터링되면서, 실패 브랜치(line 163)에서 이미 `datum.LastFindSucceeded = false`를 세팅하고 있음. 04-REVIEW.md가 지적한 원본 위치(`Action_FAIMeasurement.cs:96`)의 잔존 `true`는 현재 코드에 존재하지 않는다.
- **D-08:** 추가 방어 코드(belt-and-suspenders) 삽입하지 **않음**. WR-05는 Phase 6 리팩터링으로 구조적으로 해소된 것으로 기록하고 closed 처리.

### 검증 방식
- **D-09:** 각 수정 건에 대해 before/after diff + 근거(04-REVIEW.md 인용)를 VERIFICATION에 기록.
- **D-10:** WR-01/WR-03 수정 후 SIMUL_MODE 런타임 실행으로 Datum 성공 경로와 실패 경로의 로그 + 판정 결과를 확인(기존 UAT 회귀 없음을 점검). 별도 단위 테스트 프로젝트나 UAT 세션은 추가하지 않는다.
- **D-11:** WR-05는 "이미 해결됨" 증거로 `InspectionSequence.cs:163` 현재 코드를 VERIFICATION에 인용.

### 주석 규칙
- **D-12:** 모든 수정 지점에 `//260423 hbk` 주석 필수(사용자 피드백 규칙).

### Claude's Discretion
- 변수명, 로그 메시지의 세부 표현
- VERIFICATION 문서 구성 순서/형식
- Logging에 사용할 `ELogType` 선택(Trace vs Error) — 기존 호출부 관례 따름

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 1차 근거 (수정 요구사항)
- `.planning/phases/04-datum/04-REVIEW.md` §WR-01 (line 40-65), §WR-03 (line 77-95), §WR-05 (line 119-137) — 버그 상세 및 fix 스니펫
- `.planning/ROADMAP.md` §"Phase 10: Datum 정확성 결함 수정" — Success Criteria 3건

### 대상 소스 파일
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (lines 69-75, 157-163) — WR-01 수정 대상
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` (lines 261-283, `IntersectLines`) — WR-01 같은 버그 패턴
- `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` (lines 56-74) — WR-03 수정 대상
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (lines 144-171, `TryRunDatumPhase`) — WR-05 현 상태(이미 해결) 증거

### 참고
- `.planning/REQUIREMENTS.md` §ALG-05 (Datum 정확성 보강)
- `CLAUDE.md` — Halcon operator try/catch 관례, HTuple 사용 패턴

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`VisionAlgorithmService.IntersectLines`** (static) — 이미 존재하는 공통 교점 유틸. WR-01 가드를 이곳에 넣으면 미래 호출자도 보호된다.
- **`Logging.PrintLog((int)ELogType.Trace/Error, ...)`** — 기존 실패 로그 패턴 재사용.

### Established Patterns
- Halcon operator 호출은 모두 `try { ... } catch { return false; }` (CLAUDE.md에 명시). WR-01/WR-03 수정도 이 패턴 유지.
- 수정 주석은 `//YYMMDD hbk` 형식 (feedback memory). Phase 4/6 코드에 이미 적용되어 있음 (`//260413 hbk`, `//260409 hbk`).
- `out` 파라미터 + `bool` 반환으로 성공/실패 전달 — 기존 `Try*` 메서드 관례.

### Integration Points
- `InspectionSequence.TryRunDatumPhase` → `DatumFindingService.TryFindDatum` → WR-01 수정 반영
- `Action_FAIMeasurement.Run` → `FAIEdgeMeasurementService.TryMeasure`(transform 파라미터) → WR-03 수정 반영
- 수정 후 기존 호출자 시그니처 변경 없음 → 회귀 위험 최소.

</code_context>

<specifics>
## Specific Ideas

- 04-REVIEW.md의 WR-01 fix 스니펫과 WR-03 fix 스니펫을 그대로 적용. 사용자가 이미 검토한 제안임.
- `IntersectLines`(VisionAlgorithmService)는 out 파라미터만 있고 error 메시지가 없는 시그니처 — 기존 계약 유지, 내부에서만 가드 추가.

</specifics>

<deferred>
## Deferred Ideas

- **WR-02** (`DatumConfig.CurrentTransform` 스레드 안전성) — Phase 10 범위 밖. Warning level이지만 현재 호출 그래프에서 실제 race는 발생하지 않는 것으로 평가됨. 별도 phase 또는 backlog로.
- **WR-04** (`GetDefaultRunnableAction` 불필요한 루프) — UI 쪽 dead code 정리, Datum 정확성과 무관. backlog.
- **IN-01 ~ IN-04** — 정보 수준 품질 항목. 이번 phase 범위 밖.
- **공통 Intersection 가드 헬퍼 추출** — 지금은 3곳에 인라인 삽입. 향후 4번째 호출이 생기면 공통화 고려.
- **Halcon 연산용 단위 테스트 인프라** — 프로젝트에 테스트 프레임워크 부재. 별도 인프라 phase에서 검토.

</deferred>

---

*Phase: 10-datum-defects*
*Context gathered: 2026-04-23*
