---
phase: 38-v1-1-carryover-cleanup-2026-05-28
reviewed: 2026-05-28T13:26:59Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/SystemHandler.cs
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 38: Code Review Report

**Reviewed:** 2026-05-28T13:26:59Z
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

Phase 38(v1.1 carry-over cleanup) diff(base 795ac8b)를 standard 깊이로 검토했다. 변경 범위는 4개 파일로 한정되며, 모두 작고 외과적이다: MeasurementFactory UI 목록 축소(Create switch 보존), InspectionRecipeManager 로드시 PixelResolution 단일값 정규화, DatumConfig 각도 UI 정리 + ReuseFromShotName 제거, SystemHandler.Initialize() Stopwatch 계측.

전반적으로 INI 하위호환과 C# 7.2 준수가 잘 지켜졌다. ReuseFromShotName 제거는 코드 사용처 0(planning 문서/제거된 라인 외 .cs 참조 없음)을 확인했고, ParamBase 리플렉션 직렬화는 unknown INI 키를 무시하므로 구 레시피 로딩 회귀 위험이 없다. Critical 이슈는 없으나, PixelResolution 정규화의 엣지 케이스 1건과 신규 FAI 기본 측정타입이 UI에서 사라진 타입을 가리키는 일관성 문제 1건을 Warning으로 분류했다.

## Warnings

### WR-01: PixelResolution 정규화가 CAM 섹션 부재 시 유효한 per-FAI 해상도를 1.0으로 덮어쓸 수 있음

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs:309-314`
**Issue:**
정규화 블록은 `shot.PixelResolution`을 무조건 모든 FAI의 `PixelResolutionX/Y`에 분배한다.
```csharp
double camRes = shot.PixelResolution;
foreach (FAIConfig fai2 in shot.FAIList) {
    fai2.PixelResolutionX = camRes;
    fai2.PixelResolutionY = camRes;
}
```
그러나 `shot.PixelResolution`은 `SHOT_x_CAM` 섹션이 존재할 때만 로드된다(L274-277 `if (loadFile.ContainsSection(camSection)) shot.Load(...)`). CAM 섹션이 없는 구 레시피에서는 `shot.PixelResolution`이 `CameraSlaveParam`의 기본값 `1.0`으로 남고, 이 값이 FAI들이 INI에서 로드한 의미 있는 per-FAI 해상도(예: 0.005 mm/px)를 1.0으로 덮어쓴다. 결과적으로 모든 측정 mm가 200배 등으로 왜곡되어 공차 판정이 전수 오판될 수 있다.

이 "정규화 = 의도적 보정"은 38-01-SUMMARY의 threat surface(T-38-01)에 X≠Y / FAI≠Shot 차이 케이스로 문서화되어 있으나, "CAM 섹션 자체가 없어 camRes가 기본값 1.0인" 케이스는 별도로 다뤄지지 않았다. CAM 섹션이 있는 신규 포맷에서는 문제가 없지만, 키/섹션 부재 시 silent 1.0 fallback은 측정 신뢰성에 직접 영향을 준다.

**Fix:**
camRes가 신뢰 가능한 경우에만 분배하도록 가드를 추가한다. 예를 들어 CAM 섹션 존재 여부를 추적하거나, camRes가 기본 sentinel(1.0)일 때는 정규화를 건너뛰어 기존 per-FAI 값을 보존한다.
```csharp
// CAM 섹션이 실제 로드되어 camRes 가 유효할 때만 정규화 (기본값 1.0 clobber 방지)
bool camLoaded = loadFile.ContainsSection(camSection);
if (camLoaded) {
    double camRes = shot.PixelResolution;
    foreach (FAIConfig fai2 in shot.FAIList) {
        fai2.PixelResolutionX = camRes;
        fai2.PixelResolutionY = camRes;
    }
}
```
(`camSection`은 L274에서 이미 계산되므로 같은 변수를 재사용하거나 bool로 캐시한다.) 정규화를 항상 수행해야 한다면, 최소한 camRes==1.0(미설정 sentinel)일 때 Trace 로그로 경고를 남겨 silent 왜곡을 가시화한다.

### WR-02: 신규 FAI 기본 측정타입(EdgePairDistance)이 UI 드롭다운에서 제거되어 선택 불가/빈 콤보 발생

**File:** `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs:53-68`, `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:54`
**Issue:**
`GetTypeNames()`에서 `EdgePairDistance`를 포함한 5종을 UI 노출 목록에서 제거했다. 그러나 `FAIConfig.EdgeMeasureType`의 기본값은 여전히 `"EdgePairDistance"`이다(FAIConfig.cs:54). PropertyGrid 콤보의 ItemsSource(`EdgeMeasureTypeList` = GetTypeNames 캐시)에 해당 값이 없으므로:
- 신규 FAI 생성 시 콤보가 빈 값으로 표시되거나 첫 항목으로 강제 표시됨(PropertyTools 동작에 따라 다름).
- 구 레시피에서 제거된 5종 중 하나를 Type으로 가진 FAI를 편집하면 콤보에 현재값이 없어 사용자가 무심코 다른 타입으로 덮어쓸 위험.

Create() switch는 5종을 보존하므로 로딩/실행 자체는 정상 동작하나(이 분리는 올바름), UI 일관성 갭이 남는다.

**Fix:**
`FAIConfig.EdgeMeasureType`의 기본값을 GetTypeNames에 남아있는 유효 타입(예: `"CircleDiameter"` 또는 가장 흔히 쓰는 타입)으로 변경한다.
```csharp
// FAIConfig.cs:54
public string EdgeMeasureType { get; set; } = "CircleDiameter"; //260528 hbk Phase 38 — 기본값을 UI 노출 타입으로 정렬
```
구 레시피의 제거된 타입 보존이 필요하다면(편집 시 콤보 비는 문제 방지), 해당 케이스를 UAT로 확인하고 필요 시 현재 Type을 목록에 동적 합집합하는 방안을 검토한다. 본 파일은 phase 38 diff 범위 밖이므로, 최소한 38-UAT에 "신규 FAI 측정타입 콤보 기본 표시"와 "구 레시피 제거타입 편집" 시나리오를 명시 검증 항목으로 추가할 것을 권장한다.

## Info

### IN-01: AngleTolerance 기본값 1.0 → 0.0 변경의 INI 하위호환 — 의도 확인됨

**File:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:122`
**Issue:**
`AngleTolerance` 기본값을 1.0에서 0.0(sentinel = 게이트 off)으로 변경했다. 주석에 명시된 대로 기존 INI에 값이 있으면 ParamBase Load가 덮어쓰므로 하위호환은 유지된다. 다만 INI에 키가 존재하나 값이 정확히 0.0으로 저장된 구 레시피(이전에 사용자가 0을 명시)와 "키 자체가 없어 기본 0.0이 적용된" 신규 레시피가 구분되지 않는다 — 둘 다 게이트 off로 동일 동작하므로 실질 문제는 없다. 단일 sentinel 모델(D-36-13)의 의도된 동작으로 판단된다. 기록 목적의 정보 항목.

**Fix:** 조치 불필요. 동작 의도가 주석과 일치함.

### IN-02: SystemHandler Stopwatch 계측 — Trace 로그 다수 추가(운영 시 로그 볼륨 고려)

**File:** `WPF_Example/SystemHandler.cs:105-169`
**Issue:**
Initialize() 각 단계마다 `Logging.PrintLog((int)ELogType.Trace, ...)` 10여 건을 추가했다. 코드는 정확하다 — `prev`가 각 단계 후 갱신되어 cumulative/delta 계산이 올바르며, 마지막 Step 8 이후 `prev` 재할당 생략도 정상(이후 사용처 없음). C# 7.2 준수(Stopwatch, 보간 없는 string.Format 오버로드 사용). Initialize는 앱 시작 시 1회만 호출되므로 성능 영향은 무시할 수준이고, Trace 채널로 분리되어 운영 노이즈도 제한적이다. 계측 목적상 적절. 정보 항목으로만 기록.

**Fix:** 조치 불필요. 필요 시 향후 영구 계측이 아니라면 진단 완료 후 제거 가능.

### IN-03: ReuseFromShotName 필드 제거 — 코드 사용처 0 확인, 직렬화 영향 없음

**File:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:34-36 (제거됨)`
**Issue:**
`ReuseFromShotName` 속성을 완전 제거했다. 전체 .cs 검색 결과 로직 사용처가 0(planning 문서와 제거된 라인 외 참조 없음)으로 확인되어 안전하다. ParamBase 리플렉션 직렬화는 클래스에 없는 INI 키를 무시하므로, 기존 레시피에 `ReuseFromShotName=` 키가 있어도 파싱 오류 없이 건너뛴다(38-UAT T 항목으로 검증 예정). `ImageSourceMode`의 유효값 주석에 여전히 "ReuseFromShot" 모드가 언급되어 있으나(DatumConfig.cs:30), 이는 모드 문자열일 뿐 제거된 필드와 무관하다. 잔여 주석 정합성만 추후 확인 권장.

**Fix:** 조치 불필요(기능). 선택적으로 DatumConfig.cs:30의 ImageSourceMode 주석이 더 이상 동작하지 않는 ReuseFromShot 경로를 가리키지 않는지 확인.

---

_Reviewed: 2026-05-28T13:26:59Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
