---
phase: quick-260623-itv
plan: 01
subsystem: Utility/Ini
tags: [refactor, conventions, QUAL-01]
dependency_graph:
  requires: []
  provides: [IniValue 좌표 파서 const화]
  affects: [WPF_Example/Utility/Ini.cs]
tech_stack:
  added: []
  patterns: [매직넘버 const화, 헝가리언 지역변수, early-return 유지]
key_files:
  modified: [WPF_Example/Utility/Ini.cs]
decisions:
  - "TryParseCircle/Line/Rect 3종만 변경, 나머지 공개 API 전혀 불변"
  - "헤더 주석을 파일 최상단 1줄로만 제한"
  - "null guard (strArray == null) 동작 보존 — 방어 코드 삭제 않음"
metrics:
  duration: "5m"
  completed: "2026-06-23"
  tasks_completed: 2
  files_modified: 1
---

# Quick 260623-itv: Ini.cs 리팩토링 Summary

**One-liner:** IniValue 좌표 파서 3종(TryParseCircle/Line/Rect)에 매직넘버 const화 + 헝가리언 지역변수 적용. 공개 API·직렬화 100% 불변.

## 변경된 3개 메서드 핵심

### TryParseCircle
- `strArray` → `szParts`, 공유 `double value` 중간변수 제거
- `strArray.Length < 3` → `szParts.Length < CIRCLE_FIELD_COUNT`
- `strArray[0/1/2]` → `szParts[FIELD_X/FIELD_Y/FIELD_W]`
- `double x = value; double y = value; double radius = value;` → `double dX; double dY; double dRadius;` (직접 out 수신)

### TryParseLine
- `strArray` → `szParts`, 공유 `double value` 제거
- `strArray.Length < 4` → `szParts.Length < LINE_FIELD_COUNT`
- 인덱스 0/1/2/3 → `FIELD_X/Y/W/H`
- 혼동 소지 주석 추가: `// szParts: X1,Y1,X2,Y2 순서`

### TryParseRect
- `strArray` → `szParts`, 공유 `double value` 제거
- `strArray.Length < 4` → `szParts.Length < RECT_FIELD_COUNT`
- 인덱스 0/1/2/3 → `FIELD_X/Y/W/H`
- 기존 잘못된 들여쓰기(TryParseLine과 혼재된 공백) 정돈

## 추가된 const (IniValue struct 상단)

```csharp
private const int CIRCLE_FIELD_COUNT = 3;
private const int LINE_FIELD_COUNT = 4;
private const int RECT_FIELD_COUNT = 4;
private const int FIELD_X = 0;
private const int FIELD_Y = 1;
private const int FIELD_W = 2;   // Circle: radius
private const int FIELD_H = 3;
```

## 빌드 결과

- MSBuild Debug/x64: **0 errors** (warning 6종 기존과 동일 — Ini.cs 무관)
- 출력: `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` 생성 확인

## 공개 API 불변 확인

git diff 검토 결과:
- `public struct IniValue` 시그니처 변경 없음
- `TryConvertCircle/Line/Rect`, `ToCircle/Line/Rect` 시그니처 변경 없음
- `implicit/explicit operator` 전부 변경 없음
- `IDictionary<,>/ICollection<,>/IDisposable` 명시적 인터페이스 구현 변경 없음
- Save/Load/LoadValue 직렬화 로직 변경 없음
- Ordered 관련 예외 메시지 문자열 변경 없음
- `#if JS` 블록 변경 없음
- 변경 범위: TryParseCircle/Line/Rect 내부 + const 선언 7개 + 헤더 주석 1줄

## Commit Hash(es)

- `fc74fe1`: refactor(quick-260623-itv-01): IniValue 좌표 파서 const화 및 지역변수 헝가리언 적용

## Deviations from Plan

None - 플랜 그대로 실행. 헤더 주석은 Task 2가 아닌 Task 1 커밋에 함께 포함됨(동일 커밋 내 1줄 추가, 동치).

## Self-Check: PASSED

- [x] WPF_Example/Utility/Ini.cs 존재
- [x] 커밋 fc74fe1 존재
- [x] 빌드 0 errors 확인
- [x] 3개 FIELD_COUNT const 존재 (grep 확인)
- [x] 공개 API 라인 diff에 미등장
