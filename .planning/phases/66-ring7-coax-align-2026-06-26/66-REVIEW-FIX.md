---
phase: 66-ring7-coax-align-2026-06-26
fixed_at: 2026-06-29T00:00:00Z
review_path: .planning/phases/66-ring7-coax-align-2026-06-26/66-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 66: Code Review Fix Report

**Fixed at:** 2026-06-29
**Source review:** .planning/phases/66-ring7-coax-align-2026-06-26/66-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (WR-01, WR-02, IN-01)
- Fixed: 3
- Skipped: 0

## Fixed Issues

### IN-01: ShotConfig.CoaxLight_Brightness 에 [Browsable(false)] 미적용

**Files modified:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs`
**Commit:** 85767b1
**Applied fix:** `CoaxLight_Brightness` 프로퍼티 바로 위에 `[Browsable(false)]` 어트리뷰트 추가.
동축 2필드(CoaxLight_Enabled / CoaxLight_Brightness) 모두 PropertyGrid 에서 숨김 처리.
필드 선언 및 INI 직렬화 키는 보존(하위호환).

### WR-01: CoaxLevel 범위 무검증 — 외부 JSON 변조 시 음수·256+ 값 LightHandler 전달

**Files modified:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`
**Commit:** 13217ab
**Applied fix:** `TrySaveCoax` 내부 `refPose.CoaxLevel = coaxLevel` 직전에 0~255 범위 클램프 추가.
`int nClamped = coaxLevel`으로 복사 후 `if (nClamped < 0)` / `if (nClamped > 255)` if-else 로 클램프,
클램프된 값을 `refPose.CoaxLevel = nClamped` 로 저장. 삼항 연산자 미사용.

### WR-02: 슬라이더 초기화 시 CoaxSlider_ValueChanged 이벤트 연쇄 — 불필요한 JSON 쓰기 발생

**Files modified:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs`, `WPF_Example/Custom/UI/TrayVisionView.xaml.cs`
**Commit:** 28762e6
**Applied fix:** 두 파일 모두 동일 패턴 적용.
- 클래스 필드에 `private bool _isLoadingCoax = false;` 추가.
- `LoadSlotCoaxToUi()` / `LoadTrayCoaxToUi()` 진입 시 `_isLoadingCoax = true`, `finally` 블록에서 `_isLoadingCoax = false` 복원 (try/finally 패턴).
- `CoaxCheckBox_Changed` / `CoaxSlider_ValueChanged` 핸들러 시작부에 `if (_isLoadingCoax) return;` 가드 추가.
로드 중 슬라이더·체크박스 값 설정에 의한 연쇄 `TrySaveCoax` 호출 차단.

---

## Build Verification

MSBuild Debug/x64: **PASS** (exit code 0, error 0, DatumMeasurement.exe 생성 확인)
경고: MSB3884 (MinimumRecommendedRules.ruleset 미탐색) — 기존 빌드부터 존재하는 비관련 경고.

---

_Fixed: 2026-06-29_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
