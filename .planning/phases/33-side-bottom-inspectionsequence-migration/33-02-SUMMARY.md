---
phase: 33-side-bottom-inspectionsequence-migration
plan: 02
status: complete
created: 2026-05-26
updated: 2026-05-26
commits:
  - a9a2cc5
---

# Plan 33-02 SUMMARY — InspectionRecipeManager 시퀀스별 FIXTURE 직렬화

## What was built
- **`ResolveFixtureSequence(ESequence)` overload** (L82-89): 시퀀스별 InspectionSequence 해석
- **`SaveFixtureForSequence(IniFile, ESequence, string)` helper** (L92-105): 시퀀스 + 섹션 prefix → INI 저장
- **`LoadFixtureForSequence(IniFile, ESequence, string)` helper** (L108-126): INI → 시퀀스 DatumConfigs 복원. 섹션 부재 시 빈 DatumConfigs 로 clear 후 early return
- **`SavePhase6Format` 본문** (L173-174): `FIXTURE_SIDE` + `FIXTURE_BOTTOM` 신규 섹션 저장 2 라인 추가
- **`LoadPhase6Format` 본문** (L247-248): `FIXTURE_SIDE` + `FIXTURE_BOTTOM` 로드 2 라인 추가

## Key files changed
| File | Lines | Note |
|------|-------|------|
| WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs | +54 | 헬퍼 3 메서드 + 호출 4 라인 + 주석 6 라인 |

## Self-Check
- ✓ `ResolveFixtureSequence()` (zero-arg, L72-79) byte-identical 보존 — 시그니처/본문/호출 사이트(L113, L182) 모두 무변경
- ✓ Top `[FIXTURE]` 섹션 저장 블록(L113-124) byte-identical, 로드 블록(L182-194) byte-identical
- ✓ 신규 헬퍼는 별도 ESequence overload — Top 폴백 경로 보호 (T-33-05 mitigation)
- ✓ `LoadFixtureForSequence` 의 `seq.DatumConfigs.Clear()` 가 `ContainsSection` 확인 전에 호출 — 기존 Phase 6 INI 로드 시 Side/Bottom 의 stale 데이터 잔존 차단 (T-33-07 mitigation)
- ✓ D-06 가드: `InspectionSequence.cs` / `Action_FAIMeasurement.cs` / `VisionResponsePacket.cs` `git diff = 0` 라인 확인 완료
- ✓ grep 검증: `ResolveFixtureSequence(ESequence` ×1, `FIXTURE_SIDE` ×3, `FIXTURE_BOTTOM` ×3, `SaveFixtureForSequence` ×3, `LoadFixtureForSequence` ×3, `260526 hbk Phase 33` ×6
- ⏳ msbuild Debug/x64 Rebuild 검증은 Plan 33-03 Task 1 에서 일괄 수행 (앱 실행 중 PID 9012 → MSB3027 회피)

## Open Question 결정 인용
- **Open Q1 (lock — CONTEXT.md)**: `ResolveFixtureSequence(ESequence)` overload + 시퀀스별 헬퍼 패턴 채택. zero-arg overload 보존으로 Top 폴백 호출 site 회귀 0.
- **Open Q2 (lock — Plan 33-02 objective)**: Bottom Multi-Die 자동 FAI 매핑은 **Die_{i}_X / Y / Angle 3 FAI 분리 (D-03 Option B)** 방침으로 lock. FAIResultData 의 단일 `DistanceMm` 필드 + InspectionSequence.AddResponse 기존 루프 byte-identical 재사용 보장. 실제 자동 데이터 생성 코드는 **v1.2 Bottom 도메인 작업으로 이연** — Phase 33 sign-off 시점에는 사용자 수동 Shot/FAI 추가로 Bottom 검사 검증.
- **D-04 (단순화 — planner 결정)**: 레거시 BottomSequence/TopSequence INI (`[BOTTOM_DIE_*]` 등) 자동 변환은 수행하지 않음. 사유: Phase 6 reject 메시지 + v1.2 이연으로 변환 의미 부재.

## Wave 3 인계 (Plan 33-03 UAT)
- **Test 1 msbuild Debug/x64 Rebuild**: 앱 PID 9012 종료 후 수행 — exit 0, error 0, Phase 22 baseline (6 warnings) + CS0618 4건 = 최대 10 warning 허용
- **Test 2 Side SIMUL UAT**: Datum 추가 + 티칭 + FAI 측정
- **Test 3 Bottom SIMUL UAT**: 동일 — Multi-Die 자동매핑은 v1.2 이연이므로 수동 Shot/FAI 추가 시나리오
- **Test 4 Top 회귀 SIMUL**: Phase 23.1 sign-off 시점 동작 100% 동일 확인 (Top `[FIXTURE]` 섹션 byte-identical 검증)
- **Test 5 INI 라운드트립**: Side/Bottom Save → 재로드 → DatumConfigs 보존 확인 (`FIXTURE_SIDE_DATUM_0` 등 신규 섹션 INI 파일 직접 확인 가능)

## Commits
- `a9a2cc5` feat(33-02): Side/Bottom DatumConfigs INI 직렬화 — FIXTURE_SIDE/BOTTOM
