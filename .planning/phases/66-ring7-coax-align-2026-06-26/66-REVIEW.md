---
phase: 66-ring7-coax-align-2026-06-26
reviewed: 2026-06-29T00:00:00Z
depth: standard
files_reviewed: 9
files_reviewed_list:
  - WPF_Example/Custom/EthernetVision/AlignRefPose.cs
  - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/Custom/UI/BottomVisionView.xaml
  - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
  - WPF_Example/Custom/UI/TrayVisionView.xaml
  - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
findings:
  critical: 0
  warning: 2
  info: 1
  total: 3
status: issues_found
---

# Phase 66: Code Review Report

**Reviewed:** 2026-06-29
**Depth:** standard
**Files Reviewed:** 9
**Status:** issues_found

## Summary

Phase 66 변경 범위: (1) ShotConfig Ring7 조명 필드 추가 + INI 직렬화, (2) InspectionSequence ApplyShotLightsInternal/TurnOffShotLights Ring7 대칭 소등, (3) AlignRefPose CoaxEnabled/CoaxLevel 필드 추가, (4) AlignShapeMatchService GetSlotRefPose/TrySaveCoax 공개 API, (5) SystemHandler ApplyCoaxLightForSlot, (6) BottomVisionView/TrayVisionView 동축 UI 패널.

Ring7 점등/소등 대칭 구현은 정확하다. 동축 load-merge-save 설계(TrySaveCoax)도 티칭 데이터 보존 관점에서 올바르다. CoaxLevel 범위 가드와 슬라이더 초기 로드 시 이벤트 연쇄 저장 문제 2건이 발견되었다.

---

## Warnings

### WR-01: CoaxLevel 범위 무검증 — 외부 JSON 변조 시 음수·256+ 값 LightHandler 전달

**File:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs:260-262`

**Issue:** `TrySaveCoax`에서 `coaxLevel` 파라미터를 범위 검증 없이 JSON에 그대로 직렬화한다. `GetSlotRefPose`로 로드한 후 `ApplyCoaxLightForSlot`(`SystemHandler.cs:373`)에서 `LightHandler.Handle.SetLevel(LIGHT_ALIGN_COAX, nLevel)`에 그대로 전달된다. JSON 파일이 수동 편집되거나 구 버전 파일에 비정상값이 들어있으면 직렬 포트 조명 컨트롤러에 범위 외 값이 전달된다.

`SetLevel`이 내부적으로 클램프하지 않는 구조라면 HW 프로토콜 오류로 이어진다. 소스에서 확인할 수 없으므로 방어적 클램프가 필요하다.

**Fix:**
```csharp
// TrySaveCoax 내부 — 저장 전 클램프
int nClamped = coaxLevel;
if (nClamped < 0)   nClamped = 0;
if (nClamped > 255) nClamped = 255;
refPose.CoaxLevel = nClamped;
```
또는 `ApplyCoaxLightForSlot`에서 읽은 직후 클램프:
```csharp
if (nLevel < 0)   nLevel = 0;
if (nLevel > 255) nLevel = 255;
```

---

### WR-02: 슬라이더 초기화 시 CoaxSlider_ValueChanged 이벤트 연쇄 — 불필요한 JSON 쓰기 발생

**File:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs:840-841` / `WPF_Example/Custom/UI/TrayVisionView.xaml.cs:389-390`

**Issue:** `LoadSlotCoaxToUi()`와 `LoadTrayCoaxToUi()`에서 `sld_coaxLevel.Value = nLevel`을 설정하면 `CoaxSlider_ValueChanged` 이벤트가 즉시 발화된다. 이 핸들러는 `ApplyCoaxLight()` + `SaveSlotCoaxToJson()`(또는 `SaveTrayCoaxToJson()`)을 호출한다. 결과적으로 뷰 최초 로드 시 또는 슬롯 전환 시 JSON에 불필요한 재저장이 발생한다.

현재는 데이터 일치성 손상이 없어 기능 회귀는 없지만, 다음 두 가지 부작용이 있다:
- `_selectedSlot == None`인 상태에서 `LoadSlotCoaxToUi` 내부 초기화 도중 Bottom 뷰의 `SaveSlotCoaxToJson`이 `_selectedSlot == None`으로 스킵하므로 Bottom 뷰에선 무해하다.
- Tray 뷰에서는 `LoadTrayCoaxToUi` → 슬라이더 set → `SaveTrayCoaxToJson` → `TrySaveCoax`가 호출되어 로드 직후 항상 재저장이 발생한다. 레시피 미설정 상태에서 `jsonPath`가 null이면 저장 실패 에러 메시지가 `lbl_status`에 표시될 수 있다.

**Fix:** 로드 중에는 이벤트 핸들러를 임시 해제하거나 가드 플래그를 사용한다.
```csharp
// Tray / Bottom 공통 패턴
private bool _isLoadingCoax = false;

private void LoadTrayCoaxToUi()
{
    _isLoadingCoax = true;
    try
    {
        // ... sld_coaxLevel.Value = nLevel; 등 복원 ...
    }
    finally
    {
        _isLoadingCoax = false;
    }
}

private void CoaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (_isLoadingCoax) return;   // 초기화 중 연쇄 방지
    int nLevel = (int)e.NewValue;
    // ...
}
```

---

## Info

### IN-01: ShotConfig.CoaxLight_Brightness 에 [Browsable(false)] 미적용 — PropertyGrid 에 밝기 필드만 노출

**File:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:70-73`

**Issue:** Phase 66 D-03 의도는 "검사 PropertyGrid 에서 동축 숨김"이다. `CoaxLight_Enabled`에 `[Browsable(false)]`가 추가되었으나 `CoaxLight_Brightness`에는 추가되지 않았다. PropertyGrid 가 Enabled/Brightness를 연속 표시하는 구조라면 밝기 필드만 혼자 남아 혼란을 줄 수 있다.

**Fix:** 두 필드 모두 숨기는 것이 의도라면 밝기 필드에도 어트리뷰트 추가:
```csharp
[Browsable(false)]
[Category("Light|Coax")]
public bool CoaxLight_Enabled { get; set; }

[Browsable(false)]
public int CoaxLight_Brightness { get; set; }
```

---

_Reviewed: 2026-06-29_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
