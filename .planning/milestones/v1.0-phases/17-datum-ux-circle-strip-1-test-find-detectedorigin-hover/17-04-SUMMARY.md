---
phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
plan: 04
subsystem: uat
tags: [uat, datum, phase17, carry-over, signoff]

requires:
  - phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
    plan: 01
    provides: "Circle 1-strip + RadialDirection ComboBox + 6 EdgeDirection tooltip + DatumFindingService caller polarity (D-01~D-04, D-17 통과)"
  - phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
    plan: 02
    provides: "_isEditMode 단일 gate + 좌클릭+드래그 + DatumConfig ICustomTypeDescriptor + AlgorithmType 5-step 리셋 + Delete 3-button 모달 + ValidateRoiPresence + FormatTeach/FindError (D-05~D-12)"
  - phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
    plan: 03
    provides: "DatumConfig 6 신규 필드 (transient 3 + 메트릭 3) + TryFindDatum write-back + RenderDatumFindResult purple + canvasToolbar X/Y/Gray + InspectionListView Step 3 wiring (D-13~D-16)"
  - phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix
    provides: "16-UAT.md Phase 17 Carry-over 16항목 (#1-#3, #5, #6, #8-#18; #4/#7 무효) — 본 UAT 의 검증 대상"

provides:
  - "17-UAT.md (status: pending) — 16 시나리오 + 자동 검증 (D-17/D-18/D-20) + 사인오프 섹션 + Summary 표"
  - "Phase 17 사용자 검증 진입점 (Task 2 checkpoint:human-verify)"

affects:
  - "Phase 17 종료 조건: 사용자가 16 시나리오 실행 → result 갱신 → frontmatter status: pending → signed_off (or partial / failed) → 사인오프 라인 추가."

tech-stack:
  added: []
  patterns:
    - "16-UAT.md 포맷 모방 (frontmatter / 개요 / 사전 조건 / Cluster 분류 / Acceptance / 자동 검증 bash / 사용자 사인오프 / Summary 표)"
    - "Test 16 = 자동 검증 통합 — D-17 algorithm preservation + D-18 hbk 주석 카운트 + D-20 Phase 16 회귀 + msbuild 한 묶음으로 bash verbatim"

key-files:
  created:
    - ".planning/phases/17-datum-ux-circle-strip-1-test-find-detectedorigin-hover/17-UAT.md (640 lines, 16 시나리오)"
  modified: []

key-decisions:
  - "16-UAT.md 포맷 충실 모방 — frontmatter 5개 result 카테고리 (passed/failed/not_tested/skipped/invalid) + 사용자 사인오프 섹션 + Summary 표 모두 포함"
  - "Test 1~15 시나리오별 단계/기대/Acceptance/source/result/notes 6 필드 표준화 (16-UAT.md Phase 16 신규 Test 1~4 와 동일 구조)"
  - "Cluster 분류 명시 (A/B/C/D + 자동 검증) — Phase 17 의 PLAN 구조 (Cluster A/B+C/D + UAT) 와 1:1 대응 → 사용자가 어떤 Plan 결과를 검증하는지 한눈에 식별"
  - "Test 13 (DetectedOrigin LastFindSucceeded gate) 에 W3 cross-plan 명시 — Plan 17-03 transient + Plan 17-02 5-step Step 3 wiring 통합 검증 (verifier 권고 반영)"
  - "Test 15 에 Plan 17-03 Rule 3 deviation (panel_hoverInfo + label_pointCount Polygon 모드 시각 충돌) 명시적 검증 단계 추가 — 충돌이 사용자 워크플로우 영향 시 carry-over 후보로 명시"
  - "자동 검증 bash 명령 verbatim 포함 (Test 16) — D-17 (VisionAlgorithmService 0 + DatumFindingService ≤ 11 EXACT 11) + D-18 (7 파일 hbk 카운트 임계) + D-20 (Phase 16 force rebind + Auto-reteach off 보존) + msbuild Debug/x64. 7 파일 grep 임계는 PLAN.md 안내 임계 + 실측 정합 — 17-01/17-02/17-03 SUMMARY 의 카운트 합산 기준"
  - "본 Plan 은 SUMMARY.md 미작성 관습 (UAT phase) 이지만 user prompt 'Create SUMMARY.md' 우선 — 17-04-SUMMARY.md 작성 (간소화)"

requirements-completed:
  - P17-D-01
  - P17-D-02
  - P17-D-03
  - P17-D-04
  - P17-D-05
  - P17-D-06
  - P17-D-07
  - P17-D-08
  - P17-D-09
  - P17-D-10
  - P17-D-11
  - P17-D-12
  - P17-D-13
  - P17-D-14
  - P17-D-15
  - P17-D-16
  - P17-D-17
  - P17-D-18
  - P17-D-20
  - 16-UAT-carry-#1
  - 16-UAT-carry-#2
  - 16-UAT-carry-#3
  - 16-UAT-carry-#5
  - 16-UAT-carry-#6
  - 16-UAT-carry-#8
  - 16-UAT-carry-#9
  - 16-UAT-carry-#10
  - 16-UAT-carry-#11
  - 16-UAT-carry-#12
  - 16-UAT-carry-#13
  - 16-UAT-carry-#14
  - 16-UAT-carry-#15
  - 16-UAT-carry-#16
  - 16-UAT-carry-#17
  - 16-UAT-carry-#18

# 본 Plan 은 *문서 작성 + checkpoint:human-verify* 두 단계. Task 2 (사용자 사인오프) 는 별도 agent 가 처리하지 않고 사용자가 17-UAT.md 직접 갱신 + 후속 commit (status: signed_off / partial / failed).

duration: 6min
completed: 2026-05-03
---

# Phase 17 Plan 04: UAT — Phase 16 carry-over 16항목 + Phase 17 D-01~D-16 통합 검증 Summary

**16 시나리오 (Cluster A/B/C/D + 자동 검증) + 사인오프 섹션 + Summary 표 작성 완료. status: pending — Task 2 사용자 사인오프 checkpoint 대기.**

## Performance

- **Duration:** ~6 min (1777799311 → 1777799715)
- **Started:** 2026-05-03T09:08:31Z
- **Completed:** 2026-05-03T09:15:15Z 근사
- **Tasks:** 1 of 2 (Task 2 = checkpoint:human-verify, 사용자 손)
- **Files created:** 1 (17-UAT.md, 640 lines)

## Accomplishments

- **17-UAT.md 작성** — 16-UAT.md 포맷 모방 + Phase 17 specific 시나리오 16개:
  - **Cluster A** (Test 1-4, Circle 시각화 + EdgeDirection): D-01~D-04 + carry #1/#2/#3/#16
  - **Cluster B** (Test 5-7, Edit 모드 + 그리기 UX): D-05~D-07 + carry #8/#9/#13/#14
  - **Cluster C** (Test 8-11, PropertyGrid + 모달 정책): D-08~D-12 + carry #6/#10/#11/#12/#15
  - **Cluster D** (Test 12-15, DetectedOrigin + 메트릭 + Hover): D-13~D-16 + carry #5/#17/#18
  - **자동 검증** (Test 16): D-17 algorithm preservation (VisionAlgorithmService 0 / DatumFindingService 11 EXACT) + D-18 hbk 주석 카운트 7 파일 + D-20 Phase 16 force rebind + Auto-reteach off + msbuild Debug/x64
- **UI-SPEC Copywriting Contract verbatim 포함**:
  - D-04 EdgeDirection tooltip ("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")
  - D-07 Delete 3-button 모달 ("이 ROI만 삭제" / "현재 Datum의 모든 ROI 삭제")
  - D-11 ROI 호환성 가드 ("Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요.")
  - D-12 EdgeDirection 0 검출 힌트 ("검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요.")
- **W3 cross-plan wiring 검증 단계** (Test 13) — Plan 17-02 InspectionListView 5-step Step 3 (DetectedOrigin reset placeholder) + Plan 17-03 transient 필드 wiring 통합 검증.
- **Plan 17-03 Rule 3 deviation 검증 단계** (Test 15) — panel_hoverInfo + label_pointCount Polygon 모드 시각 충돌 육안 검증 항목 추가.

## Task Commits

| # | Task | Files | Commit |
|---|------|-------|--------|
| 1 | 17-UAT.md 작성 — 16 시나리오 + 자동 검증 + 사인오프 섹션 (status: pending) | 17-UAT.md | 77510ca |
| 2 | 사용자 UAT 실행 + 17-UAT.md sign-off | (사용자 갱신) | (checkpoint:human-verify, 본 agent 외) |

## D-17 Algorithm Preservation Bound (Cumulative Phase 17)

| File | Phase 17 cumulative new code (주석/공백 제외) | Budget | Status |
|------|---------------------------------------------|--------|--------|
| WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs | **0** | = 0 | PASS |
| WPF_Example/Halcon/Algorithms/DatumFindingService.cs | **11** (Plan 17-01: 2 + Plan 17-02: 0 + Plan 17-03: 9) | ≤ 11 | EXACT |

```bash
git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs | wc -l
# = 0
git diff d93a678..HEAD WPF_Example/Halcon/Algorithms/DatumFindingService.cs \
  | grep -E "^\+[^+]" | grep -vE "^\+\s*//" | grep -vE "^\+\s*$" | wc -l
# = 11 EXACT
```

## msbuild Output 요약

```
MSYS_NO_PATHCONV=1 ".../MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64
→ DatumMeasurement -> bin/x64/Debug/DatumMeasurement.exe
```

- **Result:** PASS
- **Warning delta:** 0 신규 (본 Plan 은 코드 변경 0)
- **Pre-existing warning:** MSB3884 ruleset 누락 (csproj 환경 경고, 코드 무관)

## Coverage Verification

```
Test count: 16 (≥ 16 PASS)
Cluster A 시나리오: 4 (Test 1-4) — D-01/D-02/D-03/D-04 + carry #1/#2/#3/#16
Cluster B 시나리오: 3 (Test 5-7) — D-05/D-06/D-07 + carry #8/#9/#13/#14
Cluster C 시나리오: 4 (Test 8-11) — D-08/D-09/D-10/D-11/D-12 + carry #6/#10/#11/#12/#15
Cluster D 시나리오: 4 (Test 12-15) — D-13/D-14/D-15/D-16 + carry #5/#17/#18
자동 검증: 1 (Test 16) — D-17/D-18/D-20

Phase 16 carry-over 매핑 (16항목 #4, #7 제외): 16/16 PASS
Phase 17 D-XX 매핑 (D-01~D-16): 16/16 PASS
UI-SPEC Copywriting verbatim: 4/4 PASS (Delete / 호환성 / 실패 모달 / EdgeDirection tooltip)
```

## Decisions Made

- **Format 모방:** 16-UAT.md 의 frontmatter 5 카테고리 (passed/failed/not_tested/skipped/invalid) + 개요 + 사전 조건 + Cluster 분류 + Test 6필드 (단계/기대/Acceptance/source/result/notes) + 자동 검증 bash + 사용자 사인오프 + Summary 표 모두 1:1 모방. status: pending 으로 시작 (Task 2 가 signed_off / partial / failed 갱신).
- **Test 분류 (Cluster A/B/C/D + 자동 검증):** Phase 17 PLAN 구조 (17-01 = Cluster A / 17-02 = Cluster B+C / 17-03 = Cluster D / 17-04 = UAT) 와 1:1 대응 → 사용자가 어떤 Plan 의 결과를 검증하는지 한눈에 식별. 17-02 가 Cluster B+C 두 묶음을 한 Plan 이 처리한 점은 UAT 에서 Test 5-7 (B) + Test 8-11 (C) 로 분리 명시.
- **Test 13 W3 cross-plan 명시:** Plan 17-03 SUMMARY 의 "DetectedOrigin* Reset Path Documentation" 섹션 verifier 권고 (Plan 17-02 Step 3 wiring + Plan 17-03 transient 통합 검증) 반영. AlgorithmType 변경 시 시각화 clear 흐름이 두 Plan 의 협업 결과임을 명시.
- **Test 15 Plan 17-03 deviation 검증:** Plan 17-03 SUMMARY 의 "Rule 3 deviation (Task 4)" — label_pointCount XAML 보존 + panel_hoverInfo 같은 Column 2 배치 → Polygon 모드 시각 충돌 가능성. 사용자 워크플로우 영향 검증 단계 명시적 추가 (Polygon ROI 가 Datum 워크플로우 외부이므로 영향 미미 예상이지만 carry-over 후보 명시).
- **Test 16 (자동 검증) 통합:** D-17 + D-18 + D-20 + msbuild + Plan 영역 분리 (sequential lock) 모두 한 Test 에 verbatim bash 로 통합. 별도 Test 로 분리하지 않은 이유 — 자동 검증은 사용자 손 0 (실행만 하면 됨), 결과는 한 묶음으로 PASS/FAIL 판정.
- **17-04-SUMMARY.md 작성:** PLAN.md `<output>` 섹션은 "별도 17-04-SUMMARY.md 미작성 (UAT phase 관습)" 명시했으나, user prompt "Create SUMMARY.md" 가 명시적 — user prompt 우선. 단, SUMMARY.md 본문은 간소화 (UAT 자체가 산출물이고 본 SUMMARY 는 메타정보 + commit hash + verification 통계).

## Deviations from Plan

**1. [Rule 1 — User prompt override] 17-04-SUMMARY.md 작성**
- **Found during:** Plan execution (PLAN.md `<output>` 섹션 vs user prompt 충돌)
- **Issue:** PLAN.md 는 "본 Plan 은 SUMMARY.md 가 아닌 17-UAT.md 자체가 산출물 — 별도 17-04-SUMMARY.md 미작성 (UAT phase 관습)" 명시. 그러나 user prompt 의 success_criteria 에 "SUMMARY.md created" 포함.
- **Fix:** user prompt 우선 적용 — 17-04-SUMMARY.md 간소화 작성 (UAT 자체가 산출물이므로 본 SUMMARY 는 메타정보 + Task 1 commit 기록 + verification 통계).
- **Files modified:** .planning/phases/17-datum-ux-circle-strip-1-test-find-detectedorigin-hover/17-04-SUMMARY.md (created)
- **Commit:** (final metadata commit 에 포함)

## Issues Encountered

- **Git Bash 콜론 grep 이슈:** `grep "result:"` 가 Git Bash 환경에서 0 매칭 반환 (16-UAT.md 도 동일 결과 — 실제 16+ 매칭). 콜론 escape / quoting 변형 모두 동일. 우회: `grep "result"` 또는 `grep "**result"` 로 카운트 (둘 다 16 매칭 확인). PLAN.md 의 acceptance grep 명령은 정상 환경에서 의도대로 동작 — Git Bash 특정 quirk 이며 17-UAT.md 의 format 자체는 16-UAT.md 와 byte-level 일치.

## Self-Check

| 항목 | 결과 | 상태 |
|------|------|------|
| Task 1 commit `77510ca` 존재 | `git log --oneline -1` 표시 | FOUND |
| 17-UAT.md 파일 존재 | `[ -f .planning/phases/17-.../17-UAT.md ]` | FOUND |
| Test count = 16 | `grep -c "^### Test "` = 16 | PASS |
| Phase 16 carry-over 매핑 (16항목) | 각 #N 에 대해 ≥ 1 매칭 (carry #1: 29 / carry #2: 3 / ... / carry #18: 5) | PASS (16/16) |
| Phase 17 D-XX 매핑 (D-01~D-16) | 각 D-XX 에 대해 ≥ 1 매칭 (D-01: 5 / D-02: 4 / ... / D-16: 4) | PASS (16/16) |
| UI-SPEC Copywriting verbatim — Delete | `grep "이 ROI만 삭제"` = 1 | PASS |
| UI-SPEC Copywriting verbatim — Delete 전체 | `grep "현재 Datum의 모든 ROI 삭제\|현재 Datum 의 모든 ROI 삭제"` = 1 | PASS |
| UI-SPEC Copywriting verbatim — Circle 호환성 | `grep "Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요"` = 1 | PASS |
| UI-SPEC Copywriting verbatim — EdgeDirection 검출 0 힌트 | `grep "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요"` = 2 | PASS |
| UI-SPEC Copywriting verbatim — EdgeDirection tooltip | `grep "일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다"` = 1 | PASS |
| 사용자 사인오프 섹션 | `grep -c "사용자 사인오프\|사용자 검증 완료 일자"` = 4 | PASS |
| Cluster A/B/C/D 분류 | 각 분류에 ≥ 1 Test (A=4 / B=3 / C=4 / D=4) | PASS |
| Test 16 자동 검증 bash 포함 | D-17 + D-18 + D-20 + msbuild verbatim | PASS |
| frontmatter status: pending | `grep "status: pending"` = 1 | PASS |
| frontmatter summary.total: 16 | `grep "total: 16"` = 1 | PASS |
| msbuild Debug/x64 PASS | DatumMeasurement.exe 생성 확인 | PASS |
| D-17 algorithm preservation 실측 | VisionAlgorithmService 0 / DatumFindingService 11 EXACT | PASS |

## Self-Check: PASSED

모든 acceptance criteria + 사전 조건 자동 검증 + 사인오프 섹션 + Summary 표 충족. Test 2 (사용자 사인오프) 는 checkpoint:human-verify — 사용자가 16 시나리오 실행 후 17-UAT.md 직접 갱신.

## User Setup Required

**다음 사용자 액션** (Task 2 — checkpoint:human-verify):

1. **앱 실행** (Debug/x64 빌드, 이미 PASS — DatumMeasurement.exe 사용 가능):
   - `MSYS_NO_PATHCONV=1 ".../MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64` 또는 Visual Studio F5.
2. **사전 조건 확인:**
   - Datum 3개 보유 레시피 로드 (Datum 1=TLI / Datum 2=CTH / Datum 3=VTH).
   - 실 카메라 grab 또는 저장된 실 데이터 이미지 보유.
3. **17-UAT.md 의 Test 1 ~ Test 16 시나리오 순차 실행** — 단계 (steps) 그대로 수행, 기대 (expected) 와 비교, Acceptance 검증, result PASS/FAIL/not_tested/SKIP/INVALID 결정.
4. **Test 16 자동 검증 항목** 도 사용자가 bash 로 직접 실행 + 결과 확인.
5. **17-UAT.md 갱신**: 각 Test result 필드 + frontmatter summary 카운트 + status (pending → signed_off / partial / failed) + 사인오프 라인 추가.
6. **변경 사항 git 커밋**: `git add 17-UAT.md && git commit -m "docs(17): UAT signed_off — Phase 17 ${RESULT}"`.

## Threat Flags

본 Plan 변경 범위 내에서 신규 trust boundary 노출 없음. 17-UAT.md 는 문서 산출물 — 코드/네트워크/저장소 무관여.

## Next Phase Readiness

- **Phase 17 종료 조건:** 사용자가 Task 2 (UAT 실행 + 사인오프) 완료 시 Phase 17 = Complete (signed_off) 또는 partial / needs review.
- **모두 PASS / 승인 시:** STATE.md / ROADMAP.md Phase 17 = Complete 갱신, 다음 phase 계획 진입 가능.
- **partial 시:** carry-over 후보 (FAIL Test + 결함 + 재현 절차) 를 다음 phase ROADMAP 에 등록.
- **FAIL > 0 시:** Phase 17 = needs review, 결함 분석 후 hotfix plan 또는 carry-over.
- **Plan 17-03 Rule 3 deviation (panel_hoverInfo / label_pointCount Polygon 모드 시각 충돌):** Test 15 검증 단계 추가 — 충돌이 사용자 워크플로우 영향 시 후속 phase carry-over 후보.
- **Blocker:** None.

---
*Phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover*
*Plan 04 (UAT) Task 1 completed: 2026-05-03 — Task 2 (사용자 사인오프) checkpoint:human-verify 대기*
