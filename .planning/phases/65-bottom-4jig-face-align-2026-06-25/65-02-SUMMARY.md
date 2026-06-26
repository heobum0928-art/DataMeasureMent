---
phase: 65-bottom-4jig-face-align-2026-06-25
plan: "02"
subsystem: UI / BottomVisionView / EthernetVision
tags: [align, bottom, slot-ui, shape-matching, AV-08, six-slot]
dependency_graph:
  requires: [65-01 (EBottomAlignSlot enum + AlignShapeMatchService slot API)]
  provides: [BottomVisionView 6슬롯 선택 UI, 슬롯별 Teach/Run/HasTemplate 연결]
  affects: [ProcessAlignTest 라우팅 (Plan 03)]
tech_stack:
  added: []
  patterns: [ComboBox Tag 패턴 enum 보관, Dictionary ROI 슬롯 보관, 함수 분리(RefreshTeachStatus)]
key_files:
  created: []
  modified:
    - WPF_Example/Custom/UI/BottomVisionView.xaml
    - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
decisions:
  - "슬롯 선택 위젯 = ComboBox(Tag=EBottomAlignSlot) — Row 시프트 없이 Row 2 GroupBox 내부 상단 삽입"
  - "슬롯별 ROI 보관 = Dictionary<EBottomAlignSlot, RoiDefinition[]> — 슬롯 전환 시 ROI 자동 복원"
  - "RefreshStatus를 RefreshTeachStatus로 분리 — 슬롯 없음/있음 분기 라벨 명확화"
  - "TryTeach 슬롯 가드(None → 조기 반환) — T-65-04 DoS 완화 구현"
metrics:
  duration: "~4 min"
  completed: "2026-06-26"
  tasks: 2
  files: 2
requirements: [AV-08]
---

# Phase 65 Plan 02: BottomVisionView 6슬롯 면별 선택 UI Summary

## One-liner

BottomVisionView 좌측 컨트롤 패널에 6슬롯 ComboBox(3D 2개 + 2D 4개) 추가 + 슬롯별 ROI 보관 + TryTeach/Run/HasTemplate 4개 호출 전부 _selectedSlot 전달.

## What Was Built

### Task 1: XAML 면 슬롯 선택 GroupBox 추가 (896e809)

`WPF_Example/Custom/UI/BottomVisionView.xaml` 수정:

- Row 2 `카메라 / 이미지 로더` GroupBox 내부 상단(WrapPanel 위)에 슬롯 선택 블록 삽입
  - `<ComboBox x:Name="cmb_slot" Width="180" SelectionChanged="SlotComboBox_SelectionChanged"/>` — 6슬롯 항목은 코드비하인드에서 채움
  - `<TextBlock x:Name="lbl_slotStatus" ...>슬롯 선택 필요</TextBlock>` — 선택 슬롯 라벨 표시
- 행 시프트 없음 — 기존 GroupBox 내부 삽입으로 RowDefinitions 무변경
- 티칭 GroupBox 헤더 갱신: `"티칭 (ROI 2개)"` → `"티칭 (선택 슬롯 · ROI 2개)"` (슬롯 종속 명시)
- 기존 위젯 16개(btn_grab/live/stop/openFolder/prevImage/nextImage/drawRoi1/drawRoi2/teach/run/chk_showRoi/chk_showEdge/btn_calReset/calDrawRoi/calAddStep/calCompute) x:Name 무변경

### Task 2: 코드비하인드 슬롯 선택/전환 + slot 인자 전달 + 슬롯별 ROI 보관 (214db6b)

`WPF_Example/Custom/UI/BottomVisionView.xaml.cs` 수정:

**필드 추가:**
- `_selectedSlot = EBottomAlignSlot.None` — 현재 선택 면 슬롯 (초기값 None=미선택)
- `_slotRois = new Dictionary<EBottomAlignSlot, RoiDefinition[]>()` — 슬롯별 ROI 쌍([0]=roi1, [1]=roi2)

**메서드 추가:**
- `PopulateSlotComboBox()` — Loaded 시 6개 슬롯 항목을 ComboBoxItem(Tag=enum)으로 채움. 3D 2개 먼저, 2D 4개 순서. 초기 선택 없음(SelectedIndex=-1)
- `SlotComboBox_SelectionChanged()` — Tag 타입 안전 캐스트 후 `_selectedSlot` 갱신 → 슬롯별 ROI 복원(_slotRois 조회) → `_drawingSlot=0` 리셋 → `lbl_slotStatus` 갱신 → `RefreshStatus()` 호출
- `RefreshTeachStatus()` — `HasTemplate(VIEW_MODE, _selectedSlot)` 호출 후 None/슬롯 분기로 `lbl_teachStatus` 갱신

**기존 메서드 수정:**
- `BottomVisionView_Loaded`: `PopulateSlotComboBox()` 추가 (Loaded 순서: 슬롯 채우기 → RefreshStatus)
- `TeachButton_Click`: (1) `_selectedSlot==None` 가드(T-65-04) (2) `TryTeach(..., VIEW_MODE, _selectedSlot, out error)` 슬롯 오버로드 호출 (3) 성공 시 `_slotRois[_selectedSlot]` 저장
- `RunButton_Click`: `Run(_viewer.CurrentImage, VIEW_MODE, _selectedSlot)` 슬롯 전달
- `RefreshStatus`: `HasTemplate` 호출 → `RefreshTeachStatus()` 위임으로 교체

**무변경 메서드 (회귀 가드):**
- `AttachSharedViewer`, `GrabButton_Click`, `LiveButton_Click`, `StopButton_Click`
- `DrawRoi1Button_Click`, `DrawRoi2Button_Click`
- `ShowRoiCheckBox_Changed`, `ShowEdgeCheckBox_Changed`
- `ApplyAlignVisualization`, `ClearAlignVisualization`
- `OnCalCircleDrawn`, `CalResetButton_Click`, `CalDrawRoiButton_Click`, `CalAddStepButton_Click`, `CalComputeButton_Click`
- `ValidateRois`, `RectToTeachParams`, `FormatAlignResult`
- `OpenFolderButton_Click`, `PrevImageButton_Click`, `NextImageButton_Click`, `LoadCurrentLoaderImage`

## Deviations from Plan

없음 — 계획서 지시대로 정확히 구현됨.

## Threat Surface Scan

T-65-03 (Tampering/cmb_slot): ComboBox 항목이 6개 고정 enum Tag 로만 구성. 외부 문자열 입력 없음. `selectedItem.Tag is EBottomAlignSlot` 타입 안전 캐스트 후 None 폴백 구현. accept 완료.

T-65-04 (DoS/슬롯 미선택 티칭): `TeachButton_Click` 진입 시 `_selectedSlot == None` 가드 → 조기 반환. 의도치 않은 단일경로 덮어쓰기 방지 완료.

## Known Stubs

없음 — 슬롯 선택/전달 UI 레이어는 완전 구현됨. Plan 03에서 ProcessAlignTest의 실제 Matcher.Run 배선이 진행됨.

## Self-Check

- BottomVisionView.xaml cmb_slot 존재: FOUND (L98)
- BottomVisionView.xaml lbl_slotStatus 존재: FOUND (L101)
- BottomVisionView.xaml SlotComboBox_SelectionChanged 연결: FOUND (L99)
- BottomVisionView.xaml.cs _selectedSlot 필드: FOUND (L50)
- BottomVisionView.xaml.cs _slotRois 필드: FOUND (L52)
- TryTeach _selectedSlot 전달: FOUND (L346)
- Run _selectedSlot 전달: FOUND (L380)
- HasTemplate _selectedSlot 전달 (TeachButton): FOUND (L350)
- HasTemplate _selectedSlot 전달 (RefreshTeachStatus): FOUND (L647)
- msbuild Debug/x64: DatumMeasurement.exe 생성, CS 에러 0
- 커밋 896e809: FOUND
- 커밋 214db6b: FOUND

## Self-Check: PASSED
