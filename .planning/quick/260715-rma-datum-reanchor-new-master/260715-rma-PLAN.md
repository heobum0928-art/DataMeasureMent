---
phase: quick-260715-rma
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/ContentItem/MainView.xaml
requirements: [REANCHOR-01]

must_haves:
  truths:
    - "Datum 노드 선택 + 새 마스터 이미지(Grab/Load) 상태에서 'Re-anchor' 실행 시, 옛 datum 으로 새 마스터를 Find 한 변환 T(=CurrentTransform)를 획득한다"
    - "Find 실패(T 없음) 시 아무 것도 변경하지 않고 중단하고 사용자에게 알린다 (라인핏 datum 은 옛 검색 ROI 로 찾으므로 새 마스터가 너무 밀리면 실패 가능)"
    - "적용 전에 migrated ROI 를 화면에 미리보기(overlay)로 보여주고 사용자 확인을 받는다 — 확인 전에는 원본 좌표 불변"
    - "확인 시: 레시피를 백업한 뒤, 이 datum(DatumRef)을 참조하는 모든 측정의 지오메트리 + EdgePairDistance 가 쓰는 Owner FAI 의 ROI_* + datum 자체 검색 ROI 를 T 로 일괄 변환한다"
    - "변환 규칙: 중심(*_Row/*_Col)은 AffineTransPoint2d(T,...), 각도(*_Phi, 라디안)는 +Atan2(-T[1],T[0]), 길이/반경(*_Length*/*_Radius)은 불변. Nominal/공차/MeasCorrectionFactor 불변"
    - "이전 완료 후 datum 을 새 마스터로 재티칭(기존 teach 경로 재사용)하여 새 기준(RefOrigin/RefMatch)을 확정한다 — 순서는 Find(T)→이전→재티칭 고정"
  artifacts:
    - path: "WPF_Example/UI/ContentItem/MainView.xaml.cs"
      provides: "BtnReanchorToNewMaster_Click + TransformMeasurementGeometry/TransformFaiRoi/TransformDatumOwnRois/TransformPointInPlace 헬퍼 + 미리보기/확인/백업 흐름"
      contains: "TransformMeasurementGeometry"
  key_links:
    - from: "MainView 재앵커 핸들러"
      to: "datum.CurrentTransform (옛 datum 으로 새 마스터 Find 결과)"
      via: "BtnTestFindDatum 과 동일 find 경로(TryComposeAlign/TryFindDatum) 재사용해 T 획득"
      pattern: "CurrentTransform"
    - from: "TransformMeasurementGeometry"
      to: "각 측정 타입 지오메트리 필드"
      via: "타입별 (Row,Col) 변환 + Phi 회전. ApplyPointRoiMoveDelta 의 타입 매핑과 동일 필드셋"
      pattern: "AffineTransPoint2d"
---

<objective>
기준 마스터 샘플 교체 시, 모든 측정 ROI 를 일일이 다시 그리지 않고 **옛 datum 으로 새 마스터를 Find 한 강체 변환 T 로 일괄 재-앵커**한다.

Purpose: 마스터를 새 것으로 바꾸면 새 마스터가 옛 기준과 다른 위치/각도에 놓여, 옛 기준 좌표로 저장된 측정 ROI 들이 통째로 어긋난다. 옛 datum 으로 새 마스터를 Find 하면 나오는 CurrentTransform 이 정확히 "옛 기준 → 새 마스터 포즈" 강체 변환이므로(조사 wf_c044c03a-65e 확정), 이를 모든 ROI 에 적용하면 재작업 없이 이전된다.

핵심 리스크 = **모든 ROI 를 한 번에 덮어써서 잘못되면 복구 곤란.** 따라서 (1) Find 실패 시 안전 중단, (2) 미리보기+확인, (3) 레시피 백업, (4) Find→이전→재티칭 순서 강제를 반드시 코드에 박는다.

전제(사용자 확인): 옛/새 마스터의 **광학 배율 동일**(같은 카메라·렌즈·WD·부품). 강체 변환은 스케일을 보정하지 못하므로 배율이 다르면 이 방식 부적합.
</objective>

<context>
@CLAUDE.md
@WPF_Example/UI/ContentItem/MainView.xaml.cs
@WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
@WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
@WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs

<interfaces>
<!-- 조사(wf_c044c03a-65e) + 직접 인벤토리 확정 — 재탐색 불필요 -->

**측정 타입별 지오메트리 필드 (전부 절대 reference-frame 좌표, Phi=라디안):**
- EdgeToLineDistance / EdgeToLineAngle / ArcEdgeDistance : Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2
- CompoundAngle / CompoundCenterCDistance / CompoundCenterBDistance / CompoundShortAxisDistance : Rect_Row, Rect_Col, Rect_Phi, Rect_Length1, Rect_Length2
- CircleDiameter / CircleCenterDistance : Circle_Row, Circle_Col, Circle_Radius (Phi 없음)
- ArcLineIntersectDistance : EdgeA1_/EdgeB1_/EdgeA2_/EdgeB2_ 각각 Row,Col,Phi,Length1,Length2
- DualImageEdgeDistance : PointROI_Row/Col/Phi/Length1/Length2, LineROI_Row/Col/Phi/Length1/Length2
- LineToLineDistance / LineToLineAngle : Line1_/Line2_ 각각 Row,Col,Phi,Length1,Length2
- PointToLineDistance : Point_/Line_ 각각 Row,Col,Phi,Length1,Length2
- PointToPointDistance : Point1_/Point2_ 각각 Row,Col,Phi,Length1,Length2
- **EdgePairDistance : 자체 지오메트리 없음** → Owner(FAIConfig).ROI_Row/Col/Phi/Length1/Length2 사용 (EdgePairDistanceMeasurement.cs:66-98 확인)

**FAIConfig ROI 필드:** ROI_Row, ROI_Col, ROI_Phi, ROI_Length1, ROI_Length2 (+ PolygonPoints — 폴리곤은 이번 범위 밖, 있으면 로그만)

**DatumConfig 자체 검색 ROI (절대 좌표, 재앵커 대상):**
- Line1_/Line2_/Vertical_/Horizontal_A_/Horizontal_B_ : Row,Col,Phi(라디안),Length1,Length2
- Circle_ : CircleROI_Row/Col/Radius (CTH), Circle_* 파라미터는 비지오메트리
- Pattern : PatternRoi_Row/Col/Phi, PatternRoi_Length1/2 / PatternRoi2_Row/Col/Phi, PatternRoi2_Length1/2

**변환 규칙(런타임 VisionAlgorithmService.TryFitLine 과 동일):**
```
AffineTransPoint2d(T, row, col, out nr, out nc);   // 중심
rotAngle = Atan2(-T[1].D, T[0].D);                 // 라디안
phi' = phi + rotAngle;                             // 각도
// length/radius 불변 (강체는 크기 보존)
```

**T 획득 = 기존 Test Find 경로 재사용 (MainView.BtnTestFindDatum_Click, ~line 3601):**
- align: seq.TryComposeAlign(datum, image, modelPath, out err) → datum.CurrentTransform 갱신
- non-align: svc.TryFindDatum(image, datum, out transform, out err) → transform = T
- ok==false 면 T 없음 → 중단
- CurrentTransform HTuple, Length>=5, AffineTransPoint2d/HomMat2dInvert 사용 가능(BuildCorrectedResultRoiOverlays 선례)

**datum→측정 매핑:** 측정.DatumRef == datum.DatumName. 시퀀스 datum 목록 = InspectionSequence.DatumConfigs. Shot 의 FAI/측정 순회는 CollectShotRois/GetCurrentShotContext 선례.

**재티칭 경로:** 패턴 = BtnCreatePatternModel 로직(모델 재생성+RefMatch), 라인핏 = DatumFindingService.TryTeach*(RefOrigin/RefAngle). 본 작업은 재티칭을 **기존 티치 버튼 재사용 안내**로 처리(자동 호출은 위험 — 아래 Task 3 참고).
</interfaces>
</context>

<constraints>
- 삼항 `?:` 금지, C# 7.2, 파일 스타일 유지(MainView = K&R 혼재), `//YYMMDD hbk` 신규 금지
- **파괴적 커밋 전 반드시**: (1) Find 성공(T 유효) 확인, (2) 미리보기+사용자 확인, (3) 레시피 백업
- 순서 불변: Find(T획득) → ROI 이전 → (재티칭). 재티칭을 먼저 하면 CurrentTransform 이 identity 로 덮여 T 소실
- datum 별 개별 T (전역 T 금지) — 이번 v1 은 **현재 선택된 Datum 노드 1개** 범위로 한정(멀티 datum 일괄은 후속)
- 각도 타입(LineToLineAngle/EdgeToLineAngle/CompoundAngle)도 ROI 위치는 이전 대상(측정값이 각도일 뿐 ROI 는 이동해야 함) — MeasCorrectionFactor 제외와 혼동 금지
- Nominal/TolerancePlus/ToleranceMinus/MeasCorrectionFactor/Edge 파라미터 불변
</constraints>

<tasks>

<task type="auto">
  <name>Task 1: 지오메트리 변환 헬퍼 (측정/ FAI / datum ROI + point/phi 변환)</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
순수 좌표 변환 헬퍼들 추가 (write 는 하되 UI/흐름 없음 — 단위 검증 용이):

1) `private void TransformPointInPlace(HTuple T, ref double row, ref double col)` — AffineTransPoint2d 로 (row,col) 갱신.
2) `private double RotAngleOf(HTuple T)` → `Atan2(-T[1].D, T[0].D)` (라디안).
3) `private void TransformMeasurementGeometry(MeasurementBase m, HTuple T)` — 타입별 분기(ApplyPointRoiMoveDelta 의 타입 매핑 그대로 확장):
   - 각 (Row,Col) → TransformPointInPlace, 각 Phi → += RotAngleOf(T), Length/Radius 불변.
   - EdgeToLineDistance/Angle/ArcEdge: Point_*
   - Compound* : Rect_*
   - Circle* : Circle_Row/Col (Radius 불변, Phi 없음)
   - ArcLineIntersect : EdgeA1/B1/A2/B2_*
   - DualImage : PointROI_*, LineROI_*
   - LineToLine(Distance/Angle) : Line1_*, Line2_*
   - PointToLine : Point_*, Line_*
   - PointToPoint : Point1_*, Point2_*
   - EdgePairDistance : **자체 지오메트리 없음 → skip (FAI ROI 는 별도 4)에서 처리)**
4) `private void TransformFaiRoi(FAIConfig fai, HTuple T)` — ROI_Row/Col → 변환, ROI_Phi += RotAngle, ROI_Length* 불변. PolygonPoints 비어있지 않으면 Logging 경고만(폴리곤 이전 범위 밖).
5) `private void TransformDatumOwnRois(DatumConfig d, HTuple T)` — Line1_/Line2_/Vertical_/Horizontal_A_/Horizontal_B_ (Row/Col 변환, Phi += rot), CircleROI_Row/Col (변환, Radius 불변), PatternRoi_/PatternRoi2_ (Row/Col 변환, Phi += rot). Length/Radius 불변.

`using HalconDotNet` 이미 있음. HTuple 인덱싱은 `T[1].D` 형태(BuildCorrectedResultRoiOverlays 선례).
  </action>
  <verify><automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated></verify>
  <done>빌드 PASS. 5개 헬퍼 존재. 모든 측정 타입 + FAI + datum ROI 커버. Length/Radius/Nominal 불변.</done>
</task>

<task type="auto">
  <name>Task 2: T 획득(옛 datum 으로 새 마스터 Find) — 실패 시 안전 중단</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
`private bool TryFindTransformForReanchor(DatumConfig datum, out HTuple T, out string error)` 추가.
- BtnTestFindDatum_Click 의 find 분기(align: seq.TryComposeAlign / non-align: svc.TryFindDatum)를 그대로 재사용해 현재 halconViewer 이미지(새 마스터)로 Find.
- 성공 시 T = datum.CurrentTransform (align) 또는 out transform (non-align). T.Length>=5 검증.
- 실패(ok==false 또는 T 무효) → error 세팅, false 반환. **datum/ROI 아무 것도 변경 안 함.**
- 이미지 소스 = 현재 halconViewer.CurrentImage (사용자가 새 마스터 Grab/Load 한 상태 전제). null 이면 실패.
  </action>
  <verify><automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated></verify>
  <done>빌드 PASS. Find 실패 시 false + 무변경. 성공 시 유효 T 반환.</done>
</task>

<task type="auto">
  <name>Task 3: 재앵커 오케스트레이션 (미리보기+확인+백업+커밋) + 버튼</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs, WPF_Example/UI/ContentItem/MainView.xaml</files>
  <action>
**(A) MainView.xaml** — 기존 datum 편집 버튼군(btn_teachDatum 근처)에 `btn_reanchor` 버튼 추가("Re-anchor to New Master"), Datum 노드 선택 시만 활성(기존 btn_teachDatum 활성 로직과 동일 게이팅).

**(B) BtnReanchor_Click 핸들러**:
1. 현재 선택 = DatumConfig datum (InspectionListView.SelectedParam). 아니면 안내 후 return.
2. `TryFindTransformForReanchor(datum, out T, out err)` — 실패 시 CustomMessageBox("재앵커 불가", err) 후 return (무변경).
3. **미리보기**: 대상 ROI 집합(이 datum 을 DatumRef 로 하는 측정들의 지오메트리 + 그 Owner FAI ROI + datum 자체 ROI)을 **복제본에 T 적용**하여 migrated RoiDefinition 목록 생성 → halconViewer.UpdateDisplayState 로 노란 오버레이 표시. (원본 필드는 아직 불변)
   - 구현 단순화: BuildPointRoiDefinitions 로 각 측정의 현재 ROI → RoiDefinition 얻고, 그 center 를 T 로 변환한 미리보기 목록 생성(TransformRoiCenterInPlace 재사용 가능).
4. 확인 모달: "이 Datum 을 참조하는 측정 N개 + 검색 ROI 를 새 마스터 위치로 이전합니다. 미리보기(노란색)가 실제 특징 위에 얹혀 있는지 확인하세요. 진행할까요? (레시피가 자동 백업됩니다)" OK/Cancel.
   - Cancel → 원래 오버레이 복원(현재 선택 재렌더) 후 return, **무변경**.
5. OK →
   a. **레시피 백업**: 현재 레시피 파일을 `<recipe>.bak_YYYYMMDDHHmmss` 로 복사(파일 복사만; 실패해도 진행 여부는 사용자 이미 동의 — 단 백업 실패 시 경고 로그).
   b. **커밋**: datum 을 DatumRef 로 하는 각 측정에 TransformMeasurementGeometry(m, T); EdgePairDistance 또는 FAI rect 사용 시 그 FAI 에 TransformFaiRoi(fai, T)(FAI 당 1회, 중복 방지); TransformDatumOwnRois(datum, T).
   c. RaisePropertyChanged + RefreshParamEditor + 캔버스 재렌더.
   d. 안내: "이전 완료. 이제 이 datum 을 **새 마스터로 재티칭**(패턴: 모델 재생성 / 라인핏: Teach)한 뒤 Recipe Save 하세요." — **재티칭은 사용자가 기존 버튼으로 수행**(자동 호출 시 순서·실패 처리 복잡 → v1 은 수동 안내).
   ⚠ 커밋은 순수 메모리 필드 변경. Recipe Save 는 사용자가(재티칭 후) 명시적으로.
  </action>
  <verify><automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated></verify>
  <done>빌드 PASS. 버튼→Find→미리보기→확인→백업→커밋 흐름. Cancel/Find실패 시 무변경. 커밋 후 재티칭 안내.</done>
</task>

<task type="auto">
  <name>Task 4: 회귀 가드 + 규칙 감사</name>
  <files>(검증 전용)</files>
  <action>
git diff 로: 기존 측정/FAI/Datum 클래스의 필드/직렬화 무변경(MainView 만 추가), BtnTestFindDatum_Click 무변경(find 로직 재사용은 신규 메서드로 분리, 기존 핸들러 미수정), MeasurementBase 등 무변경.
규칙: 삼항 0 / C# 8+ 0 / 신규 //YYMMDD hbk 0.
  </action>
  <verify><automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Rebuild -v:minimal -nologo</automated></verify>
  <done>Rebuild PASS(신규 warning 0). 회귀 가드 + 규칙 감사 통과.</done>
</task>

</tasks>

<verification>
1. Debug|x64 Build PASS
2. Find 실패(새 마스터 크게 이탈) → "재앵커 불가" + 무변경
3. 미리보기 노란 ROI 가 새 마스터 특징 위에 얹힘 → 확인 → 이전 → (재티칭) → 검사 시 측정값 ≈ Nominal (PASS)
4. Cancel → 무변경
5. 각도 타입 ROI 도 위치 이전됨(측정값 단위와 무관)
</verification>

<success_criteria>
- 마스터 교체 시 측정 ROI 일괄 이전(재작업 0), 미리보기+백업+안전중단 안전장치 동작
- 순서 Find→이전→재티칭 준수, datum 별 T
- 배율 동일 전제에서 이전 후 측정값이 공칭 범위
</success_criteria>

<output>
완료 후 260715-rma-SUMMARY.md 생성. HUMAN-UAT: (1) 실제 새 마스터로 미리보기 육안 정합 확인, (2) 이전+재티칭 후 검사 측정값이 공칭 범위인지, (3) 배율 상이 케이스에서 오차 발생 여부(전제 위반 감지).
</output>
