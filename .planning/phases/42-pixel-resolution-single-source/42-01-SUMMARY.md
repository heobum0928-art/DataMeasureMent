---
phase: 42-pixel-resolution-single-source
plan: "01"
subsystem: inspection-measurement
tags: [pixel-resolution, single-source, rewire, property-grid]
dependency_graph:
  requires: []
  provides:
    - "Shot 단일소스 픽셀분해능 소비 (ShotConfig.PixelResolution)"
    - "PropertyGrid 항목별 PixelResolutionX/Y 숨김 (FAIConfig + EdgePairDistanceMeasurement)"
  affects:
    - "Action_FAIMeasurement EStep.Measure 루프"
    - "EdgePairDistanceMeasurement.TryExecute temp FAIConfig 구성"
tech_stack:
  added: []
  patterns:
    - "[PropertyTools.DataAnnotations.Browsable(false)] 정적 숨김 (DatumConfig 선례)"
    - "ShotParam.PixelResolution 1회 캡처 후 루프 내 전달"
    - "ownerFai.Owner as ShotConfig Owner 체인 walk"
key_files:
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
  created: []
decisions:
  - "D-01 (B) Rewire 채택: fai.PixelResolutionX → ShotParam.PixelResolution (pixRes 지역변수)"
  - "D-06 EdgePair Owner 체인 walk: ownerFai.Owner as ShotConfig, pixelResolution fallback 방어"
  - "D-04/D-05 정적 Browsable(false) 선택: 무조건 숨김이므로 ICustomTypeDescriptor 동적 필터 미사용"
  - "D-07 INI 하위 호환: 필드 자체 삭제 0, ParamBase Reflection 직렬화 Browsable 무관"
metrics:
  duration_minutes: 25
  completed_date: "2026-06-15"
  tasks_completed: 3
  files_modified: 3
---

# Phase 42 Plan 01: 픽셀분해능 단일소스 Rewire + PropertyGrid 정리 Summary

**One-liner:** `fai.PixelResolutionX` 소비 2곳을 `ShotParam.PixelResolution` 단일소스로 Rewire하고, FAIConfig/EdgePair 항목별 PixelResolutionX/Y 4개를 Browsable(false)로 PropertyGrid에서 숨김.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | 측정 소비 경로 Rewire (D-01, D-06) | 24225b2 | Action_FAIMeasurement.cs, EdgePairDistanceMeasurement.cs |
| 2 | PropertyGrid 항목별 PixelResolution 숨김 (D-04, D-05) | 57ee17c, 24225b2 | FAIConfig.cs, EdgePairDistanceMeasurement.cs |
| 3 | 회귀 검증 + 단일소스 정합 문서화 | (코드 변경 없음) | — |

## Changes Made

### Task 1: Action_FAIMeasurement.cs + EdgePairDistanceMeasurement.cs

**Action_FAIMeasurement.cs (K&R 스타일 유지):**
- `EStep.Measure` case, `foreach (var fai in ShotParam.FAIList)` 진입 직전에 1회 캡처:
  ```csharp
  double pixRes = ShotParam != null ? ShotParam.PixelResolution : 1.0; //260615 hbk Phase 42 D-01 Shot 단일소스
  ```
- DualImage 경로 (line ~270): `fai.PixelResolutionX` → `pixRes` + `//260615 hbk Phase 42 D-01`
- 1-image 경로 (line ~285): 동일 교체 + 주석

**EdgePairDistanceMeasurement.cs (Allman 스타일 유지):**
- `ownerFai` 확인 직후 Owner 체인 walk 추가:
  ```csharp
  //260615 hbk Phase 42 D-06 — PixelResolution 은 shot 단일소스(파라미터 경유). self 필드 소비 제거
  var ownerShot = ownerFai.Owner as ShotConfig;
  double resolvedPixelRes = (ownerShot != null) ? ownerShot.PixelResolution : pixelResolution;
  ```
- temp FAIConfig 구성: `PixelResolutionX = resolvedPixelRes`, `PixelResolutionY = resolvedPixelRes`

### Task 2: FAIConfig.cs + EdgePairDistanceMeasurement.cs

**FAIConfig.cs:**
- `[Category("Calibration")]` 제거
- `PixelResolutionX`, `PixelResolutionY` 각 프로퍼티 위에 `[PropertyTools.DataAnnotations.Browsable(false)]` 추가
- 주석: `// Calibration: INI 호환 잔존 저장용. 소비 없음 — Shot 단일소스(D-01). PropertyGrid 숨김. //260615 hbk Phase 42 D-04/D-05`

**EdgePairDistanceMeasurement.cs:**
- `[Category("EdgePair|Calibration")]` 제거
- `PixelResolutionX`, `PixelResolutionY` 각 프로퍼티 위에 `[PropertyTools.DataAnnotations.Browsable(false)]` 추가
- 주석: `// INI 호환 잔존 저장용 — D-06 재배선 후 TryExecute 에서 소비 안 함. //260615 hbk Phase 42 D-05`

## Task 3: 회귀 검증 결과

### (a) 정규화 레시피 회귀 0 등가성 논증

`InspectionRecipeManager.cs:328-334` 로딩 정규화(D-10):
```csharp
if (loadFile.ContainsSection(camSection)) {
    double camRes = shot.PixelResolution;
    foreach (FAIConfig fai2 in shot.FAIList) {
        fai2.PixelResolutionX = camRes;
        fai2.PixelResolutionY = camRes;
    }
}
```
CAM 섹션이 있는 정규화된 레시피는 로딩 후 `fai.PixelResolutionX == shot.PixelResolution` 가 성립한다.
따라서 Task 1 Rewire (`fai.PixelResolutionX` → `ShotParam.PixelResolution`) 전후에 **동일 값**이 전달된다. **측정 mm 값 회귀 0.**

### (b) 구형(CAM 미존재) 레시피 의도적 보정

CAM 섹션이 없는 레시피는 정규화 미실행 → `fai.PixelResolutionX ≠ shot.PixelResolution` 가능.
Rewire 후 shot 값으로 수렴하므로 측정 mm 값이 달라질 수 있다.
이는 **D-01의 의도적 보정**(shot 단일소스 수렴)이며, Phase 38 성공기준 #3 "픽셀분해능 단일소스"의 연속선이다.
구형 레시피 보유 사용자는 Shot PropertyGrid에서 PixelResolution을 재확인·저장하면 이후 회귀 0.

### (c) D-03 라이브 반영 구조 확인

`EStep.Measure` 가 실행될 때마다 `ShotParam.PixelResolution` 을 직접 읽으므로, Shot 노드 PropertyGrid에서 값 편집 후 다음 검사 시점에 즉시 반영된다.
별도 cascade/트리거/핸들러 구현 불필요 — 구조적으로 충족됨 (D-03 확정).

### (d) 로딩 정규화 확장 불필요 확인

Task 1 Rewire 후 `EdgePairDistanceMeasurement.PixelResolutionX/Y` 와 `FAIConfig.PixelResolutionX/Y` 가 측정 소비 경로에서 완전히 제거되었다. INI 저장 필드로만 잔존하므로 `InspectionRecipeManager` 에서 이 필드들을 추가 정규화할 필요가 없다. **변경 0.**

### (e) 편집 진입점 단일소스 수렴 확인 (D-02)

`MainView.xaml.cs:2200`:
```csharp
shot.PixelResolution = mmPerPixel;
```
2점 캘리브레이션 액션(`ApplyCalibrationResult`)이 `shot.PixelResolution` 으로 단일소스에 기록.
Shot PropertyGrid 편집 + 2점 캘리브레이션 두 진입점 모두 `shot.PixelResolution` 으로 수렴 확인. **변경 불필요.**

### (f) 최종 빌드 게이트

msbuild Debug/x64 Rebuild PASS — DatumMeasurement.exe 생성 완료.
warning 6개 (MSB3884 × 2 + CS0618 × 3 + CS0162 × 1 = Phase 41 baseline 동일), 신규 error 0, 신규 warning 0.

## Deviations from Plan

없음 — 계획대로 정확히 실행됨.

## Known Stubs

없음.

## Threat Flags

없음 — 신규 네트워크 표면/외부 입력/직렬화 포맷 변경 0. 순수 내부 소비 경로 Rewire + PropertyGrid 어트리뷰트 변경.

## Acceptance Criteria Verification

| Criterion | Result |
|-----------|--------|
| Grep `fai.PixelResolutionX` in Action_FAIMeasurement.cs EStep.Measure → 0 매치 | PASS |
| Grep `double pixRes = ShotParam` in Action_FAIMeasurement.cs → 1 매치 | PASS |
| Grep `TryExecute(image, transform, pixRes` in Action_FAIMeasurement.cs → 2 매치 | PASS |
| Grep `resolvedPixelRes` in EdgePairDistanceMeasurement.cs → 3 매치 | PASS |
| Grep `PixelResolutionX = PixelResolutionX` in EdgePairDistanceMeasurement.cs → 0 매치 | PASS |
| Grep `//260615 hbk` in 두 수정 파일 → 각 ≥ 2 매치 | PASS |
| FAIConfig.cs PixelResolutionX/Y 위 Browsable(false) 존재 | PASS |
| FAIConfig.cs [Category("Calibration")] PixelResolution 인근 제거 | PASS |
| EdgePairDistanceMeasurement.cs PixelResolutionX/Y 위 Browsable(false) 존재 | PASS |
| EdgePairDistanceMeasurement.cs [Category("EdgePair|Calibration")] 제거 | PASS |
| CameraSlaveParam.cs PixelResolution 무변경 (git diff 미포함) | PASS |
| msbuild Debug/x64 Rebuild PASS, 신규 error 0 / 신규 warning 0 | PASS |
| SUMMARY에 회귀 0 논증 (a), 구형 레시피 보정 (b), D-03 라이브 반영 (c), 정규화 확장 불필요 (d) | PASS |

## Self-Check: PASSED

- WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs: FOUND
- WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs: FOUND
- WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs: FOUND
- commit 24225b2: FOUND
- commit 57ee17c: FOUND
