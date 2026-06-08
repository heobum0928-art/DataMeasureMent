---
phase: 19-propertygrid-dynamic-exposure
status: signed_off
signed_off: 2026-05-08
total: 6
passed: 6
failed: 0
pending: 0
fix_commits:
  - dd5bfca  # GetProperties() 무인자 오버로드 root cause fix (Phase 17 부터 broken 이었던 ICustomTypeDescriptor 동적 hide)
  - e7c5087  # FAI/Measurement 노드 전환 시 PropertyGrid force rebind (Datum D-09 패턴 적용)
  - 26639dc  # EdgeMeasureTypeList 정적 readonly cache (PropertyGrid 콤보 ItemsSource 인식)
---

# Phase 19 — UAT Report (signed_off)

Phase 19 fix 3 commit 적용 후 사용자 UAT 결과.

## Test Results

### Test 1 — DatumConfig TLI 회귀 (CO-02)
- **Expected:** AlgorithmType=TwoLineIntersect → Line1_*/Line2_* 만 노출, Circle_*/Vertical_*/Horizontal_* 숨김
- **Result:** **PASS** (2026-05-08 사용자 확인 — fix dd5bfca 후)

### Test 2 — DatumConfig CTH 회귀 (CO-02 + Phase 17 D-03 + Phase 18 CO-01)
- **Expected:** AlgorithmType=CircleTwoHorizontal → Circle_* + Horizontal_A_*/B_* 노출 (Circle_EdgeDirection 제외) + Circle_RadialDirection 콤보 Inward/Outward 2값
- **Result:** **PASS** (2026-05-08 사용자 확인 — Circle_EdgeDirection 숨김 + RadialDirection 2값 정상)

### Test 3 — DatumConfig VTH 회귀 (CO-02)
- **Expected:** AlgorithmType=VerticalTwoHorizontal → Vertical_* + Horizontal_A_*/B_* 노출, Line1_*/Line2_*/Circle_* 숨김
- **Result:** **PASS** (2026-05-08 사용자 확인)

### Test 4 — FAIConfig EdgeMeasureType 콤보 + 6개 옵션 (QUAL-03)
- **Expected:** PropertyGrid 의 "Edge|Measurement" 카테고리 안 EdgeMeasureType 행이 콤보박스 드롭다운으로 표시 + 6개 옵션 (EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/CircleDiameter/LineToLineDistance)
- **Result:** **PASS** (2026-05-08 사용자 확인 — fix 26639dc 후)
- **Notes:** fix 1·2 후에는 텍스트박스 였다가 fix 3 (EdgeMeasureTypeList 정적 readonly cache) 적용 후 콤보로 정상 표시. 매번 new List 생성이 PropertyTools.Wpf 의 ItemsSource 인식을 깨뜨린 것이 root cause.

### Test 5 — FAIConfig CircleDiameter 선택 시 6필드 hide (QUAL-03)
- **Expected:** EdgeMeasureType=CircleDiameter → Sigma/EdgeDirection/EdgePolarity/EdgeSelection/EdgeSampleCount/EdgeTrimCount 6개 + 각 *List 숨김. EdgePairDistance 등 다른 타입 선택 시 모두 노출
- **Result:** **PASS** (2026-05-08 사용자 확인)

### Test 6 — INI round-trip 회귀 (QUAL-03)
- **Expected:** EdgeMeasureType=CircleDiameter 저장/로드, INI 키 미존재 시 기본값 EdgePairDistance fallback
- **Result:** **PASS** (2026-05-08 사용자 확인 — 회귀 없음)

## 회귀 검증 (3 fix 후)

| 항목 | Result |
|---|---|
| FAIConfig EdgeDirection 콤보 (LtoR/RtoL/TtoB/BtoT 4개) | PASS |
| FAIConfig EdgePolarity 콤보 (DarkToLight/LightToDark 2개) | PASS |
| Datum/FAI 노드 전환 시 PropertyGrid 갱신 | PASS (fix e7c5087) |
| Datum AlgorithmType 콤보 + 동적 hide | PASS |
| Datum CTH RadialDirection 콤보 (Inward/Outward) | PASS (Phase 18 CO-01 회귀 없음) |

## Root Cause Analysis (3 ROOT CAUSES, separate fixes)

**Phase 19 verifier 가 PASS_WITH_NOTES 로 통과시킨 정적 검증은 실제 PropertyTools.Wpf 동작과 다른 3개의 근본 결함을 가렸다. 사용자 UAT 가 차례로 노출.**

1. **dd5bfca — ICustomTypeDescriptor.GetProperties(Attribute[]) 가 dead code**
   - PropertyTools.Wpf 의 PropertyGrid 가 무인자 GetProperties() 만 호출 (TypeDescriptor.GetProperties(object) → ICustomTypeDescriptor.GetProperties() 무인자 위임).
   - Phase 17 부터 줄곧 broken 상태였으나 Phase 18 CO-01 이 정적 [Browsable(false)] 로 한 케이스만 우회.
   - Fix: BuildFilteredProperties 헬퍼로 hide 로직 추출, 무인자 + Attribute[] 양쪽이 호출.

2. **e7c5087 — Datum 노드 force rebind 가 SelectedObject binding 손상**
   - InspectionListView.xaml line 257 의 SelectedObject binding 이 Datum 클릭 시 line 419-420 직접 할당으로 끊김.
   - Phase 18 까지 FAI 가 평범 reflection 으로 우회되었으나 ICustomTypeDescriptor 추가 후 명시적 갱신 필요.
   - Fix: FAI / Measurement 분기에 Datum 의 force rebind 패턴(_isRebinding flag + null→new) 동일 적용.

3. **26639dc — EdgeMeasureTypeList getter 가 매번 new List 생성**
   - PropertyTools.Wpf 의 ItemsSource lookup 이 매번 다른 인스턴스 반환을 처리 못 해 콤보박스로 인식 안 함.
   - DatumConfig.AlgorithmType 콤보가 동작했던 이유는 어트리뷰트 위치 + 사용 빈도 차이로 추정 (정확한 원인은 PropertyTools.Wpf 내부 로직).
   - Fix: 정적 readonly _edgeMeasureTypeListCache 단일 인스턴스 + MeasurementFactory.GetTypeNames() 단일 소스 유지.

## Out-of-scope (별도 작업으로 이관)

- **측정 추가 모달 자유 텍스트 → 콤보박스 다이얼로그 교체** (`InspectionListView.xaml.cs:692-697` `AddMeasurementToFAI` + `TextInputBox.Show("Measurement 타입 입력...")`)
  - 현재: Phase 19 와 무관한 기존 코드 (Phase 6 Plan 04, D-24)
  - 향후: Phase 19 closure 후 quick task 로 처리 (사용자 결정)

- **FAI CircleDiameter 측정에 Datum Circle 알고리즘 + 파라미터 적용** (사용자 요청)
  - 현재: Phase 19 스코프 외 (신규 기능)
  - 향후: Phase 28 후보 — FAI/Measurement UI 개선 + Datum Circle 알고리즘 재사용 검토

## 최종 판정

**Phase 19 SIGNED_OFF (2026-05-08).** QUAL-03 + CO-02 충족. 6/6 테스트 PASS, 회귀 없음. 별도 작업 2건 이관.
