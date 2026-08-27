---
phase: 74-pattern-model-brush-masking
plan: 03
status: complete
date: 2026-08-27
---

# 74-03 SUMMARY — 브러시 ViewModel + 사이드 패널

## 확정 공개 계약 (Plan 04/05 가 이대로 배선한다)

```csharp
// namespace ReringProject.UI
public class PatternBrushMaskViewModel : INotifyPropertyChanged
{
    public Func<IList<string>> ModelPathsProvider { get; set; }  // 마스크를 붙일 모델 파일 경로 목록
    public Func<string> ModelRegenerator { get; set; }           // 성공=null, 실패=오류 문자열

    public void Attach(MainResultViewerControl viewer);
    public void Detach();

    public bool IsMaskEnabled { get; set; }   // SystemSetting.UsePatternBrushMask 와 양방향
    public bool IsBrushActive { get; set; }
    public bool IsEraseMode { get; set; }
    public double BrushRadiusPx { get; set; }
    public string StatusText { get; }

    public void ClearMask();
    public void ReloadMaskFromDisk();
    public void RefreshStatus();
}

public partial class PatternBrushPanel : UserControl
{
    public PatternBrushMaskViewModel ViewModel { get; private set; }  // 생성자에서 만들어 DataContext 로 건다
}
```

호스트 XAML 사용법:
```xml
xmlns:ui="clr-namespace:ReringProject.UI"
<ui:PatternBrushPanel x:Name="brushPanel" Margin="0,0,0,8"/>
```

## 설계 요점

- **호스트는 훅 2개 + `Attach(viewer)` 만 채우면 된다.** 저장·재생성·상태문구는 전부 VM 이 처리한다.
- **저장이 재생성보다 먼저**인 것이 코드 순서로 강제된다 (실측: `TrySaveMask` 40줄 < `ModelRegenerator()` 71줄).
  `TryCreateModel` 이 **디스크의 마스크 파일을 읽기** 때문에 순서가 뒤집히면 옛 마스크로 재생성된다.
- **마스크가 비면 마스크 파일도 삭제**한다(지우개로 다 지운 경우) — 고아 파일을 남기지 않는다.
- `ReloadMaskFromDisk` 는 **옵션이 꺼져 있어도** 이미 칠해 둔 것을 화면에 올린다
  (`TryLoadMask` 는 옵션 게이트를 타므로 여기서는 `HasMask` + 직접 `ReadRegion`).
  그래야 사용자가 "마스크가 있는데 지금 반영은 안 되는 상태" 임을 안다.
- **모달 금지** — `CustomMessageBox` 0건. 칠할 때마다 팝업이 뜨면 못 쓴다.
- `Attach` 는 `Detach()` 로 시작하고 `-=` → `+=` 순서로 구독한다(중복 구독 방지, 이 저장소 관습).

## 검증 결과

**빌드 SIMUL-ON:** 에러 **0** / 경고 **18줄** / 코드 종류 2종 — baseline 유지.
XAML 오타는 컴파일 에러로 잡히므로 이 빌드가 패널 XAML 의 실질 검증이다.

| acceptance | 기대 | 실측 |
|---|---|---|
| VM 공개 7종 | 7 | **7** ✅ |
| `class ... : INotifyPropertyChanged` | 1 | **1** ✅ |
| `BrushStrokeCompleted -=` | 2 | **2** ✅ |
| `BrushStrokeCompleted +=` | 1 | **1** ✅ |
| `PatternMaskService.TrySaveMask` | 1 | **1** ✅ |
| `PatternMaskService.DeleteMask` | 2 | **2** ✅ |
| **저장 < 재생성 순서** | 참 | **40줄 < 71줄** ✅ |
| `CustomMessageBox` (모달 금지) | 0 | **0** ✅ |
| `region.Dispose()` / `loaded.Dispose()` | 1 / 1 | **1 / 1** ✅ |
| `?:` / `??` / `?.` | 0 | **0 / 0 / 0** ✅ |
| `x:Class="ReringProject.UI.PatternBrushPanel"` | 1 | **1** ✅ |
| `public PatternBrushMaskViewModel ViewModel { get; private set; }` | 1 | **1** ✅ |
| code-behind 메서드 수 | 1 | **1** ✅ (`ClearMaskButton_Click` 뿐) |
| code-behind 줄 수 | ≤40 | **27** ✅ |
| 바인딩 줄 수 | ≥6 | **6** ✅ |
| csproj Compile 2 + Page 1 | 각 1 | **각 1** ✅ (462 / 463 / 514줄) |

## Deviations

없음. 계획대로 구현했다.

## Self-Check: PASSED

1. 빌드 에러 0, 경고 코드 종류 2종뿐 ✅
2. `PatternBrushPanel.xaml.cs` 27줄 (얇은 view) ✅
3. VM 에 `CustomMessageBox` 0건 ✅
4. 저장(`TrySaveMask`)이 재생성(`ModelRegenerator()`)보다 먼저 실행된다 ✅
5. csproj 3개 항목 추가, unstaged 유지 ✅
