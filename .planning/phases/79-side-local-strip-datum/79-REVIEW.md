---
phase: 79-side-local-strip-datum
reviewed: 2026-09-18T00:00:00Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
  - WPF_Example/UI/ContentItem/MainView.xaml
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
  - WPF_Example/UI/ViewModel/CycleResultDto.cs
  - WPF_Example/UI/ViewModel/MeasurementResultRow.cs
  - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
  - WPF_Example/VersionDefine.cs
findings:
  critical: 0
  warning: 3
  info: 2
  total: 5
status: issues_found
---

# Phase 79: Code Review Report

**Reviewed:** 2026-09-18
**Depth:** standard (diff-focused, `git diff 0e4de450..HEAD`)
**Files Reviewed:** 15
**Status:** issues_found (WARNING/INFO only — no BLOCKER found)

## Summary

Phase 79(국부 기준선 옵션)의 diff 를 `0e4de450..HEAD` 기준으로 훑었다. 핵심 로직(`EdgeToLineDistanceMeasurement.TryExecute`
의 `bUseLocalRef` 분기, `InspectionSequence._localRefLines` 저장소, `Action_FAIMeasurement.InjectLocalRef`/
`TryResolveLocalRef`, CSV/JSON 하위호환, Load() 구버전 INI 기본값 복원, MainView ROI 배선)를 추적한 결과:

- **옵션 OFF 경로는 실제로 안 바뀐다.** `IsInjectedLocalRefUsable()`이 `IsLocalRefEnabled`를 최우선으로 게이트하므로,
  꺼진 측정은 `bUseLocalRef=false` 로 고정되어 기존 `dAxisOriginRow/Col = DatumOriginRow/Col` 경로 그대로 실행된다.
  오버레이(`FAI-RefLine`) 추가도 `if (bUseLocalRef)` 안에서만 일어난다. 회귀 없음 확인.
- **CSV/JSON 하위호환은 올바르게 배선됐다.** `COLUMN_COUNT=14` 불변, `COL_SELECTED_Z=15`/`COL_REF_SOURCE=16` 는
  `fields.Count` 가드 후 옵션 열로만 읽는다. `CycleResultDto.RefSource` 는 신규 속성이라 옛 `cycle.json` 역직렬화 시
  기본값(null)으로 안전하게 채워진다.
- **INI `Load()` 하위호환도 올바르다.** `EdgeToLineDistanceMeasurement.Load()` 가 옛 레시피에 없는 7개 키(Threshold/
  Sigma/SampleCount/TrimCount/Polarity/Direction/Selection)만 선언 기본값으로 되돌리고, 0 이 곧 "미교시" 의미인
  `LocalRef_Row/Col/Phi/Length1/Length2`, `IsLocalRefEnabled` 는 건드리지 않는다 — `MeasurementBase.Load`/
  `DatumConfig.Load` 의 기존 패턴과 동일.
- **`_localRefLines` 딕셔너리 동시성**: 쓰기(`StoreLocalRefLine`/`RemoveLocalRefLinesOfDatum`)와 읽기
  (`TryGetLocalRefLine`)가 모두 `_datumStateLock` 으로 감싸져 있고, 무거운 Halcon 연산(`TryFitLine`)은 락 밖에서
  수행한다 — 기존 `_datumTransforms` 락 패턴과 동일한 수준의 안전성이다. Test Find(UI 스레드)와 자동 사이클
  (시퀀스 스레드)이 동시에 같은 Datum 을 재검출하는 경우의 레이스는 `_datumTransforms` 에 이미 있던 것과 동급
  위험이며 이 phase 가 새로 만든 문제는 아니다.
- 다만 아래 WARNING/INFO 항목은 실제로 고쳐야 한다.

## Warnings

### WR-01: 신규 코드에 CLAUDE.md 필수 규칙("한 줄짜리 분기도 중괄호 생략 금지") 위반 다수

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:1930,1932,1935`
**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:3635,3647,3675,3676,3677,3679,3682,3689,3690,3692,3693,3705,3707,3708`

**Issue:** CLAUDE.md 는 "중괄호는 한 줄짜리 분기라도 생략하지 않는다"를 예외 없는 필수 규칙으로 명시하고
("위반은 회귀로 간주한다"), 검증용 grep 도 제공한다. 이번 diff 의 신규 헬퍼(`InjectLocalRef`,
`TryResolveLocalRef`, `ComputeLocalRefLinesForDatum`, `CollectLocalRefConsumers`,
`AppendShotLocalRefConsumers`, `IsLocalRefConsumer`, `TryGetLocalRefLine`)에서 총 17곳이
`if (...) return;` / `if (...) return false;` / `if (...) continue;` 형태로 중괄호 없이 작성됐다.
(참고: `InspectionSequence.cs:459,675-677` 등 같은 패턴이 이미 파일에 있지만 이들은 이번 diff 밖의 기존
코드라 이 phase 의 회귀는 아니다 — 위 목록만 phase 79 신규 코드다.)

**Fix:** 각 지점을 중괄호 블록으로 감싼다. 예:
```csharp
// Action_FAIMeasurement.cs:1930-1935
private void InjectLocalRef(MeasurementBase meas, InspectionSequence parentSeq2) {
    var etld = meas as EdgeToLineDistanceMeasurement;
    if (etld == null) { return; }
    etld.InjectedLocalRef = null;
    if (!etld.IsLocalRefEnabled) { return; }
    string szReason;
    bool bResolved = TryResolveLocalRef(etld, parentSeq2, out szReason);
    if (bResolved) { return; }
    ...
```
동일하게 `InspectionSequence.cs` 의 나머지 13곳도 `{ }` 로 감싼다.

### WR-02: `BuildLocalRefSettingsKey()`가 double 포맷에 legacy `"R"` 라운드트립 지정자를 사용

**File:** `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs:44,572-599`
**Issue:** `LocalRef_Row/Col/Phi/Length1/Length2`, `LocalRefSigma` 를 `double.ToString("R", ...)` 로 직렬화해
"스테일 설정" 판정 키(`SettingsKey`)를 만든다. .NET Framework 의 `"R"` 지정자는 x64 빌드(`AnyCPU`/`x64`,
이 프로젝트는 x64 강제)에서 일부 double 값에 대해 완전한 라운드트립을 보장하지 못하는 것으로 문서화된
알려진 결함이 있다(Microsoft 권고: `"G17"` 사용). 여기서는 값 정확도가 아니라 "이전에 피팅한 설정과 지금
설정이 같은가"를 문자열 비교로만 판정하므로, 이 결함이 실제로 발현되면 **같은 값인데 다른 문자열**이 나와
`bStale=true` 로 잘못 판정되고 국부 기준이 조용히 전역으로 폴백된다(측정은 안전하지만 옵션이 의도치 않게
꺼진 것처럼 동작 — 로그는 남지만 원인이 헷갈릴 수 있다).
**Fix:** `"R"` 대신 `"G17"` 사용 권장:
```csharp
private const string SETTINGS_KEY_NUMBER_FORMAT = "G17";
```

### WR-03: `CollectLocalRefConsumers` 가 이 시퀀스 소유 모든 Shot 을 매 Datum 검출마다 순회 — 동시 편집 레이스 노출면 확대

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:3660-3695`
**Issue:** 기존 `IsDatumOwnedByCurrentShot`(현재 실행 중인 Shot 하나의 `FAIList` 만 확인)와 달리, 신규
`CollectLocalRefConsumers`→`AppendShotLocalRefConsumers` 는 `SystemHandler.Handle.Sequences.RecipeManager.Shots`
전체를 순회하며 각 `shot.FAIList`/`fai.Measurements` 를 `foreach` 로 읽는다. Datum 검출은 자동 사이클 중
매 tick(비캐시 Datum) 또는 사이클당 최소 1회(캐시 Datum) 일어나므로, 만약 사용자가 검사 실행 중에 다른
Shot 의 레시피(FAIList/Measurements 컬렉션 자체, 항목 추가/삭제)를 PropertyGrid 로 편집하면
`InvalidOperationException: Collection was modified` 가 시퀀스 스레드에서 발생할 노출면이 기존보다
넓어진다. 기존 코드도 유사 위험이 없진 않았으나(예: `RunMeasure`의 `foreach (var fai in ShotParam.FAIList)`),
이번 추가는 "현재 실행 중인 Shot" 범위를 넘어 "이 시퀀스가 소유한 모든 Shot"으로 순회 범위를 넓혔다는 점에서
새로 넓어진 레이스 노출이다.
**Fix:** 필수는 아니지만, 순회 전 `shot.FAIList`/`fai.Measurements` 스냅샷(`ToArray()`/`ToList()`)을 떠서
반복 중 컬렉션 변경 예외를 흡수하거나, 최소한 이 메서드를 감싸는 `try/catch (InvalidOperationException)` 로
"검출 성공/실패는 그대로 두고 국부 기준만 이번 tick 스킵" 하도록 방어하는 것을 권장. (운영 중 레시피 편집이
정책적으로 금지되어 있다면 우선순위는 낮음 — 확인 필요.)

## Info

### IN-01: MainView.xaml.cs — 같은 캐스트(`meas as EdgeToLineDistanceMeasurement`)를 두 변수로 중복 수행

**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:915-925, 956-966, 1012-1022, 1054-1064, 1116-1130`
**Issue:** `ApplyPointRoiMoveDelta`/센터 읽기/`ApplyPointRoiResize`/삭제/마스터 재앵커 5곳 모두
`var etldLocalRef = meas as EdgeToLineDistanceMeasurement;` 로 먼저 체크하고, 바로 아래서 다시
`var etld = meas as EdgeToLineDistanceMeasurement;` 로 같은 캐스트를 반복한다. 동작에는 문제 없으나
(둘 다 같은 참조를 가리킴), 캐스트 1번으로 줄이면 더 읽기 쉽다.
**Fix:** 예시(이동 델타 적용부):
```csharp
var etld = meas as EdgeToLineDistanceMeasurement;
if (etld != null && subKey == EdgeToLineDistanceMeasurement.LOCAL_REF_ROI_SUBKEY) {
    etld.LocalRef_Row += deltaRow;
    etld.LocalRef_Col += deltaCol;
    return;
}
if (etld != null) { etld.Point_Row += deltaRow; etld.Point_Col += deltaCol; return; }
```
나머지 4곳도 동일하게 정리 가능.

### IN-02: `MeasurementHistoryCsvWriter.BuildLine` XML 주석이 컬럼 수와 어긋남

**File:** `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs:96`
**Issue:** `/// <summary>15개 컬럼을 CSV_HEADER 순서대로 콤마 join...</summary>` 이지만 Phase 77(선택Z)+
Phase 79(사용기준) 추가로 실제 컬럼 수는 17개다. Phase 79 가 바로 이 메서드에 `fields.Add(MapRefSource(meas))`
한 줄을 추가하면서 주석 갱신을 놓쳤다(Phase 77 때도 15→16 갱신을 놓쳤던 것이 누적).
**Fix:** `17개 컬럼`으로 수정하거나, 매직넘버 자체를 없애고 "CSV_HEADER 컬럼 순서대로" 로만 서술.

---

_Reviewed: 2026-09-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
