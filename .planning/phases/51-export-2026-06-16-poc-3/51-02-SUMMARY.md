---
phase: 51-export-2026-06-16-poc-3
plan: 02
type: execute
wave: 2
status: code_complete_pending_uat
requirements: [BATCH-01]
---

# 51-02 SUMMARY — SHOT 다중 선택 일괄 검사 + 단일 xlsx Export (UI)

## 결과 요약

Wave 2 (UI) 코드 3태스크 구현 완료. 컴파일 0 CS errors (C# 7.2 호환). 최종 빌드 copy + 육안 UAT(Task 5)는 앱 실행중 exe 잠금으로 대기 — 앱 종료 후 재빌드 → UAT 필요.

## 변경 파일

1. **WPF_Example/UI/ControlItem/NodeViewModel.cs** — `IsChecked`(TwoWay 백킹+RaisePropertyChanged) + `IsCheckboxVisible`(NodeType==Action && Param is ShotConfig) 프로퍼티 추가. 기존 IsSelected 무변경.
2. **WPF_Example/UI/ControlItem/InspectionListView.xaml** —
   - Resources 에 `BooleanToVisibilityConverter x:Key="boolToVis"` 추가 (신규 — 기존 없음).
   - DataTemplate Grid.Column=2 에 SHOT 체크박스(IsChecked TwoWay + IsCheckboxVisible→Visibility).
   - Recipe Name Grid 3열(6*/3*/2*) → 5열(6*/2*/3*/3*/2*) 확장. btn_start Col1 유지, btn_batchRun(Col2)/btn_batchExport(Col3, IsEnabled=False) 신규, btn_RecipeSelect Col2→Col4 이동.
3. **WPF_Example/UI/ControlItem/InspectionListView.xaml.cs** —
   - 필드: `_batchAccumulated`(List<CycleResultDto>), `_batchService`(BatchRunService) — UI 인스턴스 소유, static 금지.
   - `CollectCheckedShots` 재귀 헬퍼.
   - `Btn_batchRun_Click`: 체크 SHOT 수집 → 동일 시퀀스 검증(D-02) → ComputeLocalShotIndex 로컬 인덱스 → BatchRunService.StartBatch.
   - `OnBatchComplete`: Dispatcher.Invoke → _batchAccumulated.AddRange → btn_batchExport 활성.
   - `Btn_batchExport_Click`: SaveFileDialog → ReringProject.Export.RepeatExcelExportService.Export(_batchAccumulated, recipeName, path).

## 설계 결정 기록

1. **다중 선택 = IsChecked 체크박스 방식** — PropertyTools.Wpf.TreeListBox 의 SelectionMode="Extended" 지원 불확실(PATTERNS #1)하여 컨트롤 의존성 없는 체크박스 확정. SHOT 노드(Action+ShotConfig)만 노출.
2. **Export = RepeatExcelExportService.Export 재사용** (D-06/D-08) — 신규 Export 코드 0. Phase 40 포맷 그대로. CycleResultDto 가 ReringProject.UI 네임스페이스라 code-behind 와 동일 네임스페이스 → using 불필요.
3. **_batchAccumulated 누적 유지 정책** — 명시 초기화 버튼 없음(누적 우선). 매 일괄검사 AddRange 누적. 향후 Phase 41.1(Gage R&R) 재사용 가능.
4. **UAT** — 미수행 (앱 실행중 빌드 잠금). 앱 종료 → 재빌드 → Task 5 시나리오 6항목 수행 필요.

## 빌드 노트

- `msbuild Debug|x64`: CoreCompile 0 CS errors. 잔여 경고는 전부 기존(CS0618 deprecated Top/BottomSequence, CS0162 VirtualCamera) — 본 플랜 신규 경고 0.
- 빌드 최종 실패 = MSB3021/MSB3027 (bin\x64\Debug\DatumMeasurement.exe 파일 잠금, 앱 PID 3900 + VS PID 13056 실행중). 코드 문제 아님.
