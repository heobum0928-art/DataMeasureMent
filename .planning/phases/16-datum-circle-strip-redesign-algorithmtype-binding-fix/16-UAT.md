---
phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix
type: uat
status: pending
created: 2026-04-30
updated: 2026-04-30
summary:
  total: 14
  passed: 0
  failed: 0
  not_tested: 14
---

# Phase 16 UAT — Datum Circle Strip Redesign + AlgorithmType Binding Fix

## 개요

Phase 16 가 해결한 결함:

| Plan | 결함 | 해결 |
|------|------|------|
| 16-01 (R1) | Circle ROI 그린 직후 알고리즘이 사용할 strip 사각형이 보이지 않음 | `RenderCircleStripOverlay` helper + Circle 분기 pre-teach 시각화 |
| 16-01 (R2) | 검출 후 노란 원 + 빨간 6px 십자가 가시성 부족 (center 가 가장 중요) | light green 검출 원 + 노란 size=12 center cross + gray size=4 raw edges + z-order |
| 16-02 (R3) | Phase 15 UAT Test 10/11/12 — ROI 편집 후 Datum 전환 시 AlgorithmType combobox stale | InspectionListView Datum 분기에 PropertyGrid SelectedObject `null → datumCfg` force rebind |
| 16-02 (R4) | ROI 이동/리사이즈마다 자동 재티칭 호출 → HALCON edge measurement 리소스 과부하 | MainView `HandleDatumRoiMove` / `HandleDatumRoiResize` 의 자동 호출 라인 직접 삭제 + `NotifyDatumParamMaybeChanged` 본문 noop |

D-22 보존: VisionAlgorithmService / DatumFindingService / DatumConfig 한 줄도 변경하지 않음.

Phase 15 carry-over (15-UAT.md partial sign-off):
- Test 5/6/7/8 — Circle/Vertical 알고리즘 검출 정확성 (Phase 15 not_tested → Phase 16 재검증)
- Test 10/11/12 — ROI 이동 후 AlgorithmType binding (Phase 15 FAIL → Phase 16 fix 검증)
- Test 13/14/15 — #1405 carry-over 검증 / SIMUL 회귀

## 사전 조건

- [ ] Plan 16-01 + 16-02 모두 commit 완료 (`fa11033`, `d4897de`, `c51b297`, `a46d86d`)
- [ ] msbuild Debug/x64 PASS (commit 시점에 검증 완료)
- [ ] 신규 warning 0 on HalconDisplayService.cs / InspectionListView.xaml.cs / MainView.xaml.cs
- [ ] `git diff --stat WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` = 0 라인
- [ ] `git diff --stat WPF_Example/Halcon/Algorithms/DatumFindingService.cs` = 0 라인
- [ ] `git diff --stat WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` = 0 라인
- [ ] 사용자가 실 카메라 grab 또는 저장된 실 데이터 이미지 보유
- [ ] Datum 3개 보유 레시피 로드 (Datum 1=TwoLineIntersect, Datum 2=CircleTwoHorizontal, Datum 3=VerticalTwoHorizontal)

---

## 시나리오 — Phase 16 신규 (Test 1~4)

### Test 1 — Pre-teach Strip 시각화 (R1, D-01/D-02/D-08)

**단계**:
1. Datum 2 (CircleTwoHorizontal) 선택
2. btn_teachDatum ON (편집 모드)
3. 캔버스에 원 ROI 그림 (적절한 반지름)

**기대**:
- 원 ROI 그린 직후 round(360 / Circle_PolarStepDeg) 만큼의 strip 사각형이 **회색 thin line, fill 없음** 으로 정적 표시됨 (PolarStepDeg=10° 기본값 → 36개)
- z-order: 원 ROI 경계 (밑) → Strip 사각형 (위)

**Acceptance**:
- 화면에 stepCount 개 strip 사각형 회색 외곽선 보임
- Strip 위치 = 알고리즘이 실제 측정할 위치 (canonical 식: `rectRow = ROI_Row - Radius * Sin(thetaRad)`, `rectCol = ROI_Col + Radius * Cos(thetaRad)`)

**result**: not_tested
**notes**:

---

### Test 2 — RectL1Ratio 즉시 갱신 (R1, D-03)

**단계**:
1. Test 1 의 strip 시각화가 표시된 상태
2. PropertyGrid 의 `Circle_RectL1Ratio` 0.05 → 0.20 변경

**기대**:
- Strip 사각형의 반경 방향 (length1) 크기가 즉시 4배로 커짐
- 앱 재시작 / 노드 재선택 불필요 — PropertyGrid 변경 → RaisePropertyChanged + RefreshParamEditor → SetDatumOverlay 재호출 경로

**Acceptance**:
- Strip 시각화가 즉시 변함 (육안 확인)

**result**: not_tested
**notes**:

---

### Test 3 — Post-teach 검출 원 + Center cross + Raw edges (R2, D-05/D-06/D-07/D-08)

**단계**:
1. 정상 ROI (Circle + Horizontal A + Horizontal B) 로 btn_teachDatum 클릭

**기대**:
- 검출 원 = **light green** (D-05)
- Center cross = **노란색 size=12, line width 3** (D-06, 정밀 원 center 가장 두드러지게)
- Circle raw edge points = **gray size=4** (D-07, 검출 trace)
- 다른 ROI raw edges 회귀 0: Line1 cyan / Line2 magenta / Horizontal A green / Horizontal B lime green / Vertical orange (size=6 default)
- z-order: ROI 경계 → Strip → Raw edges → 검출 원 → Center cross (top, 가려지지 않음)

**Acceptance**:
- 검출 원 위 노란 큰 십자가 두드러지게 보임 (가장 위)
- PropertyGrid 의 `CircleCenter_Row` / `CircleCenter_Col` / `CircleDetected_Radius` 값과 화면 좌표 일치

**result**: not_tested
**notes**:

---

### Test 4 — Auto-reteach off (R4, D-13/D-14)

**단계**:
1. Datum 2 선택 → btn_teachDatum 1회 클릭 (성공 검증 후)
2. ROI (원 또는 Horizontal A/B 중 하나) 5회 임의 이동
3. Logging trace 파일 확인 (`Logging.PrintLog((int)ELogType.Trace, ...)`)
4. btn_teachDatum 1회 추가 클릭

**기대**:
- 단계 2 ROI 이동 5회 동안: trace 에 `InvokeTryTeachDatumForEdit ENTRY` / `MeasurePos` / `TryFindLine` 추가 로그 **0건**
- 단계 4 btn_teachDatum 1회 클릭 시: `InvokeTryTeachDatum ENTRY` 로그 1회만 추가
- 단계 2 후: `LastTeachSucceeded` 변경되지 않음 → 검출 원/center 시각화는 **stale 데이터 그대로** 표시 (사용자가 mismatch 인지 가능 — D-14 verbatim)

**Acceptance**:
- Trace 로그에 자동 재티칭 0건
- 검출 원이 새 ROI 위치와 mismatch 한 채로 보임 (의도적 stale)
- btn_teachDatum 수동 트리거는 정상 동작

**result**: not_tested
**notes**:

---

## 시나리오 — Phase 15 carry-over (Test 5~14)

### Test 5 — CircleTwoHorizontal × LtoR (Horizontal_A/B), Circle EdgeSelection=First

> 15-UAT.md Test 5 verbatim 인용. Phase 16 효과: Plan 16-01 시각화 재작성으로 검출 원/center 가시성 향상 — 사용자가 검출 정확성 직관 검증 가능.

**단계**: 15-UAT.md acceptance 절차 그대로 수행.
**기대**: Circle Horizontal A/B 의 LtoR 검출 + Circle First selection 으로 안정적 원 fitting.
**result**: not_tested
**notes**:

---

### Test 6 — CircleTwoHorizontal × Horizontal direction = RtoL/BtoT 혼합

> Phase 16 효과: 알고리즘 보존 (D-22) → Phase 15 동작 그대로.

**단계**: 15-UAT.md Test 6 절차.
**result**: not_tested
**notes**:

---

### Test 7 — VerticalTwoHorizontal × Vertical TtoB + Horizontal LtoR

**단계**: 15-UAT.md Test 7 절차.
**result**: not_tested
**notes**:

---

### Test 8 — VerticalTwoHorizontal × Vertical BtoT

**단계**: 15-UAT.md Test 8 절차.
**result**: not_tested
**notes**:

---

### Test 10 — ROI 이동 후 자동 재티칭 (TwoLineIntersect)

> ⚠ Phase 16 정책 변경 — D-13 (Auto-reteach off). 본 Test 의 acceptance 는 **Phase 15 와 정반대로** 변경됨:
> - Phase 15: ROI 이동 → 자동 재티칭 PASS (자동 호출)
> - Phase 16: ROI 이동 → 자동 재티칭 **호출 0건** (수동 btn_teachDatum 만 트리거) + 검출 원 stale 표시 (D-14)
> 본 Test 는 D-13 신규 정책 검증으로 재정의.

**단계**:
1. Datum 1 (TwoLineIntersect) 선택 → btn_teachDatum 1회 (검증 후)
2. Line1 또는 Line2 ROI 임의 이동 5회
3. Trace 로그 확인

**기대**:
- 자동 재티칭 호출 0건 (D-13)
- LastTeachSucceeded 변경 안 됨, 검출 라인 외삽 / 교점 십자 stale (D-14)
- btn_teachDatum 수동 클릭 시 1회 재티칭 정상

**result**: not_tested
**notes**:

---

### Test 11 — ROI 이동 후 자동 재티칭 (CircleTwoHorizontal) ★ Phase 15 FAIL → Phase 16 PASS 기대

> Phase 15 FAIL 의 핵심: ROI 이동 후 Datum 1→2→3 클릭 시 AlgorithmType combobox stale.
> Phase 16 fix: Plan 16-02 D-09/D-10 force rebind. 본 Test 는 fix 검증.

**단계**:
1. Datum 2 (CircleTwoHorizontal) 선택 → 원 ROI 임의 이동 3회
2. Datum 1 (TwoLineIntersect) 클릭
3. PropertyGrid AlgorithmType combobox 값 확인
4. Datum 2 재클릭 → AlgorithmType 값 확인
5. Datum 3 (VerticalTwoHorizontal) 클릭 → AlgorithmType 값 확인
6. (선택) Datum 2 재선택 후 btn_teachDatum 클릭 → CircleTwoHorizontal 코드 경로 실행 확인 (Trace 로그)

**기대**:
- 단계 3: AlgorithmType combobox = "TwoLineIntersect" (즉시 갱신, stale 0)
- 단계 4: AlgorithmType combobox = "CircleTwoHorizontal" (즉시 갱신)
- 단계 5: AlgorithmType combobox = "VerticalTwoHorizontal" (즉시 갱신)
- ROI 이동 5회 동안 자동 재티칭 호출 0건 (D-13)

**result**: not_tested
**notes**:

---

### Test 12 — ROI 이동 후 자동 재티칭 (VerticalTwoHorizontal) ★ Phase 15 FAIL → Phase 16 PASS 기대

> Phase 16 fix 동일 — Plan 16-02 D-09/D-10 force rebind + D-13 Auto-reteach off.

**단계**: Test 11 과 동일 패턴. Datum 3 (VerticalTwoHorizontal) 시작점.
1. Datum 3 선택 → Vertical/Horizontal A/B ROI 임의 이동 3회
2. Datum 신규 생성 (4번째 Datum) → 클릭 → AlgorithmType 기본값 확인
3. Datum 1/2/3 재순회 → 각 AlgorithmType 즉시 갱신 확인

**기대**:
- 신규 Datum 생성 후 AlgorithmType 기본값 즉시 표시
- 모든 Datum 클릭 시 AlgorithmType combobox stale 0
- 자동 재티칭 호출 0건

**result**: not_tested
**notes**:

---

### Test 13 — EdgeSelection (First/Last/All) 전파 검증 (#1405 carry-over)

> Phase 16 효과: 알고리즘 보존 (D-22) → Phase 15-03 의 selection 분기 그대로.

**단계**: 15-UAT.md Test 13 절차.
**result**: not_tested
**notes**:

---

### Test 14 — #1405 carry-over: VTH ConcatObj→TupleConcat 패턴 회귀

> Phase 16 효과: 알고리즘 보존 → 회귀 0 기대.

**단계**: 15-UAT.md Test 14 절차 (smoke harness 미호출 시 code-level grep 검증).
**result**: not_tested
**notes**:

---

### Test 15 — SIMUL_MODE Phase 14-05 동일 시나리오 재실행

**단계**: 15-UAT.md Test 15 절차 (SIMUL_MODE).
**result**: not_tested
**notes**:

---

## 검증 항목 (자동)

```bash
# 알고리즘 보존 검증 (D-22)
git diff --stat WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs       # = 0 라인
git diff --stat WPF_Example/Halcon/Algorithms/DatumFindingService.cs          # = 0 라인
git diff --stat WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs         # = 0 라인

# 빌드 검증
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64
# → PASS, 신규 warning 0 on HalconDisplayService.cs / InspectionListView.xaml.cs / MainView.xaml.cs

# hbk 주석 카운트 (Phase 16)
grep -c "//260429 hbk Phase 16" WPF_Example/Halcon/Display/HalconDisplayService.cs       # ≥ 14
grep -c "//260429 hbk Phase 16" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs    # ≥ 4
grep -c "//260429 hbk Phase 16" WPF_Example/UI/ContentItem/MainView.xaml.cs              # ≥ 6
```

---

## Summary 표

| # | Test | result | notes |
|---|------|--------|-------|
| 1 | Pre-teach Strip 시각화 (R1) | not_tested | |
| 2 | RectL1Ratio 즉시 갱신 (R1) | not_tested | |
| 3 | Post-teach 검출 원 + Center cross (R2) | not_tested | |
| 4 | Auto-reteach off (R4) | not_tested | |
| 5 | CircleTwoHorizontal × LtoR (carry) | not_tested | |
| 6 | CircleTwoHorizontal × RtoL/BtoT (carry) | not_tested | |
| 7 | VerticalTwoHorizontal × Vert TtoB + Horiz LtoR (carry) | not_tested | |
| 8 | VerticalTwoHorizontal × Vert BtoT (carry) | not_tested | |
| 10 | ROI 이동 자동 재티칭 / TLI (D-13 정책) | not_tested | |
| 11 | ROI 이동 binding fix / CTH (D-09/D-10) ★ | not_tested | |
| 12 | ROI 이동 binding fix / VTH (D-09/D-10) ★ | not_tested | |
| 13 | EdgeSelection 전파 (#1405 carry) | not_tested | |
| 14 | #1405 VTH ConcatObj→TupleConcat 회귀 | not_tested | |
| 15 | SIMUL_MODE Phase 14-05 재실행 | not_tested | |

**진행 가이드**:
- 각 Test 의 `result` 필드를 PASS / FAIL / not_tested 로 갱신하면서 진행
- FAIL 발생 시 `notes` 에 결함 내용 + 재현 절차 + Phase 17 carry-over 후보 명시
- 모두 PASS 시 frontmatter `status: pending` → `status: signed_off`, `summary.passed/failed/not_tested` 갱신, 마지막에 사인오프 라인 추가:
  ```
  사용자 검증 완료 일자: YYYY-MM-DD
  결정: 승인 (or 보류 / partial)
  ```

---

## 사용자 사인오프

(검증 완료 후 작성)
