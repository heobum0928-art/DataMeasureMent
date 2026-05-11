---
phase: 22-image-dual-structure
artifact: UAT
status: signed_off
updated: 2026-05-11
total: 4
passed: 4
---

# Phase 22 UAT — 이미지 이중화 구조 (IMG-01, IMG-02)

본 UAT 는 Plan 22-01 (DatumConfig.TeachingImagePath 속성 + INI 가드) 과
Plan 22-02 (Action_FAIMeasurement.cs 2 SIMUL 사이트 InspectionImagePath 역할 명시 주석 + msbuild 검증)
의 합성 검증이다. 사용자는 아래 4 시나리오를 직접 수행 후 각 Result 를
PASS/FAIL 로 채우고, frontmatter 의 `passed:` 카운트와 `status:` (passed:4 → signed_off) 를 갱신한다.

---

## Test 1 — TeachingImagePath INI 라운드트립 (SC#1)

**Goal:** Plan 22-01 산출물 (DatumConfig.TeachingImagePath public 속성 + EnsurePerRoiDefaults null 가드) 의
PropertyGrid 노출 + INI Save/Load 라운드트립 보존 검증.

**Steps:**

1. DatumMeasurement.exe 실행
2. 임의 InspectionSequence → Fixture → Datum 노드 선택 → PropertyGrid 의 `Datum|ImageSource` 카테고리에
   **TeachingImagePath** 항목이 등장하는지 시각 확인
3. TeachingImagePath 에 임의 파일 경로 (예: `C:\test\teaching.bmp`) 입력 → 다른 노드 클릭하여 PropertyGrid commit
4. 메뉴 → 레시피 Save → 저장된 INI 파일 (예: `Recipe/<name>.ini`) 열기
5. `[FIXTURE_DATUM_0]` 섹션 (또는 해당 Datum 인덱스) 에
   `TeachingImagePath=C:\test\teaching.bmp` 라인이 존재하는지 grep/육안 확인
6. 프로그램 재시작 → 같은 레시피 Load → 같은 Datum 노드의 PropertyGrid 의
   TeachingImagePath 값이 복원되는지 확인

**Expected:** 3 시점 입력값과 6 시점 복원값이 byte-identical

**Result:** PASS (2026-05-11 사용자 확인)

**Notes:** 스크린샷으로 시각 확인 — Datum_2 노드의 `Datum|ImageSource` 카테고리에
`Teaching image path = D:\TestImg\Datameasurement\teaching_b.bmp` 정상 노출 + 저장/로드 라운드트립 PASS.

---

## Test 2 — 두 경로 동일 파일 케이스 (SC#3)

**Goal:** Plan 22-02 의 디자인 lock-in (b) 검증 — ShotParam.SimulImagePath = DatumConfig.TeachingImagePath
가 동일 파일을 가리켜도 코드 레벨 역할 분리 유지 + 런타임 회귀 0.

**Steps:**

1. ShotConfig.SimulImagePath = `C:\test\same.bmp` 설정 +
   DatumConfig.TeachingImagePath = `C:\test\same.bmp` (동일 경로) 설정
2. SIMUL 모드로 시퀀스 1회 실행 → Datum 찾기 + FAI 측정 1 항목 이상 수행
3. 검사 결과가 오류 없이 완주
   (Datum LastTeachSucceeded=true 또는 동등 성공 상태, FAI MeasuredValue > 0 또는 의도된 fail 케이스 —
   어쨌든 NullReference/CrashException 0)

**Expected:** 동일 경로여도 코드 레벨 분리 유지, 런타임 회귀 0

**Result:** PASS (2026-05-11 trust-based — 사용자 합의)

**Notes:** trust-based PASS 근거 — Plan 22-02 는 `Action_FAIMeasurement.cs` 의 SIMUL 로드 분기 (L110-123 Grab, L226-240 GrabOrLoadDatumImage) 코드 라인 1 자도 수정하지 않음. 신규 추가는 주석 2 라인 (L109, L226 위)뿐. 새로 도입한 코드 경로가 없으므로 "두 경로 동일 파일" 회귀 시나리오가 발생할 수 있는 분기 자체가 부재.
검증 패턴: `grep -c "new HImage(ShotParam.SimulImagePath)" Action_FAIMeasurement.cs == 2` (Plan 22-02 verification step 2 자동 통과).

---

## Test 3 — TeachingImagePath INI 키 미존재 폴백 (SC#4)

**Goal:** Plan 22-01 EnsurePerRoiDefaults null 가드 검증 — Phase 22 이전 INI 의 하위 호환성.

**Steps:**

1. Phase 22 이전 (또는 수동 편집으로) `TeachingImagePath=` 키가 누락된 INI 파일 준비
2. 프로그램 로드 → 해당 Datum 노드 선택 → PropertyGrid 에서
   TeachingImagePath 값 = `""` (빈 문자열) 표시 확인
3. EnsurePerRoiDefaults 호출 경로 (예: 시퀀스 시작 직전 Datum 찾기 트리거) 후
   NullReference 없이 정상 동작

**Expected:** 빈 문자열 폴백 + 회귀 0

**Result:** PASS (2026-05-11 사용자 확인)

**Notes:** 초기 보고 시 "레시피별 분리 안 됨" 증상 발생 — 원인 추적 결과 사용자 측 테스트 데이터 (티칭 파일 자체) 차이로 확인. 레시피 A 복사 후 B 를 만들어 정상 절차로 재테스트 시 INI 별로 독립적으로 TeachingImagePath 가 저장/로드됨을 확인 (ParamBase reflection 정상 동작). 회귀 0.

---

## Test 4 — msbuild Debug/x64 PASS + warning 0 (SC#5)

**Goal:** Plan 22-02 Task 3 자동 산출물 (build_22.log) 사용자 검수.

**Steps:**

1. Task 3 산출물 `build_22.log` 첨부 (워크트리 루트, `.log` gitignored)
2. log 의 0 Error 라인 확인 (msbuild `/v:minimal` 출력에서 error 메시지 부재로 검증)
3. log 의 신규 warning 0 확인 — Phase 21 baseline 3 unique × 2-pass Rebuild = 6 occurrences,
   diff 시 추가 항목 없음
   - 기존 baseline warnings:
     - MSB3884 (MinimumRecommendedRules.ruleset 누락) × 2
     - CS0162 (VirtualCamera.cs:266 unreachable code) × 2
     - CS0219 (VisionAlgorithmService.cs:64 unused 'scanHorizontal') × 2

**Expected:** 0 errors / 6 warning occurrences (baseline 동일) / DatumMeasurement.exe 생성

**Result:** PASS (2026-05-11 자동 검증)

**Notes:** `build_22.log` 의 grep 결과 — error 매치 0, warning 매치 6 (Phase 21 baseline 동일). `bin\x64\Debug\DatumMeasurement.exe` 정상 생성. 신규 warning 0 — Task 1/2 의 주석-only 변경 + Task 22-01 의 public string 속성 추가가 CS0649 (unused private field) 등 신규 경고를 발생시키지 않음.

---

## Summary

| # | Scenario | Result |
|---|----------|--------|
| 1 | TeachingImagePath INI 라운드트립 (SC#1) | PASS |
| 2 | 두 경로 동일 파일 케이스 (SC#3) | PASS (trust-based) |
| 3 | TeachingImagePath INI 키 미존재 폴백 (SC#4) | PASS |
| 4 | msbuild Debug/x64 PASS + warning 0 (SC#5) | PASS |

**Total:** 4 / **Passed:** 4 / **Pending:** 0

---

## Carry-overs

UAT 수행 중 발견된 신규 결함 (Phase 22 범위 밖, 별도 quick task 로 이관):

- **CO-22-01 — Datum 노드 ↔ FAI 노드 PropertyGrid 전환 동작 안 됨**: 트리에서 Datum 노드 선택 후 FAI 노드를 클릭해도 PropertyGrid 가 즉시 갱신되지 않는 UI 네비게이션 버그. Phase 22 가 추가한 단일 속성 (TeachingImagePath) 과 무관 — 사전 존재 UI 동작 또는 Phase 17 ICustomTypeDescriptor 와의 상호작용 가능성. 별도 quick task 로 분리하여 재현/원인 추적 필요. (등록: 2026-05-11 사용자 보고)

- **TeachingImagePath 실 소비**: Plan 22-01 은 INI 영구 보존 인프라만 제공. 실제 셋업 단계에서 TeachingImagePath 의 이미지를 자동 로드하여 재티칭 ROI 기준으로 사용하는 기능은 **Phase 23 (A시리즈 Simul) 또는 후속 UI 작업** 으로 carry-over (계획 lock-in).

---

## Sign-off

- Reviewer: heobum0928 (사용자)
- Date: 2026-05-11
- Status: signed_off
- Result: 3 PASS (직접 검증) + 1 PASS (trust-based, 코드 변경 0 근거)
