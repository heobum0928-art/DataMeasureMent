# Phase 57: 패턴 ROI UX & Datum 정렬 보강 - Pattern Map

**Mapped:** 2026-06-19
**Files analyzed:** 8 (전부 MODIFY — 신규 파일 없음)
**Analogs found:** 8 / 8 (전부 in-file 또는 동일 코드베이스 미러)

> 이 phase 는 기존 ALIGN(Phase 54/55/56) 코드의 **보강(hardening)** 이다. 신규 파일 생성 0건.
> 모든 작업의 "analog" 은 외부 코드가 아니라 **같은 파일/같은 코드베이스 안의 검증된 패턴**(미러 대상)이다.
> 따라서 본 문서의 핵심은 "어디를 어느 기존 패턴에 맞춰 고치는가" 의 정확한 좌표·발췌이다.

---

## File Classification

| 수정 파일 | Role | Data Flow | 미러/Analog 패턴 | Match Quality | 해당 항목 |
|-----------|------|-----------|------------------|---------------|-----------|
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | action (state machine) | event-driven (step transition) | 단일이미지 align 분기 `:177-187` (DualImage 분기로 미러) / `EStep` switch 재배선 | exact (in-file) | #4, #6 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | sequence/service | transform | `TryComposeAlign:453` (재사용) / leveling 멤버 제거 | exact (in-file) | #4, #6 |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | service (vision algo) | transform / file-I/O | `AlignPreTransform:29` 소비 패턴(`:252`/`:1932`) / DualImage `:762` / leveling 제거 | exact (in-file) | #4, #6 |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | display/render | request-response (draw) | `RenderDatumFindResult:301` slate blue origin 십자 `:311` (recolor 미러) | exact (in-file) | #3 |
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | view (code-behind) | request-response | `SetDatumOverlayVisible:614` + `_datumOverlayVisible:584` (#2 미러 원본) | exact (in-file) | #2 |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | view (code-behind) | request-response | `DrawPatternRoi2Button_Click:2724` 미러 / `InvokeCreatePatternModel:2776` 경고 삽입 | exact (in-file) | #1, #2 |
| `WPF_Example/UI/ContentItem/MainView.xaml` | view (XAML layout) | n/a | `btn_drawPatternRoi:181` / `btn_drawPatternRoi2:225` 버튼 재배치 | exact (in-file) | #1, #2 |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | model/config | file-I/O (INI via ParamBase) | `IsLevelingReference:43` 제거 / pattern 필드 보존 | exact (in-file) | #6 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs` | param (PropertyGrid) | file-I/O | `LevelingEnabled:39` 프로퍼티 제거 | exact (in-file) | #6 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` | service (INI save/load) | file-I/O | leveling save/load `:97`/`:113`/`:139` 제거 + stale-key 무시 검증 | exact (in-file) | #6 |

---

## Pattern Assignments

### #4 — Side 4-ROI(DualImage) 정렬 (단일 공유 transform)

**대상 파일:** `Action_FAIMeasurement.cs` (DualImage 분기) + `DatumFindingService.cs` (DualImage 검출에 AlignPreTransform 입력)

**미러 원본 = 단일이미지 align 배선** (`Action_FAIMeasurement.cs:176-203`):
```csharp
if (datum.IsPatternAlignEnabled) {
    string modelPath = InspectionSequence.ResolveDatumModelPath(datum); // 54-05 티칭과 동일 키 헬퍼 (D-07)
    string alignErr;
    if (!parentSeq.TryComposeAlign(datum, img, modelPath, out alignErr)) {
        ...
        datum.RuntimeDetectFailed = true;
        parentSeq.MarkAlignFailed(datum.DatumName); // D-10 lenient — 측정 NG(ALIGN_FAIL) 강제, abort 안 함
    }
} else {
    string derr;
    if (!parentSeq.TryRunSingleDatum(datum, img, null, out derr)) { ... parentSeq.MarkDatumFailed(...); }
}
```

**현재 DualImage 분기 = align 미적용(deferred 게이트)** (`Action_FAIMeasurement.cs:147-158`):
```csharp
//260618 hbk ... DualImage align 삽입은 후속 phase(deferred).
string derr;
if (!parentSeq.TryRunSingleDatum(datum, imgH, imgV, out derr)) {  // ← align 없이 바로 검출
    ...
    parentSeq.MarkDatumFailed(datum.DatumName);
}
```

**#4 실행 지침 (executor MUST):**
1. DualImage 분기(`:147-158`)를 단일이미지 분기(`:176-203`)와 **구조 동형**으로 재배선: `datum.IsPatternAlignEnabled` 분기 추가.
2. align-enabled 시 `TryComposeAlign(datum, imgH, modelPath, out alignErr)` 호출 — **refImage = `imgH`(가로축, `TeachingImagePath`)**. 세로축(`imgV`)에는 패턴 모델 없음 (D-04).
3. `TryComposeAlign` 자체는 변경 최소 (D-01 재사용). 단, 내부 ④단계(`InspectionSequence.cs:533-541`)의 `detectSvc.TryFindDatum(refImage, datum, ...)` 가 **1-image 오버로드**라 DualImage datum 의 세로 ROI 를 검출하지 못함 → DualImage 일 때 `TryFindDatum(imgH, imgV, datum, ...)` 2-image 오버로드를 타도록 보정 필요.
4. **AlignPreTransform 를 DualImage 검출에 전파**: 현재 `TryFindVerticalTwoHorizontalDualImage`(`DatumFindingService.cs:762`)는 `AlignPreTransform` 를 **전혀 소비하지 않는다**(단일이미지 변형 `:252`/`:1932`만 ROI 중심을 transform). D-01 의 "가로/세로 ROI **모두**에 동일 transform" 을 충족하려면 이 메서드의 Vertical/Horizontal_A/Horizontal_B ROI 중심에 `AffineTransPoint2d(AlignPreTransform, ...)` 를 적용해야 한다.

**AlignPreTransform 소비 패턴 (미러 — `DatumFindingService.cs:250-260` 발췌):**
```csharp
if (AlignPreTransform != null && AlignPreTransform.Length > 0)
{
    HTuple acR, acC;
    HOperatorSet.AffineTransPoint2d(AlignPreTransform, circRoiRow, circRoiCol, out acR, out acC);
    // ... 변환된 중심으로 검출 ROI 재배치
}
```
**회전분 추출 패턴 (Line ROI 용, `:1932-1941` 발췌):**
```csharp
if (AlignPreTransform != null && AlignPreTransform.Length > 0)
{
    HTuple atRow, atCol;
    HOperatorSet.AffineTransPoint2d(AlignPreTransform, roiRow, roiCol, out atRow, out atCol);
    double alignRot = Math.Atan2(AlignPreTransform[3].D, AlignPreTransform[0].D); // Phi 에 가산
}
```

**SameFrame 계약 (반드시 준수 — `DatumFindingService.cs:89-98`):** DualImage 두 입력은 동일 sensor frame 가정 (텔레센트릭, D-02). width/height 불일치 시 즉시 차단. → 같은 좌표계이므로 단일 transform 이 두 이미지에 글로벌 유효 (D-01/D-02 의 수학적 근거).

**이중적용 가드 (확립된 패턴):** ALIGN 은 검출 ROI 에 transform 1회 적용 후 검출(nominal 불변). `TryComposeAlign` 은 align 단독 경로로 호출되어 통상 `existing` transform 없음 → `alignRigid` 단독 저장 (`InspectionSequence.cs:451` 주석 W4 참조). DualImage 도 동일 — transform 1회만 적용 보장.

---

### #6 — leveling 완전 제거

**대상 파일 (제거 footprint 전수):**

| 파일 | 제거 대상 (line) | 비고 |
|------|------------------|------|
| `DatumConfig.cs` | `IsLevelingReference` + `[Category("Datum\|Leveling")]` 속성 (`:40-43`) | ParamBase 자동 INI 직렬화 대상 → 제거 시 per-datum 섹션에서 키 사라짐. **stale 키는 ParamBase.Load 가 무시**(매칭 프로퍼티 부재) → 로드 크래시 없음 (D-14, MEMORY: parambase_missing_key 주의) |
| `InspectionSequence.cs` | `LevelingEnabled`(`:49`), `_levelingAngleRad`/`_levelingComputed`(`:65-66`), `LevelingAngleRad`/`LevelingComputed`(`:67-68`), `SetLevelingAngle`/`ResetLeveling`(`:69-70`), `ClearDatumTransforms` 내 `ResetLeveling()` 호출(`:363`), `TryComputeLevelingAngle`(`:603-639`) | `ResetLeveling()` 호출 제거 시 `ClearDatumTransforms` 나머지 로직 무손상 확인 |
| `DatumFindingService.cs` | `TryGetLevelingAngle` 4-arg 오버로드(`:609-684`) + 6-arg dRow/dCol 오버로드(`:689-`) | 두 오버로드 모두 제거. 호출처는 `TryComputeLevelingAngle` 하나뿐 (동시 제거됨) |
| `Action_FAIMeasurement.cs` | `EStep.Level` enum(`:33`) + `case EStep.Level`(`:80-118`) 전체 + SIMUL 진단 로그 블록 | **상태머신 전이 재배선 = 최우선 위험 (D-14)** |
| `InspectionMasterParam.cs` | `LevelingEnabled` PropertyGrid 프로퍼티(`:34-50`) + `[Category("Fixture\|Leveling")]` | |
| `InspectionRecipeManager.cs` | save `:97`, 신규레시피 보존 `:113`, load `:139` 의 `LevelingEnabled` 키 3곳 | |

**#6 상태머신 재배선 패턴 (최우선 — D-14):**
현재 전이 사슬: `Init → MoveZ → Level → DatumPhase → Grab → Measure → End`.
`Level` 제거 후: `MoveZ` 의 종료 라인을 `Step = (int)EStep.DatumPhase;` 로 변경.

현재 `MoveZ` 종료 (`Action_FAIMeasurement.cs:75`):
```csharp
Step = (int)EStep.Level;   // ← 변경 대상: EStep.DatumPhase 로
```
**executor 자유 영역 (D-13/Claude's Discretion):** enum 멤버 순서·구체적 전이 구현 방식. 단, `(int)EStep` 캐스팅 + `switch((EStep)Step)` 패턴(이 파일의 확립된 관용)은 유지. enum 값이 정수 캐스팅에 쓰이므로 멤버 제거 시 나머지 케이스의 정수값 변동에 주의(switch 가 명칭 기반이라 안전하지만 혼동 방지).

**off-회귀 0 검증 패턴 (D-14):** 기존 INI 의 `LevelingEnabled`/`IsLevelingReference` 키는 매칭 프로퍼티 제거 후 stale 가 됨.
- `InspectionRecipeManager` 의 `LevelingEnabled` 명시 read(`:139`)는 제거되므로 무관.
- `DatumConfig.IsLevelingReference` 는 ParamBase 자동 직렬화였으므로, 제거 후 옛 INI 의 해당 키는 `ParamBase.Load` 가 매칭 프로퍼티 못 찾아 **무시** (참조: MEMORY `reference_parambase_missing_key_zeroes_default` — 단 이 케이스는 "키→프로퍼티" 가 아니라 "프로퍼티 없음→키 무시" 라 안전). 로드 크래시 0 을 UAT 로 확증.

---

### #3 — Datum 시각화 색상 통일 (slate blue, 기준선 유지)

**대상 파일:** `HalconDisplayService.cs` — `RenderDatumFindResult`(`:301`)

**미러 원본 = slate blue origin 십자 (이미 적용된 검증 패턴, `:310-312`):**
```csharp
//  "purple" 는 HALCON 유효 색상명 아님 → SetColor 예외 → catch swallow → 십자 전체 미표시. "slate blue" 로 교체.
HOperatorSet.SetColor(window, "slate blue");
HOperatorSet.SetLineWidth(window, 2);
```

**recolor 대상 1 — magenta 기준선 (`:346`):**
```csharp
HOperatorSet.SetColor(window, "magenta");   // ← "slate blue" 로 recolor (선/길이/좌표 무변경, D-10 기준선 유지)
HOperatorSet.SetLineWidth(window, 3);
```
이 magenta 블록은 **수평 기준선(`:360-363`) + 수직 기준선(`:381-383`) 둘 다**에 적용되는 SetColor 다. 색만 바꾸고 GetPart 전체관통 길이 로직(`:348-358`)은 그대로 (D-10: 14208px 이미지 시인성 위해 유지).

**recolor 대상 2 — legacy yellow (CONTEXT `:52/:70/:166/:200`):**
> ⚠️ **주의 — CONTEXT 인용 라인 중 datum 관련만 recolor.** 실제 코드 확인 결과:
> - `:52`/`:70` = **teach 모드 ROI 선택 강조색**(`selectedRoiId == yellow`, Circle/Rect ROI). datum 시각화 아님 → #3 무관, 건드리지 말 것.
> - `:166` = `Main-Crosshair-H/V` 메인 십자선 yellow. datum 아님 → 무관.
> - `:200` = `FAI-EdgeRaw` 측정 raw 에지점 yellow cross. datum 아님 → 무관.
>
> 즉 #3 의 실제 recolor 대상은 **`RenderDatumFindResult` 내부 magenta 기준선(`:346`) 한 곳**. legacy yellow 중 datum 십자/기준선에 해당하는 것은 현재 코드에 없음(이미 slate blue/magenta 로 정리됨). executor 는 magenta→slate blue 만 적용하고, datum 관련 legacy yellow 잔재가 추가로 발견되면 동일 recolor. **datum 무관 yellow 는 절대 변경 금지.**

**HALCON SetColor 함정 (MEMORY `feedback_halcon_setcolor_invalid_names` + code_context):**
- 유효 색상명만 사용. `"slate blue"` 는 검증됨(`:311` 동작 중). `"purple"` 등 비표준명 → SetColor 예외 → catch swallow → **렌더 블록 전체 silent 미표시**.
- recolor 시 반드시 `"slate blue"` (공백 포함, 소문자) 정확히 사용.

**catch swallow 관습 (`:395-398`):** 이 메서드는 전체를 `try { } catch { }` 로 감싸 display 예외를 삼킨다. 잘못된 색상명이 들어가도 예외가 silent 처리되므로 "안 보임" 증상 시 색상명 1순위 의심.

---

### #2 — 패턴 ROI 표시/숨김 토글

**미러 원본 = `SetDatumOverlayVisible` (`MainResultViewerControl.xaml.cs:584,614-618`):**
```csharp
private bool _datumOverlayVisible = true;                 // :584 — 게이트 필드 (기본 ON)

public void SetDatumOverlayVisible(bool visible)          // :614 — 토글 setter
{
    _datumOverlayVisible = visible;
    Render();                                             // 즉시 재렌더
}
```

**렌더 게이트 미러 (RenderNow, `:803`/`:812`/`:831`):**
```csharp
if (_datumConfig != null && _datumOverlayVisible) { ... }                       // teach datum
if (_datumOverlayVisible && _resultDatumOverlays != null) { ... }              // 결과 datum 기준선
if (_datumOverlayVisible && _resultDatumRoiOverlays != null && ...) {          // 보정 datum 검색 ROI (orange)
    _displayService.RenderResultRoiBoxes(ViewerHost.HalconWindow, _resultDatumRoiOverlays, "orange", 2);
}
```

**#2 실행 지침 (executor):**
1. `MainResultViewerControl.xaml.cs` 에 `private bool _patternRoiOverlayVisible = true;` 필드 + `public void SetPatternRoiOverlayVisible(bool visible) { _patternRoiOverlayVisible = visible; Render(); }` 추가 (위 미러 동형).
2. 패턴 ROI 렌더 게이트를 RenderNow 에 추가. 패턴 ROI rect 는 `RenderResultRoiBoxes(window, rects, color, lineWidth)` (`HalconDisplayService.cs:483`) 로 그릴 수 있음 — datum 검색 ROI(orange) 패턴 그대로. 패턴 ROI 좌표는 `DatumConfig.PatternRoi_Row/Col/Phi/Length1/Length2` (+ PatternRoi2_*) 에서 `{row,col,phi,l1,l2}` double[] 로 구성.
3. UI 체크박스: `MainView.xaml` 에 `chk_overlayPattern` 추가 + `MainView.xaml.cs` 에 `Chk_overlayPattern_Changed` 핸들러 — 기존 `Chk_overlayDatum_Changed`(`MainView.xaml.cs:2199` 호출부) 미러:
```csharp
halconViewer.SetDatumOverlayVisible(chk_overlayDatum.IsChecked == true);  // :2199 — 미러 대상
```
4. **색상명 유효성 (#3 함정 공유):** 패턴 ROI 색은 유효 HALCON 색상명만. 측정(green)/datum 검색(orange)과 구분되는 유효명 권장 (예: `"slate blue"` 또는 `"cyan"`).

---

### #1 — 패턴 ROI 2개 필수 (경고 + override) + 버튼 나란히 배치

**미러 원본 = 패턴 1/2 ROI 그리기 핸들러 쌍 (`MainView.xaml.cs:2685~`/`:2724-2767`):**
`DrawPatternRoi2Button_Click`(`:2724`) 는 `DrawPatternRoiButton_Click` 의 정확한 미러 (canvasMode 만 `PatternRoi`→`PatternRoi2`, write-back 필드만 `PatternRoi_*`→`PatternRoi2_*`). 이 미러 관계 유지.

**경고 삽입 지점 = `InvokeCreatePatternModel` (`MainView.xaml.cs:2796-2799`):**
```csharp
// W2: PatternRoi 미확보 시 모델 생성 차단 (silent 실패 0)
if (datum.PatternRoi_Length1 <= 0.0 || datum.PatternRoi_Length2 <= 0.0) {
    CustomMessageBox.Show("모델 생성 실패", "패턴 ROI(Rect) 를 먼저 그리세요. ([패턴 ROI] 버튼)");
    return;   // ← 패턴1 미확보는 하드 블록 유지
}
```

**#1 실행 지침 (executor):**
1. 패턴1 가드(`:2796-2799`)는 유지 (패턴1 은 필수).
2. **패턴2 미확보 시 경고+override** 를 패턴1 가드 직후에 삽입. 패턴2 판정 = `datum.PatternRoi2_Length1 <= 0.0 || datum.PatternRoi2_Length2 <= 0.0` (기존 폴백 조건과 동일, `InspectionSequence.cs:490` 미러). `CustomMessageBox` 가 OK/Cancel 반환을 지원하면 그것으로, 아니면 Yes/No 형 다이얼로그로 "패턴 2 권장 — 단일은 회전 정밀도 저하" 경고 후 사용자 OK 시 진행, Cancel 시 `return` (D-06, hard block 아님).
   - 문구는 Claude's Discretion (D-06 취지 유지).
3. **단일 패턴 폴백 경로 보존 (D-08):** `TryComposeAlign` 의 `PatternRoi2_Length1 > 0` 분기(`InspectionSequence.cs:490-514`)는 그대로 — 패턴2 없으면 단일 패턴 θ 사용. 코드 변경 없음.

**버튼 나란히 배치 = `MainView.xaml` (`:181`/`:225`):**
현재 순서: `btn_drawPatternRoi`(패턴1, `:181`) → `btn_createPatternModel`(패턴 모델 생성, `:203`) → `btn_drawPatternRoi2`(패턴2, `:225`).
→ D-07: 패턴1/패턴2 버튼을 **인접**시킴 (패턴1, 패턴2, [패턴 모델 생성] 순서로 재배치). 버튼 스타일/Click 핸들러/x:Name 무변경 — XAML 요소 순서만 이동. `Margin="0,0,4,0"` 간격 관습 유지.

---

## Shared Patterns

### lenient (skip+log, abort 없음) — #4/#5 공통
**Source:** `InspectionSequence.cs` `MarkDatumFailed`(`:368`) / `MarkAlignFailed`(`:382`), `Action_FAIMeasurement.cs` DatumPhase 전반
```csharp
public void MarkAlignFailed(string datumName)
{
    if (!string.IsNullOrEmpty(datumName))
    {
        _alignFailedDatums.Add(datumName);
        MarkDatumFailed(datumName); // 측정 NG 강제 위해 기존 게이트 set 에도 add
    }
}
```
**Apply to:** #4 DualImage align 실패 시 동일하게 `MarkAlignFailed(datum.DatumName)` 호출 (단일이미지 분기 `:187` 동형). **#5 검증:** DatumPhase 의 모든 실패 분기가 `continue`/skip 으로 끝나고 abort(throw/FinishAction Error) 가 없음을 확인 — `EStep.DatumPhase` 종료가 무조건 `Step = (int)EStep.Grab`(`:208`) 임이 lenient 보장. align/datum 실패가 측정 진행을 막지 않음을 UAT 로 확증.

### HALCON SetColor 유효 색상명 — #2/#3 공통
**Source:** `HalconDisplayService.cs:310-311` (slate blue 검증), MEMORY `feedback_halcon_setcolor_invalid_names`
**Apply to:** 모든 신규/변경 SetColor — 유효명만 (`"slate blue"`, `"green"`, `"orange"`, `"cyan"`, `"red"`, `"magenta"`, `"yellow"`, `"lime green"` 검증됨). 비표준명("purple" 류) 금지 → catch swallow 로 silent 미표시.

### HImage dispose — #4 공통
**Source:** `Action_FAIMeasurement.cs:159-162` (DualImage finally)
```csharp
} finally {
    if (imgH != null) { try { imgH.Dispose(); } catch { } }
    if (imgV != null) { try { imgV.Dispose(); } catch { } }
}
```
**Apply to:** #4 에서 추가되는 어떤 HImage 도 try/finally dispose (CLAUDE.md 규약).

### ResolveDatumModelPath 단일 키 헬퍼 — #4 공통
**Source:** `InspectionSequence.cs:399` (static helper, 티칭·런타임 공유)
**Apply to:** #4 DualImage align 의 modelPath 도 **반드시** `InspectionSequence.ResolveDatumModelPath(datum)` 사용 — 직접 경로 도출 금지 (경로 불일치 = ALIGN_FAIL 구조적 차단). 단일이미지 분기 `:178` 동형.

### PropertyGrid 재바인딩 — #1 공통
**Source:** `MainView.xaml.cs:2848-2850`
```csharp
try { datum.RaisePropertyChanged(string.Empty); } catch { }
if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
```
**Apply to:** #1 에서 datum 필드 변경 후 PropertyGrid 갱신 필요 시 동일 패턴.

### 주석 규약 (MEMORY `feedback_comment_convention`)
모든 코드 변경에 `//260619 hbk Phase 57 ...` 형식 주석 필수 (날짜 YYMMDD + 이니셜 hbk).

---

## No Analog Found

없음. 본 phase 는 전부 기존 코드 보강이며, 모든 작업이 같은 파일/같은 코드베이스의 검증된 패턴을 미러한다.

| 항목 | 비고 |
|------|------|
| (해당 없음) | 신규 알고리즘/엔진/capability 0건 (CONTEXT Out of scope) |

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (action/sequence/recipe/config)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (vision algo + align transform 소비)
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` (render)
- `WPF_Example/UI/ContentItem/` (MainView, MainResultViewerControl)

**핵심 위험 (planner 우선 순위):**
1. **#6 `EStep.Level` 제거 후 상태머신 전이 재배선** (D-14, 최우선) — `MoveZ → DatumPhase` 직결.
2. **#4 AlignPreTransform 의 DualImage 검출 미전파** — `TryFindVerticalTwoHorizontalDualImage`(`:762`) 가 현재 transform 미소비. D-01 의 "두 이미지 동일 transform" 충족하려면 이 메서드 보강 필요(단일이미지 변형 `:252`/`:1932` 패턴 이식).
3. **#4 TryComposeAlign ④단계의 1-image 검출** — `InspectionSequence.cs:537` 이 1-image `TryFindDatum` 호출. DualImage datum 은 2-image 오버로드로 분기 필요.
4. **#3 CONTEXT 인용 yellow 라인 중 datum 무관(`:52/:70/:166/:200`) 오변경 금지** — 실제 recolor 는 magenta(`:346`) 한 곳.

**Pattern extraction date:** 2026-06-19
