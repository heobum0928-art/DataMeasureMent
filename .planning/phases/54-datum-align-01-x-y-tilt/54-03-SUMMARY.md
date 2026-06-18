---
phase: 54-datum-align-01-x-y-tilt
plan: "03"
subsystem: PatternMatchService
tags: [datum, pattern-align, halcon, shape-model, ncc, greenfield]
dependency_graph:
  requires:
    - DeviceHandler.EXTENSION_SHAPE_MODEL/.ncm (54-02)
  provides:
    - PatternMatchService.TryCreateModel (create+write shape/ncc)
    - PatternMatchService.TryFindRefPose (ref pose 기록, D-09)
    - PatternMatchService.TryFindPose (reduce_domain + 다운샘플 coarse find, D-06/D-06a)
    - PatternMatchService.TryBuildAlignRigid (vector_angle_to_rigid + lenient identity 폴백)
  affects:
    - Wave 2: DatumPhase 패턴매칭 통합 (54-04)
    - Wave 3: 패턴 모델 생성/ref pose 티칭 (54-05)
tech_stack:
  added: []
  patterns:
    - DatumFindingService/VisionAlgorithmService 구조·규약 미러
    - 전 메서드 try/catch(return false) + HObject/HImage dispose
    - HALCON 24.11 find_shape_model/find_ncc_model 시그니처(출력 4개)
key_files:
  created:
    - WPF_Example/Halcon/Algorithms/PatternMatchService.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "매칭 본문 greenfield (D-01/D-06/§3 설계대로 신규 작성)"
  - "find angle 은 거칠어 측정 미사용 — 정밀 θ는 line-fit(D-01b)"
  - "다운샘플 좌표 → 1/scale 로 원본 복원(D-06a)"
deviations:
  - "find_shape_model/find_ncc_model 호출에 존재하지 않는 5번째 출력(acuity)을 넣어 CS1501 컴파일 오류 발생 → 합본 빌드 게이트에서 검출, 오케스트레이터가 시그니처 정정(commit 30a8f27). HALCON 24.11 공식 출력은 Row,Column,Angle,Score 4개."
metrics:
  duration: "30min"
  completed: "2026-06-18"
  tasks_completed: 2
  tasks_total: 2
  files_modified: 2
---

# Phase 54 Plan 03: PatternMatchService Summary

**One-liner:** HALCON shape/ncc 패턴 매칭 서비스(greenfield) — 모델 생성/저장/로드, reduce_domain + 다운샘플 coarse find, ref/cur pose → rigid transform. 4개 공개 메서드, 전 메서드 try/catch + dispose 규약 준수.

## Commits

| Task | Commit | Files |
|------|--------|-------|
| 1+2: 4개 메서드 + csproj 등록 | c910281 | PatternMatchService.cs (신규 511줄), csproj (+1) |
| fix: 시그니처 정정 | 30a8f27 | PatternMatchService.cs (acuity 제거) |

병합 커밋: `dedcf31` (chore: merge executor worktree 54-03).

## Methods

1. `TryCreateModel` — reduce_domain + create_{shape,ncc}_model + write (engine 분기)
2. `TryFindRefPose` — read model + find on template image (D-09 ref pose 1회 기록)
3. `TryFindPose` — reduce_domain(검색영역 제한) + 다운샘플(zoom_image_factor) + coarse find + 스케일 복원
4. `TryBuildAlignRigid` — vector_angle_to_rigid + lenient identity 폴백

## Verification

MSBuild Debug/x64: PASS (합본 빌드, FindShapeModel 정정 후 EXIT=0)

## Self-Check: PASSED (post-merge build fix 적용)

> 작성자 주: 서브에이전트 Write/Bash 권한 차단으로 SUMMARY는 오케스트레이터가 생성. 워크트리 단독 빌드 PASS 보고였으나 합본 빌드에서 CS1501 검출 → 정정 후 통과. 사용자 제공 HALCON 24.11 find_shape_model 문서로 정정 내용 교차검증 완료.
