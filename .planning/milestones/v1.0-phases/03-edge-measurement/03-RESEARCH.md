# Phase 3: 에지 측정 알고리즘 - Research

**Researched:** 2026-04-09
**Domain:** Halcon MeasurePos 에지 거리 측정 + FAIConfig 공차 판정 + 캔버스 오버레이
**Confidence:** HIGH (코드베이스 직접 검증)

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ALG-01 | FAI ROI 내에서 Halcon MeasurePos로 에지 페어 거리(mm)를 계산한다 | MeasurementAlgorithm.TryInspectSingleEdge 가 단일 에지 라인을 반환. 두 FAI ROI의 라인 사이 픽셀 거리 계산 후 PixelResolution 곱으로 mm 변환 필요. |
| ALG-02 | FAIConfig의 Tolerance 기준으로 OK/NG 판정을 수행한다 | FAIConfig.SetResult(double) 이미 구현됨. NominalValue ± Abs(LowerTolerance/UpperTolerance) 로 IsPass 결정. |
| ALG-04 | 측정 결과(에지 위치, 거리, 판정)를 캔버스에 오버레이로 표시한다 | HalconDisplayService.Render + MainResultViewerControl.UpdateDisplayState 파이프라인 존재. EdgeInspectionOverlay + DispText 확장 필요. |
</phase_requirements>

---

## Summary

Phase 2까지 ROI 정의(FAIConfig.ToRoiDefinition), 에지 감지 알고리즘(MeasurementAlgorithm.TryInspectSingleEdge), 캔버스 렌더링 파이프라인(HalconDisplayService + MainResultViewerControl)이 모두 코드베이스에 구현되어 있다. Phase 3의 핵심 작업은 이 세 계층을 "측정 서비스" 레이어로 연결하는 것이다.

현재 `Action_FAIMeasurement.cs`의 `EStep.Measure`는 스텁(stub)이다: 모든 FAI를 NominalValue로 설정하고 AllPass=true를 반환한다. Phase 3는 이 스텁을 실제 `FAIEdgeMeasurementService`로 교체한다.

거리 계산 방식: 하나의 FAI는 두 에지를 측정한다 — `MeasureType`(FirstToFirst, FirstToLast, LastToFirst, LastToLast) 필드에 따라 첫 번째/마지막 에지 위치를 선택하고, 두 에지 위치 사이의 픽셀 거리를 구한 후 `FAIConfig.PixelResolutionX` 또는 `PixelResolutionY`로 mm 변환한다.

오버레이 표시: `EdgeInspectionOverlay` 클래스는 라인 + 포인트를 담는다. 측정 결과용으로 두 에지 라인, 연결선, 거리 텍스트, OK/NG 배지를 `HalconDisplayService.Render`의 `inspectionOverlays`와 `displayMessages`로 전달한다.

**Primary recommendation:** `FAIEdgeMeasurementService` 신규 클래스를 `WPF_Example/Halcon/Algorithms/` 에 추가하고, `Action_FAIMeasurement`의 스텁을 이 서비스 호출로 교체한다. UI 오버레이는 `FAIMeasurementContext`에 `List<EdgeInspectionOverlay>` + `List<string>` 필드를 추가해 전달한다.

---

## Project Constraints (from CLAUDE.md)

- .NET Framework 4.8 + WPF + Halcon 24.11 — 변경 불가
- C# 7.2 문법만 사용 (record, nullable ref, switch expression 금지)
- `HOperatorSet.*` 호출은 반드시 `try { } catch { return false; }` 패턴으로 감쌈
- `HImage`는 반드시 `using` 또는 명시적 `Dispose()` (try/finally)
- `ActionBase.Run()` 에서 절대 throw 금지 — `FinishAction(EContextResult.Error)`로 전파
- `lock(_imageLock)` 패턴으로 `HImage` 공유 버퍼 보호
- 모든 공개 유틸리티 메서드에 XML doc 주석 필수
- 수정 코드에 `//YYMMDD hbk` 주석 필수 (feedback_comment_convention)
- 신규 파일명: `FAIEdgeMeasurementService.cs` → `<Domain>Service.cs` 규칙 적용
- 결과 모델명: `FAIEdgeMeasurementResult` → PascalCase 명사
- Brace style: Halcon/ 폴더는 Allman 스타일 유지

---

## Standard Stack

### Core (모두 이미 설치됨)
| 라이브러리 | 버전 | 용도 | 비고 |
|-----------|------|------|------|
| halcondotnet | 24.11 | MeasurePos, GenMeasureRectangle2, CloseMeasure, DispText | `HALCON-24.11-Progress-Steady` 설치됨 [VERIFIED: codebase] |
| HalconDotNet namespace | — | HTuple, HObject, HImage, HOperatorSet | 전역 using 패턴 |
| System.Collections.Generic | .NET 4.8 | List<T>, Dictionary | |
| System.Linq | .NET 4.8 | Where, Select | |

### 신규 파일 (Phase 3에서 생성)
| 파일 | 위치 | 역할 |
|------|------|------|
| `FAIEdgeMeasurementResult.cs` | `WPF_Example/Halcon/Models/` | 단일 FAI 측정 결과 모델 (에지 위치, 픽셀거리, mm거리, IsPass, 오버레이) |
| `FAIEdgeMeasurementService.cs` | `WPF_Example/Halcon/Algorithms/` | FAIConfig + HImage → FAIEdgeMeasurementResult 변환 서비스 |

### 수정 파일 (Phase 3에서 변경)
| 파일 | 변경 내용 |
|------|-----------|
| `Action_FAIMeasurement.cs` | EStep.Measure 스텁 → FAIEdgeMeasurementService 호출 |
| `FAIMeasurementContext.cs` (Action_FAIMeasurement 내부) | `InspectionOverlays`, `DisplayMessages` 필드 추가 |
| `HalconDisplayService.cs` | OK/NG 텍스트 + 거리 값 DispText 메서드 추가 |
| `MainView.xaml.cs` | "Measure" 버튼 연결 (또는 기존 검사 결과 표시 파이프라인 재활용) |
| `FAIResultRow.cs` | Refresh() 호출 시 UI 갱신 보장 (이미 구현됨, 검증만) |

---

## Architecture Patterns

### 현재 파이프라인 흐름 (검증됨)

```
Action_FAIMeasurement.Run() [EStep.Measure 스텁]
    ↓
[Phase 3 교체]
FAIEdgeMeasurementService.TryMeasure(HImage, FAIConfig)
    → MeasurementAlgorithm.TryInspectSingleEdge (에지 라인 1)
    → MeasurementAlgorithm.TryInspectSingleEdge (에지 라인 2, 있으면)
    → 픽셀 거리 계산 → mm 변환 (PixelResolutionX/Y)
    → FAIConfig.SetResult(distanceMm)  → IsPass 결정
    → FAIEdgeMeasurementResult (라인 좌표 + overlays + displayMessages)
    ↓
FAIMeasurementContext.InspectionOverlays 저장
FAIMeasurementContext.ResultHalconImage 저장
    ↓
MainView.DisplaySequenceContext / DisplayParam
    → halconViewer.UpdateDisplayState(rois, overlays, messages)
    → HalconDisplayService.Render (에지 라인, 포인트, 텍스트)
    ↓
FAIResultRow.Refresh() → DataGrid 갱신
```

### Pattern 1: EEdgeMeasureType 에 따른 에지 쌍 선택
```csharp
// Source: FAIConfig.cs - MeasureType 필드 정의
// FirstToFirst: edge[0]↔edge[0] (단일 ROI의 first 에지 두 번 검출)
// FirstToLast: edge[0]↔edge[last] (ROI 내 양 끝 에지)
// 현재 MeasurementAlgorithm은 EdgeSelection ("first"/"last"/"all") 로 제어
// Phase 3에서 두 번 TryInspectSingleEdge 호출 OR EdgeSelection="all"로 양 에지 추출
```

**설계 결정 필요:** MeasureType이 FirstToFirst/FirstToLast 등을 정의하지만 현재 FAIConfig.ToRoiDefinition()은 EdgeSelection을 "First"로 하드코딩한다. Phase 3에서 MeasureType을 RoiDefinition.EdgeSelection에 매핑해야 한다.

매핑 규칙 [ASSUMED]:
- `FirstToFirst` → 동일 ROI에서 EdgeSelection="all"로 두 에지 추출 후 [0]과 [0] 사용 (단일 선)
- `FirstToLast` → EdgeSelection="all"로 [0]과 [last] 선택
- `LastToFirst` / `LastToLast` → 동일 패턴, 인덱스만 변경

더 단순한 구현: `EdgeSelection="all"`로 MeasurePos 호출 후 MeasureType에 따라 HTuple 인덱스로 에지 위치 추출.

### Pattern 2: 픽셀 → mm 거리 변환
```csharp
// Source: FAIConfig.cs, RoiDefinition.cs - PixelResolutionX/Y 필드 (Phase 2에서 추가됨)
// 에지 페어 픽셀 거리 계산
double pixelDist = Math.Sqrt(
    Math.Pow((row2 - row1), 2) + Math.Pow((col2 - col1) * (PixelResolutionX / PixelResolutionY), 2));
// mm 변환
double mmDist = pixelDist * PixelResolutionX;

// 에지 방향이 수평(LtoR/RtoL)이면 열 방향 거리만:
double mmDist = Math.Abs(col2 - col1) * PixelResolutionX;
// 에지 방향이 수직(TtoB/BtoT)이면 행 방향 거리만:
double mmDist = Math.Abs(row2 - row1) * PixelResolutionY;
```

### Pattern 3: FAIConfig.SetResult() 호출 패턴 (이미 구현됨)
```csharp
// Source: FAIConfig.cs L64-69 [VERIFIED: codebase]
public void SetResult(double measuredValue) {
    MeasuredValue = measuredValue;
    double lower = NominalValue - Math.Abs(LowerTolerance);
    double upper = NominalValue + Math.Abs(UpperTolerance);
    IsPass = (measuredValue >= lower) && (measuredValue <= upper);
}
```

### Pattern 4: 오버레이 렌더링 파이프라인 (검증됨)
```csharp
// Source: MainView.xaml.cs L365-388, MainResultViewerControl.xaml.cs L158
// 기존 overlays 파이프라인:
halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, messages);
// → HalconDisplayService.Render(window, image, rois, selectedRoiId, draftRoi, overlays, messages)
// → DispLine (에지 라인) + DispText (측정값 텍스트)

// 측정 결과 텍스트 표시 방법:
// Source: HalconDisplayService.cs L157-160 [VERIFIED: codebase]
window.DispText(message, "window", 12 + (line * 28), 12, "yellow",
    MessageTextParamNames, MessageTextParamValues);
```

### Pattern 5: Action_FAIMeasurement 스텁 교체 구조
```csharp
// Source: Action_FAIMeasurement.cs L69-82 [VERIFIED: codebase]
case EStep.Measure:
    // Phase 8(이전 플레이스홀더): "Phase 8: Halcon edge measurement will be implemented here"
    // Phase 3에서 아래로 교체:
    if (ShotParam != null)
    {
        var service = new FAIEdgeMeasurementService();
        bool allPass = true;
        var overlays = new List<EdgeInspectionOverlay>();
        var messages = new List<string>();
        using (var image = ShotParam.GetImage())
        {
            foreach (var fai in ShotParam.FAIList)
            {
                FAIEdgeMeasurementResult r;
                if (service.TryMeasure(image, fai, out r))
                {
                    fai.SetResult(r.DistanceMm);
                    overlays.AddRange(r.Overlays);
                    messages.Add(string.Format("{0}: {1:F3}mm {2}",
                        fai.FAIName, r.DistanceMm, fai.IsPass ? "OK" : "NG"));
                }
                else
                {
                    fai.ClearResult();
                    messages.Add(string.Format("{0}: FAIL", fai.FAIName));
                }
                if (!fai.IsPass) allPass = false;
            }
        }
        pMyContext.AllPass = allPass;
        pMyContext.MeasuredCount = ShotParam.FAIList.Count;
        pMyContext.InspectionOverlays = overlays;  // 신규 필드
        pMyContext.DisplayMessages = messages;      // 신규 필드
    }
    Step = (int)EStep.End;
    break;
```

### Anti-Patterns 금지 사항
- **MeasurePos handle 누수:** `GenMeasureRectangle2`로 만든 `handle`은 반드시 `CloseMeasure(handle)` 호출 (현재 MeasurementAlgorithm.cs에서 올바르게 처리 중)
- **HImage 미해제:** `ShotParam.GetImage()`는 CopyImage() 반환이므로 반드시 `using` 블록
- **직접 FAIConfig.MeasuredValue 수정:** `SetResult()` 메서드만 사용 (직접 프로퍼티 쓰기 금지)
- **UI 스레드에서 Halcon 연산:** `Action_FAIMeasurement.Run()`은 시퀀스 스레드에서 실행됨 — 측정은 여기서 수행. 결과 표시는 `ExecuteOnUi()` via `DisplayParam()`

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 에지 감지 | 커스텀 gradient 연산 | `MeasurementAlgorithm.TryInspectSingleEdge` | 이미 MeasurePos + FitLineContourXld 구현됨 |
| OK/NG 판정 | 직접 비교 로직 | `FAIConfig.SetResult(double)` | tolerance 계산이 구현됨 (NominalValue ± Abs) |
| 캔버스 라인 렌더링 | WPF Shape 직접 그리기 | `HalconDisplayService.Render` + `EdgeInspectionOverlay` | Halcon HWindow 기반 렌더러 이미 존재 |
| 텍스트 오버레이 | Canvas.TextBlock | `HWindow.DispText` | Halcon 윈도우와 동일 좌표계 사용 필수 |
| INI 저장 | 수동 파싱 | `ParamBase.Save/Load` | FAIConfig → ParamBase 상속, 자동 직렬화 |
| 픽셀→mm 변환 | 별도 캘리브레이션 클래스 | `FAIConfig.PixelResolutionX/Y` | Phase 2에서 이미 ShotConfig.PixelResolution → FAI 전파됨 |

---

## Current State Analysis

### MeasurementAlgorithm.cs 현황 [VERIFIED: codebase]

**이미 구현된 것:**
- `TryInspectSingleEdge(HImage, RoiDefinition, out EdgeInspectionOverlay)` — 단일 에지 라인 검출
- `GenMeasureRectangle2` → `MeasurePos` → `FitLineContourXld` 파이프라인
- EdgePolarity ("positive"/"negative"), EdgeSelection ("first"/"last"/"all"), Sigma, EdgeThreshold 지원
- TrimExtremePoints 로 아웃라이어 제거

**Phase 3에서 필요한 것 (신규):**
- `FAIEdgeMeasurementService.TryMeasure(HImage, FAIConfig, out FAIEdgeMeasurementResult)` 신규 클래스
  - FAIConfig → RoiDefinition 변환 (`ToRoiDefinition()` 호출)
  - MeasureType에 따라 EdgeSelection 설정
  - 두 에지 위치(row, col) 추출
  - 픽셀 거리 → mm 변환
  - 오버레이 생성 (에지 라인 2개 + 연결선 + 텍스트)

### FAIConfig 현황 [VERIFIED: codebase]

```
ROI: ROI_Row, ROI_Col, ROI_Phi, ROI_Length1, ROI_Length2 (center+half-extents)
     PolygonPoints (string, Phase 2 추가)
Edge: MeasureType, Threshold, Sigma
Calibration: PixelResolutionX, PixelResolutionY (Phase 2 추가)
Tolerance: NominalValue, UpperTolerance, LowerTolerance
Result: MeasuredValue (runtime), IsPass (runtime)
```

**갭 확인:** `ToRoiDefinition()`은 `EdgeSelection="LtoR"`으로 하드코딩 → FAIEdgeMeasurementService에서 MeasureType 기반으로 오버라이드 필요.

### FAIMeasurementContext 현황 [VERIFIED: codebase]

```csharp
public class FAIMeasurementContext : ActionContext {
    public bool AllPass { get; set; }
    public int MeasuredCount { get; set; }
}
```

`ActionContext` 기반 클래스에 `ResultHalconImage`, `InspectionOverlays`, `ResultImagePath` 필드가 이미 있는지 확인 필요.

### ActionContext 베이스 클래스 확인 필요
`ActionContext.InspectionOverlays`가 이미 베이스에 있으면 `FAIMeasurementContext`에 별도 추가 불필요. **확인 후 결정** [ASSUMED].

### HalconDisplayService.Render 현황 [VERIFIED: codebase]

- `DispLine` — 라인/포인트 (에지 위치 표시)
- `DispText` — 텍스트 메시지 (왼쪽 상단, window 좌표계)
- `inspectionOverlays`에서 RoiId 기반 색상 분기 (cyan/lime green/orange 등 하드코딩됨)

**Phase 3 갭:** OK/NG 결과에 따른 색상 (OK=green, NG=red) 분기가 없음. 새로운 RoiId 패턴 추가 또는 `displayMessages` 기반 텍스트로 대체.

---

## Common Pitfalls

### Pitfall 1: MeasurePos 에지 없음 (0개 결과)
**What goes wrong:** `MeasurePos`가 에지를 검출하지 못하면 `rows.TupleLength() == 0`. 이미 `TryInspectSingleEdge`에서 `allRows.TupleLength() <= 1` 이면 false 반환으로 처리됨.
**Why it happens:** ROI 내 에지 contrast가 Threshold 미만이거나 ROI가 너무 작음.
**How to avoid:** FAIEdgeMeasurementService에서 TryInspect 실패 시 fai.ClearResult() + "FAIL" 메시지. 절대 NominalValue로 stub 처리하지 말 것.
**Warning signs:** MeasuredValue == 0.0 (ClearResult 패턴)

### Pitfall 2: 단일 에지 vs 에지 페어 거리
**What goes wrong:** MeasurementAlgorithm의 현재 구현은 단일 에지 "라인"을 반환한다. 거리 측정은 "두 에지 위치 사이 거리"가 필요하다.
**Why it happens:** 기존 `RoiLineIntersectionAlgorithm`은 cross-hair 교점 계산용이므로 패러다임이 다름.
**How to avoid:** FAIEdgeMeasurementService는 EdgeSelection="all"로 MeasurePos를 한 번 호출하고 MeasureType에 따라 [0]번과 [-1]번 에지 위치를 선택. 또는 두 개의 별도 RoiDefinition을 만들어 각각 "first"/"last"로 호출.
**권장 구현:** `MeasurementAlgorithm.TryInspectSingleEdge` 내부 로직을 재사용하지 않고, `FAIEdgeMeasurementService`에서 `HOperatorSet.MeasurePos`를 직접 호출하되 `EdgeSelection="all"`으로 모든 에지를 받아 MeasureType으로 선택.

### Pitfall 3: HImage 스레드 안전성
**What goes wrong:** `ShotParam.GetImage()`가 CopyImage()로 복사본을 반환하므로 락은 내부에서 처리되나, 반환된 HImage를 using 없이 사용하면 GC에 의해 임의 시점 해제.
**How to avoid:**
```csharp
using (var image = ShotParam.GetImage())
{
    if (image == null) { /* 처리 */ }
    // image 사용
} // 여기서 반드시 Dispose
```

### Pitfall 4: DispText 좌표계
**What goes wrong:** `HWindow.DispText`의 좌표는 "window"/"image"/"pixel" 중 하나. "window" 모드는 픽셀 단위 화면 좌표. 이미지 확대/축소에 독립적이므로 상태 텍스트에 적합.
**How to avoid:** 현재 HalconDisplayService 패턴(`"window"` 좌표, y=12+(line*28)) 유지. 에지 위치 텍스트는 "image" 좌표로 이미지 픽셀 위치에 붙이는 것이 더 직관적이나 패닝/줌 시 위치가 고정됨 → "window" 고정 텍스트 권장.

### Pitfall 5: FAIResultRow UI 갱신
**What goes wrong:** FAIConfig.SetResult()를 호출해도 DataGrid는 자동 갱신되지 않는다. FAIResultRow.Refresh()를 명시적으로 호출해야 한다.
**How to avoid:** 측정 완료 후 UI 스레드에서 각 FAIResultRow의 Refresh() 호출. InspectionViewModel.OnActionSelected() 재호출로 rows를 새로 만드는 것도 가능.
**Warning signs:** 측정은 완료되었으나 DataGrid에 "—" 값이 계속 표시됨.

### Pitfall 6: 음수 Tolerance 처리
**What goes wrong:** FAIConfig.LowerTolerance를 음수로 입력하면 `NominalValue - Math.Abs(LowerTolerance)` 계산이 의도대로 동작하지 않을 수 있다. SetResult()는 `Math.Abs(LowerTolerance)`를 사용하므로 실제로는 음수 입력도 허용된다.
**현재 구현:** `SetResult`가 `Math.Abs(Lower/UpperTolerance)`를 사용 — 안전. 그러나 UI PropertyGrid에서 음수/양수 혼용 시 사용자 혼란 가능.

---

## Integration Points

### 1. Action_FAIMeasurement.Run() 수정 위치

```
파일: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
위치: EStep.Measure 케이스 (L70-79)
현재: // Phase 8: Halcon edge measurement will be implemented here
교체: FAIEdgeMeasurementService 호출 (위 Pattern 5 참조)
```

### 2. UI 결과 표시 경로

Phase 1에서 구축된 경로:
```
MainView.DisplayParam(SequenceContext, ParamBase)
    → DisplayContextToViewer(context, rois)
    → halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, null)
```

`FAIMeasurementContext`가 `InspectionOverlays`를 채우면 기존 파이프라인이 자동으로 렌더링.

**확인 필요:** `FAIMeasurementContext`가 `ActionContext`를 상속하고, `ActionContext`에 `InspectionOverlays` 프로퍼티가 있는지. 있으면 추가 작업 불필요. [ASSUMED — ActionContext를 직접 읽지 않아 확인 필요]

### 3. "Measure" 버튼 트리거

현재 `Action_FAIMeasurement`는 시퀀스에서 자동 실행된다. Phase 3에서 수동 테스트용 "측정" 버튼이 필요한지는 CONTEXT.md에 명시되지 않음. Phase 4에서 시퀀스 자동화를 다루므로, Phase 3는 시뮬레이션 모드(SIMUL_MODE)에서 ShotConfig.SimulImagePath 이미지를 사용해 검증 가능.

---

## Code Examples

### FAIEdgeMeasurementResult 모델 구조
```csharp
// Source: 기존 FAIEdgeMeasurementResult 없음 — 신규 생성 필요
// 위치: WPF_Example/Halcon/Models/FAIEdgeMeasurementResult.cs
namespace ReringProject.Halcon.Models
{
    public class FAIEdgeMeasurementResult
    {
        /// <summary>에지 1 위치 (이미지 픽셀 좌표)</summary>
        public double Edge1Row { get; set; }
        public double Edge1Column { get; set; }

        /// <summary>에지 2 위치 (이미지 픽셀 좌표)</summary>
        public double Edge2Row { get; set; }
        public double Edge2Column { get; set; }

        /// <summary>픽셀 거리</summary>
        public double DistancePixel { get; set; }

        /// <summary>mm 거리 (PixelResolution 적용)</summary>
        public double DistanceMm { get; set; }

        /// <summary>캔버스 오버레이 (에지 라인 2개 + 연결선)</summary>
        public List<EdgeInspectionOverlay> Overlays { get; set; } = new List<EdgeInspectionOverlay>();
    }
}
```

### FAIEdgeMeasurementService 핵심 구조
```csharp
// Source: MeasurementAlgorithm.cs 패턴 참조 [VERIFIED: codebase]
// 위치: WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
namespace ReringProject.Halcon.Algorithms
{
    public class FAIEdgeMeasurementService
    {
        /// <summary>
        /// FAIConfig ROI 내에서 MeasurePos로 두 에지를 검출하고 거리(mm)를 계산한다.
        /// </summary>
        public bool TryMeasure(HImage image, FAIConfig fai, out FAIEdgeMeasurementResult result)
        {
            result = null;
            if (image == null || fai == null)
                return false;

            try
            {
                return TryMeasureInternal(image, fai, out result);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryMeasureInternal(HImage image, FAIConfig fai, out FAIEdgeMeasurementResult result)
        {
            result = null;
            // ToRoiDefinition()으로 ROI 파라미터 획득
            var roi = fai.ToRoiDefinition();
            if (!roi.IsTaught)
                return false;

            // ... MeasurePos 호출, MeasureType 기반 에지 선택, 거리 계산
        }
    }
}
```

### HalconDisplayService 오버레이 렌더링 확장
```csharp
// Source: HalconDisplayService.cs L75-143 패턴 [VERIFIED: codebase]
// RoiId 컨벤션 추가:
// "FAI-EdgeLine1-OK"  → green
// "FAI-EdgeLine1-NG"  → red
// "FAI-EdgeLine2-OK"  → green
// "FAI-EdgeLine2-NG"  → red
// "FAI-DistLine"      → cyan (연결선)
// else: 기존 blue 폴백 유지
```

---

## MeasurePos 파라미터 레퍼런스

`HOperatorSet.MeasurePos` 시그니처 (Halcon 24.11) [VERIFIED: codebase 사용 패턴]:
```
MeasurePos(
    Image,           // 입력 이미지
    MeasureHandle,   // GenMeasureRectangle2로 생성
    Sigma,           // Gaussian smoothing (FAIConfig.Sigma, 최소 0.4)
    Threshold,       // Edge amplitude threshold (FAIConfig.Threshold)
    Transition,      // "positive"/"negative"/"all" (FAIConfig EdgePolarity 기반)
    Select,          // "first"/"last"/"all" ← MeasureType 기반 선택
    out Rows,        // 검출된 에지 y 좌표
    out Columns,     // 검출된 에지 x 좌표
    out Amplitude,   // 에지 강도
    out Distance)    // 에지 간 거리 (HTuple) ← 이 값을 직접 활용 가능!
```

**중요 발견:** `MeasurePos`의 `Distance` 출력은 연속된 에지 쌍 사이의 픽셀 거리를 이미 반환한다. `Select="all"`로 모든 에지를 추출하면 `Distance[0]`이 edge[0]~edge[1] 거리, `Distance[1]`이 edge[1]~edge[2] 거리다. MeasureType이 FirstToLast인 경우 별도 계산 없이 `sum(Distance)`를 사용할 수 있다 [CITED: Halcon MeasurePos documentation pattern from codebase usage].

`GenMeasureRectangle2` 파라미터:
```
Row, Col  — 사각형 중심 (FAIConfig.ROI_Row, ROI_Col)
Phi       — 회전각 (FAIConfig.ROI_Phi, 새 ROI는 0.0)
HalfHeight — FAIConfig.ROI_Length1
HalfWidth  — FAIConfig.ROI_Length2
ImageWidth, ImageHeight — HImage.GetImageSize()
Interpolation — "nearest_neighbor" (현재 패턴)
```

**설계 결론:** `FAIEdgeMeasurementService`는 `ToRoiDefinition()` 대신 FAIConfig의 ROI 파라미터를 직접 사용하여 `GenMeasureRectangle2`를 호출하는 것이 더 효율적. `ToRoiDefinition()`은 bounding box 변환이므로 center+phi 파라미터 정밀도 손실이 있음.

---

## State of the Art (코드베이스 기준)

| 구성 요소 | Phase 2 이전 상태 | Phase 3 목표 상태 |
|-----------|-------------------|-------------------|
| Action_FAIMeasurement.EStep.Measure | 스텁 (NominalValue 하드코딩) | FAIEdgeMeasurementService 실제 구현 |
| FAIEdgeMeasurementResult | 없음 | 신규 모델 (에지 위치 + mm 거리 + 오버레이) |
| FAIEdgeMeasurementService | 없음 | 신규 서비스 (MeasurePos 래퍼) |
| FAIMeasurementContext | AllPass, MeasuredCount만 있음 | InspectionOverlays, DisplayMessages 추가 |
| HalconDisplayService | OK/NG 색상 분기 없음 | FAI 결과용 색상 RoiId 패턴 추가 |
| FAIResultRow | Refresh() 있지만 호출 시점 없음 | 측정 완료 후 Refresh() 트리거 연결 |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | MeasureType "FirstToFirst"는 동일 ROI에서 첫 번째 에지 하나만 검출하므로 거리=0이 됨. 실제로는 두 개의 ROI를 사용하거나, FirstToFirst는 두 에지 중 첫 번째 쌍을 의미할 수 있음. | Architecture Patterns | 거리 계산 로직 전체 변경 필요 |
| A2 | FAIMeasurementContext (ActionContext 기반) 에 InspectionOverlays 프로퍼티가 이미 베이스 클래스에 있음 | Integration Points | 없으면 FAIMeasurementContext에 직접 추가해야 함 (낮은 위험) |
| A3 | FAIEdgeMeasurementService는 FAIConfig 하나에서 두 에지를 추출 (EdgeSelection="all"). 설계에 따라 두 개의 FAIConfig가 각각 one edge를 정의하는 구조일 수도 있음. | Architecture Patterns | Phase 3 설계 방향 결정 필요 |

---

## Open Questions

1. **MeasureType 의미 확정 필요**
   - What we know: `EEdgeMeasureType { FirstToFirst, FirstToLast, LastToFirst, LastToLast }` 정의됨
   - What's unclear: 하나의 FAI ROI에서 두 에지를 추출하는 것인지, 두 개의 FAI ROI 쌍인지
   - Recommendation: 하나의 ROI에서 EdgeSelection="all"로 모든 에지 추출 후 인덱스 선택이 단순하고 기존 코드와 일관적. **플래너가 이 구조로 확정하길 권장.**

2. **"Measure" 수동 버튼 필요 여부**
   - What we know: 시퀀스에서 자동 실행되나 Phase 3 검증을 위한 수동 트리거 없음
   - What's unclear: CONTEXT.md/ROADMAP에 명시 없음
   - Recommendation: Phase 3 Plan에 "수동 측정 버튼" Task 포함. SIMUL_MODE에서 SimulImagePath 기반으로 Action_FAIMeasurement.Run() 수동 트리거.

3. **ActionContext.InspectionOverlays 존재 여부**
   - What we know: MainView에서 `context.InspectionOverlays`를 참조함 (L272, L366)
   - What's unclear: FAIMeasurementContext의 부모 ActionContext에 이 필드가 실제로 선언되어 있는지 (ActionContext 파일을 직접 읽지 않음)
   - Recommendation: Plan 작성 전 `SequenceBase.cs` 또는 `ActionContext.cs` 확인.

---

## Environment Availability

Step 2.6: SKIPPED (외부 툴/서비스 의존성 없음 — 순수 코드 변경)

---

## Validation Architecture

> `workflow.nyquist_validation` 설정 없음 → enabled 처리. 단, 이 프로젝트에는 테스트 프레임워크가 없음.
> CLAUDE.md: "No test framework detected (Python mock scripts only in Test/)".

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음 (xUnit/NUnit/MSTest 없음) |
| Config file | 없음 |
| Quick run command | — (수동 실행만 가능) |
| Full suite command | — |

### Phase Requirements → Verification Map
| Req ID | Behavior | Verification Type | 방법 |
|--------|----------|-------------------|------|
| ALG-01 | FAI ROI에서 에지 페어 mm 거리 계산 | 수동 실행 | SimulImagePath 이미지 + 수동 측정 버튼 → MeasuredValue 확인 |
| ALG-02 | Tolerance 기준 OK/NG 판정 | 수동 실행 | FAIConfig.NominalValue=10, UpperTolerance=0.5 설정 후 측정 값 확인 |
| ALG-04 | 오버레이 표시 (에지 위치, 거리, 판정) | 수동 시각 확인 | 캔버스에 에지 라인 + 텍스트 오버레이 표시 여부 확인 |

### Wave 0 Gaps
- 자동화 테스트 프레임워크 없음 — 모든 검증은 수동 실행으로 대체
- 기존 Python mock 스크립트(Test/)는 TCP 프로토콜용이므로 에지 측정 단위 테스트와 무관

---

## Security Domain

> `security_enforcement` 설정 없음 → enabled. 그러나 이 Phase는 순수 로컬 이미지 처리 + 알고리즘 레이어이며 네트워크, 인증, 암호화, 사용자 입력 검증(공개 API 아님)이 없음.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | partial | FAIConfig 파라미터 범위 검사 (Sigma >= 0.4, Threshold >= 1) — 이미 MeasurementAlgorithm에서 Math.Max로 클램핑 |
| V6 Cryptography | no | — |

| Threat Pattern | STRIDE | Mitigation |
|----------------|--------|------------|
| 잘못된 FAIConfig 파라미터 (Sigma=0, ROI=0) | Tampering | `TryMeasure`에서 파라미터 검증 후 false 반환 |
| HImage null 역참조 | Tampering | null 체크 필수 (이미 기존 패턴) |

---

## Sources

### Primary (HIGH confidence — codebase 직접 검증)
- `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs` — MeasurePos 호출 패턴, EdgeSelection/Polarity 매핑
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — SetResult, MeasureType, PixelResolutionX/Y
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — GetImage(), thread-safe 패턴
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 스텁 위치, EStep 구조
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — DispLine, DispText, RoiId 색상 분기
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — UpdateDisplayState, ImageLeftClicked
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — DisplayContextToViewer 파이프라인
- `WPF_Example/UI/ViewModel/FAIResultRow.cs` — Refresh() 패턴
- `WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs` — 오버레이 데이터 모델

### Secondary (MEDIUM confidence)
- Halcon MeasurePos Distance 출력 파라미터 — 기존 코드에서 `dist` 변수로 참조됨 (현재 미사용 `amp`, `dist` 출력)

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — 모든 파일 직접 검증
- Architecture: HIGH — 파이프라인 전체 추적 완료
- Pitfalls: HIGH — 코드베이스에서 실제 패턴 확인
- MeasureType 의미: MEDIUM — enum 정의는 있으나 동작 명세 없음 (A1, A3 참조)

**Research date:** 2026-04-09
**Valid until:** 2026-05-09 (안정 스택)
