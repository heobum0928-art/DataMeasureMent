# Phase 52: 이미지 수평 보정 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-17
**Phase:** 52-datum-2026-06-16-poc-1
**Areas discussed:** 레벨링 기준 Datum/에지, 적용 시점(WHEN), 적용 범위(SCOPE), on/off 토글+영속

---

## 레벨링 기준 Datum/에지

| Option | Description | Selected |
|--------|-------------|----------|
| 시퀀스 수평 기준 Datum 라인 재사용 | 기존 DatumFindingService 수평 피팅 재사용, 신규 ROI 0 | ✓ (1차) |
| 레벨링 전용 ROI 신설 | Datum 독립 전용 2-ROI 티칭 | |
| 기준 Datum 명시 지정 | 레시피 플래그로 지정 | ✓ (다중 Datum 후속에서 확정) |

**다중 Datum 후속 질문:**
| Option | Description | Selected |
|--------|-------------|----------|
| 레시피에서 기준 Datum 1개 명시 지정 | 결정적·정확, 잘못된 Datum 레벨링 방지 | ✓ |
| 첫 번째 수평 Datum 자동 | 설정 0, 순서 의존 | |
| 전 수평 Datum 평균 각도 | 결측·이상치 민감 | |

**User's choice:** 시퀀스 수평 기준 Datum 라인을 재사용하되, 시퀀스에 수평 Datum 이 여러 개면 레시피에서 기준 1개를 명시 지정.

---

## 적용 시점 (WHEN)

| Option | Description | Selected |
|--------|-------------|----------|
| grab 직후 전처리 회전 | 각도 산출→이미지 실제 회전→회전 이미지로 검사 전체 | ✓ |
| Datum 검출 후 좌표만 보정 | 이미지 무회전, 측정 좌표 변환만 | |

**User's choice:** grab 직후 전처리 회전.

---

## 적용 범위 (SCOPE)

| Option | Description | Selected |
|--------|-------------|----------|
| 시퀀스당 1회 → 전 SHOT 공유 | 카메라/지그 고정 기울기 공통 전제 | ✓ |
| SHOT마다 개별 산출 | SHOT별 이미지 기울기 다를 때 | |

**User's choice:** 시퀀스당 1회 → 전 SHOT 공유.

---

## on/off 토글 + 영속

| Option | Description | Selected |
|--------|-------------|----------|
| 시퀀스 단위 토글 + 레시피 저장, 기본 off | 시퀀스별 on/off, INI 영속, 회귀 0 | ✓ |
| Shot 단위 토글 | SHOT별 on/off | |
| 전역 설정 토글 | Setting 창 전체 on/off | |

**User's choice:** 시퀀스 단위 토글 + 레시피 저장, 기본 off.

## Claude's Discretion
- 회전 구현/중심/경계 처리, 사전 검출 패스 방식, angle_lx 부호·방향 규약.

## Deferred Ideas
- 픽셀 캘리브(Phase 53), SHOT별 레벨링, 전 Datum 평균, 수직/2축 보정.
