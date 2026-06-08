---
quick_id: 260526-kay
slug: edgeselection-3-edgetolinedistance-d-08-
type: quick
date: 2026-05-26
description: "EdgeSelection 차단 해제 — 3군 일괄 (EdgeToLineDistance D-08/D-09 + EdgeToLineAngle + ArcEdgeDistance 필드 추가)"
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs

must_haves:
  truths:
    - "EdgeToLineDistance / EdgeToLineAngle / ArcEdgeDistance 3종 PropertyGrid 에 EdgeSelection 필드 표시 (드롭다운 All/First/Last)"
    - "각 TryExecute 가 사용자 선택 EdgeSelection 값을 TryFitLine 에 전달 (리터럴 'All' 고정 제거)"
    - "INI 하위호환: EdgeSelection 필드 없는 레거시 INI 로드 시 기본값 'All' 적용"
    - "msbuild Debug/x64 PASS, 신규 warning 0"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs"
      provides: "ICustomTypeDescriptor 차단 해제 + EdgeSelection 필드 사용 복원"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs"
      provides: "EdgeSelection 필드 신규 + EdgeSelectionList ItemsSource"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs"
      provides: "EdgeSelection 필드 신규 + EdgeSelectionList ItemsSource"
  key_links:
    - from: "PropertyGrid EdgeSelection 드롭다운 선택 (All/First/Last)"
      to: "TryFitLine strip-loop 의 measure_pos Select 인자"
      via: "사용자 선택값 → 필드 → TryFitLine selection 인자 → strip-loop 누적 패턴 (각 strip 마다 First/Last 도 1점씩 N strips → N 점)"
      pattern: "사용자 EdgeSelection 노출"
---

# Quick Task 260526-kay: EdgeSelection 차단 해제 (3군 일괄)

## Background

사용자 보고 2026-05-26: EdgeToLineDistance 의 PropertyGrid 에 EdgeSelection (First/Last/All) 드롭다운이 안 보임 — Phase 23.1 D-08/D-09 차단.

**Phase 23.1 D-08/D-09 차단의 원래 근거 (L122-128 주석):**
> FitLineContourXld 는 라인 피팅에 최소 2개 에지점 요구. "First"/"Last" 는 MeasurePos 가 단일 에지점 1개만 반환 → 라인 피팅 실패 → TryFitLine false → 측정 실패 (UI '—').

**그러나 2026-05-17 (Phase 23.1 D-08 와 같은 날짜) 의 strip-loop 도입으로 근거 무효화:**
- VisionAlgorithmService.TryFitLine L120-148: ROI 를 stripCount(기본 20) 개 strip 으로 분할
- 각 strip 마다 AppendStrip → MeasurePos 1회 호출
- `EdgeSelection = "First"` 라도 strip 당 1점 → stripCount 만큼 누적 → N (예: 20) 점 → 라인 피팅 충분
- L161-166 안전 가드: 누적 후 < 2점이면 그제야 실패 반환

**즉 Phase 23.1 D-08 의 차단 근거는 strip-loop 도입 시점에 자동 해소되었으나 차단 해제가 누락되어 왔음.**

**3군 영향 매트릭스:**

| 측정 타입 | EdgeSelection 필드 | 코드 사용 | UI 노출 | 처리 |
|-----------|-------------------|----------|---------|------|
| EdgeToLineDistance | ✅ 존재 (L46-47) | ❌ "All" 리터럴 고정 (L135) | ❌ ICustomTypeDescriptor hide | **차단 해제** (코드 + UI) |
| EdgeToLineAngle | ❌ 없음 | ❌ "All" 리터럴 (L98) | N/A | **필드 + UI 신규 추가** |
| ArcEdgeDistance | ❌ 없음 | ❌ "All" 리터럴 (L103) | N/A | **필드 + UI 신규 추가** |
| ArcLineIntersect | ✅ 존재 (Phase 32) | ✅ 사용자 선택값 | ✅ 노출 | 변경 없음 (이미 OK) |

## Tasks

### Task 1: EdgeToLineDistance — D-08/D-09 차단 해제

**File:** `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs`

**Action:**
1. **클래스 선언 (L18-20):** `System.ComponentModel.ICustomTypeDescriptor` 상속 제거. 클래스 시그니처를 `public class EdgeToLineDistanceMeasurement : MeasurementBase` 로 환원.
2. **TryExecute (L122-128):** "All" 리터럴 고정 주석 제거 + L135 의 `"All"` 인자 → `EdgeSelection` 필드 사용으로 복원. 신규 주석: `//260526 hbk quick-260526-kay — D-08 차단 해제: strip-loop 가 First/Last 도 stripCount 점 누적하므로 사용자 선택값 사용 안전`
3. **ICustomTypeDescriptor 구현 메서드 8개 (L279-313 인근):** 모두 삭제. `BuildFilteredProperties` 도 삭제.
4. **EdgeSelection 기본값 유지:** L47 `= "All"` 유지 (INI 하위호환).

**Verify:**
- grep `ICustomTypeDescriptor` in EdgeToLineDistanceMeasurement.cs → 0 hits
- grep `"All"` literal (TryFitLine 인자로 사용된) → 0 hits in TryExecute
- grep `EdgeSelection` field 사용 → TryFitLine 인자에 1 hit

### Task 2: EdgeToLineAngle — EdgeSelection 필드 신규 추가

**File:** `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs`

**Action:**
1. **EdgeSelection 필드 신설:** EdgeToLineDistance L46-47 패턴 복사:
   ```csharp
   //260526 hbk quick-260526-kay — EdgeSelection 사용자 노출 (strip-loop 가 First/Last 도 충분 점 누적)
   [ItemsSourceProperty(nameof(EdgeSelectionList))]
   public string EdgeSelection { get; set; } = "All";
   ```
2. **EdgeSelectionList 래퍼 추가:** EdgeToLineDistance L55 패턴 복사:
   ```csharp
   [PropertyTools.DataAnnotations.Browsable(false)]
   public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }
   ```
3. **TryFitLine 호출 (L98):** `"All"` → `EdgeSelection`. 주석 갱신.

**Verify:**
- grep `public string EdgeSelection` in EdgeToLineAngleMeasurement.cs → 1 hit
- grep `EdgeSelection,` or `EdgeSelection)` in TryFitLine call → 1 hit

### Task 3: ArcEdgeDistance — EdgeSelection 필드 신규 추가

**File:** `WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs`

**Action:** Task 2 와 동일 패턴 (필드 추가 + List 래퍼 + TryFitLine 인자 교체).

**Verify:** 동일.

### Task 4: msbuild Debug/x64 검증

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
    'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' `
    -t:Build -p:Configuration=Debug -p:Platform=x64 -v:m -clp:Summary
```

**Verify:** Errors=0, 신규 warning=0.

## Threat Model

| Threat ID | Category | Component | Disposition | Mitigation |
|-----------|----------|-----------|-------------|------------|
| T-Q-04 | INI 회귀 | 레거시 INI 의 `EdgeSelection=First` 로드 시 기존 "All" 강제 무시되었으나 이제 사용 | mitigate | strip-loop 가 First 도 stripCount 점 누적하므로 안전. 그래도 사용자 시각 검증 필요. |
| T-Q-05 | EdgeToLineAngle/ArcEdgeDistance INI 신규 키 | EdgeSelection 필드 추가 시 신규 INI 키 추가 — 레거시 INI 에 없음 | accept | ParamBase Load 의 누락 키 폴백 = 기본값 "All" 적용 → 회귀 0. |
| T-Q-06 | stripCount=1 에서 First/Last 점 부족 | 사용자가 stripCount=1 설정 + First 선택 → 1 점 → 라인 fit 실패 | accept | TryFitLine L161-166 의 `edgeCount < 2` 가드가 명시적 에러 반환. UI '—' 표시 + 진단 메시지. |
