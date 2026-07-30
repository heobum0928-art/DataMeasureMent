---
phase: quick-260729-hwb
plan: 01
subsystem: inspection-sequence
tags: [cross-z, prep, light, zindex, fai-measurement, bottom, protocol-v1]

# Dependency graph
requires:
  - phase: quick-260729-e9q
    provides: "SkipReason.CROSS_Z_INCOMPLETE, MarkMeasurementCrossZIncomplete, JudgeText/Excel CROSS-Z INCOMPLETE 라벨 분기(비프로토콜 실행 전용) — 이번 작업이 확장한 기반"
provides:
  - "InspectionSequence.FindShotByZIndex 크로스-Z 2패스 폴백 — $PREP 조명 경로가 $TEST 라우팅(FindActionIndicesByZIndex)과 동일 크로스-Z 인지 규칙 공유"
  - "Action_FAIMeasurement.MarkMeasurementCrossZIncomplete(meas, bRelevantTick, bProtocolCycle, parentSeq2) — 프로토콜 사이클 전용 CROSS_Z_INCOMPLETE 로그 분기"
  - "크로스-Z tick 표시/저장 이미지가 실제 측정에 쓰인 role 이미지로 교체됨([FAI CrossZ IMG] 추적 로그)"
affects: [bottom-sequence, fai-measurement, cross-z-dual-image, prep-light-resolver, manual-z-trigger]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "$PREP 조명 z 리졸버는 own-ZIndex 정확일치 1패스 → 크로스-Z 소유 shot 2패스(DoesShotOwnCrossZIndex 재사용) 폴백 구조를 따른다"
    - "크로스-Z tick 의 표시/저장 소스는 정적 대표 사진(sharedSrc) 대신 그 tick 에 실제 캡처된 role 이미지(crossZRoleImage)를 우선한다 — per-FAI 지역변수로만 관리, 새 필드 없음"
    - "미완성 크로스-Z tick 은 완성 index 게이트(AddFaiResult)가 PLC 보고 대상에서 이미 제외하므로, 화면/로그 표시만 안전하게 미완료로 승격 가능하다"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "조명 의미론: 크로스-Z 로 매칭된 경우 그 측정을 소유한 shot 자신의 조명 설정을 적용한다(ApplyShotLightsInternal 무수정). role(A/B)별 다른 조명은 현재 코드에 없으며 이번 범위 밖 — 한계로 기록."
  - "다중 매칭 결정론: 같은 z 를 두 개 이상의 shot 이 크로스-Z 로 소유할 수 있는 레시피 구성에서는 recipeManager.Shots 열거 순서의 첫 번째가 이긴다. FindActionIndicesByZIndex/AggregateIndexFais 가 이미 같은 순서를 쓰므로 실행·집계·조명이 순서를 공유하며, 새 정렬 기준을 발명하지 않는다."
  - "미완성 tick 을 NG 아닌 CROSS_Z_INCOMPLETE 로 다루는 설계 근거: AddFaiResult 의 GetMeasurementCompletionZIndex(meas, shot) == nZIndex 게이트가 미완성 index 에서 이 측정을 애초에 응답 패킷 구성 대상에서 제외한다(V1 프로토콜 응답은 fai.IsPass 를 읽지 않음). 따라서 faiAllPass=false 로 두는 것의 영향 범위는 화면 표시 / 캡처 파일명 OK·NG / cycle.json 뿐이며, 방향은 '측정 안 한 것을 PASS 라 하지 않는다'는 안전측이다."
  - "MainView.UpdateImageSourceLabel 은 수정하지 않음 — fresh Read 로 재확인한 결과 유일한 라이브 호출 경로(DisplayParam ← SetParam)는 InspectionListView 의 TreeListBox SelectionChanged 핸들러에서만 호출되며(L688/692), 이는 사용자가 트리 노드를 클릭했을 때의 티칭/리뷰 브라우징 라벨 갱신이다. 시퀀스 완료 시 자동 호출되는 라이브 결과 콜백(DisplaySequenceContext 등)에서는 호출되지 않아 검사 결과 표시 경로가 아님을 확인했다."
  - "(B) 는 SIMUL_MODE 에서 role 별 교시 경로(TeachingImagePath_Horizontal/Vertical)가 설정돼 있을 때만 실제로 이미지 내용이 달라진다(LoadCrossZRoleImage GAP-4 조건부 분기). 비-SIMUL/교시경로 미설정 시에는 LoadCrossZRoleImage 가 항상 ShotParam.GetImage() 를 반환하므로, 이번 (B) 수정은 '어느 소스를 화면에 반영하느냐'만 바꾸고 그 소스의 실제 픽셀 내용은 변경 전과 동일한 경우가 대부분이다 — 회귀 위험 낮음."

requirements-completed: [HWB-A, HWB-B, HWB-C]

# Metrics
duration: ~35min
completed: 2026-07-29
---

# Phase quick-260729-hwb: 크로스-Z $PREP 차단 + 오표시 사진 + 가짜 PASS 3중 결함 제거 Summary

**$PREP 조명 리졸버(FindShotByZIndex)에 크로스-Z 2패스 폴백을 추가해 z=24 트리거 차단을 제거하고, 크로스-Z tick 의 화면/저장 이미지를 실제 측정 role 이미지로 교체하며, 프로토콜 사이클(수동 Z트리거 포함)의 미완성 tick 을 CROSS-Z INCOMPLETE 로 명시 표시**

## Performance

- **Duration:** ~35 min (코드 작성 + 빌드 검증, human-verify 체크포인트 제외)
- **Tasks:** 2/3 완료 (Task 3 은 blocking human-verify 체크포인트 — 실기 검증 대기)
- **Files modified:** 2

## Accomplishments

### Task 1 — (A) $PREP 조명 리졸버 크로스-Z 인지 통일
- `InspectionSequence.FindShotByZIndex(int nZIndex)` 에 2-패스 폴백 추가:
  - 1패스: own-ZIndex 정확일치(기존 루프 완전 무변경) — 매칭 시 반환값 수정 전과 100% 동일.
  - 2패스: 1패스 실패 시에만, `DoesShotOwnCrossZIndex`(기존 `FindActionIndicesByZIndex` 가 이미 쓰던 헬퍼, 본문 무수정)를 재사용해 크로스-Z 소유 shot 을 재조회.
  - 2패스 매칭 시 `[PREP CrossZ]` Trace 로그로 선택된 shot/z 기록.
- `DoesShotOwnCrossZIndex`, `FindActionIndicesByZIndex` 는 한 글자도 변경하지 않음(계획 요구사항).

### Task 2 — (B) 크로스-Z role 이미지 표시/저장 반영 + (C) 프로토콜 사이클 미완료 표시
- **(C)** `MarkMeasurementCrossZIncomplete` 에 `bProtocolCycle` 파라미터 추가. 상태 마킹부(ClearResult/LastSkipReason/LastJudgement)는 그대로 두고 로그 문구만 분기 — `bProtocolCycle=false` 는 e9q 문구를 한 글자도 바꾸지 않고 그대로 재사용, `bProtocolCycle=true` 는 "프로토콜 사이클 정상 흐름의 중간 상태(고장 아님)" 취지의 별도 문구 출력.
  - `!bCompleted` 분기에 프로토콜 경로 추가: `bNonProtocolCycle==false`(=프로토콜 사이클, 수동 Z트리거 포함)일 때도 `MarkMeasurementCrossZIncomplete(meas, true, true, parentSeq2)` 호출 + `faiAllPass=false`.
  - `!bRelevant` 분기는 프로토콜 경로에서 종전 그대로 무변경(조용히 continue) — 이 tick 은 이 측정과 무관한 안전망.
- **(B)** `ProcessCrossZCaptureTick` 에 `out string szCapturedRoleKey` 추가 — 캡처 성공(`bCaptureOk=true`) 시에만 role 키를 채우고, 그 외 모든 조기 return 경로는 `null`.
  - `EStep.Measure` FAI 루프에 per-FAI 지역변수 `crossZRoleImage`(+로그용 role/측정명/z 보조 변수) 도입. 이번 tick 캡처 성공 시 `TakeCrossZImageCopy(szCapturedRoleKey)` 로 소유 사본을 받아둠(같은 FAI 안에서 첫 캡처가 결정론적으로 승리).
  - `AggregateFaiResult` 호출 지점: `crossZRoleImage==null` 이면 종전과 완전히 동일하게 `sharedSrc` 사용(비-크로스-Z 회귀 0). `!=null` 이면 `crossZRoleImage.CopyImage()` 로 만든 `SharedHImage` 를 대신 넘기고 `finally` 에서 `Release()`(기존 `sharedSrc` 소유권 계약 미러).
  - Shot 단위로 아직 표시 이미지를 덮지 않았으면(`bShotDisplayImageReplaced` 플래그) `pMyContext.ResultHalconImage` 를 교체 — Shot 전체에서 첫 크로스-Z 캡처가 화면을 차지(결정론적 규칙). 교체 시 `[FAI CrossZ IMG]` Trace 로그로 Shot명/측정명/role/z 기록.
  - `crossZRoleImage` 는 각 FAI 처리 종료 시 `finally` 에서 dispose+null 되어 누수 없음.
- **MainView 판단:** `UpdateImageSourceLabel` 은 트리 선택(SetParam ← InspectionListView TreeListBox 선택 핸들러) 전용 브라우징 라벨로 확인, 무수정.

## Task Commits

Each task was committed atomically:

1. **Task 1: $PREP FindShotByZIndex 크로스-Z 2패스 폴백** - `829cda8` (fix)
2. **Task 2: 크로스-Z role 이미지 표시/저장 + 프로토콜 사이클 미완료 표시** - `87d9cbf` (fix)

**Plan metadata:** (orchestrator will commit separately)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `FindShotByZIndex` 2-패스 폴백(+33줄)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `MarkMeasurementCrossZIncomplete` bProtocolCycle 확장, `ProcessCrossZCaptureTick` out szCapturedRoleKey 추가, `EStep.Measure` crossZRoleImage 표시/저장 반영 로직(+86/-7줄)

## Gate Verification Results (실측)

### Task 1 게이트
| Metric | Baseline | Target | 실측 | 결과 |
|---|---|---|---|---|
| `dosown_refs` | 2 | 3 | 3 | PASS |
| `prep_crossz_log` | 0 | 1 | 1 | PASS |
| `dosown_def` | 1 | 1 (불변) | 1 | PASS |
| `findactidx_def` | 1 | 1 (불변) | 1 | PASS |
| `findshot_def` | 1 | 1 (불변) | 1 | PASS |
| `applylights_def` | 1 | 1 (불변) | 1 | PASS |
| `no_csharp8_added` | 0 | 0 (불변) | 0 | PASS |

`git diff --name-only`: `InspectionSequence.cs` 단 하나. MSBuild `error CS`/신규 `warning CS`(CS0618/CS0162 제외): 빈 출력.

### Task 2 게이트
| Metric | Baseline | Target | 실측 | 결과 |
|---|---|---|---|---|
| `markincomplete_calls` | 2 | 3 | 3 | PASS |
| `roleimg_log` | 0 | 1 | 1 | PASS |
| `takecopy_refs` | 4 | 5 | 5 | PASS |
| `skipreason_const` | 1 | 1 (불변) | 1 | PASS |
| `markincomplete_def` | 1 | 1 (불변) | 1 | PASS |
| `e9q_nonprotocol_msg` | 1 | 1 (불변) | 1 | PASS |
| `review_label` | 1 | 1 (불변) | 1 | PASS |
| `excel_label` | 1 | 1 (불변) | 1 | PASS |
| `loadrole_def` | 1 | 1 (불변) | 1 | PASS |
| `proctick_def` | 1 | 1 (불변) | 1 | PASS |
| `aggregate_def` | 1 | 1 (불변) | 1 | PASS |
| `nonprotocol_gate` | 1 | 1 (불변) | 1 | PASS |
| `no_csharp8_added` | 0 | 0 (불변) | 0 | PASS |

`git diff --name-only`: `Action_FAIMeasurement.cs` 단 하나. MSBuild `error CS`/신규 `warning CS`(CS0618/CS0162 제외): 빈 출력.

**MSB302x(exe 파일잠금) 발생 여부:** 발생하지 않음. 두 Task 모두 빌드 로그에 `MSB3021`/`MSB3026`/`MSB3027` 없음. 최종 빌드 후 `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` 타임스탬프가 빌드 직후로 갱신됨(2026-07-29 13:12:41 KST) — 재빌드 불필요, 사용자가 받을 exe 는 이번 두 커밋을 모두 포함한 최신 산출물.

## Decisions Made
(위 frontmatter `key-decisions` 참조 — 조명 의미론, 다중 매칭 결정론, 미완성 tick 안전 근거, MainView 판단, SIMUL_MODE 한정 회귀 범위)

## Deviations from Plan

None - plan executed exactly as written. `<planner_findings>` 의 라인번호/인과사슬을 fresh Read 로 재확인한 뒤 그대로 따랐고, 계획 밖 파일 수정이나 로직 변경 없음.

## Issues Encountered

`roleimg_log` 카운트가 1차 구현에서 4로 나와(want 1) 게이트 실패 — 원인은 `crossZRoleImage` 관련 지역변수 3개의 인라인 trailing 주석에 `[FAI CrossZ IMG]` 문자열을 중복 기재해 라인전체주석 스트립(`grep -v '^[[:space:]]*//'`) 대상에서 빠졌기 때문. 해당 주석 3곳을 "표시 이미지 교체 로그용" 으로 재작성해 리터럴 문자열을 실제 `Logging.PrintLog` 호출 1곳에만 남기고 재빌드/재검증하여 해결(Rule 1 - 자체 발견 버그, 새 코드 로직 변경 없음, 주석 텍스트만 수정).

## Known Stubs
None - 이번 변경은 기존 인프라(CROSS_Z_INCOMPLETE/SharedHImage/TakeCrossZImageCopy)만 재사용하며 신규 stub 데이터 경로 없음.

## Threat Flags
None - `<threat_model>` 에 이미 등록된 4개 표면(T-HWB-01~04, mitigate)만 다루었고, 신규 네트워크/인증/파일접근/스키마 경로를 추가하지 않음. T-HWB-05(accept)는 코드 변경 없이 기존 `AddFaiResult` 완성 index 게이트 근거만 재확인.

## User Setup Required

Task 3(blocking human-verify checkpoint) — 실기(BOTTOM SHOT_E5, ZIndex=23 유지) 재검증 필요. 아래 "다음 단계" 참조.

## Next Phase Readiness

Task 3 체크포인트가 남아 있음. 사용자가 새로 빌드된 `DatumMeasurement.exe` 로 다음을 확인해야 한다:
1. 레시피 무변경(SHOT_E5 ZIndex=23 유지) 상태에서 BOTTOM z=23 → z=24 수동 Z트리거가 모두 성공.
2. z=23 tick 에서 `E5_P1`/`E5_P2` 가 `CROSS-Z INCOMPLETE` 로 표시(가짜 PASS 아님) + `[FAI CrossZ IMG]` 로그.
3. z=24 tick 에서 "트리거 실패" 모달 없음 + `[PREP CrossZ]` 로그 + 실제 측정값/정상 판정.
4. 비-크로스-Z Shot / RUN 버튼 경로 회귀 없음.

실패 항목이 보고되면 `[PREP CrossZ]`/`[FAI CrossZ IMG]`/`CROSS_Z_INCOMPLETE` 로그를 근거로 원인 분석 후 승인 받아 추가 수정.

---
*Phase: quick-260729-hwb*
*Completed: 2026-07-29*

## Self-Check: PASSED
- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND: WPF_Example/bin/x64/Debug/DatumMeasurement.exe (fresh build)
- FOUND commit: 829cda8
- FOUND commit: 87d9cbf
