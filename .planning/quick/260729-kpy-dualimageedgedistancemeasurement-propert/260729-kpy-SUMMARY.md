---
phase: quick-260729-kpy
plan: 01
subsystem: ui
tags: [propertygrid, dualimage-measurement, cross-z, ini-serialization, browsable]

# Dependency graph
requires:
  - phase: quick-260729-jdi
    provides: "LoadCrossZRoleImage 의 SIMUL_MODE 게이트 제거 — 크로스-Z 듀얼이미지 측정이 실HW/SIMUL 양쪽에서 동일 경로로 동작하는 전제"
provides:
  - "DualImageEdgeDistanceMeasurement.TeachingImagePath_Vertical / TeachingImagePath_Horizontal 두 프로퍼티에 [PropertyTools.DataAnnotations.Browsable(false)] 적용 — 측정 속성창(PropertyGrid)의 Image>DualImage 카테고리에서 두 입력 필드 숨김"
affects: [dualimage-edge-distance-measurement, property-grid-ui]

# Tech tracking
tech-stack:
  added: []
  patterns: ["PropertyGrid 전용 숨김은 PropertyTools.DataAnnotations.Browsable(false) 단독 사용 — System.ComponentModel.Browsable(false)/Newtonsoft.Json.JsonIgnore 를 같이 붙이면 ParamBase 직렬화 경로가 끊겨 값이 소실된다(패턴 B, 절대 금지). 검사Grab/런타임 측정이 채우고 읽는 프로퍼티는 반드시 패턴 A(PropertyTools 전용)만 쓴다."]

key-files:
  created: []
  modified: [WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs]

key-decisions:
  - "화면 표시만 숨기고 INI 직렬화/런타임 값 전달 경로는 100% 보존 — 검사Grab(가로/세로 토글)이 채우고 크로스-Z 측정이 읽는 값이라 지우면 안 됨"
  - "ZIndexA/ZIndexB(Point z_index / Line z_index)는 사용자 명시 승인 하에 스코프에서 제외 — 계속 속성창에 노출"

requirements-completed: [KPY-01]

# Metrics
duration: ~15min
completed: 2026-07-29
---

# Phase quick-260729-kpy: DualImageEdgeDistanceMeasurement 티칭 이미지 경로 PropertyGrid 숨김 Summary

**`TeachingImagePath_Vertical`/`TeachingImagePath_Horizontal` 두 프로퍼티에 `[PropertyTools.DataAnnotations.Browsable(false)]`만 추가해 측정 속성창에서 티칭 이미지 경로 입력칸 2개를 숨기고, INI 직렬화/런타임 값 전달은 그대로 유지(실기 검증 완료)**

## Performance

- **Duration:** ~15 min
- **Tasks:** 3 (Task 1 auto, Task 2 auto, Task 3 checkpoint:human-verify)
- **Files modified:** 1

## Accomplishments
- `TeachingImagePath_Vertical`("세로축 티칭 이미지") / `TeachingImagePath_Horizontal`("가로축 티칭 이미지") 두 프로퍼티에 `[PropertyTools.DataAnnotations.Browsable(false)]` + 의도 주석(향후 3중 attribute 통일 시도로 직렬화가 끊기는 사고 방지) 추가
- 금지 attribute(`System.ComponentModel.Browsable(false)` / `Newtonsoft.Json.JsonIgnore`) 미추가를 카운트 고정 게이트(8/8 불변)로 정적 확인
- `ParamBase.Save`/`Load` 코드 리딩으로 Browsable attribute 를 어디서도 검사하지 않음을 재확인(직렬화 안전성 근거)
- **실기 검증(사용자 승인 완료):** 프로그램 재시작 후 E5_P1/E5_P2 속성창에서 두 티칭 이미지 필드 소멸 확인, ZIndexA/ZIndexB 필드는 그대로 노출 확인, 수동 z=23→24 트리거로 크로스-Z 측정값이 변경 전과 동일(30.5mm 근처 OK) 재현 — 값/동작 보존 증명

## Task Commits

Each task was committed atomically:

1. **Task 1: 두 티칭 이미지 경로 프로퍼티에 PropertyGrid 전용 숨김 attribute 추가** + **Task 2: 값 보존 가드(정적 검증, 파일 변경 없음)** - `d7896d1` (fix)
2. **Task 3: 실기 확인 (checkpoint:human-verify)** - 코드 변경 없음, 사용자 실기 검증만 수행

**Plan metadata:** `9b36952` (docs: plan)

_Note: Task 1과 Task 2는 Task 2가 읽기 전용 정적 검증(파일 수정 없음)이라 동일 커밋(`d7896d1`)에 귀속됨._

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs` - `TeachingImagePath_Vertical`/`TeachingImagePath_Horizontal` 에 `[PropertyTools.DataAnnotations.Browsable(false)]` 각 1줄 + 의도 주석 2줄 추가(순수 추가, 삭제 0)

## Gate Verification Results

| Metric | 변경 전 | 목표 | 실측 | 결과 |
|---|---|---|---|---|
| `PropertyTools.DataAnnotations.Browsable(false)` 카운트 | 11 | 13 | 13 | PASS |
| `System.ComponentModel.Browsable(false)` 카운트 | 8 | 8 (불변) | 8 | PASS |
| `Newtonsoft.Json.JsonIgnore` 카운트 | 8 | 8 (불변) | 8 | PASS |
| `git diff` 삭제 라인 | - | 0 | 0 | PASS |
| 수정 파일 개수 | - | 1 | 1 | PASS |
| MSBuild Debug\|x64 `error CS`/`error MSB` | - | 0 | 0 | PASS |

## Decisions Made
- 패턴 A(`PropertyTools.DataAnnotations.Browsable(false)` 단독)만 사용, 패턴 B(3중 attribute)는 명시적으로 배제 — 같은 파일에 이미 확립된 두 패턴 중 값 보존이 필요한 프로퍼티에 맞는 쪽을 선택
- ZIndexA/ZIndexB, RUN 버튼 지원 등은 스코프 밖으로 명시 제외(사용자 사전 승인)

## Deviations from Plan

None - plan executed exactly as written. 파일 1개, 추가 2줄(attribute) + 주석 2줄, 삭제 0줄로 계획된 범위 그대로 적용.

## Issues Encountered
None.

## Human Verification (Task 3 checkpoint)

**결과: 승인 (approved)**

사용자가 다음을 실기로 확인 후 "승인"으로 응답:
1. 프로그램 완전 재시작 후 새 빌드로 재실행
2. E5_P1/E5_P2 속성창 `Image > DualImage` 카테고리에서 "가로축 티칭 이미지"/"세로축 티칭 이미지" 두 필드가 더 이상 보이지 않음을 확인
3. 같은 카테고리의 "Point z_index (ZIndexA)"/"Line z_index (ZIndexB)" 두 필드는 그대로 보임을 확인 (건드리지 않은 항목 회귀 없음)
4. 수동 z=23→24 트리거로 크로스-Z 듀얼이미지 측정(SHOT_E5/FAI_E5)이 변경 전과 동일하게 30.5mm 근처 OK 판정을 냄을 확인 — 값/직렬화가 UI 숨김 변경에도 살아있음을 실증

이로써 T-KPY-01(INI 직렬화 소실 위험)이 실기로 반증됨: 정적 게이트(SCM=8/JsonIgnore=8 불변) + 실측 재시작 후 정상 측정치가 이중으로 확인됨.

## Known Stubs
None - 이번 변경은 순수 UI 표시 attribute 추가이며 신규 데이터 소스/컴포넌트가 없음.

## Threat Flags
None - 이번 변경은 플랜의 threat_model 에 이미 등록된 표면(PropertyGrid 표시 여부)만 다루며, 신규 네트워크/인증/파일접근/스키마 경로를 추가하지 않음.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `DualImageEdgeDistanceMeasurement` 의 티칭 이미지 경로 2필드는 현장 작업자 화면에서 더 이상 혼란을 주지 않으며, 내부 값/직렬화/런타임 동작은 변경 전과 100% 동일함이 실기로 증명됨
- 같은 파일에 남아있는 패턴 A/B 두 가지 attribute 관례는 이후 유사 작업(다른 측정 타입의 dead/redundant 필드 정리)에도 그대로 재사용 가능

---
*Phase: quick-260729-kpy*
*Completed: 2026-07-29*

## Self-Check: PASSED
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs
- FOUND commit: d7896d1
- FOUND commit: 9b36952
