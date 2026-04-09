---
status: partial
phase: 04-datum
source: [04-01-SUMMARY.md, 04-02-SUMMARY.md]
started: 2026-04-09T18:00:00Z
updated: 2026-04-09T18:30:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Datum 트리 노드 표시
expected: InspectionListView에서 레시피를 로드하면 각 Shot(Action) 노드 아래에 "Datum" 자식 노드가 FAI 노드보다 앞에 표시된다. layout.png 아이콘이 사용된다.
result: pass

### 2. Datum PropertyGrid 바인딩
expected: Datum 노드를 클릭하면 PropertyGrid에 DatumConfig 필드가 표시된다 — Line1 ROI(Row/Col/Phi/Length1/Length2), Line2 ROI, Reference(RefOriginRow/Col/AngleRad), Edge Detection(EdgeThreshold/Sigma/EdgePolarity), IsConfigured. FAI 추가/삭제/수정 버튼은 비활성화된다.
result: pass

### 3. Datum INI 레시피 저장/로드
expected: Datum PropertyGrid에서 값을 수정한 후 레시피를 저장하고 앱을 재시작하면 수정한 Datum 값이 그대로 유지된다. INI 파일에 [SHOT_0_DATUM] 등의 섹션이 생성되어 있다.
result: pass

### 4. 기존 레시피 하위 호환
expected: Phase 4 이전에 저장한 레시피(DATUM 섹션 없음)를 로드해도 에러 없이 정상 로드된다. Datum은 미설정 상태(IsConfigured=false)로 표시된다.
result: issue
reported: "이전꺼 불러도 현재랑 동일하게 나와있음 — DatumConfig 기본값이 0이 아닌 값(Length1=100 등)이라 미설정과 구분 불가"
severity: major
fix: DatumConfig 기본값을 모두 0으로 변경 — 적용 후 재테스트 pass

### 5. Datum 미설정 시 무보정 측정 (Identity Pass-through)
expected: Datum이 미설정(IsConfigured=false) 상태에서 FAI 측정을 실행하면 Phase 3과 동일하게 원본 ROI 위치에서 에지 측정이 수행된다. 보정 없이 정상 동작한다.
result: blocked
blocked_by: prior-phase
reason: "검사 시퀀스가 등록되지 않아 측정 실행 불가 — Phase 5 (검사 시퀀스 & TCP)에서 시퀀스 등록 후 테스트 가능"

### 6. Datum 측정 파이프라인 통합
expected: Datum이 설정된 상태에서 검사를 실행하면 Action_FAIMeasurement가 FindDatum을 먼저 실행한 후 FAI 측정을 수행한다. datumTransform이 FAI ROI에 적용되어 보정된 위치에서 측정된다.
result: blocked
blocked_by: prior-phase
reason: "검사 시퀀스가 등록되지 않아 측정 실행 불가 — Phase 5에서 테스트 가능"

### 7. Datum 실패 시 FAI 전체 NG 처리
expected: Datum이 설정된 상태에서 FindDatum이 실패하면(에지를 찾지 못함) 해당 Shot의 모든 FAI가 ClearResult 처리되고 AllPass=false가 된다. 개별 FAI 측정은 수행되지 않는다.
result: blocked
blocked_by: prior-phase
reason: "검사 시퀀스가 등록되지 않아 측정 실행 불가 — Phase 5에서 테스트 가능"

## Summary

total: 7
passed: 3
issues: 1
pending: 0
skipped: 0
blocked: 3

## Gaps

- truth: "기존 레시피 하위 호환 — 미설정 Datum이 기본값과 구분 가능"
  status: fixed
  reason: "DatumConfig 기본값이 Line1_Length1=100 등 비-0 값이라 이전 레시피 로드 시 미설정과 구분 불가"
  severity: major
  test: 4
  fix_applied: "DatumConfig.cs 기본값을 모두 0으로 변경, 재테스트 통과"
