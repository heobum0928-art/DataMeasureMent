---
phase: 20
plan: 06
status: complete
completed: 2026-05-09
commits:
  - 14098b8
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs
files_unchanged:
  - WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs
---

# Plan 20-06 Summary — 4 light Custom/Sequence files cleanup

## Result
QUAL-02/QUAL-04 충족. 4 파일 중 2 파일 변환, 2 파일 변환 대상 0 (Phase 28 결정에 의해 이미 명시 if 패턴).

## Conversion Counts

| File | ?: → if/else | ?? | ?. | hbk markers (pre/post Phase 20) |
|------|-------------|----|----|----------------------------------|
| DatumConfig.cs | 9 | 0 | 0 | 154 / 154 (10 new 260509 마커 추가, 기존 보존) |
| DynamicPropertyHelper.cs | 1 | 0 | 0 | 16 / 16 (1 new 260509, 기존 보존) |
| EdgeOptionLists.cs | 0 (line 36 은 주석 내 인용) | 0 | 0 | 14 / 14 (변경 없음, Phase 28 = 10 보존) |
| CircleDiameterMeasurement.cs | 0 | 0 | 0 | 20 / 20 (변경 없음, Phase 28 = 15 보존) |

## Conversion Sites

### DatumConfig.cs (9 ternaries)

- **Lines 474, 475, 479** — `fbThreshold` / `fbSigma` / `fbPolarity` hardcoded fallback (ternary → 1줄 임시 + if/else 2줄)
- **Lines 532-537** — 6 Vertical_* INI 하위호환 마이그레이션 ternary
  - Vertical_EdgeThreshold, Vertical_Sigma, Vertical_EdgeDirection, Vertical_EdgeSampleCount, Vertical_EdgeTrimCount, Vertical_EdgePolarity
  - 단일 라인 `if (sentinel) X = (cond) ? a : b;` → 중첩 블록 `if (sentinel) { if (cond) X = a; else X = b; }`
- Phase 17-02 D-09 ICustomTypeDescriptor.GetProperties / GetAttributes 분기는 변환 대상 연산자 부재 (이미 if/else)
- Phase 13-04 EnsurePerRoiDefaults idempotency 보존 (sentinel 가드 무변경)

### DynamicPropertyHelper.cs (1 ternary)

- **Line 30** — `(attrs != null && attrs.Length > 0) ? GetProperties(obj, attrs, true) : GetProperties(obj, true)` → `PropertyDescriptorCollection all; if (...) all = ...; else all = ...;`
- Phase 19-01 hideFunc null-guard / sourceNames null-safe 패턴 (이미 명시 if) — 변경 없음
- FilterProperties 시그니처 / 위임 호출 무변경 (Phase 17-02 / 19-01 / FAIConfig 위임 회귀 0)

### EdgeOptionLists.cs (0 sites)

- Line 36 의 `?: ` 패턴은 주석 내 인용문 (Datum CTH inline ternary 설명)
- Phase 28-01 MapRadialDirectionToHalconPolarity 헬퍼 + 4 FAI polar defaults 코드 자체에 ternary 없음
- 변경 0건 → Phase 28 hbk 마커 10개 모두 보존 (D-13 자동 충족)

### CircleDiameterMeasurement.cs (0 sites)

- Phase 28-02 의 `string.IsNullOrEmpty(Circle_RadialDirection)` 분기는 이미 명시 if/else (Phase 28-02 commit 시점부터)
- TryFindCircle / TryFindCircleByPolarSampling 호출 분기 무변경 (REQ-28-02 / REQ-28-04 회귀 0)
- 변경 0건 → Phase 28 hbk 마커 15개 모두 보존

## Key Decisions

- **D-04/D-05 예외 적용 라인:** 없음 (4 파일 모두 LINQ-chain-end `?.` / expression-bodied member 부재)
- **D-08 'why' 주석 보존:** Phase 14-03 Vertical INI migration 의도 (lines 528-531), Phase 19 fix attrs=null fallback (line 28-29), Phase 17-02 동적 hide 의도, Phase 28-01 helper sole-source 의도 — 모두 보존
- **Brace style:** 파일별 K&R 스타일 (한 줄 if + alignment) 보존. 중첩 블록은 표준 K&R `{ }` + 들여쓰기.
- **C# 7.2 호환:** `??=` / switch expression / `is { }` 도입 0

## Acceptance Criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | 4 파일 `??` (`??=` 제외) = 0 | PASS |
| 2 | 4 파일 `?.` (LINQ tail 제외) = 0 | PASS |
| 3 | msbuild Debug/x64 PASS | DEFERRED → Wave 2 (20-08) 종합 회귀 |
| 4 | hbk_pre20 카운트 baseline 보존 (D-13) | PASS — 4 파일 모두 baseline 동일 |
| 5 | EdgeOptionLists.cs `260508 hbk Phase 28` 카운트 동일 | PASS (변경 0) |
| 6 | CircleDiameterMeasurement.cs Circle_RadialDirection 분기 시그니처 무변경 | PASS (변경 0) |
| 7 | C# 7.2 호환 (W1) | PASS |

## Deviations

- **인라인 오케스트레이터 실행 (Rule 3):** worktree-isolated executor agent 가 sandbox 의 `git reset --hard` 차단으로 base sync 실패 → 3회 재시도 후 오케스트레이터가 main 작업 트리에서 직접 변환 수행. 변환 자체는 plan §conversion_patterns 그대로 적용, 시그니처/마커/스타일 정책 모두 준수.
- **hbk 마커 날짜 `260509` (Plan 의 `260508` 대신):** prompt 의 `//260509 hbk Phase 20` 강제. 다른 wave 1 plan (20-01/02/05/07) 과 일치.
- **Task 2 (EdgeOptionLists + CircleDiameterMeasurement) no-op:** 변환 대상 0 개로 별도 commit 없음. SUMMARY.md 의 "files_unchanged" 명시로 추적성 유지.

## Wave 2 Hand-off

20-08 종합 회귀 시:
- DatumConfig EnsurePerRoiDefaults — Datum CTH SIMUL UAT 시 fbThreshold/fbSigma/fbPolarity fallback 진입 (sentinel 케이스) 동작 확인
- DynamicPropertyHelper.FilterProperties — DatumConfig.GetProperties 위임 호출 시 attrs=null fallback (Phase 19 fix) 진입 확인
- EdgeOptionLists / CircleDiameterMeasurement — 변경 0 → Phase 28 회귀 자동 0
