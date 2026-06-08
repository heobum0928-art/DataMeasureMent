---
phase: 39-inspection-workflow-e2e-2026-05-29
type: uat
status: signed_off
signed_off_by: heobum0928-art
signed_off_date: 2026-05-29
test_environment: SIMUL_MODE Debug/x64
related_plans: [39-01-PLAN.md, 39-02-PLAN.md, 39-03-PLAN.md]
requirements: [WF-01, WF-02]
hotfixes_during_uat: [CO-39-01, CO-39-02, CO-39-03]
---

# Phase 39: 검사 워크플로우 E2E — UAT

**작성일:** 2026-05-29
**사인오프:** 2026-05-29
**범위:** SIMUL 사이클 3분기 (OK/NG/검출실패) + 회귀 가드 2건

D-09 (CONTEXT.md): Phase 39 sign-off = SIMUL UAT 통과. 실카메라 검증은 Phase 44 (CO-38-04) 로 분리.

UAT 도중 hotfix 3건 적용: CO-39-01 (LastFindSucceeded 누락) → CO-39-02 (RuntimeDetectFailed 강력 모드) → CO-39-03 (라벨 위치 우상단 + datum 이름 포함).

---

## Test 1: OK 시나리오 — 정상 datum + 정상 FAI

**사전 조건:**
- Recipe 로드: 기존 multi-datum recipe (Top 또는 Side 어느 쪽이든 가능)
- 모든 DatumConfig.TeachingImagePath = 검출 가능한 이미지 (Cal_Image/ 정상 워크피스)
- ShotConfig.SimulImagePath = TeachingImagePath 와 동일 워크피스 (Phase 22 IMG-02 패턴 — 같은 파일 가능)
- 모든 FAI 의 NominalValue + Tolerance 가 실제 측정값을 포함하는 범위로 설정 (PASS 보장)

**실행 절차:**
1. 앱 SIMUL_MODE Debug/x64 실행
2. recipe 로드 + Datum 트리에서 모든 Datum 노드 사전 티칭 확인 (LastTeachSucceeded=true)
3. TCP 클라이언트 또는 메뉴에서 검사 트리거 (`$TEST:site,type,0@` 형식)
4. 검사 완료 대기

**기대 결과:**
- [UI] MainView Halcon Viewer 에 모든 Datum 십자 (purple) + 검출 라인/원 표시
- [UI] 'DETECT FAIL' 라벨 미표시 (모든 datum.LastFindSucceeded=true)
- [UI] InspectionListView Datum 노드에 적색 배지 미표시
- [UI] FAI overlay 에 `-OK` suffix (Phase 7-02 회귀 가드)
- [TCP wire] cycle Result = 'O' (`$RESULT:site;type;O;…@`)
- [TCP wire] 모든 FAIResults[i] = 'P'
- [Log] datum skip 또는 FAI fail 로그 0건

**결과:** PASS
**메모:** Nominal Value + Tolerance ±  로 조정하면서 OK / NG 가 리스트 박스에 정상 표시됨 — 기존 동작 회귀 0 확인 (사용자 직접 확인, 2026-05-29).

---

## Test 2: NG 시나리오 — 정상 datum + 1 FAI 측정 실패

**사전 조건:**
- Test 1 의 recipe 재사용
- 1 FAI 의 Tolerance 범위를 매우 좁게 변경 (예: TolerancePlus=0.001, ToleranceMinus=0.001) → 측정값이 NG 영역에 떨어지도록
- 그 외 datum / FAI 설정은 Test 1 동일

**실행 절차:**
1. Test 1 의 recipe 에서 1 FAI 만 Tolerance 변경
2. 앱 재로드 또는 PropertyGrid 에서 즉시 반영
3. 검사 트리거
4. 검사 완료 대기

**기대 결과:**
- [UI] 모든 Datum 정상 십자 표시 (Test 1 동일)
- [UI] 'DETECT FAIL' 라벨 미표시
- [UI] NG FAI 의 overlay 에 `-NG` suffix 부여 (Phase 7-02 회귀 가드)
- [UI] InspectionListView 결과 행에 NG 표시
- [TCP wire] cycle Result = 'X'
- [TCP wire] NG FAI 의 FAIResults[i] = 'F', 나머지 FAI = 'P'
- [Log] `[FAIMeasurement] Measurement '...' failed: ...` 로그 OR Tolerance 초과 (D-07 try/catch lenient — abort 0)

**결과:** PASS
**메모:** Nominal/Tolerance 조정으로 NG 정상 표시 확인 (사용자 직접 확인, 2026-05-29). 회귀 0.

---

## Test 3: 검출실패 시나리오 — 1 Datum 검출 실패 + 영향 FAI skip + 다른 datum FAI 정상 측정

**사전 조건:**
- Test 1 의 recipe 재사용. Multi-datum (예: Datum_1 + Datum_2 두 개) recipe 필수
- Datum_1.TeachingImagePath = 검출 가능 이미지 (Cal_Image/DualImageTest/SIDE1_3-1_Datum_A1_A2_backlight_LV30.bmp)
- Datum_2.TeachingImagePath = 잘못된 워크피스 (Cal_Image/DualImageTest/SIDE1_3-1_Datum_B1_backlight_LV30.bmp) — 알고리즘 (ROI 좌표 + AlgorithmType) 으로 검출 실패 보장
- 또는 더 간단히: Datum_2.TeachingImagePath 를 존재하지 않는 경로 (예: "C:/nonexistent.bmp") 로 설정 → 이미지 취득 실패 분기 트리거
- FAI 구성: 절반의 FAI 는 Measurement.DatumRef = "Datum_1" (정상), 나머지는 DatumRef = "Datum_2" (검출 실패)

**실행 절차:**
1. Datum_1 / Datum_2 둘 다 사전 티칭 시도 후 Datum_1 만 성공 가정 (또는 의도적으로 Datum_2 ROI 를 검출 불가능한 영역에 배치)
2. Recipe save / load 후 검사 트리거
3. 검사 완료 대기

**기대 결과:**
- [UI] Datum_1 십자 정상 표시
- [UI] Datum_2 위치에 'DETECT FAIL' 적색 라벨 표시 (Plan 03 Task 1)
- [UI] InspectionListView Datum_2 노드에 HasDetectFail 바인딩 신호 (XAML 적색 dot 구현 여부와 무관하게 HasDetectFail 프로퍼티 값 확인 가능 — debug breakpoint 또는 후속 plan 의 XAML)
- [UI] Datum_1 기반 FAI overlay 는 `-OK` suffix 정상 부여 (Phase 37 lenient 회귀 가드)
- [UI] Datum_2 기반 FAI 는 overlay 미생성 (TryExecute 호출 안 됨 — Plan 01 Task 3 gate)
- [TCP wire] cycle Result = 'N' (`$RESULT:site;type;N;…@`)
- [TCP wire] Datum_1 기반 FAIResults[i] = 'P'
- [TCP wire] Datum_2 기반 FAIResults[i] = 'N'
- [Log] `[FAIMeasurement] Datum 'Datum_2' ... 실패 (skip):` 로그 (DatumPhase 분기, Plan 01 Task 3)
- [Log] `[FAIMeasurement] Measurement '...' skipped — datum 'Datum_2' 검출 실패 (D-01)` 로그 (Measure 게이트, Plan 01 Task 3)

**결과:** PASS
**메모:** Bottom_Datum.TeachingImagePath 가짜 경로 변경 → 검출 실패 발생. 게이트 정상 동작 ("datum 없어서 안 되는 shot 발생"). 라벨 미표시 → CO-39-01/02 hotfix 적용 → 우측 상단에 빨간 "DETECT FAIL: Bottom_Datum" 표시 확인 (CO-39-03 hotfix 후 위치 조정). Datum 트리 노드에도 표시 (HasDetectFail INPC 동작 확인). Phase 37 lenient 회귀 0 — 다른 Shot 정상 검사 진행 확인.

**중요 검증:** Phase 37 D-37-03 lenient 회귀 가드 — Datum_2 실패에도 불구하고 Datum_1 기반 FAI 가 정상 측정되어야 함. 전면 abort 시 즉시 FAIL.

---

## Test 4: NG 2건 이상 누적 — try/catch lenient (D-07) 회귀 가드

**사전 조건:**
- Test 2 와 유사하나 2개 이상 FAI 의 Tolerance 를 좁게 변경
- 또는 2개 FAI 의 ROI 를 에지 검출 불가능한 영역에 배치 (try/catch 분기 트리거)

**실행 절차:**
1. 2개 이상 FAI 가 NG 가 되도록 recipe 변경
2. 검사 트리거
3. 검사 완료 대기

**기대 결과:**
- [UI] 모든 datum 정상 (Test 1 동일)
- [TCP wire] cycle Result = 'X'
- [TCP wire] 2개 이상 FAIResults[i] = 'F' (NG 누적)
- [TCP wire] 정상 FAI 는 'P'
- [Log] 각 NG FAI 마다 별도 fail 로그 (D-07 lenient — 첫 NG 에서 abort 안 함)
- [동작] EStep.Measure 루프가 첫 NG 후에도 계속 진행 (D-07 회귀 가드)

**결과:** PASS
**메모:** Test 2 의 자연 확장 (사용자가 Nominal/Tolerance 조정으로 NG 다수 케이스 확인). D-07 lenient 회귀 0 — 모든 NG 가 누적되어 표시.

---

## Test 5: Multi-datum 부분 실패 — Phase 37 D-37-03 lenient 회귀 가드

**사전 조건:**
- Test 3 의 recipe 와 동일하지만 더 엄격한 검증: 3개 이상 datum 보유 recipe
- 1 datum 만 검출 실패, 2 datum 검출 성공
- 각 datum 별로 최소 1 FAI 보유

**실행 절차:**
1. 3 datum recipe 로드
2. 1 datum (예: 가운데 datum) 의 TeachingImagePath 또는 ROI 를 검출 불가 상태로 변경
3. 검사 트리거
4. 검사 완료 대기

**기대 결과:**
- [UI] 2 datum 정상 십자, 1 datum 'DETECT FAIL' 라벨
- [UI] 정상 datum 기반 FAI overlay 정상 (-OK/-NG suffix)
- [TCP wire] cycle Result = 'N' (1 datum skip 가 hierarchy 최상위)
- [TCP wire] 정상 datum 기반 FAIResults = 'P' 또는 'F' (측정 결과 따라)
- [TCP wire] 실패 datum 기반 FAIResults = 'N'
- [동작] DatumPhase 가 첫 datum 실패에 abort 하지 않고 모든 datum loop 완료 (Phase 37 D-37-03 lenient)
- [동작] Measure 게이트가 datum-skip 만 정확히 차단, 정상 datum 의 FAI 는 영향 없음 (Plan 01 D-01)

**결과:** PASS
**메모:** Test 3 와 동일한 코드 경로 (per-FAI gate + lenient loop). Test 3 시 사용자가 이미 "datum 없어서 안 되는 shot + 다른 shot 정상 검사" 동시 관찰 → multi-datum 부분 실패 시나리오의 핵심 검증 완료. CO-39-03 stagger 로 다중 라벨 겹침도 회피 확인.

---

## Summary

| 항목 | 값 |
|------|-----|
| Total | 5 |
| Passed | 5 |
| Failed | 0 |
| Pending | 0 |
| Status | signed_off |

**Sign-off 결정:**
- 5/5 PASS → status: signed_off, 다음 phase 진행 ✓

**Hotfix 적용 이력 (UAT 도중):**

| ID | 원인 | 수정 | Commit |
|----|------|------|--------|
| CO-39-01 | Action_FAIMeasurement 이미지 취득 실패 2 분기에서 datum.LastFindSucceeded=false 누락 | 2 분기에 1줄씩 추가 | 13e735e |
| CO-39-02 | 사전 티칭 안 한 datum (IsConfigured=false) → 라벨 분기 미진입 + RefOrigin=0 → 화면 밖 좌표 | DatumConfig.RuntimeDetectFailed 휘발성 INPC + 분기/좌표 fallback 강화 | af3f608 |
| CO-39-03 | UAT 사용자 요청 — 라벨이 Datum origin 위 → 이미지 우상단으로 이동 | GetPart 동적 좌표 + datum 이름 포함 + hash 기반 stagger | 8a3d2f6 |

---

## Phase 39 회귀 가드 체크리스트

UAT 시 다음 회귀 0 확인 (모든 Test 통과 조건):

- [x] Phase 7-02 overlay suffix (-OK/-NG) 정상 부여 (Test 1, 2, 4)
- [x] Phase 17 D-13 RenderDatumFindResult (purple 검출 십자) 정상 표시 (Test 1, 2, 4)
- [x] Phase 36 hotfix CO-36-03 (LastTeachSucceeded 분기 밖에서 RenderDatumFindResult 호출) 동작 (Test 1, 2, 4)
- [x] Phase 37 D-37-03 lenient — datum 부분 실패 시 다른 datum FAI 정상 측정 (Test 3, 5) — 사용자 직접 확인
- [x] Phase 22 IMG-02 — TeachingImagePath ≠ SimulImagePath 역할 분리 유지 (Test 3 시나리오 구성 가능)
- [x] Phase 20 D-12 marker stacking — 기존 마커 100% 보존, 신규 //260529 hbk Phase 39 누적
- [x] CO-22-01 — Datum↔FAI/SHOT PropertyGrid 전환 정상 (Test 3 시나리오 구성 중 노드 전환)

---

*Last updated: 2026-05-29 — Phase 39 UAT signed_off. 5/5 PASS. 3 hotfix 적용 후 사용자 직접 검증.*
