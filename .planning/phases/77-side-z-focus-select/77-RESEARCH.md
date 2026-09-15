# Phase 77: SIDE Z 범위 자동 초점 선택 — 연구 (RESEARCH)

**Researched:** 2026-09-15
**Domain:** 기존 코드베이스 확장 (신규 라이브러리 없음) — HALCON 24.11 measure_pos 기반 에지 강도 점수 + Shot 단위 Z 범위 이미지 누적 + 측정별 최선 Z 선택
**Confidence:** HIGH (전 항목 코드 근거 확보, 신규 외부 지식 불필요)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### 확정 결정 (LOCKED)

| ID | 결정 |
|---|---|
| D-77-01 | PLC 흐름: Z 높이마다 **새 z 번호로 `$PREP`/`$TEST`** 전송. 앱은 Z 축을 움직이지 않는다(기존 프로토콜 불변). |
| D-77-02 | 선명도 점수 = 측정이 이미 계산하는 `measure_pos` 에지 강도(\|amp\|) 평균. sobel_amp 별도 계산 안 함. |
| D-77-03 | 설정 단위 = **Shot 단위 Z 범위**(ZIndexStart/ZIndexEnd) + 그 Shot 의 **측정(ROI)마다 자동 선택**. |
| D-77-04 | 사용자 원문: "같은 shot 에서 ROI 내 에지들의 평균값으로 가장 강한 에지로 선택" |

점수 정의(CONTEXT.md 확정): 점수 = Σ(strip 별 \|amp\|) ÷ `EdgeSampleCount`. 에지 못 찾은 strip = 0.
strip 수가 모든 Z 에서 같으므로 흐린 영상에서 strip 이 빠져 평균이 오르는 함정이 없다. Strongest 가
아닌 선택(First/Last)도 **실제 채택된 에지의 \|amp\|** 를 쓴다. 후보 영상마다 측정을 실제로 돌리므로
**채택 Z 의 측정 결과를 그대로 사용**(재계산 없음).

### 열린 항목 — 계획 단계에서 결정(사용자 제안값, 진행 지시됨)

| # | 항목 | 제안값 | 본 연구의 채택 여부 |
|---|---|---|---|
| O-1 | z 번호 배정(PLC 제어팀 협의) | — | 범위 밖(연구 대상 아님, UAT 게이트) |
| O-2 | 설정 기본값·하위호환 | INI 키 없으면 0 → 범위 꺼짐 | **채택** — `ZIndexStart`/`ZIndexEnd` 기본 0/0 |
| O-3 | 동점 처리 | 최고점수 대비 기준 Z 점수차 N%(기본 3%) 이내 → 기준 Z 채택 | **채택** — Shot 파라미터 `ZFocusTieBreakPercent`(기본 3.0) 신설 제안 |
| O-4 | 측정 실행 시점 | 범위의 마지막 z 도착 시 1회 평가, 중간 z 누락 시 도착분으로 평가+경고 | **채택** — 크로스-Z 완성판정과 동일 패턴으로 구현 가능(§Architecture Patterns 3) |
| O-5 | 메모리 | 평가 직후 즉시 Dispose, 사이클 시작 시 비움 | **채택** — `m_dicCrossZImages` Dispose 정책 그대로 재사용 |
| O-6 | 수동 RUN/오프라인 | z=0 폴백 → 현재 영상 1장 | **채택** — `HasStaticDualImages` 폴백과 동형 패턴으로 구현 |
| O-7 | 기록·표시 | Algorithm 로그+CSV 선택Z·점수, 화면은 기준 Z 영상+오버레이 | **채택** — §Code Examples 3, 4 |
| O-8 | 사이클 타임 | 실측 필요 | 연구 불가(실기 필요) — §Common Pitfalls 5 |
| O-9 | XY 흔들림 | UAT 검증 | 연구 불가(실기 필요) — §성공 기준 3 |

### Deferred Ideas (OUT OF SCOPE)

CONTEXT.md 에 별도 "Deferred Ideas" 섹션 없음. 범위 밖으로 명시된 것: HALCON `depth_from_focus`(픽셀단위
선택, 이 목적엔 과함 — CONTEXT.md 참고조사), sobel_amp 별도 계산(D-77-02 로 명시 기각).

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | 설명 | 연구가 제공하는 근거 |
|----|------|----------------------|
| SZF-01 | Shot Z 범위 설정 | `ShotConfig.ZIndexStart/ZIndexEnd` 신설 설계 + `DatumConfig.ZIndexA/B`/`ParamBase.Load` 하위호환 패턴(§Code Examples 1) |
| SZF-02 | 범위 z 영상 누적 | `InspectionSequence.m_dicCrossZImages` 패턴을 Shot-keyed 저장소로 재사용(§Architecture Patterns 2) |
| SZF-03 | 측정별 강도 점수·선택 | `VisionAlgorithmService.TryFitLine`/`AppendStrip` 의 `amp` 확장 + 후보영상별 `TryExecute` 비교(§Code Examples 2, 3) |
| SZF-04 | 기록·표시 | `MeasurementBase` 신규 필드 → `CycleResultSerializer`/`MeasurementHistoryCsvWriter`/오버레이 라벨 확장(§Code Examples 4) |
| SZF-05 | 회귀 0 | 범위 꺼짐(0/0) 경로 무변경 보장 설계 — 모든 신규 분기가 `IsZRangeEnabled` 게이트 뒤에 위치(전 섹션) |

</phase_requirements>

---

## Summary

이 phase 는 신규 라이브러리가 필요 없다 — 순수하게 **기존 코드베이스의 두 선례를 조합**하는 작업이다.

1. **크로스-Z 듀얼이미지 패턴**(Phase 68, `DualImageEdgeDistanceMeasurement.ZIndexA/B` + `InspectionSequence.m_dicCrossZImages`)이
   "한 측정이 서로 다른 z 에서 찍은 여러 장을 모아서 쓴다"는 정확히 같은 문제를 이미 풀어냈다. 다만 그 패턴은
   **측정 단위**(역할 A/B 고정 2장)이고, Phase 77 은 **Shot 단위**(N 장, 가변 개수)이며 "여러 장을 합쳐 1번
   측정"이 아니라 "여러 장 각각 측정해서 제일 선명한 결과를 채택"이라는 점이 다르다. → 저장소 구조는 재사용하되
   Shot-keyed 로 단순화하고, 실행 로직은 새로 짠다.
2. **에지 강도(`amp`)는 이미 `AppendStrip`(`VisionAlgorithmService.cs:339-343`) 안에서 계산되고 있고 버려지고 있다.**
   `MeasurePos` 가 돌려주는 `amp` 튜플에서 strip 마다 채택된 에지의 `|amp|` 를 뽑아 합산하는 것은 최소 침습
   변경(기존 호출부 시그니처 무변경, opt-in out 파라미터 1개 추가)으로 가능하다.
3. **INI 하위호환·경고 UX·PropertyGrid 노출 패턴**은 `DatumConfig.ZIndexA/B`, `DatumConfig.DatumZIndex`,
   `MeasurementBase.MeasCorrectionFactor` 세 가지 선례가 이미 "키 없으면 안전 기본값" 문제를 반복적으로
   풀어냈다 — 이번에도 그 패턴을 그대로 복제하면 된다.

**Primary recommendation:** 신규 `.cs` 파일 없이 기존 9개 파일(`ShotConfig.cs`, `InspectionSequence.cs`,
`Action_FAIMeasurement.cs`, `VisionAlgorithmService.cs`, `MeasurementBase.cs`,
`EdgeToLineDistanceMeasurement.cs`, `EdgeToLineAngleMeasurement.cs`, `CycleResultSerializer.cs`,
`MeasurementHistoryCsvWriter.cs`/`MeasurementHistoryCsvLoader.cs`, `VersionDefine.cs`)를 확장한다.
Wave 1 은 **1 Shot × 1 측정(EdgeToLineDistance)** 만으로 end-to-end tracer 슬라이스를 완성해 설계 리스크를
가장 먼저 검증하고, 이후 Wave 에서 EdgeToLineAngle·기록·UI·회귀검증으로 확장한다.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Shot Z 범위 설정(ZIndexStart/End) | Recipe Model (`ShotConfig`) | PropertyGrid(반사 기반, 별도 XAML 불필요) | INI 직렬화 대상이라 모델 계층 소유. `ShotConfig` 는 이미 `[Category]` reflection 로 PropertyGrid 에 자동 노출(§Architecture Patterns 4) |
| z 소유권 판정(범위 매칭) | Sequence Orchestration (`InspectionSequence`) | — | `FindShotByZIndex`/`FindActionIndicesByZIndex`/`ComputeLastZIndex` 가 이미 이 책임을 지고 있다(크로스-Z 선례와 동일 계층) |
| 범위 영상 누적·수명관리 | Sequence Orchestration (`InspectionSequence`) | — | `m_dicCrossZImages` 와 동일 소유자 — 시퀀스 스레드가 사이클 경계를 알고 있어야 Dispose 시점을 정할 수 있다 |
| 측정별 점수 계산(에지 강도) | Vision Algorithm (`VisionAlgorithmService.TryFitLine`) | Measurement Model(`EdgeToLineDistance/Angle`) | HALCON `MeasurePos` 호출 지점이 유일한 `amp` 출처 — 알고리즘 계층에서 계산해 모델 계층 필드에 옮겨 담는다 |
| 후보 영상별 실행·비교·선택 | Action Orchestration (`Action_FAIMeasurement`) | — | `TryExecuteMeasurement`/`TryExecuteCrossZMeasurement` 와 동일 계층 — "이 tick 에 무엇을 실행할지" 판단은 Action 이 담당 |
| 기록(로그/CSV/JSON) | Action Orchestration + UI ViewModel(`CycleResultDto`) | — | 기존 `[ALGO]`/`[FitLine]` 로그와 `CycleResultSerializer`→CSV/xlsx 파이프라인 재사용 |
| 표시(오버레이/화면) | UI Display (`HalconDisplayService`, overlay 라벨) | — | `EdgeInspectionOverlay.MeasurementName` 라벨 확장만으로 충분 — 신규 렌더 경로 불필요 |

---

## Standard Stack

신규 외부 패키지 없음. 전부 기존 의존성(HALCON 24.11 `halcondotnet`, .NET 4.8, C# 7.2)만 사용한다.

### Package Legitimacy Audit

> 이 phase 는 외부 패키지를 설치하지 않는다 — 게이트 N/A.

| Package | Registry | Verdict | Disposition |
|---|---|---|---|
| (해당 없음) | — | — | — |

**Packages removed due to [SLOP] verdict:** 없음
**Packages flagged as suspicious [SUS]:** 없음

---

## Architecture Patterns

### System Architecture Diagram

```
PLC                          앱(이 PC, SIDE 시퀀스)
──────────────────────────────────────────────────────────────────────────
$PREP z=3 ──►│ ApplyPrepToSequence(z=3) ──► ApplyShotLights(FindShotByZIndex 확장) [조명만]
$TEST z=3 ──►│ ProcessTest ──► FindActionIndicesByZIndex(z=3) [범위 매칭 추가]
             │      └─► Action_FAIMeasurement.Run() : Init(버퍼 클리어)→Grab→Measure→End
             │            Grab: 이미지 촬영 → ShotParam.SetImage() [기존]
             │                              → parentSeq.StoreZRangeImage(shot,3,img) [신규]
             │            Measure: nCurZ(3) != ZIndexEnd(7) → 측정 보류(Pending), P/F 미확정
$PREP z=5 ──►│  ...
$TEST z=5 ──►│  ... (같은 Shot 재실행) → StoreZRangeImage(shot,5,img) → 여전히 z=5 != 7 → 보류
$PREP z=7 ──►│  ...
$TEST z=7 ──►│  ... → StoreZRangeImage(shot,7,img) → nCurZ(7) == ZIndexEnd(7) [완성 tick]
             │            └─► 각 측정(EdgeToLineDistance 등)마다:
             │                  후보 z ∈ {3,5,7}(존재하는 것만) 의 누적영상 각각에 대해
             │                    TryExecuteMeasurement(candidateImg) → (resultValue, score)
             │                  최고 score 채택(동점규칙 §O-3 적용) → 그 실행결과를 그대로 사용
             │                  → LastMeasuredValue/LastJudgement/LastSelectedZIndex/LastFitScore 기록
             │            End: 로그([ALGO] z-select 라인) + overlay 라벨("(z=5)") + 결과 CSV/JSON 반영
──────────────────────────────────────────────────────────────────────────
범위 꺼짐(ZIndexStart=ZIndexEnd=0)인 Shot/TOP/BOTTOM: 위 신규 분기 전부 미도달 — 기존 1-tick 경로 그대로.
```

### Recommended Project Structure

신규 폴더/파일 없음 — 아래 9개 기존 파일만 확장한다(신규 `.cs` 회피 제약 완전 충족, csproj 무변경).

```
WPF_Example/
├── Custom/Sequence/Inspection/
│   ├── ShotConfig.cs                         # ZIndexStart/ZIndexEnd/ZFocusTieBreakPercent + Load() 하위호환
│   ├── InspectionSequence.cs                 # m_dicZRangeImages + DoesShotOwnZRange + 완성판정 확장
│   ├── Action_FAIMeasurement.cs              # RunGrab 누적 훅 + Z선택 실행 루프 + 기록
│   ├── MeasurementBase.cs                    # LastFitScore/LastSelectedZIndex(+_copyExclude 추가)
│   ├── MeasurementHistoryCsvWriter.cs        # 선택Z/점수 trailing 컬럼(하위호환 append-only)
│   ├── MeasurementHistoryCsvLoader.cs        # 위 컬럼 옵션 파싱(fields.Count 가드)
│   └── Measurements/
│       ├── EdgeToLineDistanceMeasurement.cs  # TryFitLine score out 수신 → LastFitScore 대입
│       └── EdgeToLineAngleMeasurement.cs     # 동일
├── Halcon/Algorithms/
│   └── VisionAlgorithmService.cs             # TryFitLine 신규 opt-in out(EdgeStrengthScore) + AppendStrip amp 반환
├── UI/ViewModel/
│   └── CycleResultDto.cs (또는 CycleResultSerializer.cs)  # MeasurementResultDto.SelectedZIndex/Score
└── VersionDefine.cs                          # 1.7.46.0 → 1.7.47.0
```

### Pattern 1: Shot 소유권 판정을 "정확일치"에서 "범위 포함"으로 일반화

**What:** `InspectionSequence` 의 z 매칭 함수들은 이미 "own-ZIndex 정확일치 1패스 → 크로스-Z 폴백 2패스"
구조다(`FindShotByZIndex`, `FindActionIndicesByZIndex`). 여기에 **3번째 폴백**(범위 포함 판정)을 추가한다.

**When to use:** `ShotConfig.IsZRangeEnabled == true` 인 Shot 에 한해서만 3패스가 유효 매치를 낸다 — 나머지는
전부 기존 1/2패스에서 이미 걸러지므로 회귀 0.

**Example (근거 코드):**
```csharp
// Source: InspectionSequence.cs:941-987 (FindShotByZIndex, 기존 2패스 구조 — 그대로 인용)
private ShotConfig FindShotByZIndex(int nZIndex)
{
    var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
    if (recipeManager == null) { return null; }
    // 1패스: own-ZIndex 정확일치
    foreach (var shot in recipeManager.Shots) {
        if (shot == null) continue;
        bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
        bool bZMatch = shot.ZIndex == nZIndex;
        if (bOwnedByThisSeq && bZMatch) { return shot; }
    }
    // 2패스: 크로스-Z 폴백 (DoesShotOwnCrossZIndex)
    foreach (var shot in recipeManager.Shots) {
        if (shot == null) continue;
        if (shot.OwnerSequenceName != Name) continue;
        if (DoesShotOwnCrossZIndex(shot, nZIndex)) { return shot; }
    }
    // ── 77 신설: 3패스 — Z 범위 폴백
    foreach (var shot in recipeManager.Shots) {
        if (shot == null) continue;
        if (shot.OwnerSequenceName != Name) continue;
        if (DoesShotOwnZRange(shot, nZIndex)) { return shot; } // 신규 헬퍼
    }
    return null;
}

// 신규 헬퍼 — DoesShotOwnCrossZIndex(:992-1024)와 동일한 자리에 병렬로 추가
private bool DoesShotOwnZRange(ShotConfig shot, int nZIndex)
{
    if (shot == null) { return false; }
    if (!shot.IsZRangeEnabled) { return false; }        // 0/0(꺼짐) → 항상 false, 회귀 0
    return nZIndex >= shot.ZIndexStart && nZIndex <= shot.ZIndexEnd;
}
```

**적용 대상 함수 목록(수정 필요):**

| 함수 | 파일:줄 | 변경 |
|---|---|---|
| `FindShotByZIndex` | `InspectionSequence.cs:941` | 3패스 추가($PREP 조명 라우팅) |
| `FindActionIndicesByZIndex` | `InspectionSequence.cs:1033` | `bZRangeMatch = DoesShotOwnZRange(...)` OR 항 추가($TEST 라우팅) |
| `ComputeLastZIndex`/`MaxCrossZCompletionZIndex` | `InspectionSequence.cs:831,865` | Z-range 완성 index(=`shot.ZIndexEnd`)도 `Math.Max` 체인에 반영 — "마지막 Index" PLC ACK 오판정 방지 |
| `IsZIndexUsedByCrossZDatum`/`BuildDeclaredZIndexSet`(1742 부근) | `InspectionSequence.cs` | 범위 내 z 전부를 "선언된 z 집합"에 포함 — `WarnIfEmptyScope`류 오경고 방지 |
| `IsCrossZIndexPairMisconfigured` 대응 오설정 판정 | `Action_FAIMeasurement.cs:1726` | `ZIndexStart<=0 || ZIndexEnd<=0 || ZIndexEnd<ZIndexStart` → 오설정 NG(신규, §Common Pitfalls 4) |

**주의:** `DoesShotOwnCrossZIndex`(측정 단위, `FAIList`→`Measurements` 순회)와 달리 `DoesShotOwnZRange`는
**Shot 자체의 2개 필드만 비교**하는 O(1) 함수다 — 훨씬 저렴하다. 이는 D-77-03(Shot 단위 범위, 측정 단위
아님)의 직접적 결과이며 크로스-Z 보다 구현이 단순한 이유이기도 하다.

### Pattern 2: 범위 영상 누적 저장소 — Shot-keyed, 크로스-Z 저장소와 병렬

**What:** `m_dicCrossZImages`(`InspectionSequence.cs:73`, `Dictionary<string, HImage>`, `StoreCrossZImage`/
`TakeCrossZImageCopy`/`HasCrossZImage`/`ClearCrossZImages`, `:1463-1562`)는 **측정 역할(A/B) 별 키**를 쓴다
(`BuildCrossZMeasurementKey` = Shot명+측정명 + Suffix A/B). Phase 77 은 측정이 아니라 **Shot 이 여러 z 를
공유**하므로, 키를 `Shot명 + "|z" + zIndex` 로 단순화한 **별도 사전**을 신설하는 편이 크로스-Z 의미(2-role
고정)를 오염시키지 않는다.

**When to use:** Shot 의 `RunGrab()` 이 z 범위 내 임의의 z 에서 촬영할 때마다.

**Example:**
```csharp
// Source: InspectionSequence.cs:1463-1562 패턴을 그대로 미러링 (병렬 사전, 같은 lock 재사용 가능)
private readonly Dictionary<string, HImage> m_dicZRangeImages = new Dictionary<string, HImage>();

private static string BuildZRangeKey(string szShotName, int nZIndex)
{
    return szShotName + "|z" + nZIndex;
}

public void StoreZRangeImage(string szShotName, int nZIndex, HImage image)
{
    lock (_crossZImageLock) // 기존 락 재사용 — 별도 락 신설 시 데드락 표면적만 늘어난다
    {
        string szKey = BuildZRangeKey(szShotName, nZIndex);
        HImage existing;
        bool bHasExisting = m_dicZRangeImages.TryGetValue(szKey, out existing);
        if (bHasExisting && existing != null) { existing.Dispose(); }
        if (image != null) { m_dicZRangeImages[szKey] = image.CopyImage(); }
        else { m_dicZRangeImages[szKey] = null; }
    }
}

// TakeZRangeImageCopy / HasZRangeImage 는 TakeCrossZImageCopy(:1491)/HasCrossZImage(:1512) 와 동형 구현

private void ClearZRangeImages()
{
    lock (_crossZImageLock)
    {
        foreach (var kvp in m_dicZRangeImages) { if (kvp.Value != null) { kvp.Value.Dispose(); } }
        m_dicZRangeImages.Clear();
    }
}
```

**수명 연결 지점(4곳 — `ClearCrossZImages()` 호출부 전부에 나란히 추가):**

| 트리거 | 파일:줄 |
|---|---|
| `BeginCrossZImageCycle()` (z=0 사이클 시작) | `InspectionSequence.cs:1551` |
| `ClearCrossZImagesAfterBatchCycle()` (배치 완료 후 메모리 정리) | `InspectionSequence.cs:1560` |
| `$RESET` 처리 경로 | `InspectionSequence.cs:1574` 인근(`ResetSequenceCycleStates` 호출부) |
| (참고) `$PREP` z=0 tick 직후 | `InspectionSequence.cs:475` 인근 |

**메모리(O-5 근거 수치):** SIDE 16544×9200 Gray8 ≈ 152MB/장. 범위 4장 ≈ 610MB, **완성 tick 평가 직후
즉시 `ClearZRangeImages()` 또는 개별 `Dispose`** 해야 한다 — STATE.md 2026-08-06 "배치 메모리 폭증"
사고(HALCON mimalloc 이 해제 메모리를 OS 에 안 돌려주는 문제, `SetSystem('memory_allocator','system')`
로 완화됨, `WPF_Example/SystemHandler.cs` 참고)가 이미 한 번 재현된 전례가 있다 — 누적 저장소를
"평가 후 즉시 비우기" 원칙 없이 구현하면 같은 사고가 재발한다.

### Pattern 3: 완성 판정 = 크로스-Z `HalfPending`/`BothReady` 게이트의 Shot 버전

**What:** 크로스-Z 는 `ECrossZGate`(Misconfigured/NotMyTick/CaptureFailed/HalfPending/BothReady)로
"이 tick 에 측정을 실행할지"를 판정한다(`Action_FAIMeasurement.cs:740-817`, `ResolveCrossZGate:884-889`).
Z-range 는 **측정 단위가 아니라 Shot 단위**로 같은 질문을 한 번만 물으면 된다 — `MeasureShotFaiList`
진입 시점에 게이트 1회.

**Example:**
```csharp
// MeasureShotFaiList (Action_FAIMeasurement.cs:604 인근) 최상단에 삽입할 게이트 — 의사코드
bool bZRangeShot = ShotParam != null && ShotParam.IsZRangeEnabled;
if (bZRangeShot)
{
    bool bProtocolCycle = parentSeq2 != null && parentSeq2.IsProtocolDrivenCycle();
    int nCurZ = parentSeq2 != null ? parentSeq2.GetExecutionZIndex() : 0;
    bool bIsCompletionTick = nCurZ == ShotParam.ZIndexEnd;
    if (bProtocolCycle && !bIsCompletionTick)
    {
        // O-4: 마지막 z 아니면 보류 — MarkMeasurementCrossZIncomplete 와 동일한 "고장 아님" 로그로
        //  전 FAI/측정을 Pending 표시하고 return(측정 실행 없음). 판정(P/F)에 반영하지 않는다.
        return;
    }
    if (!bProtocolCycle)
    {
        // O-6: 수동 RUN/오프라인 — z 는 항상 0(HasStaticDualImages 폴백과 동형).
        //  누적된 후보가 있으면 그걸 쓰고, 없으면(수동 RUN 1장만 찍음) 기존 ShotParam.GetImage() 단일 경로로 폴백.
    }
}
// bZRangeShot==false 인 기존 Shot/TOP/BOTTOM 은 이 블록 전체를 건너뛴다 — 원본 코드와 100% 동일 경로(SZF-05).
```

**중간 z 누락 처리(O-4):** "마지막 z 도착 시 평가, 중간 누락은 도착분으로 평가 + 경고" — 완성 tick
도달 시 `TryGetZRangeImages` 가 `ZIndexStart..ZIndexEnd` 범위를 순회하며 **존재하는 후보만** 수집하고,
`ZIndexStart..ZIndexEnd` 길이보다 수집된 후보 수가 적으면 `Logging.PrintLog(ELogType.Error, ...)` 로
경고(크로스-Z 의 `MarkMeasurementCrossZIncomplete` 로그 문구 패턴 재사용, `Action_FAIMeasurement.cs:1794-1820`).
**측정 자체는 중단하지 않는다** — 있는 후보만으로 최선 선택(성공 기준 4 "빠진 z 가 있어도 사이클 완주").

### Pattern 4: PropertyGrid 노출 — 반사 기반, XAML/InspectionListView 수정 불필요

**What:** `ShotConfig`(`WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs`)의 기존 `ZIndex`
프로퍼티(`:182`)는 `[Category("...")]` 어트리뷰트만으로 PropertyGrid(PropertyTools.Wpf)에 자동 노출된다 —
별도 XAML 등록이나 `InspectionListView.xaml.cs` 수정이 없다. `ZIndexStart`/`ZIndexEnd`도 동일 패턴으로
추가하면 된다(신규 `.cs` 불필요 제약과 정확히 맞아떨어짐).

**Example:**
```csharp
// Source: ShotConfig.cs:176-182 (기존 ZIndex 선언부 바로 아래에 병렬 추가 권장)
[Category("Shot|Identity")]
[System.ComponentModel.Description("Z 범위 시작(포함). 0=범위 기능 꺼짐(기존 동작과 동일). 이 Shot 이 여러 z 에서 촬영되어야 할 때만 설정.")]
public int ZIndexStart { get; set; } = 0;

[Category("Shot|Identity")]
[System.ComponentModel.Description("Z 범위 끝(포함, 완성 tick). ZIndexStart 와 함께 설정해야 유효 — 하나만 설정하면 오설정으로 취급.")]
public int ZIndexEnd { get; set; } = 0;

[PropertyTools.DataAnnotations.Browsable(false)] // 계산 프로퍼티 — DatumConfig.IsDualImageShot(ShotConfig.cs:29) 와 동일 패턴
public bool IsZRangeEnabled
{
    get { return ZIndexStart > 0 && ZIndexEnd > 0 && ZIndexEnd > ZIndexStart; }
}

// 동점 규칙(O-3) — Shot 파라미터
[Category("Shot|Identity")]
[System.ComponentModel.Description("Z 선택 동점 허용폭(%). 최고 점수 Z 와 기준 Z(ZIndex) 점수 차이가 이 % 이내면 기준 Z 를 채택한다(반복성 우선). 0=항상 최고점수 채택.")]
public double ZFocusTieBreakPercent { get; set; } = 3.0;
```

### Anti-Patterns to Avoid

- **크로스-Z 저장소(`m_dicCrossZImages`)를 재활용해 Z-range 후보까지 같이 넣지 말 것.** 키 스킴이
  "측정명+역할" 대 "Shot명+z" 로 근본적으로 다르다 — 억지로 합치면 `BuildCrossZMeasurementKey` 를
  범위 케이스에서도 호출해야 해서 오히려 코드가 더 복잡해지고, 두 기능이 서로의 Dispose 시점을 침범할
  위험이 생긴다.
- **`RunGrab()` 의 `!ShotParam.HasImage` 가드를 건드리지 말 것.** `EStep.Init` 이 매 트리거(z tick)마다
  `ShotParam.ClearAllResults()`(이미지 버퍼 Dispose)를 호출하므로(`ShotConfig.cs:517-532` 주석,
  Action_FAIMeasurement 의 "Run 사이클 진입 시 매번" 계약) 이 가드는 **이미 매 z tick 마다 재촬영을
  허용한다** — 별도 우회 로직을 추가하면 오히려 기존 계약을 깨뜨린다.
- **점수 계산을 `TryExecute` 반환값(resultValue)이나 새 `out` 필수 파라미터로 만들지 말 것.**
  `MeasurementBase.TryExecute` 는 15개 파생 타입의 추상 시그니처다 — 필수 파라미터를 늘리면 13개
  무관 타입까지 전부 손대야 한다. `LastFitScore` 처럼 **부수효과 프로퍼티**(기존 `LastMeasuredValue`/
  `LastJudgement` 와 동일 패턴)로 노출하는 것이 D-06(§Common Pitfalls) 회피책이다.
- **`EdgeSelection="All"`(SIDE 다수 측정의 기본값, `EdgeToLineDistanceMeasurement.cs:44`)을 무시하고
  `Strongest` 전용으로 점수 로직을 짜지 말 것.** `AppendStrip`(`:319-374`) 은 `bPickStrongest` 가 false
  일 때 strip 에서 반환된 **모든** 에지를 그대로 누적한다 — "이 strip 이 채택한 에지" 가 1개가 아니므로
  점수 계산 시 별도 집계 규칙이 필요하다(§Common Pitfalls 1).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 에지 강도(초점) 측정 | 별도 Sobel/Laplacian/Tenengrad 초점 스코어 계산기 | `MeasurePos` 가 이미 반환하는 `amp` 튜플 | D-77-02 로 명시 확정 — 신규 HALCON 연산자 호출 자체가 범위 밖(sobel_amp 명시 기각). 추가 연산자 호출은 strip 수만큼 비용이 배가된다 |
| Z 후보 중 최선 선택 | 커스텀 focus-stacking/depth-from-focus 파이프라인 | 후보 영상마다 기존 측정 알고리즘(`TryFitLine`)을 그대로 재실행 후 점수 비교 | CONTEXT.md 참고조사: `depth_from_focus` 는 픽셀 단위 선택이라 "측정당 1장" 목적엔 과함. 이미 검증된 측정 로직을 재사용하면 새 실패 모드가 생기지 않는다 |
| 여러 z 이미지 수명관리 | 신규 캐시 클래스/컬렉션 프레임워크 | `Dictionary<string,HImage>` + clone-on-store(`m_dicCrossZImages` 패턴) | 이미 프로덕션에서 검증된 소유권 계약(clone 저장, 호출자 Dispose 책임) — HImage 이중 Dispose/누수 버그를 새로 만들 이유가 없다 |
| INI 키 부재 하위호환 | 신규 마이그레이션 프레임워크/버전 태그 | `ParamBase.Load` override + `sec.ContainsKey(...)` 가드(`DatumConfig.ZIndexA/B`, `MeasurementBase.MeasCorrectionFactor` 패턴) | 이 코드베이스에 이미 3개 이상의 검증된 선례가 있다 — 새 방식을 발명하면 리뷰어가 "왜 다르게 했나"부터 물어야 한다 |

**Key insight:** 이 phase 의 모든 "어려운 부분"은 이미 이 코드베이스 안에 답이 있다. 새로 발명해야 할
로직은 사실상 "범위 내 여러 후보에 대해 기존 로직을 반복 실행하고 점수로 정렬하는 20~30줄"뿐이다.

---

## Common Pitfalls

### Pitfall 1: `EdgeSelection="All"` 에서 "채택된 에지"가 모호하다
**What goes wrong:** `AppendStrip`(`VisionAlgorithmService.cs:319-374`) 은 `selection="All"`일 때
(`bPickStrongest==false`) MeasurePos 가 돌려준 **strip 당 여러 에지**를 그대로 `allRows/allCols` 에
누적한다. CONTEXT.md 의 점수 공식("Σ(strip 별 \|amp\|) ÷ EdgeSampleCount")은 **strip 당 값 1개**를
전제한다 — All 모드에서 strip 이 에지 3개를 반환하면 그 3개 중 무엇을 "그 strip 의 amp"로 볼지 정의가
없다.
**Why it happens:** `EdgeSelection="All"` 은 SIDE 레시피의 다수 항목 기본값이다(`EdgeToLineDistanceMeasurement.cs:44`
주석 "ROI 내 에지 분포 전체를 라인으로 피팅"). D-77-02/D-77-04("실제 채택된 에지의 amp")는 Strongest/First/Last
케이스만 명시적으로 다뤘다.
**How to avoid:** 계획 단계에서 명시적으로 정의할 것(권장: **strip 내 `|amp|` 평균**을 그 strip 의 기여값으로
채택 — "강한 에지 하나가 아니라 그 strip 구간 전체의 신뢰도"라는 의미로 Strongest/First/Last 와 개념적으로
가장 가깝다). `AppendStrip` 에 `out double stripAmpContribution` 을 추가해 4가지 selection 각각의 계산식을
명확히 분기시킬 것.
**Warning signs:** 점수가 strip 수와 무관하게 비정상적으로 크거나(다중 에지 합산 실수), All 선택 측정만
Z 선택이 항상 첫 후보로 고정되는 경우(점수 계산이 예외를 삼켜 0 을 반환하고 있을 가능성).

### Pitfall 2: `TryFitLine` 의 `datumTransform` 회전 보정이 strip 좌표를 매 후보마다 다시 계산한다
**What goes wrong:** `TryFitLine`(`VisionAlgorithmService.cs:47-62`)은 `datumTransform` 으로 ROI 좌표를
매 호출마다 재계산한다. Z 후보별로 같은 `transform`(Datum 은 별도 크로스-Z z 에서 1회만 검출됨, D-76-07 참고)을
N 번(후보 수만큼) 넘기게 되는데, **Datum 변환은 z 후보와 무관하게 고정값**이라는 전제가 깨지면(예: SIDE 부품이
Z 이동 중 미세하게 XY 밀림, O-9) 후보마다 다른 실제 위치의 ROI 를 비교하게 되어 점수 비교 자체가 무의미해진다.
**Why it happens:** 텔레센트릭 렌즈 가정("XY 흔들림 없음")이 검증되지 않았다(CONTEXT.md O-9, 성공 기준 3).
**How to avoid:** UAT 체크리스트에 "같은 Z 반복 촬영 시 선택 Z/측정값 흔들림" 뿐 아니라 "Z 처음/끝에서 같은
고정점(예: 패턴매칭 기준점) 좌표 비교"를 추가해 XY 드리프트를 정량 확인할 것.
**Warning signs:** 범위 켠 Shot 의 측정값이 범위 끈 것보다 반복성이 눈에 띄게 나쁨(성공 기준 2 "측정값 기준
동등" 미충족).

### Pitfall 3: PLC 가 범위 z 를 순서대로 안 보내거나 반복 전송할 때 완성 tick 재평가
**What goes wrong:** 크로스-Z 는 두 tick(A, B) 순서 무관 완성 판정이 이미 구현돼 있다(`HasCrossZImage(keyA) &&
HasCrossZImage(keyB)`). Z-range 는 "마지막 z(ZIndexEnd) 도착 = 완성"이 트리거이므로, **같은 z 를 PLC 가
두 번 보내면**(재시도/중복) `nCurZ == ZIndexEnd` 조건이 **매번** 참이 되어 측정이 중복 평가되고 결과가
매번 새 CSV/로그 행을 만든다.
**Why it happens:** 완성 판정을 "이번 tick == ZIndexEnd" 라는 stateless 비교로만 구현하면 재전송을 구분할
수단이 없다.
**How to avoid:** 계획 단계에서 "직전 사이클에서 이미 평가됨" 플래그(예: `ShotConfig` 에 per-cycle
`_bZRangeEvaluatedThisCycle`, `BeginCrossZImageCycle()` 트리거에서 리셋)를 둘지, 아니면 "여러 번 평가돼도
멱등(같은 후보 집합 → 같은 결과)이라 무해" 로 볼지 명시적으로 결정할 것. 후자를 택하면 문서화만으로 충분.
**Warning signs:** 결과 CSV 에 같은 사이클의 같은 측정 행이 중복 발생.

### Pitfall 4: 오설정(ZIndexStart 만 설정, End 없음 등) 조용한 폴백
**What goes wrong:** `IsCrossZIndexPairMisconfigured`(`Action_FAIMeasurement.cs:1726-1749`) 선례처럼 "하나만
설정"/"End < Start"/"존재하지 않는 z 참조" 를 명시 검사하지 않으면, `IsZRangeEnabled` 계산식(`ZIndexEnd >
ZIndexStart`)이 자동으로 "미설정"과 동치 취급해 **조용히 범위 기능이 꺼진 것처럼** 동작한다 — 사용자는
설정했다고 믿는데 실제로는 기존 1-tick 경로로 돈다.
**Why it happens:** `IsZRangeEnabled` 게터가 방어적으로 짜여 있어 오설정을 "꺼짐"으로 흡수한다(안전하지만
무성).
**How to avoid:** `DatumConfig.WarnDatumZIndexChanged()`(`:266-293`) 패턴처럼 **PropertyGrid 세터에서 즉시
경고**(`ZIndexEnd < ZIndexStart` 등 명백한 오설정만, 값 저장은 막지 않음)를 추가할 것. 최소한 로그 레벨
경고(`ELogType.Error`)는 반드시 필요.
**Warning signs:** 사용자가 "범위를 켰는데 효과가 없다"고 보고하는 케이스.

### Pitfall 5: 사이클 타임 — 측정 수 × Z 수만큼 `TryFitLine` 반복 (O-8)
**What goes wrong:** 완성 tick 에서 측정 1개당 후보 Z 수만큼 `TryExecute`(→`TryFitLine`→strip 루프)를
반복한다. SIDE C13-14 FAI 만 해도 6개 측정 × 후보 4개 Z = 24회 `TryFitLine`(strip 100개 가정 시 2400회
`MeasurePos` 호출) — 이 phase 가 25개 측정 전체(SIDE_1/2/3/4)에 적용되면 수십~백 단위로 배증한다.
**Why it happens:** "후보 영상마다 측정을 실제로 돌려 재계산 없이 그대로 채택"(D-77-02 확정)이 정확도
우선 설계이기 때문 — 사이클 타임은 트레이드오프로 이미 감수된 것이다.
**How to avoid:** 계획에서 **완성 tick 이후의 측정 실행 구간**만 Stopwatch 로 실측(`swMeasureTotal` 기존
계측 재사용, `Action_FAIMeasurement.cs:563` 패턴)해 Before/After 비교표를 UAT 증거로 남길 것. 범위를 전체
25개가 아니라 **초점 문제가 실제로 있는 측정에만 선택적으로 적용**(Shot 단위이므로 어차피 Shot 을 쪼개야
선택 적용 가능 — O-1 z 배정 협의와 직결)하는 것도 완화책으로 문서화.
**Warning signs:** 사이클 전체 tact 가 기존 대비 눈에 띄게 증가(정량 임계값은 실측 후 사용자와 합의 필요 —
ASSUMED 아님, 순수 미지수).

---

## Code Examples

### 1. `ShotConfig.Load()` 하위호환 — DatumConfig ZIndexA/B 패턴 그대로 복제

```csharp
// Source: DatumConfig.cs:1337-1366 (인용, 그대로 미러링)
// ParamBase.Load 는 INI 누락 Int32 키를 0 으로 덮어쓴다. ZIndexStart/ZIndexEnd 는 0 이 "꺼짐"이라는
//  선언 기본값과 동일하므로 실은 별도 override 가 필요 없다(DatumZIndex 의 -1 과 달리 0 이 이미 안전값).
//  단, 명시적으로 "키 부재 = 꺼짐"을 문서화하는 짧은 Load() override 를 남겨 향후 유지보수자가
//  ZIndexA/B 사례를 보고 불필요한 -1 폴백을 추가하지 않도록 한다(MeasurementBase.cs:184-206 주석과 동일 취지).
public override bool Load(IniFile loadFile, string groupName) {
    bool result = base.Load(loadFile, groupName);
    // ZIndexStart/ZIndexEnd: 키 부재 시 reflection 기본값 0 == 선언 기본값 0 → 별도 폴백 불필요(진짜 no-op).
    // (기존 Ring/Bar 채널 마이그레이션 블록 :384-419 바로 아래에 이어 붙일 것)
    return result;
}
```

### 2. `VisionAlgorithmService.TryFitLine` — opt-in 점수 out 파라미터 (기존 호출부 무변경)

```csharp
// Source: VisionAlgorithmService.cs:20-31 시그니처에 마지막 인자로 추가(기존 collectedEdges 패턴과 동일 위치)
public bool TryFitLine(
    HImage image,
    double roiRow, double roiCol, double roiPhi,
    double roiLength1, double roiLength2,
    HTuple datumTransform,
    int sampleCount, int trimCount, double sigma, int threshold,
    string direction, string polarity,
    out double row1, out double col1, out double row2, out double col2,
    out string error,
    string selection = "all",
    List<ValueTuple<double, double>> collectedEdges = null,
    EdgeStrengthScore score = null)   // ← 신규, opt-in(null=기존 호출부 전부 무변경, 계산 skip)
{
    // ... 기존 로직 동일 ...
    if (scanHorizontal) {
        for (int i = 0; i < stripCount; i++) {
            double stripAmp;   // 신규 out — AppendStrip 이 채운다(에지 없으면 0)
            EStripResult sr = AppendStrip(image, r1, left, r2, right, imageWidth, imageHeight,
                Math.Max(0.4, sigma), Math.Max(1, threshold), pol, measurePhi, measureSel, bPickStrongest,
                ref allRows, ref allCols, out stripAmp);   // AppendStrip 시그니처에 out double 1개 추가
            if (score != null) { score.AddStrip(stripAmp, sr == EStripResult.Ok); }
            // ... 기존 카운터 로직 동일 ...
        }
    }
    // (세로 스캔 루프도 동일하게 out stripAmp 반영)
    ...
}

// 신규 — 4개 selection(First/Last/All/Strongest) 별 "채택 에지 amp" 정의를 한 곳에 모은다.
public class EdgeStrengthScore
{
    private double _dSumAbsAmp;
    private int _nDenominator;   // = EdgeSampleCount(전달받은 stripCount), 못 찾은 strip 도 분모에 포함(D-77-02)

    public void SetDenominator(int nStripCount) { _nDenominator = nStripCount; }
    public void AddStrip(double dStripAmp, bool bOk) { _dSumAbsAmp += dStripAmp; } // NoEdge/Failed → stripAmp=0 이미 반영됨
    public double Average { get { return _nDenominator > 0 ? _dSumAbsAmp / _nDenominator : 0.0; } }
}
```

```csharp
// AppendStrip 내부 — Source: VisionAlgorithmService.cs:339-363 확장 지점
HTuple edgeRows, edgeCols, amp, dist;
HOperatorSet.MeasurePos(image, measureHandle, sigma, threshold, polarity, selection,
    out edgeRows, out edgeCols, out amp, out dist);
if (edgeRows.TupleLength() <= 0) { stripAmp = 0.0; return EStripResult.NoEdge; }
if (bPickStrongest) {
    int nStrongest = FindStrongestEdgeIndex(amp);           // 기존 함수 그대로(:298-317)
    if (nStrongest < 0) { stripAmp = 0.0; return EStripResult.NoEdge; }
    stripAmp = Math.Abs(amp[nStrongest].D);                 // Strongest: 채택 에지 그 자체
    HOperatorSet.TupleConcat(allRows, edgeRows[nStrongest], out allRows);
    HOperatorSet.TupleConcat(allCols, edgeCols[nStrongest], out allCols);
    return EStripResult.Ok;
}
// First/Last: MeasurePos 가 이미 단일 에지만 반환 → amp[0] 이 "채택된 에지"
// All: strip 내 여러 에지 → 평균 |amp| 를 그 strip 의 기여값으로 채택(§Common Pitfalls 1, 계획 확정 필요)
double dAbsSum = 0.0;
for (int k = 0; k < amp.TupleLength(); k++) { dAbsSum += Math.Abs(amp[k].D); }
stripAmp = amp.TupleLength() > 0 ? dAbsSum / amp.TupleLength() : 0.0;
HOperatorSet.TupleConcat(allRows, edgeRows, out allRows);
HOperatorSet.TupleConcat(allCols, edgeCols, out allCols);
return EStripResult.Ok;
```

### 3. `EdgeToLineDistanceMeasurement.TryExecute` — 점수 수신 (2줄 추가)

```csharp
// Source: EdgeToLineDistanceMeasurement.cs:117-131 확장
var svc = new VisionAlgorithmService();
var score = new EdgeStrengthScore();          // 신규
score.SetDenominator(EdgeSampleCount);         // 신규 — 분모 = 이 측정의 strip 수(모든 Z 동일값 사용 전제, D-77-02)
if (!svc.TryFitLine(image,
    Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
    datumTransform,
    EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
    EdgeDirection, EdgePolarity,
    out pr1, out pc1, out pr2, out pc2, out error,
    EdgeSelection, collectedEdgePoints,
    score))                                    // 신규 — 마지막 인자
{
    LastFitScore = 0.0;                        // 실패 시 0(항상 최하위 후보가 되도록)
    return false;
}
LastFitScore = score.Average;                  // 신규 — MeasurementBase 부수효과 프로퍼티
// ... 기존 로직 동일 ...
```

### 4. `Action_FAIMeasurement` — 후보별 실행·선택·기록 (신설 헬퍼, 의사코드 골격)

```csharp
// MeasureShotFaiList 의 완성 tick 분기에서, 기존 foreach(var meas in fai.Measurements) 대신 호출
// Source 패턴: TryExecuteCrossZMeasurement(:2019-2054)를 "N후보 반복+점수비교"로 확장한 형태
private bool TryExecuteMeasurementWithZSelection(
    MeasurementBase meas, ShotConfig shot, InspectionSequence parentSeq2,
    HTuple transform, double pixRes,
    out double resultValue, out string measError, out List<EdgeInspectionOverlay> measOverlays)
{
    resultValue = 0; measError = null; measOverlays = null;
    double dBestScore = double.NegativeInfinity;
    int nBestZ = -1;
    double dRefScore = double.NegativeInfinity;   // 기준 Z(shot.ZIndex) 점수 — 동점규칙(O-3)
    bool bBestOk = false;
    double dBestValue = 0; string szBestError = null; List<EdgeInspectionOverlay> lstBestOverlays = null;

    for (int nZ = shot.ZIndexStart; nZ <= shot.ZIndexEnd; nZ++)
    {
        if (!parentSeq2.HasZRangeImage(shot.ShotName, nZ)) { continue; } // O-4: 중간 z 누락 허용
        using (HImage candidate = parentSeq2.TakeZRangeImageCopy(shot.ShotName, nZ))
        {
            if (candidate == null) { continue; }
            double v; string e; List<EdgeInspectionOverlay> ov;
            bool ok = meas.TryExecute(candidate, transform, pixRes, out v, out e, out ov);
            double dScore = ok ? GetLastFitScore(meas) : 0.0;   // LastFitScore — EdgeToLineDistance/Angle 만 유의미, 그 외 타입은 0(항상 tie)
            if (nZ == shot.ZIndex) { dRefScore = dScore; }
            if (dScore > dBestScore)
            {
                dBestScore = dScore; nBestZ = nZ;
                bBestOk = ok; dBestValue = v; szBestError = e; lstBestOverlays = ov;
            }
        }
    }
    // O-3 동점 규칙: 최고점수 대비 기준Z 점수차가 ZFocusTieBreakPercent 이내면 기준 Z 채택(반복성 우선)
    if (dRefScore > double.NegativeInfinity && dBestScore > 0.0)
    {
        double dGapPercent = (dBestScore - dRefScore) / dBestScore * 100.0;
        if (dGapPercent <= shot.ZFocusTieBreakPercent) { nBestZ = shot.ZIndex; /* 기준 Z 결과 재사용 로직 필요 */ }
    }
    meas.LastSelectedZIndex = nBestZ;
    meas.LastSelectedZScore = dBestScore;
    resultValue = dBestValue; measError = szBestError; measOverlays = lstBestOverlays;
    // [ALGO] 로그 — 기존 LogAndTallyAlgorithm(:858-878) 포맷에 z-select 정보 추가
    Logging.PrintLog((int)ELogType.Algorithm,
        "[ALGO] {0} · z-select best=z{1}(score={2:F2}) ref=z{3}(score={4:F2})",
        shot.ShotName, nBestZ, dBestScore, shot.ZIndex, dRefScore);
    return bBestOk;
}
```

**주의(계획에서 반드시 결정):** 위 의사코드는 "최고 점수 후보의 실행 결과(값/overlay)를 그대로 채택"(D-77-02)
원칙을 따른다. 동점 규칙으로 "기준 Z 채택"이 결정되면 **기준 Z 의 실행 결과를 별도로 다시 확보**해야
한다(루프 중 기준 Z 실행 결과도 별도 보관 필요 — 위 의사코드는 이 분기를 완전히 구현하지 않았다, 계획
단계 TODO).

### 5. `MeasurementBase` 신규 필드 (§Anti-Patterns 참고 — 부수효과 프로퍼티)

```csharp
// Source: MeasurementBase.cs:96-116 (LastMeasuredValue/LastJudgement 옆에 병렬 추가)
[PropertyTools.DataAnnotations.Browsable(false)]
public double LastFitScore { get; set; }        // 0.0 기본 — Strongest/First/Last/All 무관하게 EdgeToLineDistance/Angle 만 대입

[PropertyTools.DataAnnotations.Browsable(false)]
public int LastSelectedZIndex { get; set; } = -1;   // -1 = 범위 미적용(기존 단일 이미지 경로)

[PropertyTools.DataAnnotations.Browsable(false)]
public double LastSelectedZScore { get; set; }

// _copyExclude(:212-217) 에 4개 추가 — Copy/Paste 시 실행결과가 다른 측정으로 번지지 않도록
private static readonly HashSet<string> _copyExclude = new HashSet<string> {
    "MeasurementName",
    "LastMeasuredValue", "LastJudgement", "LastHasResult", "LastSkipReason",
    "LastFitScore", "LastSelectedZIndex", "LastSelectedZScore",              // 신규
    "DatumOriginRow", "DatumOriginCol", "DatumAngleRad", "DatumAngle2Rad",
    "DatumDetectedCircleRow", "DatumDetectedCircleCol"
};
```

**INI 노출 참고:** `DatumOriginRow`(`:66-93`) 사례처럼 `ParamBase` 의 INI reflection 은 `[Browsable(false)]`
여부와 무관하게 **public 프로퍼티를 전부 저장**한다 — `LastFitScore` 등도 INI 에 값 0 으로 기록되는 것을
"수용"할 것(기존 `LastMeasuredValue` 등과 동일한 이미 용인된 노이즈, 별도 `[JsonIgnore]` 불필요).

### 6. CSV 컬럼 확장 — trailing append-only (하위호환)

```csharp
// Source: MeasurementHistoryCsvWriter.cs:24 (CSV_HEADER, 맨 뒤에 추가)
private const string CSV_HEADER = "검사일시,RecipeName,IndexNumber,ShotName,FAIName,MeasurementName,TypeName," +
    "NominalValue,TolerancePlus,ToleranceMinus,MeasuredValue,Judgement,HasResult,OverallCycleResult,검사구분," +
    "SelectedZIndex,SelectedZScore";   // 신규 — 항상 맨 뒤(기존 14/15컬럼 규약, Loader 는 COLUMN_COUNT 불변 유지)

// BuildLine(:95-117) fields 리스트 맨 뒤에 추가
fields.Add(meas.LastSelectedZIndex.ToString(CultureInfo.InvariantCulture));   // -1 = 범위 미적용
fields.Add(meas.LastSelectedZScore.ToString(NUM_FORMAT, CultureInfo.InvariantCulture));
```

```csharp
// Source: MeasurementHistoryCsvLoader.cs:122,635-650 패턴(COL_RUNMODE 옵션 컬럼과 동일하게)
// COLUMN_COUNT(14) 는 그대로 둔다 — 신규 컬럼은 "옵션"으로만 읽는다(옛 15/16컬럼 미만 파일도 정상 파싱).
private const int COL_SELECTED_Z = 15;
private const int COL_SELECTED_Z_SCORE = 16;
// 읽기 시: bool bHasColumn = fields.Count > COL_SELECTED_Z; (기존 COL_RUNMODE 가드와 동일 패턴)
```

---

## State of the Art

| 기존 접근(크로스-Z, Phase 68) | 이번 접근(Phase 77) | 무엇이 바뀌었나 | 영향 |
|---|---|---|---|
| 측정 단위 2-role(A/B) 고정 z 매칭 | Shot 단위 N-후보 z 범위 | 저장 키 스킴을 측정별→Shot별로 단순화 | 저장소 신설(재사용 아님), 완성 판정 로직도 "역할 2개 모두 도착"에서 "마지막 z 도착"으로 변경 |
| 두 장을 **합쳐서** 1번 측정 실행 | 후보마다 **각각** 측정 실행 후 점수로 선택 | 실행 횟수가 O(후보 수)로 증가 | 사이클 타임 실측 필요(§Common Pitfalls 5) |
| `amp` 값 폐기(Strongest 선택에만 내부적으로 사용) | `amp` 를 점수로 외부 노출 | `AppendStrip`/`TryFitLine` 시그니처 확장 | opt-in 설계로 기존 15개 호출부(다른 8개 측정 타입 경유) 전부 무변경 |

**Deprecated/outdated:** 없음 — 이 phase 는 기존 메커니즘을 대체하지 않고 병행(옵트인) 확장한다.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `EdgeSelection="All"` strip 의 점수 기여값을 "strip 내 \|amp\| 평균"으로 정의 | §Common Pitfalls 1, §Code Examples 2 | CONTEXT.md 가 이 경우를 명시하지 않음(Strongest/First/Last 만 명시) — 계획/사용자 확인 없이 구현하면 SIDE 다수 측정(기본값 All)의 실제 선택 결과가 사용자 기대와 다를 수 있음 |
| A2 | 동점 시 "기준 Z 결과 재사용"을 위해 루프 중 기준 Z 실행결과도 별도 보관해야 한다(§Code Examples 4 TODO) | §Code Examples 4 | 미구현 시 동점 규칙이 값만 비교하고 실제 채택 결과(overlay 등)는 최고점수 후보 것을 그대로 쓰는 모순 발생 |
| A3 | PLC 재전송(같은 z 중복 $TEST) 시 완성 tick 재평가를 "멱등이라 무해"로 처리해도 된다 | §Common Pitfalls 3 | 실제로는 CSV/로그 중복 행이 통계를 오염시킬 수 있음 — 사용자 확인 필요 |
| A4 | 초점 척도로 Tenengrad 계열이 실용적이라는 참고문헌(Pertuz et al. 2013)은 CONTEXT.md 에서 이미 인용된 것을 그대로 승계 — 이번 세션에서 재검증하지 않음 | CONTEXT.md 원문(본 문서 비수록) | D-77-02 가 이미 `measure_pos amp` 를 확정했으므로 이 참고문헌은 실제 구현에 영향 없음(정보용) |

---

## Open Questions

1. **`EdgeSelection="All"` 의 정확한 strip 점수 집계식**
   - What we know: CONTEXT.md 는 Strongest/First/Last 만 "채택된 에지"를 명시했다.
   - What's unclear: All 은 strip 당 여러 점이 나올 수 있어 "그 strip 의 amp" 단수 정의가 없다.
   - Recommendation: 계획 단계에서 "strip 내 \|amp\| 평균"(A1)으로 확정하거나, 사용자에게 재확인.

2. **동점 규칙 채택 시 실행 결과 재사용 방식**
   - What we know: "채택 Z 의 측정 결과를 그대로 사용"(재계산 없음)이 원칙.
   - What's unclear: 동점 규칙으로 "기준 Z" 가 채택되면 그 기준 Z 의 실행 결과가 최고점수 후보의 결과와
     다를 수 있다 — 어느 쪽을 최종 채택할지 루프 설계에 명시적으로 반영돼야 한다.
   - Recommendation: §Code Examples 4 의 TODO 를 계획 Task 로 명문화.

3. **범위를 25개 측정 전체에 걸지, 초점 문제가 실증된 일부에만 걸지**
   - What we know: 설정 단위가 Shot(D-77-03)이라 Shot 을 쪼개야 선택 적용이 가능하다.
   - What's unclear: O-1(z 번호 배정 제어팀 협의)이 완료되지 않아 확정 불가 — 이 phase 의 UAT 자체가
     이 협의에 의존한다(ROADMAP.md Phase 77 "Blocked" 명시).
   - Recommendation: 코드/계획은 Shot 단위로 범용 구현하고, 실제 적용 범위(어느 Shot 에 range 를 켤지)는
     레시피 설정값으로 남겨 제어팀 협의 후 결정.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| HALCON 24.11 Progress Steady | `MeasurePos`/`FitLineContourXld` (기존 의존성, 신규 아님) | ✓ (CLAUDE.md 명시) | 24.11 | — |
| MSBuild 15.0/2022 | Debug/x64 빌드 검증 | ✓ (STATE.md 2026-08-27 확인) | `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` (PATH 미등록, 절대경로 필요) | — |
| SIDE 실기 PC | UAT(실측 사이클타임/반복성/XY흔들림) | ✗(이 세션에서 접근 불가) | — | SIMUL_MODE 코드 검증까지만 이 세션에서 가능, 실기는 O-1 협의 후 별도 세션 |

**Missing dependencies with no fallback:** 없음 (SIDE 실기 UAT는 phase 성공 기준에 이미 "실기 검증" 별도
항목으로 존재하므로 블로킹 아님).

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | 없음(CLAUDE.md 명시 — 단위테스트 프레임워크 미도입) |
| Config file | 없음 |
| Quick run command | `"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m -nologo` |
| Full suite command | 위와 동일(빌드가 유일한 자동 검증) + grep 기반 정적 단언(아래) + SIDE 실기 UAT |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SZF-01 | `ShotConfig.ZIndexStart/End` 저장/로드, 0/0 꺼짐 | 빌드+grep 단언 | `grep -n "ZIndexStart\|ZIndexEnd" ShotConfig.cs` (신규 프로퍼티 선언 확인) + Debug/x64 빌드 PASS | ✅ (grep 자체가 테스트, 별도 파일 불필요) |
| SZF-02 | 범위 z 영상 누적·수명관리 | grep 단언 + 실기 UAT | `grep -n "m_dicZRangeImages\|StoreZRangeImage\|ClearZRangeImages" InspectionSequence.cs` (4개 lifecycle 호출부 존재 확인) | ✅ Wave 0에서 구현과 동시 작성 |
| SZF-03 | 측정별 강도점수·선택 | 빌드+grep+수동 SIMUL 실행 | `grep -n "LastFitScore\|EdgeStrengthScore" VisionAlgorithmService.cs EdgeToLineDistanceMeasurement.cs` + SIMUL 모드 1 Shot 수동 실행(로그로 [ALGO] z-select 라인 확인) | ✅ Wave 1 tracer 슬라이스로 확보 |
| SZF-04 | 기록(로그/CSV/오버레이) | grep + CSV 헤더 diff | `grep -n "SelectedZIndex" MeasurementHistoryCsvWriter.cs MeasurementHistoryCsvLoader.cs` + 신규 CSV 1행 append 후 `COLUMN_COUNT` 불변 확인 | ✅ |
| SZF-05 | 회귀 0(범위 꺼짐 경로 무변경) | diff 기반 정적 단언 | 범위 꺼짐(0/0) 경로를 타는 기존 함수(`FindShotByZIndex` 1/2패스, `RunGrab`, `TryExecuteMeasurement`) 의 diff 가 **가드 추가 외 로직 변경 없음**을 코드리뷰로 확인 + SIMUL 기존 회귀 시나리오(Phase 39 UAT 재실행) | ⚠ 전용 회귀 스크립트 없음 — 기존 수동 SIMUL 절차 재사용 |

### Sampling Rate
- **Per task commit:** Debug/x64 빌드(`-p:Configuration=Debug -p:Platform=x64`) + CLAUDE.md 가독성 grep 5종(삼항/`??`/`?.`/switch식/hbk 날짜주석) 0건.
- **Per wave merge:** 위 + 신규/수정 함수 grep 단언 표(위) 전항목 재확인.
- **Phase gate:** Debug/x64 빌드 PASS + SIMUL 모드로 1 Shot(EdgeToLineDistance) end-to-end 수동 실행 로그 확인 후, SIDE 실기 PC UAT(O-1 협의 완료 후 별도 세션) 그린.

### Wave 0 Gaps
- 단위테스트 프레임워크 부재로 인해, `EdgeStrengthScore.Average` 계산식(strip 합÷분모) 자체의 순수함수
  단위검증이 불가능하다 — **Wave 0/1에서 임시 콘솔/디버그 로그**(`Logging.PrintLog(ELogType.Algorithm, ...)`)
  로 strip별 amp/합/분모를 전부 출력해 육안 계산 대조하는 것으로 대체할 것(이 코드베이스의 기존 관례,
  `[FitLine] strips ok ...` 로그와 동일 패턴).
- Framework install: 불필요(정책상 미도입 유지).

---

## Security Domain

이 phase 는 신규 네트워크 엔드포인트/인증/권한 경계를 추가하지 않는다(기존 `$PREP`/`$TEST` TCP 프로토콜
불변, D-77-01). ASVS 대부분 카테고리가 이 phase 범위 밖이다.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | 기존 TCP 프로토콜에 인증 계층 없음(이 phase 로 변경 없음) |
| V3 Session Management | No | 해당 없음 |
| V4 Access Control | No | 해당 없음 |
| V5 Input Validation | Yes(경미) | PLC 가 보내는 z_index 정수값은 기존 `TestPacket`/`PrepPacket` 파싱 경로(`packet.IsRequestValid`)를 그대로 통과 — 신규 파싱 경로 없음. `ZIndexStart/End` 는 레시피 INI(로컬 파일, 신뢰 경계 밖 입력 아님)에서만 읽는다 |
| V6 Cryptography | No | 해당 없음 |

### Known Threat Patterns for {stack}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| PLC 가 비정상적으로 큰/음수 z_index 를 반복 전송해 `m_dicZRangeImages` 무한 증식(리소스 고갈) | Denial of Service | 기존 `DoesZIndexExistInRecipe` 류 화이트리스트 게이트를 재사용해 레시피에 선언된 z 범위 밖 값은 저장하지 않도록(§Pattern 1의 `DoesShotOwnZRange` 게이트가 이미 이 역할을 겸함 — 범위 밖 z 는 애초에 Shot 매칭이 안 돼 저장 호출 자체가 발생하지 않는다) |

---

## Sources

### Primary (HIGH confidence — 코드베이스 직접 확인, 이 세션에서 Read/Grep 수행)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — Shot 프로퍼티/PropertyGrid/Load 패턴
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — z 매칭 3함수, 크로스-Z 저장소 전체(:1463-1562), 완성판정 체인(:823-925)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — Grab/Measure 단계, 크로스-Z 게이트(:740-889), TryExecuteMeasurement/TryExecuteCrossZMeasurement(:1882-2054)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — ZIndexA/B·DatumZIndex 경고 패턴(:220-319), 오설정 Load override(:1337-1371)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — TryFitLine/AppendStrip 전체(:1-374)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — TryExecute 계약, Last* 부수효과 필드 패턴(:96-233)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs`, `EdgeToLineAngleMeasurement.cs` — TryFitLine 호출부(각 1회)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs`, `MeasurementHistoryCsvLoader.cs` — CSV 하위호환 append-only 컬럼 패턴
- `WPF_Example/UI/ViewModel/CycleResultDto.cs`, `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` — DTO/직렬화 파이프라인
- `WPF_Example/Custom/SystemHandler.cs` — `$PREP`/`$TEST`/`$RESET` 처리(:1256-1330)
- `.planning/phases/77-side-z-focus-select/77-CONTEXT.md`, `.planning/ROADMAP.md`(Phase 77 절), `.planning/STATE.md`(현재 상태·메모리 사고 이력), `.planning/phases/76-side-datum/76-CONTEXT.md`
- `D:/Data/Recipe/FAI_1/main.ini`(읽기 전용, EdgeSelection 값 실측 grep)

### Secondary (MEDIUM confidence)
- `.planning/phases/77-side-z-focus-select/77-CONTEXT.md` 내 참고조사(Pertuz et al. 2013, HALCON `depth_from_focus`) — CONTEXT.md 가 이미 결정에 반영한 인용을 그대로 승계, 이 세션에서 원문 재검증 안 함.

### Tertiary (LOW confidence)
- 없음.

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — 신규 의존성 없음, 전부 기존 코드 확장.
- Architecture: HIGH — 크로스-Z 패턴이 거의 동형 선례로 존재하며 전 함수를 파일:줄 단위로 확인함.
- Pitfalls: MEDIUM — `EdgeSelection="All"` 집계식(A1) 등 CONTEXT.md 가 명시하지 않은 세부사항이 남아있어 계획 단계 확정 필요.

**Research date:** 2026-09-15
**Valid until:** 이 phase 계획/실행이 시작되기 전까지 유효(코드베이스 자체 조사라 외부 라이브러리 드리프트
없음 — 단, 이 사이 다른 phase 가 `VisionAlgorithmService.TryFitLine`/`InspectionSequence` 크로스-Z 블록을
수정하면 재확인 필요).
