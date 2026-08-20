---
phase: quick-260820-dfw
plan: 01
status: complete
subsystem: inspection
tags: [csharp, refactor, out-params-to-return-value, datum, cross-z-capture]

requires:
  - phase: quick-260819-sgg
    provides: "CrossZCaptureTickResult(K&R+public 필드) 패턴 확립 — 이번 플랜의 DualDatumImageResult 도 동일 스타일 재사용"
provides:
  - "DualDatumImageResult(Horizontal/Vertical/Pending) 클래스 신설 — Datum DualImage 로드 체인 6개 함수를 관통하던 out 3종(HImage/HImage/bool) 조합을 이름 있는 필드로 교체"
  - "TryGrabOrLoadDualDatumImages 는 외부 out 시그니처(imageHorizontal/imageVertical/bPending) 유지, 나머지 5개 함수(TryLoadStaticDualDatumImages/TryGrabOrLoadCrossZDatumImages/TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore/TryTakeCrossZImageClones)는 DualDatumImageResult result 파라미터로 전환(out 파라미터 0개)"
affects: [action-faimeasurement]

tech-stack:
  added: []
  patterns:
    - "out 다중 파라미터 대신 이름 있는 필드를 가진 소형 private class 리턴값 사용 — 파일 상단 ShotMeasureAccumulator/CrossZCaptureTickResult 와 동일한 K&R+public 필드 스타일. 참조형 result 인스턴스를 체인 전체에 공유 전달(out/ref 별칭과 동치)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "TryGrabOrLoadDualDatumImages 만 유일한 외부 호출부(ProcessDatumDualImage)를 갖고 있어 out 시그니처를 그대로 유지 — 함수 내부에서 result 객체를 생성해 하위 함수 배선 후 반환 직전 3줄로 지역 out 변수에 옮겨 담는 어댑터 패턴 적용"
  - "하위 5개 함수는 이미 초기화된 동일 result 인스턴스를 전달받으므로 함수 진입부 재초기화 코드(imageHorizontal=null 등)를 넣지 않음 — new DualDatumImageResult() 생성 시점의 필드 기본값(null/null/false)이 원본 top-of-function 초기화와 동치이기 때문"

requirements-completed: [DFW-01]

duration: 약 10분
completed: 2026-08-20
---

# Quick 260820-dfw: FAI Datum DualImage 6-함수 체인 리팩토링 (out 3종 → DualDatumImageResult) Summary

**`Action_FAIMeasurement.cs` 순수 시그니처 리팩토링 1건 — Datum 가로/세로 기준 이미지 로드 체인 6개 함수(TryGrabOrLoadDualDatumImages/TryLoadStaticDualDatumImages/TryGrabOrLoadCrossZDatumImages/TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore/TryTakeCrossZImageClones)를 관통하던 `out HImage imageHorizontal, out HImage imageVertical[, out bool bPending]` 조합을, 오늘 이미 검증된 `CrossZCaptureTickResult` 패턴과 동일한 `DualDatumImageResult` 클래스 리턴값으로 교체. 유일한 외부 호출부(`ProcessDatumDualImage`)는 1바이트도 변경 없음. `wc -l` 최종 실측값(1790)과 정확히 일치, clean Rebuild error0/warning12(baseline). 외부 호출 라인은 byte-diff(xxd)로 편집 전/후 완전 동일 확인.**

## Performance

- **Duration:** 약 10분 (커밋 1건, 사전확인+6개 Edit+검증+빌드+byte-diff 포함)
- **Started:** 2026-08-20T00:49:00Z
- **Completed:** 2026-08-20T00:59:00Z
- **Tasks:** 1/1 완료
- **Files modified:** 1

## Accomplishments

- `DualDatumImageResult` 클래스 신설(`private class`, `public HImage Horizontal/Vertical`, `public bool Pending`) — 파일 상단 `ShotMeasureAccumulator`/`CrossZCaptureTickResult` 와 동일한 K&R+public 필드 스타일, `TryGrabOrLoadDualDatumImages` 바로 위에 배치
- `TryGrabOrLoadDualDatumImages` 는 외부 `out` 시그니처(`out HImage imageHorizontal, out HImage imageVertical, out bool bPending`) 그대로 유지 — 내부에서 `DualDatumImageResult result = new DualDatumImageResult();` 생성 후 하위 함수(`TryGrabOrLoadCrossZDatumImages`/`TryLoadStaticDualDatumImages`)를 result 로 호출, 반환 직전 3줄로 지역 out 변수에 값 이관
- 나머지 5개 함수는 `out` 파라미터를 전부 제거하고 마지막 파라미터로 `DualDatumImageResult result` 를 받아 기존 `out` 대입 자리를 `result.Horizontal`/`result.Vertical`/`result.Pending` 필드 대입으로 치환 — 조건/분기/제어흐름/부수효과(`SafeDisposeImage` 호출, `CaptureAndStoreCrossZDatumImage`/`IsCrossZDatumBothStored` 흐름 등) 1도 변경 없음
- 사이에 낀 무변경 헬퍼 4개(`CaptureAndStoreCrossZDatumImage`/`BuildCrossZDatumKey`/`ResolveCrossZDatumRoleKeys`/`IsCrossZDatumBothStored`) 시그니처/본문 완전 무변경
- 로그 메시지 문자열(한국어 포함) 4종 byte-identical 보존 확인
- 신규 코드 삼항 `?:` 0건(if-else 만 사용), C# 7.2, public/internal API 노출 변경 0건

## Task Commits

Each task was committed atomically:

1. **Task 1: Datum DualImage 6-함수 체인 out 3종 조합 → DualDatumImageResult 클래스 리턴값 전환** - `084ff87` (refactor)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `DualDatumImageResult` 클래스 신설, 6개 함수 중 5개 out→result 파라미터 전환. 1781줄 → 1790줄(순증가 +9)

## Verification Results

| 검증 항목 | 기대값 | 실측값 | 결과 |
|---|---|---|---|
| `wc -l`(결정론적) | 1790 | 1790 | PASS |
| `class DualDatumImageResult` 선언 | 1 | 1 | PASS |
| `DualDatumImageResult result` 파라미터/지역변수 | 6 | 6 | PASS |
| `result.Horizontal` 참조 | 10 | 10 | PASS |
| `result.Vertical` 참조 | 10 | 10 | PASS |
| `result.Pending` 참조 | 3 | 3 | PASS |
| `out HImage imageHorizontal, out HImage imageVertical, out bool bPending` 시그니처 | 1(TryGrabOrLoadDualDatumImages 만) | 1 | PASS |
| `out HImage imageHorizontal, out HImage imageVertical) {` 시그니처(5개 내부함수) | 0 | 0 | PASS |
| 헬퍼 4개(CaptureAndStoreCrossZDatumImage/BuildCrossZDatumKey/ResolveCrossZDatumRoleKeys/IsCrossZDatumBothStored) 시그니처 | 각 1(무변경) | 각 1 | PASS |
| `ProcessDatumDualImage` 외부 호출부 문자열 매치 | 1 | 1 | PASS |
| `ProcessDatumDualImage` 외부 호출 라인(290행) byte-diff(xxd) | 0 hunk(완전동일) | 0 hunk | PASS |
| 로그 문구 4종(한국어) byte-identical | 각 1 | 각 1 | PASS |
| UTF-8 BOM | efbbbf | efbbbf | PASS |
| CRLF 오염 | 0 | 0 | PASS |
| MSBuild Rebuild(Debug/x64, 스크래치 OutputPath) | error0/warning12 | error0/warning12(CS0618×10+CS0162×2) | PASS |
| 커밋 대상 파일 수 | 1 | 1 | PASS |
| `git status` csproj | unstaged M | unstaged M | PASS |
| 커밋 diff 파일 삭제(unexpected deletion) | 0 | 0 | PASS |

### 외부 호출부 byte-diff 상세 (사용자 명시 요구)

편집 직전 290행(`if (!TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)) {`)을 `xxd` 로 스냅샷 저장 후, 6개 Edit 적용 완료 뒤(줄번호 이동 없음 — 이 줄은 6개 Edit 범위보다 위에 위치, Edit 1이 파일 상단에 클래스를 추가했지만 290행은 그보다 더 위에 있어 영향 없음) 동일 290행을 재추출해 `diff` 로 직접 대조 — hunk 0개(완전 byte-identical). `ProcessDatumDualImage` 내부의 이 유일한 외부 호출부는 이번 플랜의 6개 Edit 범위 밖이며 실측으로도 1바이트도 바뀌지 않았음을 확증.

### 커밋 위생

`git add`로 대상 파일 경로만 직접 지정(`git add -A`/`-a` 미사용), `git diff --cached --name-only` 로 정확히 1줄만 출력됨을 커밋 전 확인. 커밋 후 `git show --name-only --format='' HEAD` 도 1개 파일만 출력. `git status --porcelain` 확인 결과 `WPF_Example/DatumMeasurement.csproj` 는 커밋 전후 내내 ` M`(unstaged) 상태 유지, 한 번도 스테이징되지 않음. `git diff --diff-filter=D --name-only HEAD~1 HEAD` 결과 삭제 파일 0건.

## Decisions Made

- Edit 도구를 사용해 6개 치환(Edit 1~6) 전부 plan 의 old_string/new_string 을 그대로 적용 — 손으로 재구성하지 않음
- 사전 확인 단계에서 plan 의 baseline grep 카운트/줄수(1781줄)가 실제 파일과 정확히 일치함을 재확인 후 진행 — 재탐색 불필요
- 외부 호출부 무변경을 grep 카운트뿐 아니라 xxd 기반 byte-diff 로 추가 검증(사용자 하드 리마인더 요구사항)

## Deviations from Plan

None - plan 대로 정확히 실행됨. 6개 old_string 모두 사전 확인 시점에 정확히 1건씩 매치, new_string 적용 후 줄수/카운트/빌드/byte-diff 전부 plan 이 예측한 값과 일치.

## Issues Encountered

None.

## User Setup Required

None - 외부 서비스 설정 불필요. 정적 검증(grep 카운트+wc -l+빌드+로그 문구 byte-identical+외부 호출부 byte-diff)만으로 회귀 0 결론 — 순수 out→리턴값 전환이라 판정 로직 접근 없음. plan 이 권고한 실기 UAT(VerticalTwoHorizontalDualImage 타입 Datum 으로 고정 이미지 경로/크로스-Z 경로 각각 실행)는 선택사항이며 이번 세션에서는 미실행.

## Next Phase Readiness

- `Action_FAIMeasurement.cs` Datum DualImage 6-함수 체인 리팩토링 완료, 후속 코드 작업 없음
- Blockers 없음
- 오늘까지 리팩토링 시리즈(260819 fik/gf1/hyk/j6j/q9t/rle/s05/sgg/sxj/tcs + 260820 dfw) 전부 "동작 무변경" 검증됨, 파일 최종 1790줄

## Known Stubs

없음 - 순수 리팩토링(out→리턴값 전환)이며 신규 데이터 소스/바인딩/UI 변경 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. T-dfw-01(result 인스턴스 생성/전달 변조 위험)은 `new DualDatumImageResult()` 기본값이 원본 초기화와 동치임을 grep+빌드로 확인, T-dfw-02(사이에 낀 헬퍼 4개 무변경)는 시그니처 전수 대조로 확인 완료.

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
FOUND: .planning/quick/260820-dfw-fai-dual-datum-image-refactor/260820-dfw-SUMMARY.md
```

커밋 존재 확인:
```
FOUND: 084ff87 (Task 1)
```

---
*Phase: quick-260820-dfw*
*Completed: 2026-08-20*
