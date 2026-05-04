---
phase: 13-datum-algorithm-extensibility
plan: 01
subsystem: halcon-algorithms
tags: [datum, algorithm, validation, direction-check, req-5d, phase-13]
dependency_graph:
  requires:
    - phase-12-plan-02: TryTeachCircleTwoHorizontal / TryTeachVerticalTwoHorizontal success branch (기준값 저장 완료 상태)
    - phase-12-plan-01: DatumConfig.RefAngleRad / LastTeachSucceeded 필드
  provides:
    - ValidateHorizontalVerticalAngles: CircleTwoHorizontal + VerticalTwoHorizontal 방향 정합성 게이트
  affects:
    - phase-13-plan-02: DatumFindingService API 변경 없음 (private helper 전용)
tech_stack:
  added: []
  patterns:
    - "private static bool helper + 2 const 상수 패턴 (MIN_HORIZONTAL_EDGES 선례 확장)"
    - "LastTeachSucceeded=false 복원 + error 전달 후 return false 패턴"
    - "수평 phi [-90,+90] normalize → 절댓값 비교 패턴"
    - "각도 교차편차 [0,180) normalize → 90° 편차 비교 패턴"
key_files:
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - "에러 리터럴에 ° (Unicode degree) 대신 ASCII ' deg' 사용 — C# 소스 파일 인코딩 안전성 확보 (Plan 허용 조항)"
  - "ValidateHorizontalVerticalAngles 는 try/catch 없음 — 순수 수학 연산이라 예외 불발생, PATTERNS.md §B 가이드 준수"
  - "빌드 검증을 메인 레포지토리(/WPF_Example) 기준으로 수행 — worktree 환경에 PropertyTools.Wpf / WPF.MDI DLL 미포함(MC3074 XAML 에러)이므로"
metrics:
  duration: "~15 min"
  completed: "2026-04-25"
  tasks_completed: 3
  files_modified: 1
---

# Phase 13 Plan 01: Datum Direction Validation (Req 5d) Summary

**One-liner:** `ValidateHorizontalVerticalAngles` private static helper + 임계각 상수 2개 추가로 CircleTwoHorizontal / VerticalTwoHorizontal 티칭 시 수평 방향(±15°) + 수직 직각성(90°±5°) 검증 게이트 활성화.

---

## Files Changed

| File | Change | Lines Added | Lines Removed |
|------|--------|-------------|---------------|
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | 상수 2개 + helper 1개 추가, CircleTwoHorizontal TODO 교체, VerticalTwoHorizontal 검증 삽입 | +60 | -1 |

---

## Tasks Completed

| Task | Name | Commit | Key Changes |
|------|------|--------|-------------|
| 1 | Add ValidateHorizontalVerticalAngles helper + 2 constants | `09eee3d` | HORIZONTAL_TOLERANCE_DEG=15.0, PERPENDICULAR_TOLERANCE_DEG=5.0, ValidateHorizontalVerticalAngles 정의 |
| 2 | Wire validation into TryTeachCircleTwoHorizontal | `2ed08e9` | L377 TODO 제거, vertPhiCircle=PI/2 + helper 호출 블록 삽입 |
| 3 | Wire validation into TryTeachVerticalTwoHorizontal | `4ee7369` | vertPhiDetected=Atan2(vrE-vrB, vcE-vcB) + helper 호출 블록 삽입 |

---

## Req 5d Coverage Mapping

| 요구사항 | 경로 | 수직 phi 계산 | 호출 위치 |
|----------|------|---------------|-----------|
| CircleTwoHorizontal 수평 방향 검증 | TryTeachCircleTwoHorizontal | Math.PI / 2.0 (수직 가상선 고정) | LastTeachSucceeded=true 직후 |
| VerticalTwoHorizontal 수평 방향 검증 | TryTeachVerticalTwoHorizontal | Math.Atan2(vrE-vrB, vcE-vcB) (검출 수직선) | LastTeachSucceeded=true 직후 |
| TwoLineIntersect | 호출 없음 (D-12 scope 제한) | — | — |
| TryFindDatum (런타임) | 호출 없음 (Phase 12 Out-of-scope 유지) | — | — |

---

## Error Literal Catalog (신규 2개)

| 에러 ID | 리터럴 Prefix | 전체 포맷 예시 |
|---------|--------------|---------------|
| Req-5d-Horizontal | `"Horizontal line orientation out of range: "` | `"Horizontal line orientation out of range: 18.3 deg (expected +/-15.0 deg)"` |
| Req-5d-Perpendicularity | `"Horizontal/Vertical perpendicularity violated: delta="` | `"Horizontal/Vertical perpendicularity violated: delta=78.2 deg (expected 90 +/-5.0 deg)"` |

---

## Build Result

```
빌드 환경: 메인 레포지토리 /WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64
결과: DatumMeasurement -> ...\bin\x64\Debug\DatumMeasurement.exe (성공)
DatumFindingService.cs 신규 warning: 0건
기존 경고 (Plan scope 외):
  - VirtualCamera.cs CS0162 (접근할 수 없는 코드)
  - VisionAlgorithmService.cs CS0219 (미사용 변수)
  - MSB3884 MinimumRecommendedRules.ruleset
```

---

## Deviations from Plan

**Plan Adherence: All tasks executed as specified.**

한 건의 소폭 조정:
- **에러 리터럴 `°` → ` deg`:** Plan L175에서 명시적으로 허용한 ASCII-only 대안 사용. Plan must_haves에서 요구한 prefix(`"Horizontal line orientation out of range:"`, `"Horizontal/Vertical perpendicularity violated:"`)는 정확히 그대로 유지됨.

---

## Known Stubs

없음 — 이 Plan은 DatumFindingService.cs 내부 private 로직만 수정하며 UI 렌더링이나 외부 데이터 흐름에 관여하지 않음.

---

## Threat Flags

없음 — 이 Plan은 기존 public API 시그니처를 변경하지 않으며 새로운 네트워크 엔드포인트, 파일 접근 경로, 또는 인증 경로를 추가하지 않음.

---

## Self-Check

- [x] `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` 존재 확인
- [x] `HORIZONTAL_TOLERANCE_DEG` 1건 확인 (`grep -cF` → 1)
- [x] `PERPENDICULAR_TOLERANCE_DEG` 1건 확인 (`grep -cF` → 1)
- [x] `ValidateHorizontalVerticalAngles(` 3건 확인 (정의 1 + 호출 2)
- [x] `Horizontal line orientation out of range:` 1건 확인
- [x] `Horizontal/Vertical perpendicularity violated:` 1건 확인
- [x] `// TODO: Phase 13` 0건 확인 (제거됨)
- [x] `//260424 hbk Phase 13` 8건 확인 (≥6 기준 충족)
- [x] TwoLineIntersect 경로 ValidateHorizontalVerticalAngles 호출 0건 확인
- [x] TryFindDatum 경로 ValidateHorizontalVerticalAngles 호출 0건 확인
- [x] 커밋 09eee3d 존재 확인
- [x] 커밋 2ed08e9 존재 확인
- [x] 커밋 4ee7369 존재 확인
- [x] msbuild Debug/x64 성공 + DatumFindingService.cs 신규 warning 0건

## Self-Check: PASSED
