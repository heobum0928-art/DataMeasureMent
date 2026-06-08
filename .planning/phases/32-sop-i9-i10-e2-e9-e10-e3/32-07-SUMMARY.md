---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: "07"
subsystem: halcon-measurement
tags: [overlay, EdgeInspectionOverlay, ArcLineIntersect, CompoundAngle, CompoundCenterC, CompoundCenterB, CompoundShortAxis, visualization]

requires:
  - phase: 32-sop-i9-i10-e2-e9-e10-e3
    provides: "Plan 03/04/05 — ArcLineIntersect/Compound 4종 알고리즘 재작성 + E3 신규 타입 (overlay 빈 리스트 상태)"

provides:
  - "ArcLineIntersect TryExecute: FAI-Edge1 + FAI-Edge2(피팅 라인 2개) + FAI-Intersection(교점 점 마커) 3개 overlay"
  - "CompoundAngle TryExecute: FAI-Edge1(LargestRect 중심 점 마커) + FAI-DiagLine(대각선 라인) 2개 overlay"
  - "CompoundCenterCDistance TryExecute: FAI-Edge1(중심 점 마커) + FAI-DistLine(수선 드롭선, footOk 가드) — foot 오버로드 교체"
  - "CompoundCenterBDistance TryExecute: 동일 (MeasureAxis Y 기본값, E10-overlay 주석)"
  - "CompoundShortAxisDistance TryExecute: FAI-ShortAxis(단축 폭 세그먼트) 1개 overlay — phiPerp 계산"

affects:
  - "32-sop-i9-i10-e2-e9-e10-e3"
  - "HalconDisplayService (overlay 렌더 소비자)"
  - "Action_FAIMeasurement (overlay 누적 경로)"

tech-stack:
  added: []
  patterns:
    - "overlay ADDITIVE 원칙: return true 직전 삽입, HALCON 재호출 없음, 이미 계산된 로컬 변수만 참조"
    - "foot 반환 오버로드 교체 패턴: ComputeProjectionDistance 단일→foot 오버로드, footOk 가드로 FAI-DistLine skip"
    - "점 마커 패턴: LineRow1==LineRow2, LineColumn1==LineColumn2 (길이 0 라인)"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundShortAxisDistanceMeasurement.cs"

key-decisions:
  - "overlay 삽입 위치 = return true 직전 — 실패 경로(return false)에서는 빈 리스트 유지, 잔상 없음"
  - "CompoundCenterC/B: ComputeProjectionDistance 단일 오버로드 → foot 반환 오버로드 교체 — 수치 결과 byte-identical (단일 오버로드가 내부에서 foot 오버로드를 위임 호출하므로 동일)"
  - "CompoundAngle FAI-DiagLine: 대각선(LargestRect 중심 ↔ DatumC 검출 원중심)만 시각화 — DatumB 기준선 추가는 EdgeToLineAngle 패턴 참조 불필요, 대각선만으로 각도 시각화 충분"
  - "ArcLineIntersect FAI-Intersection: LineRow1==LineRow2 점 마커 패턴 채택 (길이 0 라인) — HalconDisplayService 기본 분기 처리"
  - "CompoundShortAxis phiPerp = phi + PI/2 순수 C# Math — HALCON 연산자 없음, try/catch 불필요"

patterns-established:
  - "overlay ADDITIVE 삽입: return true 직전 overlays.Add 블록, 계산된 로컬 변수 재사용, HALCON 재호출 금지"
  - "foot 반환 오버로드 교체: footOk=false 시 FAI-DistLine skip, 수치 결과 동일 보장"

requirements-completed: []

duration: 20min
completed: "2026-05-21"
---

# Phase 32 Plan 07: Measurement Overlay Visualization Summary

**Plan 03/04/05 에서 알고리즘만 재작성되고 빈 리스트로 남아있던 5종 측정 타입에 결과 overlay(피팅 라인, 교점, 중심 마커, 수선 드롭선, 단축 세그먼트)를 추가 — 수치 계산 무변경, msbuild Debug/x64 PASS**

## Performance

- **Duration:** 약 20분
- **Started:** 2026-05-21
- **Completed:** 2026-05-21
- **Tasks:** 5 (Tasks 1~4 코드, Task 5 빌드)
- **Files modified:** 5

## Accomplishments

- ArcLineIntersectDistance: `FAI-Edge1`(EdgeA 피팅 라인) + `FAI-Edge2`(EdgeB 피팅 라인) + `FAI-Intersection`(교점 점 마커) 3개 overlay 추가 — I9/I10 측정 시 두 에지 라인과 교점이 캔버스에 표시됨
- CompoundAngle: `FAI-Edge1`(LargestRect 중심 점 마커) + `FAI-DiagLine`(대각선 라인) 2개 overlay 추가 — E2 각도 측정 시 중심점과 검출 원중심 연결선이 시각화됨
- CompoundCenterC/B: `FAI-Edge1`(중심 점 마커) + `FAI-DistLine`(수선 드롭선, footOk 가드) 추가 + foot 반환 오버로드 교체 — E9/E10 거리 측정 시 cyan 드롭선 표시
- CompoundShortAxis: `FAI-ShortAxis`(단축 폭 세그먼트) 1개 overlay 추가 — E3 단축 폭 측정 시 세그먼트 + 양 끝 X마커 표시
- msbuild Debug/x64 Build succeeded — 신규 컴파일 오류 0건 (기존 warning 2건: MSB3884 ruleset + CS0162 unreachable code, 이전 baseline과 동일)

## Task Commits

각 측정 타입이 단일 atomic commit 에 묶임 (Tasks 1~4+5 통합):

1. **Tasks 1~5: 5종 overlay 추가 + 빌드 검증** - `9123626` (feat)

**Plan metadata:** 별도 docs commit (이 SUMMARY 포함)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs` — FAI-Edge1/FAI-Edge2/FAI-Intersection 3개 overlay 추가 (return true 직전)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs` — FAI-Edge1/FAI-DiagLine 2개 overlay 추가
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs` — foot 오버로드 교체 + FAI-Edge1/FAI-DistLine overlay 추가
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs` — 동일 (E10-overlay, MeasureAxis Y)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundShortAxisDistanceMeasurement.cs` — phiPerp 계산 + FAI-ShortAxis overlay 추가

## Decisions Made

- **CompoundCenterC/B foot 오버로드 교체:** `ComputeProjectionDistance` 단일 오버로드를 foot 반환 오버로드로 교체. 수치 결과 byte-identical — 단일 오버로드 내부가 foot 오버로드를 위임 호출하므로 수학적으로 완전히 동일. `footOk=false` 시 `FAI-DistLine` skip (투영 실패는 드문 케이스이므로 수치 0 그대로 유지).
- **점 마커 패턴(ArcLineIntersect FAI-Intersection, CompoundAngle FAI-Edge1, CompoundCenterC/B FAI-Edge1):** `LineRow1==LineRow2, LineColumn1==LineColumn2` (길이 0 라인) 패턴 채택. HalconDisplayService 기본 분기에서 Points 배열의 X 마커만 표시.
- **CompoundAngle 대각선만 시각화:** DatumB 기준선(daR1/daC1/daR2/daC2)은 overlay에 미포함 — 대각선(LargestRect 중심 ↔ DatumC 검출 원중심)만으로 각도 시각화 충분하다고 판단. 기준선 추가는 향후 필요 시 별도 plan.

## Deviations from Plan

없음 — 플랜에 명세된 대로 정확히 실행됨.

## Issues Encountered

없음.

## Known Stubs

없음 — 5종 모두 기존 빈 리스트에서 실측 기하 변수를 활용하는 실제 overlay로 완성됨.

## Threat Flags

없음 — 본 plan 은 순수 로컬 계산값(row/col 좌표)을 overlay 객체에 기록하는 작업. 외부 신뢰 불가 입력 없음, 신규 네트워크 엔드포인트/인증 경로/파일 접근 패턴 없음.

## Next Phase Readiness

- Phase 32 전체 6개 plan 모두 완료 (01~07).
- 다음 단계: 사용자 SIMUL_MODE 통합 UAT — 5종 overlay 시각 확인 (녹/적 에지 라인, 교점 마커, 수선 드롭선, 단축 세그먼트).
- Phase 32 UAT 후 sign-off 시 Phase 32 완료 처리.

## Self-Check

파일 존재 확인:
- `WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs` — FOUND (FAI-Edge1, FAI-Edge2, FAI-Intersection)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs` — FOUND (FAI-Edge1, FAI-DiagLine)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs` — FOUND (FAI-Edge1, FAI-DistLine, footOk)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs` — FOUND (FAI-Edge1, FAI-DistLine, footOk)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundShortAxisDistanceMeasurement.cs` — FOUND (FAI-ShortAxis, phiPerp)

커밋 존재 확인: `9123626` — FOUND

빌드: msbuild Debug/x64 → `DatumMeasurement.exe` 생성 확인 — PASSED

## Self-Check: PASSED

---
*Phase: 32-sop-i9-i10-e2-e9-e10-e3*
*Completed: 2026-05-21*
