---
phase: 33-side-bottom-inspectionsequence-migration
plan: 01
status: complete
created: 2026-05-26
updated: 2026-05-26
commits:
  - d7aef99
  - 8d5fcb5
---

# Plan 33-01 SUMMARY — SequenceHandler 마이그레이션 + 4 레거시 클래스 deprecate

## What was built
- **SequenceHandler.RegisterSequences L30-34**: Side/Bottom 인스턴스를 `TopSequence` / `BottomSequence` → `InspectionSequence` 로 교체 (D-01)
- **SequenceHandler.RegisterActions L37-43**: placeholder 주석 추가 (등록 라인은 byte-identical, RebuildInspectionActions 가 동적 FAI 모드 진입 시 교체)
- **Sequence_Top.cs L41 TopSequence**: `[System.Obsolete(..., false)]` 부여 (D-05)
- **Sequence_Bottom.cs L89 BottomSequence**: `[System.Obsolete(..., false)]` 부여
- **Action_TopInspection.cs L194 TopInspectionAction**: `[System.Obsolete(..., false)]` 부여
- **Action_BottomInspection.cs L283 BottomInspectionAction**: `[System.Obsolete(..., false)]` 부여

## Key files changed
| File | Lines | Note |
|------|-------|------|
| WPF_Example/Custom/Sequence/SequenceHandler.cs | +4 / -2 | RegisterSequences Side/Bottom 라인 + 2 신규 주석 |
| WPF_Example/Custom/Sequence/Top/Sequence_Top.cs | +2 | [Obsolete] + 주석 |
| WPF_Example/Custom/Sequence/Bottom/Sequence_Bottom.cs | +2 | [Obsolete] + 주석 |
| WPF_Example/Custom/Sequence/Top/Action_TopInspection.cs | +2 | [Obsolete] + 주석 |
| WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs | +2 | [Obsolete] + 주석 |

## Self-Check
- ✓ RegisterSequences Top 라인 byte-identical (D-06)
- ✓ 4 레거시 클래스에 [Obsolete] 부여 — grep 4/4 PASS
- ✓ BottomDieInfo / TopSequenceContext / BottomSequenceContext 등 helper 는 미수정 (Wave 2 재활용 가능 상태)
- ✓ InspectionSequence.cs / Action_FAIMeasurement.cs / InspectionRecipeManager.cs **변경 0 라인** (D-06, `git diff` 검증 완료)
- ⏳ msbuild Debug/x64 Rebuild 검증은 Plan 33-03 Task 1 에서 일괄 수행 (앱 실행 중 — MSB3027 회피)

## D-06 회귀 가드 증명
```
git diff HEAD~2 HEAD -- \
  WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
  WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs \
  WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
# (empty output — 3 파일 모두 0 라인 변경)
```

## Wave 2 인계
- **Plan 33-02 (InspectionRecipeManager)**: Side/Bottom DatumConfigs INI 직렬화 미배선 — `[FIXTURE_SIDE]` / `[FIXTURE_BOTTOM]` 섹션 + `SaveFixtureForSequence` / `LoadFixtureForSequence` 헬퍼 도입 예정 (PATTERNS.md L470-485 Open Question 1 해법)
- Bottom Multi-Die 자동 FAI 매핑 (D-03 Option B = Die_{i}_X/Y/Angle 3 FAI 분리) 은 **v1.2 이연** — Phase 33 sign-off 시점에는 사용자 수동 Shot/FAI 추가로 Bottom 검사 검증.

## Commits
- `d7aef99` feat(33-01): RegisterSequences Side/Bottom -> InspectionSequence (D-01)
- `8d5fcb5` feat(33-01): deprecate 4 legacy classes with [Obsolete] (D-05)
