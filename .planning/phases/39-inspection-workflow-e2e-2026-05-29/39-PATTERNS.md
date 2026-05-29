# Phase 39: 검사 워크플로우 E2E - Pattern Map

**Mapped:** 2026-05-29
**Files analyzed:** 9 (수정 8 + UAT 자산 1)
**Analogs found:** 9 / 9 (모두 in-repo)

## File Classification

| File | Role | Data Flow | Closest Analog (in-repo) | Match Quality |
|------|------|-----------|--------------------------|---------------|
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | action (Sequence step) | event-driven (step machine) | self §EStep.DatumPhase L80-121, §EStep.Measure L154-263 | exact |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | sequence (cycle aggregator) | request-response (TCP cycle) | self §AddResponse L67-97, §TryGetDatumTransform L218-225 | exact |
| `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` | data model (recipe + result) | CRUD + runtime result | self `MeasuredValue`/`IsPass` L97-101, `ClearResult` L126-129 | exact |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` | data model (abstract) | runtime result accumulator | self `LastHasResult` L51-52, `ClearResult` L90-95 | exact |
| `WPF_Example/TcpServer/VisionResponsePacket.cs` | network (wire protocol) | request-response | self `FAIResultData` L510-526, `TestResultPacket` 직렬화 L353-365 | exact |
| `WPF_Example/Custom/SystemHandler.cs` | system (router) | request-response | self `ProcessRecipeChange` L156-183 (참조용) | role-match |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | UI (overlay host) | event-driven UI update | self `RefreshFAIResults` L585-740 (refresh 패턴) | exact |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | UI (Halcon render) | streaming render | self `RenderDatumOverlay` L683-895, `DrawRoiLabelAt` L912-938 | exact |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | UI (tree + force-rebind) | event-driven UI update | self CO-22-01 force rebind L558-585 | exact |

## Pattern Assignments

### `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`

**역할:** Shot 단위 step machine. `EStep.DatumPhase` 에서 Datum 검출, `EStep.Measure` 에서 FAI/Measurement 순회 측정.

**현행 코드 영향점 (CONTEXT canonical_refs):**

[A] §EStep.DatumPhase per-datum loop (L82-117) — **D-01 게이팅 신호 기록 지점**
```csharp
// 현행 (Phase 37): datum 실패 시 continue 만 하고 _datumTransforms 미저장.
// → 이미 "lookup miss = 검출 실패" 시그널이 들어있음. 추가 작업은 (옵션 A) HashSet<string> 채우기만 추가.
foreach (var datum in parentSeq.DatumConfigs) {
    if (datum == null) continue;
    if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
        HImage imgH = null, imgV = null;
        try {
            if (!TryGrabOrLoadDualDatumImages(datum, out imgH, out imgV)) {
                Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' DualImage 취득 실패 (skip)");
                continue;  // ← D-01: 이 지점에서 parentSeq.MarkDatumFailed(datum.DatumName) 추가
            }
            string derr;
            if (!parentSeq.TryRunSingleDatum(datum, imgH, imgV, out derr)) {
                Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' 검출 실패 (skip): " + (derr ?? ""));
                // ← D-01: 여기에도 parentSeq.MarkDatumFailed(...) 추가
            }
        } finally {
            if (imgH != null) { try { imgH.Dispose(); } catch { } }
            if (imgV != null) { try { imgV.Dispose(); } catch { } }
        }
    } else {
        HImage img = GrabOrLoadDatumImage(datum);
        if (img == null) {
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' 이미지 취득 실패 (skip)");
            continue;  // ← D-01
        }
        try {
            string derr;
            if (!parentSeq.TryRunSingleDatum(datum, img, null, out derr)) {
                Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' 검출 실패 (skip): " + (derr ?? ""));
                // ← D-01
            }
        } finally {
            img.Dispose();
        }
    }
}
```
**필요 변경:** `continue` 직전 + datum 실패 분기에 `parentSeq.MarkDatumFailed(datum.DatumName)` 1줄씩 추가. lenient 정책(L119 `Step = (int)EStep.Grab`)은 그대로 유지 — abort 안 함.

[B] §EStep.Measure inner loop (L162-244) — **D-01 per-FAI gate 적용 지점**
```csharp
foreach (var fai in ShotParam.FAIList) {
    bool faiAllPass = true;
    foreach (var meas in fai.Measurements) {
        HTuple transform;
        if (parentSeq2 == null || !parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)) {
            //260413 hbk Fixture 미존재 또는 미지정 DatumRef → identity fallback
            try {
                HOperatorSet.HomMat2dIdentity(out transform);
            } catch {
                transform = new HTuple();
            }
        }
        // ↑ 현행: lookup miss 시 identity fallback (Phase 37 D-37-03 lenient).
        // ↓ D-01 변경 후 흐름:
        //   1. parentSeq2.IsDatumFailed(meas.DatumRef) 체크 (NEW)
        //   2. true → meas.LastSkipReason = "DATUM_FAIL" 기록 + meas.ClearResult() + 다음 meas 로 continue
        //   3. false (정상 또는 DatumRef 빈문자열) → 기존 identity fallback 경로 유지

        // ... consumer 주입 (L177-207)
        // ... TryExecute (L213) ... EvaluateJudgement (L221)
        if (!meas.LastJudgement) {
            faiAllPass = false;  // ← skip 한 meas 도 faiAllPass=false 누적 (skip = NG 강도 가장 셈)
        }
        measuredCount++;
    }
    // ... fai.IsPass/MeasuredValue 집계 (L247-252)
    if (!faiAllPass) allPass = false;
}
pMyContext.AllPass = allPass;
```

**try/catch lenient 패턴 (D-07 유지 — 회귀 가드)** (L211-219):
```csharp
bool ok = false;
try {
    ok = meas.TryExecute(image, transform, fai.PixelResolutionX, out resultValue, out measError, out measOverlays);
} catch (Exception ex) {
    ok = false;
    resultValue = 0;
    measError = ex.Message;
    measOverlays = null;
}
if (ok) {
    meas.EvaluateJudgement(resultValue);
} else {
    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + (meas.MeasurementName ?? meas.TypeName) + "' failed: " + (measError ?? ""));
    meas.ClearResult();
    meas.LastJudgement = false;
}
```
→ D-07: 이 블록은 일체 수정 금지. 측정 실패는 ClearResult + LastJudgement=false 로 NG 누적되어 외부 루프가 처리.

[C] 마커 컨벤션 (memory feedback_comment_convention):
- 모든 신규 라인 끝에 `//260529 hbk Phase 39 WF-01/WF-02 ...` 부착. 기존 마커는 보존 (Phase 20 D-12 marker stacking 규칙).

---

### `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`

**역할:** Inspection 시퀀스. `_datumTransforms` 캐시 소유자 + `AddResponse` 에서 TCP 결과 패킷 빌드.

**현행 §AddResponse (L67-97):**
```csharp
//260409 hbk Phase 5: 종합 판정 + FAI별 결과 TCP 전송 (D-07)
protected override void AddResponse() {
    if (RequestPacket == null) return;

    var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;

    // 종합 판정: 모든 FAI가 Pass여야 OK
    bool allPass = true;
    var responsePacket = new TestResultPacket {
        Target = RequestPacket.Sender,
        Site = RequestPacket.Site,
        InspectionType = RequestPacket.TestType,
        IsDynamicFAI = true,
    };

    foreach (var shot in recipeManager.Shots) {
        foreach (var fai in shot.FAIList) {
            if (!fai.IsPass) allPass = false;
            responsePacket.FAIResults.Add(new FAIResultData(
                fai.FAIName ?? "FAI",
                fai.IsPass,
                fai.MeasuredValue
            ));
        }
    }

    responsePacket.Result = allPass ? EVisionResultType.OK : EVisionResultType.NG;
    pMyContext.ResultInfo = responsePacket.Result;

    ResponseQueue.Enqueue(responsePacket);
}
```

**D-03/D-05/D-06 변경 패턴 (3-state cycle result):**
```csharp
// (newly) bool 1개 → 2 bool (detect-fail 우선 누적용)
bool anyDatumSkip = false;     //260529 hbk Phase 39 D-03 — datum-skip 1건이라도 있으면 cycle=NotExist
bool allPass = true;

foreach (var shot in recipeManager.Shots) {
    foreach (var fai in shot.FAIList) {
        // FAIResultData 시그니처에 ResultCode 추가 (D-06 — 옵션 1: 신규 ctor 오버로드)
        EVisionResultType faiResultCode;
        if (fai.WasDatumSkipped) {        //260529 hbk Phase 39 — 신규 플래그 (옵션)
            faiResultCode = EVisionResultType.NotExist;  // 'N'
            anyDatumSkip = true;
        } else if (!fai.IsPass) {
            faiResultCode = EVisionResultType.NG;        // 'F'
            allPass = false;
        } else {
            faiResultCode = EVisionResultType.OK;        // 'P'
        }
        responsePacket.FAIResults.Add(new FAIResultData(
            fai.FAIName ?? "FAI",
            faiResultCode,                                //260529 hbk Phase 39 D-06
            fai.MeasuredValue
        ));
    }
}

// D-03: 검출실패 > NG > OK 계층
responsePacket.Result = anyDatumSkip ? EVisionResultType.NotExist
                     : !allPass     ? EVisionResultType.NG
                                    : EVisionResultType.OK;
```

**현행 §TryGetDatumTransform (L218-225) — Phase 37 의 lookup miss 신호:**
```csharp
public bool TryGetDatumTransform(string datumRef, out HTuple transform) {
    if (string.IsNullOrEmpty(datumRef)) {
        HOperatorSet.HomMat2dIdentity(out transform);
        return true;
    }
    return _datumTransforms.TryGetValue(datumRef, out transform);
}
```
→ **이 코드는 변경 금지** (Phase 37 D-37-03 동작 그대로). D-01 게이트는 별도 새 메서드 `IsDatumFailed(datumRef)` (옵션 C) 또는 `_failedDatums.Contains(datumRef)` 직접 노출 (옵션 A) 로 구현.

**옵션 A 분석 (CONTEXT specifics §Per-FAI gate 후보):**
- `private readonly HashSet<string> _failedDatums = new HashSet<string>();` 신규 필드 추가 (L54 `_datumTransforms` 옆).
- `public void ClearDatumTransforms()` (L191-194) 본문에 `_failedDatums.Clear();` 1줄 추가.
- 신규 `public void MarkDatumFailed(string datumName) { if (!string.IsNullOrEmpty(datumName)) _failedDatums.Add(datumName); }`.
- 신규 `public bool IsDatumFailed(string datumRef) => !string.IsNullOrEmpty(datumRef) && _failedDatums.Contains(datumRef);`.
- TryRunSingleDatum(L197-216) 실패 분기 + TryRunDatumPhase(L146-189) 실패 분기 / 호출부(Action_FAIMeasurement L91/L95/L104/L110) 양쪽에서 일관되게 MarkDatumFailed 호출.

→ planner 권장: **옵션 A** (계약 변경 0, _datumTransforms 의미 보존, 추적 명확). 옵션 B 는 시그니처 변경(out succeeded) 으로 회귀 위험.

---

### `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — D-02 LastSkipReason 후보 위치 1

**현행 runtime 결과 필드 (L96-129):**
```csharp
// Result (runtime, not saved) — Action_FAIMeasurement가 Measurement 집계 결과를 써주고, TCP 응답(FAIResultData)이 읽어간다
[Browsable(false)]
public double MeasuredValue { get; set; }

[Browsable(false)]
public bool IsPass { get; set; }

// ... FAIName (INotifyPropertyChanged 패턴) ...

public void ClearResult() {
    MeasuredValue = 0;
    IsPass = false;
}
```

**D-02 신규 필드 패턴 (`LastHasResult` 패턴 복사):**
```csharp
//260529 hbk Phase 39 D-02 — datum-skip subtype. true 이면 FAI 가 측정 미실행(검출실패 원인).
//  AddResponse 가 EVisionResultType.NotExist(='N') 매핑에 사용. UI Excel export 분기에도 활용.
//  ClearResult / Action_FAIMeasurement EStep.Init(L60-62 ShotParam.ClearAllResults) 에서 false 로 리셋.
[Browsable(false)]
public bool WasDatumSkipped { get; set; }   //260529 hbk Phase 39 D-02

public void ClearResult() {
    MeasuredValue = 0;
    IsPass = false;
    WasDatumSkipped = false;                //260529 hbk Phase 39 D-02
}
```

**analog 근거 (왜 이 패턴이 맞나):**
- `MeasurementBase.LastHasResult` (`MeasurementBase.cs:51-52`) — 정확히 같은 의도(런타임 플래그 + INI 미직렬화 + ClearResult 동기 리셋). `[Browsable(false)]` + `ClearResult` 안에서 false 로 복귀 패턴이 일치.
- `MeasurementBase.ClearResult()` (`MeasurementBase.cs:90-95`) — 신규 플래그를 함께 클리어하는 1라인 추가 컨벤션.

---

### `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — D-02 LastSkipReason 후보 위치 2 (planner 선택)

**현행 (L43-52):**
```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public double LastMeasuredValue { get; set; } //260413 hbk 휘발성, INI 저장 제외

[PropertyTools.DataAnnotations.Browsable(false)]
public bool LastJudgement { get; set; } //260413 hbk true=OK

//260517 hbk CO-23-01: HasResult 판정 기준 분리 — 0.0 결과값도 정상 측정으로 표시하기 위해
//  LastMeasuredValue != 0 대신 별도 플래그 사용. EvaluateJudgement에서 true, ClearResult에서 false 설정.
[PropertyTools.DataAnnotations.Browsable(false)]
public bool LastHasResult { get; set; } //260517 hbk CO-23-01
```

**D-02 옵션 (Measurement 레벨에서 표현):**
```csharp
//260529 hbk Phase 39 D-02 — measurement 단위 skip reason. null/"" = 정상, "DATUM_FAIL" = datum 검출 실패로 skip.
//  string 채택 근거: enum 신설 시 INI/직렬화 영향 검토 필요. string 은 ParamBase reflection 의 case "String" 경로로 자동 호환 (DatumConfig.cs:38 TeachingImagePath 패턴 동일). 휘발성(저장 제외)로 명시하려면 [Browsable(false)] + ParamBase.Save/Load 분기 점검.
[PropertyTools.DataAnnotations.Browsable(false)]
public string LastSkipReason { get; set; } //260529 hbk Phase 39 D-02

public void ClearResult()
{
    LastMeasuredValue = 0;
    LastJudgement = false;
    LastHasResult = false;
    LastSkipReason = null;       //260529 hbk Phase 39 D-02
}
```

**Discretion 결정 가이드 (CONTEXT §Claude's Discretion):**
- **string 채택 권장.** enum 신설은 v2.7 PROTO-* 영역(`ECycleResult`) 과 충돌 (D-10 위반 위험).
- FAI 레벨(`FAIConfig.WasDatumSkipped`) + Measurement 레벨(`MeasurementBase.LastSkipReason`) **둘 다 두는 것**이 안전:
  - FAI.WasDatumSkipped → TCP cycle aggregation (AddResponse) 에서 사용.
  - Measurement.LastSkipReason → MeasurementResultRow ResultDisplay/JudgeText 분기에서 "DETECT FAIL" 라벨 표시.

---

### `WPF_Example/TcpServer/VisionResponsePacket.cs` — D-05/D-06 FAIResultData 시그니처

**현행 `FAIResultData` (L510-526):**
```csharp
//260409 hbk Phase 5: FAI별 개별 측정 결과 (D-04, D-05)
public class FAIResultData {
    public string FAIName { get; set; }
    public EVisionResultType Result { get; set; }
    public double DistanceMm { get; set; }

    public FAIResultData() {
        Result = EVisionResultType.NG;
        DistanceMm = 0;
    }

    public FAIResultData(string name, bool isPass, double distMm) {
        FAIName = name;
        Result = isPass ? EVisionResultType.OK : EVisionResultType.NG;
        DistanceMm = distMm;
    }
}
```

**D-06 변경 패턴 (시그니처 추가 — 기존 ctor 보존, 호출부 점진 이행):**
```csharp
//260529 hbk Phase 39 D-06 — 3-state(P/F/N). 기존 (bool isPass) ctor 는 호환 유지 (회귀 0).
public FAIResultData(string name, EVisionResultType result, double distMm) {  //260529 hbk Phase 39 D-06
    FAIName = name;
    Result = result;
    DistanceMm = distMm;
}
```
→ `AddResponse` 호출만 신규 ctor 로 교체. `Result` 프로퍼티 set 자체는 이미 `EVisionResultType` 받으므로 직렬화 영향 0.

**현행 와이어 직렬화 (L353-365 IsDynamicFAI 분기):**
```csharp
//260409 hbk Phase 5: 동적 FAI 모드 직렬화 (D-05)
if (testPacket.IsDynamicFAI) {
    msg += testPacket.GetResultString();
    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
    msg += testPacket.FAICount.ToString();
    for (int i = 0; i < testPacket.FAICount; i++) {
        msg += VisionServer.MSG_CONTENTS_SEPERATOR;
        var faiData = testPacket.FAIResults[i];
        msg += (faiData.Result == EVisionResultType.OK) ? TEST_RESULT_PASS : TEST_RESULT_FAIL;
        //                                                ↑ ← D-06: 'N' 출력 누락 — 여기 수정 필요
        msg += VisionServer.MSG_CONTENTS_SEPERATOR;
        msg += faiData.DistanceMm.ToString("0.000");
    }
}
```

**D-06 직렬화 수정 패턴 (analog: `GetResultString` L560-574):**
```csharp
// 직렬화 라인 교체 — switch 로 3-state 출력
string faiCode;                                                          //260529 hbk Phase 39 D-06
if (faiData.Result == EVisionResultType.OK) faiCode = TEST_RESULT_PASS;  //260529 hbk Phase 39 D-06 — 'P'
else if (faiData.Result == EVisionResultType.NotExist) faiCode = TEST_RESULT_NOTEXIST; //260529 hbk Phase 39 D-06 — 'N'
else faiCode = TEST_RESULT_FAIL;                                          //260529 hbk Phase 39 D-06 — 'F' (NG 및 fallback)
msg += faiCode;
```

**Cycle Result ('N' 송신 회귀 가드):**
- `TestResultPacket.GetResultString()` (L560-574) 는 이미 `EVisionResultType.NotExist` → `TEST_RESULT_NOTEXIST` 매핑 처리 — 수정 불필요.
- `responsePacket.Result = EVisionResultType.NotExist` 만 새로 세팅하면 자동으로 'N' 송신됨.

---

### `WPF_Example/Custom/SystemHandler.cs` — D-05 결과 흐름 검증 (변경 거의 없음)

**현행 ProcessRecipeChange L156-183 (참조용 — Result 세팅 컨벤션):**
```csharp
private RecipeChangeResultPacket ProcessRecipeChange(RecipeChangePacket packet) {
    RecipeChangeResultPacket resultPacket = new RecipeChangeResultPacket();
    // ...
    if (Recipes.HasRecipe(recipeName) == false) {
        resultPacket.Result = EVisionResultType.NG;
    } else if ((Setting.CurrentRecipeName != recipeName) && LoadRecipe(recipeName)) {
        resultPacket.Result = EVisionResultType.OK;
    }
    // ...
    return resultPacket;
}
```

**Phase 39 영향:** 별도 코드 변경 없음. `MainRun` 폴링 루프가 `InspectionSequence.AddResponse` 가 enqueue 한 패킷을 그대로 직렬화 → TCP 송신. **검증만 수행**: `responsePacket.Result = EVisionResultType.NotExist` 가 와이어에서 'N' 으로 송신되는지 UAT 단계에서 확인 (Cal_Image 기반 검출실패 케이스).

---

### `WPF_Example/UI/ContentItem/MainView.xaml.cs` — D-04 'DETECT FAIL' 라벨 트리거

**현행 patterns (`halconViewer.UpdateDisplayState` 호출 L717/L730/L738):**
```csharp
halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages); //260409 hbk
```
→ overlay 갱신 진입점. D-04 라벨은 `HalconDisplayService.RenderDatumOverlay` 내부 추가가 더 정합 (L890 `RenderDatumFindResult` 이후 1단 추가).

**Phase 22 IMG-02 회귀 가드 패턴 (memory: project_phase22_progress, project_v1_1_progress):**
- `Action_FAIMeasurement.cs:126` 주석:
```csharp
//260511 hbk Phase 22 IMG-02 — ShotParam.SimulImagePath = InspectionImagePath 역할 (검사 사이클 마다 로드). 
//  티칭 기준 이미지는 별도 DatumConfig.TeachingImagePath (셋업 시 1회, INI 보존) 사용 — 역할 분리. 
//  Simul 에서 두 경로 동일 파일 가능 (UAT Test 2).
```
- **본 phase 적용 규칙:** UAT 입력 이미지 셋업/주입 시 `DatumConfig.TeachingImagePath` ↔ `ShotConfig.SimulImagePath` 혼동 금지. SIMUL 검출실패 케이스를 만들 때:
  - 시나리오 A: `TeachingImagePath` 와 `SimulImagePath` 가 의도적으로 일치 → datum 검출 성공 (OK 경로).
  - 시나리오 B: `SimulImagePath` 를 다른 워크피스 이미지로 지정 → datum 검출 실패 (NotExist 경로).
- Cal_Image/DualImageTest/SIDE1_3-1_Datum_A1_A2_backlight_LV30.bmp / SIDE1_3-1_Datum_B1_backlight_LV30.bmp — 두 다른 파일을 양쪽에 분리 배치하면 자연스럽게 검출 실패 시나리오 구성 가능.

---

### `WPF_Example/Halcon/Display/HalconDisplayService.cs` — D-04 overlay 'DETECT FAIL' 라벨

**현행 `RenderDatumOverlay` L683-895:**
- L800-819 `if (datum.IsConfigured)` 블록 — magenta 십자 + "Datum Origin" 라벨 (analog: WriteString 호출 패턴).
- L822 `if (datum.LastTeachSucceeded)` 블록 — 검출 라인/원/중심점 (analog: 성공 시 가시화 게이트).
- L889 `RenderDatumFindResult(window, datum)` — `LastFindSucceeded` 분기 내부 처리.

**analog 라벨 렌더 (L912-938 `DrawRoiLabelAt`):**
```csharp
private void DrawRoiLabelAt(HWindow window, double row, double col, string label) {
    try {
        EnsureFontInitialized(window);
        if (!string.IsNullOrEmpty(_normalFontName)) {
            string smallFont = _normalFontName.Replace("-18", "-13");
            HOperatorSet.SetFont(window, smallFont);
        }
        HOperatorSet.SetColor(window, "yellow");
        HOperatorSet.SetTposition(window, row, col);
        HOperatorSet.WriteString(window, label);
        if (!string.IsNullOrEmpty(_normalFontName)) {
            HOperatorSet.SetFont(window, _normalFontName);
        }
    } catch {
        // Suppress display errors (기존 RenderDatumOverlay catch 관습 유지)
    }
}
```

**D-04 라벨 추가 패턴 (D-04 'DETECT FAIL' — analog 그대로 차용):**
```csharp
//260529 hbk Phase 39 D-04 — Datum 검출 실패 시 'DETECT FAIL' 라벨 렌더.
//  분기: !LastFindSucceeded && IsConfigured (Teach 는 했지만 런타임 Find 실패).
//  위치: RefOrigin 십자 위쪽(L817 "Datum Origin" 라벨과 충돌 없게 추가 오프셋).
//  색상: "red" (적색 경고 — analog: 기존 yellow Datum Origin 라벨 옆 시각 구분).
//  주의: SetColor 색상명은 표준명만 사용 (memory feedback_halcon_setcolor_invalid_names — "light green" 사례).
//        "red"/"#FF0000" 모두 표준. 비표준명은 catch swallow 로 silent 미표시 → 1순위 의심.
if (datum.IsConfigured && !datum.LastFindSucceeded) {
    try {
        HOperatorSet.SetColor(window, "red");
        EnsureFontInitialized(window);
        HOperatorSet.SetTposition(window, datum.RefOriginRow - 40, datum.RefOriginCol + 5);
        HOperatorSet.WriteString(window, "DETECT FAIL");
    } catch {
        // Suppress display errors (RenderDatumOverlay catch 관습)
    }
}
```
**호출 위치:** `RenderDatumOverlay` 본문 L885-890 (`RenderDatumFindResult` 호출 직전 또는 직후). L891 `catch { /* Suppress */ }` 안에 흡수되도록 본 try 블록 안에 둠.

**analog 근거 (Z-stack 컨벤션):**
- `// Suppress display errors (기존 RenderDatumOverlay catch 관습 유지)` — L893, L936 동일 컨벤션 명시. D-04 라벨도 같은 컨벤션 준수.
- L887-889 주석: "검출 십자는 자체 LastFindSucceeded 게이트(메서드 내부)만 따르면 충분. z-stack last 유지." — D-04 'DETECT FAIL' 라벨도 동일하게 LastFindSucceeded 자체 게이트 가짐.

---

### `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — D-04 Datum 노드 적색 배지

**현행 트리 노드 헤더 갱신 패턴 (`NodeViewModel.OnParamPropertyChanged` L210-232):**
```csharp
//260526 hbk CO-31-01 — Param Name 변경 → Node.Name 동기화 + RaisePropertyChanged("Name")
private void OnParamPropertyChanged(object sender, PropertyChangedEventArgs e) {
    string newName = null;
    switch (e.PropertyName) {
        case nameof(DatumConfig.DatumName):
            newName = (sender as DatumConfig)?.DatumName;
            break;
        // ... ShotName / FAIName / MeasurementName
    }
    if (newName != null && this.Node != null) {
        this.Node.Name = newName;
        RaisePropertyChanged("Name");
    }
}
```

**Phase 37 hotfix B 회귀 가드 (e.Source 비대칭 패턴 — InspectionListView.xaml.cs L410-418):**
```csharp
//260511 hbk CO-22-01 — sender 기준 게이트 (e.Source 비대칭 회피)
//  기존 `e.Source is TreeListBox` 게이트는 TreeListBox 내부 (계층형 inner item / EditableTextBlock 등) 가 발생시킨
//  Selector.SelectionChanged routed event bubble 시 e.Source 가 inner element 가 되어 게이트 실패 → 함수 전체 skip.
//  Datum → FAI 전환에서 본 핸들러가 skip 되면 XAML 바인딩도 force rebind 도 모두 안 돌아 PropertyGrid 가 직전 Datum 으로 stale.
if (!(sender is TreeListBox list)) return;
if (!ReferenceEquals(sender, treeListBox_sequence)) return;
```
**적용 규칙 (Phase 39):**
- 신규 라우티드 이벤트 핸들러를 추가하면 **반드시 `sender` 기준 게이트** 사용. `e.Source` 게이트 금지.
- 적색 배지 NodeViewModel 노출 시, 이미 등록된 OnParamPropertyChanged 흐름을 그대로 활용 (`DatumConfig.LastFindSucceeded` 가 change notification 발화하도록 INPC 추가하는 것이 더 간결).

**Force rebind 패턴 (CO-22-01 — L558-585):**
```csharp
//260511 hbk CO-22-01 — Action(ShotConfig) 분기 force rebind 추가.
if (ParamEditor != null && itemParam is ParamBase) {
    _isRebinding = true;
    try {
        ParamEditor.SelectedObject = null;
        ParamEditor.SelectedObject = itemParam;
    } finally {
        _isRebinding = false;
    }
}
```
**Phase 39 적용:** 직접적 적용 없음. 단 D-04 적색 배지 UI 가 Datum 노드 선택 후 상태 변경(검출 직후) 시 PropertyGrid 가 stale 되지 않도록 동일 패턴 (`_isRebinding` 게이트 + force rebind) 차용 가능.

**Datum 노드 적색 배지 권장 구현 경로 (planner 결정 사항):**
1. `DatumConfig.LastFindSucceeded` setter 를 INPC 발화하도록 변경 (현행 단순 auto-prop → backing field + RaisePropertyChanged).
2. `NodeViewModel.OnParamPropertyChanged` 에 `case nameof(DatumConfig.LastFindSucceeded)` 추가 → `RaisePropertyChanged("HasDetectFail")` 같은 신규 노출 프로퍼티.
3. `InspectionListView.xaml` 의 Datum 노드 DataTemplate 에 `HasDetectFail` 바인딩 한 빨강 작은 dot/badge 추가.
4. 적색 = `#FFD32F2F` 또는 `Red` 컬러 (XAML SolidColorBrush; HALCON 색상 컨벤션과 분리).

---

## Shared Patterns

### 헝가리안/마커 컨벤션 (Phase 20 D-12 marker stacking)
**Source:** memory `feedback_comment_convention.md` + 코드베이스 전반 (예: `Action_FAIMeasurement.cs` L80-117 에 6세대 마커 누적 — Phase 6 / Phase 31 / Phase 32 / Phase 34 / Phase 37).
**Apply to:** 모든 신규/수정 라인.
**규칙:**
- 신규 라인 끝: `//260529 hbk Phase 39 WF-01/WF-02 D-XX — <요지>` 형식.
- 기존 마커는 절대 삭제하지 않음. 위/옆에 누적 append.

### try/catch lenient (Phase 37 D-37-03 / D-07)
**Source:** `Action_FAIMeasurement.cs` L88-100 (datum), L211-219 (measurement).
**Apply to:** 신규 코드의 모든 외부 호출(Halcon, 파일 I/O).
**Pattern:**
```csharp
try {
    ok = ExternalCall(...);
} catch (Exception ex) {
    ok = false;
    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] ... failed: " + ex.Message);
}
// abort 안 함. 누적 결과(allPass / WasDatumSkipped)에 반영.
```

### Logging 컨벤션
**Source:** `Action_FAIMeasurement.cs` L223 / L90 / L95 / L104 / L110 / `InspectionSequence.cs` L165 / L185.
**Apply to:** D-04 datum skip 로그.
**Pattern:**
```csharp
Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' 검출 실패 (skip): " + (reason ?? ""));
```
- 항상 `(int)ELogType.X` 캐스팅.
- 컴포넌트 prefix `[FAIMeasurement]` / `[DatumPhase]` 유지.
- 한국어 + 영문 혼용 (`검출 실패 (skip)`) — 기존 패턴 보존.

### Browsable(false) + 휘발성 필드
**Source:** `MeasurementBase.cs` L43-52 / `DatumConfig.cs` L405-413 / `FAIConfig.cs` L97-101.
**Apply to:** 신규 `WasDatumSkipped`, `LastSkipReason`.
**Pattern:**
```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public bool/string XxxField { get; set; }   // runtime 전용, INI 미직렬화 (단 string 은 ParamBase reflection 이 자동 직렬화하므로 Browsable 만으로는 부족 — `MeasurementBase.LastMeasuredValue` 처럼 명시적 휘발 의도라면 ParamBase Save 분기 확인 필요)
```

### HALCON SetColor 가드 (memory feedback_halcon_setcolor_invalid_names)
**Source:** `HalconDisplayService.cs:867` 주석:
```csharp
//260430 hbk Quick 260430 — "light green" 비표준 색상명 → HALCON SetColor 예외 → catch swallow → 검출 원 + center cross 둘 다 미표시 결함. hex "#90EE90" 으로 교체 (사용자 의도 보존).
HOperatorSet.SetColor(window, "#90EE90");
```
**Apply to:** D-04 적색 라벨 'DETECT FAIL'.
**규칙:** 표준 색상명(red, yellow, blue, cyan, magenta, green, gray, orange) 또는 hex(#RRGGBB)만 사용. "light red", "dark red" 같은 비표준명 절대 금지.

### MeasurePos / Halcon 알고리즘 컨벤션 (memory feedback_halcon_measurepos_must_haves)
**Apply to:** 본 phase는 알고리즘 추가 없음 — N/A. (혹시 datum 재구현이 들어가면 strip-loop + measurePhi + EdgeSelection 3종 필수.)

## Anti-pattern Guards

### Phase 37 hotfix B (CO-22-01) — e.Source 비대칭
**Source:** `InspectionListView.xaml.cs` L410-418, L558-585.
**증상:** Routed event 핸들러에서 `e.Source is TreeListBox` 게이트 사용 시 inner element (EditableTextBlock 등) 이벤트가 게이트 통과 못해 핸들러 skip → 분기 force rebind 호출 누락 → UI stale (`PropertyGrid` 가 이전 Datum 유지).
**Phase 39 적용 시점:** D-04 적색 배지 / 'DETECT FAIL' 라벨 UI 갱신 코드 작성 시.
**Fix shape (재현 패턴):**
```csharp
if (!(sender is TreeListBox list)) return;                       //260511 hbk CO-22-01
if (!ReferenceEquals(sender, treeListBox_sequence)) return;       //260511 hbk CO-22-01
// 모든 Datum/Action/Shot 노드 타입 분기에서 force rebind:
if (ParamEditor != null && itemParam is ParamBase) {
    _isRebinding = true;
    try {
        ParamEditor.SelectedObject = null;
        ParamEditor.SelectedObject = itemParam;
    } finally {
        _isRebinding = false;
    }
}
```

### Phase 22 IMG-02 (역할 분리)
**Source:** `Action_FAIMeasurement.cs` L126-129 주석 + `DatumConfig.cs` L38-46.
**증상:** `ShotConfig.SimulImagePath` (검사 사이클마다 로드) ↔ `DatumConfig.TeachingImagePath` (셋업 시 1회) 역할 혼동 → 잘못된 이미지로 검출.
**Phase 39 적용 시점:** UAT 시 검출실패 케이스 이미지 셋업 시.
**규칙:**
- Shot 입력(SimulImagePath) = "지금 검사할 워크피스" 시뮬레이션.
- Datum 입력(TeachingImagePath / TeachingImagePath_Vertical) = "티칭에 사용한 기준 이미지" — Datum 검출 알고리즘이 이 이미지로 실행.
- SIMUL 모드에서 datum 검출 실패 케이스 만들기 = Teaching 이미지에서는 잘 찾히지만 Inspection 이미지(SimulImagePath)에는 datum 형상이 없거나 변형된 케이스.

### Phase 20 D-12 — Marker stacking
**Source:** `Action_FAIMeasurement.cs` L80-117 (Phase 6 / 31 / 32 / 34 / 37 마커 6세대 누적).
**규칙:** 신규 마커 추가 시:
- 기존 마커는 줄 끝에 그대로 유지.
- 신규 마커는 같은 줄 추가 또는 위 줄 신규로 append (메모리 patterns: "기존 마커 보존, 위에 누적 append").
- 절대 기존 마커를 삭제하거나 변경하지 않음 (회귀 사유 추적 위해).

### Phase 37 D-37-03 lenient 회귀 0 가드
**Source:** `Action_FAIMeasurement.cs` L80-121 + `InspectionSequence.cs` L146-216.
**증상 (재발 가능):** Datum 실패 시 한 곳에서라도 `FinishAction(EContextResult.Error)` 호출 또는 `Step = (int)EStep.End` jump 추가 → Phase 37 의 "다른 datum 살아있어도 측정 진행" 의도 파괴.
**Phase 39 규칙:**
- D-01 per-FAI gate 는 **Measure 루프 안에서만** 동작. DatumPhase 의 lenient 흐름(L119 `Step = (int)EStep.Grab`) 변경 금지.
- `TryGetDatumTransform` (L218-225) 시그니처 변경 금지. 별도 `IsDatumFailed` 메서드로 게이트 분리.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| (없음) | — | — | 모든 변경점이 in-repo 분석 가능한 직접 analog 보유 |

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (action, sequence, FAIConfig, MeasurementBase, DatumConfig)
- `WPF_Example/TcpServer/` (VisionResponsePacket, FAIResultData, TestResultPacket 직렬화)
- `WPF_Example/Halcon/Display/` (HalconDisplayService.RenderDatumOverlay + DrawRoiLabelAt)
- `WPF_Example/UI/ContentItem/` (MainView.xaml.cs 화면 갱신 진입점)
- `WPF_Example/UI/ControlItem/` (InspectionListView, NodeViewModel — 트리 헤더 갱신 / force rebind)
- `WPF_Example/UI/ViewModel/` (InspectionViewModel, MeasurementResultRow — 결과 행 갱신)
- `WPF_Example/Custom/SystemHandler.cs` (MainRun 결과 송신 경로 참조용)

**Files scanned:** 11 (전부 in-repo, .claude/worktrees/ 제외)
**Pattern extraction date:** 2026-05-29
