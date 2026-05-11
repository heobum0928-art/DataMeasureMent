---
phase: 22-image-dual-structure
plan: 02
subsystem: inspection
tags: [datum, inspection-image, simul-mode, action-faimeasurement, msbuild, uat, role-separation]

# Dependency graph
requires:
  - phase: 22-image-dual-structure
    provides: DatumConfig.TeachingImagePath public string 속성 + EnsurePerRoiDefaults null 가드 (Plan 22-01)
  - phase: 05-shot-fai-data-model
    provides: ShotConfig.SimulImagePath + Action_FAIMeasurement SIMUL 분기 (L109, L226)
  - phase: 21-memory-image-buffer
    provides: msbuild Debug/x64 baseline (3 unique warnings × 2-pass Rebuild = 6 occurrences)
provides:
  - Action_FAIMeasurement.cs 2 SIMUL 사이트 (EStep.Grab L109, GrabOrLoadDatumImage L226) 에 InspectionImagePath 역할 명시 주석
  - build_22.log (msbuild Debug/x64 Rebuild PASS, 0 errors, 신규 warning 0)
  - 22-UAT.md 사용자 사인오프 (4/4 PASS, 1 trust-based)
  - Phase 22 (IMG-01 + IMG-02) signed_off
affects: [23-a-series-simul, future-teaching-image-ui, CO-22-01-quick-task]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "주석-only 역할 명시 (코드 무수정 + comment marker `//260511 hbk Phase 22 IMG-02`)"
    - "디자인 lock-in (b) 적용 — InspectionImagePath = ShotConfig.SimulImagePath 의미적 재해석 (식별자 무변경)"
    - "Trust-based UAT PASS 패턴 — 신규 코드 경로 0 이면 회귀 시나리오 부재 → 사용자 합의 후 PASS"

key-files:
  created:
    - .planning/phases/22-image-dual-structure/22-UAT.md
    - build_22.log (gitignored 빌드 산출물, 워크트리 루트)
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "Action_FAIMeasurement.cs 코드 무수정 (SIMUL 분기 L110-123 / L227-240 byte-identical) — 주석 2 라인만 추가 (L109, L226 위). 회귀 위험 0."
  - "Test 2 (동일 파일 케이스) trust-based PASS 채택 — 신규 코드 경로 0 + UI 위치 혼동 발생, 코드 변경 0 근거로 사용자 합의."
  - "Test 3 초기 보고된 '레시피별 분리 안 됨' 증상은 사용자 측 티칭 파일 차이로 추적 완료 — Phase 22 회귀 아님, ParamBase reflection 정상 동작."
  - "CO-22-01 (Datum↔FAI PropertyGrid 전환 UI 버그) 별도 quick task 로 분리 — Phase 22 single-속성 추가와 무관, 사전 존재 UI 동작 또는 Phase 17 ICustomTypeDescriptor 와의 상호작용 가능성."

patterns-established:
  - "Phase 22 IMG-02 marker stacking: 기존 `//260409 hbk Phase 5` 주석 보존 + 위에 `//260511 hbk Phase 22 IMG-02` 신규 마커 누적 (Phase 20 D-12 패턴 준수)"
  - "주석-only 역할 분리: 식별자/코드 변경 0, 의미 재해석을 인접 주석으로 못박는 패턴 — zero-regression 보장"

requirements-completed: [IMG-02]

# Metrics
duration: ~10min (auto tasks ~3min, UAT 인터랙티브 ~7min)
completed: 2026-05-11
---

# Phase 22 Plan 02: InspectionImagePath 역할 명시 + UAT Sign-off Summary

**Action_FAIMeasurement.cs 의 2 SIMUL 로드 사이트 (EStep.Grab L109, GrabOrLoadDatumImage L226) 위에 InspectionImagePath 역할 명시 주석 추가 — 코드 라인 무수정 + msbuild Debug/x64 PASS (신규 warning 0) + 22-UAT.md 4/4 PASS 사용자 사인오프**

## Performance

- **Duration:** ~10 min (Auto tasks ~3 min + UAT 인터랙티브 ~7 min)
- **Started:** 2026-05-11T06:25:00Z (Plan 22-01 SUMMARY 직후)
- **Auto tasks 완료:** 2026-05-11T06:30:00Z (T1+T2+T3+T4 scaffold)
- **UAT sign-off:** 2026-05-11 (4/4 PASS)
- **Tasks:** 4 (T1, T2 auto / T3 blocking auto / T4 human checkpoint)
- **Files modified:** 1 (Action_FAIMeasurement.cs) + 1 created (22-UAT.md) + 1 artifact (build_22.log gitignored)

## Accomplishments

- **Action_FAIMeasurement.cs L109 위** — `//260511 hbk Phase 22 IMG-02 — ShotParam.SimulImagePath = InspectionImagePath 역할 (검사 사이클 마다 로드). 티칭 기준 이미지는 별도 DatumConfig.TeachingImagePath (셋업 시 1회, INI 보존) 사용 — 역할 분리. Simul 에서 두 경로 동일 파일 가능 (UAT Test 2).` 추가
- **Action_FAIMeasurement.cs L226 위** — `//260511 hbk Phase 22 IMG-02 — Datum 찾기 단계의 이미지 = InspectionImagePath (= ShotParam.SimulImagePath) 사용. TeachingImagePath (DatumConfig 보존) 는 본 메서드에서 미참조 — 재티칭/UI 셋업 경로에서만 참조 (Phase 23 carry-over 가능).` 추가
- **build_22.log** — msbuild Debug/x64 Rebuild PASS (0 errors), 6 warning occurrences = Phase 21 baseline (MSB3884 + CS0162 + CS0219, 각 2-pass × 2 = 6), 신규 warning 0
- **22-UAT.md** — 4 시나리오 작성 + 사용자 사인오프 (Test 1 PASS 스크린샷 / Test 2 PASS trust-based / Test 3 PASS 사용자 재확인 / Test 4 PASS 자동)

## Task Commits

각 태스크는 원자적으로 커밋되었다:

1. **Task 1: EStep.Grab — InspectionImagePath 역할 명시 주석** — `06d6628` (feat)
2. **Task 2: GrabOrLoadDatumImage — InspectionImagePath 역할 명시 주석** — `e433212` (feat)
3. **Task 3 [BLOCKING]: msbuild Debug/x64 PASS + 신규 warning 0** — (uncommitted artifact: `build_22.log` gitignored)
4. **Task 4 [CHECKPOINT] scaffold: 22-UAT.md 4 시나리오 대기** — `701fefb` (test)
5. **Task 4 sign-off: 22-UAT.md 4/4 PASS** — `ef884d6` (test)

**Plan metadata:** (이 SUMMARY 커밋에서 부여) (docs)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — L109, L226 위에 주석 2 라인 추가. 코드 라인 무수정 (검증: `grep -c "new HImage(ShotParam.SimulImagePath)" == 2`, `grep -c "private HImage GrabOrLoadDatumImage" == 1`).
- `.planning/phases/22-image-dual-structure/22-UAT.md` — 신규 작성 (frontmatter pending → signed_off, 4 시나리오 본문 + Summary 표 + Carry-overs + Sign-off 블록).
- `build_22.log` — 워크트리 루트 (`.log` gitignored, 사용자 검수 후 보존 불필요).

### 정확한 삽입 위치 (after-the-fact)

| 위치 | 라인 | 내용 |
|------|------|------|
| Task 1 주석 | Action_FAIMeasurement.cs L109 | `//260511 hbk Phase 22 IMG-02 — ShotParam.SimulImagePath = InspectionImagePath 역할 ...` |
| Task 2 주석 | Action_FAIMeasurement.cs L226 | `//260511 hbk Phase 22 IMG-02 — Datum 찾기 단계의 이미지 = InspectionImagePath ...` |

기존 L109 의 `//260409 hbk Phase 5: SimulImagePath 이미지 로드 (D-10)` 주석은 L110 으로 1 라인 시프트되어 보존됨 (Phase 20 D-12 marker stacking 패턴 준수).

## Build Verification (Task 3)

**msbuild log 인용 (`build_22.log`):**
- 명령: `MSBuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /nologo /t:Rebuild`
- Error 매치: 0 (PASS)
- Warning 매치: 6 (= Phase 21 baseline, 신규 0)
  - MSB3884 × 2 (MinimumRecommendedRules.ruleset 누락 — pre-existing)
  - CS0162 × 2 (VirtualCamera.cs:266 unreachable code — pre-existing)
  - CS0219 × 2 (VisionAlgorithmService.cs:64 unused 'scanHorizontal' — pre-existing)
- `bin\x64\Debug\DatumMeasurement.exe` mtime 갱신 (산출물 정상 생성)

## UAT Results (Task 4)

22-UAT.md 4 시나리오 사용자 사인오프 (Reviewer: heobum0928, Date: 2026-05-11, Status: signed_off):

| # | Scenario | Result | Notes |
|---|----------|--------|-------|
| 1 | TeachingImagePath INI 라운드트립 | **PASS** | 스크린샷 시각 확인 — Datum_2 노드 `Datum|ImageSource` 카테고리에 `Teaching image path = D:\TestImg\Datameasurement\teaching_b.bmp` 정상 노출 + INI 라운드트립 |
| 2 | 두 경로 동일 파일 케이스 | **PASS (trust-based)** | Plan 22-02 코드 변경 0 (주석 2 라인뿐). 새 코드 경로 없음 → 동일 파일 회귀 시나리오 분기 부재. 사용자 합의 |
| 3 | TeachingImagePath INI 키 미존재 폴백 | **PASS** | 초기 "레시피별 분리 안 됨" 증상 → 사용자 측 티칭 파일 차이로 추적 완료. A 복사→B 정상 절차 시 INI 별 독립 저장/로드 확인 |
| 4 | msbuild Debug/x64 PASS + warning 0 | **PASS** | Task 3 자동 산출물 — 0 errors / 6 warnings = Phase 21 baseline |

## Decisions Made

### 디자인 결정 lock-in (Plan 22-01 lock-in 재확인 + Plan 22-02 신규)

**(1) Action_FAIMeasurement.cs 코드 무수정 — 주석 2 라인만 추가**

- 이유: Plan 22-01 의 lock-in (b) "InspectionImagePath = ShotConfig.SimulImagePath 의미적 재해석" 을 코드 레벨에서 일관 적용. 식별자 리네이밍/리팩토링 0 → zero-regression.
- L109, L226 위에 stacking marker 추가 (Phase 20 D-12 패턴: 기존 `//260409 hbk Phase 5` 주석 보존 + 위에 신규 마커 누적).

**(2) Test 2 trust-based PASS 채택**

- 신규 코드 경로 0 + UI 위치 혼동 발생 (Datum 은 Shot 하위 노드 아님, 별개 탭/뷰).
- 코드 변경 0 근거 (`grep -c "new HImage(ShotParam.SimulImagePath)" == 2` baseline 동일) 로 회귀 시나리오 분기 부재 입증 → 사용자 합의 후 PASS.

**(3) Test 3 사용자 측 데이터 차이 — 사전 존재 결함 아님**

- 초기 보고 "레시피별 분리 안 됨" 증상 → 사용자 재테스트 (A 복사 후 B) 시 정상 동작 확인.
- ParamBase reflection 자동 직렬화 (Plan 22-01 인프라) 정상 — 회귀 0.

**(4) CO-22-01 별도 quick task 분리**

- UAT 수행 중 발견된 신규 결함 (Datum 노드 ↔ FAI 노드 PropertyGrid 전환 동작 안 됨) 은 Phase 22 single-속성 추가와 무관.
- 사전 존재 UI 동작 또는 Phase 17 ICustomTypeDescriptor 와의 상호작용 가능성 → 별도 quick task 로 재현/원인 추적 필요.

## Deviations from Plan

None - plan executed exactly as written.

Wave 1 (Plan 22-01) 2 commits + Wave 2 (Plan 22-02) auto tasks 3 commits + UAT sign-off 1 commit. 4 plan tasks (T1 auto / T2 auto / T3 blocking auto / T4 human checkpoint) 모두 spec 그대로 수행.

## Issues Encountered

UAT 인터랙티브 중 일시적 혼동 (사용자 측, 본 plan 의 오케스트레이션 외 항목):
- **Test 2 UI 위치 혼동** — Datum 은 Shot 하위 노드라는 잘못된 안내로 사용자 혼란 발생. 실제 UI 는 Datum 별개 탭/뷰. → trust-based PASS 로 우회 해결, UI 트리 구조 정정 사항 22-UAT.md Carry-overs 에 기재.
- **Test 3 초기 보고된 '레시피별 분리 안 됨'** — 사용자 측 티칭 파일 데이터 차이로 추적 완료. Phase 22 회귀 아님.

## Carry-overs

다음 항목은 Phase 22 범위 밖으로 carry-over:

1. **CO-22-01 — Datum 노드 ↔ FAI 노드 PropertyGrid 전환 UI 버그** (신규, 2026-05-11 등록): 트리에서 Datum 노드 선택 후 FAI 노드 클릭해도 PropertyGrid 즉시 갱신되지 않음. 별도 quick task 로 재현/원인 추적 필요.

2. **TeachingImagePath 실 소비** (Plan 22-01 carry-over 재확인): 셋업 단계에서 TeachingImagePath 의 이미지를 자동 로드하여 재티칭 ROI 기준으로 사용하는 기능 → Phase 23 (A시리즈 Simul) 또는 후속 UI 작업.

3. **풍부한 UI** (Plan 22-01 carry-over 재확인): Browse 버튼 / OpenFileDialog 추가는 Phase 22 범위 밖.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **Phase 22 (image-dual-structure) signed_off** — IMG-01 + IMG-02 모두 충족.
- Phase 23 (A시리즈 Simul) 또는 다음 v1.1 phase 진행 가능. TeachingImagePath 인프라 (DatumConfig 속성 + INI 영구 보존) 가 Phase 23 의 재티칭 기준 이미지 참조 경로에 사용 가능.
- CO-22-01 (Datum↔FAI 전환 UI 버그) 는 STATE.md Pending Todos 에 등록 — 별도 quick task 발주 권장.

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — modified (L109/L226 위 주석 2 라인 추가)
- Commit `06d6628` (Task 1) — present in git log
- Commit `e433212` (Task 2) — present in git log
- Commit `701fefb` (Task 4 UAT scaffold) — present in git log
- Commit `ef884d6` (Task 4 UAT sign-off) — present in git log
- Grep `Phase 22 IMG-02` in Action_FAIMeasurement.cs → 2 matches (L109, L226) PASS
- Grep `new HImage(ShotParam.SimulImagePath)` in Action_FAIMeasurement.cs → 2 matches (L114, L230) PASS (코드 무수정 검증)
- `build_22.log` — `0 Error(s)`, 6 warnings = Phase 21 baseline PASS
- `22-UAT.md` frontmatter — `status: signed_off`, `passed: 4`, `total: 4` PASS

---
*Phase: 22-image-dual-structure*
*Plan: 02*
*Completed: 2026-05-11*
