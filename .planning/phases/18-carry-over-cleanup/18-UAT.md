---
phase: 18-carry-over-cleanup
type: uat
status: pending
created: 2026-05-05
updated: 2026-05-05
summary:
  total: 6
  passed: 0
  failed: 0
  not_tested: 6
  skipped: 0
---

# Phase 18 UAT — Carry-over 정리 (CO-01/CO-03/CO-04/CO-05/CO-06)

## 개요

Phase 17 partial sign-off 에서 이관된 5개 CO 항목 검증.

| Test | CO | 내용 |
|------|----|------|
| Test 1 | CO-01 | Circle_RadialDirection PropertyGrid ItemsSource (Inward/Outward 전용) |
| Test 2 | CO-04-A | "ROI 다시 그리기" 컨텍스트 메뉴 표시 조건 |
| Test 3 | CO-04-B | "ROI 다시 그리기" 실행 동작 |
| Test 4 | CO-05 | Circle Strip 성공/실패 색상 시각화 |
| Test 5 | CO-06 | FormatTeachError [DatumName] 접두사 |
| Test 6 | CO-03 | btn_teachDatum IsConfigured 게이팅 사양 명문화 |

---

### Test 1 — CO-01: Circle_RadialDirection PropertyGrid ItemsSource

**단계**:
1. 애플리케이션 시작 → InspectionListView 에서 AlgorithmType=CircleTwoHorizontal Datum 선택
2. PropertyGrid 에서 Circle_RadialDirection 필드의 드롭다운 클릭

**기대**:
- 드롭다운에 "Inward" / "Outward" 두 항목만 표시
- "LtoR", "RtoL", "TtoB", "BtoT" 항목 없음

**Acceptance**:
- `grep -c "Circle_RadialDirectionList" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` ≥ 2
- `grep -c "allNoFilter" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` ≥ 1
- 런타임: 콤보박스 항목 수 = 2

**source**: CO-01
**result**: not_tested
**notes**:

---

### Test 2 — CO-04-A: "ROI 다시 그리기" 컨텍스트 메뉴 표시 조건

**단계**:
1. CTH Datum 선택 → btn_teachDatum ON (TeachDatum 모드 진입)
2. 캔버스에서 Datum Circle ROI 위에 우클릭

**기대**:
- ContextMenu 에 "ROI 다시 그리기" 항목이 Visible 로 표시됨
- 빈 캔버스 위 우클릭 시 해당 항목 Collapsed (미표시)

**Acceptance**:
- `grep -c "RedrawRoiMenuItem" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml` ≥ 1
- `grep -c "IsTeachDatumMode" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` ≥ 2

**source**: CO-04
**result**: not_tested
**notes**:

---

### Test 3 — CO-04-B: "ROI 다시 그리기" 실행 동작

**단계**:
1. CTH Datum 선택 → btn_teachDatum ON
2. Datum Circle ROI 위 우클릭 → "ROI 다시 그리기" 클릭

**기대**:
- Circle ROI 오버레이가 캔버스에서 사라짐 (CircleROI_Radius = 0 리셋)
- btn_teachDatum 상태 유지 (TeachDatum 모드 OFF 되지 않음)

**Acceptance**:
- `grep -c "RoiRedrawRequested" WPF_Example/UI/ContentItem/MainView.xaml.cs` ≥ 1
- `grep -c "ClearDatumRoiFields" WPF_Example/UI/ContentItem/MainView.xaml.cs` ≥ 1

**source**: CO-04
**result**: not_tested
**notes**:

---

### Test 4 — CO-05: Circle Strip 성공/실패 색상 시각화

**단계**:
1. CTH Datum 선택 → 정상 ROI 설정 → btn_teachDatum 클릭 (성공 케이스)
2. 캔버스에서 Circle 주변 polar strip 색상 확인
3. (선택) Circle_RadialDirection 을 반대로 설정 후 재티칭 → 실패 strip 색상 확인

**기대**:
- 에지 검출 성공 strip: 녹색 (green)
- 에지 검출 실패 strip: 빨강 (red)
- CircleStripSuccesses 가 null 이거나 인덱스 범위 초과 시: 회색 (gray, fallback)

**Acceptance**:
- `grep -c "CircleStripSuccesses" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` ≥ 1
- `grep -c "stripColor" WPF_Example/Halcon/Display/HalconDisplayService.cs` ≥ 3

**source**: CO-05
**result**: not_tested
**notes**:

---

### Test 5 — CO-06: FormatTeachError [DatumName] 접두사

**단계**:
1. CTH Datum 이름 = "Datum 2" 확인 (PropertyGrid DatumName 필드)
2. Circle_RadialDirection 을 의도적으로 반대로 설정 → btn_teachDatum 클릭 (검출 실패 유도)
3. 실패 모달 텍스트 확인

**기대**:
- 모달 타이틀: "티칭 실패"
- 모달 메시지: "[Datum 2] 검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요." (또는 해당 에러 메시지의 [DatumName] 접두사 포함)

**Acceptance**:
- `grep -c "datum.DatumName" WPF_Example/UI/ContentItem/MainView.xaml.cs` ≥ 1
- `grep -c "DatumConfig datum, string err" WPF_Example/UI/ContentItem/MainView.xaml.cs` = 1

**source**: CO-06
**result**: not_tested
**notes**:

---

### Test 6 — CO-03: btn_teachDatum IsConfigured 게이팅 사양 명문화

Phase 17 Test 10 SKIP 항목을 올바른 사양으로 재작성. 코드 변경 없음 — IsConfigured 게이팅(hotfix#3)이 이미 올바른 동작을 구현한다.

**시나리오 A — IsConfigured=false (새 Datum, 첫 티칭)**:
1. 새 Datum 생성 → InspectionListView 에서 선택 (IsConfigured=false 상태)
2. AlgorithmType = CircleTwoHorizontal 설정
3. 캔버스에 ROI 를 그리지 않은 채로 btn_teachDatum 클릭

기대:
- ValidateRoiPresence 모달 표시 없음 (IsConfigured=false → 가드 스킵)
- Wizard 단계(StartDatumTeachStep) 로 즉시 진입 — 첫 ROI 그리기 안내 시작

**시나리오 B — IsConfigured=true (기존 Datum 재티칭, ROI 삭제 후)**:
1. 이미 티칭 완료된 CTH Datum 선택 (IsConfigured=true)
2. PropertyGrid 에서 CircleROI_Radius 를 0 으로 수동 설정
3. btn_teachDatum 클릭

기대:
- ValidateRoiPresence 모달 표시: Title="티칭 실패", Message="Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요."
- btn_teachDatum 자동 OFF

**Acceptance**:
- `grep -c "ValidateRoiPresence" WPF_Example/UI/ContentItem/MainView.xaml.cs` ≥ 2
- `grep -c "IsConfigured" WPF_Example/UI/ContentItem/MainView.xaml.cs` ≥ 1
- 시나리오 A: 새 Datum + ROI 미생성 + btn_teachDatum 클릭 → 모달 없이 wizard 진행
- 시나리오 B: 기존 Datum + ROI 삭제 + btn_teachDatum 클릭 → ValidateRoiPresence 모달 표시

**source**: CO-03 (D-04, D-05, D-06)
**result**: not_tested
**notes**:
