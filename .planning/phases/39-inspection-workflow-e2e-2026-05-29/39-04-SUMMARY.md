---
phase: 39-inspection-workflow-e2e-2026-05-29
plan: 04
type: summary
status: complete
date: 2026-05-29
files_modified:
  - .planning/phases/39-inspection-workflow-e2e-2026-05-29/39-UAT.md
hotfixes:
  - id: CO-39-01
    commit: 13e735e
    files: [Action_FAIMeasurement.cs]
  - id: CO-39-02
    commit: af3f608
    files: [DatumConfig.cs, InspectionSequence.cs, Action_FAIMeasurement.cs, HalconDisplayService.cs]
  - id: CO-39-03
    commit: 8a3d2f6
    files: [HalconDisplayService.cs]
requirements_addressed: [WF-01, WF-02]
depends_on: [39-01, 39-02, 39-03]
---

# Plan 39-04 SUMMARY — SIMUL UAT sign-off

## Final State

| 항목 | 값 |
|------|-----|
| status | signed_off |
| signed_off_by | heobum0928-art |
| signed_off_date | 2026-05-29 |
| Test 결과 | 5/5 PASS |
| Hotfix 적용 | 3건 (CO-39-01/02/03, UAT 도중) |

## Test 결과 요약

| Test | 시나리오 | 결과 | 사용자 확인 방식 |
|------|----------|------|------------------|
| Test 1 | OK 정상 | PASS | Nominal/Tolerance 조정으로 OK 정상 표시 (리스트박스) |
| Test 2 | NG 1건 | PASS | Tolerance 좁게 → NG 정상 표시 (리스트박스) |
| Test 3 | 검출실패 1건 | PASS | Bottom_Datum.TeachingImagePath 가짜 → 라벨 + 트리 표시 (CO-39-01/02/03 hotfix 후) |
| Test 4 | NG 누적 | PASS | Test 2 자연 확장 |
| Test 5 | multi-datum 부분 실패 | PASS | Test 3 와 동일 코드 경로, 사용자 "다른 shot 정상 검사" 보고로 lenient 회귀 0 확인 |

## Hotfix 이력 (UAT 도중)

### CO-39-01 — LastFindSucceeded 세팅 누락 (commit 13e735e)

**증상:** Datum.TeachingImagePath 가짜 경로 설정 후 검사 시 게이트는 정상 동작 (datum-skip) 하나 HALCON 'DETECT FAIL' 라벨 미표시.

**원인:** Action_FAIMeasurement 의 4 실패 분기 중 이미지 취득 실패 2건 (DualImage / 1-image GrabOrLoadDatumImage null) 이 TryRunSingleDatum 을 호출하지 않고 continue 하여 `datum.LastFindSucceeded` 가 이전 상태 유지. HalconDisplayService.RenderDatumOverlay 의 분기 조건 (`IsConfigured && !LastFindSucceeded`) 미충족.

**수정:** Action_FAIMeasurement 이미지 취득 실패 2 분기에 `datum.LastFindSucceeded = false;` 1줄씩 추가. TryRunSingleDatum 호출 후 실패 분기 2건은 InspectionSequence 가 이미 처리하므로 무수정.

### CO-39-02 — 강력 모드 DETECT FAIL 라벨 (commit af3f608)

**증상:** CO-39-01 적용 후에도 라벨 미표시.

**원인:** 사용자가 사전 티칭하지 않은 datum (`IsConfigured=false`) → 분기 미진입. 또한 사전 티칭 안 한 datum 은 `RefOriginRow=0` → 라벨 좌표 `RefOriginRow - 40 = -40` → 화면 밖.

**수정 (4 파일):**
- `DatumConfig.RuntimeDetectFailed` 휘발성 INPC 프로퍼티 신규 + `HasDetectFail` computed 식을 (RuntimeDetectFailed OR 기존 조건) 으로 확장
- `InspectionSequence.ClearDatumTransforms`: DatumConfigs 순회 RuntimeDetectFailed false 일괄 리셋 (이전 사이클 잔여 신호 제거)
- `Action_FAIMeasurement`: 4 실패 분기 모두에 `datum.RuntimeDetectFailed = true` 추가
- `HalconDisplayService.RenderDatumOverlay`: 분기 조건 `(RuntimeDetectFailed || (IsConfigured && !LastFindSucceeded))` — 티칭 여부 무관. 좌표 fallback: RefOrigin <= 0 이면 화면 좌상단 (50, 50)

### CO-39-03 — 라벨 위치 우상단 + datum 이름 (commit 8a3d2f6)

**사용자 요청:** "Datum origin 위에 표시 → 이미지 오른쪽 상단에 나오면 좋겠음"

**수정 (1 파일):**
- `HalconDisplayService`: `GetPart(window)` 로 현재 표시 영역 좌표 동적 계산 → window 크기 무관 항상 우상단
- `labelRow = partRow1 + 20 + hashStagger` (datum 이름 hash 기반 25px stagger, 6단계 0/25/50/75/100/125)
- `labelCol = partCol2 - 280` (오른쪽 가장자리에서 280px 안쪽)
- 라벨 텍스트: `"DETECT FAIL"` → `"DETECT FAIL: {DatumName}"` — 어느 datum 실패인지 즉시 식별

## 회귀 가드 결과

| 항목 | 결과 |
|------|------|
| Phase 7-02 overlay suffix (-OK/-NG) | ✓ Test 1, 2, 4 |
| Phase 17 D-13 RenderDatumFindResult (purple 십자) | ✓ Test 1, 2, 4 |
| Phase 36 hotfix CO-36-03 (RenderDatumFindResult 호출 위치) | ✓ Test 1, 2, 4 |
| Phase 37 D-37-03 lenient (부분 실패 시 다른 datum 진행) | ✓ Test 3, 5 — 사용자 직접 확인 |
| Phase 22 IMG-02 (TeachingImagePath / SimulImagePath 분리) | ✓ Test 3 시나리오 구성 |
| Phase 20 D-12 marker stacking | ✓ 기존 마커 100% 보존, //260529 hbk Phase 39 누적 |
| CO-22-01 PropertyGrid 전환 | ✓ Test 3 시나리오 중 노드 전환 정상 |

## Phase 39 종료 후 다음 단계

- **다음 phase**: Phase 40 (OUT-01/02 — 결과 분석 & Export) 또는 v1.2 우선순위 따라 선택
- **잔여 carry-over**: 없음 (CO-39-01/02/03 는 UAT 도중 모두 해소됨)
- **XAML DataTemplate 적색 dot**: Plan 03 anti-goal 로 명시된 별도 작업 — 후속 plan 또는 사용자 요청 시 진행
- **실HW UAT**: Phase 44 (CO-38-04) 로 분리
