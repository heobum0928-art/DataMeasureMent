---
phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
type: uat
status: pending
created: 2026-05-03
updated: 2026-05-03
summary:
  total: 16
  passed: 0
  failed: 0
  not_tested: 16
  skipped: 0
  invalid: 0
status_note: Phase 17 UAT — Phase 16 carry-over 16항목 (#1~#3, #5, #6, #8~#18; #4/#7 제외) + Phase 17 D-01~D-16 통합 검증 대기
---

# Phase 17 UAT — Datum UX 재설계 + Circle 1-strip + Test Find DetectedOrigin + Hover

## 개요

Phase 17 가 해결한 결함 (Plan 17-01 ~ 17-03) + Phase 16 carry-over 16항목:

| Plan | 결함 cluster | 해결 |
|------|-------------|------|
| 17-01 (Cluster A) | Circle pre-teach N개 strip 인지 부담 / Circle 안→밖 vs 밖→안 PropertyGrid 누락 / EdgeDirection slot 제한 + 검출 0 hint 부재 | RenderCircleStripOverlay 단일 0° strip / Circle_RadialDirection ComboBox + EnsurePerRoiDefaults fallback / 6 *_EdgeDirection 한국어 tooltip / DatumFindingService caller polarity 매핑 (D-17 통과) |
| 17-02 (Cluster B+C) | Edit OFF 시 Circle/Polygon 항상 변형 + Rect/Circle/Polygon 비대칭 / 캔버스 진입만으로 도형 따라옴 / Delete 시 단일/전체 구분 부재 / PropertyGrid 모든 알고리즘 필드 동시 노출 / AlgorithmType 변경 시 stale + 자동 재검출 / 호환성 ROI 부재 시 무반응 / 성공 시 모달 / 실패 시 inline 라벨 | MainResultViewerControl _isEditMode 단일 gate (HitTestSelectedRoi 가드) / 좌클릭+드래그 그리기 명시 가드 / DatumConfig ICustomTypeDescriptor 동적 PropertyGrid (D-03 Circle_EdgeDirection 자동 hide) / 5-step 리셋 (force rebind + 검출 reset + ROI 보존 + 자동 재검출 X) / Delete 3-button 모달 (CustomMessageBox YesNoCancel) / ValidateRoiPresence + ClearAllDatumRoiFields helpers / FormatTeachError + FormatFindError (D-04 EdgeDirection 힌트 통합) |
| 17-03 (Cluster D) | DetectedOrigin 시각화 부재 (transient 필드 없음) / 검출 품질 메트릭 PropertyGrid 노출 부재 / 마우스 hover X/Y/Gray 라이브 표시 부재 / AlgorithmType 변경 시 DetectedOrigin 잔상 | DatumConfig 6 신규 필드 (transient 3 + 메트릭 3, [Browsable(false)]+[JsonIgnore]+[ReadOnly] 양쪽 부착) / TryFindDatum 결과 transient write-back (D-17 cumulative ≤ 11 라인 EXACT) / RenderDatumFindResult purple DispCross size=14 + 좌표 텍스트 + RefAngle 화살표 / canvasToolbar X/Y/Gray TextBlock + UpdatePointerLabel / BtnTestFindDatum_Click 성공경로 SetDatumOverlay 단일화 + RaisePropertyChanged + RefreshParamEditor / InspectionListView 5-step Step 3 wiring (DetectedOrigin 0 리셋) |

D-17 algorithm preservation 누적 (Phase 17): VisionAlgorithmService.cs = 0 라인, DatumFindingService.cs = 11 라인 (17-01 +2 / 17-02 +0 / 17-03 +9, EXACT match).

Phase 16 carry-over 16항목 (16-UAT.md "Phase 17 Carry-over" 섹션, #4/#7 제외):
- #1, #2, #3 (Circle 시각화 + RadialDirection + Circle_EdgeDirection hide)
- #5 (개별 ROI 미리보기 + 메트릭 PropertyGrid 노출 — 본 phase 메트릭만 흡수, 별도 미리보기 창은 deferred)
- #6 (성공 시 모달 X)
- #8 (좌클릭+드래그 그리기 시작)
- #9 (Circle ROI Edit 모드 결함)
- #10 (실패 시 사유 모달)
- #11 (선택된 알고리즘 파라미터만 PropertyGrid 노출)
- #12 (AlgorithmType 변경 → PropertyGrid 즉시 갱신)
- #13 (Edit 모드에서만 사이즈/이동)
- #14 (Delete ROI 단일/전체 모달)
- #15 (모든 알고리즘 동일 패턴)
- #16 (EdgeDirection 모든 옵션 + tooltip + 검출 0 힌트)
- #17 (Test Find DetectedOrigin 시각화)
- #18 (마우스 hover 좌표 + 밝기)

## 사전 조건

- [ ] Plan 17-01 commit 완료: 728ed89 (RenderCircleStripOverlay), 6b62a0a (RadialDirection + tooltips), a09aeef (caller polarity), 888f0d3 (docs)
- [ ] Plan 17-02 commit 완료: 54ba7ef (MainResultViewerControl edit gate), 645f8fa (DatumConfig ICustomTypeDescriptor), a3c8126 (InspectionListView 5-step), 2399d95 (MainView Delete + 가드 + 실패모달), 702e81a (docs)
- [ ] Plan 17-03 commit 완료: f00c72f (DatumConfig 6 필드), f1b6412 (TryFindDatum write-back), 6423068 (RenderDatumFindResult purple), b58a221 (canvasToolbar X/Y/Gray), 5b3b8ac (UpdatePointerLabel + BtnTestFindDatum + InspectionListView Step 3), 992057d (docs)
- [ ] msbuild Debug/x64 PASS, 신규 warning 0 on 수정 범위
- [ ] git diff WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (Phase 17 누적) = 0 라인 (D-17 / D-20)
- [ ] git diff WPF_Example/Halcon/Algorithms/DatumFindingService.cs 신규 코드 (Phase 17 누적, 주석 제외) ≤ 11 라인 — EXACT 11 (D-17)
- [ ] 사용자가 실 카메라 grab 또는 저장된 실 데이터 이미지 보유
- [ ] Datum 3개 보유 레시피 로드 (Datum 1 = TwoLineIntersect, Datum 2 = CircleTwoHorizontal, Datum 3 = VerticalTwoHorizontal)

---

## 시나리오 — Cluster A (Circle 시각화 + EdgeDirection)

### Test 1 — Circle pre-teach Strip 1개 표시 (D-01, carry #1)

**단계**:
1. Datum 2 (CircleTwoHorizontal) 선택
2. btn_teachDatum ON (편집 모드)
3. 캔버스에 원 ROI 그림 (적절한 반지름, RectL1Ratio/RectL2Ratio 기본값)

**기대**:
- 캔버스에 회색 strip 사각형이 **0° (3시 방향) 1개만** 표시.
- Circle_PolarStepDeg 가 10° (stepCount=36) 여도, 18° (stepCount=20) 여도, 그 외 어떤 값이어도 **1개만**.
- z-order: 원 ROI 경계(밑) → strip 사각형(위).
- 색상: gray, LineWidth=1 (Phase 16 D-01 색상 보존).

**Acceptance**:
- 화면 strip 사각형 카운트 = 1 (육안 확인).
- Circle_PolarStepDeg 값 변경에도 카운트 1 유지.

**source**: Phase 17 D-01 / Phase 16 carry #1
**result**: not_tested
**notes**:

---

### Test 2 — Circle_RadialDirection PropertyGrid 노출 (D-02, carry #2)

**단계**:
1. Datum 2 (CTH) 선택
2. PropertyGrid Circle 카테고리 확인
3. `Circle_RadialDirection` 필드의 값을 "Inward" → "Outward" 변경 후 다시 "Inward" 로 복원

**기대**:
- `Circle_RadialDirection` 필드가 ComboBox 로 표시.
- 옵션 = ["Inward", "Outward"] (PascalCase, 2개).
- 기본값 (sentinel "" → EnsurePerRoiDefaults fallback) = "Inward".
- 신규 필드가 없는 기존 INI 레시피 로드 시에도 "Inward" 자동 보충.

**Acceptance**:
- PropertyGrid Circle 카테고리에 RadialDirection ComboBox 보임.
- 옵션 2개 확인 (Inward/Outward).
- 기존 INI 레시피 로드 후 PropertyGrid 표시 시 "Inward" 자동 표시.

**source**: Phase 17 D-02 / Phase 16 carry #2
**result**: not_tested
**notes**:

---

### Test 3 — Circle_EdgeDirection 동적 hide (D-03, carry #3)

**단계**:
1. Datum 2 (CTH) 선택 → PropertyGrid Circle 카테고리 확인
2. Datum 1 (TLI) 선택 → PropertyGrid Circle 카테고리에서 Circle_* 필드들이 모두 사라짐 확인
3. Datum 2 (CTH) 재선택 → PropertyGrid Circle 카테고리 재확인

**기대**:
- CTH 분기에서 PropertyGrid 의 Circle 카테고리에 `Circle_EdgeDirection` 필드 **비표시**.
- Circle_RadialDirection, Circle_EdgeSelection, Circle_RectL1Ratio, Circle_RectL2Ratio, Circle_PolarStepDeg, Circle_Sigma, Circle_EdgeThreshold 등 다른 Circle_* 필드는 표시됨.
- TLI 선택 시 Circle_* 전체 비표시.
- DatumConfig.IsHiddenForAlgorithm("Circle_EdgeDirection", CircleTwoHorizontal) 가 true 반환 검증.

**Acceptance**:
- PropertyGrid Circle 카테고리에 Circle_EdgeDirection ComboBox 부재 (CTH 분기).
- INI 저장 시 Circle_EdgeDirection 필드 자체는 보존 (하위호환, 데이터 모델 only).

**source**: Phase 17 D-03 / Phase 16 carry #3
**result**: not_tested
**notes**:

---

### Test 4 — EdgeDirection 모든 옵션 + tooltip + 검출 0 힌트 (D-04, carry #16)

**단계**:
1. Datum 2 (CTH) 선택
2. PropertyGrid Horizontal_A 카테고리 → `Horizontal_A_EdgeDirection` ComboBox 옵션 확인
3. ComboBox 위에 마우스 hover → tooltip 확인
4. Horizontal_A_EdgeDirection 을 의도적으로 잘못된 값 ("TtoB" 또는 "BtoT") 으로 설정 → btn_teachDatum 클릭

**기대**:
- ComboBox 옵션: LtoR, RtoL, TtoB, BtoT **4개 모두 활성**.
- Hover tooltip: `"일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다."` (UI-SPEC verbatim 한국어).
- 검출 실패 (no edges) 시 모달 표시:
  - Title: `"티칭 실패"`
  - Message: `"검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요."`
- tooltip 은 6 ROI (Line1/Line2/Vertical/Circle/Horizontal_A/Horizontal_B) 모든 *_EdgeDirection 에 적용.

**Acceptance**:
- ComboBox 옵션 카운트 = 4 (LtoR/RtoL/TtoB/BtoT).
- tooltip 텍스트 한국어 verbatim 일치.
- 검출 실패 모달 텍스트 verbatim 일치 (FormatTeachError helper 출력).

**source**: Phase 17 D-04 / Phase 16 carry #16
**result**: not_tested
**notes**:

---

## 시나리오 — Cluster B (Edit 모드 + 그리기 UX)

### Test 5 — 좌클릭+드래그 그리기 시작 (D-05, carry #8)

**단계**:
1. Datum 2 (CTH) 선택
2. btn_teachDatum ON (편집 모드 진입, 캔버스 모드 = TeachDatum)
3. 마우스를 캔버스 위로 이동만 시도 (클릭 없이 hover만)
4. 좌클릭 누른 상태로 드래그
5. LeftButtonUp 으로 확정

**기대**:
- 단계 3: 진입만으로는 미리보기 도형 안 그려짐 (이전 MouseEnter 동작 폐기).
- 단계 4: 좌클릭 누른 순간부터 도형 시작, 드래그 중 크기 확장, MouseMove 가 좌클릭 가드 통과.
- 단계 5: LeftButtonUp 시 확정 → HalconViewer_DatumRect/CircleCompleted 콜백 호출.
- ViewerHost_HMouseDown 의 _isDrawingRect/_isDrawingCircle 분기에 좌클릭 명시 가드 작동: `(buttons & HalconLeftButton) != HalconLeftButton` 시 미진입.

**Acceptance**:
- 마우스 hover만으로 도형 미생성 (단계 3 PASS).
- 좌클릭+드래그 시 도형 생성 (단계 4-5 PASS).
- Rect/Circle 양쪽 동일 동작 (Polygon 은 별도 우클릭 패턴이므로 본 Test 무관).

**source**: Phase 17 D-05 / Phase 16 carry #8
**result**: not_tested
**notes**:

---

### Test 6 — _isEditMode 단일 gate Rect+Circle+Polygon (D-06, carry #9, #13)

**단계**:
1. Datum 2 (CTH) 선택 → Circle ROI + Horizontal_A + Horizontal_B 그리고 btn_teachDatum 종료
2. **Edit 모드 OFF** 상태에서:
   - Rect ROI (Horizontal_A) 위에서 좌클릭+드래그 시도
   - Circle ROI 경계/내부 위에서 좌클릭+드래그 시도
   - Polygon ROI 가 있는 FAI 노드 선택 후 폴리곤 위에서 좌클릭+드래그 시도
3. 캔버스 우클릭 → ContextMenu Edit ON 토글
4. **Edit 모드 ON** 상태에서 위 3 ROI 각각 좌클릭+드래그

**기대**:
- 단계 2 (Edit OFF): 어떤 ROI 도 hit-test 미수행 → 클릭/드래그가 ROI 에 닿지 않음 (HitTestSelectedRoi 가 `if (!_isEditMode) return null;` 가드로 즉시 반환).
- 단계 4 (Edit ON): 3 종 ROI 모두 정상 이동/리사이즈 가능.
- Rect / Circle / Polygon 모두 동일 게이트 적용 (carry #13 비대칭 해소 + carry #9 Circle 항상 변형 가능 결함 해소).

**Acceptance**:
- Edit OFF 시 ROI 변형 0 건 (Rect/Circle/Polygon 모두).
- Edit ON 시 3종 모두 변형 가능.
- 우클릭 ContextMenu 의 Edit/Edit OFF 토글 동작 보존 (Phase 13-03 SetEditMode 경로).

**source**: Phase 17 D-06 / Phase 16 carry #9, #13
**result**: not_tested
**notes**:

---

### Test 7 — Delete ROI 3-button 모달 (D-07, carry #14)

**단계**:
1. Datum 2 (CTH) 선택 → Circle + Horizontal_A + Horizontal_B 3개 ROI 모두 그림 + btn_teachDatum 종료
2. Edit 모드 ON → 임의 ROI (예: Horizontal_A) 우클릭 → ContextMenu Delete 메뉴 클릭
3. 모달 표시 확인
4. **분기 a (Yes)**: "이 ROI만 삭제" 선택 → 단일 ROI 만 사라지고 다른 ROI 보존 확인
5. **분기 b (No)**: Test 1 상태 복원 후 동일 단계 → "현재 Datum의 모든 ROI 삭제" 선택 → 현재 Datum 의 모든 ROI 사라짐 확인 (다른 Datum 의 ROI 는 보존)
6. **분기 c (Cancel)**: Test 1 상태 복원 후 동일 단계 → "취소" 선택 → 변경 없음 확인

**기대** (UI-SPEC Copywriting verbatim):
- 모달 title: `"ROI 삭제"`
- 모달 message: `"선택한 ROI 를 삭제하시겠습니까?"` (또는 본문에 [예]=단일 / [아니오]=전체 / [취소] 안내 포함)
- Yes (단일) → ClearDatumRoiFields(roiId) → 단일 ROI 만 사라짐.
- No (전체) → ClearAllDatumRoiFields(datum) → 현재 Datum 의 6 RoiId (Line1/Line2/Vertical/Circle/Horizontal_A/Horizontal_B) 일괄 0 reset, 다른 Datum 보존.
- Cancel/None → 무동작.
- LastTeachSucceeded / LastFindSucceeded 도 false 로 reset (검출 시각화 자동 clear).

**Acceptance**:
- 3 분기 모두 의도된 동작.
- CustomMessageBox.ShowConfirmation YesNoCancel 사용 (CustomMessageBox 본체 변경 0).
- 모달 title/message 한국어 verbatim 일치.

**source**: Phase 17 D-07 / Phase 16 carry #14
**result**: not_tested
**notes**:

---

## 시나리오 — Cluster C (PropertyGrid 동적 노출 + 모달 정책)

### Test 8 — AlgorithmType 별 PropertyGrid 동적 노출 (D-08/D-09, carry #11/#15)

**단계**:
1. Datum 1 (TLI) 선택 → PropertyGrid 노출 필드 확인
2. Datum 2 (CTH) 선택 → PropertyGrid 노출 필드 확인
3. Datum 3 (VTH) 선택 → PropertyGrid 노출 필드 확인

**기대** (UI-SPEC § ICustomTypeDescriptor 표):
| AlgorithmType | 노출 그룹 | 숨김 그룹 |
|---------------|-----------|-----------|
| TwoLineIntersect | Line1_*, Line2_* | Circle_*, Vertical_*, Horizontal_A_*, Horizontal_B_* |
| CircleTwoHorizontal | Circle_* (RadialDirection 포함, EdgeDirection 제외), Horizontal_A_*, Horizontal_B_* | Line1_*, Line2_*, Vertical_*, **Circle_EdgeDirection** |
| VerticalTwoHorizontal | Vertical_*, Horizontal_A_*, Horizontal_B_* | Line1_*, Line2_*, Circle_* |

- 기존 compile-time [Browsable(false)] (SourceShotName, raw edge HTuples, *Detected_*) 도 그대로 숨김 보존.
- AlgorithmType combobox / IsConfigured 등은 알고리즘 무관 항상 노출.

**Acceptance**:
- 각 알고리즘에서 노출/숨김 그룹 일치.
- DatumConfig.GetProperties(Attribute[]) 가 PropertyTools.Wpf 에 전달되는 PropertyDescriptor 가 AlgorithmType 별 필터링됨.
- DatumConfig.GetProperties() 무인자는 base TypeDescriptor 위임 → ParamBase INI Reflection 경로 무영향.

**source**: Phase 17 D-08/D-09 / Phase 16 carry #11/#15
**result**: not_tested
**notes**:

---

### Test 9 — AlgorithmType 변경 5-step 흐름 (D-10, carry #12)

**단계**:
1. Datum 2 (CTH) 선택 → btn_teachDatum 1회 클릭 (검출 성공: 검출 원 + center cross + DetectedOrigin 표시 확인)
2. PropertyGrid AlgorithmType ComboBox 변경: CircleTwoHorizontal → VerticalTwoHorizontal
3. 즉시 변화 확인:
   - PropertyGrid 갱신
   - 캔버스 시각화
   - 검출 시각화
4. Trace 로그 확인 (TryTriggerDatumAutoReteach / InvokeTryTeachDatum / MeasurePos 호출 카운트)

**기대** (D-10 5-step):
- Step 1 (PropertyGrid 즉시 갱신): VTH 그룹 (Vertical_* + Horizontal_A_* + Horizontal_B_*) 노출, CTH 그룹 (Circle_* + Horizontal_A_* + Horizontal_B_*) 중 Circle_* 숨김.
- Step 2 (LastTeachSucceeded/LastFindSucceeded reset = false): 검출 원 (light green) / center cross (yellow) / DetectedOrigin (purple) 모두 사라짐.
- Step 3 (DetectedOrigin/메트릭 0 reset): DetectedOriginRow/Col/RefAngle/EdgeCount/FitRMSE/AngleDeg = 0 (PropertyGrid Datum|Result 카테고리 0 표시).
- Step 4 (ROI 보존): Circle/Horizontal A/B ROI 도형 자체는 캔버스에 그대로 보임 (사용자가 그린 도형 보존).
- Step 5 (자동 재검출 X): TryTriggerDatumAutoReteach / InvokeTryTeachDatum / MeasurePos 호출 0건 (Phase 16 D-13/D-14 Auto-reteach off 정책 일치).

**Acceptance**:
- 5 항목 모두 만족.
- InspectionListView.OnParamEditorSelectionChanged 의 AlgorithmType whitelist 가드 작동 (TLI/CTH/VTH 3종만).
- ParamEditor.SelectedObject = null → datum force rebind (Phase 16 D-09/D-10 패턴 보존).

**source**: Phase 17 D-10 / Phase 16 carry #12
**result**: not_tested
**notes**:

---

### Test 10 — btn_teachDatum 호환성 가드 (D-11)

**단계**:
1. 새 Datum 생성 (4번째 Datum) → ROI 도형 미생성 상태
2. AlgorithmType = CircleTwoHorizontal 선택
3. ROI 미생성 상태에서 btn_teachDatum 클릭
4. 단계 1-3 을 AlgorithmType = VerticalTwoHorizontal 로 반복
5. 단계 1-3 을 AlgorithmType = TwoLineIntersect 로 반복

**기대** (UI-SPEC Copywriting verbatim):
- CTH ROI 미생성 분기 모달:
  - Title: `"티칭 실패"`
  - Message: `"Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요."`
- VTH Vertical ROI 미생성 분기 모달:
  - Title: `"티칭 실패"`
  - Message: `"Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요."`
- TLI Line1/Line2 ROI 미생성 분기 모달:
  - Title: `"티칭 실패"`
  - Message: `"필요한 ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요."` (일반 메시지)
- 모달 닫은 후 btn_teachDatum 자동 OFF + canvas mode 해제 + halconViewer.IsEditMode = false wiring.

**Acceptance**:
- 모달 텍스트 UI-SPEC verbatim 일치 (FormatTeachError + ValidateRoiPresence helper 출력).
- 알고리즘이 사용하지 않는 다른 ROI (예: CTH 4번째 Datum 에 Line1 그려져 있어도) 는 가드 통과 (ROI 자동 삭제 안 함 — 보존만).

**source**: Phase 17 D-11
**result**: not_tested
**notes**:

---

### Test 11 — 모달 정책: 성공 X / 실패 O (D-12, carry #6/#10)

**단계**:
1. **분기 a (성공 — teach)**: Datum 2 (CTH) 정상 ROI + btn_teachDatum 클릭 (성공 케이스)
2. **분기 b (성공 — find)**: 단계 1 직후 btn_testFindDatum 클릭 (성공 케이스)
3. **분기 c (실패 — teach)**: Horizontal_A_EdgeDirection = 잘못된 값 으로 강제 후 btn_teachDatum 클릭 (검출 0 케이스)
4. **분기 d (실패 — find)**: 티칭 미완료 상태에서 btn_testFindDatum 클릭

**기대**:
- 분기 a (teach 성공): 모달 **없음**. 캔버스 시각화만 (검출 원 light green + center cross yellow + DetectedOrigin purple 미발생 — find 미실행).
- 분기 b (find 성공): 모달 **없음**. 캔버스에 purple DispCross + 좌표 텍스트 + 화살표만 (Test 12 검증).
- 분기 c (teach 실패): CustomMessageBox.Show("티칭 실패", FormatTeachError(...)) — 검출 0 분기는 D-04 EdgeDirection 힌트 메시지 ("검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요.").
- 분기 d (find 실패): CustomMessageBox.Show("Find 실패", FormatFindError(...)) — 티칭 미완료 분기는 `"Datum 티칭이 완료되지 않았습니다. 먼저 Teach Datum 을 실행하세요."`.
- label_drawHint / label_testFindResult 의 inline 사유 표시 패턴 폐기 (모달로 통일).

**Acceptance**:
- 성공 모달 0회 (분기 a + b).
- 실패 모달 1회씩 + 텍스트 verbatim 일치 (분기 c + d).
- `grep -c "label_drawHint.Content = \"Datum 티칭 실패" MainView.xaml.cs` = 0.

**source**: Phase 17 D-12 / Phase 16 carry #6, #10
**result**: not_tested
**notes**:

---

## 시나리오 — Cluster D (DetectedOrigin + 결과 메트릭 + Hover)

### Test 12 — Test Find DetectedOrigin 시각화 (D-13/D-14, carry #17)

**단계**:
1. Datum 2 (CTH) 정상 티칭 성공 (Test 11 분기 a 후속)
2. btn_testFindDatum 클릭
3. AskTestImageSource 모달에서 "현재 이미지" 또는 "Load Image" 선택 (취소 아닌 경로)

**기대**:
- 검출 성공 시 캔버스에 4 시각화 요소 표시:
  1. **purple DispCross** size=14, lineWidth=2 (DetectedOriginRow/Col 위치).
  2. **"Find ({row:F1}, {col:F1})" 좌표 텍스트** (purple, EnsureFontInitialized 폰트).
  3. **DetectedRefAngle 방향 화살표** (purple, length=20, head=5).
  4. z-stack **가장 위** (RenderDatumOverlay 가 LastFindSucceeded 분기 후 마지막에 RenderDatumFindResult 호출 → 다른 시각화 가리지 않음).
- 모달 **없음** (D-12 성공).
- BtnTestFindDatum_Click 성공 경로: SetDatumOverlay(datum, true) → datum.RaisePropertyChanged("") → mParentWindow.inspectionList.RefreshParamEditor() (PropertyGrid Datum|Result 메트릭 즉시 표시).

**Acceptance**:
- 4 시각화 요소 모두 표시.
- 모달 0 (성공 분기).
- PropertyGrid Datum|Result 카테고리에 DetectedEdgeCount/FitRMSE/AngleDeg 값 표시 (Test 14 와 함께 검증).

**source**: Phase 17 D-13/D-14 / Phase 16 carry #17
**result**: not_tested
**notes**:

---

### Test 13 — DetectedOrigin transient 필드 LastFindSucceeded gate (D-13, W3 cross-plan)

**단계**:
1. Test 12 직후 (purple 십자 + 좌표 + 화살표 표시 상태)
2. PropertyGrid AlgorithmType ComboBox 변경 (CTH → VTH)
3. 캔버스 + PropertyGrid 즉시 변화 확인
4. AlgorithmType 다시 CTH 로 복원 → btn_testFindDatum 재클릭
5. 시각화 재표시 확인

**기대**:
- 단계 3: AlgorithmType 변경 직후 purple 십자 + 좌표 텍스트 + 화살표 모두 사라짐 (D-10 5-step Step 2: LastFindSucceeded=false reset → RenderDatumOverlay 가 LastFindSucceeded 분기 reject → RenderDatumFindResult 미호출).
- 단계 3: PropertyGrid Datum|Result 카테고리에 DetectedEdgeCount/FitRMSE/AngleDeg = 0 표시 (D-10 5-step Step 3: InspectionListView 가 transient 6 필드 0 reset).
- 단계 4 (재트리거): btn_testFindDatum 재클릭 시 새 알고리즘으로 재계산 후 다시 시각화 표시.

**Acceptance**:
- AlgorithmType 변경 시 시각화 즉시 clear + PropertyGrid 메트릭 0 reset.
- 재트리거 시 정상 재표시.
- InspectionListView.OnParamEditorSelectionChanged Step 3 wiring (DetectedOriginRow/Col/RefAngle/EdgeCount/FitRMSE/AngleDeg = 0) 동작 검증 (W3 cross-plan).

**source**: Phase 17 D-13 (Plan 17-03 transient + Plan 17-02 5-step Step 3 wiring 통합)
**result**: not_tested
**notes**:

---

### Test 14 — ROI 결과 메트릭 PropertyGrid 노출 (D-16, carry #5 부분)

**단계**:
1. 임의 Datum 티칭 또는 Test Find 후
2. PropertyGrid "Datum|Result" 카테고리 확인
3. 각 메트릭 값을 직접 편집 시도 (입력 또는 변경 의도)

**기대**:
- 3 메트릭 필드 표시:
  - `DetectedEdgeCount` (int, ReadOnly)
  - `DetectedFitRMSE` (double, ReadOnly)
  - `DetectedAngleDeg` (double, ReadOnly)
- 각 필드 편집 불가 (회색/잠금 — `[System.ComponentModel.ReadOnly(true)] + [PropertyTools.DataAnnotations.ReadOnly(true)]` 양쪽 부착).
- TryFindDatum 성공 시 자동 갱신 (TLI 분기는 line1RawRows.TupleLength + line2RawRows.TupleLength). DetectedFitRMSE 는 현재 placeholder = 0 (FitLine residuals 미수집, Phase 18 deferred).
- 단계 3: 편집 무반응 (ReadOnly 작동 확인).

**Acceptance**:
- 3 필드 ReadOnly 표시.
- 값 갱신 확인 (TryFindDatum 성공 후 EdgeCount/AngleDeg 갱신, FitRMSE 는 0 placeholder).
- carry #5 의 별도 미리보기 창은 본 phase deferred (frontmatter status_note 명시).

**source**: Phase 17 D-16 / Phase 16 carry #5 (부분)
**result**: not_tested
**notes**:

---

### Test 15 — 마우스 hover X/Y/Gray (D-15, carry #18)

**단계**:
1. 이미지 로드 (Datum 노드 선택 또는 Load 후 Grab)
2. 마우스를 캔버스 이미지 위로 이동
3. 마우스를 이미지 바깥 (canvasToolbar 영역, 또는 캔버스 영역 외부) 으로 이동
4. 이미지 없음 상태 (앱 처음 실행 직후 또는 Clear 후) 에서 캔버스 영역 hover

**기대** (UI-SPEC § Hover 표):
- 단계 2 (이미지 위): `X: 123 · Y: 456 · Gray: 200` (정수 + 가운데 점 ` · ` separator).
- 단계 3 (이미지 바깥): `X: N/A · Y: N/A · Gray: N/A`.
- 단계 4 (이미지 없음): `X: N/A · Y: N/A · Gray: N/A`.
- 폰트: FontSize=13, Foreground="#FFAAAAAA" (label_drawHint 와 동일), Margin="0,0,8,0".
- 배치: canvasToolbar Border 내부 Grid.Column="2" 우측 정렬 (panel_hoverInfo StackPanel 3 TextBlock).
- 기존 label_pos (캔버스 하단 오버레이 `X:{0:0.0}, Y:{1:0.0}, G:{2}`) 는 그대로 유지 — Phase 13 보존.
- PublishPointerInfo → PointerInfoChanged → UpdatePointerLabel 기존 파이프라인 재사용 (신규 GetGrayval 호출 0).

**Acceptance**:
- 3 케이스 모두 의도된 표시.
- 하단 label_pos 도 정상 (Phase 13 보존, 회귀 0).
- mm 단위 표시 없음 (deferred — Phase 18).

**Plan 17-03 Rule 3 deviation 검증** (panel_hoverInfo Polygon 모드 충돌):
- Polygon ROI 드로잉 모드 진입 시 (FAI Polygon ROI 그리는 중) `label_pointCount` 가 Visible 상태가 됨.
- panel_hoverInfo 와 label_pointCount 가 같은 Column 2 에 배치되어 시각적으로 겹칠 수 있음 (드로잉 모드에서만).
- 검증 단계: Polygon ROI 가 있는 FAI 노드 → btn_polygonRoi ON → 점 1~2개 클릭 → label_pointCount 가시 + panel_hoverInfo 가시 → 시각 충돌 여부 육안 확인.
- 충돌이 사용자 워크플로우를 방해하면 후속 carry-over 후보로 명시 (Polygon ROI 는 Datum 워크플로우 외부이므로 영향 미미 예상).

**source**: Phase 17 D-15 / Phase 16 carry #18 / Plan 17-03 Rule 3 deviation
**result**: not_tested
**notes**:

---

## 시나리오 — 알고리즘 보존 + 회귀 (자동 검증 + 통합)

### Test 16 — 알고리즘 보존 + Phase 16 회귀 + 빌드 자동 검증 (D-17 + D-18 + D-20)

**자동 검증 명령** (bash, MSYS_NO_PATHCONV=1 권장):

```bash
# ============================================================================
# D-17 알고리즘 보존 (Phase 17 누적, Phase 16 commit 이후 기준)
# ============================================================================
# Phase 17 시작점 commit = d93a678 (docs(state): record Phase 17 planning complete)

# VisionAlgorithmService.cs Phase 17 누적 diff = 0 라인
git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs | wc -l
# 기대: 0

# DatumFindingService.cs Phase 17 누적 신규 코드 (주석/공백 제외)
git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/DatumFindingService.cs \
  | grep -E "^\+[^+]" \
  | grep -vE "^\+\s*//" \
  | grep -vE "^\+\s*$" \
  | wc -l
# 기대: ≤ 11 (실측 11 EXACT — Plan 17-01 +2 caller polarity, Plan 17-03 +9 transient write-back)

# ============================================================================
# D-20 Phase 16 회귀 (force rebind + Auto-reteach off)
# ============================================================================

# Phase 16 D-09/D-10 force rebind 보존
grep -c "ParamEditor.SelectedObject = null" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
# 기대: ≥ 2 (Phase 16 D-09/D-10 + Phase 17 D-10 Step 1 추가)

# Phase 16 D-13/D-14 Auto-reteach off 보존: 자동 재티칭 호출 라인이 추가되지 않았음
grep -c "InvokeTryTeachDatumForEdit\|HandleDatumRoiMove\|HandleDatumRoiResize" WPF_Example/UI/ContentItem/MainView.xaml.cs
# 기대: 본 grep 자체는 helper 정의/호출 카운트만 (≥0). InvokeTryTeachDatumForEdit 가 RoiMove/Resize 콜백에서 호출되는 라인 0 확인 (육안 또는 git diff d93a678..HEAD WPF_Example/UI/ContentItem/MainView.xaml.cs 로 확인).

# NotifyDatumParamMaybeChanged 본문 noop 보존
grep -A 5 "private void NotifyDatumParamMaybeChanged" WPF_Example/UI/ContentItem/MainView.xaml.cs
# 기대: 본문이 noop (return; 또는 빈 블록)

# ============================================================================
# D-18 hbk 주석 카운트 (Phase 17, //260503 hbk Phase 17)
# ============================================================================

grep -c "//260503 hbk Phase 17" WPF_Example/Halcon/Display/HalconDisplayService.cs
# 기대: ≥ 5 (Plan 17-01 RenderCircleStripOverlay + Plan 17-03 RenderDatumFindResult)

grep -c "//260503 hbk Phase 17" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
# 기대: ≥ 18 (Plan 17-01 RadialDirection + tooltip 6 + Plan 17-02 ICustomTypeDescriptor 12+ + Plan 17-03 transient 6)

grep -c "//260503 hbk Phase 17" WPF_Example/Halcon/Algorithms/DatumFindingService.cs
# 기대: ≥ 4 (Plan 17-01 caller polarity + Plan 17-03 transient write-back)

grep -c "//260503 hbk Phase 17" WPF_Example/UI/ContentItem/MainView.xaml.cs
# 기대: ≥ 12 (Plan 17-02 Delete + ValidateRoiPresence + FormatXxxError + IsEditMode wiring + Plan 17-03 BtnTestFindDatum 성공경로 + UpdatePointerLabel)

grep -c "//260503 hbk Phase 17" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
# 기대: ≥ 4 (Plan 17-02 5-step + Plan 17-03 Step 3)

grep -c "260503 hbk Phase 17\|//260503 hbk Phase 17" WPF_Example/UI/ContentItem/MainView.xaml
# 기대: ≥ 1 (Plan 17-03 panel_hoverInfo)

grep -c "//260503 hbk Phase 17 D-02" WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs
# 기대: ≥ 1 (Plan 17-01 RadialDirections)

grep -c "//260503 hbk Phase 17" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
# 기대: ≥ 4 (Plan 17-02 _isEditMode setter + HitTestSelectedRoi gate + 좌클릭 가드)

# ============================================================================
# 빌드
# ============================================================================
MSYS_NO_PATHCONV=1 "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal
# 기대: exit code 0, DatumMeasurement.exe 생성, 신규 warning 0 on 수정 범위
# Pre-existing warnings (out-of-scope): VisionAlgorithmService.cs CS0219, VirtualCamera.cs CS0162, MSB3884 ruleset 누락 — 본 phase 무관

# ============================================================================
# Plan 영역 분리 검증 (sequential lock)
# ============================================================================

# Plan 17-01 영역 (RadialDirection)
grep -c "Circle_RadialDirection" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
# 기대: ≥ 4 (필드 + ItemsSourceProperty + List getter + EnsurePerRoiDefaults)

# Plan 17-02 영역 (ICustomTypeDescriptor)
grep -c "ICustomTypeDescriptor\|IsHiddenForAlgorithm" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
# 기대: ≥ 4 (헤더 + 메소드 그룹 + helper 선언/호출)

# Plan 17-03 영역 (transient + 메트릭)
grep -c "DetectedOriginRow\|DetectedOriginCol\|DetectedRefAngle\|DetectedEdgeCount\|DetectedFitRMSE\|DetectedAngleDeg" \
  WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
# 기대: ≥ 6 (6 필드 선언)

# 시각화 검증
grep -c "thetaRad = 0.0" WPF_Example/Halcon/Display/HalconDisplayService.cs
# 기대: 1 (Plan 17-01 단일 strip)

grep -c "for.*stepCount" WPF_Example/Halcon/Display/HalconDisplayService.cs
# 기대: 0 (Plan 17-01 stepCount 루프 폐기)

grep -c "purple" WPF_Example/Halcon/Display/HalconDisplayService.cs
# 기대: ≥ 1 (Plan 17-03 RenderDatumFindResult)
```

**Acceptance**: 모든 자동 검증 PASS.

**source**: Phase 17 D-17/D-18/D-20 (algorithm preservation + hbk 주석 컨벤션 + Phase 16 회귀)
**result**: not_tested
**notes**:

---

## 사용자 사인오프

**사용자 검증 완료 일자**: YYYY-MM-DD
**결정**: 승인 / 보류 / partial

**최종 결과**:
- PASS: N건
- FAIL: N건
- not_tested: N건
- SKIP: N건
- INVALID: N건

**Phase 17 deliverables 검증**:
- Cluster A (Circle 1-strip + RadialDirection + EdgeDirection 정책): ___
- Cluster B (Edit 모드 + 좌클릭 드래그 + Delete 모달): ___
- Cluster C (PropertyGrid 동적 노출 + AlgorithmType 변경 + 호환성 + 모달): ___
- Cluster D (DetectedOrigin + 결과 메트릭 + Hover): ___
- 자동 검증 (D-17 algorithm preservation + D-18 hbk 주석 + D-20 Phase 16 회귀 + 빌드): ___

**다음 phase carry-over (FAIL 시 채움)**:
- (FAIL Test 번호와 결함 + 재현 절차 + 후속 plan 후보 명시)
- (예시) panel_hoverInfo / label_pointCount 시각 충돌 (Plan 17-03 Rule 3 deviation) — Polygon ROI 드로잉 모드 워크플로우 영향 시 carry-over.

---

## Summary 표

| # | Test | result | notes |
|---|------|--------|-------|
| 1 | Circle pre-teach Strip 1개 표시 (D-01, carry #1) | not_tested | |
| 2 | Circle_RadialDirection PropertyGrid 노출 (D-02, carry #2) | not_tested | |
| 3 | Circle_EdgeDirection 동적 hide (D-03, carry #3) | not_tested | |
| 4 | EdgeDirection 모든 옵션 + tooltip + 검출 0 힌트 (D-04, carry #16) | not_tested | |
| 5 | 좌클릭+드래그 그리기 시작 (D-05, carry #8) | not_tested | |
| 6 | _isEditMode 단일 gate Rect+Circle+Polygon (D-06, carry #9, #13) | not_tested | |
| 7 | Delete ROI 3-button 모달 (D-07, carry #14) | not_tested | |
| 8 | AlgorithmType 별 PropertyGrid 동적 노출 (D-08/D-09, carry #11/#15) | not_tested | |
| 9 | AlgorithmType 변경 5-step 흐름 (D-10, carry #12) | not_tested | |
| 10 | btn_teachDatum 호환성 가드 (D-11) | not_tested | |
| 11 | 모달 정책: 성공 X / 실패 O (D-12, carry #6/#10) | not_tested | |
| 12 | Test Find DetectedOrigin 시각화 (D-13/D-14, carry #17) | not_tested | |
| 13 | DetectedOrigin transient LastFindSucceeded gate (D-13, W3 cross-plan) | not_tested | |
| 14 | ROI 결과 메트릭 PropertyGrid 노출 (D-16, carry #5 부분) | not_tested | |
| 15 | 마우스 hover X/Y/Gray + Polygon 시각 충돌 검증 (D-15, carry #18) | not_tested | |
| 16 | 알고리즘 보존 + Phase 16 회귀 + 빌드 자동 검증 (D-17/D-18/D-20) | not_tested | |

**진행 가이드**:
- 각 Test 의 `result` 필드를 PASS / FAIL / not_tested / SKIP / INVALID 중 하나로 갱신하면서 진행.
- FAIL 발생 시 `notes` 에 결함 내용 + 재현 절차 + 다음 phase carry-over 후보 명시.
- 모두 PASS 시 frontmatter `status: pending` → `status: signed_off`, `summary.passed/failed/not_tested/skipped/invalid` 갱신, 마지막에 사인오프 라인 추가:
  ```
  사용자 검증 완료 일자: YYYY-MM-DD
  결정: 승인
  ```
- FAIL 1건 이상 시 `status: partial` 또는 `status: failed` + carry-over 후보 명시.
