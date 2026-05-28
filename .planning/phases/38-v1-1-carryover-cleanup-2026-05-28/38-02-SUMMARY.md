---
phase: 38-v1-1-carryover-cleanup-2026-05-28
plan: "02"
subsystem: DatumConfig
tags:
  - datum-config
  - angle-tolerance
  - property-grid
  - ini-compat
  - cleanup
dependency_graph:
  requires: []
  provides:
    - "AngleTolerance 기본 OFF (0.0 sentinel)"
    - "TwoLineAngleToleranceDeg PropertyGrid 숨김"
    - "ReuseFromShotName 완전 제거"
  affects:
    - "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
tech_stack:
  added: []
  patterns:
    - "IsHiddenForAlgorithm switch 진입 전 무조건 숨김 패턴"
key_files:
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
decisions:
  - "AngleTolerance 기본값 1.0→0.0 (sentinel=off): 신규 Datum 배지 미표시로 혼란 제거. 기존 INI에 값이 있으면 ParamBase Load가 덮어씀(하위호환 우선)."
  - "TwoLineAngleToleranceDeg PropertyGrid 숨김은 IsHiddenForAlgorithm switch 진입 전 무조건 처리로 구현 — 모든 알고리즘 케이스를 단일 라인으로 커버."
  - "ReuseFromShotName 제거 후 INI 하위호환: ParamBase.Load는 GetType().GetProperties()로 프로퍼티를 기준 루프하므로 INI 키가 있어도 매칭 프로퍼티 없으면 무시(skip)."
  - "저위험 주석 정리 (D-13): DatumConfig.cs에서 명백히 dead인 주석 없음 — ReuseFromShotName 3줄 삭제 외 주석 변경 없음."
metrics:
  duration: "4m 14s"
  completed: "2026-05-28"
  tasks_completed: 2
  tasks_total: 2
  files_modified: 1
---

# Phase 38 Plan 02: DatumConfig 각도 파라미터 UI 정리 + ReuseFromShotName 제거 Summary

**한 줄 요약:** AngleTolerance 기본 0.0(배지 OFF sentinel), TwoLineAngleToleranceDeg PropertyGrid 전체 숨김, 미사용 ReuseFromShotName 완전 제거 — 검사 게이트 로직 회귀 0 + INI 하위호환 유지.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | AngleTolerance 기본 OFF + TwoLineAngleToleranceDeg PropertyGrid 숨김 (#6 D-12) | be7f5ab | DatumConfig.cs |
| 2 | ReuseFromShotName 완전 제거 + 저위험 주석 정리 (#12 D-04/D-05/D-06, #10 D-13) | 67c3ccc | DatumConfig.cs |

## Changes Made

### Task 1: AngleTolerance 기본 OFF + TwoLineAngleToleranceDeg PropertyGrid 숨김

**변경 파일:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`

**(a) AngleTolerance 기본값 1.0 → 0.0**

```csharp
// 변경 전
public double AngleTolerance { get; set; } = 1.0; //260528 hbk Phase 36 D-36-05/07/13

// 변경 후
public double AngleTolerance { get; set; } = 0.0; //260528 hbk Phase 36 D-36-05/07/13 //260528 hbk Phase 38 #6
```

DatumFindingService.cs:739의 `if (config.AngleTolerance > 0.0)` 게이트가 0.0이면 `AngleValidationStatus=None`(배지 미표시)으로 작동 — DatumFindingService.cs 무수정.

**(b) TwoLineAngleToleranceDeg PropertyGrid 숨김**

`IsHiddenForAlgorithm` 메서드의 `switch(alg)` 진입 직전에 무조건 숨김 한 줄 추가:

```csharp
private static bool IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) {
    if (name == "TwoLineAngleToleranceDeg") return true; //260528 hbk Phase 38 #6 D-12 — 모든 알고리즘에서 PropertyGrid 숨김 (직각 게이트 로직은 무변경)
    switch (alg) {
        ...
    }
}
```

- 필드 정의(L108) 및 INI 직렬화는 제거하지 않음 → 직각 게이트(DatumFindingService.cs:957-975 default 10°) 안전망 보존
- PropertyGrid 노출만 차단

### Task 2: ReuseFromShotName 완전 제거

**변경 파일:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`

삭제된 3줄:

```csharp
//260413 hbk Phase 6: ReuseFromShot 모드일 때 재사용할 Shot 이름 (D-07)
[Category("Datum|ImageSource")]
public string ReuseFromShotName { get; set; } = "";
```

- `SourceShotName`(L36)은 InspectionListView.xaml.cs:702-703 실사용처가 있으므로 유지
- ParamBase reflection 직렬화이므로 프로퍼티 삭제만으로 INI Save/Load 자동 제외

**저위험 주석 정리 (D-13):** DatumConfig.cs 파일 내 명백히 dead인 주석 없음 — 정리 대상 없음.

## INI 하위호환 검증 (D-06 / T-38-03)

**ParamBase.Load 미지원 키 처리 동작 (코드 인용):**

```csharp
// WPF_Example/Sequence/Param/ParamBase.cs L369-430
public virtual bool Load(IniFile loadFile, string group) {
    PropertyInfo[] props = GetType().GetProperties();  // ← 프로퍼티 기준 루프
    foreach (var prop in props) {
        string name = prop.Name;
        string type = prop.PropertyType.Name;
        try {
            switch (type) {
                case "String":
                    string sValue = loadFile[group][name].ToString();
                    prop.SetValue(this, sValue);
                    break;
                // ...
            }
        }
        catch(Exception e) { ... }
    }
    return true;
}
```

**결론:** Load는 `GetType().GetProperties()`로 **프로퍼티 목록을 기준**으로 루프하며, 각 프로퍼티 이름으로 INI 값을 조회합니다. `ReuseFromShotName` 프로퍼티를 삭제하면 이 루프에서 해당 프로퍼티가 나타나지 않으므로, INI 파일에 `ReuseFromShotName` 키가 있어도 **아무 프로퍼티에도 매핑되지 않아 조용히 무시**됩니다. 예외 발생 없음, 다른 프로퍼티 로드 영향 없음 (D-06 요건 충족).

마찬가지로 `AngleTolerance` 키가 기존 INI에 있으면 ParamBase Load가 그 값으로 `AngleTolerance` 프로퍼티를 덮어씀(기본값 0.0보다 INI 값 우선) → 기존 레시피 하위호환 유지.

`TwoLineAngleToleranceDeg`는 필드/직렬화를 유지하므로 기존 INI 로드/저장 회귀 없음.

## Verification Results

| 검증 항목 | 결과 |
|-----------|------|
| AngleTolerance = 0.0 | PASS (grep 확인) |
| TwoLineAngleToleranceDeg PropertyGrid 숨김 | PASS (IsHiddenForAlgorithm에 `return true` 확인) |
| TwoLineAngleToleranceDeg 필드/직렬화 보존 | PASS (L108 정의 유지) |
| DatumFindingService.cs:739 게이트 무변경 | PASS (파일 미수정 확인) |
| DatumFindingService.cs:957-975 직각 게이트 무변경 | PASS (파일 미수정 확인) |
| ReuseFromShotName 0 매치 | PASS (grep WPF_Example/ → 0 결과) |
| SourceShotName 유지 | PASS (DatumConfig.cs + InspectionListView.xaml.cs 3곳 확인) |
| msbuild Debug/x64 | PASS (메인 프로젝트 임시 복사 검증, 신규 warning 0) |
| INI 하위호환 | PASS (ParamBase.Load 코드 인용으로 검증) |

## Deviations from Plan

없음 - 플랜 그대로 실행.

## Known Stubs

없음.

## Threat Flags

없음 — 오프라인 Windows 산업용 데스크톱 앱, 신규 외부/네트워크 인터페이스 없음.

## Self-Check: PASSED

| Item | Status |
|------|--------|
| WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs | FOUND |
| .planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-02-SUMMARY.md | FOUND |
| Commit be7f5ab (Task 1) | FOUND |
| Commit 67c3ccc (Task 2) | FOUND |
