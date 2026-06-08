---
phase: 37-side-multi-datum-dualimage-2026-05-28
type: context
status: ready-to-plan
created: 2026-05-28
absorbs: [CO-36-06, CO-36-07]
fast_track: true
---

# Phase 37 — Side 다중 Datum (4 DualImage / 8-image) 검사 구조

## Goal

Side 검사가 **datum 4개**(각 `VerticalTwoHorizontalDualImage`, 가로+세로 2장 → 총 8장)를 **각자 독립 검출**하고, 각 측정(FAI/Measurement)이 **자기 datum 을 이름으로 참조**하여 보정하며, **측정 이미지는 datum 이미지와 별개**인 구조를 지원한다. 한 datum 실패가 다른 datum 의 측정을 막지 않는다.

## Background (Phase 36 UAT 중 발견 — CO-36-06/07)

Phase 36 시각화 디버깅 중, Side 검사 실제 요구가 현재 구조와 안 맞음이 드러남:
- `TryRunDatumPhase` 가 **1 이미지로 전체 datum 검출** + **하나 실패 시 전체 abort**(`return false`)
- DualImage 판단을 **`DatumConfigs[0]` 하나로** 결정 (`Action_FAIMeasurement.cs:84`, `TryGrabOrLoadDualDatumImages` L329 "단일 Datum 가정")
- → datum 4개 중 현재 이미지에서 안 잡히는 것 때문에 잘 잡힌 datum 의 측정까지 전부 막힘

핵심: **측정별 datum 참조 구조(`MeasurementBase.DatumRef` → `InspectionSequence._datumTransforms[name]` → `TryGetDatumTransform`)는 이미 존재.** 데이터 모델(`DatumConfigs` 리스트 N개, 각 DualImage datum 이 자기 `TeachingImagePath`+`TeachingImagePath_Vertical` 보유)도 거의 완비. **실행 루프만** 다중 datum 독립 처리로 고치면 됨.

## 결정 (D-37-01 ~ D-37-08) — 전부 LOCKED 2026-05-28

| # | 결정 | 내용 |
|---|------|------|
| **D-37-01** 데이터 모델 | 신규 엔티티 불필요. `InspectionSequence.DatumConfigs` 리스트에 datum 4개. 각 DatumConfig = Side datum 1개(VerticalTwoHorizontalDualImage), 자기 가로/세로 2장 경로 보유. |
| **D-37-02** datum별 이미지 로드 | `TryGrabOrLoadDualDatumImages` 를 **datum 인자**를 받게 변경 → 각 datum 이 자기 2장 로드 (`DatumConfigs[0]` 한정 제거). |
| **D-37-03** datum 독립 실행 (lenient) | `TryRunDatumPhase` 에서 datum 실패해도 **abort 안 함**. 성공한 datum 만 `_datumTransforms[name]` 저장. 측정은 자기 `DatumRef` 미해결 시 **그 측정만** 실패/식별. (전부-성공 강제 `return false` 제거) |
| **D-37-04** DualImage 판단 datum별 | `DatumConfigs[0]` 전체판단 제거. loop 안에서 각 datum 의 AlgorithmType 검사 (이미 `InspectionSequence.cs:181` 패턴 존재). |
| **D-37-05** 실행 순서 | datum phase 가 **모든 datum 먼저 검출** → `_datumTransforms` 채움 → `EStep.Measure` 가 `DatumRef` 로 참조. |
| **D-37-06** 측정 ≠ datum 이미지 (사용자 확정) | SIMUL/실측 무관 **datum 검출 이미지와 측정 이미지는 항상 별개.** 측정은 자기 Shot 이미지 사용, datum transform 은 이름 참조. datum 검출 이미지 = SIMUL 시 datum 의 TeachingImagePath/Vertical, 실측 시 datum 전용 grab. |
| **D-37-07** 티칭 UI | 기존 DualImage 티칭 흐름(가로/세로 2장) **재활용** + datum 추가로 4개 생성. 신규 UI 최소화. |
| **D-37-08** 가드 정책 | Phase 34/36 의 "guard 4파일 변경 0" **이 phase 는 해제** — InspectionSequence / Action_FAIMeasurement 구조 변경이 목적. 단 변경 최소·국소화. |

## Scope (실제 코드 변경 — 최소·국소)

1. `InspectionSequence.TryRunDatumPhase` (1-image + 2-image 오버로드) — lenient 전환 (datum 실패 시 continue, 성공만 `_datumTransforms` 저장, 전체는 항상 true 또는 부분성공 반환). datum별 DualImage 판단 유지.
2. `Action_FAIMeasurement`:
   - `TryGrabOrLoadDualDatumImages` → **datum 인자** 받아 per-datum 2장 로드
   - `EStep.DatumPhase` — `DatumConfigs[0]` 분기 제거 → datum loop, 각 datum 의 이미지 로드 후 검출 (mixed algorithm 허용)
   - 호출부가 `TryRunDatumPhase` 실패로 전체 action abort 하지 않게 (lenient)
3. UI — datum 4개 생성/티칭 흐름 확인·보완 (대부분 기존 DualImage 재활용)
4. 측정 이미지 분리 동작 검증 (이미 구조 존재 — `DatumRef`/`_datumTransforms`)

## Deferred (Phase 37 외)

- LineToLineAngle / PC2 분리 / Side Fixture INI → **Phase 27** (별도)
- `Line1_SourceShotName`/`Line2_SourceShotName` 모델(한 datum 의 두 라인이 다른 이미지) → Phase 27 의 기존 scope (이번과 형제, 다른 케이스)
- CO-36-01 수직도 사용자 필드화 → 별도/이번에 포함 검토
- CO-36-05 Phase 36 잔여 시각 UAT

## 빠른 구현 전략 (사용자 요청)

- research 스킵 (이번 세션 코드 조사 완료)
- 기존 인프라 최대 재활용 — 신규 데이터 모델 0
- 변경 = 실행 루프 3곳(TryRunDatumPhase / TryGrabOrLoadDualDatumImages / EStep.DatumPhase) + UI datum 추가 흐름 + SIMUL UAT

## 다음

`/gsd-plan-phase 37`
