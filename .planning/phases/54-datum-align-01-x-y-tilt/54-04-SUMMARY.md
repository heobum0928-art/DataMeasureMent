---
phase: 54-datum-align-01-x-y-tilt
plan: "04"
subsystem: DatumPhase-Align
tags: [datum, pattern-align, roi-transform, leveling, halcon]
dependency_graph:
  requires:
    - DatumConfig ALIGN-01 필드 (54-01)
    - RecipeFiles.GetPatternModelFilePath (54-02)
    - PatternMatchService.TryFindPose/TryBuildAlignRigid (54-03)
  provides:
    - InspectionSequence.ResolveDatumModelPath (54-05 공유 키 헬퍼, D-07)
    - InspectionSequence.TryComposeAlign (D-02 ①②③④)
    - InspectionSequence.MarkAlignFailed/IsAlignFailed/_alignFailedDatums (D-10)
    - DatumFindingService.TryGetLevelingAngle(dRow,dCol) offset 오버로드
    - Action_FAIMeasurement DatumPhase align 통합 + RotateImageByAngle 0회
  affects:
    - Wave 3: 패턴 티칭 UI는 동일 ResolveDatumModelPath 키 사용 (54-05)
tech_stack:
  added: []
  patterns:
    - _datumTransforms 누적 + ROI 좌표변환 자동 추종 (Measure 무수정)
    - lenient 실패 게이트 (MarkAlignFailed → 측정 NG, abort 0)
key_files:
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "측정=원본 픽셀 (RotateImageByAngle 4곳 전면 폐기, D-03/D-05 warp 0회)"
  - "dRow=curRow-RefMatchRow, dCol=curCol-RefMatchCol (매칭 변위 부호)"
  - "합성 = HomMat2dCompose(alignRigid, existing); TryComposeAlign 은 align 단독 경로(검출 미수행) → 통상 existing 없음 → align 1회 적용 보장"
  - "모델 경로 = ResolveDatumModelPath(datum) 단일 헬퍼 (54-05 와 동일 키, D-07)"
  - "DualImage 분기 align 미삽입 (회전만 폐기) — 후속 phase deferred"
  - "line-fit θ 실패 → θ=0 폴백 (x,y 보정 유지, lenient)"
metrics:
  duration: "post-merge orchestrator-finished"
  completed: "2026-06-18"
  tasks_completed: 2
  tasks_total: 2
  files_modified: 3
---

# Phase 54 Plan 04: DatumPhase 패턴매칭 보정 통합 Summary

**One-liner:** IsPatternAlignEnabled 가드 하에 보정 전 원본 grab 이미지로 패턴 매칭(x,y) → 변위만큼 이동한 line-fit ROI에서 정밀 θ → (x,y+θ) rigid를 `_datumTransforms[DatumName]`에 합성. 측정은 ROI 좌표변환으로 자동 추종(Measure 무수정), RotateImageByAngle 전면 폐기(warp 0회), off=identity(회귀 0).

## Commits

| Task | Commit | Files |
|------|--------|-------|
| 1: offset 오버로드 + ResolveDatumModelPath/TryComposeAlign/align-fail set | cda9f3e | DatumFindingService.cs, InspectionSequence.cs |
| 2: DatumPhase align 삽입 + ALIGN_FAIL 게이트 + RotateImageByAngle 폐기 | e4a92bf | Action_FAIMeasurement.cs |

## 핵심 규약

- **부호:** `dRow = curRow - datum.RefMatchRow`, `dCol = curCol - datum.RefMatchCol`
- **합성:** `HomMat2dCompose(alignRigid, existing)`. TryComposeAlign 은 align 단독 경로(검출 미수행)로 호출 → 통상 existing 없음 → alignRigid 단독 저장 = align 1회 적용 보장(W4)
- **lenient(D-10):** 매칭 score 미달/모델 로드 실패/rigid 실패 → false + `_datumTransforms` 미변경 → MarkAlignFailed → 측정 NG(LastSkipReason=ALIGN_FAIL), 시퀀스 abort 0
- **경로 단일소스(D-07):** `InspectionSequence.ResolveDatumModelPath(datum)` (actName="Datum" 상수, propertyName=DatumName, engine=PatternEngine) — 54-05 티칭과 동일 헬퍼

## Verification

- MSBuild Debug/x64: **PASS** (EXIT=0, 합본 빌드)
- `grep -c "RotateImageByAngle" Action_FAIMeasurement.cs` → **0** (호출 4곳 전면 폐기; 주석에도 API명 미잔존)
- `grep -c "Handle.RecipeFiles"` → 0 (Recipes 정정 확인)
- grep acceptance: ResolveDatumModelPath / TryComposeAlign / TryGetLevelingAngle(dRow,dCol) / HomMat2dCompose(alignRigid,existing) / MarkAlignFailed / IsAlignFailed / ALIGN_FAIL 전부 존재
- **미수행(런타임):** SIMUL 평행이동 페어 부호 방향 검증(W3) — 실데이터 UAT 필요 (Wave 3 UI 완료 후 통합 테스트로 이관)

## ⚠ 실행 노트 (deviation)

54-04 에이전트가 3개 파일을 수정했으나 디스패치 내부 오류로 최종 메시지·커밋 유실. 오케스트레이터가 미커밋 변경을 grep acceptance 전수 검증 + 빌드 PASS 확인 후 2개 task 커밋으로 마무리. RotateImageByAngle 폐기 주석에 남아있던 API 문자열을 제거하여 grep BLOCKER(==0) 충족.

## Self-Check: PASSED (orchestrator-verified, 런타임 부호검증 carry-over)
