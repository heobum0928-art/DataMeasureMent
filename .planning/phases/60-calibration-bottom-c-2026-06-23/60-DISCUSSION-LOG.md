# Phase 60: Calibration — Bottom (C) - Discussion Log

> Audit trail only. Decisions in CONTEXT.md.

**Date:** 2026-06-24
**Mode:** `--auto` (사용자 외부). Claude 권장 설계 + 근거 문서화.

## Auto-selected decisions
| 결정 | 선택 | 근거 |
|---|---|---|
| 범위 | AV-05(피커센터)만, **AV-06 각도캘 폐기** | 사용자 "Calibration 하나로만" — 2-패턴 angle_lx 가 각도 충족 |
| 서비스 (D-01) | 신규 `PickerCenterCalibrationService` 상태형 누적(Reset/TryAddStep/TryComputePickerCenter), handler 소유 | 외부 36-스텝 트리거에 맞는 누적형 |
| 스텝 중심 (D-02) | phase 59 2-패턴 midpoint(`AlignShapeMatchService.TryFindCenter` 추가) | 이형부품 회전 안정점 |
| 원 피팅 (D-03) | `FitCircleContourXld`("atukey") + GenContourPolygonXld | 검증된 강건 최소자승 재사용 |
| 저장 (D-04) | SystemSetting [ETHERNET_VISION] PickerCenterRow/Col | Phase 58 INI 패턴, 머신 캘 |
| 적용 (D-05) | 피커센터 기준 강체 변환, 규약 UAT 확정 | SC-3 반영, 컨트롤러 규약 의존 |
| UAT | 검증 직전 정지(실 피커+36 이미지 필요, Phase 61 후) | Phase 58/59 동일 |

## 핵심 발견
- `FitCircleContourXld`("atukey") 즉시 재사용. NewDDA 엔 편심원 캘 없음(신규). MathNet 있으나 HALCON 우선.

## Deferred
- Phase 61 UI / Phase 62 TCP / AV-06 폐기(재도입 안 함).
