# Phase 10: Datum 정확성 결함 수정 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-23
**Phase:** 10-datum-defects
**Areas discussed:** WR-01 범위, WR-05 처리, 검증 방식

---

## WR-01 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 3곳 모두 수정 (Recommended) | DatumFindingService.cs:75, :163 + VisionAlgorithmService.cs:274. 같은 버그 패턴이 3곳에 있어 일괄 수정. | ✓ |
| 리뷰 명시 2곳만 | DatumFindingService.cs:75, :163만 수정. VisionAlgorithmService.IntersectLines는 별도 phase로 남김. | |
| 공통 헬퍼로 추출 | IntersectLines 가드를 VisionAlgorithmService의 공통 헬퍼로 만들고 DatumFindingService에서 호출. | |

**User's choice:** 3곳 모두 수정 (Recommended)
**Notes:** 버그 패턴이 동일하므로 scope 범위 안에서 일괄 수정하는 것이 합리적.

---

## WR-05 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 이미 해결됨으로 기록 후 닫음 (Recommended) | VERIFICATION에서 현재 코드가 이미 조건을 만족함을 증거로 문서화하고 WR-05는 closed 처리. 추가 코드 수정 없음. | ✓ |
| 방어적으로 추가 리셋 삽입 | Action_FAIMeasurement.cs Datum 실패 분기에도 LastFindSucceeded=false를 명시적으로 세팅(이중 안전). | |

**User's choice:** 이미 해결됨으로 기록 후 닫음 (Recommended)
**Notes:** Phase 6 리팩터링으로 `InspectionSequence.TryRunDatumPhase` line 163에서 이미 실패 시 `LastFindSucceeded=false`를 세팅. 문서화만으로 충분.

---

## 검증

| Option | Description | Selected |
|--------|-------------|----------|
| 코드 리뷰 + 런타임 로그 (Recommended) | 세 수정의 before/after diff를 VERIFICATION에 기록. 런타임 실행 시 Datum 실패·성공 경로 로그로 확인. 기존 UAT 회귀 없음만 점검. | ✓ |
| 합성 hom_mat2d 유닛 테스트 추가 | WR-03 검증을 위해 합성 translate+rotate 행렬로 회전각 추출 정확성을 확인하는 미니 테스트 스크립트/하니스 추가. | |
| 수동 UAT 세션만 | 실제 기기 없이 SIMUL_MODE로 Datum 수정 전/후 측정값 비교를 사람이 확인. | |

**User's choice:** 코드 리뷰 + 런타임 로그 (Recommended)
**Notes:** 프로젝트에 단위 테스트 프레임워크 부재 → 신규 테스트 인프라 도입은 scope creep. SIMUL_MODE 런타임 로그로 충분히 검증 가능.

---

## Claude's Discretion

- 수정 지점의 변수명·로그 메시지 세부 표현
- VERIFICATION 문서 구성 순서/형식
- `ELogType` 선택 (Trace vs Error) — 기존 호출부 관례 따름

## Deferred Ideas

- WR-02 (CurrentTransform 스레드 안전성) — 별도 phase/backlog
- WR-04 (GetDefaultRunnableAction dead code) — backlog
- IN-01~IN-04 (품질 info 항목) — 본 phase 범위 밖
- 공통 Intersection 가드 헬퍼 추출 — 4번째 호출 발생 시 재고려
- Halcon 연산용 단위 테스트 인프라 — 별도 인프라 phase
