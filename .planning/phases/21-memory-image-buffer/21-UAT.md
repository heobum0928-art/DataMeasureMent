---
phase: 21-memory-image-buffer
uat_date: 2026-05-10
status: pending  # 사용자 PASS 후 signed_off (4/4 PASS) 또는 partial (gap 명시) 로 변경
signed_off_by: heobum0928@gmail.com
ac_coverage: [AC1, AC2, AC4]  # AC3 는 21-VERIFICATION.md 자동 audit 으로 충족
related_files:
  - .planning/phases/21-memory-image-buffer/21-VERIFICATION.md
  - .planning/phases/21-memory-image-buffer/21-01-SUMMARY.md
  - .planning/phases/21-memory-image-buffer/21-02-SUMMARY.md
---

# Phase 21 UAT — Memory Image Buffer

Phase 21 (BUF-01 / BUF-02) 의 사용자 SIMUL_MODE 검증. 4 테스트 PASS → Phase 21 sign-off.

**Trace 로그 파일 경로 안내:**
- Default: `<AppBaseDirectory>\Trace\YYYY-MM-DD.log` (`SystemSetting.TraceLogSavePath` 기본값)
- 일반 환경: `D:\Trace\2026-05-10.log` (Setting.ini 의 `TraceLogSavePath` 가 D 드라이브로 설정된 경우)
- 정확한 경로 확인: 앱 실행 시 `[SYSTEM] Initialized` 라인이 어떤 파일에 출력되는지로 식별. 또는 Setting.ini 의 `TraceLogSavePath` 키 직접 확인.

---

## Test 1 — AC#1 결과 이미지 표시 (디스크 비접근, D-08 Option B 시각 확인)

**Purpose:** 21-VERIFICATION.md AC#1 의 grep audit (forbidden API 0 hits) 가 실제 런타임에서도 디스크 I/O 없이 동작하는지 시각/I/O 모니터로 보강 확인.

**Steps:**
1. `bin\x64\Debug\DatumMeasurement.exe` 실행 (SIMUL_MODE).
2. 임의 recipe 로드 → Inspection Sequence 1회 실행 (FAI 검사 완료까지).
3. InspectionListView 에서 임의 FAI 노드 클릭.
4. **기대 결과:** MainView 의 halconViewer 에 마지막 Shot 이미지가 표시된다 — 디스크 접근 지연 없이 즉시 (메모리 경로 — Plan 01 GetImage XML doc 의 caller-disposes 계약).
5. (선택, 권장) Process Monitor (Sysinternals) 를 띄워 클릭 시점 file I/O 이벤트 캡처. 필터: `Process Name is DatumMeasurement.exe` AND `Operation is ReadFile`. 기대: recipe 디렉토리 / 이미지 디렉토리 read 이벤트 0.

**Expected:** halconViewer 에 마지막 Shot 이미지 즉시 표시 (디스크 I/O 0).

**Result:** _____ (PASS / FAIL — 사용자 기입)
**Notes:** _____ (지연 체감 / 모달 누락 / Process Monitor 캡처 결과 등 — 사용자 기입)

---

## Test 2 — AC#2 dispose 입증 (D-11 ① logging 카운트, recipe load × 5)

**Purpose:** D-02 의 3 dispose 채널 중 #1 (recipe change) 이 실제로 발화하여 `[InspectionRecipeManager] ClearShots disposed` 로그를 출력하는지 카운트로 입증.

**Steps:**
1. SIMUL_MODE 앱 실행 직전 Trace 로그 파일 위치 확인 (위 안내 참조).
2. (옵션) 기존 로그 파일을 백업 또는 새 파일로 시작하기 위해 시점 기록 — 또는 시작 시각 기록 후 해당 시각 이후 라인만 카운트.
3. 앱 실행 (정상 Initialized 까지 대기).
4. **Recipe A** 로드 → **Recipe B** 로드 → **Recipe A** 로드 → **Recipe B** 로드 → **Recipe A** 로드 (총 5회 LoadRecipe).
5. 앱 종료 (X 버튼).
6. Trace 로그에서 다음 명령 실행:
   ```
   findstr /c:"[InspectionRecipeManager] ClearShots disposed" <Trace 로그 파일 경로>
   ```
   또는 PowerShell:
   ```powershell
   Select-String -Path '<Trace 로그 파일 경로>' -Pattern '\[InspectionRecipeManager\] ClearShots disposed'
   ```

**Expected:**
- 각 LoadRecipe 마다 ClearShots 가 호출 — Custom/SystemHandler.OnRecipeChanged_FlushBuffers (subscriber, Plan 02 channel #1) + InspectionRecipeManager.LoadPhase6Format 내부 (기존 경로) 가 둘 다 호출되어 recipe 1회당 2 라인 가능.
- 앱 종료 시 SystemHandler.Release() 가 ClearShots 1회 추가 호출 (Plan 02 channel #3).
- 보수적 임계 = **≥ 5 hits** PASS, 낙관적 기대 = ~11 hits (5×2 + 1).

**Result:** _____ (PASS / FAIL — 사용자 기입)
**Hit count:** _____ (실측치 — 사용자 기입)
**Last log line:** _____ (마지막 1 라인 인용 — 사용자 기입)
**Notes:** _____ (recipe A/B 이름, 실측 카운트가 임계 미만이면 어떤 채널 누락인지 추정 — 사용자 기입)

---

## Test 3 — AC#4 SIMUL_MODE 회귀 (D-10 byte-identical, Datum 티칭 + FAI 측정 1회)

**Purpose:** Phase 21 의 코드 변경 (XML doc + subscriber wire-up + Logging instrumentation) 이 byte-identical 행위 보존인지 시퀀스 1회 실행으로 확인. Phase 20 sign-off 시점과 동일한 동작 기대.

**Steps:**
1. SIMUL_MODE 앱 실행.
2. **Datum 티칭:** `btn_teachDatum` 클릭 → 티칭 ROI 그리기 (algorithm 별 step 진행) → Datum 검출 성공 확인 (LastTeachSucceeded=true 시각 표시).
3. **Inspection Sequence 1회 실행:** 외부 TCP test 트리거 또는 UI 의 시퀀스 시작 버튼 (환경에 따라 — Phase 20 sign-off UAT 와 동일 트리거 사용).
4. **검증:**
   - Datum 검출 PASS (Phase 20 sign-off 시점과 동일).
   - FAI 측정 N건 실행 — 각 결과 (PASS/NG) 가 Phase 20 baseline 분포와 동일.
   - 결과 이미지 리뷰 (Test 1 의 click 흐름) 가 정상 동작.

**Expected:** Phase 20 sign-off 시점 동작과 동일 (byte-identical) — Datum 검출 PASS, FAI 측정 결과 분포 동일, 결과 이미지 정상 표시.

**Result:** _____ (PASS / FAIL — 사용자 기입)
**Notes:** _____ (Phase 20 baseline 대비 차이가 있으면 명시; 차이 없으면 "동일 — byte-identical OK" — 사용자 기입)

---

## Test 4 — AC#4 msbuild Debug/x64 PASS (자동 검증 인용)

**Purpose:** Phase 21 변경 후 fresh build 가 0 errors / 0 new warnings 임을 확인.

**Steps:**
1. 21-VERIFICATION.md AC#4 섹션의 msbuild 결과 인용 — Plan 03 Task 1 에서 자동 PASS 확인 완료.
2. (선택) 사용자가 추가로 fresh `clean + rebuild` 직접 실행 가능 — 다음 명령:
   ```powershell
   & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
       'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' `
       -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:m -clp:Summary
   ```

**Expected:** Errors=0, Warnings 신규=0 (3 pre-existing baseline × 2 build = 6 total OK).

**Result:** PASS (자동 검증 인용 — 21-VERIFICATION.md AC#4)
**Notes:** Plan 03 Task 1 결과: 오류 0, 경고 6 (= 3 pre-existing × 2), elapsed 00:00:02.63.

---

## Summary

- **Total:** 4
- **Passed:** ___ (사용자 기입)
- **Failed:** ___ (사용자 기입)
- **Pending:** ___ (사용자 기입)

**Sign-off:** _____ (사용자 서명/이메일 + 날짜 — 사용자 기입)

---

## Sign-off Procedure

1. 4 테스트 모두 PASS → frontmatter `status: pending` → `status: signed_off` 로 변경 + Sign-off 라인 채움 → 응답 "approved" 또는 "Phase 21 sign-off".
2. 일부 FAIL / partial → frontmatter `status: pending` → `status: partial` 로 변경 + gap 명시 (어떤 AC / 어떤 Test / 어떤 채널) → 응답 "partial — <gap 설명>".
3. orchestrator 가 응답 받으면 21-03-SUMMARY.md 작성 + Phase 21 sign-off / carry-over 결정.

---

*Phase: 21-memory-image-buffer*
*Plan 03 Task 2 scaffold — 사용자 SIMUL UAT 4 테스트*
*Date: 2026-05-10*
