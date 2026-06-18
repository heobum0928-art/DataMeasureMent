---
phase: 54-datum-align-01-x-y-tilt
plan: "02"
subsystem: ModelPath
tags: [datum, pattern-align, model-path, recipe, halcon]
dependency_graph:
  requires: []
  provides:
    - DeviceHandler.EXTENSION_SHAPE_MODEL (".shm")
    - DeviceHandler.EXTENSION_NCC_MODEL (".ncm")
    - RecipeFileHelper.GetPatternModelFilePath (engine-aware 경로 재계산)
  affects:
    - Wave 2: InspectionSequence.ResolveDatumModelPath (54-04)
    - Wave 3: 패턴 모델 생성/저장 (54-05)
tech_stack:
  added: []
  patterns:
    - GetModelFilePath(.mmf) 미러 — engine 분기로 .shm/.ncm
    - D-07 이름 기반 결정적 경로(절대경로 비저장)
    - D-07a 레시피 폴더 하위 저장(Directory.CreateDirectory 자동)
key_files:
  modified:
    - WPF_Example/Custom/Device/DeviceHandler.cs
    - WPF_Example/Utility/RecipeFileHelper.cs
decisions:
  - "기존 .mmf(MIL) 상수/GetModelFilePath 무수정 보존 (회귀 0)"
  - "engine='NCC' → .ncm, 그 외 → .shm (OrdinalIgnoreCase)"
metrics:
  duration: "5min"
  completed: "2026-06-18"
  tasks_completed: 2
  tasks_total: 2
  files_modified: 2
---

# Phase 54 Plan 02: 모델 경로/확장자 Summary

**One-liner:** HALCON shape/ncc 모델 확장자 상수(.shm/.ncm)를 DeviceHandler 에 추가하고, RecipeFileHelper 에 engine-aware 결정적 경로 재계산 메서드(GetPatternModelFilePath)를 추가 — 기존 MIL .mmf 경로는 무수정.

## Commits

| Task | Commit | Files |
|------|--------|-------|
| 1: 확장자 상수 | 050f7ac | DeviceHandler.cs (+4 lines) |
| 2: GetPatternModelFilePath | a4a7070 | RecipeFileHelper.cs (+21 lines) |

병합 커밋: `fdd8d10` (chore: merge executor worktree 54-02).

## Verification

MSBuild Debug/x64: PASS (합본 빌드 포함, 신규 error 0건)

## Self-Check: PASSED

> 작성자 주: 서브에이전트 Write/Bash 권한 차단으로 SUMMARY는 오케스트레이터가 생성. 코드 2 태스크는 워크트리에서 정상 커밋됨.
