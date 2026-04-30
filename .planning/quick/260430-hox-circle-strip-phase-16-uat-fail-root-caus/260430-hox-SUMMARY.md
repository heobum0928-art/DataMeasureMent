---
quick_id: 260430-hox
description: Circle strip 사각형 크기 대폭 축소 — Phase 16 UAT FAIL root cause
date: 2026-04-30
status: complete
commit: 7ca39b6
mode: quick
---

# Quick 260430-hox SUMMARY

## 변경 사항 (3 파일, +12 / -6)

| 파일 | 변경 |
|------|------|
| WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs | `Circle_RectL1Ratio` / `Circle_RectL2Ratio` default `0.05` → `0.02` |
| WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (TryFindCircleByPolarSampling) | `halfL1` / `halfL2` = `Math.Min(radius * ratio, 12.0)` cap 추가 |
| WPF_Example/Halcon/Display/HalconDisplayService.cs (RenderCircleStripOverlay) | `length1` / `length2` = `Math.Min(radius * ratio, 12.0)` cap 추가 (viz/algo 식 일관성) |

모든 변경 라인에 `//260430 hbk Quick 260430-hox` 주석 (총 5건).

## Phase 16 D-22 부분 완화 사유

원래 D-22: VisionAlgorithmService.cs / DatumFindingService.cs / DatumConfig.cs 변경 금지 (Phase 16 plan 변경 회피).
완화 사유: Phase 16 UAT 에서 발견된 root cause 가 정확히 이 3 파일 안에 있음 (strip 크기 default + 계산식). Plan 변경이 아니라 **Phase 16 결함 수정** 으로 분류 → 사용자 승인 후 진행.

## 검증

- Build: msbuild Debug/x64 **PASS**, 신규 warning **0건** (기존 2건은 pre-existing)
- 사용자 수동 UAT 재실행 대기 — 16-UAT.md Test 1/3/5 재검증 필요

## 영향 범위

- Phase 16 UAT Test 1 (pre-teach strip viz): strip 사각형 max 24×24px 로 cap → 거대화 해결 기대
- Phase 16 UAT Test 3 (post-teach 검출 원): strip 노이즈 감소 → 검출 원/center cross 가시성 개선 기대
- Phase 16 UAT Test 5 (`insufficient polar samples (1)`): strip 적정 크기 → MeasurePos edge 노이즈 감소 → polar sample 검출 성공 기대

## 범위 밖 (Phase 17 carry-over 유지)

- 시각화 정책 재설계 (N개 동시 표시 → 1개 strip 만 표시)
- PropertyGrid UI 추가 (각도 step UI control, edge 검출 방향 UI control)
- teach trigger UX 명확화 (자동/수동 안내)

## Next

`/gsd-verify-work 16` 재개 → Test 1/3/5 재실행
