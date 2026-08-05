# Quick Task 260805-ivy: Manual Measure(Caliper) 수평/수직 고정 모드 추가 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning (사용자가 이미 근본원인/구현 방법을 정확히 지정함 — 재조사 불필요)

<domain>
## Task Boundary

`MainResultViewerControl`(WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs)의 "Manual Measure" 기능(우클릭 메뉴 → 체크 → 이미지 위 두 점 클릭 → 유클리드 거리(px) 표시, 레시피/저장과 무관한 순수 UI 유틸리티)에 수평/수직 고정 모드를 추가한다. 지금은 두 번째 점 찍을 때 X/Y에 제약이 없어 자유방향 거리만 나온다.

이 클래스는 메인 뷰(`MainView.xaml:416` halconViewer)와 Align 전용 뷰(`_alignViewer`) 둘 다에서 공유되므로 이 한 클래스만 고치면 두 화면 모두 반영된다.

</domain>

<decisions>
## Implementation Decisions (사용자가 파일:라인까지 지정 — LOCKED)

### 신규 enum
- `ECaliperMode.cs` 신규 파일(1파일 1enum, E접두 관례 — `EImageSource.cs`/`EBottomAlignSlot.cs`/`EEthernetVisionMode.cs` 참고):
  ```csharp
  public enum ECaliperMode { Free, Horizontal, Vertical }
  ```

### 필드 추가
- `MainResultViewerControl.xaml.cs` 88-97번째 줄 근처: `private ECaliperMode _manualMeasureAxisMode = ECaliperMode.Free;`

### 핵심 로직
- `ApplyManualMeasurePoint`(:1669-1682)의 else 분기(끝점 설정 시점), `imagePoint` 대입 **직전**에 if-else 분기:
  - `Horizontal`이면 `imagePoint.Y = _manualMeasureStartPoint.Value.Y`로 치환 후 대입
  - `Vertical`이면 `imagePoint.X = _manualMeasureStartPoint.Value.X`로 치환 후 대입
  - `Free`면 기존 그대로
- `GetDistance`(:1888-1893)는 **수정 불필요** — 좌표가 이미 축 정렬되면 deltaX 또는 deltaY가 0이 되어 자동으로 올바른 거리가 나온다.

### UI
- `MainResultViewerControl.xaml`의 `ViewerContextMenu`(16-44번째 줄) `ManualMeasureMenuItem` 옆에 Free/Horizontal/Vertical 3-way 체크 가능 서브메뉴 추가.
- `UpdateContextMenuState()`(:1705-1746)에서 체크 상태 동기화 — 기존 `CrosshairMenuItem`의 `IsCheckable=true` 패턴 참고.

### 모드 전환 시 리셋
- 모드 전환 시 이미 시작점만 찍힌 상태라면 **리셋 권장**(모드가 바뀌면 이전 시작점의 축 기준이 무의미해지므로) — `ResetManualToolState()`(:2037-2043) 패턴 그대로 사용.

### Claude's Discretion
- 서브메뉴의 정확한 XAML 구조(RadioButton 스타일 MenuItem 3개 vs 별도 서브메뉴)는 기존 컨텍스트 메뉴 스타일에 맞춰 구현 재량.

</decisions>

<specifics>
## Specific Ideas

- 영향 범위: `MainResultViewerControl` 한 클래스(+enum 파일 1개)만 수정하면 메인 뷰/Align 뷰 둘 다 반영됨 — 별도 파일 수정 불필요.
- 이 기능은 다른 레시피/측정 파라미터와 완전히 무관하고 저장도 안 되는 순수 UI 유틸리티 — 회귀 위험이 낮은 격리된 작업.

</specifics>

<canonical_refs>
## Canonical References

No external specs — 사용자가 근본원인과 구현 방법을 파일:라인 단위로 이미 확정해 전달함.

</canonical_refs>
