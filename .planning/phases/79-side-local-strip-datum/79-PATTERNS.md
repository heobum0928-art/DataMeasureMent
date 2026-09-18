# Phase 79: SIDE 핀 옆 띠 기준 옵션 - Pattern Map

**Mapped:** 2026-09-18
**Files analyzed:** 6 (전부 기존 파일 수정, 신규 .cs 파일 없음 — CLAUDE.md 제약)
**Analogs found:** 6 / 6 (전부 같은 파일 내부의 기존 패턴이 최선의 analog — "self-analog")

> 이 phase 는 신규 파일이 없다. 모든 "analog" 는 **수정 대상 파일 자신의 인접 코드**다 (같은 클래스 안의
> `LastFitScore`/`MeasCorrectionFactor`/`IsDatumOwnedByCurrentShot` 등 기존 필드·메서드가 새로 추가할
> 필드·메서드의 정확한 틀을 이미 보여준다). 아래 표의 "Closest Analog" 열은 모두 파일 경로만 다르고
> "같은 파일 내부 기존 패턴"인 경우가 대부분이다.

## File Classification

| File to Modify | Role | Data Flow | Closest Analog (동일 파일 내부 우선) | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | sequence-action (state machine step) | event-driven (Datum 검출 성공 이벤트 → 사전계산) | 같은 파일의 `IsDatumOwnedByCurrentShot`(순회 패턴) + `RunDatumSingleImageDetection`/`RunDatumDualImageDetection`(삽입 지점) | exact (같은 파일, 같은 책임 계층) |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` | measurement model + algorithm consumer | request-response (TryExecute 1회 호출 → 값 리턴) | 같은 파일의 기존 `TryExecute` axis 계산 블록(157-210행) + `EdgeThreshold`/`Sigma` 등 Edge 카테고리 필드 | exact |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` | abstract model base | CRUD (필드 리셋/보정 lifecycle) | 같은 파일의 `LastFitScore`/`LastSelectedZIndex`(Phase 77 런타임 필드 패턴) + `Load()` override(Phase 하위호환 패턴) | exact |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | UI code-behind (ROI 배선만, MVVM 예외 허용 지점) | request-response (마우스 드래그 → ROI 필드 갱신) | 같은 파일의 `BuildPointRoiDefinitions`/`ApplyPointRoiMoveDelta`/`TryGetPointRoiCenter`/`ApplyPointRoiResize` 4함수 중 `DualImageEdgeDistanceMeasurement`(Point/Line 2-ROI) 분기 | exact |
| `WPF_Example/UI/ViewModel/CycleResultDto.cs` (research 명명, 실제 존재 여부 Step6 확인 필요) | DTO / serialization | CRUD (JSON round-trip) | Phase 77-03 `SelectedZ`/`FormatSelectedZ` 필드 추가 선례 | role-match (파일 미확인, 아래 "No Analog" 참고) |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` (`TryFitLine`) | backend algorithm service | transform (HImage+ROI → 2점) | 변경 없음 — 호출부만 늘어남(시그니처 재사용) | exact (수정 없이 재사용) |

## Pattern Assignments

### `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (sequence-action, event-driven)

**신규 메서드:** `ComputeLocalRefLines(DatumConfig datum, InspectionSequence parentSeq, HImage imgHorizontal)` — private, `RunDatumSingleImageDetection`/`RunDatumDualImageDetection` 바로 아래에 추가.

**삽입 지점 1 — 1-image datum, 성공 분기 직후** (`RunDatumSingleImageDetection`, 478-505행, 현재 코드 확인됨):
```csharp
private void RunDatumSingleImageDetection(DatumConfig datum, InspectionSequence parentSeq, HImage img, ref int nDatumOk, ref int nDatumFail) {
    if (datum.IsPatternAlignEnabled) {
        string modelPath = InspectionSequence.ResolveDatumModelPath(datum, parentSeq.Name);
        string alignErr;
        if (!parentSeq.TryComposeAlign(datum, img, modelPath, out alignErr)) {
            ...
            nDatumFail++;
        } else {
            nDatumOk++;
            // 신규: ComputeLocalRefLines(datum, parentSeq, img);
        }
        ...
    } else {
        string derr;
        if (!parentSeq.TryRunSingleDatum(datum, img, null, out derr)) {
            ...
            nDatumFail++;
        } else {
            nDatumOk++;
            // 신규: ComputeLocalRefLines(datum, parentSeq, img);
        }
        ...
    }
}
```

**삽입 지점 2 — DualImage datum** (`RunDatumDualImageDetection`, 418-441행, imgH 만 전달):
```csharp
private bool RunDatumDualImageDetection(DatumConfig datum, InspectionSequence parentSeq, HImage imgH, HImage imgV) {
    if (datum.IsPatternAlignEnabled) {
        ...
        if (!parentSeq.TryComposeAlign(datum, imgH, imgV, modelPath, out alignErr)) { ...; return false; }
    } else {
        ...
        if (!parentSeq.TryRunSingleDatum(datum, imgH, imgV, out derr)) { ...; return false; }
    }
    // 신규: ComputeLocalRefLines(datum, parentSeq, imgH); // 가로축 이미지만
    return true;
}
```
**주의:** DualImage 쪽은 `return true;` 가 두 분기 뒤에 공통으로 한 줄만 있으므로 그 직전에 1곳만 추가하면 됨(1-image 는 분기마다 각각 추가해야 함 — 코드 구조 차이 그대로 반영).

**순회 패턴 재사용 — analog: 같은 파일 `IsDatumOwnedByCurrentShot` (347-361행, 검증됨)**
```csharp
private bool IsDatumOwnedByCurrentShot(DatumConfig datum) {
    if (ShotParam == null) return true;
    bool bSourceShotUnresolved = string.IsNullOrEmpty(datum.SourceShotName);
    if (bSourceShotUnresolved) return true;
    string shotName = ShotParam.ShotName ?? "";
    bool bSourceShotMatches = datum.SourceShotName == shotName;
    if (bSourceShotMatches) return true;
    foreach (var fai in ShotParam.FAIList) {
        if (fai == null) continue;
        foreach (var meas in fai.Measurements) {
            if (meas != null && meas.DatumRef == datum.DatumName) return true;
        }
    }
    return false;
}
```
→ `ComputeLocalRefLines` 는 이 이중 foreach(`ShotParam.FAIList` → `fai.Measurements`)를 그대로 복사해 `meas as EdgeToLineDistanceMeasurement` + `DatumRef==datum.DatumName` + `IsLocalRefEnabled` 필터만 추가한다. **새 순회 로직을 만들지 않는다.**

**Dispose/수명 — 변경 없음, 절대 손대지 않는 지점:**
```csharp
// ProcessDatumSingleImage, 471-473행 (검증됨)
} finally {
    img.Dispose();
}
// ProcessDatumDualImage, 407-410행 (검증됨)
} finally {
    SafeDisposeImage(imgH);
    SafeDisposeImage(imgV);
}
```
`ComputeLocalRefLines` 호출은 반드시 이 `finally` **이전**(위 삽입 지점 1·2)에서 끝나야 한다 — 이미지가 여전히 살아있는 유일한 구간.

**에러 로그 패턴 — analog: 같은 파일 곳곳의 `Logging.PrintLog((int)ELogType.Error, LOG_TAG + ...)`**
```csharp
Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' 검출 실패 (skip): " + derr);
```
→ `[LocalRef]` 접두 로그도 이 `LOG_TAG` + `(int)ELogType.Error` 캐스팅 관례를 그대로 따른다(연구 문서 예시 그대로).

---

### `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` (measurement, request-response)

**analog: 같은 파일 기존 필드 선언 스타일 (22-93행, 검증됨)** — `[Category("Point|ROI")]`/`[Category("Edge")]` 어노테이션 + `[Browsable(false)]`+`[JsonIgnore]` 런타임 필드 이중 패턴:
```csharp
[Category("Point|ROI")]
public double Point_Row { get; set; }
...
[Category("Edge")]
public int EdgeThreshold { get; set; } = 10;
public double Sigma { get; set; } = 1.0;
...
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumOriginRow { get; set; } // 런타임 주입 전용, INI/JSON 제외 안 됨(0 직렬화 허용)
```
→ 신규 `IsLocalRefEnabled`(`[Category("Local Ref")]`, bool, 기본 false), `LocalRef_Row/Col/Phi/Length1/Length2`(`[Category("Local Ref|ROI")]`, double), `LocalRefEdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection`(`[Category("Local Ref|Edge")]`)는 **이 카테고리 어노테이션 스타일을 그대로 복사**하고, `LastLocalRefFound`/`LastLocalRefRow1/Col1/Row2/Col2` 는 `MeasurementBase.LastFitScore`처럼 **public 필드**(프로퍼티 아님, INI/CopyTo 자동 제외 — 아래 MeasurementBase 절 참고)로 선언한다.

**axis 계산부 — 삽입 지점 (155행 `if (datumOriginInjected)` 진입 직전, 검증됨):**
```csharp
// 기존 코드 그대로 (151행)
bool datumOriginInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0);
double footRow = pRow;
double footCol = pCol;
bool footOk = false;
// 신규 1줄 분기 삽입 지점 — axisOriginRow/Col 지역변수를 여기서 만들어 아래 155행 블록의
// DatumOriginRow/DatumOriginCol 참조를 전부 이 지역변수로 치환(각도 DatumAngleRad 는 그대로 유지, O-79-02(b))
if (datumOriginInjected) // 정상 경로: projection_pl 로 datum 기준선까지 수직거리
{
    ...
}
```
**주의:** 155-294행 블록 내부에 `DatumOriginRow`/`DatumOriginCol` 참조가 다수(186, 191-209행) 있으므로, 새 지역변수(`axisOriginRow`/`axisOriginCol`)를 도입해 **일괄 치환**해야 한다(리서치 Pattern 2 그대로). 이 블록의 나머지 로직(sinT/cosT 부호 정규화, per-edge-point projection, `useAngle2` 분기)은 **완전히 무변경** — `DatumOriginRow`→`axisOriginRow` 텍스트 치환만.

**로그 analog: 같은 파일에는 없음 — Action_FAIMeasurement 의 `Logging.PrintLog((int)ELogType.Error, ...)` 패턴 재사용.**

---

### `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` (model base, CRUD lifecycle)

**analog: 같은 파일 Phase 77 런타임 필드 패턴 (118-128행, 검증됨)**
```csharp
// Phase 77 SZF-03: Z 범위 선택에 쓰인 에지 강도 점수(EdgeStrengthScore.Average)와 채택된 z 번호.
//  LastErrorMessage 와 같은 이유로 필드로 선언 — ParamBase.Save/Load 는 프로퍼티만 순회하므로
//  필드는 INI 레시피에 쓰이지 않고 CopyPublicPropertiesTo(붙여넣기)도 건드리지 않는다.
public double LastFitScore;
public int LastSelectedZIndex = SELECTED_Z_NONE;
```
→ 신규 `public string LastRefSource;` (값 "Local"/"GlobalFallback"/null) 를 이 옆에 같은 이유(필드=INI/CopyTo 자동 제외)로 추가.

**ClearResult() — analog: 같은 파일 187-197행, 검증됨**
```csharp
public void ClearResult()
{
    LastMeasuredValue = 0;
    LastJudgement = false;
    LastHasResult = false;
    LastSkipReason = null;
    LastErrorMessage = null;
    LastFitScore = 0.0; // Phase 77: 이전 사이클 선택 점수 잔재 방지
    LastSelectedZIndex = SELECTED_Z_NONE;
    LastZCandidateScores = null;
}
```
→ `LastRefSource = null;` 을 여기 추가 (Pitfall 2 방지 — 이 리셋이 DatumPhase 보다 먼저 호출됨을 실행 전 반드시 확인).

`EdgeToLineDistanceMeasurement` 쪽 `LastLocalRefFound`/`LastLocalRefRow1` 등은 `MeasurementBase` 가 아니라 그 측정 클래스 자신에만 있으므로, 이 파일이 아니라 `EdgeToLineDistanceMeasurement.TryExecute` 진입부(현재 115행 `LastFitScore = 0.0;` 옆)에서 리셋한다 — **`ClearResult()` 안이 아님**(타입이 다르므로).

**하위호환 Load() override — analog: 같은 파일 228-247행, 검증됨**
```csharp
public override bool Load(IniFile loadFile, string groupName)
{
    bool result = base.Load(loadFile, groupName);
    IniSection sec;
    bool bHasSection = loadFile.TryGetSection(groupName, out sec) && sec != null;
    if (!bHasSection || !sec.ContainsKey("MeasCorrectionFactor"))
    {
        MeasCorrectionFactor = 1.0;
    }
    ...
    return result;
}
```
→ `EdgeToLineDistanceMeasurement` 에 동일 패턴의 **자신만의** `override Load()`를 추가(RESEARCH.md Pattern 4 코드 그대로, 문자열 필드 `LocalRefEdgePolarity`/`LocalRefEdgeDirection` 포함 — `IniValue.ToString()` 무인자 오버로드가 키 없으면 `null` 리턴함을 `Ini.cs:317` 에서 확인했으므로 override 필수).

**CopyTo 제외 목록 — analog: 같은 파일 253-258행, 검증됨**
```csharp
private static readonly HashSet<string> _copyExclude = new HashSet<string> {
    "MeasurementName",
    "LastMeasuredValue", "LastJudgement", "LastHasResult", "LastSkipReason",
    "DatumOriginRow", "DatumOriginCol", "DatumAngleRad", "DatumAngle2Rad",
    "DatumDetectedCircleRow", "DatumDetectedCircleCol"
};
```
`LastLocalRefFound`/`LastLocalRefRow1` 등은 **필드**(프로퍼티 아님)이므로 `CopyPublicPropertiesTo`(프로퍼티만 순회)가 애초에 건드리지 않는다 — 이 HashSet 에 추가할 필요 없음(연구 문서와 동일 결론).

---

### `WPF_Example/UI/ContentItem/MainView.xaml.cs` (UI code-behind, ROI 배선만)

> **경로 정정:** RESEARCH.md 는 `MainView.xaml.cs`로만 표기했으나 실제 파일은 `WPF_Example/UI/ContentItem/MainView.xaml.cs` (루트 `MainWindow.xaml.cs` 아님). 4함수 실측 라인: `BuildPointRoiDefinitions`(428), `ApplyPointRoiMoveDelta`(876), `TryGetPointRoiCenter`(908), `ApplyPointRoiResize`(952) — RESEARCH.md 서술과 일치, 신규 확인.

**analog: 같은 파일, `EdgeToLineDistanceMeasurement` 자신의 기존 단일-ROI 분기 (892, 925행, 검증됨)**
```csharp
// ApplyPointRoiMoveDelta, 891-892행
var etld = meas as EdgeToLineDistanceMeasurement;
if (etld != null) { etld.Point_Row += deltaRow; etld.Point_Col += deltaCol; return; }

// TryGetPointRoiCenter, 924-925행
var etld = meas as EdgeToLineDistanceMeasurement;
if (etld != null) { row = etld.Point_Row; col = etld.Point_Col; return true; }
```
**LocalRef ROI 는 같은 측정 타입 안에 "2번째 ROI"로 추가되므로, 단일-ROI 분기가 아니라 `DualImageEdgeDistanceMeasurement`(Point+Line 2-ROI, subKey 구분) 패턴을 analog 로 써야 한다:**
```csharp
// BuildPointRoiDefinitions, 476-497행 — DualImage 분기(Point/Line 2개 독립 RoiDefinition)
var dual = m as DualImageEdgeDistanceMeasurement;
if (dual != null) {
    if (dual.PointROI_Length1 > 0 && dual.PointROI_Length2 > 0) {
        result.Add(new RoiDefinition {
            Id = faiName + "_" + measName + "_Point",
            Name = measName + "_Point",
            Row1 = dual.PointROI_Row - dual.PointROI_Length1, Column1 = dual.PointROI_Col - dual.PointROI_Length2,
            Row2 = dual.PointROI_Row + dual.PointROI_Length1, Column2 = dual.PointROI_Col + dual.PointROI_Length2,
            IsTaught = true
        });
    }
    if (dual.LineROI_Length1 > 0 && dual.LineROI_Length2 > 0) { /* 동일 패턴, subKey "_Line" */ }
    return result;
}
```
→ `EdgeToLineDistanceMeasurement` 는 지금 `if (etld != null)` 단일 분기(단일 ROI)이지만, LocalRef ROI 추가 후에는 **이 DualImage 분기와 같은 모양**으로 바꿔야 한다: `etld.Point_*`(subKey "_Point" 또는 기존 관례 유지) + `etld.LocalRef_*`(subKey "_LocalRef") 2개를 `IsTaught` 조건(`LocalRef_Length1>0 && LocalRef_Length2>0`)으로 각각 추가하고 `return result;` (4함수 모두 동일하게 `dual` 분기 형태로 확장). **`ArcLineIntersectDistanceMeasurement`/`DualImageEdgeDistanceMeasurement`처럼 조기 `return`으로 나머지 단일-ROI 분기를 건너뛰는 구조를 그대로 따른다.**

`ApplyPointRoiResize` (952행 이하)도 동일하게 `dual` 분기(1:1 subKey 매핑)를 analog 로 확장.

---

### 결과 표시·기록 (`RefSource` 표시/CSV) — 이전 리서치 버전 코드 재사용, 변경 없음

RESEARCH.md "Code Examples" 절이 CSV 헤더/파싱 확장, 오버레이 색상 분기(`FAI-RefLine`, 주황)를 이미 구체 코드로 갖고 있음(이전 버전에서 이월, 이번 세션 변경 없음). 이 phase 의 pattern-map 산출물 범위에서는 **CSV `COLUMN_COUNT`(14) 유지 + `COL_REF_SOURCE`(16) 조건부 읽기**만 재확인하면 되고, 새 analog 탐색은 불필요(연구 문서가 이미 확정).

## Shared Patterns

### 헝가리언 접두사 + 조건 연산자 금지 (CLAUDE.md 전역)
**적용 대상:** 이번 phase 의 모든 신규 지역변수/조건문 (`bTaught`, `bHasTransform`, `bDatumOriginInjected` 등)
```csharp
bool bTaught = etld.LocalRef_Length1 > 0 && etld.LocalRef_Length2 > 0;
if (!bTaught) { continue; }
```
`?:`/`??`/`?.` 전부 금지 — RESEARCH.md 코드 예시들도 이미 이 스타일(`if/else`, 명시적 null 체크)을 따르고 있음. `svc.TryFitLine` 호출 결과의 `err` 문자열 로그에 `??` 를 쓰지 않도록 `(err ?? "")` 같은 표현은 **명시적 null 체크로 치환**해야 함(RESEARCH.md 예시 코드 자체에 `(err ?? "")`가 남아 있어 계획 단계에서 반드시 고쳐야 할 위반 — planner 주의).

### HALCON 객체 Dispose 불필요 (이번 phase 특이사항)
`ComputeLocalRefLines`는 기존 `img`/`imgH` 를 재사용만 하고 신규 `HImage`/`HObject`/`HTuple` 을 만들지 않는다(`TryFitLine` 내부 `contour` 는 그 함수 자신이 Dispose). **새 Dispose 대상 없음** — 모든 신규 코드에 `finally { x.Dispose(); }` 추가할 필요 없음(오히려 불필요한 try/finally 로 가독성만 떨어뜨림).

### 옵션 꺼짐 회귀 0 — 가드는 항상 진입 최상단
`Action_FAIMeasurement.ComputeLocalRefLines` 의 `foreach` 루프, `EdgeToLineDistanceMeasurement.TryExecute` 의 axis 분기 모두 `if (!IsLocalRefEnabled) { ...(스킵)... }` 가드를 **분기 최상단**에 둔다(Pitfall 3) — `MeasurementBase.EvaluateJudgement` 의 `MeasCorrectionEnabled && AppliesCorrectionFactor && ...` 가드 순서(플래그 먼저, 그다음 세부조건)와 동일 관례.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `WPF_Example/UI/ViewModel/CycleResultDto.cs` | DTO | CRUD | 이번 세션에서 실제 경로/필드를 직접 열람하지 못함(Read 미실행) — RESEARCH.md 는 "Phase 77-03 `선택Z` 선례"를 analog 로 지목했으나, 정확한 파일 경로(`CycleResultDto.cs` vs 다른 DTO 파일명)와 `RefSource` 필드 삽입 지점은 planner/구현자가 `Grep "SelectedZ"` 로 재확인 필요 |
| `MeasurementHistoryCsvWriter`/`Loader.cs` | utility (CSV I/O) | batch | 마찬가지로 이번 세션에 직접 열람하지 않음 — RESEARCH.md Pitfall 4(`COLUMN_COUNT` 상향 금지, `COL_REF_SOURCE`=16 조건부 읽기)가 유일한 근거이며 실제 파일의 정확한 상수/라인은 미검증 |

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/Custom/Sequence/Inspection/Measurements/`, `WPF_Example/UI/ContentItem/`, `WPF_Example/Halcon/Algorithms/`
**Files scanned:** 4개 전체 열람(Action_FAIMeasurement.cs 330-510행 구간, EdgeToLineDistanceMeasurement.cs 전체 377행, MeasurementBase.cs 전체 274행, MainView.xaml.cs 428-497·876-965행 구간) + Grep 다수
**Pattern extraction date:** 2026-09-18
