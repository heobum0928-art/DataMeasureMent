---
phase: quick-260518-vxp
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/Sequence/Action/MeasurementBase.cs
  - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
autonomous: true
requirements: [QUICK-260518-VXP]

must_haves:
  truths:
    - "Datum 노드에서 Load Image 시 선택 경로가 DatumConfig.TeachingImagePath 에 기록된다"
    - "FAI 노드 PropertyGrid 에서 자식 Measurement 가 1개 이상이면 레거시 Edge 파라미터가 숨겨진다"
    - "측정/FAI 노드 선택 시 해당 ROI 가 노란색으로 하이라이트되고 명칭 라벨이 캔버스에 표시된다"
    - "Tol-/Tol+ 칸에 부호 무관하게 입력해도 NominalValue 중심의 올바른 공차 범위가 적용된다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "IOfflineImageParam 구현 — TeachingImagePath get/set"
    - path: "WPF_Example/Sequence/Action/MeasurementBase.cs"
      provides: "EvaluateJudgement 절대값 공차 처리"
    - path: "WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs"
      provides: "동적 FAI 모드에서 레거시 Edge 파라미터 PropertyGrid 숨김"
    - path: "WPF_Example/Halcon/Display/HalconDisplayService.cs"
      provides: "FAI/measurement ROI 명칭 라벨 렌더"
  key_links:
    - from: "InspectionListView.button_loadImage_Click (Datum 분기)"
      to: "MainView.LoadAndDisplay → DatumConfig.SetLatestImagePath"
      via: "Datum 전용 경로 persistence"
      pattern: "TeachingImagePath"
    - from: "InspectionList_SelectionChanged (Measurement/FAI 분기)"
      to: "halconViewer ROI 하이라이트"
      via: "selectedRoiId 전달"
      pattern: "UpdateDisplayState.*selId"
---

<objective>
Phase 23.1 EdgeToLineDistance ROI 티칭 SIMUL UAT 후 발견된 UI/UX 갭 4건을 정리한다. 알고리즘 로직은 정상 — 데이터 persistence / PropertyGrid 노출 / 캔버스 시각화 / 입력 UX 의 갭만 수정한다.

Purpose: Datum Load 가 올바른 경로에 기록되도록, 사용자 혼란을 주는 죽은 파라미터를 숨기도록, 선택 항목을 캔버스에서 구분 가능하도록, 공차 입력 부호 실수를 방지하도록 한다.
Output: 4개 갭 수정 — 회귀 0 (Shot 노드 Load / 비동적 FAI PropertyGrid / 기존 INI 직렬화 무변경).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

<interfaces>
<!-- 실행자가 필요로 하는 핵심 계약. 코드베이스에서 추출. 추가 탐색 불필요. -->

IOfflineImageParam (WPF_Example/Sequence/Param/CameraParam.cs L31-35):
```csharp
public interface IOfflineImageParam {
    string GetLatestImagePath();
    void SetLatestImagePath(string imagePath);
}
```

ShotConfig 의 IOfflineImageParam 구현 패턴 (ShotConfig.cs L19-28) — 동일 패턴으로 DatumConfig 에 적용:
```csharp
public string GetLatestImagePath() { return SimulImagePath; }
public void SetLatestImagePath(string imagePath) { SimulImagePath = imagePath; }
```

DatumConfig (DatumConfig.cs):
- L15: `public class DatumConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor`
- L35: `public string TeachingImagePath { get; set; } = "";` (Phase 22 IMG-01)
- ParamBase reflection 직렬화가 TeachingImagePath(string) 를 자동 처리.

MeasurementBase.EvaluateJudgement (MeasurementBase.cs L64-76) — 현재:
```csharp
double lower = NominalValue + ToleranceMinus;
double upper = NominalValue + TolerancePlus;
if (lower > upper) { double tmp = lower; lower = upper; upper = tmp; }
```

FAIConfig (FAIConfig.cs):
- L11: `public class FAIConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor`
- L31: `public List<FAIConfig> ... Measurements` 참조 (실제: Measurements 리스트 보유)
- L66-74 레거시 FAI-레벨 Edge 필드: EdgeThreshold, Sigma, EdgeDirection, EdgeSelection, EdgeSampleCount, EdgeTrimCount, EdgePolarity
- L253-263: `IsHiddenForEdgeMeasureType(string name, string edgeMeasureType)` — 현 hideFunc.
- L231-238: BuildFilteredProperties — DynamicPropertyHelper.FilterProperties 호출.
- FAIConfig 는 `Measurements` (List) 프로퍼티 보유 — `fai.Measurements.Count` 로 동적 모드 판정.

HalconDisplayService.Render rois 루프 (HalconDisplayService.cs L42-106):
- selectedRoiId == roi.Id → roiColor="yellow", width=3; 아니면 green/width=2.
- Rectangle 은 L97-100 DrawRectangleOutline 으로 렌더 — 라벨 없음.
- DrawRoiLabelAt(HWindow window, double row, double col, string label) (L803-816) — yellow 텍스트 라벨 렌더, 이미 존재.

GetCurrentFAIRois (MainView.xaml.cs L173-): RoiDefinition.Id/Name 채워서 반환. FAI ROI Id=FAIName, measurement ROI Id=FAIName+"_"+measName, Name=measName.

LoadAndDisplay (MainView.xaml.cs L295-333): OpenFileDialog 후 `if (param is IOfflineImageParam offlineImageParam) offlineImageParam.SetLatestImagePath(dialog.FileName);`
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: #3 Datum Load 경로 persistence + #Tol 공차 절대값 처리</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs, WPF_Example/UI/ContentItem/MainView.xaml.cs, WPF_Example/Sequence/Action/MeasurementBase.cs</files>
  <behavior>
    - #3: Datum 노드에서 Load Image → 선택한 이미지 경로가 DatumConfig.TeachingImagePath 에 기록된다 (SimulImagePath 아님).
    - #3: Shot 노드 Load 동작은 무변경 — ShotConfig.SimulImagePath 에 기록 (회귀 0).
    - #Tol: ToleranceMinus=0.030 (양수) + NominalValue=10 → lower=9.970, upper=10.030 (정상 ± 범위).
    - #Tol: 비대칭 TolerancePlus=0.050 / ToleranceMinus=0.020 → lower=Nom-0.020, upper=Nom+0.050 (절대값 처리, 정상 동작).
    - #Tol: ToleranceMinus=-0.030 (음수, 기존 입력 방식) → lower=Nom-0.030 (동일 결과, 하위호환).
  </behavior>
  <action>
#3 — DatumConfig 가 IOfflineImageParam 을 구현하게 한다:
1. DatumConfig.cs L15 클래스 선언에 `, ReringProject.Sequence.IOfflineImageParam` 추가 (또는 `IOfflineImageParam` — 동일 네임스페이스). ShotConfig.cs L9 패턴 동일.
2. DatumConfig 에 IOfflineImageParam 메서드 2개 추가 (ShotConfig.cs L19-28 패턴 그대로). TeachingImagePath 를 backing 으로:
   ```csharp
   //260518 hbk IOfflineImageParam — Datum 노드 Load 버튼이 선택 경로를 TeachingImagePath 에 기록
   public string GetLatestImagePath() { return TeachingImagePath; }
   public void SetLatestImagePath(string imagePath) { TeachingImagePath = imagePath; }
   ```
   XML 주석 부착 (신규 public 메서드). DatumConfig 는 Allman 스타일 — 파일 스타일 따름.
3. MainView.xaml.cs LoadAndDisplay (L295-333) 는 무수정 — 이미 `if (param is IOfflineImageParam offlineImageParam) offlineImageParam.SetLatestImagePath(...)` 분기 보유. 단, InspectionListView.button_loadImage_Click 의 Datum 분기는 `ResolveDatumCameraParam(datum)` 가 반환하는 ShotConfig 를 LoadAndDisplay 에 넘기므로 경로가 여전히 ShotConfig 로 간다.
   → InspectionListView.xaml.cs button_loadImage_Click L617-622 Datum 분기를 수정: 표시는 Shot 으로 위임하되 경로 저장은 DatumConfig 로 가도록. 권장 — LoadAndDisplay 에 표시용 param + 경로저장용 param 분리 오버로드 추가. MainView.xaml.cs 에 신규 메서드:
   ```csharp
   //260518 hbk #3 — 표시용(displayParam)과 경로 persistence(pathSinkParam) 분리. Datum 노드 Load 전용.
   public void LoadAndDisplay(ICameraParam displayParam, IOfflineImageParam pathSinkParam)
   ```
   본문은 기존 LoadAndDisplay 복제하되 L309-311 의 SetLatestImagePath 대상을 `pathSinkParam` (null 이면 skip) 으로 변경. 기존 1-인자 LoadAndDisplay 는 `LoadAndDisplay(param, param as IOfflineImageParam)` 로 위임 가능 (코드 중복 제거). InspectionListView.xaml.cs 는 이 파일에 없으므로 Datum 분기 한 줄만 수정: `mParentWindow.mainView.LoadAndDisplay(resolved, datumForLoad);` — datumForLoad 가 IOfflineImageParam 이 됨.
   주의: InspectionListView.xaml.cs 는 files_modified 에 없음 → 이 task 에서 함께 수정 필요. files_modified 에 InspectionListView.xaml.cs 를 포함시킨다 (executor 가 frontmatter 갱신).

#Tol — MeasurementBase.EvaluateJudgement (MeasurementBase.cs L68-69) 수정:
   ```csharp
   double lower = NominalValue - System.Math.Abs(ToleranceMinus); //260518 hbk #Tol — 부호 무관 절대값 처리
   double upper = NominalValue + System.Math.Abs(TolerancePlus);  //260518 hbk #Tol
   ```
   L70-73 의 lower>upper swap 블록은 절대값 처리 후 항상 lower<=upper 이므로 유지해도 무해(idempotent) — 안전판으로 그대로 둔다. L61 XML 주석의 "lower = Nominal + ToleranceMinus (음수 허용)" 문구를 "lower = Nominal - Abs(ToleranceMinus), upper = Nominal + Abs(TolerancePlus)" 로 갱신.
   PropertyGrid 라벨 명확화: MeasurementBase.cs L26-30 TolerancePlus/ToleranceMinus 에 `[System.ComponentModel.Description(...)]` 추가 — "공차는 부호 무관하게 입력 (절대값 적용). 비대칭 공차도 지원." 취지. DatumConfig.cs L114 의 Description attribute 사용 패턴 참고.
   INI 하위호환: 기존 음수 ToleranceMinus 도 Abs 처리로 동일 결과 — 회귀 0.
  </action>
  <verify>
    <automated>MISSING — SIMUL_MODE 빌드 후 수동 UAT. 빌드 검증: msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 가 0 error</automated>
  </verify>
  <done>DatumConfig 가 IOfflineImageParam 구현, Datum 노드 Load 시 TeachingImagePath 기록됨. Shot 노드 Load 는 SimulImagePath 무변경. EvaluateJudgement 가 Abs 처리. 빌드 0 error.</done>
</task>

<task type="auto">
  <name>Task 2: #4 FAI 레거시 Edge 탭 동적 숨김</name>
  <files>WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs</files>
  <action>
FAIConfig 가 자식 Measurement 를 1개 이상 보유할 때 (동적 FAI 모드) 레거시 FAI-레벨 Edge 파라미터를 PropertyGrid 에서 숨긴다.

1. FAIConfig.cs L253-263 `IsHiddenForEdgeMeasureType` 는 static — Measurements.Count 에 접근하려면 인스턴스 컨텍스트 필요. BuildFilteredProperties (L231-238) 가 인스턴스 메서드이므로 거기서 동적 판정한다.
2. BuildFilteredProperties 의 hideFunc 람다 (L237) 를 확장: 기존 `IsHiddenForEdgeMeasureType` 호출에 더해 동적 모드 숨김 조건 OR.
   ```csharp
   //260518 hbk #4 — 동적 FAI 모드(자식 Measurement >= 1)에서 레거시 FAI-레벨 Edge 파라미터 숨김.
   //  각 Measurement 가 자기 파라미터를 보유하므로 FAI-레벨 Edge 값은 죽은 값 → 사용자 혼란 방지.
   bool hasDynamicMeasurements = Measurements != null && Measurements.Count > 0;
   ```
   그리고 hideFunc:
   ```csharp
   name => IsHiddenForEdgeMeasureType(name, EdgeMeasureType)
           || (hasDynamicMeasurements && IsLegacyEdgeParam(name))
   ```
3. 신규 private static 헬퍼 `IsLegacyEdgeParam(string name)` 추가 — 숨길 레거시 Edge 프로퍼티 이름 매칭:
   EdgeMeasureType, EdgeThreshold, Sigma, EdgeDirection, EdgeSelection, EdgeSampleCount, EdgeTrimCount, EdgePolarity.
   주의: EdgeMeasureType 자체를 숨길지 결정 — 동적 모드에서 EdgeMeasureType 은 measurement 생성 시 사용되는 죽은 값이므로 숨김 대상에 포함. (단 ItemsSource 소스 List 들 — EdgeMeasureTypeList/EdgeDirectionList/EdgePolarityList — 은 sourceNames 화이트리스트(L232-236)에 있어 강제 포함되나, hideFunc 가 *List 이름은 매칭 안 하므로 무관. 보수적으로 IsLegacyEdgeParam 은 *List 이름은 매칭하지 않는다 → ItemsSource 누락으로 인한 콤보 깨짐 회피. 어차피 부모 프로퍼티가 숨겨지면 List 도 화면에 안 나옴.)
4. INI 직렬화 영향 0 — ParamBase 직렬화는 GetType().GetProperties() Reflection 경로 (ICustomTypeDescriptor 우회). 숨겨진 필드도 INI 에 그대로 read/write → 하위호환 유지. L223 주석 참고.
5. 회귀 0 확인: Measurements.Count == 0 (비동적 FAI) → hasDynamicMeasurements=false → 레거시 Edge 파라미터 그대로 노출 (기존 동작).
   CLAUDE.md: C# 7.2, FAIConfig 는 K&R 스타일 (파일 확인 — Phase 19 코드가 K&R). 수정 라인 //260518 hbk 주석.
  </action>
  <verify>
    <automated>MISSING — SIMUL_MODE 빌드 후 수동 PropertyGrid 확인. 빌드 검증: msbuild 0 error</automated>
  </verify>
  <done>Measurement 1개 이상인 FAI 노드 선택 시 PropertyGrid 에 레거시 Edge 파라미터 미표시. 비동적 FAI(Measurement 0개) 는 그대로 노출. INI 직렬화 무변경. 빌드 0 error.</done>
</task>

<task type="auto">
  <name>Task 3: #6 ROI 선택 하이라이트 + 명칭 라벨</name>
  <files>WPF_Example/Halcon/Display/HalconDisplayService.cs, WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
선택된 measurement/FAI 의 ROI 를 노란색으로 하이라이트하고, 모든 ROI 에 명칭 라벨을 캔버스에 표시한다.

A. ROI 명칭 라벨 렌더 — HalconDisplayService.cs Render rois 루프:
1. 하이라이트 색상 분기는 이미 존재 (L49-58: selectedRoiId == roi.Id → yellow/3). 추가 작업 불필요.
2. Rectangle ROI 라벨 누락이 갭. L97-100 DrawRectangleOutline 직후 (Rectangle 분기 안), ROI 명칭 라벨을 그린다:
   ```csharp
   //260518 hbk #6 — Rectangle ROI 명칭 라벨 (좌상단 외곽 위쪽). Datum 라벨 패턴(DrawRoiLabelAt) 재사용.
   if (!string.IsNullOrEmpty(roi.Name))
       DrawRoiLabelAt(window, roi.Row1 - 22, roi.Column1, roi.Name);
   ```
   DrawRoiLabelAt (L803-816) 은 yellow 텍스트 — 선택/비선택 무관 라벨 가독성 확보. roi.Name 은 GetCurrentFAIRois 가 measurement 이름/FAIName 으로 채워줌 (interfaces 참조).
3. Polygon 분기 (L91-96) 에도 라벨 추가: 첫 점 기준 위쪽.
   ```csharp
   //260518 hbk #6 — Polygon ROI 명칭 라벨
   if (!string.IsNullOrEmpty(roi.Name) && pts != null && pts.Count > 0)
       DrawRoiLabelAt(window, pts[0].Y - 22, pts[0].X, roi.Name);
   ```
4. Circle 분기 (L62-88) 에도 라벨 추가 — `continue` 직전: `DrawRoiLabelAt(window, roi.CenterRow - roi.Radius - 22, roi.CenterCol, roi.Name);` (Name 비어있지 않을 때만).
   HalconDisplayService.cs 는 Allman 스타일 — 파일 따름.

B. 선택 노드 → ROI 하이라이트 배선 — MainView.xaml.cs:
1. 현 갭: InspectionList_SelectionChanged → SetParam → DisplayParam 경로는 selectedRoiId 를 전달 안 함 (L307 LoadAndDisplay, L338 DisplayContextToViewer 모두 null). Measurement/FAI 노드 클릭 시 하이라이트 안 됨.
2. DisplayParam (MainView.xaml.cs L335-) 또는 SetParam (L241-247) 에서 선택된 param 의 RoiId 를 도출해 halconViewer 에 전달.
   - param 이 FAIConfig 면 selId = FAIConfig.FAIName.
   - param 이 EdgeToLineDistanceMeasurement(또는 MeasurementBase) 면 selId = 그 measurement 를 포함하는 FAI 의 FAIName + "_" + measurementName (GetCurrentFAIRois 의 measurement ROI Id 규칙과 일치 — interfaces L191 `fai.FAIName + "_" + measName`). measName 은 MeasurementName 비면 TypeName.
3. DisplayParam 의 DisplayContextToViewer (L338) 호출 후 또는 ConvertParamRects 결과 렌더 직후, selId 가 있으면 `halconViewer.SetSelectedRoi(selId);` 호출 (HalconViewerControl.xaml.cs L149-153 SetSelectedRoi 존재 — Render 재호출).
   가장 안전한 위치: DisplayParam 끝부분 (RefreshFAIResultRows 직후) 에 선택 param 기반 selId 계산 → SetSelectedRoi. param==null 이면 SetSelectedRoi(null).
4. measurement → FAI 역탐색은 기존 헬퍼 재사용: `FindFaiNameContainingMeasurement` (MainView.xaml.cs 에 존재 — L1158/L1314 에서 사용). measurement ROI Id 조립:
   ```csharp
   //260518 hbk #6 — 선택 노드의 ROI 하이라이트 ID 도출
   string selRoiId = null;
   if (param is FAIConfig faiSel) selRoiId = faiSel.FAIName;
   else if (param is MeasurementBase measSel) {
       string faiName = FindFaiNameContainingMeasurement(measSel);
       string mName = measSel.MeasurementName;
       if (string.IsNullOrEmpty(mName)) mName = measSel.TypeName;
       if (!string.IsNullOrEmpty(faiName)) selRoiId = faiName + "_" + mName;
   }
   halconViewer.SetSelectedRoi(selRoiId);
   ```
   주의: GetCurrentFAIRois 가 measurement ROI Name 으로 measName(=MeasurementName 비면 TypeName) 을 쓰고 Id 로 `FAIName + "_" + measName` 을 쓴다 (interfaces L188-191). selRoiId 조립이 정확히 그 규칙과 일치해야 하이라이트 매칭됨 — measName 도출 로직을 GetCurrentFAIRois 와 동일하게 한다.
5. ROI 자체 목록도 갱신 필요할 수 있음 — DisplayParam 이 ConvertParamRects(param) 를 쓰는데, FAI/measurement 노드는 GetCurrentFAIRois 의 multi-ROI 가 더 정확. 단 회귀 위험 최소화를 위해 ROI 목록 변경은 하지 않고 SetSelectedRoi 만 추가한다 (rois 자체가 이미 GetCurrentFAIRois 경로로 그려지는 노드 선택 흐름이면 라벨+하이라이트 동작). 만약 DisplayParam 경로 rois 에 measurement ROI 가 없어 하이라이트가 안 보이면, executor 는 InspectionList_SelectionChanged 의 Measurement/FAI 분기 (InspectionListView.xaml.cs L445-486) 에서 `mParentWindow.mainView.halconViewer.UpdateDisplayState(GetCurrentFAIRois 동등, selId, null, null)` 를 호출하는 방식으로 대체 — MainView 에 public 헬퍼 `HighlightSelectedRoi(ParamBase param)` 를 추가하고 InspectionListView 의 Measurement/FAI 분기에서 호출. InspectionListView.xaml.cs 수정 시 files_modified 에 추가.
   권장: MainView 에 `public void HighlightSelectedRoi(ParamBase param)` 추가 — 위 selRoiId 도출 + `var rois = GetCurrentFAIRois(); halconViewer.UpdateDisplayState(rois, selRoiId, null, null);`. InspectionListView 의 Measurement 분기(L445)와 FAI 분기(L467)에서 각각 `mParentWindow.mainView.HighlightSelectedRoi(itemParam as ParamBase);` 호출. 이게 rois+하이라이트 일관 갱신으로 가장 견고.
   CLAUDE.md: 수정 라인 //260518 hbk, 파일별 brace 스타일.
  </action>
  <verify>
    <automated>MISSING — SIMUL_MODE 빌드 후 수동 캔버스 확인. 빌드 검증: msbuild 0 error</automated>
  </verify>
  <done>Measurement/FAI 노드 선택 시 해당 ROI 가 노란색 하이라이트 + width 3 으로 렌더되고, 모든 ROI(Rect/Circle/Polygon)에 명칭 라벨이 캔버스에 표시된다. 빌드 0 error.</done>
</task>

</tasks>

<verification>
- SIMUL_MODE 빌드 0 error (msbuild Debug/x64).
- #3: Datum 노드 선택 → Load Image → 파일 선택 → DatumConfig PropertyGrid 의 TeachingImagePath 가 선택 경로로 채워짐. Shot 노드 Load 는 SimulImagePath 만 갱신 (회귀 확인).
- #4: Measurement 보유 FAI 노드 선택 → PropertyGrid 에 EdgeThreshold/Sigma/EdgeDirection 등 레거시 Edge 파라미터 미표시. Measurement 0개 FAI → 그대로 표시.
- #6: Measurement/FAI 노드 선택 → 캔버스에서 선택 ROI 노란색 + 명칭 라벨 표시. 다른 ROI 는 green + 라벨.
- #Tol: ToleranceMinus 에 양수 0.030 입력 → 측정 판정 범위가 [Nom-0.030, Nom+TolPlus] 로 정상.
- INI 레시피 하위호환: 기존 INI 로드/저장 무변경 (DatumConfig/FAIConfig/MeasurementBase 직렬화 필드 변경 없음).
</verification>

<success_criteria>
- 4개 갭(#3, #4, #6, #Tol) 모두 수정 완료.
- 회귀 0: Shot 노드 Load / 비동적 FAI PropertyGrid / 기존 INI 직렬화 무변경.
- CLAUDE.md 준수: C# 7.2, .NET 4.8, 파일별 brace 스타일, 수정 라인 //260518 hbk 주석, 신규 public 메서드 XML 주석.
- SIMUL_MODE 빌드 0 error.
</success_criteria>

<output>
After completion, create `.planning/quick/260518-vxp-phase-23-1-uat-4-datum-load-teachingimag/260518-vxp-SUMMARY.md`
</output>
