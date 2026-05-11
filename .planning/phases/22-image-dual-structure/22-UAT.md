---
phase: 22-image-dual-structure
artifact: UAT
status: pending
updated: 2026-05-11
total: 4
passed: 0
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

**Result:** __ (PASS/FAIL)

**Notes:** (FAIL 시 원인 기재)

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

**Result:** __ (PASS/FAIL)

**Notes:** (FAIL 시 원인 기재)

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

**Result:** __ (PASS/FAIL)

**Notes:** (FAIL 시 원인 기재)

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

**Result:** __ (PASS/FAIL)

**Notes:** (FAIL 시 원인 기재)

---

## Summary

| # | Scenario | Result |
|---|----------|--------|
| 1 | TeachingImagePath INI 라운드트립 (SC#1) | pending |
| 2 | 두 경로 동일 파일 케이스 (SC#3) | pending |
| 3 | TeachingImagePath INI 키 미존재 폴백 (SC#4) | pending |
| 4 | msbuild Debug/x64 PASS + warning 0 (SC#5) | pending |

**Total:** 4 / **Passed:** 0 / **Pending:** 4

---

## Carry-overs

(사용자 검증 중 발견된 신규 결함은 본 섹션에 기재 — Phase 23 또는 별도 quick-task 로 이관)

- (없음)

---

## Sign-off

- Reviewer: __
- Date: __
- Status: pending → signed_off (4/4 PASS 후 갱신)
