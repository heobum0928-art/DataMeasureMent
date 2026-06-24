# Phase 61: UI — TabControl (D) - Context

**Gathered:** 2026-06-24
**Status:** Ready for planning
**Mode:** `--auto` (사용자 외부, "자동진행 검증 직전까지"). Claude 권장 설계 + 근거 문서화.

<domain>
## Phase Boundary

v1.3 Align 비전의 **UI 통합 계층**. MainWindow 에 TabControl([검사]/[Tray]/[Bottom])을 추가하고, 기존 MainView 를 [검사] 탭으로 이동하며, Tray/Bottom 비전 뷰(툴바+티칭+결과+Bottom 캘)를 추가해 phase 58/59/60 서비스에 배선한다. **이 phase 가 밀린 UAT 13건(58:4+59:5+60:4)을 실측할 버튼을 제공한다.**

**🔒 핵심 anti-goal (이 UI phase 의 가장 중요한 제약):**
- **MainView.xaml + MainView.xaml.cs(~3230줄) = 0줄 수정.** 기존 Grabber 검사 UI 는 손대지 않고, MainWindow.xaml 의 `<ui:MainView x:Name="mainView" .../>`(line 70) **요소를 TabControl 의 [검사] TabItem 안으로 재부모화만** 한다 (회귀 0 핵심).
- **유일하게 허용되는 기존 파일 편집 = MainWindow.xaml(TabControl 래퍼) + MainWindow.xaml.cs(탭 Visibility 배선).** 이는 AV-07 이 명시적으로 요구하는 통합 지점.
- `MainResultViewerControl`(HALCON 뷰어), 기존 Sequence/Action/카메라/phase 58~60 서비스 = **무수정**(consume only, modify 0).

**범위 밖:** $RESULT TCP(Phase 62/63 — 이미 63 완료). 측정 알고리즘 변경. UAT(런타임 — 실 카메라/이미지 필요 → 검증 직전 정지).

**공통 제약:** 헝가리언 · C# 7.2 · Halcon try-catch · 함수 30줄 · 매직넘버 const · 이더넷 실패해도 Grabber/UI 정상 · EthernetVisionMode=None 이면 Tray/Bottom 탭 숨김+비활성.
</domain>

<decisions>
## Implementation Decisions

### MainWindow TabControl 래퍼 (D-01)
- **D-01:** MainWindow.xaml line 70 의 `<ui:MainView x:Name="mainView" Grid.Column="0" Grid.Row="0"/>` 를 **TabControl 로 교체**. TabControl(Grid.Row=0) 안에 3 TabItem: **[검사]** = 기존 `<ui:MainView x:Name="mainView"/>` 그대로 이동(x:Name 보존 → MainWindow.xaml.cs 의 mainView 참조 무손상), **[Tray 비전]** / **[Bottom 비전]** = 신규 뷰. line 71~72(GridSplitter + LogView)는 Row 1~2 그대로. 우측 InspectionListView 컬럼 무변경.
- MainView.xaml.cs 변경 0 — 단지 XAML 트리에서 부모만 Grid→TabItem 으로 바뀜(x:Name 동일이라 code-behind 참조 정상).

### Tray/Bottom 비전 뷰 (D-02)
- **D-02:** 신규 `TrayVisionView` + `BottomVisionView` UserControl(`WPF_Example/Custom/UI/` 권장). 레퍼런스 ShotTabView/MainView 패턴 + 현재 MainResultViewerControl 차용. 구성(AV-08):
  - **툴바**: Grab(`EthernetVisionHandler.Handle.Camera.Grab()` → viewer.LoadImage) / Live(Camera.Live=StartStream 루프) / Stop(Camera.Stop).
  - **티칭 패널**: ROI 2개 그리기(`viewer.StartRectangleDrawing()`×2 → CommitActiveRectangle) → `Matcher.TryTeach(img, roi1, roi2, mode, out err)` → 저장 상태(HasTemplate) 표시.
  - **결과 패널**: 검사(Run) 버튼 → `Matcher.Run(img, mode)` → AlignResult 표시(OffsetX/Y, Bottom 만 Theta, Score, Found).
  - **(Bottom 전용) 캘 패널**: 36-스텝 — `PickerCal.Reset()` → 스텝마다 grab+`TryAddStep` → `TryComputePickerCenter` → 피커센터 표시. (외부 회전은 PLC; UI 는 스텝 트리거/누적/계산 버튼.)
  - **상태 표시**: 대기 / LIVE / 검사중 / 미연결 (Camera 상태 + EthernetVisionHandler.IsInitialized).

### HALCON 뷰어 공용 (D-03) — AV-08 "공용"
- **D-03:** **단일 공유 `MainResultViewerControl` 인스턴스**를 align 에 사용(MainResultViewerControl 클래스 재사용, 무수정). EthernetVisionMode 가 None/Tray/Bottom **상호배타**라 align 탭은 한 번에 하나만 보임 → 공유 1 뷰어로 충분(다중 HWindowControlWPF 회피). 구현: 공유 뷰어를 활성 align 뷰의 viewer-host 에 배치(planner 가 WPF 부모 처리 방식 확정 — 모드 배타라 실질적으로 1 align 뷰만 실재화).
- **airspace 제약(필수)**: HWindowControlWPF 는 네이티브 HWND → 그 위에 WPF 툴바/상태/배지를 ZIndex 오버레이하면 가려짐. **툴바/패널/상태는 뷰어와 별도 Grid 행 또는 사이드 패널**(HWND 밖)에 배치. (MainView 가 Row0=툴바 / Row1=HWindow 분리한 패턴 동일.) 참조 [[feedback_halcon_hwnd_airspace]].

### 탭 Visibility (모드 게이트) (D-04)
- **D-04:** `SystemSetting.Handle.EthernetVisionMode` 기준:
  - None → Tray/Bottom 탭 모두 Collapsed (=[검사]만).
  - Tray → [검사] + [Tray] visible, [Bottom] Collapsed.
  - Bottom → [검사] + [Bottom] visible, [Tray] Collapsed.
  - 적용 시점: MainWindow Loaded + 설정창(SettingWindow) 닫힌 후 모드 변경 반영. TabItem.Visibility code-behind 제어(MainWindow.xaml.cs).

### 서비스 배선 (D-05)
- **D-05:** 전 호출은 싱글턴 직접: `EthernetVisionHandler.Handle.{Camera,Matcher,PickerCal,IsInitialized}`, `SystemSetting.Handle.EthernetVisionMode`. UI 는 서비스 wrapping facade — 측정/매칭/캘 로직 0(전부 58~60 서비스에 위임). 모든 호출 try-catch(실패해도 UI/Grabber 정상). 이더넷 미연결 시 Grab 은 D:\align_test.bmp 폴백(phase 58 동작) + 상태=미연결.

### Claude's Discretion
- 정확한 뷰 파일 위치/명, 툴바 버튼 레이아웃/아이콘, 공유 뷰어 WPF 부모 처리 메커니즘(모드 배타 전제), 상태 라벨 문구, Live 스트림 루프 구현(StartStream 위임), 2-ROI 티칭 UX 세부(슬롯/순서).

</decisions>

<canonical_refs>
## Canonical References

### v1.3 요구사항 / 로드맵
- `.planning/ROADMAP.md` §"Phase 61: UI — TabControl (D)" + v1.3 공통 제약.
- `.planning/REQUIREMENTS.md` line 115~116 (AV-07/08).
- 58/59/60 CONTEXT (서비스 계약): `.planning/phases/58.../58-CONTEXT.md`, `59.../59-CONTEXT.md`(+revision), `60.../60-CONTEXT.md`.

### 현재 코드 (재사용/통합 지점)
- `WPF_Example/MainWindow.xaml` line 70 `<ui:MainView x:Name="mainView" Grid.Row="0"/>` — **TabControl 래퍼 삽입 지점**. line 71~72 GridSplitter+LogView 유지. MainWindow.xaml.cs `PopupView(EPageType.Setting)`(SettingWindow), Loaded.
- `WPF_Example/UI/ContentItem/MainView.xaml` + `.cs`(~3230줄) — **재부모화만, 0줄 수정**. 자체완결 UserControl.
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml` + `.cs` — HALCON 뷰어(HWindowControlWPF). public: `LoadImage(HImage)`, `StartRectangleDrawing()`/`CommitActiveRectangle()`, `SetRois`, `RectDrawingCompleted` 이벤트. **무수정 재사용**. airspace 주의.
- `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` — `Handle.Camera`(Grab/Live/Stop), `.Matcher`(TryTeach/Run/HasTemplate), `.PickerCal`(Reset/TryAddStep/TryComputePickerCenter), `.IsInitialized`.
- `WPF_Example/Custom/SystemSetting.cs` — `Handle.EthernetVisionMode`(None/Tray/Bottom).

### 구조 참조 (외부)
- `D:\Backup\파이널비전\WPF_Example_260604` — TabControl 패턴(MainView TabControl, TabItem 헤더, SelectionChanged, ItemContainerStyle Visibility), ShotTabView(툴바+티칭 패널 레이아웃), StatusBar. **단 뷰어는 RuntimeResizer(OpenCV)라 현재 프로젝트는 MainResultViewerControl(HALCON) 사용.** Tray/Bottom align 뷰는 신규(패턴만 차용).

> 외부 UI-SPEC 별도 미생성 — 디자인은 현 MainResultViewerControl + 레퍼런스 TabControl 패턴 + airspace 제약으로 이 CONTEXT 에 고정("기존 패턴 확장").

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainView`(UserControl): x:Name 보존 재부모화 → MainWindow.xaml.cs 참조 무손상, 0줄 수정.
- `MainResultViewerControl`: HALCON 뷰어 + ROI 드로잉(StartRectangleDrawing/CommitActiveRectangle) → Tray/Bottom 티칭 2-ROI 재사용. 무수정.
- `EthernetVisionHandler.Handle` / `SystemSetting.Handle`: 서비스/모드 싱글턴.
- StatusBar.Model.SetText(): 범용 상태 메시지 sink(무변경).
- 레퍼런스 ShotTabView/MainView TabControl + 툴바 레이아웃 패턴.

### Established Patterns
- TabControl + TabItem(UserControl host) + SelectionChanged + ItemContainerStyle Visibility.
- Grab 패턴: MainView.GrabAndDisplay(`pDev.GrabHalconImage`) → align 은 `EthernetVisionHandler.Handle.Camera.Grab()` 미러.
- airspace: 네이티브 HWND 위 WPF 오버레이 금지 → 툴바/상태 별도 행.

### Integration Points
- MainWindow.xaml: line 70 MainView → TabControl(3 TabItem) 교체. MainWindow.xaml.cs: 탭 Visibility(모드) + (선택) 탭 전환 시 뷰 init.
- 신규 파일: TrayVisionView.xaml(+.cs), BottomVisionView.xaml(+.cs) (Custom/UI/), csproj 등록.
- MainView.xaml/.cs, MainResultViewerControl, Grabber, 58~60 서비스: **무수정**.

</code_context>

<specifics>
## Specific Ideas
- "신규 설계 말고 기존 패턴 확장": TabControl/뷰어/툴바 전부 기존 패턴, 신규는 align 2 뷰 + 서비스 배선뿐.
- 이 phase 완료 = 밀린 UAT 13건(58:4+59:5+60:4) 실측 가능 → 61 후 58/59/60/61 UAT 일괄.
- 공유 뷰어/airspace 가 이 phase 의 기술적 핵심 리스크.

</specifics>

<deferred>
## Deferred Ideas
- $RESULT TCP(TRAY/BOTTOM) → Phase 62 = **Phase 63 으로 흡수 완료**(별도 세션). 61 의 Run/Cal 결과를 63 TCP 응답에 연결하는 통합 검증은 UAT/후속.
- 런타임 UAT(실 카메라 grab/live, 실 ROI 티칭, 실 매칭/캘) → Phase 61 완료 후 일괄(58~61).

</deferred>

---

*Phase: 61-ui-tabcontrol-d-2026-06-24*
*Context gathered: 2026-06-24 (--auto, Claude-decided; UI design pinned by existing patterns + scout)*
