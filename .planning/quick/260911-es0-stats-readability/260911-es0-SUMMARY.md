---
phase: quick-260911-es0
plan: 01
subsystem: ui
tags: [wpf, datagrid, statistics, cpk, mvvm-lite]

requires: []
provides:
  - "StatisticsWindow 행 상태 3단계(빨강/노랑/기본) 색 표시"
  - "문제 항목만 보기 즉시 필터(Items.Filter)"
  - "기본 정렬 = 나쁜 순(불량→주의→정상, Cpk 오름차순), Cpk/벗어난 양 헤더 숫자 정렬"
  - "기준/허용범위/벗어난 양 칸 3개 추가"
  - "필터바 요약 한 줄(전체/불량/주의/정상 개수)"
affects: [statistics-window, cpk-report-export]

tech-stack:
  added: []
  patterns:
    - "판정/표시/정렬/필터 로직을 code-behind 에서 정적 헬퍼 클래스(StatRowPresenter)로 분리 — UI 비의존, 순수 함수"

key-files:
  created: []
  modified:
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml

key-decisions:
  - "최소/최대(MinValue/MaxValue) 칸은 추가하지 않음 — 넣으면 약 1280px 폭을 넘어 기본 창에서 가로 스크롤 발생, NG 수·벗어난 양으로 불량 여부는 이미 보이고 분포는 히스토그램/추이 차트로 이미 보임"
  - "공차 미설정 행은 CpkText 표시값을 그대로 두고(기존 표시 유지, 회귀 방지), 판정(JudgeStatus)·정렬(GetCpkSortValue)에서만 계산불가 취급 — 표시와 판정 기준을 분리"
  - "DetectFailCount 는 행 상태 판정(JudgeStatus)에 넣지 않음 — 요구 범위(R1)가 NG/Cpk 만 지정, 검출실패는 기존 그대로 별도 칸으로만 노출"
  - "XAML 추가 줄에는 CLAUDE.md 삼항/null 연산자 grep 을 적용하지 않음 — Binding/StringFormat 등 XAML 문법 자체가 grep 패턴과 오탐 충돌 가능. CLAUDE.md 하드룰 대상은 C# 조건 연산자이므로 .xaml.cs 파일에만 적용"

requirements-completed: [R1-row-status-color, R2-problem-only-filter, R3-default-sort-worst-first, R4-extra-columns, R5-summary-line]

duration: 30min
completed: 2026-09-11
---

# Quick 260911-es0: 통계 창 가독성 개선 Summary

**StatisticsWindow 에 행 상태 색 3단계 + "문제 항목만 보기" 필터 + 나쁜 순 기본 정렬 + 기준/허용범위/벗어난 양 칸 + 요약 줄을 정적 헬퍼(StatRowPresenter)로 구현**

## Performance

- **Duration:** 약 30분
- **BASE commit:** `962a82c0` (docs(state): fast-260911 기록)
- **Tasks:** 2/2 auto 태스크 완료 + Task 3(실기 UAT) 는 아래 "Manual Verification Required" 로 이관
- **Files modified:** 2 (StatisticsWindow.xaml.cs, StatisticsWindow.xaml)

## Accomplishments
- `EStatLevel`(Bad/Warning/Normal) + `StatRow` 신규 5개 프로퍼티(StatusLevel, CpkSortValue, ToleranceRangeText, OutOfRangeText, OutOfRangeAmount) 추가
- `StatRowPresenter` 정적 헬퍼 클래스 신설(UI 비의존, 순수 함수): JudgeStatus, GetCpkSortValue, BuildToleranceRangeText, FillOutOfRange, SortWorstFirst, BuildSummary, IsProblemRow + 기존 BuildRows/CpkToText/YieldRateToText 이동
- RowStyle 에 DataTrigger 2개 추가(Bad=빨강 배경/진한 빨강 글자, Warning=노랑 배경/진한 갈색 글자). 기존 IsSelected 파란 테두리+굵게 강조는 손대지 않아 색 행 위에서도 그대로 보임
- "문제 항목만 보기" 체크박스 + `grid_Stats.Items.Filter` 즉시 적용, 선택 행이 필터로 사라지면 차트를 비움
- Columns 12개 → 15개: 기준/허용범위/벗어난 양 3칸 추가, Cpk·벗어난 양 칸에 `SortMemberPath` 지정(숫자 정렬)
- 필터바 아래 요약 TextBlock "전체 N항목 · 불량 X · 주의 Y · 정상 Z" (체크박스 상태와 무관하게 전체 rows 기준)

## Task Commits

1. **Task 1: StatRowPresenter 정적 헬퍼 + EStatLevel + StatRow 신규 프로퍼티** - `cbdc00ca` (feat)
2. **Task 2: XAML 색 트리거·체크박스·요약·신규 칸 + code-behind 배선** - `83e45f6c` (feat)

_Task 3(체크포인트, 실기 확인)은 아래 "Manual Verification Required" 참고. 코드 커밋 없음._

## Files Created/Modified
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` - EStatLevel/StatRow 신규 프로퍼티, StatRowPresenter 정적 헬퍼(판정/정렬/문자열/요약/필터), DoQuery 배선, ApplyProblemFilter/Chk_ProblemOnly_Changed 추가
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml` - RowStyle DataTrigger 2개, 체크박스+요약+범례 StackPanel, 칸 12개→15개(기준/허용범위/벗어난 양 추가, Cpk/벗어난 양 SortMemberPath)

## Decisions Made
- 최소/최대 칸 생략 (frontmatter key-decisions 참고 — 폭 초과 방지, NG 수·벗어난 양·차트로 대체 가능)
- 공차 미설정 행: CpkText 표시는 유지, 판정/정렬에서만 계산불가 취급 (기존 표시값 회귀 방지)
- DetectFailCount 는 행 상태 판정에서 제외 (요구 범위 밖, 기존처럼 별도 칸으로만 노출)
- XAML 추가 줄에는 삼항/null 연산자 grep 미적용 (XAML 문법 오탐 방지, CLAUDE.md 규칙 대상은 C# 조건 연산자)

## Deviations from Plan

None - plan executed exactly as written. 유일한 즉석 수정은 Task 1 자체 검증 단계에서 발견한 사항(아래).

### Auto-fixed Issues

**1. [Rule 2 - CLAUDE.md 하드룰 준수] BuildRows 이동 시 hbk 날짜 주석 1건 잔존 발견 → 제거**
- **Found during:** Task 1 자체 grep 검증(`hbk` 게이트)
- **Issue:** `BuildRows` 를 `StatRowPresenter` 로 이동하면서 원본의 `//260707 hbk 불량률→수율` 주석이 diff 상 추가 줄로 잡혀 hbk 게이트가 1건으로 나옴 (hard_rules: "이동하는 기존 코드의 hbk 주석도 이동 시 지운다")
- **Fix:** 해당 줄 주석을 `// 불량률→수율 긍정지표 전환` 으로 교체(날짜/서명 제거, 내용은 유지)
- **Files modified:** WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
- **Verification:** 재실행 grep 6종 전부 0
- **Committed in:** `cbdc00ca` (Task 1 커밋에 포함, 별도 커밋 아님)

---

**Total deviations:** 1 auto-fixed (Rule 2 — CLAUDE.md 하드룰 준수)
**Impact on plan:** 코드 이동 과정의 부수 정리로 스코프 변경 없음.

## Issues Encountered
None.

## Manual Verification Required

Task 3(checkpoint:human-verify, non-blocking)는 실행하지 않고 아래 체크리스트를 기록만 하고 종료합니다. 사용자가 실기(Release x64, D:\Data)에서 확인 후 결과를 알려주세요.

- [ ] 1. 프로그램 실행(Release x64) → 메뉴에서 양산 이력 통계 창 열기 → 기간 오늘로 조회
- [ ] 2. NG 가 난 항목이 연한 빨강 배경으로 표 맨 위에 모여 있는지. Cpk 1.0~1.33 항목은 연한 노랑, 나머지는 흰색
- [ ] 3. 행 하나 클릭 → 파란 테두리 + 굵은 글씨 선택 강조가 빨강/노랑 행에서도 보이고, 아래 히스토그램/추이 차트가 그려지는지
- [ ] 4. "벗어난 양" 칸이 "하한 −0.0425" 형태로 보이는지(범위 안이면 "-"). "허용범위" 는 "하한 ~ 상한", "기준" 은 소수 4자리
- [ ] 5. "문제 항목만 보기" 체크 → 조회 안 눌러도 빨강/노랑만 남는지. 선택했던 행이 남아 있으면 차트 유지, 사라졌으면 차트가 비는지. 체크 해제 → 전체 복귀
- [ ] 6. 요약 줄 "전체 N항목 · 불량 X · 주의 Y · 정상 Z" 숫자가 색 칠해진 행 수와 맞는지, 체크박스를 켜도 숫자가 안 바뀌는지
- [ ] 7. Cpk 헤더 클릭 → 숫자 순 정렬(∞ 뒤, "-" 맨 뒤). 조회 다시 누르면 나쁜 순으로 돌아오는지
- [ ] 8. 기존 기능: 레시피 콤보 필터, CPK 리포트 export 저장이 전과 같이 되는지

**resume-signal:** "approved" 또는 문제 항목 설명 (사용자가 장비 확인 후 별도로 알려줌)

## Next Phase Readiness
- 코드/빌드 완료, 실기 UAT 대기 상태로 STATE.md 에 기록됨
- 후속 작업 없음 — UAT 통과 시 그대로 종료

---
*Quick task: 260911-es0*
*Completed: 2026-09-11*

## Self-Check: PASSED

- FOUND: WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
- FOUND: WPF_Example/UI/Statistics/StatisticsWindow.xaml
- FOUND: .planning/quick/260911-es0-stats-readability/260911-es0-SUMMARY.md
- FOUND commit: cbdc00ca
- FOUND commit: 83e45f6c
