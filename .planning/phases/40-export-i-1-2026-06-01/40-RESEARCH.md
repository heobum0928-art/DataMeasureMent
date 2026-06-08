# Phase 40: 결과 분석 & Export I — 리뷰어 + 1회 검사 엑셀 - Research

**작성일:** 2026-06-01
**도메인:** WPF + ClosedXML + Newtonsoft.Json + HalconDisplayService overlay 재렌더
**신뢰도:** HIGH (코드베이스 직접 확인) / MEDIUM (ClosedXML 의존성 체인 일부 ASSUMED)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**결과 영속화 전략 (OUT-01 핵심 토대)**
- **D-01 (재현 방식 = 구조화 JSON + 재렌더):** 검사 시 cycle 단위 구조화 JSON 저장. JSON = 측정값(mm) + 판정(OK/NG) + nominal/tolerance + overlay 기하(EdgeInspectionOverlay 직렬화: Points, LineRow/Col 등) + 원본/결과 이미지 경로. 리뷰어는 JSON 역직렬화 후 HalconDisplayService 로 overlay 재렌더. Newtonsoft.Json 이미 사용 가능. OUT-01(재렌더)·OUT-02(xlsx)의 공통 토대.
- **D-02 (cycle 메타데이터 = 타임스탬프 + 모델/레시피명 + 종합판정):** 최소 세트 = 검사 일시 + 현재 레시피/모델명 + 종합 판정(OK/NG/검출실패).
- **D-03 (저장 위치/단위 = ResultSavePath/{YYYYMMDD}/ cycle 폴더):** 기존 SystemSetting.ResultSavePath(기본 ./Result) 활용. `./Result/{YYYYMMDD}/{HHmmss}_..../` 형태의 cycle 폴더에 JSON + (관련) 이미지. 1 검사 = 1 cycle = 전 Shot/FAI 포함.

**xlsx Export (OUT-02)**
- **D-04 (라이브러리 = ClosedXML, MIT):** 상용 산업 제품 MIT 라이선스. fluent API + 이미지/하이퍼링크 지원. NPOI/OpenXML SDK/EPPlus 5+ 는 기각.
- **D-05 (행 구조 = 1행 = 1측정):** 컬럼 = Shot / FAI / 측정명 / nominal / tol+ / tol- / 측정값 / 판정.
- **D-06 (메타 배치 = 시트 상단 헤더 블록):** 시트 상단에 모델명·검사일시·종합 OK/NG, 그 아래 측정 테이블.
- **D-07 (이미지 연결 = 하이퍼링크):** 셀에 결과 이미지 파일 경로 하이퍼링크.

**리뷰어 UI (OUT-01)**
- **D-08 (UI 위치 = 별도 창 Window):** 메뉴/버튼으로 여는 독립 리뷰어 Window.
- **D-09 (폴더 로드 UX = 날짜 폴더 → cycle 목록 → 선택):** Ookii.Dialogs.Wpf 폴더 다이얼로그로 날짜 폴더 선택 → cycle 목록(시각·종합판정) → cycle 선택 시 이미지 + overlay + 측정표 재현.
- **D-10 (xlsx 트리거 = 리뷰어 수동 [엑셀 export] 버튼):** 리뷰어에서 연 cycle 을 [엑셀 export] 버튼으로 xlsx 생성.

### Claude's Discretion
- overlay JSON 스키마 상세 (EdgeInspectionOverlay 어느 필드를 직렬화/복원하는지) — 재렌더 충실도 결정
- 검사 cycle 완료 시 JSON 저장 wiring 시점 (어느 Action/Sequence/SystemHandler 경로에서 cycle 결과를 모아 직렬화하는지)
- 리뷰어 재렌더 시 HalconDisplayService 재사용 방식 (라이브 경로와 동일 메서드 공유 여부)
- 에러/빈 결과/검출실패 cycle 의 리뷰어·xlsx 표현
- 결과 폴더 정리/보존 정책 — POC 범위에서는 필수 아님

### Deferred Ideas (OUT OF SCOPE)
- 50회 반복도 통계(mean/stddev/range/Cpk) — Phase 41 (OUT-03)
- 검출 알고리즘별 통계 분석표 — Phase 41 (OUT-04)
- 검사 후 자동 xlsx 생성 — Phase 40 은 리뷰어 수동 export 만(D-10)
- 셀 임베드 썸네일 이미지
- cycle 메타에 작업자/TestId/시퀀스명 추가
- 결과 폴더 보존/정리 정책
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| OUT-01 | 결과 이미지 리뷰어 — 날짜/원본 폴더 로드 → 결과 재현 | D-01 JSON 영속화 + HalconDisplayService.Render 재사용 + HalconViewerControl.LoadImage/SetInspectionOverlays 패턴 확인됨 |
| OUT-02 | 시퀀스 1회 검사 결과 → 엑셀 export | ClosedXML 0.105.0 .NET Standard 2.0 → net48 호환 확인, 의존성 체인 + packages.config 수동 등록 방법 확인됨 |
</phase_requirements>

---

## Summary

Phase 40 은 검사 결과 사후 출력 계층을 신설한다. **공통 토대**: 검사 cycle 완료 시 구조화 JSON 을 `./Result/{YYYYMMDD}/{HHmmss}_cycle/cycle.json` 에 저장하고, 리뷰어(별도 WPF Window)와 xlsx export 가 동일 JSON 을 소비한다. **코드베이스 조사 결과**: cycle 완료 경계는 `InspectionSequence.AddResponse()` (SequenceBase.Finish() 직전 호출) 가 자연스러운 wiring 지점임을 확인했다. `EdgeInspectionOverlay` 는 모든 필드(RoiId, Points[{Row,Col}], LineRow1/Col1/Row2/Col2)가 단순 CLR 타입이므로 `[JsonIgnore]` 없이 직렬화 가능하다. `HalconDisplayService.Render()` 는 `inspectionOverlays` 파라미터를 통해 overlay 리스트를 직접 받으며, 리뷰어가 `HalconViewerControl.LoadImage(path)` + `SetInspectionOverlays(deserializedOverlays)` 를 순서대로 호출하면 라이브 경로와 동일한 재현이 가능하다.

**ClosedXML 의존성**: 0.105.0 (2025-05-14, .NET Standard 2.0, MIT). packages.config(classic) 환경에서 `SixLabors.Fonts` 가 이전에 prerelease 였으나 **2.1.3(stable)** 이 현재 존재하므로 packages.config 에 직접 수동 추가 가능. `DocumentFormat.OpenXml` 3.x 계열을 함께 등록해야 한다.

**Primary recommendation:** `InspectionSequence.AddResponse()` 직전에 `CycleResultSerializer.Save(shot/fai/overlay 스냅샷)` 를 호출하는 신규 서비스를 만들고, 리뷰어 Window 에서 `HalconViewerControl` + `HalconDisplayService` 를 라이브 경로와 공유하는 구조로 구현한다.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| cycle 결과 JSON 직렬화/저장 | Service (CycleResultSerializer) | InspectionSequence (wiring 진입점) | Sequence 레이어가 직렬화 로직 소유하면 SequenceBase 공통 프레임워크 오염 — 별도 Service 분리 |
| 날짜/cycle 폴더 스캔 | ReviewerWindow (ViewModel) | SystemSetting (경로 참조) | 폴더 구조는 D-03 에 따라 ResultSavePath 기준 — ViewModel 이 탐색 |
| overlay 재렌더 | HalconDisplayService (기존 재사용) | HalconViewerControl | HalconDisplayService.Render() 가 이미 overlay 파라미터를 지원 — 추가 레이어 불필요 |
| xlsx 빌드 | ExcelExportService (신규) | ClosedXML | ClosedXML fluent API 를 직접 Window 코드-비하인드에 두면 테스트 불가 — 서비스 분리 |
| 리뷰어 UI / 측정 테이블 | ReviewerWindow (WPF Window) | — | D-08: 별도 Window. MainView 와 결합도 0. |
| 폴더 선택 다이얼로그 | ReviewerWindow (UI 진입) | Ookii.Dialogs.Wpf | 기존 DeviceSelector 패턴 그대로 재사용 |

---

## Standard Stack

### Core
| 라이브러리 | 버전 | 용도 | 선정 이유 |
|-----------|------|------|----------|
| Newtonsoft.Json | 13.0.3 (기존) | cycle JSON 직렬화/역직렬화 | 이미 packages.config 에 등록됨, CLR 타입 → JSON 변환 zero-config |
| ClosedXML | 0.105.0 (신규) | xlsx 빌드 | MIT, .NET Standard 2.0 (net48 호환), fluent API, 하이퍼링크 지원 [VERIFIED: nuget.org] |
| Ookii.Dialogs.Wpf | 5.0.1 (기존) | 날짜 폴더 선택 다이얼로그 | 이미 packages.config 에 등록됨, DeviceSelector 패턴 재사용 가능 |
| HalconDisplayService | 기존 | overlay 재렌더 | Render(window, image, rois, selectedRoiId, overlays) 시그니처가 이미 overlay 파라미터 지원 |

### Supporting (ClosedXML 전이 의존성 — packages.config 에 수동 추가 필요)
| 라이브러리 | 버전 | 용도 | 비고 |
|-----------|------|------|------|
| DocumentFormat.OpenXml | 3.1.1 이상 | xlsx Open XML 포맷 기반 | ClosedXML 이 내부적으로 사용. >=3.1.1 <4.0.0 [VERIFIED: nuget.org] |
| SixLabors.Fonts | 2.1.3 (stable) | 폰트 렌더링 | ClosedXML 의존성. 현재 stable 버전 존재 → prerelease 이슈 해소됨 [VERIFIED: nuget.org] |
| ClosedXML.Parser | 2.0.0 이상 | 수식 파서 | ClosedXML 내부 [ASSUMED — nuget.org 의존성 목록 기반] |
| ExcelNumberFormat | 1.1.0 이상 | 숫자 서식 | ClosedXML 내부 [ASSUMED] |
| Microsoft.Bcl.HashCode | 1.1.1 이상 | .NET Standard 2.0 hashcode backport | System.Buffers, System.Memory 는 이미 packages.config 에 있음 [ASSUMED] |
| RBush.Signed | 4.0.0 이상 | 공간 인덱스 | ClosedXML 내부 [ASSUMED] |

> **packages.config classic NuGet 주의:** PackageReference 방식과 달리 전이 의존성이 자동 복원되지 않는다. 위 전이 의존성을 packages.config 에 `<package id="..." version="..." targetFramework="net48" />` 형태로 수동 추가하고 csproj 에 `<Reference>` 를 명시해야 한다. `Install-Package ClosedXML -IncludePrerelease` 를 PM Console 에서 실행하면 일부 자동 처리되나, 전이 의존성 전부를 커버하지 못할 수 있으므로 수동 검증 필수.

**설치 방법 (PM Console):**
```
PM> Install-Package ClosedXML -Version 0.105.0
```
SixLabors.Fonts prerelease 이슈 발생 시 먼저 stable 을 명시:
```
PM> Install-Package SixLabors.Fonts -Version 2.1.3
PM> Install-Package ClosedXML -Version 0.105.0
```

### Alternatives Considered
| 대신 | 대안 | 트레이드오프 |
|------|------|------------|
| ClosedXML | NPOI 2.8.0 | NPOI 는 1.10 이후 상업적 사용 시 월정 라이선스 비용 발생 — 산업용 상용 제품에는 위험 |
| ClosedXML | OpenXML SDK (DocumentFormat.OpenXml 직접) | verbose API, 하이퍼링크/서식 구현 공수 3배 이상 |
| ClosedXML | EPPlus 5+ | EPPlus 5+ 는 GPL/상업 라이선스 이중 구조, 상용 제품 MIT 필요 |

---

## Architecture Patterns

### System Architecture Diagram

```
[검사 cycle 완료]
        │
        ▼
InspectionSequence.AddResponse()
  │  (기존: TCP 응답 큐에 패킷 enqueue)
  │
  ├──► [신규] CycleResultSerializer.Save()
  │           │
  │           ├── CycleResultDto 빌드
  │           │     Shot[] → FAI[] → Measurement[]
  │           │     overlay = List<EdgeInspectionOverlay> (직렬화 가능)
  │           │     meta = 타임스탬프 + 레시피명 + 종합판정
  │           │
  │           └── JSON 파일 저장
  │                 ResultSavePath/{YYYYMMDD}/{HHmmss}_cycle/
  │                   cycle.json  ← 구조화 결과
  │                   (이미지 파일은 기존 GetResultImageSavePath 경로 재사용)
  │
  ▼
[기존 경로 계속: ResponseQueue.Enqueue]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[사용자: 메뉴 버튼 클릭 → ReviewerWindow.Show()]
        │
        ▼
ReviewerWindow
  ├── [날짜 폴더 선택] Ookii VistaFolderBrowserDialog
  │         → ResultSavePath/{YYYYMMDD}/ 열기
  │         → 하위 cycle 폴더 스캔 → ListBox 에 (시각 + 종합판정) 표시
  │
  ├── [cycle 선택] → cycle.json 역직렬화
  │         CycleResultDto deserialized
  │
  ├── [이미지 표시] HalconViewerControl.LoadImage(imagePath)
  │
  ├── [overlay 재렌더] HalconViewerControl.SetInspectionOverlays(overlays)
  │         → HalconDisplayService.Render() 동일 경로 사용
  │
  ├── [측정 테이블] DataGrid ← MeasurementResultRow 유사 DTO
  │
  └── [엑셀 export 버튼] → ExcelExportService.Export(cycleResult, savePath)
              │
              ▼
        ClosedXML IXLWorkbook
          시트 상단: 메타 블록(모델명·일시·종합판정)
          그 아래: Shot/FAI/측정명/nominal/tol+/tol-/값/판정 테이블
          이미지 컬럼: ws.Cell(r,c).Hyperlink = new XLHyperlink(imagePath)
          wb.SaveAs(xlsxPath)
```

### Recommended Project Structure
```
WPF_Example/
├── Custom/
│   ├── Sequence/Inspection/
│   │   └── CycleResultSerializer.cs    # cycle JSON 저장/로드 서비스 (신규)
│   └── Export/
│       └── ExcelExportService.cs       # ClosedXML xlsx 빌드 서비스 (신규)
├── UI/
│   ├── Reviewer/
│   │   ├── ReviewerWindow.xaml         # 결과 리뷰어 별도 Window (신규)
│   │   └── ReviewerWindow.xaml.cs
│   └── ViewModel/
│       └── CycleResultDto.cs           # cycle 결과 DTO (신규, JSON 직렬화 대상)
└── packages.config                      # ClosedXML + 전이 의존성 추가 필요
```

### Pattern 1: CycleResultDto — JSON 직렬화 DTO

```csharp
// Source: 코드베이스 분석 기반 설계
// FAIConfig.cs:97-114, MeasurementBase.cs:43-58, EdgeInspectionOverlay.cs:22-48 참고
public class CycleResultDto
{
    // D-02 메타
    public DateTime InspectionTime { get; set; }
    public string RecipeName { get; set; }
    public string OverallJudgement { get; set; } // "OK" / "NG" / "DETECT_FAIL"

    // D-03 이미지 경로 (절대 경로 or cycle 폴더 상대 경로)
    public string CycleFolderPath { get; set; }

    // D-01/D-05 측정 데이터
    public List<ShotResultDto> Shots { get; set; } = new List<ShotResultDto>();
}

public class ShotResultDto
{
    public string ShotName { get; set; }
    public string OwnerSequenceName { get; set; }
    public string ResultImagePath { get; set; }  // 이미지 파일 경로
    public List<FaiResultDto> FAIs { get; set; } = new List<FaiResultDto>();
}

public class FaiResultDto
{
    public string FAIName { get; set; }
    public bool IsPass { get; set; }
    public bool WasDatumSkipped { get; set; }
    public List<MeasurementResultDto> Measurements { get; set; }
    // overlay — [JsonIgnore] 해제 후 직렬화 (모든 필드 CLR 타입)
    public List<EdgeInspectionOverlay> LastOverlays { get; set; }
}

public class MeasurementResultDto
{
    public string MeasurementName { get; set; }
    public string TypeName { get; set; }
    public double NominalValue { get; set; }
    public double TolerancePlus { get; set; }
    public double ToleranceMinus { get; set; }
    public double LastMeasuredValue { get; set; }
    public bool LastJudgement { get; set; }
    public bool LastHasResult { get; set; }
    public string LastSkipReason { get; set; }  // "DATUM_FAIL" or null
}
```

**핵심 포인트:** `EdgeInspectionOverlay` 의 모든 필드(RoiId: string, Points: List<EdgeInspectionPoint>, LineRow1/Col1/Row2/Col2: double)는 단순 CLR 타입이므로 `[JsonIgnore]` 없이 직렬화 가능하다. `FAIConfig.LastOverlays` 는 `[JsonIgnore]` 가 붙어 있지만 이는 INI ParamBase 직렬화 제외용 — JSON 직렬화 대상이 되는 별도 DTO 에서는 무관하다. [VERIFIED: EdgeInspectionOverlay.cs:22-48, FAIConfig.cs:110-114]

### Pattern 2: cycle 완료 wiring 지점

```csharp
// Source: InspectionSequence.cs:75-125, SequenceBase.cs:435-449 직접 확인
// 권장 wiring: InspectionSequence.AddResponse() 내부에서 TCP 패킷 빌드 직후 직렬화

protected override void AddResponse() {
    if (RequestPacket == null) return;

    // ... 기존 종합 판정 로직 (anyDatumSkip, allPass 계산) ...

    // [신규] cycle 완료 → JSON 저장
    // AddResponse() 는 Finish()/Error() 에서 호출 — 전 FAI 결과가 확정된 시점
    var cycleResult = CycleResultSerializer.BuildDto(
        recipeManager,
        responsePacket.Result,
        DateTime.Now,
        SystemHandler.Handle.Recipes.CurrentRecipeName);
    CycleResultSerializer.SaveAsync(cycleResult);

    ResponseQueue.Enqueue(responsePacket);
}
```

**cycle 완료 경계 확인:** `SequenceBase.Finish()` 가 `AddResponse()` → `SaveResultImage()` → `OnFinish?.Invoke()` 순으로 호출한다. `InspectionSequence` 는 `AddResponse()` 를 override 하여 종합판정을 계산한다. 전 Shot/FAI 결과가 `InspectionRecipeManager.Shots` 에 이미 채워진 시점이므로 이 위치가 자연스러운 직렬화 진입점이다. [VERIFIED: SequenceBase.cs:435-449, InspectionSequence.cs:75-125]

### Pattern 3: HalconDisplayService 재렌더 재사용

```csharp
// Source: MainView.xaml.cs:243-251, HalconViewerControl.xaml.cs:161-170 확인
// 리뷰어 Window 에서 동일 패턴 사용

// 리뷰어 Window 코드-비하인드
private void LoadCycleResult(CycleResultDto cycle, ShotResultDto shot, FaiResultDto fai)
{
    // 1. 이미지 로드 (HalconViewerControl.LoadImage 는 경로 기반 로드 지원)
    halconViewer.LoadImage(shot.ResultImagePath);

    // 2. overlay 재렌더 (REPLACE 의미 — SetInspectionOverlays 는 Clear+AddRange)
    halconViewer.SetInspectionOverlays(fai.LastOverlays);

    // 3. 측정 테이블 갱신
    measurementGrid.ItemsSource = fai.Measurements.Select(m => new ReviewMeasurementRow(m));
}
```

`HalconViewerControl.SetInspectionOverlays()` 는 내부적으로 `HalconDisplayService.Render()` 를 호출하며, `Render()` 의 `inspectionOverlays` 파라미터로 역직렬화된 overlay 를 그대로 전달할 수 있다. 리뷰어 Window 에 `HalconViewerControl` 인스턴스를 신규로 두거나, 라이브 경로의 HalconViewerControl 재사용 없이 독립 뷰어를 구성한다 (D-08 별도 Window 결정). [VERIFIED: HalconViewerControl.xaml.cs:161-170]

### Pattern 4: ClosedXML xlsx 빌드 (하이퍼링크 포함)

```csharp
// Source: github.com/ClosedXML/ClosedXML/wiki/Using-Hyperlinks 확인
using ClosedXML.Excel;

public void Export(CycleResultDto cycle, string outputPath)
{
    using (var wb = new XLWorkbook())
    {
        var ws = wb.Worksheets.Add("Result");

        // D-06 메타 헤더 블록 (행 1~3)
        ws.Cell(1, 1).Value = "모델명";
        ws.Cell(1, 2).Value = cycle.RecipeName;
        ws.Cell(2, 1).Value = "검사일시";
        ws.Cell(2, 2).Value = cycle.InspectionTime.ToString("yyyy-MM-dd HH:mm:ss");
        ws.Cell(3, 1).Value = "종합판정";
        ws.Cell(3, 2).Value = cycle.OverallJudgement;

        // D-05 측정 테이블 헤더 (행 5)
        int headerRow = 5;
        ws.Cell(headerRow, 1).Value = "Shot";
        ws.Cell(headerRow, 2).Value = "FAI";
        ws.Cell(headerRow, 3).Value = "측정명";
        ws.Cell(headerRow, 4).Value = "Nominal";
        ws.Cell(headerRow, 5).Value = "Tol+";
        ws.Cell(headerRow, 6).Value = "Tol-";
        ws.Cell(headerRow, 7).Value = "측정값";
        ws.Cell(headerRow, 8).Value = "판정";
        ws.Cell(headerRow, 9).Value = "이미지";

        int row = headerRow + 1;
        foreach (var shot in cycle.Shots)
        {
            foreach (var fai in shot.FAIs)
            {
                foreach (var meas in fai.Measurements)
                {
                    ws.Cell(row, 1).Value = shot.ShotName;
                    ws.Cell(row, 2).Value = fai.FAIName;
                    ws.Cell(row, 3).Value = meas.MeasurementName;
                    ws.Cell(row, 4).Value = meas.NominalValue;
                    ws.Cell(row, 5).Value = meas.TolerancePlus;
                    ws.Cell(row, 6).Value = meas.ToleranceMinus;
                    ws.Cell(row, 7).Value = meas.LastHasResult ? meas.LastMeasuredValue : double.NaN;
                    ws.Cell(row, 8).Value = meas.LastSkipReason == "DATUM_FAIL"
                        ? "DETECT FAIL"
                        : (meas.LastHasResult ? (meas.LastJudgement ? "OK" : "NG") : "-");

                    // D-07 하이퍼링크 (절대 경로)
                    if (!string.IsNullOrEmpty(shot.ResultImagePath))
                    {
                        ws.Cell(row, 9).Value = "이미지 열기";
                        ws.Cell(row, 9).Hyperlink = new XLHyperlink(shot.ResultImagePath);
                    }
                    row++;
                }
            }
        }

        wb.SaveAs(outputPath);
    }
}
```

### Pattern 5: Ookii 폴더 다이얼로그 (기존 패턴 재사용)

```csharp
// Source: DeviceSelector.xaml.cs:250-263 확인 — 동일 패턴 사용
var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
dlg.Multiselect = false;
dlg.SelectedPath = SystemHandler.Handle.Setting.ResultSavePath;
if (dlg.ShowDialog() == true)
{
    // 날짜 폴더 내 cycle 서브폴더 스캔
    LoadCycleFolders(dlg.SelectedPath);
}
```

### Anti-Patterns to Avoid
- **FAIConfig.LastOverlays 직접 직렬화:** FAIConfig 는 ParamBase 기반 INI 직렬화 대상. `[JsonIgnore]` 가 붙어 있으므로 FAIConfig 를 JSON 직렬화 대상으로 쓰면 overlay 가 제외됨. 반드시 별도 DTO(`FaiResultDto`) 로 복사해서 직렬화.
- **SequenceBase.SaveResultImage() 경로에 JSON wiring:** SaveResultImage 는 비동기 Task + SaveFailImage 가드 조건이 있어 Always 실행이 아님. JSON 직렬화는 AddResponse() 에서 별도로 수행해야 함.
- **결과 이미지를 xlsx 에 임베드:** D-07 에서 하이퍼링크 선택됨. 이미지 임베드는 파일 비대와 ClosedXML API 복잡도 상승.
- **ReviewerWindow 에서 라이브 MainView 의 halconViewer 재사용:** 라이브 경로가 해당 뷰어를 점유 중일 때 충돌 발생. ReviewerWindow 는 독립된 HalconViewerControl 인스턴스를 소유.

---

## Don't Hand-Roll

| 문제 | 직접 구현하지 말 것 | 대신 사용 | 이유 |
|------|---------------------|----------|------|
| xlsx 파일 빌드 | Open XML SDK 직접 조작 | ClosedXML | Open XML은 row/cell/sheet element 조작이 verbose, 하이퍼링크·서식 구현 공수 3배 |
| JSON 직렬화/역직렬화 | BinaryFormatter, 수동 INI | Newtonsoft.Json | 이미 참조됨, CLR 타입 → JSON 변환 zero-config |
| 폴더 선택 다이얼로그 | OpenFileDialog 폴더 트릭 | Ookii.Dialogs.Wpf | 이미 참조됨, Windows Vista 스타일 네이티브 폴더 다이얼로그 |

---

## Common Pitfalls

### Pitfall 1: packages.config 전이 의존성 미등록
**무엇이 잘못되는가:** ClosedXML 을 PM Console 로 설치해도 `SixLabors.Fonts`, `DocumentFormat.OpenXml`, `ClosedXML.Parser` 등이 packages.config 에 자동으로 추가되지 않아 런타임에 `FileNotFoundException` 발생.
**왜 발생하는가:** packages.config 포맷(classic NuGet)은 전이 의존성 자동 복원을 지원하지 않음. PackageReference 방식과 다름.
**예방법:** PM Console `Install-Package ClosedXML` 실행 후 실제로 복원된 packages 폴더를 확인하여 모든 전이 의존성이 packages.config 에 등록됐는지 검증. 빠진 항목 수동 추가.
**경고 신호:** 빌드 성공 후 앱 시작 시 `Could not load file or assembly 'SixLabors.Fonts'` 류 예외.

### Pitfall 2: App.config binding redirect 충돌
**무엇이 잘못되는가:** DocumentFormat.OpenXml 3.x 가 System.Memory, System.Buffers 에 대해 다른 버전을 요구할 경우 기존 binding redirect 와 충돌.
**예방법:** App.config 의 `<assemblyBinding>` 섹션에 DocumentFormat.OpenXml.Framework 및 관련 어셈블리 redirect 를 추가. 기존 `System.Memory 4.0.1.2`, `System.Runtime.CompilerServices.Unsafe 6.0.0` redirect 는 현재 존재 — 추가 충돌 없으면 그대로 유지.
**경고 신호:** `MixedMode assembly` 또는 `version conflict` 예외.

### Pitfall 3: overlay 직렬화 시 HTuple/HalconDotNet 타입 포함
**무엇이 잘못되는가:** overlay 관련 다른 클래스에 HTuple 필드가 있으면 JSON 직렬화 시 예외 발생.
**왜 발생하는가:** HalconDotNet 타입은 JSON serializable 하지 않음.
**예방법:** `EdgeInspectionOverlay` 는 모든 필드가 CLR 타입(`string`, `double`, `List<EdgeInspectionPoint>`)으로 구성됨 — 직렬화 안전. DTO 를 통해 HalconDotNet 타입과 격리 유지. [VERIFIED: EdgeInspectionOverlay.cs:22-48]

### Pitfall 4: JSON 저장 시 SeralizeObject exception → 검사 cycle 차단
**무엇이 잘못되는가:** `AddResponse()` 내부에서 직렬화 예외 발생 시 TCP 응답이 누락되고 sequence 가 중단됨.
**예방법:** `CycleResultSerializer.Save()` 를 `try { } catch { Logging.PrintErrLog(...) }` 로 감싸 exception isolation 적용. 기존 `SaveResultImage` 의 비동기+예외 격리 패턴(`SequenceBase.cs:382-415`) 그대로 따를 것.

### Pitfall 5: 결과 이미지 경로 절대/상대 혼용
**무엇이 잘못되는가:** cycle.json 의 이미지 경로를 절대 경로로 저장하면 ResultSavePath 변경 시 모두 깨짐. 상대 경로로 저장하면 다른 PC 에서 로드 실패.
**권장:** cycle 폴더 기준 상대 경로 or 절대 경로 정책을 하나로 고정. POC 범위에서는 절대 경로가 단순. 단, xlsx 하이퍼링크는 절대 경로(`file:///C:/...`) 로 저장해야 외부 Excel 에서 정상 동작.

### Pitfall 6: HalconViewerControl.LoadImage 비동기 + SetInspectionOverlays 순서
**무엇이 잘못되는가:** `LoadImage(path)` 가 동기적으로 HImage 를 로드하고 `Render()` 를 호출하는데, 이후 `SetInspectionOverlays()` 가 다시 `Render()` 를 호출 — 각각 정상이나 순서 뒤집으면 overlay 가 없는 상태로 렌더됨.
**예방법:** `LoadImage()` → `SetInspectionOverlays()` 순서 유지. `HalconViewerControl` 은 최신 호출의 내부 상태를 누적하므로 순서만 맞으면 정상. [VERIFIED: HalconViewerControl.xaml.cs:112-170]

---

## Code Examples

### cycle 결과 JSON 저장
```csharp
// CycleResultSerializer.cs — AddResponse() 진입점에서 호출
// Source: InspectionSequence.cs:75-125 분석 기반
public static void SaveAsync(CycleResultDto dto)
{
    Task.Factory.StartNew(() => {
        try {
            string dateDir = Path.Combine(
                SystemHandler.Handle.Setting.ResultSavePath,
                dto.InspectionTime.ToString("yyyyMMdd"));
            string cycleDir = Path.Combine(dateDir,
                dto.InspectionTime.ToString("HHmmss") + "_cycle");
            Directory.CreateDirectory(cycleDir);

            string jsonPath = Path.Combine(cycleDir, "cycle.json");
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(dto,
                Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) {
            try { Logging.PrintErrLog((int)ELogType.Error,
                "[CycleResultSerializer] Save failed: " + ex.Message); } catch { }
        }
    });
}
```

### cycle.json 역직렬화
```csharp
// ReviewerWindow.cs — cycle 선택 시 로드
public static CycleResultDto Load(string jsonPath)
{
    try {
        string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<CycleResultDto>(json);
    }
    catch {
        return null;
    }
}
```

### cycle 폴더 스캔 (날짜 폴더 → cycle 목록)
```csharp
// ReviewerWindow.cs — 날짜 폴더 선택 후
private void LoadCycleFolders(string dateFolderPath)
{
    var cycleDirs = Directory.GetDirectories(dateFolderPath)
        .Where(d => File.Exists(Path.Combine(d, "cycle.json")))
        .OrderByDescending(d => d)  // 최신 순
        .ToList();

    CycleList.ItemsSource = cycleDirs.Select(d => {
        var dto = CycleResultSerializer.Load(Path.Combine(d, "cycle.json"));
        return new CycleListItem {
            FolderPath = d,
            DisplayText = dto != null
                ? dto.InspectionTime.ToString("HH:mm:ss") + " " + dto.OverallJudgement
                : Path.GetFileName(d)
        };
    }).ToList();
}
```

---

## State of the Art

| 구버전 방식 | 현재 방식 | 변경 시점 | 영향 |
|------------|----------|----------|------|
| ClosedXML < 0.97 의 SixLabors.Fonts prerelease 의존성 | SixLabors.Fonts 2.1.3 stable → prerelease 이슈 해소 | 2024-2025 | packages.config 수동 추가 가능 |
| LastOverlays = [JsonIgnore] (Phase 39.1에서 추가됨) | FAIConfig.LastOverlays 는 런타임 전용 메모리 캐시 — DTO 복사 후 직렬화 | 2026-05-29 (Phase 39.1) | overlay 영속화는 DTO 레이어 필요 |

**Deprecated/outdated:**
- `GetResultImageSavePath()` 가 `ELogType.Image` 경로(ImageSavePath) 기반 — cycle JSON 저장에는 `ResultSavePath` (`ELogType.Result`) 경로 사용 권장. 기존 메서드는 이미지 저장용으로 그대로 유지.

---

## Assumptions Log

| # | 주장 | 섹션 | 오류 시 영향 |
|---|------|------|------------|
| A1 | ClosedXML.Parser, ExcelNumberFormat, RBush.Signed, Microsoft.Bcl.HashCode 가 전이 의존성으로 packages.config 수동 추가 필요 | Standard Stack | PM Console 로 설치하면 일부 자동 처리될 수도 있음 — 설치 후 실제 packages.config 검증 필요 |
| A2 | SixLabors.Fonts 2.1.3 이 .NET Standard 2.0 호환 (nuget.org 는 net8.0 타겟만 명시) | Standard Stack | .NET Framework 4.8 에서 런타임 로드 실패 가능 — 설치 테스트 필수 |
| A3 | DocumentFormat.OpenXml 3.1.1 ~ <4.0.0 이 기존 System.Memory/Buffers binding redirect 와 충돌하지 않음 | Common Pitfalls | App.config 에 추가 redirect 필요할 수 있음 |
| A4 | InspectionSequence.AddResponse() 호출 시점에 recipeManager.Shots 의 전 FAI.LastOverlays 가 이미 채워져 있음 | Architecture Patterns | Action_FAIMeasurement.EStep.Measure 가 AddResponse() 보다 먼저 완료됨 — SequenceBase 실행 순서로 보장되나 DualImage 등 비동기 경로 추가 확인 권장 |

---

## Open Questions

1. **DocumentFormat.OpenXml 3.x 버전 상한**
   - 알고 있는 것: ClosedXML 0.105.0 은 `>= 3.1.1 && < 4.0.0` 요구
   - 불명확한 것: 현재 nuget.org 최신은 3.4.1 — 이 범위 내에 있으므로 호환될 것으로 예상하나, packages.config 에 3.1.1 을 명시할지 3.4.1 을 명시할지
   - 권장: 3.1.1 (최소 요구) 또는 3.4.1 (최신 stable) 로 고정, 빌드 테스트로 확인

2. **SixLabors.Fonts 2.1.3 의 .NET Framework 4.8 런타임 호환성**
   - 알고 있는 것: nuget.org 페이지는 net8.0 타겟만 명시, "no dependencies"
   - 불명확한 것: .NET Standard 2.0 경로로 실제 로드되는지
   - 권장: Plan 의 Wave 0 에 패키지 설치 + 빌드 + 간단한 `new XLWorkbook()` smoke test 포함

3. **CurrentRecipeName 접근 경로**
   - 알고 있는 것: `SystemHandler.Handle.Recipes.GetVersion()` 은 MenuBar 에서 사용됨
   - 불명확한 것: 현재 로드된 레시피명을 얻는 공개 API (D-02 메타 = 레시피명)
   - 권장: `SystemHandler.Handle.Recipes` 의 공개 API 확인 후 plan 에서 명시

---

## Environment Availability

| 의존성 | 필요한 기능 | 사용 가능 | 버전 | 대안 |
|--------|------------|---------|------|------|
| Newtonsoft.Json | cycle JSON 직렬화 | ✓ | 13.0.3 (packages.config 확인) | — |
| Ookii.Dialogs.Wpf | 폴더 선택 다이얼로그 | ✓ | 5.0.1 (packages.config 확인) | — |
| ClosedXML | xlsx 빌드 | ✗ (신규 설치 필요) | 0.105.0 | — |
| DocumentFormat.OpenXml | xlsx 기반 | ✗ (신규 설치 필요) | 3.1.1~3.4.1 | — |
| SixLabors.Fonts | ClosedXML 의존 | ✗ (신규 설치 필요) | 2.1.3 | — |
| HalconDisplayService | overlay 재렌더 | ✓ | 기존 클래스 | — |
| HalconViewerControl | 이미지 표시 | ✓ | 기존 컨트롤 | — |

**신규 설치 필요 항목 (blocking):**
- ClosedXML 0.105.0 + 전이 의존성(DocumentFormat.OpenXml, SixLabors.Fonts 등) — Wave 0 에서 packages.config 추가 + 빌드 검증 필수

---

## Validation Architecture

### Test Framework
| 속성 | 값 |
|------|---|
| Framework | 없음 (Python mock scripts 만 존재, MSTest/xUnit 없음) |
| Config file | 없음 |
| Quick run command | SIMUL_MODE 빌드 + 앱 구동 |
| Full suite command | 수동 UAT |

### Phase Requirements → Test Map
| REQ ID | 동작 | 테스트 유형 | 자동화 명령 | 파일 존재 여부 |
|--------|------|------------|------------|--------------|
| OUT-01 | 날짜 폴더 로드 → cycle 목록 표시 | 수동 smoke | SIMUL_MODE 앱 구동 후 리뷰어 메뉴 클릭 | ❌ Wave 0 |
| OUT-01 | cycle 선택 → 이미지 + overlay + 측정표 재현 | 수동 시각 검증 | SIMUL_MODE + 기존 Cal_Image 사용 | ❌ Wave 0 |
| OUT-02 | 엑셀 export 버튼 → xlsx 파일 생성 | 수동 smoke | 생성된 파일 Microsoft Excel 에서 열기 | ❌ Wave 0 |
| OUT-02 | xlsx 내 이미지 하이퍼링크 정상 동작 | 수동 smoke | Excel 에서 링크 클릭 → 외부 뷰어 오픈 | ❌ Wave 0 |

### Wave 0 Gaps
- [ ] 기존 테스트 인프라 없음 — 수동 UAT 시나리오 문서화
- [ ] Wave 0: packages.config 에 ClosedXML + 전이 의존성 추가 → `new XLWorkbook()` smoke test 빌드 검증
- [ ] Wave 0: CycleResultDto 클래스 작성 + `JsonConvert.SerializeObject(dto)` / `DeserializeObject` round-trip 검증

---

## Security Domain

> `security_enforcement` 키 없음 → 활성화 기준 적용.

### Applicable ASVS Categories

| ASVS 카테고리 | 해당 여부 | 표준 제어 |
|--------------|---------|----------|
| V2 Authentication | 해당 없음 | 기존 LoginManager 변경 없음 |
| V3 Session Management | 해당 없음 | 세션 없음 |
| V4 Access Control | 부분 | 리뷰어 Window 는 기존 IsEditable 권한 체계 하에서 접근 |
| V5 Input Validation | 해당 | 폴더 경로 입력 → Path.Combine + Directory.Exists 검증 |
| V6 Cryptography | 해당 없음 | xlsx/JSON 은 암호화 불필요 (로컬 파일) |

### Known Threat Patterns

| 패턴 | STRIDE | 표준 완화책 |
|------|--------|------------|
| 악의적 경로 traversal (폴더 선택) | Tampering | Ookii 다이얼로그는 OS 네이티브 — 브라우저 레벨 필터. 경로 후처리 시 Path.GetFullPath() 로 정규화 |
| xlsx 파일 하이퍼링크 인젝션 | Tampering | 결과 이미지 경로는 서버 내부 생성 경로 — 외부 입력 아님. 이미지 경로를 외부에서 받을 경우 URI 스킴 whitelist(file:// 만 허용) |

---

## Sources

### Primary (HIGH confidence)
- `WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs:22-48` — 직렬화 대상 필드 타입 직접 확인
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:97-114` — LastOverlays [JsonIgnore] 확인
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:43-58` — LastMeasuredValue/LastJudgement/LastSkipReason 확인
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:70-125` — AddResponse() cycle 완료 경계 확인
- `WPF_Example/Sequence/Sequence/SequenceBase.cs:381-449` — SaveResultImage + Finish/Error 호출 순서 확인
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs:112-170` — LoadImage/SetInspectionOverlays 시그니처 확인
- `WPF_Example/Halcon/Display/HalconDisplayService.cs:19-282` — Render() 시그니처 + overlay 분기 확인
- `WPF_Example/UI/ContentItem/MainView.xaml.cs:243-262` — RenderStoredOverlaysForFai 패턴 확인
- `WPF_Example/UI/Device/DeviceSelector.xaml.cs:250-263` — Ookii VistaFolderBrowserDialog 기존 패턴 확인
- `WPF_Example/packages.config` — 현재 의존성 목록 확인
- `WPF_Example/App.config` — binding redirect 현황 확인
- `WPF_Example/Setting/SystemSetting.cs:55-176` — ResultSavePath, GetResultImageSavePath 경로 확인

### Secondary (MEDIUM confidence)
- [NuGet Gallery | ClosedXML 0.105.0](https://www.nuget.org/packages/closedxml/) — 최신 버전, .NET Standard 2.0 타겟, 의존성 목록
- [NuGet Gallery | DocumentFormat.OpenXml 3.4.1](https://www.nuget.org/packages/DocumentFormat.OpenXml/) — 3.4.1 latest stable, .NET Framework 4.8 compatible
- [NuGet Gallery | SixLabors.Fonts 2.1.3](https://www.nuget.org/packages/SixLabors.Fonts/) — stable 버전 존재 확인
- [ClosedXML Installation docs](https://docs.closedxml.io/en/latest/installation.html) — packages.config SixLabors.Fonts 이슈 + 권장 workaround
- [ClosedXML Wiki: Using Hyperlinks](https://github.com/ClosedXML/ClosedXML/wiki/Using-Hyperlinks) — `ws.Cell(r,c).Hyperlink = new XLHyperlink(path)` API 확인

### Tertiary (LOW confidence)
- ClosedXML 전이 의존성(ClosedXML.Parser, ExcelNumberFormat, RBush.Signed, Microsoft.Bcl.HashCode) 버전 — nuget.org 의존성 목록 기반 [ASSUMED — Plan Wave 0 설치 후 실제 검증 필요]

---

## Metadata

**신뢰도 분류:**
- 표준 스택: HIGH — 코드베이스 직접 확인 + NuGet 공식 페이지 검증
- 아키텍처: HIGH — 코드 흐름 직접 추적 (AddResponse → Finish 순서, HalconDisplayService 재사용 패턴)
- 함정: HIGH — 코드베이스 패턴 기반 (SaveResultImage exception isolation 패턴 등)
- ClosedXML 전이 의존성 체인: MEDIUM — 설치 smoke test 미수행, A1~A3 ASSUMED 항목 존재

**Research 날짜:** 2026-06-01
**유효 기간 예상:** 60일 (ClosedXML 등 stable 라이브러리, 코드베이스 변경 시 재검토)
