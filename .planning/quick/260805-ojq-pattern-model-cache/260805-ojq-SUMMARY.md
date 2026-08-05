---
phase: quick-260805-ojq
plan: 01
subsystem: vision-algorithms
tags: [halcon, pattern-matching, ncc, shape-model, memory-leak, caching, thread-safety]

# Dependency graph
requires: []
provides:
  - "PatternMatchService static 모델 캐시(GetOrLoadModel/InvalidateCache) — modelPath 키, lock 보호"
  - "TryFindPose lazy-load 전환 — 매 호출 read+clear 제거, 캐시 hit 시 재사용"
  - "TryCreateModel 재티칭 성공 시 캐시 무효화 훅"
affects: [align-vision, batch-inspection, pattern-align-datum]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "static Dictionary<string, T> + lock(object) 기반 프로세스 전역 캐시 — 인스턴스가 매 호출 new 되는 서비스에서 상태 공유"

key-files:
  created: []
  modified:
    - "WPF_Example/Halcon/Algorithms/PatternMatchService.cs"

key-decisions:
  - "TryFindRefPose(티칭 1회뿐, 저빈도)는 캐싱 대상에서 제외 — 기존 read+clear 그대로 유지(CONTEXT.md LOCKED)"
  - "Find-vs-Find 동시 호출 및 무효화-vs-Find 경합은 accept 처리 — HALCON 모델 조회는 read-only로 안전, 경합 시에도 catch(Exception)로 흡수되어 크래시로 번지지 않음(스코프 확대 없음)"
  - "앱 종료 시 캐시 전체 정리 훅은 의도적으로 추가하지 않음(Claude's Discretion, 런타임 무한증가 해결에 불필요, OS가 프로세스 종료 시 회수)"

patterns-established:
  - "modelPath 키 static 캐시 패턴 — 향후 다른 HALCON 모델(예: OCR, Classifier) 재사용 필요 시 동일 패턴 적용 가능"

requirements-completed: [QUICK-260805-ojq]

# Metrics
duration: 15min
completed: 2026-08-05
---

# Quick 260805-ojq: PatternMatchService 모델 캐시 도입 Summary

**`PatternMatchService.TryFindPose`가 매 호출마다 디스크에서 NCC/Shape 모델 전체를 read+clear 하던 구조를 `modelPath` 키 static 캐시(lazy-load + lock 보호)로 교체하여, 프로세스 메모리가 53GB+까지 폭증하던 확정 근본원인을 제거함(Task 1 완료, Task 2 사람 실기 UAT 대기 중)**

## Performance

- **Duration:** 약 15분
- **Started:** 2026-08-05 (세션 시작)
- **Completed:** 2026-08-05T09:16:23Z (Task 1 기준, 코드+검증+커밋)
- **Tasks:** 1/2 완료 (Task 2는 `checkpoint:human-verify` — 실기 UAT 대기)
- **Files modified:** 1

## Accomplishments
- `PatternMatchService.cs`에 static 모델 캐시 인프라 신규 추가: `_cacheLock`(락) + `_modelCache`(Dictionary) + `CachedModelEntry`(내부 클래스) + `GetOrLoadModel`(조회/lazy-load) + `InvalidateCache`(재티칭 무효화)
- `TryFindPose`의 NCC/Shape 두 분기 모두 `ReadNccModel`/`ReadShapeModel` 직접 호출을 `GetOrLoadModel` 캐시 조회로 교체
- `TryFindPose`의 `finally` 블록에서 매 호출마다 실행되던 `ClearNccModel`/`ClearShapeModel`을 제거 — 소유권이 캐시로 이전
- `TryCreateModel`의 재티칭 성공(`return true;`) 직전에 `InvalidateCache(modelPath)` 훅을 추가하여 stale 모델 재사용 회귀를 방지
- `TryFindRefPose` 본문은 글자 하나도 수정하지 않음(diff 0) — `AlignShapeMatchService.cs`의 티칭 경로 회귀 없음 보장
- 3개 시그니처(`TryCreateModel`/`TryFindRefPose`/`TryFindPose`) 전부 무변경 — 기존 3개 호출부 파일(`InspectionSequence.cs`, `MainView.xaml.cs`, `AlignShapeMatchService.cs`) 전부 무수정 상태로 캐싱 이득을 자동으로 받음

## Task Commits

Each task was committed atomically:

1. **Task 1: PatternMatchService에 static 모델 캐시 도입 — TryFindPose lazy-load 전환 + TryCreateModel 재티칭 무효화** - `7004151` (fix)

Task 2는 `checkpoint:human-verify`(코드 변경 없음, 실기 UAT 전용) — 아래 "Next Phase Readiness" 참고.

## Files Created/Modified
- `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` - static 모델 캐시 도입(`GetOrLoadModel`/`InvalidateCache`), `TryFindPose` lazy-load 전환, `TryCreateModel` 재티칭 무효화 훅 추가. `TryFindRefPose`는 무변경.

## Decisions Made
- 플랜에 명시된 그대로 실행 — 별도 아키텍처 판단 없음(Rule 4 해당 없음). CONTEXT.md LOCKED 항목(`TryFindRefPose` 무변경, 시그니처 불변, 앱 종료 캐시 정리 미추가) 전부 그대로 준수.

## Deviations from Plan

None - plan executed exactly as written. 6개 find/replace 편집 전부 플랜의 BEFORE 텍스트와 라이브 파일이 완전 일치했고, 추가 수정/버그 발견 없음.

## Issues Encountered
None. 빌드는 정상 경로(`WPF_Example/DatumMeasurement.csproj`, Debug/x64)로 첫 시도에 성공했고, 파일 잠금(MSB3021/3027/3030) 이슈는 발생하지 않아 스크래치 OutDir 폴백은 필요하지 않았다.

## Known Stubs
None.

## Threat Flags
None — 이번 변경은 프로세스 내부 캐싱 전략 변경으로, 신뢰 경계 밖 노출면이나 권한 구조에 영향 없음(threat_model T-ojq-06 사전 평가와 일치).

## 참고 사항 (플랜 output 명시 요구사항)

> **참고:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`(v1.3 Align 비전, Tray/Bottom 이더넷 카메라)도 `PatternMatchService.TryFindPose`/`TryCreateModel`을 그대로 호출하는 별도 서브시스템이며, 이번 캐싱 수정으로 무수정 상태로 동일한 이득(read+clear 반복 제거)을 자동으로 받는다. CONTEXT.md에는 명시되지 않았던 호출부이나 grep으로 확인 완료, 시그니처 불변이라 영향 없음.
>
> **앱 종료 시 캐시 정리 훅은 의도적으로 추가하지 않음** (CONTEXT.md "Claude's Discretion" 항목) — 이번 버그는 런타임 중 무한 증가였고, 프로세스 종료 시 OS가 회수하므로 필수가 아니라 스코프를 최소로 유지함. 필요해지면 별도 quick task로 추가 가능.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Task 2(`checkpoint:human-verify`)가 남아 있음 — 이 quick-task는 아직 완전히 끝나지 않았다.** 코드 변경(Task 1)은 완료·커밋되었고 모든 정적 검증(빌드, 구조 grep, TryFindRefPose 무변경, 호출부 인벤토리)을 통과했지만, 사용자가 실기(라이브 애플리케이션/SIMUL)에서 아래 3가지를 직접 확인해야 최종 완료된다:

1. **(a) Test Find 반복 클릭**: Bottom 카메라 NCC 엔진 + `IsPatternAlignEnabled` 켜진 Datum에서 [Test Find] 버튼을 30회 이상 연속 클릭 → 초반 1~2회 상승 후 평평(flat) 유지 확인 (계속 우상향하면 실패)
2. **(b) 일괄검사 연속 실행**: 패턴정렬 Datum 포함 레시피로 일괄검사 20회 이상 연속 실행 → 메모리가 GB 단위로 단조 증가하지 않음 확인
3. **(c) 재티칭 회귀 확인**: (a)에서 쓴 Datum을 재티칭(ROI 이동 또는 [패턴 모델 생성] 재클릭) 후 즉시 [Test Find] → 검출 결과가 재티칭한 새 위치 기준으로 정상 동작하는지 확인(예전 위치에 고정되면 stale 캐시 회귀 실패)

사전 준비: 앱을 Debug/x64로 완전히 재빌드(Rebuild 권장) 후 실행, 작업 관리자에서 `DatumMeasurement.exe`의 메모리(비공개 작업 집합)를 상시 관찰.

이 3가지 중 하나라도 이상이 있으면 관찰된 수치(메모리 값, 클릭/회차 수)와 어느 단계(a/b/c)에서 실패했는지 알려주시면 후속 조치하겠습니다. 모두 정상이면 "승인"으로 완료 처리됩니다.

---
*Phase: quick-260805-ojq*
*Completed (Task 1 only): 2026-08-05*

## Self-Check: PASSED
- FOUND: WPF_Example/Halcon/Algorithms/PatternMatchService.cs
- FOUND: .planning/quick/260805-ojq-pattern-model-cache/260805-ojq-SUMMARY.md
- FOUND: 7004151 (commit hash in git log)
