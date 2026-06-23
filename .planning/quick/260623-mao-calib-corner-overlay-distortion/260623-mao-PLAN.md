---
phase: quick-260623-mao
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
  - WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
autonomous: true
requirements: [CALIB-VIZ-01]
must_haves:
  truths:
    - "검출 성공 후 CalibrationViewer 위에 검출된 saddle 코너가 십자(+) 마커로 표시된다"
    - "새 이미지 로드/검출 실패 시 직전 코너 마커가 사라진다"
    - "리포트에 중앙부 평균 간격(px) vs 외곽부 평균 간격(px) 실제 값이 표시된다"
    - "리포트에 X축 편차% / Y축 편차% 가 종합 편차% 와 함께 표시된다"
    - "임계 초과 시 기존 [경고] 라벨이 그대로 동작한다 (회귀 0)"
  artifacts:
    - path: "WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs"
      provides: "CalibrationResult 에 코너 좌표 + 중앙/외곽 평균 + X/Y 편차% 노출"
      contains: "CornerRows"
    - path: "WPF_Example/Halcon/Display/HalconDisplayService.cs"
      provides: "Calib-Corners RoiId 배치 DispCross 렌더 분기"
      contains: "Calib-Corners"
    - path: "WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs"
      provides: "코너 오버레이 push + 리포트 보강"
      contains: "SetInspectionOverlays"
  key_links:
    - from: "CalibrationWindow.DetectButton_Click"
      to: "CalibrationViewer.SetInspectionOverlays"
      via: "EdgeInspectionOverlay(RoiId=Calib-Corners, Points=코너)"
      pattern: "SetInspectionOverlays"
    - from: "HalconDisplayService.Render"
      to: "HOperatorSet.DispCross"
      via: "Calib-Corners 분기 batch render"
      pattern: "Calib-Corners"
---

<objective>
체커보드 캘리브레이션 검출 결과 시각화를 2가지로 강화한다.

(1) **코너 오버레이** — `CheckerboardCalibrationService` 가 검출한 saddle 코너 좌표(rows/cols)를 `CalibrationResult` 에 노출하고, `CalibrationWindow` 가 [검출] 성공 후 `CalibrationViewer`(HalconViewerControl) 의 **기존 오버레이 파이프라인**(`SetInspectionOverlays`)을 재사용해 코너 십자(+) 마커를 HALCON 창 내부에 렌더한다. "어디를 잡았는지" 육안 확인.

(2) **왜곡 수치 보강** — 현재는 종합 편차% 단일값만 표시. 중앙부 평균 간격(px) vs 외곽부 평균 간격(px) 실제값, X/Y 축별 편차%, 종합 편차% + 임계 초과 여부를 한눈에 보강.

Purpose: POC 시연 시 캘리브레이션 신뢰도를 사람이 즉시 판단(코너 위치 + 왜곡 수치)할 수 있게 한다.
Output: 코너 마커 표시 + 보강된 리포트 텍스트. 디스플레이/리포팅 전용 — caltab/undistort 미도입(D-07/D-08 텔레센트릭 결정 준수).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

<critical_constraints>
- **C# 7.2** — switch expression / nullable reference types / record 금지. Allman brace (Halcon 파일 컨벤션).
- **주석 규약** — 수정 라인에 `//260623 hbk` 마커 (기존 `//260623 hbk Phase 53` 마커 위에 누적, Phase 20 D-12 stacking 패턴).
- **HALCON 색상명** — 반드시 표준명("cyan"/"yellow"/"green"/"red"). 비표준명은 SetColor 예외 → catch swallow → 렌더 블록 전체 silent 미표시 (메모리 사실). 본 plan 은 코너 마커에 **"cyan"** 사용.
- **HALCON 오버레이는 창 내부 렌더** — WPF 오버레이는 airspace 로 가려짐. 반드시 기존 `HalconDisplayService.Render` 의 `DispCross`/`DispLine` 경로(HWindow 내부 렌더)를 재사용. **새 HWindow 드로잉 경로 발명 금지.**
- HOperatorSet 호출은 try/catch (기존 HalconDisplayService catch swallow 관습 유지).
</critical_constraints>

<interfaces>
<!-- 확정된 오버레이 파이프라인 (executor 는 이 계약을 그대로 사용 — 코드베이스 재탐색 불필요) -->

이미 검증된 오버레이 경로:
- `HalconViewerControl.SetInspectionOverlays(IEnumerable<EdgeInspectionOverlay> overlays)` (public)
  → 내부 `_inspectionOverlays` 갱신 후 `Render()` → `HalconDisplayService.Render(... _inspectionOverlays ...)` 호출.
  → `null` 전달 시 오버레이 클리어 (Clear 후 빈 리스트).

`EdgeInspectionOverlay` (WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs):
```csharp
public class EdgeInspectionOverlay {
    public string RoiId { get; set; }
    public List<EdgeInspectionPoint> Points { get; set; } = new List<EdgeInspectionPoint>();
    public double LineRow1/LineColumn1/LineRow2/LineColumn2 { get; set; }
}
public class EdgeInspectionPoint { public double Row { get; set; } public double Column { get; set; } }
```

`HalconDisplayService.Render` 의 검증된 배치-점 렌더 패턴 (FAI-EdgeRaw 분기, line 187~210):
```csharp
// RoiId 분기 → Points 를 HTuple rows/cols 로 누적 → DispCross 일괄 → continue (라인 미렌더)
HTuple rRows = new HTuple(); HTuple rCols = new HTuple();
foreach (var p in overlay.Points) { rRows = rRows.TupleConcat(p.Row); rCols = rCols.TupleConcat(p.Column); }
HOperatorSet.SetColor(window, "yellow");
HOperatorSet.SetLineWidth(window, 1);
HOperatorSet.DispCross(window, rRows, rCols, 4.0, 0.0);
continue;  // 기본 DispLine + 큰 X 마커 loop skip
```
→ **이 패턴을 그대로 미러링**해서 RoiId="Calib-Corners" 분기 추가 (색상만 "cyan", 마커 크기 6.0).

`CalibrationResult` 현재 멤버 (53-01): MmPerPixel, MmPerPixelX, MmPerPixelY, MeanSpacingPx, CenterOuterDeviationPct, IsDistortionWarn, CornerCount.

`ComputeCenterOuterDeviationPct(gapsX, gapsY, image, out centerMean, out outerMean)` 는 이미 종합 centerMean/outerMean 을 out 으로 산출 중 (TryCalibrate L103~104). 현재 result 에 미반영.

`CalibrationViewer.DetectButton_Click` 입력 = `CalibrationViewer.CurrentImage` (HImage). 코너 검출 좌표는 서비스 내부 `rows`/`cols` HTuple — 현재 result 미노출.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1: CalibrationResult 에 코너 좌표 + 왜곡 상세 수치 노출 (서비스)</name>
  <files>WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs</files>
  <behavior>
    - TryCalibrate 성공 시 result.CornerRows / CornerCols 에 검출 코너 좌표 전체가 담긴다 (length == CornerCount).
    - result.CenterMeanPx / OuterMeanPx 에 중앙부·외곽부 평균 간격(px) 실값이 담긴다 (한쪽 그룹 비면 0).
    - result.DeviationXPct / DeviationYPct 에 X축(가로 간격)·Y축(세로 간격)별 중앙↔외곽 편차%가 담긴다 (한쪽 그룹 비면 0% 가드).
    - 기존 MmPerPixel/CenterOuterDeviationPct/IsDistortionWarn 값/계산은 불변 (회귀 0).
  </behavior>
  <action>
1. `CalibrationResult` 클래스에 public 프로퍼티 추가 (기존 7종 아래에, `//260623 hbk` 마커):
   - `public double[] CornerRows { get; set; }` — 검출 코너 row 좌표 전체
   - `public double[] CornerCols { get; set; }` — 검출 코너 col 좌표 전체
   - `public double CenterMeanPx { get; set; }` — 중앙부 평균 간격(px)
   - `public double OuterMeanPx { get; set; }` — 외곽부 평균 간격(px)
   - `public double DeviationXPct { get; set; }` — X축(가로) 중앙↔외곽 편차%
   - `public double DeviationYPct { get; set; }` — Y축(세로) 중앙↔외곽 편차%

2. 축별 편차% 산출 헬퍼 추가 (private static). 기존 `ComputeCenterOuterDeviationPct` 의 종합 로직을 단일 축 gaps 리스트로 분리한 형태:
   ```
   //260623 hbk: 단일 축 gaps 의 중앙↔외곽 편차% (X/Y 축별 리포트용). 종합과 동일 반경 게이트 + 0% 가드.
   private static double ComputeAxisDeviationPct(List<EdgeGap> gaps, HImage image)
   ```
   - 이미지 크기 조회 실패 → 0 반환 (기존 패턴).
   - innerR = diag*0.33, outerR = diag*0.66 (기존 상수 일치).
   - centerGaps/outerGaps 누적은 기존 `AccumulateRadialGroups` 재사용.
   - 한쪽 그룹 비면 0% 반환 (가드). `abs(outerMean - centerMean)/centerMean*100`.

3. `TryCalibrate` 의 result 생성부(L106~115)에서:
   - 기존 `ComputeCenterOuterDeviationPct(gapsX, gapsY, image, out centerMean, out outerMean)` 호출은 유지.
   - 신규 result 멤버 채우기:
     - `CenterMeanPx = centerMean`, `OuterMeanPx = outerMean`
     - `DeviationXPct = ComputeAxisDeviationPct(gapsX, image)`
     - `DeviationYPct = ComputeAxisDeviationPct(gapsY, image)`
     - `CornerRows` / `CornerCols` = rows/cols HTuple → double[] 변환 (for-loop `for i: arr[i] = rows[i].D`). rows.Length 길이.
   - 기존 멤버 대입(MmPerPixel 등)은 그대로.

C# 7.2 / Allman / try-catch 컨벤션 준수. 신규 외부 의존 없음.
  </action>
  <verify>
    <automated>cd /c/Info/Project/DataMeasurement && grep -n "CornerRows\|CornerCols\|CenterMeanPx\|OuterMeanPx\|DeviationXPct\|DeviationYPct\|ComputeAxisDeviationPct" WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs</automated>
  </verify>
  <done>CalibrationResult 에 6개 신규 프로퍼티 + ComputeAxisDeviationPct 헬퍼 존재, TryCalibrate 가 모두 채움. 기존 MmPerPixel/IsDistortionWarn 계산 무변경.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 2: HalconDisplayService 에 Calib-Corners 배치 코너 렌더 분기</name>
  <files>WPF_Example/Halcon/Display/HalconDisplayService.cs</files>
  <behavior>
    - inspectionOverlays 에 RoiId="Calib-Corners" 오버레이가 있으면 그 Points 가 cyan 작은 십자(+)로 일괄 렌더된다.
    - 라인(DispLine)이나 큰 X 마커는 그려지지 않는다 (continue).
    - 다른 RoiId(FAI-Edge*/Group-*/Datum 등) 동작 무변경 (회귀 0).
  </behavior>
  <action>
`Render` 의 inspectionOverlays foreach 내부, **기존 `FAI-EdgeRaw` 분기 바로 다음**(또는 동일한 early-branch 위치, line ~187~210 인근)에 신규 분기 추가. FAI-EdgeRaw 패턴을 그대로 미러링:

```
//260623 hbk: 캘리브 검출 코너 일괄 가시화 (cyan 작은 +). FAI-EdgeRaw 패턴 미러 — 라인/큰X skip 위해 continue.
//  "cyan" 표준 색상명 (비표준명은 SetColor 예외 swallow → 미표시 위험).
else if (string.Equals(overlay.RoiId, "Calib-Corners", StringComparison.OrdinalIgnoreCase))
{
    if (overlay.Points != null && overlay.Points.Count > 0)
    {
        try
        {
            HTuple rRows = new HTuple();
            HTuple rCols = new HTuple();
            foreach (var p in overlay.Points)
            {
                rRows = rRows.TupleConcat(p.Row);
                rCols = rCols.TupleConcat(p.Column);
            }
            HOperatorSet.SetColor(window, "cyan");
            HOperatorSet.SetLineWidth(window, 1);
            HOperatorSet.DispCross(window, rRows, rCols, 6.0, 0.0);
        }
        catch
        {
            // display 예외 swallow (기존 FAI-EdgeRaw / RenderRawEdgePoints 관습)
        }
    }
    continue;
}
```

배치 위치 주의: 반드시 `FAI-Edge` StartsWith 분기보다 **앞에** 평가되어야 한다 ("Calib-Corners" 가 다른 prefix 분기에 안 걸리도록). FAI-EdgeRaw 분기와 형제 위치가 안전.

다른 라인/메서드 무수정.
  </action>
  <verify>
    <automated>cd /c/Info/Project/DataMeasurement && grep -n "Calib-Corners\|DispCross(window, rRows" WPF_Example/Halcon/Display/HalconDisplayService.cs</automated>
  </verify>
  <done>Calib-Corners 분기 존재, cyan + DispCross + continue. 기존 FAI-EdgeRaw / FAI-Edge* 분기 무변경.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 3: CalibrationWindow 코너 오버레이 push + 리포트 보강 + 빌드 검증</name>
  <files>WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs</files>
  <behavior>
    - DetectButton_Click 성공 시 검출 코너가 CalibrationViewer 에 cyan 십자로 표시된다.
    - 이미지 로드(LoadImageButton_Click) / 라이브 촬상(GrabImageButton_Click) / 검출 실패 시 직전 코너 오버레이가 사라진다.
    - txt_report 가 중앙/외곽 평균 간격(px) 실값 + X/Y 편차% + 종합 편차%를 모두 표시한다.
    - 기존 lbl_distortionWarn 임계 경고 동작 무변경.
  </behavior>
  <action>
1. 코너 오버레이 push 헬퍼 추가 (private):
   ```
   //260623 hbk: 검출 코너를 기존 오버레이 파이프라인(SetInspectionOverlays)으로 push. 새 HWindow 경로 발명 금지.
   private void ShowCornerOverlay(CalibrationResult result)
   ```
   - result?.CornerRows == null || CornerCols == null || length mismatch → `CalibrationViewer.SetInspectionOverlays(null)` 후 return.
   - `EdgeInspectionOverlay` 1개 생성: `RoiId = "Calib-Corners"`, `Points` = CornerRows/CornerCols 를 zip 한 `EdgeInspectionPoint` 리스트 (for-loop, Row=CornerRows[i], Column=CornerCols[i]).
   - `CalibrationViewer.SetInspectionOverlays(new List<EdgeInspectionOverlay> { overlay })`.
   - 필요 using: `System.Collections.Generic`, `ReringProject.Halcon.Models` (없으면 추가).

2. `DetectButton_Click` (검출 성공 분기, `_lastResult = result;` 이후):
   - `txt_report.Text` 문자열을 보강. 기존 3줄 → 다음 형태(예시, 한국어 라벨 유지):
     ```
     1 px = {MmPerPixel:F5} mm (X {MmPerPixelX:F5} / Y {MmPerPixelY:F5})
     평균 간격 {MeanSpacingPx:F2} px · 코너 {CornerCount}개
     중앙부 {CenterMeanPx:F2} px ↔ 외곽부 {OuterMeanPx:F2} px
     편차 종합 {CenterOuterDeviationPct:F2}% (X {DeviationXPct:F2}% / Y {DeviationYPct:F2}%)
     ```
     `string.Format(CultureInfo.InvariantCulture, ...)` 유지.
   - 성공 분기 끝(`btn_apply.IsEnabled = true;` 인근)에 `ShowCornerOverlay(result);` 호출.

3. 검출 실패 분기 (`btn_apply.IsEnabled = false; return;` 직전)에 `CalibrationViewer.SetInspectionOverlays(null);` 추가 — 직전 코너 클리어.

4. 새 이미지 무효화 지점 2곳 — `LoadImageButton_Click` 과 `GrabImageButton_Click` 의 `btn_apply.IsEnabled = false;` 인근에 `CalibrationViewer.SetInspectionOverlays(null);` 추가 (새 이미지 로드 시 직전 코너 제거).

5. **빌드 검증**: `cd WPF_Example && MSBuild DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build` (SIMUL_MODE) — exit 0, 신규 에러 0. 기존 baseline 경고(CS0618/CS0162/MSB3884)는 허용.

C# 7.2 / 기존 파일 brace 스타일(이 파일은 K&R-ish — 파일 스타일 추종) / `//260623 hbk` 마커.
  </action>
  <verify>
    <automated>cd /c/Info/Project/DataMeasurement && grep -n "ShowCornerOverlay\|SetInspectionOverlays\|DeviationXPct\|CenterMeanPx" WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs && "/c/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //p:Configuration=Debug //p:Platform=x64 //t:Build //v:minimal 2>&1 | grep -iE "error|Build succeeded"</automated>
  </verify>
  <done>검출 성공 시 ShowCornerOverlay 호출 + 보강 리포트, 로드/촬상/실패 시 SetInspectionOverlays(null) 클리어, MSBuild Debug/x64 PASS (신규 에러 0).</done>
</task>

</tasks>

<verification>
- `grep` 으로 Task 1~3 산출물(신규 프로퍼티/분기/헬퍼/호출) 모두 매치.
- MSBuild Debug/x64 Build PASS — exit 0, 신규 error 0 (기존 CS0618/CS0162/MSB3884 baseline 유지).
- 코너 마커 색상 = "cyan" (표준명) — SetColor swallow 결함 방지.
- 오버레이 경로 = `SetInspectionOverlays` → `HalconDisplayService.Render` (HWindow 내부 DispCross) — 새 드로잉 경로 0개.
- 회귀 가드: FAI-Edge*/Group-*/Datum 렌더 분기 무수정, 기존 MmPerPixel/IsDistortionWarn 계산 무수정.
- **SIMUL 육안 UAT (사람 확인 — 빌드 후):** 체커보드 이미지 로드 → [검출] → (a) 뷰어 위 cyan + 마커가 코너에 표시 (b) 리포트에 중앙/외곽 px + X/Y 편차% 표기 (c) 새 이미지 로드 시 마커 사라짐. (자동 빌드로 검증 완료, 육안은 사용자 별도 확인.)
</verification>

<success_criteria>
- [ ] CalibrationResult 가 코너 좌표(CornerRows/Cols) + 중앙/외곽 평균(px) + X/Y 편차% 노출
- [ ] HalconDisplayService 에 Calib-Corners cyan DispCross 배치 분기 (continue) 추가
- [ ] CalibrationWindow 검출 성공 시 코너 cyan + 마커 표시 (기존 SetInspectionOverlays 파이프라인 재사용)
- [ ] 이미지 로드/촬상/검출 실패 시 코너 오버레이 클리어
- [ ] 리포트가 중앙/외곽 평균 간격(px) + X/Y 축별 편차% + 종합 편차% 표기
- [ ] MSBuild Debug/x64 PASS (신규 에러 0), 회귀 0 (caltab/undistort 미도입)
</success_criteria>

<output>
After completion, create `.planning/quick/260623-mao-calib-corner-overlay-distortion/260623-mao-SUMMARY.md`
</output>
