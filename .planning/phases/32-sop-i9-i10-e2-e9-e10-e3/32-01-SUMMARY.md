---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: "01"
subsystem: Halcon/Algorithms
tags: [halcon, vision-algorithm, contour, intersection, building-block]
dependency_graph:
  requires: []
  provides:
    - "VisionAlgorithmService.TryFindLargestContourRect"
    - "VisionAlgorithmService.TryIntersectLines"
    - "VisionAlgorithmService.TryIntersectContours"
  affects:
    - "Wave 2 측정 클래스 (CompoundAngle/CompoundCenterC·B/CompoundShortAxisDistance/ArcLineIntersect)"
tech_stack:
  added: []
  patterns:
    - "HALCON EdgesSubPix→UnionAdjacentContoursXld→ShapeTransXld→AreaCenterXld→SelectObj→SmallestRectangle2Xld 파이프라인"
    - "IntersectionContoursXld 3-out 래퍼 (out isOverlap 포함)"
key_files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
decisions:
  - "IntersectionContoursXld HALCON 시그니처 = 3-out (iRow, iCol, isOverlapping) — 2-out 으로 오해했다가 CS7036 빌드 오류 발생 → Rule 1 즉시 수정"
  - "TryIntersectLines = 기존 IntersectLines 단순 위임 래퍼 (IntersectionLl 직접 중복 호출 없음)"
  - "TryFindLargestContourRect 내 largestRect 는 finally 에서 Dispose — 단 TryIntersectContours 에 전달할 때는 호출측이 소유 (설계 일관성)"
metrics:
  duration: "~10 min"
  completed: "2026-05-21"
  tasks_completed: 2
  files_modified: 1
---

# Phase 32 Plan 01: VisionAlgorithmService 공통 HALCON 빌딩블록 신설 Summary

## 한 줄 요약

HALCON EdgesSubPix→UnionAdjacentContoursXld→ShapeTransXld 파이프라인으로 LargestRect를 산출하는 `TryFindLargestContourRect` 와 두 직선 교점·컨투어 교점 메서드 2개를 VisionAlgorithmService 에 추가하여 Wave 2 측정 클래스 재작성의 서비스 계약을 확립했다.

## 완료된 작업

### Task 1: TryFindLargestContourRect (commit ba2f0e0)

`VisionAlgorithmService` 에 공통 컨투어 알고리즘 메서드를 추가했다.

**구현 파이프라인:**
1. datumTransform 적용 (AffineTransPoint2d + rotAngle 보정)
2. GenRectangle2 → ReduceDomain → EdgesSubPix("canny", alpha, low, high)
3. UnionAdjacentContoursXld(unionDistance, 1, "attr_keep")
4. ShapeTransXld("rectangle2") → AreaCenterXld
5. TupleMax / TupleFind → SelectObj(maxIdx[0].I + 1) ← HALCON 1-based
6. SmallestRectangle2Xld → centerRow/Col/phi/length1/length2 출력

**안전 종결:**
- area.Length == 0 시 "no contour rectangle detected" error 후 false 반환
- 모든 HObject (rect/imageReduced/edges/unionContours/rectXld/largestRect) finally Dispose

### Task 2: TryIntersectLines + TryIntersectContours (commit d878457)

두 개의 교점 계산 메서드를 추가했다.

**TryIntersectLines:**
- 기존 static `IntersectLines` (L615~642, isOverlapping.I==1 / IsInfinity / IsNaN 가드)로 단순 위임
- ArcLineIntersect 호출용 명시적 이름 부여 (CONTEXT.md 미해결#3)

**TryIntersectContours:**
- GenContourPolygonXld로 단축 방향 선분을 XLD 컨투어로 변환
- IntersectionContoursXld("mutual") 로 사각형 XLD ↔ 선분 교점 산출
- iR.Length < 2 가드: 교점 0/1개 시 안전 종결 (CONTEXT.md 미해결#3)
- rectContour 는 호출측(E3 측정 클래스) 소유 — 본 메서드에서 Dispose 하지 않음

## 커밋 이력

| 커밋 | 설명 |
|------|------|
| ba2f0e0 | feat(32-01): TryFindLargestContourRect 공통 컨투어 알고리즘 추가 |
| d878457 | feat(32-01): TryIntersectLines + TryIntersectContours 래퍼 추가 |

## 검증 결과

- `grep "public bool TryFindLargestContourRect"` → L776 1건 매치
- `grep "UnionAdjacentContoursXld"` → L826 1건 매치
- `grep "no contour rectangle detected"` → L838 1건 매치
- `grep "ShapeTransXld.*rectangle2"` → L829 1건 매치
- `grep "public static bool TryIntersectLines"` → L926 1건 매치
- `grep "public bool TryIntersectContours"` → L943 1건 매치
- `grep "IntersectionContoursXld"` → L964 1건 매치
- msbuild Debug/x64 PASS — 신규 error 0, 기존 warning 2건(MSB3884/CS0162) 유지
- 기존 메서드(TryFitLine/TryFindCircle/IntersectLines/TryFitArc) 무수정 확인

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] IntersectionContoursXld 시그니처 불일치 수정**
- **Found during:** Task 2 빌드 검증
- **Issue:** Plan 에 2-out 시그니처(`out iR, out iC`)로 기술되었으나 HALCON 실제 시그니처는 3-out(`out iR, out iC, out isOverlapping`) — CS7036 빌드 오류 발생
- **Fix:** `out isOverlap` 추가하여 3-out 시그니처로 수정
- **Files modified:** WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
- **Commit:** d878457

## Known Stubs

없음 — 신규 메서드는 완전 구현. 측정 클래스 연동은 Wave 2 (Plan 02~05) 에서 수행.

## Threat Flags

없음 — 로컬 이미지 처리 서비스 메서드 신설만. 신규 네트워크 경로/신뢰 경계 없음.
T-32-01/T-32-02 (threat_model 등록 완화 조치) 구현 완료:
- T-32-01: 모든 HOperatorSet 호출 try/catch 감싸기 → HALCON 예외가 검사 스레드 크래시 방지
- T-32-02: finally 블록에서 모든 HObject Dispose → 측정 반복 시 네이티브 메모리 누수 방지

## Self-Check: PASSED

- VisionAlgorithmService.cs 존재: FOUND
- commit ba2f0e0 존재: FOUND (`git log --oneline` 확인)
- commit d878457 존재: FOUND (`git log --oneline` 확인)
- 메서드 3개 (TryFindLargestContourRect / TryIntersectLines / TryIntersectContours) 모두 파일 내 존재
- 빌드 PASS (신규 error 0)
