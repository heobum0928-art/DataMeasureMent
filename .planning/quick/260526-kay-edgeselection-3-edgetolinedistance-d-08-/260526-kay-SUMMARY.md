---
quick_id: 260526-kay
slug: edgeselection-3-edgetolinedistance-d-08-
date: 2026-05-26
status: complete
tags: [quick, edge-selection, phase-23.1, phase-31, propertygrid, strip-loop]
description: "EdgeSelection 차단 해제 — 3군 일괄 (EdgeToLineDistance D-08/D-09 + EdgeToLineAngle + ArcEdgeDistance 필드 추가)"
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs
commits:
  - 24b33b9  # Task 1 — EdgeToLineDistance D-08/D-09 차단 해제
  - b33e141  # Task 2 — EdgeToLineAngle EdgeSelection 필드 신규
  - 59ce666  # Task 3 — ArcEdgeDistance EdgeSelection 필드 신규
duration: ~25min
---

# Quick Task 260526-kay Summary

**3 측정 타입(EdgeToLineDistance / EdgeToLineAngle / ArcEdgeDistance) 의 EdgeSelection 사용자 노출 복원. Phase 23.1 D-08/D-09 + Phase 31 D-05/D-08 의 "All" 리터럴 고정 패턴이 strip-loop 도입(2026-05-17) 후 무효화된 근거로 일괄 해제. msbuild Debug/x64 PASS, 신규 warning 0.**

## What Changed

### Task 1: EdgeToLineDistance — Phase 23.1 D-08/D-09 차단 해제 (commit `24b33b9`)

| 변경 | 효과 |
|------|------|
| 클래스 선언에서 `ICustomTypeDescriptor` 제거 | PropertyGrid hide 해제 |
| `GetProperties`/`BuildFilteredProperties` 등 8 구현 메서드 삭제 (총 40+ 라인) | 코드 정리 |
| `TryExecute` L135 `"All"` 리터럴 → `EdgeSelection` 필드 | 사용자 선택값 적용 |
| `EdgeSelection` 기본값 `"All"` 유지 | INI 하위호환 |

### Task 2: EdgeToLineAngle — EdgeSelection 필드 신규 (commit `b33e141`)

```csharp
//260526 hbk quick-260526-kay
[ItemsSourceProperty(nameof(EdgeSelectionList))]
public string EdgeSelection { get; set; } = "All";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }
```

TryFitLine 인자: `"All"` → `EdgeSelection`

### Task 3: ArcEdgeDistance — EdgeSelection 필드 신규 (commit `59ce666`)

Task 2 와 동일 패턴.

## Verification

### Build
- msbuild Debug/x64 PASS
- Errors: 0
- Warnings: 4 (= 2 unique × 2 builds, 신규 0)
  - MSB3884 (환경)
  - CS0162 (VirtualCamera.cs:266)
- 경과 시간: 00:00:04.23

### Grep
- `ICustomTypeDescriptor` in EdgeToLineDistanceMeasurement.cs → 0 hits ✓
- `EdgeSelection` field 사용 in TryFitLine 인자 → 3 hits (3 measurement types) ✓
- `"All"` 리터럴 in 3 measurement TryFitLine 호출 → 0 hits ✓

## Background — Phase 23.1 D-08 차단 근거의 무효화

**원래 차단 근거 (Phase 23.1 D-08, 2026-05-17 일자):**
> FitLineContourXld 는 라인 피팅에 최소 2개 에지점 요구. "First"/"Last" 는 MeasurePos 가 단일 에지점 1개만 반환 → 라인 피팅 실패 → TryFitLine false → 측정 실패 (UI '—').

**무효화 시점:** 같은 날짜 2026-05-17, `VisionAlgorithmService.TryFitLine` 의 strip-loop 재작성 — "단일 MeasurePos → strip-loop 누적 (CO-23-01 구조적 차단 제거)" (코드 주석 L104-109).

**Strip-loop 동작:**
```csharp
int stripCount = 20;  // 기본값
for (int i = 0; i < stripCount; i++) {
    AppendStrip(image, ..., measureSel, ref allRows, ref allCols);
    //  각 strip 마다 MeasurePos 호출 — measureSel = "First" 도 strip 당 1점 반환
}
//  결과: stripCount(20) 만큼 누적 → 라인 피팅 충분
```

**즉:** 차단 근거가 `EdgeSelection = "First"` 도 stripCount 만큼 누적 → 라인 피팅 안전. UI 숨길 이유 없음.

**무엇이 누락되었나:** strip-loop 도입 시점에 D-08/D-09 차단을 동시 해제했어야 했으나 누락. ArcLineIntersect (Phase 32 재설계 2026-05-21) 는 처음부터 EdgeSelection 노출했지만 EdgeToLineDistance/EdgeToLineAngle/ArcEdgeDistance 는 옛 패턴 유지.

## INI 하위호환 매트릭스

| 측정 타입 | 레거시 INI 의 EdgeSelection 키 | 동작 |
|-----------|--------------------------------|------|
| EdgeToLineDistance | 존재 (Phase 23.1 이전부터) | 로드 후 정상 사용 |
| EdgeToLineAngle | 존재하지 않음 | ParamBase 폴백 = 기본값 "All" |
| ArcEdgeDistance | 존재하지 않음 | ParamBase 폴백 = 기본값 "All" |

→ 모든 케이스에서 회귀 0.

## Threats Mitigated

| Threat ID | Disposition | Result |
|-----------|-------------|--------|
| T-Q-04 (INI 회귀) | mitigate | strip-loop 안전 + edgeCount<2 가드 + 기본값 "All" |
| T-Q-05 (신규 INI 키) | accept | ParamBase 폴백 = 기본값 (회귀 0) |
| T-Q-06 (stripCount=1 + First → 1점) | accept | TryFitLine L161-166 edgeCount<2 명시 에러 |

## Files Modified

- `EdgeToLineDistanceMeasurement.cs` — 7 insertions / 47 deletions (ICustomTypeDescriptor 구현 8 메서드 + BuildFilteredProperties 제거가 큰 deletion)
- `EdgeToLineAngleMeasurement.cs` — 7 insertions / 2 deletions
- `ArcEdgeDistanceMeasurement.cs` — 7 insertions / 2 deletions

## Next Steps

- **사용자 SIMUL UAT 필요:** 3 측정 타입 각각 PropertyGrid 에 EdgeSelection 드롭다운 표시 + First/Last/All 선택 시 정상 측정 + INI Save/Load 라운드트립 확인
- (선택) 측정 결과가 기존 (모두 "All") 과 동일 시나리오에서 First/Last 선택 시 측정값이 의미 있게 변하는지 확인 — 디버깅용 옵션 동작 검증
