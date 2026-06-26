---
quick_id: 260626-dbd
slug: computeprojectiondistance-signed-a-01-5
status: complete
date: 2026-06-26
commits:
  - d0eedc9
---

# Quick 260626-dbd — ComputeProjectionDistance signed화 (감사 A-01)

## 배경
감사(quick-260625-lo5 후속) 확정 A-01. 사용자 지시: "타입별 의도부터 점검하고 진행."

## 타입별 의도 점검 (진행 전 수행)
5개 위임 타입 모두 **단일 점 → datum 기준선 투영** 구조 + `MeasureAxis`(Y=수평선/X=수직선) 부호 규약
= EdgeToLineDistance 와 동일 패턴. `ComputeProjectionDistance` XML doc(643-646)이 이미
"부호 있는 거리, measureAxis=Y +위/X +오른쪽" 명시 → 본문 unsigned 가 계약 위반. **signed 가 설계 의도 확정.**

| 타입 | 측정점 | MeasureAxis 기본 | 전달 각도 |
|---|---|---|---|
| ArcEdgeDistance | 에지선 중점 | X | X→DatumAngle2Rad / Y→DatumAngleRad |
| CircleCenterDistance | 원중심 | Y | 〃 |
| CompoundCenterB | LargestRect 중심 | Y | 〃 |
| CompoundCenterC | LargestRect 중심 | (B 동일) | 〃 |
| ArcLineIntersect | 교점 | X | 〃 |

## 수정
`WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` ComputeProjectionDistance(foot 오버로드, ~676-705):
- Math.Sqrt 절대값 → signed 투영성분 (EdgeToLineDistanceMeasurement.cs:160-216 동일 공식).
- 축별 부호 정규화: X(수직축) sinθ≥0, Y(수평축) cosθ≥0 → 틸트 방향 무관 부호 안정.
- X: `drr*dirC - dcc*dirR` (+오른쪽), Y: `drr*(-dirC) + dcc*dirR` (+위, D-02).
- foot/overlay 무영향(직선 불변), ProjectionPl 실패 시 0.0 유지.
- 한 함수로 5개 타입 동시 해결. EdgeToLineDistance 는 이 함수 미사용 → 무영향.

## 검증
- ✅ MSBuild Debug/x64 빌드 PASS (DatumMeasurement.exe 생성, 기존 경고만).
- ⏳ 실측 UAT 사용자 수행 필요 (아래).

## ⚠️ UAT (사용자 — 중요)
출력이 unsigned→signed 로 **변함**. datum 한쪽 고정·소변형이라 부호는 **안정**(틸트로 안 흔들림)하지만:
1. 5개 타입 항목(ArcEdgeDistance/CircleCenterDistance/ArcLineIntersect/CompoundCenterB·C)을 검사해
   측정값 **부호 vs NominalValue** 확인.
2. 측정점이 규약상 −쪽인 항목은 값이 **음수로 뒤집힘** → 양수 nominal 이면 NG.
   → 그 항목만 **InvertSign=True** 토글(또는 nominal 부호) — 1회 설정으로 영구 안정.
3. 크기(magnitude)는 기존과 동일(틸트보정 포함). measureY·EdgeToLineDistance·타 측정 회귀 0 확인.

## 잔여 (Phase 999.2 배치)
D-01/E-07/C-06/B-02 (LOW) 미수행 — 현 운영 미발현. ROADMAP Phase 999.2 유지.
