---
phase: 260805-iw0-parambase-copyto-shot-fai
plan: 01
subsystem: ui
tags: [reflection, copy-paste, inspection-recipe, propertygrid, wpf-treeview]

# Dependency graph
requires: []
provides:
  - "ParamBase.CopyPublicPropertiesTo — reflection 기반 일괄 필드 복사 헬퍼 (Save/Load 와 동일한 type switch 재사용)"
  - "MeasurementBase.CopyTo / FAIConfig.CopyTo / DatumConfig.CopyTo override 3종"
  - "ShotConfig.CopyTo 의 FAIList 깊은 복사"
  - "InspectionListView 붙여넣기 후 트리 자식 노드 즉시 갱신(RefreshChildNodesAfterPaste) + Add-Shot(+) FAI 복제 회귀 가드"
affects: [inspection-recipe-editing, property-grid-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ParamBase.CopyPublicPropertiesTo(target, excludeNames): 런타임 타입 기준 reflection 복사, Owner/Parent/읽기전용/인덱서 하드 가드"
    - "CopyTo override 표준형: as-cast → null guard → base.CopyTo → CopyPublicPropertiesTo(target, _copyExclude) → return bool"
    - "클래스별 private static readonly HashSet<string> _copyExclude 로 transient/결과 필드 이름 제외"

key-files:
  created: []
  modified:
    - WPF_Example/Sequence/Param/ParamBase.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

key-decisions:
  - "MeasurementBase.CopyTo 하나로 하위 15개 측정 타입 고유 필드까지 복사 (reflection GetType() 이 런타임 타입 기준이라 override 15개 불필요)"
  - "FAIConfig/ShotConfig 는 자식 컬렉션(Measurements/FAIList)을 참조 공유가 아닌 Clear+AddXxx+CopyTo 로 깊은 복사"
  - "DatumConfig._copyExclude 31개(이름1+티칭오버레이14+TryFindDatum write-back9+PhiDeg wrapper7) — 특히 PropertyGrid Datum|Result 탭에 노출되는 DetectedEdgeCount/DetectedFitRMSE/DetectedAngleDeg 를 누락 없이 제외"
  - "Add-Shot(+) 경로는 siblingShot.CopyTo(shot) 뒤에 shot.ClearFAIs() 를 추가해 FAIList 복사 확장의 부작용(sibling FAI 전체 복제)을 차단 — 종전 FAI_0 1개짜리 Shot 생성 동작 유지"

patterns-established:
  - "Pattern 1: ParamBase 파생 클래스의 CopyTo override 는 CopyPublicPropertiesTo + 클래스 전용 _copyExclude 세트로 구현한다 (수동 필드별 대입문 대신)"
  - "Pattern 2: 리스트형 자식 컬렉션(FAIList/Measurements)은 CopyTo 에서 Clear 후 Add+CopyTo 로 새 인스턴스를 만들어 참조 공유를 피한다"

requirements-completed: [IW0-01, IW0-02, IW0-03]

# Metrics
duration: 20min
completed: 2026-08-05
---

# Phase 260805-iw0: ParamBase CopyTo → Shot/FAI/Measurement/Datum 복사·붙여넣기 수정 Summary

**ParamBase 에 Save/Load 와 동일한 reflection 헬퍼(CopyPublicPropertiesTo)를 신설하고, MeasurementBase/FAIConfig/DatumConfig 에 CopyTo override 를 추가해 복사/붙여넣기가 실제로 필드를 옮기도록 수정. ShotConfig.CopyTo 는 FAIList 까지 깊은 복사하도록 확장했고, UI 는 Add-Shot 회귀 가드 + 붙여넣기 후 트리 자식 노드 즉시 갱신을 추가했다.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-05T05:30:00Z (approx.)
- **Completed:** 2026-08-05T05:48:00Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `ParamBase.CopyPublicPropertiesTo`: Save/Load 와 동일한 reflection + type switch 기반 복사 헬퍼. `Owner`/`Parent`/읽기전용/인덱서를 하드 가드로 제외.
- `MeasurementBase.CopyTo`: 타입 불일치 시 `false` 반환(`Fail to Copy` UI 메시지로 이어짐). 런타임 결과 4종 + 이름 1종 + `IDatumOriginConsumer` 주입 필드 6종, 총 11개 제외.
- `FAIConfig.CopyTo`: FAI 필드(ROI/에지/폴리곤 등) 복사 + `Measurements` 를 `AddMeasurement`+`CopyTo` 로 깊은 복사. 결과 필드 6종 제외.
- `DatumConfig.CopyTo`: ROI/에지/패턴정렬/조명/기준원점(RefOrigin*, RefAngleRad)/IsConfigured 전체 복사. 검출결과 transient 31종(이름1 + 티칭 오버레이14 + `TryFindDatum` write-back9 + `*_PhiDeg` wrapper7) 제외 — 특히 PropertyGrid `Datum|Result` 탭에 노출되는 `DetectedEdgeCount`/`DetectedFitRMSE`/`DetectedAngleDeg` 를 빠짐없이 제외해 "남의 검출결과가 조용히 복사되는" 반대 방향 결함을 차단.
- `ShotConfig.CopyTo`: `FAIList` 를 `ClearFAIs()` → `AddFAI(srcFai.FAIName)` → `srcFai.CopyTo(dstFai)` 순으로 깊은 복사 추가. 기존 조명/노광/해상도 복사 로직은 그대로 보존.
- `InspectionListView.xaml.cs`: Add-Shot(+) 경로에 `shot.ClearFAIs()` 가드 추가(FAIList 복사 확장의 부작용 차단), `button_paste_Click` 에서 붙여넣기 성공 직후 `RefreshChildNodesAfterPaste` 호출로 트리 자식(FAI/Measurement) 노드를 즉시 재구축.

## Task Commits

Each task was committed atomically:

1. **Task 1: ParamBase 일괄 복사 헬퍼 + MeasurementBase/FAIConfig CopyTo** - `b7c6c19` (feat)
2. **Task 2: DatumConfig.CopyTo 신설 + ShotConfig.CopyTo 에 FAIList 깊은 복사 추가** - `e233857` (feat)
3. **Task 3: Add-Shot 회귀 가드 + 붙여넣기 후 트리 자식 노드 갱신** - `71bb003` (feat)

**Plan metadata:** (this commit — created by orchestrator after this summary)

## Files Created/Modified
- `WPF_Example/Sequence/Param/ParamBase.cs` - `CopyPublicPropertiesTo` reflection 복사 헬퍼 신설 (기존 `CopyTo` 기본 구현 `return true` 는 그대로 보존)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` - `_copyExclude`(11개) + `CopyTo` override 신설, 타입 불일치 시 false
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` - `_copyExclude`(6개) + `CopyTo` override 신설, `Measurements` 깊은 복사
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - `_copyExclude`(31개) + `CopyTo` override 신설
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` - 기존 `CopyTo` 에 `FAIList` 깊은 복사 블록 추가, 주석 문구 갱신
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - Add-Shot(+) `shot.ClearFAIs()` 가드, `button_paste_Click` 트리 갱신 호출 + `RefreshChildNodesAfterPaste` 헬퍼 신설, 관련 주석 문구 갱신

## Decisions Made
- 하위 측정 타입 15개 파일(Measurements/)은 수정하지 않음 — `MeasurementBase.CopyTo` 의 reflection 이 `GetType()` (런타임 타입) 기준으로 동작하므로 파생 클래스 고유 필드까지 자동으로 복사됨. Plan 의 design_note 에서 사전 검증된 결정.
- `ShotConfig.CopyTo` 의 `ZPosition`/`DelayMs`/`ZIndex`/`SimulImagePath` 미복사는 이번 범위에서 의도적으로 손대지 않음(plan 의 명시적 제외 사항 — 별건으로 처리 예정).
- 트리 갱신은 `RebuildTree()` 전체 재구축 대신 `RefreshChildNodesAfterPaste` 로 대상 노드의 자식만 국소 재구축 — `RebuildTree` 는 `seq.Actions[]` 기준이라 아직 Actions[] 에 반영 안 된 신규 Shot 이 사라질 위험이 있어 회피.

## Deviations from Plan

None - plan executed exactly as written. 모든 `_copyExclude` 목록(MeasurementBase 11개, FAIConfig 6개, DatumConfig 31개)을 plan 에 명시된 그대로 옮겨 적었고, grep 기반 자동 검증으로 원소 수를 확인했다.

## Issues Encountered

빌드 잠금 관련 이슈 없음 — `DatumMeasurement.exe` 를 점유 중인 프로세스가 없어 매 태스크마다 정상적으로 `MSBuild.exe //t:Build` 를 실행해 `.exe` 를 재생성할 수 있었다 (taskkill 등 강제 종료 불필요).

다른 세션(quick 260805-f3w)이 동시에 같은 저장소에 커밋을 남겼다(`de48a56`, `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`) — 이번 plan 의 범위(6개 파일) 밖이며, 각 태스크 커밋은 `git add <file>` 로 대상 파일만 개별 스테이징해 겹침 없이 격리했다. 커밋 3개(`b7c6c19`/`e233857`/`71bb003`)를 `git show --stat` 으로 재확인해 전부 이 plan 파일에만 한정됨을 검증.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 정적 검증(빌드 0 error, 제외목록 원소 수, 파일 목록 6개, Measurements/ 무수정) 전부 통과.
- 동작 확인(SIMUL 실행 UAT 8개 항목)은 plan 의 `<verification>` 표에 정리되어 있으며 사용자 실기 검증 대기 — 특히 5-1번(이미 Test Find 한 Datum → 미검출 Datum 에 Paste 시 `Datum|Result` 값이 대상 자신의 값으로 남는지)이 회귀 방지의 핵심 확인 포인트.
- UI 신규 추가(컨텍스트 메뉴/단축키)는 범위 외로 남겨둠 — 필요 시 별도 quick task.

---
*Phase: 260805-iw0-parambase-copyto-shot-fai*
*Completed: 2026-08-05*

## Self-Check: PASSED

All 6 modified source files + this SUMMARY.md confirmed present on disk. All 3 task commits (`b7c6c19`, `e233857`, `71bb003`) confirmed present in git log.
