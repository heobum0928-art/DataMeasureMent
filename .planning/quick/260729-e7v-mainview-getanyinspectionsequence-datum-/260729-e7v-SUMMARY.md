---
phase: quick-260729-e7v
plan: 01
subsystem: ui
tags: [datum, inspection-sequence, owner-name, mainview, refactor, hardening]

# Dependency graph
requires:
  - phase: quick-260728-n7b
    provides: "TryComposeAlign 패턴2 모델경로를 datum.OwnerName 기준으로 통일 (modelPath 가 인스턴스와 무관하게 산출되는 전제)"
  - phase: quick-260728-l2r
    provides: "ref pose 기록을 범위제한 TryFindPose 로 통일 (같은 버그 계열의 선행 수정)"
provides:
  - "MainView.GetInspectionSequenceForDatum(datum) — datum.OwnerName 기준으로 소유 InspectionSequence 인스턴스를 반환하는 role-aware 헬퍼, 미해결 시 GetAnyInspectionSequence() 폴백"
  - "GetAnyInspectionSequence() 를 '폴백 전용'으로 주석 격하 (코드상 호출부 1곳만 남김)"
affects: [mainview, datum-align, test-find]

# Tech tracking
tech-stack:
  added: []
  patterns: ["datum 범위 연산은 datum.OwnerName 으로 소유 시퀀스 인스턴스를 조회하고, 미해결 시에만 임의 인스턴스로 폴백한다"]

key-files:
  created: []
  modified: [WPF_Example/UI/ContentItem/MainView.xaml.cs]

key-decisions:
  - "GetAnyInspectionSequence() 본문은 한 글자도 건드리지 않고 폴백 경로로만 유지 — 미등록 role(Side 비활성 PC)에서도 회귀 없음"
  - "동작을 '고치는' 시도를 하지 않음 — modelPath 가 이미 datum.OwnerName 기준으로 인스턴스 무관 산출되므로 이번 변경은 순수 구조적 방어선"

patterns-established:
  - "Pattern 1: datum 소유 시퀀스 조회는 GetInspectionSequenceForDatum(datum) 을 거치고, 인스턴스 정체성에 의존하는 신규 로직은 이 헬퍼를 통해 소유 인스턴스를 받아야 한다"

requirements-completed: [E7V-01]

# Metrics
duration: 12min
completed: 2026-07-29
---

# Phase quick-260729-e7v: MainView datum 범위 TryComposeAlign 호출 소유 인스턴스 통일 Summary

**`GetInspectionSequenceForDatum(datum)` 신규 헬퍼로 datum 범위 `TryComposeAlign` 호출 3곳을 datum.OwnerName 기준 소유 InspectionSequence 인스턴스로 전환, 임의 인스턴스 조회는 폴백 1곳으로 격하(동작 변경 없는 순수 리팩토링)**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-29T01:13:00Z (추정)
- **Completed:** 2026-07-29T01:25:07Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- `MainView.xaml.cs` 에 `GetInspectionSequenceForDatum(DatumConfig datum)` private 헬퍼 추가 — `pSeq[datum.OwnerName] as InspectionSequence` 조회, 실패 시 `GetAnyInspectionSequence()` 폴백
- `GetAnyInspectionSequence()` 상단 주석을 "폴백 전용"으로 격하(본문 무변경) — 코드 기준 잔여 호출부를 폴백 1곳으로 축소
- datum 범위 `TryComposeAlign` 호출부 3곳(`TryFindTransformForReanchor`, `BtnTestFindDatum_Click` DualImage/단일 이미지 분기) 전부 `GetInspectionSequenceForDatum(datum)` 으로 교체
- Debug/x64 빌드 통과, 신규 컴파일러 진단 0건, exe 파일잠금 없이 정상 갱신됨

## Task Commits

Each task was committed atomically:

1. **Task 1: GetInspectionSequenceForDatum 추가 + datum 범위 호출부 3곳 교체** - `3a60e15` (refactor)

**Plan metadata:** (orchestrator will commit separately)

## Files Created/Modified
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `GetInspectionSequenceForDatum(DatumConfig datum)` 신규 추가 + `GetAnyInspectionSequence()` 주석 폴백 격하 + 호출부 3곳 교체

## Gate Verification Results (실측)

플랜에 명시된 `(want N)` 대비 실제 게이트 출력 — 6개 값이 baseline(수정 전)에서 목표값으로 변경됨, 나머지는 불변 확인:

| Metric | Baseline (수정 전) | Target | 실측 (수정 후) | 결과 |
|---|---|---|---|---|
| `getany_total` | 4 | 2 | 2 | PASS |
| `old_callsites` | 3 | 0 | 0 | PASS |
| `new_callsites` | 0 | 3 | 3 | PASS |
| `getfordatum_def` | 0 | 1 | 1 | PASS |
| `fallback_return` | 0 | 1 | 1 | PASS |
| `owner_lookup` | 0 | 1 | 1 | PASS |
| `getany_def` | 1 | 1 (불변) | 1 | PASS |
| `getany_roles` | 1 | 1 (불변) | 1 | PASS |
| `trycompose_calls` | 3 | 3 (불변) | 3 | PASS |
| `p1_callers` | 6 | 6 (불변) | 6 | PASS |
| `no_csharp8_added` | 0 | 0 (불변) | 0 | PASS |
| `deleted_lines_total` | - | 3 | 3 | PASS |
| `deleted_are_oldcall` | - | 3 | 3 | PASS |

`git diff --name-only` (`.planning/` 제외): `WPF_Example/UI/ContentItem/MainView.xaml.cs` 단 하나만 출력됨 — 스코프 위반 없음.

`git diff --numstat -- WPF_Example/UI/ContentItem/MainView.xaml.cs`:
```
19	3	WPF_Example/UI/ContentItem/MainView.xaml.cs
```

MSBuild `error CS|warning CS` 필터(CS0618/CS0162 제외) 결과: 빈 출력 (`CS_LIST_ABOVE_MUST_BE_EMPTY` 위 아무것도 없음). CS0618 obsolete 경고 8건은 기존 `TopSequence`/`BottomSequence`/`TopInspectionAction`/`BottomInspectionAction` 관련으로 이번 변경과 무관한 기존 경고(baseline 에도 존재).

읽기 전용 참조 파일 무변경 확인:
- `git diff -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` → 빈 출력
- `git diff -- WPF_Example/Sequence/SequenceHandler.cs` → 빈 출력
- `git diff -- WPF_Example/Sequence/Param/ParamBase.cs` → 빈 출력

**MSB302x(exe 파일잠금) 발생 여부:** 발생하지 않음. 빌드 로그에 `MSB3021`/`MSB3026`/`MSB3027` 없음. `bin/x64/Debug/DatumMeasurement.exe` 가 빌드 직후 타임스탬프로 갱신됨(재빌드 불필요).

## 신규 메서드 최종 코드 블록

```csharp
        //260622 hbk Phase 57.1 Test Find 패턴 보정 연결 — TryComposeAlign 호출용 임의 활성 InspectionSequence 인스턴스 획득
        //  TryComposeAlign 은 sequence 실행 상태 무관(인자로 입력, _datumTransforms 만 transient) → 어느 인스턴스든 가능.
        //260729 hbk quick(260729-e7v): 이제 datum 범위 조회의 1차 경로가 아니라 **폴백 전용**이다.
        //  datum 이 딸린 시퀀스를 써야 하는 호출은 반드시 GetInspectionSequenceForDatum(datum) 을 쓴다.
        private ReringProject.Sequence.InspectionSequence GetAnyInspectionSequence() {
            if (pSeq == null) return null;
            ESequence[] roles = new ESequence[] { ESequence.Top, ESequence.Side, ESequence.Bottom };
            for (int i = 0; i < roles.Length; i++) {
                ReringProject.Sequence.InspectionSequence seq = pSeq[roles[i]] as ReringProject.Sequence.InspectionSequence;
                if (seq != null) return seq;
            }
            return null;
        }

        //260729 hbk quick(260729-e7v): datum 범위 호출은 그 datum 을 **실제로 소유한** 시퀀스 인스턴스로 해야 한다.
        //  임의 인스턴스(GetAnyInspectionSequence, 사실상 항상 TOP)를 넘기면, 인스턴스 정체성에 의존하는 로직이
        //  조용히 남의 시퀀스 데이터를 집는다 — 이 버그 계열로 하루에 두 번 당했다(260728-n7b 패턴2 모델경로, 260728-l2r ref pose baseline).
        //  DatumConfig.OwnerName 은 생성 경로가 InspectionSequence.AddDatum() → new DatumConfig(this) 단 하나라 소유 시퀀스명("TOP"/"SIDE"/"BOTTOM")으로 신뢰 가능.
        //  해당 role 이 이 PC 에서 미등록(SequenceHandler.IsSequenceActive 가 Top/Bottom 롤 PC 에서 Side 를 제외)이면 조회 실패 → 기존 동작 보존 위해 폴백.
        private ReringProject.Sequence.InspectionSequence GetInspectionSequenceForDatum(DatumConfig datum) {
            if (pSeq == null) return null;
            if (datum != null && !string.IsNullOrEmpty(datum.OwnerName)) {
                ReringProject.Sequence.InspectionSequence owner = pSeq[datum.OwnerName] as ReringProject.Sequence.InspectionSequence;
                if (owner != null) return owner;
            }
            return GetAnyInspectionSequence();
        }
```

## Decisions Made
- 플랜의 `<target_shape>` 를 그대로 적용 — 주석 문구/코드 라인 모두 플랜 확정본과 동일하게 유지, 임의 조정 없음
- 호출부 3곳은 각 위치의 기존 들여쓰기(16/28/20칸)를 그대로 유지한 채 우변만 교체

## Deviations from Plan

None - plan executed exactly as written. `<target_shape>` (A)(B)(C) 를 순서대로 정확히 적용했고, 로직 변경(null 체크 추가, 에러 메시지 변경, 로그 추가 등)이나 범위 이탈 수정 없음.

## Issues Encountered
None.

## Known Stubs
None - 이번 변경은 순수 리팩토링이며 신규 UI/데이터 표시 경로가 없음.

## Threat Flags
None - 이번 변경은 플랜의 threat_model 에 이미 등록된 표면(UI → InspectionSequence 인스턴스 선택)만 다루며, 신규 네트워크/인증/파일접근/스키마 경로를 추가하지 않음.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `GetInspectionSequenceForDatum(datum)` 이 확립됐으므로, 향후 datum 범위에서 인스턴스 정체성에 의존하는 신규 로직(예: `TryComposeAlign` 확장, 새 datum 알고리즘)은 이 헬퍼를 통해 소유 인스턴스를 받아야 한다.
- 사용자 관찰 동작은 오늘과 동일 — 별도 UAT 불필요(플랜의 `<no_behavior_change_note>` 근거: modelPath 는 이미 datum.OwnerName 기준, `_datumTransforms` 는 매 사이클 `ClearDatumTransforms()` 로 초기화).
- 실기 회귀는 다음 실사용/AUTO 검사 시 자연 확인 가능(구조 변경만이라 특별 검증 불필요).

---
*Phase: quick-260729-e7v*
*Completed: 2026-07-29*

## Self-Check: PASSED
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: .planning/quick/260729-e7v-mainview-getanyinspectionsequence-datum-/260729-e7v-SUMMARY.md
- FOUND commit: 3a60e15
