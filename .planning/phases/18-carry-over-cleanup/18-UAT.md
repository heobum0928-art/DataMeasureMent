---
phase: 18-carry-over-cleanup
type: uat
status: partial
source:
  - 18-01-SUMMARY.md
  - 18-02-SUMMARY.md
  - 18-03-SUMMARY.md
  - 18-04-SUMMARY.md
  - 18-05-SUMMARY.md
created: 2026-05-05
started: 2026-05-07T00:00:00
updated: 2026-05-07T00:30:00
summary:
  total: 6
  passed: 0
  issues: 2
  pending: 0
  blocked: 4
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

**정적 acceptance(grep) 사전검증 (2026-05-07):** 12/12 PASS
- Test 1: Circle_RadialDirectionList=4(≥2) ✓ / allNoFilter=2(≥1) ✓
- Test 2: RedrawRoiMenuItem=2(≥1) ✓ / IsTeachDatumMode=2(≥2) ✓
- Test 3: RoiRedrawRequested=1(≥1) ✓ / ClearDatumRoiFields=9(≥1) ✓
- Test 4: CircleStripSuccesses=1(≥1) ✓ / stripColor=3(≥3) ✓
- Test 5: datum.DatumName=2(≥1) ✓ / DatumConfig datum, string err=1(=1) ✓
- Test 6: ValidateRoiPresence=2(≥2) ✓ / IsConfigured=11(≥1) ✓

---

## Current Test

[testing complete — session ended partial: 2 issues blocking 4 tests]

## Tests

### 1. CO-01 — Circle_RadialDirection 드롭다운 (Inward/Outward 전용)

**단계**:
1. 애플리케이션 시작 → InspectionListView 에서 AlgorithmType=CircleTwoHorizontal Datum 선택
2. PropertyGrid 에서 Circle_RadialDirection 필드의 드롭다운 클릭

**기대**:
- 드롭다운에 "Inward" / "Outward" 두 항목만 표시
- "LtoR", "RtoL", "TtoB", "BtoT" 항목 없음

**source**: CO-01
**static_acceptance**: PASS (Circle_RadialDirectionList=4, allNoFilter=2)
result: issue
reported: "Fail circle에서 LtoR 항목보임"
severity: major

### 2. CO-04-A — "ROI 다시 그리기" 컨텍스트 메뉴 표시 조건

**단계**:
1. CTH Datum 선택 → btn_teachDatum ON (TeachDatum 모드 진입)
2. 캔버스에서 Datum Circle ROI 위에 우클릭

**기대**:
- ContextMenu 에 "ROI 다시 그리기" 항목이 Visible 로 표시됨
- 빈 캔버스 위 우클릭 시 해당 항목 Collapsed (미표시)
- TeachDatum 모드 OFF 상태에서는 항목 미표시

**source**: CO-04
**static_acceptance**: PASS (RedrawRoiMenuItem=2, IsTeachDatumMode=2)
result: issue
reported: "btn_teachDatum 클릭 시 외관상 무반응 (IsConfigured=true & ROI 모두 존재 시 silent re-teach). 코드 추적 후 enhancement 로 재분류 — 재티칭 확인 모달 추가."
severity: minor
reclassified: "blocker → minor (실제로는 silent re-teach 동작 중. 사용자 피드백 부재가 진짜 문제)."

### 3. CO-04-B — "ROI 다시 그리기" 실행 동작

**단계**:
1. CTH Datum 선택 → btn_teachDatum ON
2. Datum Circle ROI 위 우클릭 → "ROI 다시 그리기" 클릭

**기대**:
- Circle ROI 오버레이가 캔버스에서 사라짐 (CircleROI_Radius = 0 리셋)
- btn_teachDatum 상태 유지 (TeachDatum 모드 OFF 되지 않음)

**source**: CO-04
**static_acceptance**: PASS (RoiRedrawRequested=1, ClearDatumRoiFields=9)
result: blocked
blocked_by: prior-issue
reason: "Test 2 의 btn_teachDatum 미동작 이슈에 의해 종속 차단. TeachDatum 모드 진입 자체가 안 되어 ROI 다시 그리기 메뉴 발현/실행 검증 불가."

### 4. CO-05 — Circle Strip 성공/실패 색상 시각화

**단계**:
1. CTH Datum 선택 → 정상 ROI 설정 → btn_teachDatum 클릭 (성공 케이스)
2. 캔버스에서 Circle 주변 polar strip 색상 확인
3. (선택) Circle_RadialDirection 을 반대로 설정 후 재티칭 → 실패 strip 색상 확인

**기대**:
- 에지 검출 성공 strip: 녹색 (green)
- 에지 검출 실패 strip: 빨강 (red)
- CircleStripSuccesses 가 null 이거나 인덱스 범위 초과 시: 회색 (gray, fallback)

**source**: CO-05
**static_acceptance**: PASS (CircleStripSuccesses=1, stripColor=3)
result: blocked
blocked_by: prior-issue
reason: "Test 2 의 btn_teachDatum 미동작 이슈에 의해 종속 차단. 티칭 자체가 실행되지 않아 strip 색상 시각화 검증 불가."

### 5. CO-06 — FormatTeachError [DatumName] 접두사

**단계**:
1. CTH Datum 이름 = "Datum 2" 확인 (PropertyGrid DatumName 필드)
2. Circle_RadialDirection 을 의도적으로 반대로 설정 → btn_teachDatum 클릭 (검출 실패 유도)
3. 실패 모달 텍스트 확인

**기대**:
- 모달 타이틀: "티칭 실패"
- 모달 메시지: "[Datum 2] 검출된 에지가 없습니다. ..." (또는 해당 에러 메시지의 [DatumName] 접두사 포함)

**source**: CO-06
**static_acceptance**: PASS (datum.DatumName=2, DatumConfig datum, string err=1)
result: blocked
blocked_by: prior-issue
reason: "Test 2 의 btn_teachDatum 미동작 이슈에 의해 종속 차단. 티칭이 실행되지 않으면 실패 모달 자체가 뜨지 않아 [DatumName] 접두사 검증 불가."

### 6. CO-03 — btn_teachDatum IsConfigured 게이팅 (2 시나리오)

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

**source**: CO-03 (D-04, D-05, D-06)
**static_acceptance**: PASS (ValidateRoiPresence=2, IsConfigured=11)
result: blocked
blocked_by: prior-issue
reason: "Test 2 의 btn_teachDatum 미동작 이슈에 의해 종속 차단. btn_teachDatum 클릭 자체가 무반응이라 IsConfigured=false/true 게이팅 시나리오 검증 불가. 단, 이번 미동작 자체가 IsConfigured 게이팅 버그일 가능성도 있음 (Test 2 diagnose 결과에 따라 본 테스트 재정의 가능)."

---

## Summary

total: 6
passed: 0
issues: 2
blocked: 4
pending: 0
skipped: 0

## Gaps

- truth: "Circle_RadialDirection PropertyGrid 드롭다운에 Inward/Outward 두 항목만 표시되어야 함 (LtoR/RtoL/TtoB/BtoT 미표시)"
  status: failed
  reason: "User reported: Fail circle에서 LtoR 항목보임. 정적 grep 통과(Circle_RadialDirectionList=4, allNoFilter=2)에도 불구하고 런타임에 LtoR 항목 노출. ROOT CAUSE (gsd-debugger H1, 2026-05-07): PropertyTools.Wpf 가 [ItemsSourceProperty] resolve 시 GetProperties(Attribute[]) 반환이 아니라 owner.GetType().GetProperty(name) 직접 reflection 사용 → CO-01 의 GetProperties whitelist 패턴은 wrong layer (no-op fix). PropertyTools 가 fallback 으로 Circle_EdgeDirectionList(4항목)를 잡는 것으로 추정."
  severity: major
  test: 1
  artifacts:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (L239-249, L548-587)
  missing:
    - "올바른 ItemsSource resolution 메커니즘 (PropertyTools 가 인식하는 형태)"
    - "Circle_RadialDirectionList getter 호출 여부 검증 로그"
  fix_direction: "GetProperties whitelist 패턴 폐기. 옵션 (b) 가장 적은 변경: *List 프로퍼티들의 [Browsable(false)] 제거 + IsHiddenForAlgorithm 에 *List 이름 추가하여 PropertyGrid 노출 차단. 옵션 (a)/(c): PropertyTools 자체 ItemsSource attribute 사용 또는 custom PropertyDescriptor."

- truth: "기존 티칭된 Datum 에서 btn_teachDatum 클릭 시 사용자에게 명시적 피드백(모달 또는 시각 변화) 제공"
  status: enhancement
  reason: "User reported: btn_teachDatum 버튼이 안먹는다. 코드 추적 결과 사실은 silent re-teach 가 동작 중 — IsConfigured=true & 모든 ROI 존재 → ValidateRoiPresence null → GetFirstMissingStep=Done → InvokeTryTeachDatum 즉시 호출 → 같은 위치에서 ROI 재검출 → 같은 오버레이 표시 → ExitCanvasMode → 외관상 무반응. 사용자가 의도와 동작 차이 인지 불가. 사용자 제안: '재티칭하시겠습니까?' 확인 모달 추가."
  severity: minor
  test: 2
  artifacts:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs (TeachDatumButton_Click L1342-1392, IsConfigured 가드 L1365-1376)
  missing:
    - "재티칭 의사 확인 모달 (Yes=진행 / No=취소)"
    - "확인 모달 위치: ValidateRoiPresence null 통과 직후 + Done 단계 진입 직전 (즉시 teach 시나리오에 한해)"
  fix_direction: "TeachDatumButton_Click 에서 datum.IsConfigured && all ROI present 케이스를 명시적으로 분기. CustomMessageBox.Show('재티칭', '이 Datum 은 이미 티칭되어 있습니다.\\n다시 티칭하시겠습니까?', YesNo). No → btn_teachDatum.IsChecked=false; ExitCanvasMode(); return. Yes → 기존 InvokeTryTeachDatum 흐름 유지. 부수 효과: 사용자에게 '버튼 먹힘' 시각 신호 제공 + 의도치 않은 재티칭 방지."
