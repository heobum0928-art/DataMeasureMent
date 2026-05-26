# Phase 33: Side/Bottom InspectionSequence 마이그레이션 — Pattern Map

**Mapped:** 2026-05-26
**Files analyzed:** 6 source files (analog discovery), 7 read-only refs
**Analogs found:** 6 / 6 (모두 동일 코드베이스에 존재 — Top 이 이미 InspectionSequence 사용 중)

---

## File Classification

| 변경 대상 / 행위 | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `SequenceHandler.RegisterSequences()` L30-34 | sequence-registration (config) | request-response | 동일 파일 L31 Top 등록 라인 | **exact** (1줄 교체 × 2) |
| `SequenceHandler.RegisterActions()` L37-43 | action-registration (config) | request-response | 동일 파일 — Top 케이스 + RebuildInspectionActions L64-86 | **exact** (Top 케이스는 DynamicFAI 경로로 우회) |
| `Action_FAIMeasurement` 인스턴스화 (Side/Bottom 용) | action | request-response (Datum→Grab→Measure→End) | `Action_FAIMeasurement.cs` 자체 (변경 금지 D-06) — Top 이 이미 사용 | **exact** |
| `InspectionSequence` 인스턴스화 (Side/Bottom 용) | sequence (thread + Datum) | request-response | `InspectionSequence.cs` 자체 (변경 금지 D-06) — Top 이 이미 사용 | **exact** |
| `BottomDieInfo → FAIResultData` 매핑 (D-03) | data-transform | request-response | `InspectionSequence.AddResponse()` L66-95 (FAIResults.Add 루프) | role-match |
| `InspectionRecipeManager.LoadPhase6Format` 레거시 INI 분기 (D-04) | recipe-loader (migration) | file-I/O → state | 동일 파일 `DetectFormatVersion` L82-93 + `LoadPhase6Format` L178-249 | **exact** (분기 + Shot 생성 패턴) |
| `[Obsolete]` 적용 — 4 deprecate 클래스 (D-05) | code-attribute | n/a | (분석 — `[Obsolete]` 사용처 없음 in repo) | new-pattern |

---

## Pattern Assignments

### `SequenceHandler.RegisterSequences()` L30-34 — sequence-registration

**Analog (변경 대상 자체):** `WPF_Example/Custom/Sequence/SequenceHandler.cs:28-35`

**Imports pattern** (L1-6):
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Utility;
```

**Top 의 InspectionSequence 등록 라인 — Side/Bottom 도 복제할 패턴** (L30-31):
```csharp
SequenceBuilder.RegisterSequence(
    new InspectionSequence(ESequence.Top, SEQ_TOP, Top_Alg_Index, DeviceHandler.CAMERA_TOP, LightHandler.LIGHT_TOP),
    new TopSequence(ESequence.Side, SEQ_SIDE, Side_Alg_Index, DeviceHandler.CAMERA_SIDE, LightHandler.LIGHT_SIDE),       // ← D-01 교체
    new BottomSequence(ESequence.Bottom, SEQ_BOTTOM, Bottom_Alg_Index, DeviceHandler.CAMERA_BOTTOM, LightHandler.LIGHT_BOTTOM)  // ← D-01 교체
);
```

**적용 후 (D-01 결정):**
```csharp
SequenceBuilder.RegisterSequence(
    new InspectionSequence(ESequence.Top, SEQ_TOP, Top_Alg_Index, DeviceHandler.CAMERA_TOP, LightHandler.LIGHT_TOP),
    new InspectionSequence(ESequence.Side, SEQ_SIDE, Side_Alg_Index, DeviceHandler.CAMERA_SIDE, LightHandler.LIGHT_SIDE),
    new InspectionSequence(ESequence.Bottom, SEQ_BOTTOM, Bottom_Alg_Index, DeviceHandler.CAMERA_BOTTOM, LightHandler.LIGHT_BOTTOM)
);
```

---

### `SequenceHandler.RegisterActions()` L37-43 — action-registration

**Analog:** 동일 파일 — Top 의 동적 FAI 모드 진입은 `RebuildInspectionActions` (L64-86) 가 책임.

**현 상태 (L37-43):**
```csharp
SequenceBuilder.RegisterAction(
    new TopInspectionAction(EAction.Top_Inspection, ACT_INSPECT, Top_Alg_Index, Inspection_Model_Index),
    new TopInspectionAction(EAction.Side_Inspection, ACT_INSPECT, Side_Alg_Index, Inspection_Model_Index),
    new BottomInspectionAction(EAction.Bottom_Inspection, ACT_INSPECT, Bottom_Alg_Index, Inspection_Model_Index)
);
```

**복제 대상 패턴 — `RebuildInspectionActions` Shot 기반 동적 액션 생성 (L72-86):**
```csharp
// Shot별로 Action_FAIMeasurement 생성
var actions = new List<ActionBase>();
for (int i = 0; i < RecipeManager.ShotCount; i++) {
    ShotConfig shot = RecipeManager.Shots[i];
    EAction actionId = (EAction)((int)EAction.FAI_Base + i);
    string actionName = shot.ShotName ?? $"SHOT_{i}";
    var action = new Action_FAIMeasurement(actionId, actionName, shot);
    actions.Add(action);
}
```

**주의 (D-07 — researcher 결정):** Side/Bottom 도 `Action_FAIMeasurement` 로 통일 시 시퀀스 부트스트랩 시 `TopInspectionAction`/`BottomInspectionAction` 등록은 **무의미해진다** (인스턴스 생성 차단 = Deprecate 의 정확한 의미, D-05). 단, `EAction.Side_Inspection` / `EAction.Bottom_Inspection` 매핑은 `InitializeSequences()` L52-58 의 `AddAction(EAction.Side_Inspection)` 호출 때문에 등록 자체는 필요 — placeholder 로 `Action_FAIMeasurement` 인스턴스를 매핑하거나, `InitializeSequences` 자체를 동적 FAI 모드 진입까지 비어있는 액션 리스트로 시작하도록 변경 필요. **Plan 단계 결정.**

**현 상태 (L52-58):**
```csharp
seq = SequenceBuilder.CreateSequence(ESequence.Side);
seq.AddAction(EAction.Side_Inspection);
RegisterSequence(seq);

seq = SequenceBuilder.CreateSequence(ESequence.Bottom);
seq.AddAction(EAction.Bottom_Inspection);
RegisterSequence(seq);
```

---

### `InspectionSequence` 생성자 시그니처 — Side/Bottom 에서 그대로 호출

**Analog (변경 금지 D-06):** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:54-63`

**생성자:**
```csharp
public InspectionSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
    pDevs = SystemHandler.Handle.Devices;
    Context = new InspectionSequenceContext(this);
    pMyContext = Context as InspectionSequenceContext;
    //260417 hbk Phase 6-04 UAT: CameraMasterParam → InspectionMasterParam 교체 (DisplayName 편집 UI 노출, D-01)
    Param = new InspectionMasterParam(this);
    pMyParam = Param as CameraMasterParam;
    DefaultCamera = defaultCamera;
    DefaultLight = defaultLight;
}
```

**핵심 필드 (L37-52):**
```csharp
public class InspectionSequence : SequenceBase {
    private readonly DeviceHandler pDevs;
    private readonly InspectionSequenceContext pMyContext;
    private readonly CameraMasterParam pMyParam;
    private readonly string DefaultCamera;
    private readonly string DefaultLight;

    //260413 hbk Phase 6: Fixture DisplayName — 사용자 편집 가능 (D-01)
    public string DisplayName { get; set; } = "";

    //260413 hbk Phase 6: Multi-Datum — Fixture 레벨 Datum 소유 (D-04)
    [PropertyTools.DataAnnotations.Browsable(false)]
    public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>();

    //260413 hbk Phase 6: 런타임 transform 캐시 (D-09)
    private readonly Dictionary<string, HTuple> _datumTransforms = new Dictionary<string, HTuple>();
```

**TryRunDatumPhase — 다중 Datum 순회 (L144-171):**
```csharp
public bool TryRunDatumPhase(HImage image, out string error) {
    error = null;
    _datumTransforms.Clear();

    if (DatumConfigs.Count == 0) {
        return true; // D-10: Datum 미설정 Fixture는 무보정 pass-through
    }

    if (image == null) {
        error = "image is null";
        return false;
    }

    var service = new DatumFindingService();
    foreach (var datum in DatumConfigs) {
        HTuple transform;
        string datumError;
        if (!service.TryFindDatum(image, datum, out transform, out datumError)) {
            error = $"Datum '{datum.DatumName}' failed: {datumError}";
            datum.LastFindSucceeded = false;
            return false;
        }
        datum.LastFindSucceeded = true;
        datum.CurrentTransform = transform;
        _datumTransforms[datum.DatumName ?? ""] = transform;
    }
    return true;
}
```

**Implication for Phase 33:** Side/Bottom 도 `new InspectionSequence(ESequence.Side|Bottom, ...)` 한 줄이면 DatumConfigs / TryRunDatumPhase / DisplayName 모두 자동 획득. **InspectionSequence 자체에 Side/Bottom 분기 없음** — 100% 재사용 가능.

---

### Bottom Multi-Die → FAIResults[] 매핑 (D-03)

**Analog (TCP 응답 패턴):** `InspectionSequence.AddResponse()` L66-95

**FAIResults 빌드 루프 (L80-89):**
```csharp
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
```

**기존 Bottom Multi-Die 패턴 (Deprecate 대상 — 변환 입력 데이터로만 참조):** `Sequence_Bottom.cs:115-128`
```csharp
if (responsePacket.InspectionType == (int)ETestType.Inspection) {
    var index = 0;
    foreach (var item in pMyContext.BottomDie) {
        if (index >= TestResultPacket.MaxListCount) break;
        responsePacket.visionResults[index].X = item.Value.CenterOffsetXmm;
        responsePacket.visionResults[index].Y = item.Value.CenterOffsetYmm;
        responsePacket.visionResults[index].Angle = item.Value.DieAngle;
        responsePacket.visionResults[index].Result = item.Value.Judgment ? EVisionResultType.OK : (item.Value.newJudgment == 0 ? EVisionResultType.NotExist : EVisionResultType.NG);
        index++;
    }
    for (; index < TestResultPacket.MaxListCount; index++) {
        responsePacket.visionResults[index].Result = EVisionResultType.NotExist;
    }
}
```

**`BottomDieInfo` 데이터 모델 (`Action_BottomInspection.cs:22-34`):**
```csharp
public class BottomDieInfo {
    public double DieCenter_X { get; set; }
    public double DieCenter_Y { get; set; }
    public double DieAngle { get; set; }
    public int ContourCount { get; set; }
    public double ContourArea { get; set; }
    public int ApexCount { get; set; }
    public bool Judgment { get; set; }
    public int newJudgment { get; set; }
    public double CenterOffsetXmm { get; set; }
    public double CenterOffsetYmm { get; set; }
    public Mat image { get; set; }
}
```

**`FAIResultData` 데이터 모델 (`VisionResponsePacket.cs:511-526`):**
```csharp
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

**D-03 매핑 제약 (planner 결정 필요):**
- `FAIResultData` 의 현재 필드 = `FAIName` + `Result` + `DistanceMm` **단일 스칼라**.
- `BottomDieInfo` 는 X/Y/Angle 3개 metric 보유.
- **Open question (D-03 명시):** `FAIResultData` 에 X/Y/Angle 필드 확장 vs Die 1개당 FAI 3개 (X/Y/Angle 각각) 분리.
- **`TestResultPacket.visionResults[10]`** 배열 (L541-542) 은 그대로 보존됨 → 호스트가 새 형식 미준비 시 hybrid 송출 가능 (하지만 D-03 은 FAIResults[] 통일 명시).

---

### `InspectionRecipeManager.LoadPhase6Format` 레거시 INI 분기 (D-04)

**Analog (분기 + 로드 패턴):** `InspectionRecipeManager.cs`

**현 `DetectFormatVersion` (L82-93) — 신규 분기 추가 지점:**
```csharp
private ERecipeFormatVersion DetectFormatVersion(IniFile iniFile) {
    if (iniFile.ContainsSection("FORMAT")) {
        int version = iniFile["FORMAT"]["Version"].ToInt();
        if (version >= 6) return ERecipeFormatVersion.Phase6;
        if (version >= 1 && version <= 5) return ERecipeFormatVersion.Phase5;
        return ERecipeFormatVersion.Unknown;
    }
    if (iniFile.ContainsSection("SHOTS")) {
        return ERecipeFormatVersion.Phase5;
    }
    return ERecipeFormatVersion.Unknown;
}
```

**현 `Load` 분기 (L162-175) — 마이그레이션 진입점:**
```csharp
public bool Load(IniFile loadFile) {
    //260413 hbk Phase 6: 포맷 버전 감지 후 분기 (D-22)
    ERecipeFormatVersion version = DetectFormatVersion(loadFile);
    if (version != ERecipeFormatVersion.Phase6) {
        //260413 hbk Phase 6: 기존 포맷 거부 — 안내 메시지 표시 (D-22)
        CustomMessageBox.Show(
            "Legacy Recipe",
            "이 레시피는 이전 포맷(Phase 1~5)입니다.\n새 Phase 6 레시피로 작성하세요.",
            MessageBoxImage.Information);
        Logging.PrintLog((int)ELogType.Trace, $"[InspectionRecipeManager] Legacy recipe rejected (version={version})");
        return false;
    }
    return LoadPhase6Format(loadFile);
}
```

**Shot 생성 패턴 — Phase6 로드 본문에서 복제할 부분 (L201-218):**
```csharp
for (int s = 0; s < shotCount; s++) {
    string shotSection = $"SHOT_{s}";
    if (!loadFile.ContainsSection(shotSection)) continue;

    ShotConfig shot = AddShot();
    shot.ShotName = loadFile[shotSection]["ShotName"].ToString();
    shot.ZPosition = loadFile[shotSection]["ZPosition"].ToDouble();
    shot.DelayMs = loadFile[shotSection]["DelayMs"].ToInt();
    shot.SimulImagePath = loadFile[shotSection]["SimulImagePath"].ToString();

    int faiCount = loadFile[shotSection]["FAICount"].ToInt();
    if (faiCount < 0) faiCount = 0;

    // Camera/ShotConfig 필드 (조명 8필드 포함) 자동 로드
    string camSection = shotSection + "_CAM";
    if (loadFile.ContainsSection(camSection)) {
        shot.Load(loadFile, camSection);
    }
```

**ResolveFixtureSequence 한계 (L72-79) — Phase 33 에서 확장 필요:**
```csharp
//260413 hbk Phase 6: 현재 Fixture(InspectionSequence) 해석 — 기본 Top 시퀀스를 사용 (D-04)
private InspectionSequence ResolveFixtureSequence() {
    try {
        var seq = SystemHandler.Handle.Sequences[ESequence.Top] as InspectionSequence;
        return seq;
    } catch {
        return null;
    }
}
```
**Implication:** `ResolveFixtureSequence` 가 **항상 Top** 만 반환 → Side/Bottom 의 DatumConfigs 가 INI 에 저장/로드되지 않음. D-06 은 "변경 금지" 라 했으나 **이 메서드의 Top 하드코딩은 Side/Bottom 마이그레이션의 직접 차단 요인** → planner 가 Top 회귀 0 보장 조건에서 이 메서드의 시그니처 확장 vs RecipeManager 인스턴스 분리 결정 필요.

**레거시 INI 식별 가능 키 (참고 — 실제 파일 부재로 코드 기반 추론):**
- Bottom: `[BOTTOM_DIE_*]`, `Picker_X[]=`, `ScreenCenter_X/Y` (BottomSequenceContext L23/26-29 / BottomInspectionContext L46-58)
- Top: `[CAMERA]` `ROI` (`TopInspectionParam.ROI`), `[Top Inspection]` 카테고리 (`TopInspectionParam.cs:44`)
- Phase6 신규: `[FORMAT] Version=6`, `[FIXTURE]`, `[SHOTS]`, `[SHOT_n_FAI_m_MEAS_k]` 계층

---

### `Action_FAIMeasurement` 인스턴스 생성 — Side/Bottom 에서도 동일

**Analog (변경 금지 D-06):** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`

**생성자 (L44-48):**
```csharp
public Action_FAIMeasurement(EAction id, string name, ShotConfig shotConfig) : base(id, name) {
    Context = new FAIMeasurementContext(this);
    pMyContext = Context as FAIMeasurementContext;
    Param = shotConfig;
}
```

**EStep 흐름 (L30-37) — DatumPhase 포함:**
```csharp
private enum EStep {
    Init,
    MoveZ,       //260409 hbk Phase 5: Z축 이동 스텝 (D-08)
    DatumPhase,  //260413 hbk Phase 6: Multi-Datum 실행 (D-09)
    Grab,
    Measure,
    End
}
```

**DatumPhase 본문 (L79-104) — Side/Bottom 도 자동 활성화:**
```csharp
case EStep.DatumPhase: {
    var parentSeq = ShotParam != null ? ShotParam.Parent as InspectionSequence : null;
    if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {
        HImage datumImage = GrabOrLoadDatumImage(parentSeq);
        if (datumImage == null) {
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum image acquisition failed");
            pMyContext.AllPass = false;
            FinishAction(EContextResult.Error);
            break;
        }
        try {
            string datumError;
            if (!parentSeq.TryRunDatumPhase(datumImage, out datumError)) {
                Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum failed: " + datumError);
                pMyContext.AllPass = false;
                FinishAction(EContextResult.Error);
                break;
            }
        } finally {
            datumImage.Dispose();
        }
    }
    // DatumConfigs 비어있으면 → 무보정 pass-through (D-10)
    Step = (int)EStep.Grab;
    break;
}
```

**Implication:** Side/Bottom InspectionSequence 등록 + Shot 추가 + DatumConfigs 추가만으로 DatumPhase 가 동작. 별도 코드 추가 없음.

---

### `[Obsolete]` 어트리뷰트 적용 (D-05)

**Analog (코드베이스 내 사용처):** **없음** — 분석 결과 `[Obsolete]` 어트리뷰트는 현재 코드베이스에서 사용되지 않음. 신규 패턴.

**적용 위치 (D-05 결정):**
- `Sequence_Top.cs:41` — `public class TopSequence : SequenceBase {` 위
- `Sequence_Bottom.cs:89` — `public class BottomSequence : SequenceBase {` 위
- `Action_TopInspection.cs:194` — `public class TopInspectionAction : ActionBase {` 위
- `Action_BottomInspection.cs:283` — `public class BottomInspectionAction : ActionBase {` 위

**적용 형식 (D-05):**
```csharp
[System.Obsolete("Phase 33 — InspectionSequence/Action_FAIMeasurement 로 마이그레이션됨", false)]
public class TopSequence : SequenceBase {
    ...
}
```

**Why `false`:** Warning (CS0618) 만 발생, Error 아님 → 컴파일 차단 없이 차후 정리 안내.

---

## Shared Patterns

### `//YYMMDD hbk` 주석 + Phase 번호 (CLAUDE.md memory + Phase 20 D-12)

**Source:** `InspectionSequence.cs:1-2`, `Action_FAIMeasurement.cs:29`, 전체 파일
**Apply to:** Phase 33 의 모든 코드 변경 — 추가/수정 라인에 반드시 `//260526 hbk Phase 33 — <reason>` 형식 마커.

**예시 (`Action_FAIMeasurement.cs:178-179`):**
```csharp
consumer.DatumDetectedCircleRow = dc.DetectedCircleRow; //260521 hbk Phase 32 — E2 CompoundAngle 원중심 주입
consumer.DatumDetectedCircleCol = dc.DetectedCircleCol; //260521 hbk Phase 32
```

**Stacking 패턴 (Phase 20 D-12):** 기존 마커는 보존하고, 추가 마커는 다음 줄 또는 위 줄에 누적. Re-marking 금지.

---

### SequenceBase 생성자 시그니처 표준 (변경 없음)

**Source:** `InspectionSequence.cs:54`, `Sequence_Top.cs:48`, `Sequence_Bottom.cs:96`

3 시퀀스 모두 **byte-identical 시그니처**:
```csharp
public <SeqClass>(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name)
```
→ `RegisterSequences()` L30-34 의 5 args 패턴 그대로 사용. **추가 인자 없음** (planner 가 새 시그니처 발명 시 거부 사유).

---

### CameraMasterParam vs InspectionMasterParam 관계

**Source:** `InspectionMasterParam.cs:12` + `CameraMasterParam.cs:16`

**핵심 사실:**
- `InspectionMasterParam : CameraMasterParam` — 상속 (`InspectionMasterParam.cs:12`)
- 추가 필드 = `DisplayName` (Fixture identity) 만 (`InspectionMasterParam.cs:22-33`)
- `Device|Camera` / `Device|Light` 카테고리는 `CameraMasterParam` 에서 그대로 상속 — Side/Bottom 의 카메라/조명 설정 동작 변경 0
- `OnCreate` (`InspectionSequence.cs:97-111`) 에서 `pMyParam.LightGroupName = DefaultLight; pMyParam.DeviceName = DefaultCamera;` 호출 → Side/Bottom 도 자동 적용

**Implication for Phase 33:** Param 클래스 변경 불필요. `new InspectionSequence(...)` 한 줄 교체로 Side/Bottom 도 `InspectionMasterParam` 자동 사용.

---

### Logging 패턴

**Source:** `Action_FAIMeasurement.cs:84`, `:92`, `:206`

```csharp
Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum failed: " + datumError);
```

- `ELogType` 캐스팅 필수 (CLAUDE.md naming/Logging convention)
- `[ComponentName]` prefix in message
- Apply to: 모든 신규 로깅 in Phase 33

---

### `ResolveFixtureSequence` Top 하드코딩 — 잠재적 회귀 위험

**Source:** `InspectionRecipeManager.cs:72-79`

D-06 은 InspectionRecipeManager 의 **신규 분기 추가 (D-04) 외** 변경 금지. 그러나 `ResolveFixtureSequence()` 가 `ESequence.Top` 만 반환 → Side/Bottom 의 DatumConfigs 가 INI Save/Load 누락.

**Planner 결정 사항 (Open):**
1. (A) `Save`/`LoadPhase6Format` 시그니처에 `ESequence` 인자 추가 — Top byte-identical 호출 사이트 보존 (default = Top)
2. (B) RecipeManager 인스턴스를 시퀀스별로 분리 — `SequenceHandler.RecipeManager` 가 `Dictionary<ESequence, InspectionRecipeManager>` 로 확장
3. (C) `ResolveFixtureSequence(ESequence seqId)` overload 추가 — 기존 zero-arg overload 는 Top 폴백

Phase 33 planner 가 D-06 와의 충돌을 명시적으로 결정해야 함. **Researcher 가 조사 권장.**

---

## No Analog Found

(없음 — 모든 변경 대상이 기존 패턴의 재사용 또는 좁은 범위 신규 패턴)

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| — | — | — | 모든 대상이 동일 코드베이스의 Top 경로와 패턴 일치 |

---

## Bottom Multi-Die 도메인 보존 매트릭스 (D-02)

Action_BottomInspection.cs 의 보존 가치가 있는 도메인 로직:

| 필드 / 메서드 | 위치 | 보존 전략 (D-02) |
|---|---|---|
| `BottomDieInfo` 클래스 | `Action_BottomInspection.cs:22-34` | **유지** — FAIResults[] 매핑 시 변환 source 데이터로 사용 (D-03) |
| `BottomInspectionContext.BottomDie Dictionary` | `Action_BottomInspection.cs:66` | **이연** — Action_FAIMeasurement 의 ShotConfig.FAIList 로 매핑 (D-07) |
| Multi-Die 검사 흐름 (`RunAlgorithm` per ImageGrabIndex) | `Action_BottomInspection.cs:349-376` (EStep.Grab) | **재해석** — 1 Die = 1 FAI 매핑 (D-03), 단일 Action_FAIMeasurement 내 FAI 루프로 변환 |
| `Picker_X/Y` 정보 | `Sequence_Bottom.cs:23-24`, `Action_BottomInspection.cs:51-52` | **metadata 보존** — D-02 명시: FAI Context 의 metadata (구체 매핑 = planner 결정) |
| Calibration (`CalBase`, `Cal_Xbase/Ybase`) | `Action_BottomInspection.cs:141-142`, `Sequence_Bottom.cs:26-27` | **별도 Action 으로 분리** (D-02) — `Action_BottomCalibration` (신설) — InspectionSequence 흐름과 독립 |
| `DebugCheck` (소프트웨어 vs 하드웨어 트리거) | `Action_BottomInspection.cs:147` | **이연/제거** — InspectionSequence 의 EStep.Grab (L106-133) 이 SIMUL_MODE + GrabHalconImage 패턴 사용 → DebugCheck 분기는 Phase 33 범위 밖, v1.2 HW 통합 시 재검토 |

---

## Verification — D-06 Top 회귀 방지 가드 매트릭스

| 파일 | 변경 허용 여부 | 변경 가능 부분 |
|---|---|---|
| `InspectionSequence.cs` | **변경 금지** | 0 lines |
| `Action_FAIMeasurement.cs` | **변경 금지** | 0 lines |
| `InspectionRecipeManager.cs` | **부분 허용** | `LoadPhase6Format` 의 레거시 INI 분기 (D-04) — 신규 형식 path 는 byte-identical |
| `SequenceHandler.cs` | **변경 허용** | RegisterSequences L30-34, RegisterActions L37-43, InitializeSequences L52-58 |
| `Sequence_Top.cs` | **Obsolete 어트리뷰트만** (D-05) | class 선언 위 한 줄 추가 |
| `Sequence_Bottom.cs` | **Obsolete 어트리뷰트만** (D-05) | class 선언 위 한 줄 추가 |
| `Action_TopInspection.cs` | **Obsolete 어트리뷰트만** (D-05) | class 선언 위 한 줄 추가 |
| `Action_BottomInspection.cs` | **Obsolete 어트리뷰트만** (D-05) | class 선언 위 한 줄 추가 |
| `VisionResponsePacket.cs` | **D-03 결정 따라** | `FAIResultData` X/Y/Angle 필드 확장 (planner 결정) |

---

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/Custom/Sequence/Top/`, `WPF_Example/Custom/Sequence/Bottom/`, `WPF_Example/Sequence/Param/`, `WPF_Example/Custom/Define/`, `WPF_Example/TcpServer/`
**Files scanned:** 9 (소스 6 + 정의 1 + 응답 패킷 1 + 컨텍스트 3)
**Pattern extraction date:** 2026-05-26
**Read-only constraint observed:** Yes — PATTERNS.md 만 작성. 소스 코드 무변경.
