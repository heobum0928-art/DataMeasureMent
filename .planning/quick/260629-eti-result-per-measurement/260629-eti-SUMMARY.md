---
phase: quick-260629-eti
plan: 01
subsystem: InspectionSequence / TCP 결과 직렬화
tags: [tcp-result, measurement-unit, fai, wire-format]
dependency_graph:
  requires: [Phase 49 AddFaiResult 기반, FAIConfig.Measurements, MeasurementBase]
  provides: [측정 단위 TCP 결과 전송, ClassifyMeasurement 헬퍼]
  affects: [$RESULT 와이어 항목 수 변경 — 다측정 FAI는 측정 개수만큼 항목 증가]
tech_stack:
  added: []
  patterns: [측정 단위 foreach 순회, 3-state 분류 헬퍼 분리]
key_files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
decisions:
  - "AddFaiResult: FAI 단위 1항목 → 측정 단위 N항목 (B안 채택)"
  - "다측정 id 네이밍: FAIName_P{1-based} (엑셀 예시 정합)"
  - "ClassifyFai 보존 (호출처 없어도 회귀 위험 회피)"
metrics:
  duration: "~10분"
  completed_date: "2026-06-29"
---

# Quick 260629-eti: $RESULT 측정 단위 전환 Summary

**One-liner:** AddFaiResult를 측정 단위 순회로 재작성해 다측정 FAI의 P2 등 불량값 은폐를 제거하고 ClassifyMeasurement 헬퍼를 신규 추가.

## What Changed

### AddFaiResult 재작성 (InspectionSequence.cs)

**이전:** FAI당 항목 1개 — `fai.MeasuredValue`(=Measurements[0]) + `ClassifyFai(fai)` 1건 전송.
P2(두 번째 측정) 불량값이 와이어에서 사라지는 데이터 은폐 결함 존재.

**이후:** `fai.Measurements` 순회 → 측정마다 `FAIResultData` 1건 추가.
- 단측정 FAI (Measurements.Count == 1): id = FAIName (suffix 없음)
- 다측정 FAI (Measurements.Count >= 2): id = FAIName_P1, FAIName_P2, ...
- 0측정 FAI (Datum 샷): 루프 0회 = 항목 0개 (기존과 동일)
- val = meas.LastMeasuredValue (fai.MeasuredValue 더 이상 사용 안 함)

### ClassifyMeasurement 헬퍼 신규 (ClassifyFai 바로 아래 배치)

측정 단위 3-state 분류:
- `LastSkipReason == "DATUM_FAIL"` 또는 `"ALIGN_FAIL"` → `NotExist` + `m_bCycleHasNG = true`
- `!LastJudgement` (skip 아님) → `NG` + `m_bCycleHasNG = true`
- 그 외 → `OK`

## Commits

| Hash | Message |
|------|---------|
| 1eae9ed | feat(260629-eti): AddFaiResult 측정 단위 전환 + ClassifyMeasurement 신규 |

## Verification

- **빌드:** Debug/x64 `Build succeeded` — error 0 (warning 1건: MinimumRecommendedRules.ruleset pre-existing, 무관)
- **ClassifyFai 보존:** line 631 존재 확인
- **ApplyCycleJudgement 무변경:** 확인
- **m_bCycleHasNG / m_bCycleDatumFailed 누적 로직 무변경:** 확인
- **VisionResponsePacket.cs 무변경:** BuildResultMessageV1 / BuildFaiItemsV1 / MapCycleJudgement / MapFaiJudgement 전부 무변경
- **FAIConfig.MeasuredValue · IsPass 필드 보존:** 확인 (TCP 경로에서 읽지 않을 뿐, 필드 삭제 안 함)
- **AddFaiResult val 소스:** meas.LastMeasuredValue 사용 확인 (line 606)

## Wire-Item-Count 동작 변화

| FAI 유형 | 이전 항목 수 | 이후 항목 수 |
|---------|------------|------------|
| 단측정 FAI (Measurements=1) | 1 | 1 (id=FAIName) |
| 다측정 FAI (Measurements=2) | 1 | 2 (id=FAIName_P1, FAIName_P2) |
| Datum 샷 (Measurements=0) | 0 | 0 |

## 사이클 종합 P/F/B 동등성

측정 단위 순회로 바꿔도 "측정 1건이라도 skip/NG 이면 m_bCycleHasNG=true" 가 되어,
마지막 Index 종합 판정 F는 변경 전 ClassifyFai(FAI 단위 집계) 와 동일한 결과를 낸다.

## Deviations from Plan

없음 — 플랜 대로 정확히 실행됨.

## Known Stubs

없음.

## Threat Flags

없음 (TCP 직렬화 로직 내부 변경만, 신규 네트워크 경로/엔드포인트 없음).

## Self-Check: PASSED

- InspectionSequence.cs 수정 확인: 존재
- commit 1eae9ed 존재: 확인
- ClassifyMeasurement 메서드 존재: 확인
- ClassifyFai 보존: 확인
- BuildFaiItemsV1 / MapFaiJudgement 무변경: 확인
