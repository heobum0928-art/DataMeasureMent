---
phase: 23
plan: 03
status: partial
total: 5
passed: 1
failed: 1
blocked: 3
pending: 0
requirement_ids: [ALG-01]
sign_off_reviewer: heobum0928-art
sign_off_date: 2026-05-13
---

# Phase 23 UAT — Top #1 A시리즈 Simul end-to-end (ALG-01)

본 UAT 는 Plan 23-01 (EdgeToLineDistanceMeasurement 클래스 + TryFitLine selection 파라미터) 과 Plan 23-02 (MeasurementFactory 7번째 case + GrabOrLoadDatumImage TeachingImagePath 우선 분기) 의 합성 검증이다.
사용자는 아래 5 시나리오를 직접 수행 후 각 Result 를 PASS/FAIL/PARTIAL 로 채우고, frontmatter 의 `passed:` 카운트와 `status:` (passed:5 → signed_off, passed<5 → partial) 를 갱신한다.

---

## Pre-conditions (사용자 사전 셋업, D-16 + D-01 + D-13)

1. **Simul 이미지 비치** — `D:\TestImg\Datameasurement\` 디렉토리에 Top Fixture #1 의 PPT 도면 반영본 Simul 이미지 배치 (TeachingImagePath / SimulImagePath 분리 검증용 별도 파일 권장)
2. **DatumConfig 1개 작성** — AlgorithmType = `CircleTwoHorizontal` (per D-01 lock)
   - B1 홀 Circle ROI + 2 horizontal tangent line ROI 입력
   - TeachingImagePath = Simul 이미지 경로 또는 빈 문자열 (SC#3 검증용)
3. **FAI A1~A5 5개 작성** — EdgeMeasureType = `EdgeToLineDistance` (Plan 23-02 Factory 등록 후 자동 노출)
   - 각각 Point ROI Rectangle 1개 + 7 Edge 파라미터 (EdgeDirection default TtoB, EdgeSelection default First) + NominalValue + UpperTolerance + LowerTolerance
   - 모두 InspectionRecipeManager (IsDynamicFAIMode) 의 기존 INI 포맷 (D-15)
4. **빌드 산출물 확인** — `bin\x64\Debug\DatumMeasurement.exe` 존재 (Plan 23-02 Task 3 출력 또는 Test 5 자동 재빌드)

---

## Test 1 — SC#1 Simul end-to-end 완주 (ALG-01)

**Scenario:** Simul 이미지 로드 → Datum CTH 자동 찾기 → A1~A5 EdgeToLineDistance 측정값(mm) UI 표시가 오류 없이 완주

**Steps:**

1. DataMeasurement.exe 기동
2. Recipe 선택 (Pre-conditions 의 INI)
3. Inspection 시퀀스 1회 실행 (수동 트리거 또는 TCP 명령)
4. InspectionListView TreeView 펼침 (Datum + A1~A5 노드 가시)
5. 각 A1~A5 노드의 측정값 컬럼 (mm 단위, 0.001 정밀도 per D-09) 표시 확인

**Expected:**

- 시퀀스 Error 상태 진입 없음 (모든 5 측정 정상 완주)
- A1~A5 5개 측정값 표시 (mm)
- 정밀도 3자릿 (예: `12.345 mm`) — format = F3 확인 완료 (MeasurementResultRow.cs L54/L60 `ToString("F3")` 기존 적용)
- +Y 부호 적용 확인 (Datum B 위쪽 측정점 = 양수, D-02)

**Actual:** 시퀀스 완주 + Datum CTH (Datum.Line2 strip-loop 50 edges → trim 30 정상 로그) 동작. A1~A5 EdgeToLineDistance 측정 후 InspectionListView 측정값 컬럼 = `—` (값 없음), 판정 컬럼 = `—`. Error 로그에 `[FAIMeasurement] failed` 메시지 부재 → `TryExecute = true` 로 평가됨에도 UI 결과 미표시. (Error 로그에 ParamBase.Load reflection 노이즈 `Property set method not found` 다수 — `MeasurementBase.TypeName` get-only 인 기존 노이즈, 본 결함과 무관.)

**Result:** ❌ FAIL

**Notes:** 가능한 원인 후보 (디버깅 carry-over) — (a) `FAIConfig.PixelResolutionX = 0` → `resultValue = -datumRow * 0 = 0` (조용히 0), (b) 측정값 컬럼 binding/refresh 단절. 추적 미완료, v1.1 quick task 로 carry-over.

---

## Test 2 — SC#2 OK/NG strip 녹/적 (ALG-01)

**Scenario:** A1~A5 각각의 측정값 ↔ NominalValue/Tolerance 공차 비교 → OK/NG 판정 → InspectionListView 노드 색상 분기 (CO-05 녹/적)

**Steps:**

1. Test 1 의 시퀀스 실행 결과 유지
2. NominalValue 또는 Tolerance 를 의도적으로 다르게 설정 → A1~A5 중 일부 OK 일부 NG 만들기
3. 재실행 → InspectionListView 5 노드 색상 시각 확인
4. Halcon viewer 오버레이 영역 확인 (RESEARCH Pitfall 4 명확화 — overlay 빈 리스트 채택으로 viewer 라인 미표시 가능, **InspectionListView 노드 색상** 으로 분기 검증)

**Expected:**

- OK 판정 노드 = 녹색, NG 판정 노드 = 빨강 (CO-05 패턴 — InspectionListView)
- Halcon viewer 의 strip 색상 라인은 EdgeToLineDistance overlay 미생성 정책 (Plan 23-01 결정 — PointToLineDistance 패턴 일치) 으로 표시 안 될 수 있음. **trust-based PASS 옵션** — 노드 색상이 정확히 분기되면 PASS, viewer 라인 부재 시 carry-over 등록.

**Actual:** SC#1 측정값 부재로 OK/NG 판정 자체 불가 → 본 시나리오 검증 불가.

**Result:** ⏸ blocked (SC#1 의존)

**Notes:** Pitfall 4 인용 — overlay 추가는 별도 backlog. SC#1 carry-over 해소 후 재검증 필요.

---

## Test 3 — SC#3 TeachingImagePath ≠ InspectionImagePath 분리 (ALG-01)

**Scenario:** TeachingImagePath 와 ShotConfig.SimulImagePath (= InspectionImagePath) 가 다른 파일을 가리켜도 정상 동작, 같은 파일이어도 회귀 0

**Steps:**

1. **Case A (분리):** DatumConfig.TeachingImagePath = `teaching.bmp`, ShotConfig.SimulImagePath = `inspection.bmp` (다른 파일) → 시퀀스 실행 → Datum 찾기는 teaching.bmp 로, A1~A5 측정은 inspection.bmp 로 (Plan 23-02 Task 2 분기 확인)
2. **Case B (동일):** 두 경로 모두 동일 파일 → 시퀀스 실행 → 회귀 0 (Phase 22 UAT Test 2 패턴)
3. **Case C (TeachingImagePath 빈 문자열):** TeachingImagePath = `""` → SimulImagePath 폴백으로 Phase 22 baseline byte-identical

**Expected:**

- Case A: Datum 검출 = teaching.bmp 의 좌표, FAI 측정 = inspection.bmp 의 좌표 (각각 분리 동작)
- Case B: SC#1 동작 동일 (회귀 0)
- Case C: Phase 22 동작 byte-identical (회귀 0)

**Actual:** SC#1 측정값 부재로 두 경로 분리/회귀 동등성 비교 자체 불가 → 본 시나리오 검증 불가.

**Result:** ⏸ blocked (SC#1 의존)

**Notes:** Case A 가 실 시나리오, Case B/C 는 회귀 검증. SC#1 carry-over 해소 후 재검증 필요.

---

## Test 4 — SC#4 A6 확장성 (D-12 + D-13)

**Scenario:** A6 1개 추가가 INI 직접 편집 + UI 'Add FAI' 버튼 두 채널 모두로 가능, 코드 변경 0 으로 확장성 검증

**Steps:**

1. **Channel A (INI 직접 편집):** 프로그램 종료 → INI 파일에 A6 섹션 추가 (FAIName="A6", EdgeMeasureType="EdgeToLineDistance", ROI/Edge 파라미터 입력) → 재기동 → InspectionListView 에 A6 노드 표시 확인 + 시퀀스 실행 시 A6 측정값 출력 확인
2. **Channel B (UI 'Add FAI'):** InspectionListView 의 Shot 노드 우클릭 또는 'Add FAI' 버튼 → FAIConfig 신규 생성 → EdgeMeasureType ComboBox 에서 `EdgeToLineDistance` 선택 가능 확인 (Plan 23-02 Task 1 GetTypeNames 반영) → ROI/Edge 파라미터 입력 → 시퀀스 실행 → A6 측정값 출력

**Expected:**

- Channel A + B 둘 다 A6 측정값 정상 표시 (= 6개 측정값)
- EdgeMeasureType ComboBox 의 7번째 항목 = "EdgeToLineDistance" 가시
- 코드 추가 0 (Phase 5 IsDynamicFAIMode 인프라 + Plan 23-02 Factory 자동 노출)

**Actual:** SC#1 측정값 부재로 A6 추가 후 측정값 검증 자체 불가 → 본 시나리오 검증 불가. (Channel B EdgeMeasureType ComboBox 노출 여부는 시각 미확인.)

**Result:** ⏸ blocked (SC#1 의존)

**Notes:** D-14 확장 한계 = A23 까지 보장. SC#1 carry-over 해소 후 재검증 필요. ComboBox 노출 검증은 단독 분리 가능 (별도 spike 후보).

---

## Test 5 — SC#5 msbuild Debug/x64 PASS (D-19)

**Scenario:** Plan 23-01 + 23-02 누적 변경 후 msbuild Debug/x64 Rebuild 에서 0 errors + 신규 warning 0 (Phase 21 baseline 6 유지)

**Steps:**

1. `msbuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /nologo /t:Rebuild > build_23_w3.log 2>&1`
2. log 파일 점검 — error 0, warning 6 (MSB3884×2 + CS0162×2 + CS0219×2)

**Expected:**

- `0 Error(s)`
- Warning 매치 정확히 6 (Phase 21 baseline)
- 신규 warning code 부재

**Actual:** build_23_w3.log — EXIT=0, 0 errors, 6 warnings (MSB3884×2 + CS0162×2 + CS0219×2 = Phase 21 baseline preserved). 신규 warning 0.

**Result:** ✅ PASS (2026-05-12 자동 검증)

**Notes:** Plan 23-02 Task 3 의 build_23_w2.log 와 동일 결과 — Phase 21 baseline 6 occurrences (3 unique × 2-pass Rebuild) 유지, Plan 23-01+02 누적 변경이 신규 컴파일 warning 도입 0.

---

## Summary

| # | Scenario | Result | Notes |
|---|----------|--------|-------|
| 1 | SC#1 Simul end-to-end | ❌ FAIL | 시퀀스 완주 + Datum 정상 동작, 그러나 A1~A5 측정값/판정 컬럼 = `—` (Error 로그 [FAIMeasurement] 부재 → TryExecute success 후 결과 미표시) |
| 2 | SC#2 OK/NG strip | ⏸ blocked | SC#1 의존 |
| 3 | SC#3 TeachingImagePath 분리 | ⏸ blocked | SC#1 의존 |
| 4 | SC#4 A6 확장성 (INI + UI) | ⏸ blocked | SC#1 의존 (ComboBox 노출 단독 검증은 분리 가능) |
| 5 | SC#5 msbuild PASS | ✅ PASS | build_23_w3.log: 0 errors / 6 warnings (baseline 유지) |

**Total:** 5 / **Passed:** 1 / **Failed:** 1 / **Blocked:** 3

---

## Carry-overs

| ID | Description | Owner | Status |
|----|-------------|-------|--------|
| CO-23-01 | A1~A5 EdgeToLineDistance 측정값/판정 컬럼 미표시 — 시퀀스 완주 + Error 로그 [FAIMeasurement] 부재 + Datum 정상 동작에도 InspectionListView 결과 컬럼 = `—`. 가능한 원인: (a) FAIConfig.PixelResolutionX = 0 → resultValue silently 0, (b) MeasurementResultRow binding/refresh 단절. v1.1 quick task 로 디버깅 필요. | TBD | open |
| CO-23-02 | SC#2/SC#3/SC#4 인터랙티브 UAT 미수행 — CO-23-01 해소 후 재실행 필요. | TBD | blocked-by CO-23-01 |

---

## Sign-off

- Reviewer: heobum0928-art
- Date: 2026-05-13
- Status: partial (1 PASS / 1 FAIL / 3 blocked) — Phase 23 핵심 SC#1 FAIL → CO-23-01 carry-over 등록 후 마감
