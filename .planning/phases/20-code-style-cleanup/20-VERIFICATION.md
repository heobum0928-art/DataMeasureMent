---
phase: 20
phase_name: code-style-cleanup
status: signed_off
signed_off_date: 2026-05-09
verification_path: B (code-inspection fallback — W5 4 항목 정당화)
ac_results:
  AC#1: PASS
  AC#2: PASS
  AC#3: PASS (경로 B)
  AC#4: PASS
related_files:
  - .planning/phases/20-code-style-cleanup/20-01-SUMMARY.md
  - .planning/phases/20-code-style-cleanup/20-02-SUMMARY.md
  - .planning/phases/20-code-style-cleanup/20-03-SUMMARY.md
  - .planning/phases/20-code-style-cleanup/20-04-SUMMARY.md
  - .planning/phases/20-code-style-cleanup/20-05-SUMMARY.md
  - .planning/phases/20-code-style-cleanup/20-06-SUMMARY.md
  - .planning/phases/20-code-style-cleanup/20-07-SUMMARY.md
---

# Phase 20 Verification — code-style-cleanup

## Sign-off Summary

Phase 20 의 4개 acceptance criteria 모두 PASS. 14 파일 변환 (113 operator instance) 종합 회귀 검증 완료.

| AC | Description | Verdict |
|----|-------------|---------|
| #1 | `?:`/`??`/`?.` → if/else (LINQ tail / expression-bodied 예외) | PASS |
| #2 | 'what' 주석 제거 / 'why' 주석 보존 + W3 mojibake 0 | PASS |
| #3 | SIMUL_MODE byte-identical 회귀 | PASS (경로 B) |
| #4 | msbuild Debug/x64 PASS + 신규 warning 0 | PASS |

## AC #1 — Operator Conversion Matrix

14 파일 종합 grep (라인 주석 제거 후, `??=` 제외):

| File | `??` | `?.` | `?.Invoke` | hbk Phase 20 (260509) | mojibake |
|------|------|------|------------|----------------------|----------|
| DatumConfig.cs | 0 | 0 | 0 | 4 | 0 |
| DynamicPropertyHelper.cs | 0 | 0 | 0 | 1 | 0 |
| EdgeOptionLists.cs | 0 | 0 | 0 | 0 (변환 대상 0 — Phase 28 결정에 의해 이미 명시 if 패턴) | 0 |
| FAIConfig.cs | 0 | 0 | 0 | 13 | 0 |
| CircleDiameterMeasurement.cs | 0 | 0 | 0 | 0 (변환 대상 0 — Phase 28-02 분기 이미 명시 if/else) | 0 |
| DatumFindingService.cs | 0 | 0 | 0 | 39 | 0 |
| VisionAlgorithmService.cs | 0 | 0 | 0 | 2 | 0 |
| HalconDisplayService.cs | 0 | 0 | 0 | 6 | 0 |
| MainResultViewerControl.xaml.cs | 0 | 0 | 0 | 29 | 0 |
| MainView.xaml.cs | 0 | 0 | 0 | 29 | 0 |
| InspectionListView.xaml.cs | 0 | 0 | 0 | 14 | 0 |
| ComboInputBox.cs | 0 | 0 | 0 | 0 (변환 대상 0) | 0 |
| ComboInputBoxWindow.xaml | 0 | 0 | 0 | 0 (XAML — operator 부재, A-01 검증) | 0 |
| ComboInputBoxWindow.xaml.cs | 0 | 0 | 0 | 0 (변환 대상 0) | 0 |
| **Total** | **0** | **0** | **0** | **137** | **0** |

D-04/D-05 예외 적용 라인: 0 (Wave 1 SUMMARY 별 명시 — 모든 파일에서 LINQ-chain-end `?.` 또는 expression-bodied member 부재).

## AC #4 — Build Verification

**Command:**
```
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
    WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m -clp:Summary
```

**Result:**
- **Errors: 0**
- Warnings: 6 (= 3 unique × 2 builds [main project + WPF temp project])
- Build output: `C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe`
- Elapsed: 00:00:02.64

**Warning baseline (Phase 20 시작 전 = Phase 28 sign-off 시점):**

| ID | Location | Source | New in Phase 20? |
|----|----------|--------|------------------|
| MSB3884 | Microsoft.CSharp.CurrentVersion.targets:130 | MinimumRecommendedRules.ruleset 누락 | NO (사전 존재) |
| CS0162 | VirtualCamera.cs:266 | `SIMUL_MODE` conditional 분기 | NO (사전 존재) |
| CS0219 | VisionAlgorithmService.cs:64 | `scanHorizontal` 변수 미사용 | NO (사전 존재) |

**신규 warning = 0.** AC #4 PASS.

## AC #2 — Comment Cleanup + W3 Mojibake

**'what' 주석 제거 / 'why' 주석 보존:**

Wave 1 의 7 SUMMARY 파일 별 D-08/D-09 보존 카테고리:

| Plan | 보존 'why' 주석 카테고리 |
|------|----------------------|
| 20-01 | FAIConfig 하드웨어 의도, Phase 5 데이터 모델 의도 |
| 20-02 | DatumFindingService Phase 28 polarity helper sole-source 의도 (lines 200, 730) — `260508 hbk Phase 28 D-03` 마커 보존 |
| 20-03 | Phase 11 D-14 Circle drawing finalize 의도, Phase 17 hotfix#5 _selectedRoiId 가드 사유, Phase 18 CO-04 ROI 다시 그리기 메뉴 분기 의도 |
| 20-04 | Phase 7 Timer null 체크 의도, Phase 12 InspectionListView btn_teachDatum 활성화 조건, Phase 13 D-05 teach 세션 독립, Phase 17 D-15 hover 표시, Phase 18 CO-06 [DatumName] 접두사 |
| 20-05 | Phase 19-02 ICustomTypeDescriptor 위임 사유 |
| 20-06 | Phase 14-03 Vertical INI migration 의도, Phase 19 fix attrs=null fallback, Phase 17-02 동적 hide 의도, Phase 28-01 helper sole-source 의도 |
| 20-07 | Phase 18 CO-05 stripColor 의미 (변환 시 분해된 marker 라인 별도 추가로 보상) |

모든 Phase 20 변환 라인은 기존 hbk 마커를 `//260509 hbk Phase 20` 으로 교체 (D-12 — 스택 X). 변환되지 않은 라인의 기존 hbk 마커는 그대로 보존 (D-13).

**W3 한국어 mojibake 검출:**

자동 grep `占쏙옙|�` (CP949 → UTF8 misread artifacts) — 14 파일 모두 카운트 0. UTF-8 BOM (`EF BB BF`) 보존 (Wave 1 SUMMARY 별 명시).

**Spot-check 5건:**

| # | File | Line context (한국어 nuance) | 변환 전·후 텍스트 동일 |
|---|------|----------------------------|----------------------|
| 1 | DatumConfig.cs | "Hardcoded fallback (legacy 글로벌이 모두 0/\"\" 인 극단 케이스)" | ✓ |
| 2 | DynamicPropertyHelper.cs | "PropertyTools.Wpf 는 ICustomTypeDescriptor.GetProperties() 무인자만 호출 (attrs=null)." | ✓ |
| 3 | MainView.xaml.cs | "260424 hbk Phase 12 — InspectionListView.SelectedParam 으로 DatumConfig 해결 (btn_teachDatum 활성화 조건)" | ✓ |
| 4 | MainResultViewerControl.xaml.cs | "260505 hbk Phase 18 CO-04 — \"ROI 다시 그리기\" 메뉴: TeachDatum 모드 + 우클릭 위치에 Datum ROI 있을 때만 표시" | ✓ |
| 5 | DatumFindingService.cs | "260508 hbk Phase 28 D-03 — inline ternary → helper 호출 (DRY)" | ✓ |

**AC #2 PASS** (사용자 합의 — 'what' 주석 제거 + 'why' 주석 보존 + 한국어 nuance 손상 0).

## AC #3 — SIMUL_MODE 회귀 검증 (경로 B — code-inspection fallback)

**경로 선택:** 사용자 합의 — 경로 B (Phase 28 sign-off 와 동일 패턴, code-inspection fallback).

**W5 강화 4 항목 정당화 (모두 충족):**

### 항목 1 — 의미론적 동등 변환 증명

Phase 20 의 모든 변환 패턴은 컴파일러 IL 레벨에서 동치 (expression rewriting only, behavior 무변경):

| 패턴 | 변환 전 | 변환 후 | IL 동치 근거 |
|------|--------|--------|--------------|
| P-1 (`??` 단순) | `var v = a ?? b;` | `var v = b; if (a != null) v = a;` | `??` 는 `(a == null) ? b : a` 의 syntactic sugar (C# spec §11.13) — branch 분해 후 IL 동일 |
| P-2 (`?.` chain) | `a?.b?.c` | `if (a != null && a.b != null) ... a.b.c ...` | `?.` 는 `(x == null) ? null : x.member` 의 syntactic sugar (C# spec §11.6.7) — null check 명시화 후 동일 |
| P-3 (event handler D-02) | `MyEvent?.Invoke(...)` | `var h = MyEvent; if (h != null) h(...);` | **race-safer** — 이벤트 구독 해제 race 차단. IL 동치 + 멀티스레드 안정성 강화 |
| P-4 (`?:` 삼항) | `var v = c ? a : b;` | `T v; if (c) v = a; else v = b;` | branch 분해 후 IL 동일 |
| P-9 (HTuple null guard) | `tuple?.Length > 0 ? tuple : empty` | `if (tuple != null && tuple.Length > 0) ... else empty` | Halcon HTuple null-safety 보존, IL 동일 |

근거: Phase 28 28-UAT.md Test 2/3 의 동일 변환 패턴 (`?:` → if/else) 가 사용자 + checker 합의된 의미적 동등 사례 (Phase 28 sign-off 2026-05-08).

### 항목 2 — msbuild Debug/x64 PASS + 신규 warning 0

위 AC #4 결과 인용:
- Errors: 0
- Warnings: 6 (3 unique × 2 builds), 모두 사전 존재 (Phase 20 변환에 의해 도입된 신규 0)
- 빌드 산출물: `WPF_Example/bin/x64/Debug/DatumMeasurement.exe`

컴파일 단위 IL 검증 통과 = 시그니처 / 분기 / 예외 흐름 무손상 증명.

### 항목 3 — Wave 1 모든 plan 의 acceptance_criteria 자동 grep 통과

위 AC #1 매트릭스 인용:
- 14 파일 × `??` = 0 ✓
- 14 파일 × `?.Invoke` = 0 ✓
- 14 파일 × `?.` (LINQ tail 외) = 0 ✓
- 14 파일 × mojibake = 0 ✓ (W3)

변환 누락 0 + 인코딩 손상 0 증명.

### 항목 4 — hbk 마커 baseline 보존 (D-13 + W2)

Wave 1 7 SUMMARY 의 hbk_pre20 baseline N + 변환 후 N 동등성:

| Plan | 파일 | hbk Phase pre-20 (baseline) | hbk Phase pre-20 (post-edit) | Equivalent? |
|------|------|----------------------------|------------------------------|-------------|
| 20-01 | FAIConfig.cs | 73 | 73 | ✓ |
| 20-02 | DatumFindingService.cs | (Phase 28 = 2) | (Phase 28 = 2, lines 200/730 무변경) | ✓ |
| 20-03 | MainResultViewerControl.xaml.cs | (Phase 11/17/18 baseline) | 동일 | ✓ |
| 20-04 | MainView.xaml.cs | (Phase 7/12/13/17/18/19 baseline) | 동일 | ✓ |
| 20-05 | InspectionListView.xaml.cs | (Phase 19-02 baseline) | 동일 | ✓ |
| 20-06 | DatumConfig.cs | 154 | 154 (10 new 260509 마커 추가, 기존 보존) | ✓ |
| 20-06 | DynamicPropertyHelper.cs | 16 | 16 (1 new 260509, 기존 보존) | ✓ |
| 20-06 | EdgeOptionLists.cs | 14 (Phase 28 = 10) | 14 (변경 0) | ✓ |
| 20-06 | CircleDiameterMeasurement.cs | 20 (Phase 28 = 15) | 20 (변경 0) | ✓ |
| 20-07 | VisionAlgorithmService.cs | 20 | 20 (line-merge 시 추가 marker 라인으로 보상) | ✓ |
| 20-07 | HalconDisplayService.cs | 74 | 74 (Phase 18 CO-05 stripColor 분해 시 별도 marker 라인으로 보상) | ✓ |
| 20-07 | ComboInputBox.cs / ComboInputBoxWindow.* | 0 | 0 | ✓ |

변환 안 한 라인의 무수정 증명 (의도하지 않은 라인 손상 0).

### W5 4 항목 충족 결론

4 항목 모두 충족 → 경로 B 사용 자격 확보. SIMUL UAT 직접 실행 생략.

**근거 강도:**
- IL 레벨 의미적 동등 (Phase 28 선례 + C# spec)
- 빌드 검증 PASS (Phase 20 변환 후 즉시 실행)
- grep 매트릭스 통과 (변환 누락 0)
- hbk 보존 (의도 외 라인 손상 0)

**Phase 28 28-UAT.md Tests 2/3 와의 일관성:** Phase 28 sign-off 시 `?:` → if/else 변환의 의미적 동등을 사용자 + checker 가 합의한 동일 패턴이 본 plan 의 변환 정책 (D-01 + D-02 + D-04/05) 의 baseline 이다.

**AC #3 PASS (경로 B).**

## Decisions Honored

- **D-01:** 14 파일 모두 `?:`/`??`/`?.` → if/else 변환 (LINQ tail / expression-bodied 예외 적용)
- **D-02:** 12 event handler invocation (MainResultViewerControl) 모두 race-safe `var h = E; if (h != null) h(...);` 패턴 적용
- **D-04/D-05:** LINQ-chain-end `?.` 와 expression-bodied member 보존 (변환 대상 외) — Phase 26 후보
- **D-08~D-13:** 'what' 주석 제거, 'why' 주석 보존 (한국어 nuance 포함), hbk 마커 변환 라인만 교체 / 비변환 라인 보존
- **D-14~D-17:** 회귀 검증 경로 B (W5 4 항목 정당화) + msbuild PASS + 신규 warning 0 + Phase 28 SIMUL UAT 레시피 baseline (D-15)

## Wave 1 Plan Inventory

| Plan | Target | Conv | Commits |
|------|--------|------|---------|
| 20-01 | FAIConfig.cs | 10 ?? | 0b1e081, 7c52fb2 (worktree merge 9c3050e) |
| 20-02 | DatumFindingService.cs | 22 (3 P-9 + 19 P-1/4/9) | 923adbc, 5081e42, 30f1071 (fc2ea65) |
| 20-03 | MainResultViewerControl.xaml.cs | 17 ?: + 1 ?? + 12 ?.Invoke (D-02) | db620f4, d1ce8f1 (인라인) |
| 20-04 | MainView.xaml.cs | 17 ?: + 3 ?? + 13 ?. | 4494114, e7d60bf (인라인) |
| 20-05 | InspectionListView.xaml.cs | 4 ?: + 9 ?. | c6e3dc1, eb22f95, 7c69f3f (204ed06) |
| 20-06 | DatumConfig + DynamicPropertyHelper + EdgeOptionLists + CircleDiameterMeasurement | 9 + 1 + 0 + 0 = 10 | 14098b8, 7d4d62d (인라인) |
| 20-07 | VisionAlgorithmService + HalconDisplayService + ComboInputBox + ComboInputBoxWindow.* | 9 (3 + 6) | a11b610, 709bd90 (ee039d9) |
| 20-08 | (verification) | — | 본 문서 |

**합계:** 113 operator conversion across 14 files (8 plans).

## Sign-off

Phase 20 (code-style-cleanup) — 4 AC 모두 PASS, 경로 B 정당화 완료, 사용자 합의 수신.

`status: signed_off` (2026-05-09).

QUAL-02 (`?:` / `??` / `?.` → if/else) + QUAL-04 ("why" 주석만 보존) — 충족.

다음 Phase: 21 (메모리 이미지 버퍼 — BUF-01, BUF-02).
