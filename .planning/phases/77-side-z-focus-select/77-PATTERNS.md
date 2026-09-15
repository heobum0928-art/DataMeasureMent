# Phase 77: SIDE Z 범위 자동 초점 선택 — Pattern Map

**Mapped:** 2026-09-15
**Files analyzed:** 9 (기존 파일 확장, 신규 .cs 없음)
**Analogs found:** 9 / 9 (전부 Phase 68 크로스-Z 듀얼이미지 패턴 또는 그 인접 코드가 직접 분석)

이 phase 는 RESEARCH.md 가 이미 파일:줄 단위로 분석을 완료했다 — 신규 라이브러리·신규 파일 없이
Phase 68 크로스-Z 듀얼이미지 패턴(`DualImageEdgeDistanceMeasurement.ZIndexA/B`,
`InspectionSequence.m_dicCrossZImages`, `Action_FAIMeasurement` 크로스-Z 게이트/실행)을 Shot 단위 N-후보로
일반화하는 작업이다. 아래는 그 분석을 "새 파일마다 어느 analog 를 베끼는가" 형태로 재정리한 것이다.

## File Classification

| 수정 대상 파일 | Role | Data Flow | Closest Analog(같은 파일 내 인접 코드) | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | model (recipe config) | CRUD (INI load/save) | 같은 파일의 기존 `ZIndex` 프로퍼티(:176-182) + `DatumConfig.ZIndexA/B`/`Load()` 하위호환(`DatumConfig.cs:1337-1366`) | exact |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | service (sequence orchestration) | event-driven (PLC tick 라우팅) + in-memory store | 같은 파일의 `FindShotByZIndex`(:941-987), `FindActionIndicesByZIndex`(:1033), `m_dicCrossZImages`/`StoreCrossZImage`/`TakeCrossZImageCopy`/`ClearCrossZImages`(:1463-1562) | exact |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | controller (action step 실행) | event-driven + batch(후보 반복 실행) | 같은 파일의 `TryExecuteCrossZMeasurement`(:2019-2054), `ResolveCrossZGate`(:884-889), `IsCrossZIndexPairMisconfigured`(:1726-1749), `MarkMeasurementCrossZIncomplete`(:1794-1820) | exact |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` | utility (Halcon 알고리즘 래퍼) | transform (이미지→측정값) | 같은 파일의 `TryFitLine`(:20-62), `AppendStrip`(:319-374, `bPickStrongest`/`FindStrongestEdgeIndex` :298-317) | exact |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` | model (측정 결과 부수효과 필드) | CRUD | 같은 파일의 `LastMeasuredValue`/`LastJudgement`(:96-116), `_copyExclude`(:212-217) | exact |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` | model (구체 측정 타입) | transform | 같은 파일의 `TryExecute`→`TryFitLine` 호출부(:117-131) | exact |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs` | model (구체 측정 타입) | transform | `EdgeToLineDistanceMeasurement.cs` 의 동일 호출부(각도용 파생) | role-match |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs` | utility (CSV 직렬화) | file-I/O (append-only) | 같은 파일의 `CSV_HEADER`(:24), `BuildLine`(:95-117), 기존 `검사구분` trailing 컬럼 선례 | exact |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs` | utility (CSV 파싱) | file-I/O | 같은 파일의 `COL_RUNMODE` 옵션 컬럼 가드(:122, 635-650) | exact |

## Pattern Assignments

### `ShotConfig.cs` — `ZIndexStart`/`ZIndexEnd`/`ZFocusTieBreakPercent` 신설

**Analog:** 같은 파일의 기존 `ZIndex` 프로퍼티(`:176-182`) + `DatumConfig.cs:1337-1366`(`Load()` 하위호환 override)

**Core pattern (PropertyGrid 자동 노출, RESEARCH.md §Code Examples 4 그대로 인용):**
```csharp
[Category("Shot|Identity")]
[System.ComponentModel.Description("Z 범위 시작(포함). 0=범위 기능 꺼짐(기존 동작과 동일).")]
public int ZIndexStart { get; set; } = 0;

[Category("Shot|Identity")]
[System.ComponentModel.Description("Z 범위 끝(포함, 완성 tick).")]
public int ZIndexEnd { get; set; } = 0;

[PropertyTools.DataAnnotations.Browsable(false)]
public bool IsZRangeEnabled
{
    get { return ZIndexStart > 0 && ZIndexEnd > 0 && ZIndexEnd > ZIndexStart; }
}
```
**주의:** 위 `get` 은 `&&` 세 개를 한 줄에 늘어놓고 있다 — CLAUDE.md 가독성 규칙(조건 3개 이상은 이름 있는
`bool` 로 선추출)을 위반한다. 실행 시 그대로 베끼지 말고 다음처럼 분해할 것:
```csharp
public bool IsZRangeEnabled
{
    get
    {
        bool bStartSet = ZIndexStart > 0;
        bool bEndSet = ZIndexEnd > 0;
        bool bRangeOrdered = ZIndexEnd > ZIndexStart;
        return bStartSet && bEndSet && bRangeOrdered;
    }
}
```

**Load() 하위호환 패턴(그대로 사용 가능, 신규 침습 없음):**
```csharp
// DatumConfig.cs:1337-1366 과 동일 취지 — ZIndexStart/ZIndexEnd 는 reflection 기본값 0 이
// 이미 "꺼짐" 선언 기본값과 같으므로 실질 override 불필요. 문서화 목적의 짧은 override 만 남긴다.
public override bool Load(IniFile loadFile, string groupName)
{
    bool result = base.Load(loadFile, groupName);
    return result;
}
```

---

### `InspectionSequence.cs` — Z 범위 매칭 3패스 + 저장소

**Analog:** 같은 파일의 `FindShotByZIndex`(:941-987, 1/2패스 구조), `m_dicCrossZImages` lifecycle(:1463-1562)

**3패스 게이트 (Pattern 1, RESEARCH.md 원문 인용, :178-212):**
```csharp
private bool DoesShotOwnZRange(ShotConfig shot, int nZIndex)
{
    if (shot == null) { return false; }
    if (!shot.IsZRangeEnabled) { return false; }        // 0/0(꺼짐) → 항상 false, 회귀 0
    return nZIndex >= shot.ZIndexStart && nZIndex <= shot.ZIndexEnd;
}
```
`FindShotByZIndex`/`FindActionIndicesByZIndex` 에 3번째 폴백 패스로 이 함수를 추가한다
(`InspectionSequence.cs:941`, `:1033`). `ComputeLastZIndex`/`MaxCrossZCompletionZIndex`(:831, 865)와
`BuildDeclaredZIndexSet`(:1742 부근)도 범위 내 z 를 반영하도록 확장 필요 — RESEARCH.md §Pattern 1 표 참조.

**저장소 (Pattern 2, `m_dicCrossZImages` 와 병렬, 별도 사전 신설 — 재활용 금지):**
```csharp
private readonly Dictionary<string, HImage> m_dicZRangeImages = new Dictionary<string, HImage>();

private static string BuildZRangeKey(string szShotName, int nZIndex)
{
    return szShotName + "|z" + nZIndex;
}

public void StoreZRangeImage(string szShotName, int nZIndex, HImage image)
{
    lock (_crossZImageLock) // 기존 락 재사용
    {
        string szKey = BuildZRangeKey(szShotName, nZIndex);
        HImage existing;
        bool bHasExisting = m_dicZRangeImages.TryGetValue(szKey, out existing);
        if (bHasExisting && existing != null) { existing.Dispose(); }
        if (image != null) { m_dicZRangeImages[szKey] = image.CopyImage(); }
        else { m_dicZRangeImages[szKey] = null; }
    }
}
```
`ClearZRangeImages()` 를 `ClearCrossZImages()` 의 4개 호출부(`BeginCrossZImageCycle():1551`,
`ClearCrossZImagesAfterBatchCycle():1560`, `$RESET` 경로 `:1574` 인근, `$PREP` z=0 tick `:475` 인근)에
나란히 추가한다. **완성 tick 평가 직후 즉시 Dispose** — 2026-08-06 배치 메모리 폭증 사고(HALCON mimalloc)
전례가 있으므로 "평가 후 즉시 비우기" 원칙을 반드시 지킬 것.

---

### `Action_FAIMeasurement.cs` — 완성 게이트 + 후보별 실행·선택

**Analog:** 같은 파일의 `ResolveCrossZGate`(:884-889), `TryExecuteCrossZMeasurement`(:2019-2054),
`IsCrossZIndexPairMisconfigured`(:1726-1749)

**완성 게이트(Pattern 3, RESEARCH.md 원문):**
```csharp
bool bZRangeShot = ShotParam != null && ShotParam.IsZRangeEnabled;
if (bZRangeShot)
{
    bool bProtocolCycle = parentSeq2 != null && parentSeq2.IsProtocolDrivenCycle();
    int nCurZ = parentSeq2 != null ? parentSeq2.GetExecutionZIndex() : 0;
    bool bIsCompletionTick = nCurZ == ShotParam.ZIndexEnd;
    if (bProtocolCycle && !bIsCompletionTick)
    {
        return; // 보류 — MarkMeasurementCrossZIncomplete 와 동일 로그 패턴으로 Pending 표시
    }
    if (!bProtocolCycle)
    {
        // O-6: 수동 RUN — 누적 후보 있으면 사용, 없으면 기존 ShotParam.GetImage() 단일 경로 폴백
    }
}
```
**주의:** `int nCurZ = ... ? ... : 0;` 은 삼항 연산자다 — CLAUDE.md 위반. 실행 시 다음으로 치환:
```csharp
int nCurZ = 0;
if (parentSeq2 != null) { nCurZ = parentSeq2.GetExecutionZIndex(); }
```

**후보별 실행·선택(Pattern 4, RESEARCH.md §Code Examples 4 — 골격, A2 미완성 TODO 포함):**
```csharp
for (int nZ = shot.ZIndexStart; nZ <= shot.ZIndexEnd; nZ++)
{
    if (!parentSeq2.HasZRangeImage(shot.ShotName, nZ)) { continue; } // O-4: 중간 z 누락 허용
    using (HImage candidate = parentSeq2.TakeZRangeImageCopy(shot.ShotName, nZ))
    {
        if (candidate == null) { continue; }
        bool ok = meas.TryExecute(candidate, transform, pixRes, out v, out e, out ov);
        double dScore = 0.0;
        if (ok) { dScore = GetLastFitScore(meas); }
        // ... 최고점수 추적, 로그 [ALGO] z-select ...
    }
}
```
**계획 단계 필수 확정 사항(RESEARCH.md Assumptions A2):** 동점 규칙(O-3)으로 기준 Z 가 선택되면 **기준 Z
자신의 실행 결과(overlay 포함)를 별도 보관**해서 재사용해야 한다 — 위 골격은 최고점수 후보 결과만 보관하는
버전이라 이 분기가 비어 있다. 실행 담당 에이전트가 반드시 채워야 할 TODO.

**오설정 가드(§Pitfall 4):** `ZIndexEnd < ZIndexStart` 등 명백한 오설정은 `DatumConfig.WarnDatumZIndexChanged()`
(`DatumConfig.cs:266-293`) 패턴처럼 PropertyGrid 세터에서 즉시 `ELogType.Error` 경고할 것 — 조용한 "꺼짐" 흡수 금지.

---

### `VisionAlgorithmService.cs` — `TryFitLine`/`AppendStrip` 점수 out 확장

**Analog:** 같은 파일 자체(`TryFitLine:20-62`, `AppendStrip:319-374`, `FindStrongestEdgeIndex:298-317`)

opt-in `EdgeStrengthScore score = null` 파라미터를 시그니처 끝에 추가(기존 15개 호출부 전부 무변경).
`AppendStrip` 에 `out double stripAmp` 추가. RESEARCH.md §Code Examples 2 전문 인용 참조.

**주의(§Pitfall 1, A1 — 계획에서 확정 필요):** `EdgeSelection="All"`(SIDE 다수 측정 기본값)일 때 strip 당
여러 에지가 나온다 — "그 strip 의 amp" 를 **strip 내 `|amp|` 평균**으로 정의할 것을 RESEARCH.md 가 권장.
Strongest 는 채택된 에지의 `|amp|` 그대로, First/Last 는 `amp[0]`.

**주의(라인 270 기존 코드 인용, 삼항 사용 확인됨):**
```csharp
// VisionAlgorithmService.cs:270 (기존 코드, CLAUDE.md 제정 이전)
HTuple key = scanHorizontal ? rows : cols;
```
이 라인은 CLAUDE.md 삼항 금지 규칙 위반 상태로 이미 존재한다 — **기존 라인은 이번 phase 범위가 아니면
건드리지 말 것**(불필요한 diff 확대 방지). 단, 이 라인을 analog 로 복사해 새 코드를 작성할 때는 삼항을
쓰지 말고 `if/else` 로 풀어써야 한다(신규/수정 코드는 CLAUDE.md 규칙 적용 대상).

---

### `MeasurementBase.cs` — `LastFitScore`/`LastSelectedZIndex`/`LastSelectedZScore`

**Analog:** 같은 파일의 `LastMeasuredValue`/`LastJudgement`(:96-116), `_copyExclude`(:212-217)

```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public double LastFitScore { get; set; }

[PropertyTools.DataAnnotations.Browsable(false)]
public int LastSelectedZIndex { get; set; } = -1;   // -1 = 범위 미적용

[PropertyTools.DataAnnotations.Browsable(false)]
public double LastSelectedZScore { get; set; }
```
`_copyExclude` 에 위 3개 이름 추가(Copy/Paste 시 실행결과 오염 방지). **Anti-Pattern 경고:** `TryExecute`
추상 시그니처에 필수 `out` 파라미터를 추가하지 말 것 — 15개 파생 타입 전부 손대야 한다. 부수효과
프로퍼티로만 노출.

---

### `EdgeToLineDistanceMeasurement.cs` / `EdgeToLineAngleMeasurement.cs` — 점수 수신

**Analog:** 같은 파일의 `TryExecute`→`TryFitLine` 호출부(`EdgeToLineDistanceMeasurement.cs:117-131`)

```csharp
var score = new EdgeStrengthScore();
score.SetDenominator(EdgeSampleCount);
if (!svc.TryFitLine(image, /* ... 기존 인자 ... */, score))
{
    LastFitScore = 0.0;
    return false;
}
LastFitScore = score.Average;
```
`EdgeToLineAngleMeasurement.cs` 는 동일 호출부를 각도 계산용으로 미러링(role-match, 구조 동일).

---

### `MeasurementHistoryCsvWriter.cs` / `MeasurementHistoryCsvLoader.cs` — trailing 컬럼

**Analog:** 같은 파일들의 기존 `검사구분` trailing 컬럼 선례, `COL_RUNMODE` 옵션 가드(`Loader.cs:122, 635-650`)

Writer: `CSV_HEADER`(:24) 맨 뒤에 `SelectedZIndex,SelectedZScore` 추가, `BuildLine`(:95-117) 필드 리스트
끝에 `meas.LastSelectedZIndex`/`meas.LastSelectedZScore` append.
Loader: `COLUMN_COUNT` 불변 유지, `bool bHasColumn = fields.Count > COL_SELECTED_Z;` 가드로 옵션 파싱(기존
15/16컬럼 미만 옛 CSV 파일도 정상 파싱되도록).

## Shared Patterns

### 크로스-Z 저장소/게이트 패턴 (Phase 68) — 이 phase 전체의 뼈대
**Source:** `InspectionSequence.cs:1463-1562`(저장소), `Action_FAIMeasurement.cs:740-889, 1981-2054`(게이트/실행)
**Apply to:** `InspectionSequence.cs`(저장소 신설), `Action_FAIMeasurement.cs`(게이트/후보 반복)
**핵심:** 키 스킴이 다르므로(측정명+역할 vs Shot명+z) **저장소는 별도 사전으로 신설**하고 락(`_crossZImageLock`)만
재사용한다. `m_dicCrossZImages` 자체를 재활용하지 말 것(Anti-Pattern).

### INI 하위호환 "키 없으면 안전 기본값" 패턴
**Source:** `DatumConfig.cs:1337-1366`(ZIndexA/B), `MeasurementBase.cs:184-206`(MeasCorrectionFactor)
**Apply to:** `ShotConfig.cs` — `ZIndexStart/ZIndexEnd` 는 선언 기본값 0 이 이미 "꺼짐"과 일치하므로 진짜
no-op override 로 충분.

### 오설정 즉시 경고 패턴
**Source:** `DatumConfig.WarnDatumZIndexChanged()` (`DatumConfig.cs:266-293`)
**Apply to:** `ShotConfig.cs`(`ZIndexEnd < ZIndexStart` 등), `Action_FAIMeasurement.cs`(`IsCrossZIndexPairMisconfigured`
동형 함수 신설)

### 부수효과 프로퍼티로 측정 결과 노출 (기존 계약 변경 회피)
**Source:** `MeasurementBase.cs:96-116`(`LastMeasuredValue`/`LastJudgement`)
**Apply to:** `MeasurementBase.cs`(`LastFitScore` 등 3종), `EdgeToLineDistanceMeasurement.cs`/`EdgeToLineAngleMeasurement.cs`

## No Analog Found

없음 — 전 파일이 Phase 68 크로스-Z 패턴 또는 같은 파일 내 기존 코드로 커버됨.

## CLAUDE.md 가독성 규칙 위반 주의 (analog 원문 그대로 복사 금지)

| 위치 | 위반 유형 | 대체 방법 |
|---|---|---|
| RESEARCH.md §Pattern 4 예시 `IsZRangeEnabled` getter (`ZIndexStart > 0 && ZIndexEnd > 0 && ZIndexEnd > ZIndexStart`) | `&&` 3개 이상 한 줄 | 이름 있는 `bool bStartSet/bEndSet/bRangeOrdered` 로 선추출(본 문서 위 예시 참조) |
| RESEARCH.md §Pattern 3 의사코드 `int nCurZ = parentSeq2 != null ? parentSeq2.GetExecutionZIndex() : 0;` | 삼항 `?:` | `if (parentSeq2 != null) { nCurZ = ...; }` 로 분해 |
| `VisionAlgorithmService.cs:270` (기존 코드, analog 로 인용됨) `HTuple key = scanHorizontal ? rows : cols;` | 삼항 `?:` (기존 라인, 건드리지 않음) | 새 코드 작성 시 이 라인을 그대로 베끼지 말 것 — `if/else` 로 새로 작성 |
| RESEARCH.md §Code Examples 2 `EdgeStrengthScore.Average` getter `_nDenominator > 0 ? ... : 0.0` | 삼항 `?:` | `if (_nDenominator > 0) { return ...; } return 0.0;` |

이 외 인용된 코드에서 `??`/`?.`/switch 식/`hbk` 날짜주석은 발견되지 않았다.

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/Halcon/Algorithms/`
(RESEARCH.md 가 이미 전수 조사·인용 완료, 본 세션은 CLAUDE.md 가독성 규칙 위반 여부만 교차검증)
**Files scanned:** RESEARCH.md 인용 9개 파일 + CLAUDE.md 규칙 대조용 grep 2건
**Pattern extraction date:** 2026-09-15
