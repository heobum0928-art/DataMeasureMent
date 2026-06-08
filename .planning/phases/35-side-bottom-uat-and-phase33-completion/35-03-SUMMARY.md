---
phase: 35-side-bottom-uat-and-phase33-completion
plan: 03
status: completed
completed: 2026-05-27
---

# Plan 35-03 SUMMARY — 통합 SIMUL UAT (Phase 35 partial sign-off)

## Task 결과

| Task | 결과 | 상세 |
|---|---|---|
| 1. msbuild Debug/x64 Rebuild | PASS | EXIT=0, 0 errors, 신규 warning category 0 (baseline 5×CS0618 + 1×CS0162 + 1×MSB3884 유지) |
| 2. Top 단일-Load (Phase 23.1 회귀) | PASS | Phase 23.1 sign-off 시점과 동일 — 회귀 0 |
| 3. Top 다중-Load (CO-33-02 핵심) | PASS | Plan 35-01 hotfix 로 해소 — Datum 검출 성공 + 측정값 정상 |
| 4. Side 시퀀스 Datum + FAI | CARRY_OVER → Phase 34 | VerticalTwoHorizontal 듀얼 티칭 이미지 (CO-33-05) 의존 — Phase 34 완료 후 재검증 |
| 5. Bottom 시퀀스 SIMUL + Shot 재로드 | PASS | Datum 검출 + FAI 측정 + Shot 재로드 모두 정상 — CO-33-06 + CO-35-01 + CO-35-02 해소 검증 |
| 6. INI 라운드트립 (Top + Bottom) | PASS | OwnerSequenceName=TOP/BOTTOM 자동 직렬화 + 재로드 시 시퀀스별 트리 복원 정상 |

## UAT 중간 발견 회귀 + hotfix

UAT Wave 3 진행 중 사용자 보고 2건 → 단일 commit `1b0894b` 로 hotfix:

### CO-35-01 — "There is no action to run" 에러
- **증상**: Bottom Shot 노드 클릭 → Start 버튼 → 에러 메시지
- **Root cause**: `InspectionListView.ResolveRunnableAction` Shot 분기 가 글로벌 `RecipeManager.Shots.IndexOf(shotCfg)` 사용. Plan 35-02 의 `RebuildInspectionActions` 가 시퀀스별 로컬 actionIdx (0/1/2) 로 action 부여하므로 mismatch
- **Fix**: `ComputeLocalShotIndex` static helper 신규 — RebuildInspectionActions 의 actionIdx 부여 로직과 1:1 대응 (시퀀스 필터링 + 로컬 카운터)

### CO-35-02 — Bottom Shot 저장/재로드 후 사라짐 (사용자 보고 "가장 큰 이슈")
- **증상**: Bottom Shot 셋팅 후 앱 재시작 → Datum 외 전부 사라짐
- **Root cause**: `SequenceHandler.TryLoadNewFormat` 가 `RebuildInspectionActions(ESequence.Top)` 만 호출. Side/Bottom 시퀀스는 INI 로드 후 `seq.ActionCount = 0` → `InspectionListViewModel.CreateSequenceNode` 가 `seq[j]` 순회 시 빈 결과 → 트리에 Shot 안 보임
- **Fix**: TryLoadNewFormat 에서 Top/Side/Bottom 3 시퀀스 모두 RebuildInspectionActions 호출. 각 호출이 OwnerSequenceName 으로 필터링하므로 시퀀스별 소유 Shot 만 attach.

두 hotfix 모두 Plan 35-02 의 명시적 **Part D 영역** (`<verification>` 의 "트리 재구축 시 OwnerSequenceName 필터링 — Plan 35-03 UAT 결과로 hotfix") 에 해당.

## 가설 검증 결과

### CO-33-02 단일 root cause 가설 — **확정**

Plan 35-01 (이미지 캐시 hotfix) 의 광범위 변경:
- HalconViewerControl 두 LoadImage 오버로드 일관성 (CurrentImagePath null vs "" 의미 분리)
- LoadImage(string) 캐시 hit 가드 강화
- MainView.DisplayDatumImage 신규 (Datum 노드 선택 시 TeachingImagePath 자동 캔버스 표시)

→ **Top + Bottom 동시 해소** 확인 (Test 3 + 5 PASS)
→ Side 는 단일 root cause 가 아니라 dual-image fixture 의존성 (CO-33-05) 으로 별도 처리

### CO-33-06 (per-sequence Shot ownership) — **해소**

Plan 35-02 (OwnerSequenceName 아키텍처) + hotfix CO-35-02 (TryLoadNewFormat 3 시퀀스 호출) 통합:
- ShotConfig.OwnerSequenceName 자동 직렬화
- ApplyShotDefaults 폴백 (D-B1) — 기존 INI 호환 (TOP 폴백)
- ResolveSequenceName helper
- RebuildInspectionActions 시퀀스별 로컬 actionIdx
- TryLoadNewFormat 가 모든 시퀀스 트리 빌드

→ Bottom Shot 셋팅 → Save → 재시작 → Load → Bottom 시퀀스 트리 정상 복원 PASS

## Phase 35 → Phase 24 인계

Phase 24 (검사 워크플로우 end-to-end) prerequisite 충족:
- Top + Bottom 시퀀스 SIMUL 검증 완료
- 시퀀스별 Shot ownership 모델 안정
- 이미지 캐시 stale 회귀 해소
- INI 라운드트립 (Top + Bottom) PASS

Side end-to-end 는 Phase 34 (Datum 듀얼 티칭) 완료 후 Phase 24 에서 함께 검증 가능.

## v1.1 잔여 phase

| Phase | 상태 | 비고 |
|---|---|---|
| 34 | next | Datum VerticalTwoHorizontal 듀얼 티칭 이미지 (CO-33-05) — Side 검증 prerequisite |
| 24 | planned | 검사 워크플로우 end-to-end (Phase 34 후) |
| 25 | planned | 결과 분석 & Export |
| 27 | planned | Side Inspection 확장 (Phase 999.1 흡수) |

## 변경 파일 종합 (Phase 35 전체)

| Wave | Plan | Commit | Files | Lines |
|---|---|---|---|---|
| 1 | 35-01 (이미지 캐시 hotfix) | 17ccc91 | HalconViewerControl + MainView + InspectionListView | +38 / -5 |
| 1 | 35-01 SUMMARY | 2ea2c2a | 35-01-SUMMARY.md | docs only |
| 2 | 35-02 (OwnerSequenceName) | 11a6f61 | ShotConfig + InspectionRecipeManager + SequenceHandler + InspectionListView | +51 / -4 |
| 2 | 35-02 SUMMARY | 0c4d7de | 35-02-SUMMARY.md | docs only |
| 3 | hotfix (CO-35-01 + CO-35-02) | 1b0894b | SequenceHandler + InspectionListView | +26 / -2 |

**D-06 가드 (Phase 33 계승)**: InspectionSequence.cs / Action_FAIMeasurement.cs / VisionResponsePacket.cs **3 파일 모두 변경 0 라인** (Phase 35 전체 commit 통과).

## Phase 33 retro 부분 sign-off

Phase 33 의 carry-over 4건 중 3건 해소:
- CO-33-02 ✅ 해소
- CO-33-03 ⚠ Top + Bottom 해소, Side → Phase 34
- CO-33-04 ⚠ Test 3/4/5 PASS, Test 2 → Phase 34
- CO-33-06 ✅ 해소

Phase 33 status = 계속 partial (Test 2 Side carry-over 일환). Phase 34 완료 후 fully signed_off 가능.
