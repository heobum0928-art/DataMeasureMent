# Phase 61: UI — TabControl (D) - Discussion Log

> Audit trail only. Decisions in CONTEXT.md.

**Date:** 2026-06-24
**Mode:** `--auto` (사용자 외부, "61 자동진행 검증 직전까지"). Claude 권장 설계 + 근거 문서화.

## Auto-selected decisions
| 결정 | 선택 | 근거 |
|---|---|---|
| D-01 MainWindow 래퍼 | line 70 MainView → TabControl(3 TabItem), [검사]=기존 MainView x:Name 보존 재부모화 | MainView 자체완결 UserControl → 0줄 수정, code-behind 참조 무손상 |
| D-02 Tray/Bottom 뷰 | 신규 TrayVisionView/BottomVisionView (툴바 Grab/Live/Stop + 티칭 2-ROI + Run + Bottom 캘) | AV-08, 레퍼런스 ShotTabView 패턴 |
| D-03 뷰어 공용 | 단일 공유 MainResultViewerControl(모드 배타) + airspace(툴바 별도 행) | AV-08 "공용", 다중 HWND 회피, [[feedback_halcon_hwnd_airspace]] |
| D-04 탭 Visibility | EthernetVisionMode None/Tray/Bottom 게이트 | AV-07 |
| D-05 서비스 배선 | EthernetVisionHandler.Handle.{Camera,Matcher,PickerCal} 직접, UI=facade | 58~60 서비스 위임, 로직 0 |
| anti-goal | MainView/MainResultViewer/Grabber/58~60 서비스 무수정, MainWindow.xaml(.cs)만 편집 | 회귀 0 핵심 |

## 핵심 발견 (스카우팅)
- MainWindow.xaml line 70 = 정확한 래퍼 지점. MainView ~3230줄 자체완결(재부모화 안전). HALCON 뷰어=MainResultViewerControl(HWindowControlWPF, airspace). 서비스 싱글턴 도달 가능. 레퍼런스=TabControl 패턴(뷰어는 OpenCV라 현 HALCON 뷰어 사용).
- 61 완료 = 밀린 UAT 13건 실측 가능.

## Deferred
- $RESULT TCP=Phase 63 흡수 완료. 런타임 UAT=61 후 일괄.
