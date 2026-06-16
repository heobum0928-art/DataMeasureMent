# Phase 51: 시퀀스 일괄 검사 & 일괄 Export - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-16
**Phase:** 51-시퀀스 일괄 검사 & 일괄 Export
**Areas discussed:** 실행 트리거 & 대상 범위, SIMUL vs 실모드 동작, Export 시점/방식, 엑셀 레이아웃

---

## 실행 트리거 & 대상 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 Start 버튼 = Sequence 노드 선택 시 전 SHOT | 시퀀스 노드 1개 선택으로 전부 실행 | |
| SHOT 다중 선택 → 선택 SHOT만 실행 | 트리에서 SHOT 복수 선택, 전부=전부/2개=2개 | ✓ |
| 별도 "일괄 검사" 버튼 신규 | 전용 버튼 | |

**User's choice:** SHOT 다중 선택 방식 — "Shot을 선택해서 하는게 낫겠어 전부 선택하면 전부 하는거고 2개만 할꺼면 2개 선택하는 방식으로"
**Notes:** 다중 선택 범위는 한 시퀀스 내에서만 (Top끼리/Bottom끼리). Top+Bottom 교차 선택 불가.

| Option (선택 범위) | Description | Selected |
|--------|-------------|----------|
| 한 시퀀스 내에서만 | Top끼리/Bottom끼리 | ✓ |
| Top+Bottom 섞어서 | 시퀀스별 순차 실행 후 합쳐 Export | |

---

## SIMUL vs 실모드 동작

**User's choice:** SIMUL = 일괄 실행(각 SHOT SimulImagePath 1회씩). 실모드 = 검사할 때마다 결과+엑셀에 채움(append).
**Notes:** "Simul 모드는 일괄실행 맞고, 실제 모드는 검사 할때마다 결과에 채우고 엑셀 데이터도 채우는 방식이지"

---

## Export 시점/방식

**User's choice:** 수동 모드 = 결과만 누적 후 수동 Export 버튼. Gage R&R 모드 = N회 반복 매회 채움 + 자동 저장(실모드).
**Notes:** Gage R&R(반복+자동저장)은 Phase 41.1 범위로 분리. Phase 51 = 수동 모드만 구현, 누적/Export 경로는 Gage R&R 재사용 가능하게 설계.

---

## 엑셀 레이아웃

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 포맷 그대로 | 전 SHOT 한 시트 행, SHOT 컬럼 구분 (Phase 40 ExcelExportService) | ✓ |
| SHOT별 시트 분리 | SHOT마다 탭 | |
| 기타 | — | |

**User's choice:** 기존 포맷 그대로
**Notes:** 처음 "이해 못함" → 쉬운 설명 후 기존 포맷 재사용 선택.

## Claude's Discretion

- 다중 선택 UI 방식 (체크박스 vs Ctrl/Shift 선택)
- 선택 SHOT만 실행하는 메커니즘 (StartAll 확장 vs 선택 인덱스 전달)
- 수동 Export 버튼 위치 (ReviewerWindow 재사용 vs InspectionListView 신규)

## Deferred Ideas

- Gage R&R N회 반복 + 자동저장 → Phase 41.1
- Top+Bottom 교차 다중 선택 → 보류
