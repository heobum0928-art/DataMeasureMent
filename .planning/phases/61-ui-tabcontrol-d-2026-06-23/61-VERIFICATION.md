---
phase: 61-ui-tabcontrol-d-2026-06-23
verified: 2026-06-24T00:00:00Z
status: human_needed
score: 7/7 must-haves verified
overrides_applied: 0
human_verification:
  - test: "TabControl 탭 전환 동작 — 검사/Tray 비전/Bottom 비전 탭 클릭"
    expected: "각 탭 클릭 시 해당 뷰(MainView, TrayVisionView, BottomVisionView)가 정상 렌더링되고 기존 [검사] 탭 내 검사 기능이 회귀 없이 동작한다"
    why_human: "WPF TabControl 렌더링 및 재부모화 후 MainView 동작은 런타임에만 확인 가능"
  - test: "EthernetVisionMode=None 설정 시 탭 Visibility 게이트"
    expected: "설정창에서 EthernetVisionMode=None 으로 저장 후 닫으면 [Tray 비전][Bottom 비전] 탭이 Collapsed(비표시)된다"
    why_human: "UI 탭 표시/숨김 동작은 런타임에만 확인 가능"
  - test: "EthernetVisionMode=Tray 설정 시 탭 게이트 + Grab 동작"
    expected: "EthernetVisionMode=Tray 로 설정 후 [Tray 비전] 탭이 표시되고, Grab 버튼 클릭 시 이미지가 공유 뷰어(ViewerHostBorder)에 표시된다"
    why_human: "실 카메라(또는 SIMUL_MODE 폴백 이미지) 및 공유 뷰어 attach 동작은 런타임에만 확인 가능"
  - test: "EthernetVisionMode=Bottom 설정 시 탭 게이트 + 피커센터 캘 UI 동작"
    expected: "EthernetVisionMode=Bottom 으로 설정 후 [Bottom 비전] 탭이 표시되고, 초기화/ROI 지정/스텝 추가/계산 버튼이 각각 PickerCal 서비스에 위임하여 lbl_calStatus/lbl_pickerCenter 를 갱신한다"
    why_human: "PickerCal 서비스 연동 및 캘 결과 표시는 런타임에만 확인 가능"
  - test: "기존 검사 탭(MainView) 회귀 없음 — 재부모화 후 MainView 기능 전체"
    expected: "TabControl 추가 후 [검사] 탭 내 MainView 의 ROI 편집/티칭/FAI 측정/결과 표시 등 기존 기능이 모두 정상 동작한다"
    why_human: "MainView 기존 기능 회귀 여부는 런타임 실측이 필요하다 (코드 변경 0 이지만 XAML 트리 재부모화 후 WPF 바인딩/이벤트 경로 변경 가능성 배제 필요)"
---

# Phase 61: UI — TabControl (D) 검증 보고서

**Phase 목표:** MainWindow TabControl([검사]/[Tray]/[Bottom]) 통합, 기존 MainView=[검사] 탭 이동, 모드별 탭 Visibility, Tray/Bottom 비전 뷰 + 공유 HalconViewer
**검증일:** 2026-06-24
**상태:** human_needed
**재검증:** 없음 — 초기 검증

---

## 목표 달성 여부

### 관찰 가능 진실 (Observable Truths)

| # | 진실 | 상태 | 근거 |
|---|------|------|------|
| 1 | MainWindow Contents 영역에 3-탭 TabControl([검사]/[Tray 비전]/[Bottom 비전])이 존재한다 | ✓ VERIFIED | MainWindow.xaml:72 — `<TabControl x:Name="tabMain"` + 3 TabItem 확인 |
| 2 | [검사] 탭이 기존 MainView 를 x:Name="mainView" 보존 상태로 호스팅한다 (MainView.xaml/.cs 0 수정) | ✓ VERIFIED | MainWindow.xaml:74 `<ui:MainView x:Name="mainView"/>` 보존. git diff 56190d0..HEAD — MainView 0 변경 |
| 3 | EthernetVisionMode 에 따라 Tray/Bottom 탭 Visibility 가 코드로 제어된다 | ✓ VERIFIED | Custom/UI/MainWindow.cs:35-70 — SystemSetting.Handle.EthernetVisionMode 읽어 if-else 로 tabTray/tabBottom.Visibility 제어, try-catch 포함 |
| 4 | 단일 공유 MainResultViewerControl 인스턴스가 활성 align 뷰에 부착된다 | ✓ VERIFIED | Custom/UI/MainWindow.cs:21 `_alignViewer = new MainResultViewerControl()` 1회 생성, DetachAlignViewer() + AttachSharedViewer() 로 배타 부착 |
| 5 | TrayVisionView 가 Grab/Live/Stop 툴바 + 2-ROI 티칭(Matcher.TryTeach, Tray) + 검사(Matcher.Run, Tray→X/Y) + 상태 라벨을 제공한다 | ✓ VERIFIED | TrayVisionView.xaml.cs 전체 확인 — Camera.Grab/Live/Stop, Matcher.TryTeach(2-ROI param), Matcher.Run→FormatAlignResult(X/Y/Score), lbl_status 4-상태, 전 호출 try-catch |
| 6 | BottomVisionView 가 Tray facade + ThetaDeg 표시 + PickerCal 캘 패널(Reset/TryAddStep/TryComputePickerCenter)을 제공한다 | ✓ VERIFIED | BottomVisionView.xaml.cs 전체 확인 — FormatAlignResult HasTheta 분기 Theta 포함, PickerCal.Reset/TryAddStep/TryComputePickerCenter, CircleDrawingCompleted 구독(-=후+=) |
| 7 | 안티골: MainView/MainResultViewerControl/EthernetVision 서비스/Grabber 는 0 변경이다 | ✓ VERIFIED | `git diff --name-only 56190d0..HEAD -- WPF_Example/UI/ContentItem/MainView.xaml ... WPF_Example/Custom/EthernetVision/` = 빈 문자열(출력 없음). 전체 변경 파일이 화이트리스트 내에 한정됨 |

**점수:** 7/7 진실 검증됨

---

### 필수 아티팩트

| 아티팩트 | 기대 | 상태 | 세부사항 |
|---------|------|------|---------|
| `WPF_Example/Custom/UI/TrayVisionView.xaml` | airspace-safe 레이아웃, ViewerHostBorder | ✓ VERIFIED | 2-컬럼 Grid, ViewerHostBorder Border:156, 5개 버튼 그룹, 뷰어 직접 인스턴스화 없음 |
| `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` | AttachSharedViewer, Camera/Matcher 배선 facade | ✓ VERIFIED | AttachSharedViewer:48, Grab:73, Live:97, Stop:117, TryTeach:196, Run:228, try-catch 전체 |
| `WPF_Example/Custom/UI/BottomVisionView.xaml` | TrayVisionView + Theta 결과 + 피커센터 캘 패널 | ✓ VERIFIED | Tray 동일 구조 + Row5 캘 GroupBox(btn_calReset/DrawRoi/AddStep/Compute + lbl_calStatus/lbl_pickerCenter) |
| `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` | Tray facade + Theta + PickerCal + CircleDrawingCompleted | ✓ VERIFIED | VIEW_MODE=Bottom, FormatAlignResult HasTheta 분기, PickerCal 3-메서드, CircleDrawingCompleted 구독 |
| `WPF_Example/MainWindow.xaml` | 3-탭 TabControl 래퍼, customui xmlns, mainView 보존 | ✓ VERIFIED | xmlns:customui:10, TabControl:72, tabInspection/tabTray/tabBottom:73-81, mainView:74, logView:84 |
| `WPF_Example/Custom/UI/MainWindow.cs` | RegisterCustomUI, RefreshEthernetVisionTabs, DetachAlignViewer | ✓ VERIFIED | RegisterCustomUI:18, RefreshEthernetVisionTabs:30, DetachAlignViewer:83, try-catch:73 |
| `WPF_Example/DatumMeasurement.csproj` | Tray/Bottom View Page + Compile 등록 | ✓ VERIFIED | Page:454-460, Compile DependentUpon:421-426, 백슬래시 경로, 중복 없음 |

---

### 핵심 링크 검증 (Key Links)

| From | To | Via | 상태 | 세부사항 |
|------|-----|-----|------|---------|
| TrayVisionView.xaml.cs | EthernetVisionHandler.Handle.Camera | Grab/Live/Stop 직접 호출 | ✓ WIRED | TrayVisionView.xaml.cs:73,97,117 |
| TrayVisionView.xaml.cs | EthernetVisionHandler.Handle.Matcher | TryTeach + Run 직접 호출 | ✓ WIRED | TrayVisionView.xaml.cs:196,228 |
| TrayVisionView.xaml.cs | MainResultViewerControl | AttachSharedViewer → ViewerHostBorder.Child | ✓ WIRED | TrayVisionView.xaml.cs:54 |
| BottomVisionView.xaml.cs | EthernetVisionHandler.Handle.PickerCal | Reset/TryAddStep/TryComputePickerCenter | ✓ WIRED | BottomVisionView.xaml.cs:275,331,363 |
| BottomVisionView.xaml.cs | MainResultViewerControl.CircleDrawingCompleted | AttachSharedViewer 내 -= + += 구독 | ✓ WIRED | BottomVisionView.xaml.cs:65-66 |
| Custom/UI/MainWindow.cs | SystemSetting.Handle.EthernetVisionMode | RefreshEthernetVisionTabs 내 모드 읽기 | ✓ WIRED | Custom/UI/MainWindow.cs:35 |
| Custom/UI/MainWindow.cs | TrayVisionView / BottomVisionView | AttachSharedViewer(_alignViewer) | ✓ WIRED | Custom/UI/MainWindow.cs:65,69 |
| MainWindow.xaml | ReringProject.Custom.UI.TrayVisionView / BottomVisionView | TabItem 내 뷰 인스턴스화 (xmlns:customui) | ✓ WIRED | MainWindow.xaml:10,77,80 |
| MainWindow.xaml.cs | RefreshEthernetVisionTabs | Setting ShowDialog 직후 호출 | ✓ WIRED | MainWindow.xaml.cs:342 |

---

### 데이터 흐름 추적 (Level 4 — Dynamic Rendering Artifacts)

| 아티팩트 | 데이터 변수 | 소스 | 실 데이터 생성 | 상태 |
|---------|------------|------|--------------|------|
| TrayVisionView.xaml.cs | lbl_result.Text | Matcher.Run() → AlignResult | AlignShapeMatchService.Run() 위임 (phase 59 서비스) | ✓ FLOWING |
| TrayVisionView.xaml.cs | lbl_status.Text | Camera.Grab/Live/Stop 결과 + IsInitialized | EthernetAlignCamera 위임 (phase 58 서비스) | ✓ FLOWING |
| BottomVisionView.xaml.cs | lbl_pickerCenter.Text | PickerCal.TryComputePickerCenter() | PickerCenterCalibrationService 위임 (phase 60 서비스) | ✓ FLOWING |
| BottomVisionView.xaml.cs | lbl_calStatus.Text | PickerCal.StepCount | PickerCenterCalibrationService.StepCount 프로퍼티 | ✓ FLOWING |

서비스 자체(AlignShapeMatchService, PickerCenterCalibrationService)는 phase 59/60 에서 구현됨 — 이 phase 는 thin facade 로 위임만 한다. SIMUL_MODE 폴백 포함.

---

### 동작 스팟 체크 (Step 7b)

이 phase 는 WPF UI 컨트롤 레이어이며 msbuild 빌드 PASS(에러 0, 신규 경고 0)가 61-03-SUMMARY.md 에서 보고됨. 별도 runnable 단독 진입점 없음. 런타임 동작 검증은 Step 8 Human Verification 으로 분류.

---

### 요구사항 커버리지

| 요구사항 | 소스 Plan | 설명 | 상태 | 근거 |
|---------|---------|------|------|------|
| AV-07 | 61-03 | MainWindow 에 TabControl 을 추가해 [검사]/[Tray 비전]/[Bottom 비전] 탭으로 통합, EthernetVisionMode 탭 Visibility 제어, 기존 MainView → [검사] 탭 이동 | ✓ SATISFIED | MainWindow.xaml TabControl 확인, Custom/UI/MainWindow.cs RefreshEthernetVisionTabs, mainView x:Name 보존, git diff 0변경 |
| AV-08 | 61-01, 61-02, 61-03 | Tray/BottomVisionView 에 툴바+티칭+결과(+Bottom 캘) 패널, HalconViewer 공용 | ✓ SATISFIED | TrayVisionView/BottomVisionView 아티팩트 VERIFIED, 단일 _alignViewer attach 패턴 확인 |

**REQUIREMENTS.md 확인:** AV-07, AV-08 모두 `[x]` 체크됨(2026-06-24 업데이트).

---

### 안티패턴 탐지

코드 리뷰(61-REVIEW.md, 2026-06-24) 에서 이미 식별된 항목:

| 파일 | 위치 | 패턴 | 심각도 | 영향 |
|-----|------|------|--------|------|
| TrayVisionView.xaml.cs, BottomVisionView.xaml.cs | ValidateRois():286-298 / 429-441 | halfW/halfH 음수 미처리 (역방향 드래그 시 통과) | WR-02 경고 | HALCON TryTeach 에 음수 Length 전달 → 예외 또는 0 크기 ROI 가능성. 서비스 try-catch 로 캐치됨. |
| BottomVisionView.xaml.cs | AttachSharedViewer:56-67 | 이전 _viewer CircleDrawingCompleted 구독 해제 누락 | WR-01 경고 | 현재 _alignViewer 단일 인스턴스이므로 실 누수 없음. 향후 인스턴스 교체 시 잠재적 누수. |
| DatumMeasurement.csproj | Release\|x64 PropertyGroup | Prefer32Bit=true 불일치 | WR-03 경고 | x64 PlatformTarget 에서 무효화됨. 빌드 결과에 영향 없음. |
| DatumMeasurement.csproj | Release\|x64 PropertyGroup | LangVersion 태그 누락 | IN-02 정보 | 현재 코드에 C# 8+ 없으므로 실질 영향 없음. |
| BottomVisionView.xaml.cs | 생성자:43-46 | Unloaded 미구독 | IN-01 정보 | 현재 시나리오에서 누수 없음. 방어적 개선 권장. |

**분류:**
- WR-01/02 = 경고 (warning) — 런타임 엣지 케이스에서 발생 가능하나 주 흐름 블록 안함
- WR-03, IN-01, IN-02 = 정보 (info) — 현재 빌드/기능에 영향 없음

이 항목들이 목표 달성을 막는 것은 아니지만, 향후 phase 에서 WR-01/WR-02 수정을 권장함.

---

### 안티골 검증 (Critical Anti-Goal)

| 보호 대상 | 확인 명령 | 결과 |
|---------|---------|------|
| MainView.xaml / MainView.xaml.cs | git diff 56190d0..HEAD 출력에 해당 경로 없음 | ✓ PASS (0 변경) |
| MainResultViewerControl.xaml/.cs | git diff 56190d0..HEAD 출력에 해당 경로 없음 | ✓ PASS (0 변경) |
| Custom/EthernetVision/*.cs (5 서비스) | git diff 56190d0..HEAD 출력에 해당 경로 없음 | ✓ PASS (0 변경) |
| Custom/Sequence/**, Device/**, SystemHandler.cs | git diff 56190d0..HEAD 출력에 해당 경로 없음 | ✓ PASS (0 변경) |
| 전체 변경 범위 | git diff 56190d0..HEAD — 14개 파일 | ✓ PASS — 화이트리스트 내 (MainWindow.xaml/.cs, Custom/UI/MainWindow.cs, csproj, TrayVisionView.xaml/.cs, BottomVisionView.xaml/.cs, .planning/**) |

---

### Human Verification 필요 항목

1. **TabControl 탭 전환 동작**

   **테스트:** 앱 실행 후 [검사]/[Tray 비전]/[Bottom 비전] 탭 클릭
   **기대:** 각 탭이 정상 렌더링됨. 기존 [검사] 탭 내 MainView 기능(ROI 편집/FAI 측정 등) 회귀 없음
   **Human 이유:** WPF TabControl 재부모화 후 XAML 바인딩/이벤트 경로 동작은 런타임에만 확인 가능

2. **EthernetVisionMode 탭 게이트 동작**

   **테스트:** 설정창(Settings)에서 EthernetVisionMode 를 None/Tray/Bottom 으로 각각 변경 후 저장·닫기
   **기대:** None→[Tray]/[Bottom] Collapsed, Tray→[Tray] Visible/[Bottom] Collapsed, Bottom→[Bottom] Visible/[Tray] Collapsed
   **Human 이유:** 탭 Visibility 제어는 UI 런타임에서만 확인 가능

3. **Tray 비전 탭 — Grab + 공유 뷰어 표시**

   **테스트:** EthernetVisionMode=Tray 설정 후 [Tray 비전] 탭 → Grab 클릭
   **기대:** 이미지가 ViewerHostBorder(공유 뷰어) 영역에 표시됨. 상태 라벨 "대기" 갱신. SIMUL_MODE 에서는 D:\align_test.bmp 폴백 표시
   **Human 이유:** 실 카메라 및 공유 뷰어 attach 동작은 런타임에만 확인 가능

4. **Bottom 비전 탭 — PickerCal 캘 UI 동작**

   **테스트:** EthernetVisionMode=Bottom 설정 후 [Bottom 비전] 탭 → 초기화/ROI 지정/스텝 추가/피커센터 계산 버튼 순서로 클릭
   **기대:** lbl_calStatus 가 "누적 0" → "검색 ROI 설정됨" → "누적 N" 순서로 갱신. TryComputePickerCenter 성공 시 lbl_pickerCenter 에 좌표 표시
   **Human 이유:** PickerCal 서비스 연동 동작은 런타임에만 확인 가능

5. **기존 검사 탭(MainView) 회귀 없음 전체 확인**

   **테스트:** TabControl 추가 후 기존 [검사] 탭에서 Shot 선택/FAI 측정/결과 표시 등 기존 검사 워크플로 실행
   **기대:** 기존 검사 기능 전체 이상 없음 (회귀 0)
   **Human 이유:** MainView 코드 변경 0 이지만 XAML 트리 재부모화(Grid→TabItem) 후 WPF 이벤트/바인딩 동작 확인 필요

---

### 갭 요약

자동 검증 범위 내 갭 없음. 모든 7/7 must-have 진실 VERIFIED, 안티골 0변경 확인, csproj 등록 완료, 빌드 PASS(61-03-SUMMARY 기재).

미해결 항목:
- WR-02 (ValidateRois 음수 halfW/halfH) — 런타임 엣지 케이스. 주 흐름 블록 안함. 후속 phase 수정 권장.
- WR-01 (이전 _viewer 구독 해제 누락) — 현재 단일 _alignViewer 구조에서 실 누수 없음. 방어적 수정 권장.
- Human UAT 5건 — 코드 정합성은 VERIFIED. 58~61 일괄 런타임 UAT 예정.

---

_검증일: 2026-06-24_
_검증자: Claude (gsd-verifier)_
