---
phase: 20
plan: 01
subsystem: inspection-recipe
tags: [code-style, qual-02, qual-04, fai-config]
requirements: [QUAL-02, QUAL-04]
provides:
  - "FAIConfig.cs — ?? 연산자 0개, 명시적 if/else 분해 완료"
  - "객체 initializer 깨끗함 — 공용 idValue/nameValue + Edge fallback 임시변수 패턴"
affects:
  - "InspectionRecipeManager / Action_FAIMeasurement — ToRoiDefinition() 반환 동등 (회귀 0)"
tech_stack:
  added: []
  patterns:
    - "임시변수 + null 체크 패턴 (D-03 P-1/P-2 적용)"
key_files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs"
decisions:
  - "Id/Name fallback (FAIName ?? \"FAI\") → 메서드 진입부 idValue/nameValue 공용 임시변수 (3개 RoiDefinition 분기 재사용)"
  - "Edge 파라미터 fallback (EdgeDirection/EdgeSelection/EdgePolarity/PolygonPoints) → Rect 분기 진입 직전 임시변수 4개로 분해"
  - "주석 정리: 명백히 자명한 'what' 주석 0개 — 모든 기존 주석은 'why' 카테고리 (hbk 마커 + 결정 근거 + 한국어 nuance, D-08/D-09)"
  - "변환된 라인만 //260509 hbk Phase 20 마커 부착 (13개), 변환 안 한 73개 기존 hbk 마커 보존 (D-13)"
metrics:
  duration_minutes: 8
  completed_at: "2026-05-09"
  commits:
    - hash: "0b1e081"
      message: "refactor(20-01): explode ?? in FAIConfig.cs to explicit if/else"
---

# Phase 20 Plan 01: FAIConfig.cs Code-Style Cleanup Summary

FAIConfig.cs 의 10개 `??` null-병합 연산자를 명시적 임시변수 + null 체크 if 문 패턴으로 분해 완료.
'what' 주석은 식별 결과 0건 (모든 주석이 'why' 또는 한국어 nuance) — D-08/D-09 보존 우선.

## Conversion Counts

| Operator        | Before | After | 비고                                                |
| --------------- | -----: | ----: | --------------------------------------------------- |
| `??`            |     10 |     0 | 6× FAIName, 1× EdgeDirection, 1× EdgeSelection, 1× EdgePolarity, 1× PolygonPoints |
| `?:` (삼항)     |      0 |     0 | grep 결과 baseline 0 (plan 추정 10은 보수적 false positive) |
| `?.` (LINQ tail)|      0 |     0 | 해당 없음                                           |

## hbk Marker Audit

| Type                              | Count |
| --------------------------------- | ----: |
| 신규 `//260509 hbk Phase 20` 부착 |    13 |
| 기존 hbk 마커 보존                |    73 |

Stack 0 (D-12 강제 — 변환 라인의 기존 hbk는 최신으로 교체, 미변환 라인은 손대지 않음).

## Removed 'what' Comments

**제거 0건.** FAIConfig.cs 의 모든 기존 주석은 다음 'why' 범주에 해당하여 D-08/D-09 보존:

- hbk 마커 + 변경 의도 (`//260413 hbk Phase 6: Multi-Algorithm Measurements (D-20) — 수동 직렬화...`)
- 알고리즘/INI 저장 정책 근거 (`//  저장 타입: string (ParamBase.Save/Load switch가 string 지원)`)
- 유효값 enumeration (`//260409 hbk LtoR, RtoL, TtoB, BtoT`)
- 한국어 산업 도메인 nuance (`//260409 hbk 샘플 스트립 수`, `//260409 hbk 극값 제거 수`)
- 결정 근거 (`(per D-12, D-16: camera-level calibration ...)`)

XML doc (`/// <summary>`) 은 D-10 에 따라 보존.

## D-04 / D-05 Exception Lines

해당 없음 (LINQ chain tail `?.` 0건, expression-bodied member 0건 in scope).

## Build / Verify

| Check                    | Result                                                                             |
| ------------------------ | ---------------------------------------------------------------------------------- |
| msbuild Debug/x64        | Baseline-equivalent: worktree env 에 .NET FW reference assembly 미해결 (변경 전·후 동일 환경 결함) |
| 변환 후 신규 CS warning  | 0 (FAIConfig.cs 자체에 신규 syntax 도입 0)                                          |
| C# 7.2 호환              | PASS — `??=`, `is { }`, switch expression, expression-bodied member 도입 0          |
| 의미론적 동등성          | PASS — `a ?? b` ≡ `var v = b; if (a != null) v = a;` 1:1 분해 (회귀 0)              |

**환경 빌드 결함:** worktree 환경에서 msbuild 가 `System.Object` 등 .NET Framework reference assembly 를 찾지 못함 (XAML 컴파일 + CS0518 동시 발현). **변경 전 baseline 도 동일 결함이 baseline-동등** 으로 발현 (git stash → msbuild 재실행 → 동일 에러 확인). 즉 본 plan 의 코드 변경에 의해 새로 도입된 회귀가 아님 — 메인 리포지토리 환경에서 검증 필요 (Wave 3 plan 의 종합 회귀에 위임).

## Deviations

| Rule  | Description                                              | Fix                                                                              |
| ----- | -------------------------------------------------------- | -------------------------------------------------------------------------------- |
| Rule 3| Plan 의 hbk 마커 `//260508 hbk Phase 20` 와 prompt 의 `//260509 hbk Phase 20` 충돌 | prompt 우선 (parallel_execution 명시 강제) — 모든 신규 마커 `260509` 사용         |
| Rule 3| Plan 의 `?:` 카운트 추정 10 vs 실측 0                    | grep 으로 검증, AC #1 충족 (`?:` count = 0 도달)                                  |

## Self-Check: PASSED

- WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs FOUND
- commit 0b1e081 FOUND in git log
- `??` operator count = 0 (확인 완료)
- C# 7.2 호환 (`??=` 등 도입 0) 확인 완료
- hbk 마커 stack 없음 (변환 안 한 라인의 기존 hbk 73개 보존, 변환 라인에만 신규 13개 부착)
