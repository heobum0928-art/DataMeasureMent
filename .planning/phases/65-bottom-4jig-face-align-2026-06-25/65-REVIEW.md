---
phase: 65-bottom-4jig-face-align-2026-06-25
reviewed: 2026-06-26T12:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - WPF_Example/Custom/EthernetVision/EBottomAlignSlot.cs
  - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
  - WPF_Example/Custom/UI/BottomVisionView.xaml
  - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/DatumMeasurement.csproj
findings:
  critical: 0
  warning: 3
  info: 3
  total: 6
status: resolved
---

# Phase 65: Code Review Report

**Reviewed:** 2026-06-26
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Phase 65는 Bottom 면별 Align을 위해 EBottomAlignSlot enum(6슬롯), AlignShapeMatchService 슬롯 파라미터 확장, BottomVisionView UI 슬롯 선택, ProcessAlignTest 실측 배선 등 4개 plan을 구현했다.

핵심 설계 결정인 `slot == None` → 기존 Bottom 단일 경로 폴백(D-09) 및 Tray 경로 무변경(D-10) 은 diff에서 코드 수준으로 확인되어 회귀 위험 없음. `BuildJsonPath`의 구 `Replace("_1","")` 방식이 C# `String.Replace`의 전체치환 특성으로 인해 `Bottom_2D_SIDE_1_1` → `Bottom_2D_SIDE.json` 로 오치환될 수 있었고, 신규 `EndsWith` 방식으로 올바르게 수정되었음을 시뮬레이션으로 확인함.

경고 수준 이슈 3건이 발견됨: OOB AlignFace 거부 경로에서 Items가 비어 있는 채로 응답되는 점, RunBottomAlign 외부 catch에서 FillAlignPoseZero가 누락된 점, BottomVisionView._slotRois 복원 시 배열 길이 무확인 점. 각각 프로토콜 파서 오류·HALCON 리소스 누수 없이 안전하게 동작하나, 엣지 케이스에서 PLC가 예측 불가능한 빈 Items 응답을 수신할 수 있음.

---

## Warnings

### WR-01: OOB AlignFace 거부 경로 — Items 빈 채로 응답

**File:** `WPF_Example/Custom/SystemHandler.cs:254-258`

**Issue:** `ProcessAlignTest` 에서 AlignFace 범위 외 거부(`!bSlotValid`) 시 `FillAlignPoseZero`를 호출하지 않고 바로 `return resultPacket`한다. 이 경우 `resultPacket.Items`는 비어 있는 채로 직렬화되어 PLC에게 `$ALIGN_RESULT:BOTTOM,자재번호,AlignFace,NG,@` 형태(Items 파트 빈 문자열)가 전송된다.

`BuildAlignResultMessage`의 `BuildAlignItems` 가 Items.Count == 0 이면 빈 문자열을 반환하므로, 패킷은 형식상 유효하지만 OffsetX/OffsetY/Theta 필드가 누락된다. v3.0 스펙(D-08)이 NG 시에도 3필드를 전송하도록 요구한다면 PLC 파서가 필드 부족으로 예외를 일으킬 수 있다.

**Fix:** 거부 직전 `FillAlignPoseZero(resultPacket)` 호출 추가:

```csharp
if (!bSlotValid)
{
    Logging.PrintLog((int)ELogType.Error,
        "[ALIGN_TEST] AlignFace 범위 외 거부: {0} (유효범위 0~5) //260626 hbk", packet.AlignFace);
    FillAlignPoseZero(resultPacket); // 추가: PLC 형식 일관성 (WR-01)
    resultPacket.IsPass = false;
    return resultPacket;
}
```

---

### WR-02: RunBottomAlign 외부 catch — FillAlignPoseZero 미호출

**File:** `WPF_Example/Custom/SystemHandler.cs:341-347`

**Issue:** `RunBottomAlign`의 외부 `catch (Exception ex)` 블록은 로그 출력 후 `return false`를 반환한다. 호출자 `ProcessAlignTest`는 반환값이 `false`면 `resultPacket.IsPass = false`로 설정하고 반환하는데, 이 경로에서 `FillAlignPoseZero`가 호출된 경로는 없다.

내부 `try` 블록의 각 early-return 경로는 `FillAlignPoseZero` 를 호출하지만, 외부 catch로 빠져나오는 예외 경로(예: `EthernetVisionHandler.Handle` 자체가 예외를 던지는 경우)에서는 Items가 비어있다. WR-01과 동일하게 빈 Items 응답이 전송된다.

**Fix:** 외부 catch에서 `pResult`에 zero pose 채움:

```csharp
catch (Exception ex)
{
    Logging.PrintLog((int)ELogType.Error,
        "[ALIGN_TEST] RunBottomAlign 예외: {0} //260626 hbk", ex.Message);
    FillAlignPoseZero(pResult); // 추가: 빈 Items 응답 방지 (WR-02)
    return false;
}
```

---

### WR-03: BottomVisionView._slotRois 복원 시 배열 길이 미검증

**File:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs:160-163`

**Issue:** `SlotComboBox_SelectionChanged`에서 `_slotRois[_selectedSlot]`로 저장된 배열을 복원할 때 `savedRois.Length`를 확인하지 않고 `savedRois[0]`, `savedRois[1]`에 직접 접근한다.

저장 경로(`_slotRois[_selectedSlot] = new RoiDefinition[] { _roi1, _roi2 }`)에서 항상 길이 2의 배열이 저장되므로, 현재 코드 흐름상 실제로 IndexOutOfRangeException이 발생할 가능성은 낮다. 그러나 `_slotRois` 딕셔너리가 이 경로 외의 코드에서 수정될 경우 방어 없이 크래시가 발생한다.

**Fix:** 길이 확인 후 복원:

```csharp
if (_slotRois.ContainsKey(_selectedSlot))
{
    RoiDefinition[] savedRois = _slotRois[_selectedSlot];
    if (savedRois != null && savedRois.Length >= 2) // 방어 (WR-03)
    {
        _roi1 = savedRois[0];
        _roi2 = savedRois[1];
    }
    else
    {
        _roi1 = null;
        _roi2 = null;
    }
}
```

---

## Info

### IN-01: EBottomAlignSlot 네임스페이스 — ReringProject.Setting (AlignShapeMatchService와 혼용)

**File:** `WPF_Example/Custom/EthernetVision/EBottomAlignSlot.cs:1`

**Issue:** `EBottomAlignSlot`이 `namespace ReringProject.Setting`에 선언되어 있다. 같은 디렉터리의 `EEthernetVisionMode.cs`도 동일하게 `ReringProject.Setting`에 속해 있으므로 일관성은 유지된다. 그러나 `AlignShapeMatchService`(namespace `ReringProject`)가 이를 참조하기 위해 `using ReringProject.Setting`이 이미 선언되어 있으므로 빌드에는 문제 없다.

향후 새 파일에서 혼란을 줄이기 위해 비전 도메인 enum은 `ReringProject`(또는 `ReringProject.Define`) 단일 네임스페이스에 배치하는 것을 검토할 수 있다.

**Fix:** 현행 유지 가능. 다음 리팩토링 시 `EEthernetVisionMode`와 함께 `ReringProject.Define`으로 이전 고려.

---

### IN-02: ToDisplayLabel과 ToFileToken 로직 중복

**File:** `WPF_Example/Custom/EthernetVision/EBottomAlignSlot.cs:28-122`

**Issue:** `ToFileToken`과 `ToDisplayLabel`이 동일한 6-분기 if-chain을 반복한다. 반환값도 사실상 동일하다(`2D_SIDE_1` 등). 슬롯이 추가될 때 양쪽을 모두 수정해야 하므로 누락 위험이 있다.

**Fix:** `ToDisplayLabel`이 `ToFileToken`을 내부 호출하고, `None` 케이스만 별도로 처리:

```csharp
public static string ToDisplayLabel(EBottomAlignSlot slot)
{
    if (slot == EBottomAlignSlot.None)
    {
        return "(단일)";
    }
    return ToFileToken(slot); // 토큰 = 라벨 동일
}
```

---

### IN-03: BottomVisionView — RunButton_Click 슬롯 None 허용 주석 불일치

**File:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs:371`

**Issue:** `RunButton_Click` 주석에 "None이면 단일 경로 폴백(D-09)"이라고 되어 있다. 이는 설계상 허용된 동작이지만, 한편으로 `TeachButton_Click`에서는 슬롯 None이면 티칭을 거부한다(T-65-04 가드). 작업자가 슬롯을 선택하지 않은 채 [검사] 버튼을 누르면 기존 단일 Bottom 모델로 검사가 실행되어 혼동을 줄 수 있다.

**Fix:** 런타임 검사(Run)에도 슬롯 미선택 시 경고 메시지를 표시하는 것을 검토:

```csharp
if (_selectedSlot == EBottomAlignSlot.None) {
    lbl_status.Text = "슬롯 선택 후 검사하세요"; // 안내 (IN-03)
    return;
}
```
단, D-09 폴백을 의도적으로 허용한다면 현행 유지.

---

## 추가 확인 사항 (이상 없음)

아래 항목은 검토 결과 문제 없음이 확인되었다.

**path token 오치환 수정 (BuildJsonPath) — 정상:**
`2D_SIDE_1` 슬롯의 shm1 basename은 `Bottom_2D_SIDE_1_1`이다. 구 방식 `String.Replace("_1", "")`은 C#에서 모든 발생을 치환하므로 `Bottom_2D_SIDE.json`이 생성되는 버그가 있었다. 신규 `EndsWith("_1")` + `Substring` 방식은 `Bottom_2D_SIDE_1.json`을 정확히 생성한다. 시뮬레이션으로 6슬롯 전체 파일명 정확성 확인 완료.

**slot == None 폴백 회귀 — 없음:**
`BuildShmPath`, `HasTemplate`, `TryLoadTemplate`, `Run`, `TryTeach` 모두 `slot = EBottomAlignSlot.None` 기본값 경로에서 `modeFileName = "Bottom"`을 그대로 사용한다. 기존 `Bottom_1/2.shm` + `Bottom.json` 경로가 변경되지 않음.

**Tray 경로 — 무변경:**
`BuildShmPath`의 `else` 분기(`modeFileName = "Tray"`)는 slot 파라미터와 무관하게 실행된다. ProcessAlignTest에서 `bIsBottom == false` 시 즉시 echo ack 반환되어 grab/Run을 수행하지 않는다.

**HImage / DetectedContourXld Dispose — 정상:**
`RunBottomAlign` 내부 `try/finally`에서 `img.Dispose()`와 `res.DetectedContourXld.Dispose()`를 보장한다. Phase 61.1 WR-01 패턴 준수.

**AlignResultPacket Items 순서 — 정상:**
`FillAlignPose`가 OffsetX → OffsetY → Theta 순서로 채우며, `BuildAlignResultMessage`의 `BuildAlignItems`는 Items 리스트를 순서대로 직렬화한다. D-08 v3.0 스펙 일치.

**FromAlignFace OOB 안전 — 정상:**
음수 또는 6 이상 값은 모두 `EBottomAlignSlot.None`을 반환하고, `ProcessAlignTest`에서 즉시 `IsPass = false`로 거부된다.

**삼항 연산자 금지 규칙 — 준수:**
Phase 65 전체 diff에서 `?:` 연산자 사용 없음. 모든 조건분기가 if-else 형태로 작성됨.

**C# 7.2 준수:**
switch expression, nullable reference type, record 등 C# 8.0+ 기능 사용 없음.

**//260626 hbk 주석 — 준수:**
신규/수정된 코드 전 라인에 주석 포함 확인.

---

_Reviewed: 2026-06-26_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
