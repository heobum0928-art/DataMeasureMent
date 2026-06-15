---
phase: 42-pixel-resolution-single-source
reviewed: 2026-06-15T00:00:00Z
depth: standard
files_reviewed: 3
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
findings:
  critical: 0
  warning: 0
  info: 2
  total: 2
status: clean
---

# Phase 42: Code Review Report

**Reviewed:** 2026-06-15
**Depth:** standard
**Files Reviewed:** 3
**Status:** clean (blocking/warning 없음, info 2건)

## Summary

Phase 42 diff 3개 파일을 검토했다. 핵심 우려사항 4가지 모두 정상 확인:

1. **Owner 체인 null 처리** — `EdgePairDistanceMeasurement.TryExecute`의 `ownerFai.Owner as ShotConfig` 캐스트는 `ownerFai == null` 가드(line 66-70) 통과 이후에 실행된다. `ownerShot == null` 시 넘어온 `pixelResolution` 파라미터로 fallback하므로 NRE 및 0mm 결과 없음.

2. **`pixRes` 캡처 스코프** — line 194는 `if (ShotParam != null)` → `using (image = ...)` → `if (image != null)` 3중 중첩 블록 안에 위치한다. 즉 shot과 image가 모두 유효한 컨텍스트에서 1회 캡처하며, 이후 DualImage 경로(line 270)·1-image 경로(line 285) 두 호출부 모두에 정확히 전달된다.

3. **INI 직렬화 보존** — `FAIConfig.PixelResolutionX/Y` 및 `EdgePairDistanceMeasurement.PixelResolutionX/Y` 필드는 삭제되지 않고 `[Browsable(false)]` 어트리뷰트만 추가됐다. `ParamBase.Save/Load`는 Reflection 경로를 사용하며 `Browsable` 어트리뷰트를 무시하므로 INI 라운드트립 영향 없음(D-07 충족).

4. **측정 알고리즘 로직 무변경** — `FAIEdgeMeasurementService.TryMeasure`가 `fai.PixelResolutionX/Y`를 소비하는 방식은 동일하며, temp FAIConfig에 `resolvedPixelRes`가 X·Y 모두 대입되어 scanHorizontal/vertical 두 경로 모두 정상.

**회귀 위험:** 정규화된 레시피(`InspectionRecipeManager.cs:328-334`)는 로딩 시 `fai.PixelResolutionX == shot.PixelResolution`을 보장하므로 Rewire 전후 동일 값이 전달된다. 회귀 0 논증 타당.

---

## Info

### IN-01: `pixRes` 캡처의 삼항 null 가드가 dead code

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:194`
**Issue:** `pixRes` 선언부가 `if (ShotParam != null)` 블록(line 184) 내부에 있어, 삼항식 `ShotParam != null ? ... : 1.0`의 false 분기는 절대 도달할 수 없다. 실행 방어 효과가 없는 dead code이다.
**Fix:** 삼항 가드를 제거하고 직접 읽기로 단순화. 주석은 유지:
```csharp
double pixRes = ShotParam.PixelResolution; //260615 hbk Phase 42 D-01 Shot 단일소스
```
*우선순위: 저. 동작 정확성에 영향 없음. 다음 해당 파일 수정 시 정리 권장.*

---

### IN-02: `EdgePairDistanceMeasurement.PixelResolutionX/Y`에 `PixelResolutionY`용 주석이 없음

**File:** `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs:38-42`
**Issue:** `PixelResolutionX`에는 INI 호환 잔존 저장용 주석이 있지만(`// INI 호환 잔존 저장용 ...`), `PixelResolutionY`는 단독 주석 없이 어트리뷰트만 붙어 있다. 동일한 의도가 Y 필드에도 적용됨을 코드로 명확히 하지 않는다. 기능적 문제는 없음.
**Fix:** `PixelResolutionY` 바로 위에 인라인 주석 1줄 추가:
```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public double PixelResolutionX { get; set; } = 1.0; // INI 호환 잔존 저장용 — D-06 재배선 후 TryExecute 에서 소비 안 함. //260615 hbk Phase 42 D-05
[PropertyTools.DataAnnotations.Browsable(false)]
public double PixelResolutionY { get; set; } = 1.0; // 동상. X=Y 정방형 가정(D-09).
```
*우선순위: 저. 가독성 개선 목적.*

---

## 검토 의견 요약

블로킹/고위험/경고 발견 없음. 3개 파일의 변경은 계획(D-01, D-04~D-07)을 정확히 구현했으며, null 방어·INI 호환·알고리즘 무변경 세 조건이 모두 충족된다. Info 2건은 다음 해당 파일 수정 시 함께 정리하면 충분하다.

---

_Reviewed: 2026-06-15_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
