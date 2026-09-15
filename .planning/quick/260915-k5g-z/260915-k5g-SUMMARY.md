---
phase: quick-260915-k5g
plan: 01
subsystem: 결과 리뷰어 (WPF_Example/UI/Reviewer, ViewModel)
tags: [reviewer, ux, side-z-range, display-only]
dependency-graph:
  requires: [Phase 77 SIDE Z 범위 (SelectedZIndex, Z_RANGE_PENDING, TickJudgement)]
  provides: [ReviewerListLabelBuilder.ClassifyTick/IsIntermediateTick/IsListItemVisible/BuildUsedZSummary]
  affects: [WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs), WPF_Example/UI/ViewModel/CycleResultDto.cs]
tech-stack:
  added: []
  patterns: ["결정론적 분류 함수(ClassifyTick) → 단일 Build() 분기", "표시 전용 파생값 — cycle.json/CSV 형식 무변경"]
key-files:
  created: []
  modified:
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
decisions:
  - "중간 단계 분류 순서 고정: dto null → Result, ZIndex<0(수동/옛 JSON) → Result, IsFailTick=true → Result, 결과 있는 측정 1개 이상 → Result, Z_RANGE_PENDING 1개 이상 → ZRangePending, 나머지 → DatumOnly (K-1) — 불량 은폐 방지와 옛 데이터 무변경을 최우선"
  - "사용 Z 요약 유효 판정은 MeasurementBase.FormatSelectedZ 결과가 빈칸이 아닌지로만 판단 — 별도 '최소값 1' 규칙을 복제하지 않음 (K-5)"
  - "'불량만 보기' + '중간 단계도 보기' 는 IsListItemVisible 한 함수로 AND 합성 — 중간 줄은 IsNg=false 이므로 불량만 보기 켜면 항상 숨음 (K-4)"
metrics:
  duration: "약 45분"
  completed: "2026-09-15"
status: complete
---

# Quick 260915-k5g: 결과 리뷰어 목록 단순화 + 사용 Z 요약 Summary

결과 리뷰어 좌측 목록에서 기준점·Z 범위 대기 tick을 기본 숨기고(체크 시 회색 복귀), 범위 결과 줄 끝에 실제 사용한 Z 번호(" z3" 형식)를 붙이며, 우측 측정표 열 제목을 '사용 Z'로 바꿔 Phase 77 SIDE Z 범위 UX를 사용자가 이해하기 쉽게 다듬었다.

## What Was Built

- `ReviewerListLabelBuilder`(CycleResultDto.cs)에 `EReviewerTickKind` 열거형과 `ClassifyTick`/`IsIntermediateTick`/`IsListItemVisible`/`BuildIntermediateLabel`/`BuildUsedZSummary`/`CollectMeasurements`/`HasMeasuredResult`/`ShotHasZRangePending` 를 추가했다. `Build(dto)`는 맨 앞에서 `ClassifyTick`으로 분류해 중간 tick이면 짧은 회색 문구(`14:17:18  z=04  대기  SIDE_SHOT_1_C13-14` / `14:17:16  z=01  기준점`)를 반환하고, 결과 tick이면 기존 로직 그대로 실행한 뒤 문자열 맨 끝에 `BuildUsedZSummary`(예: ` z3`, ` z3·z4`, 없으면 빈 문자열)를 붙인다.
- `ReviewerWindow.xaml.cs`는 배선만 추가했다: `LoadCycleFolders`가 `CycleListItem.IsIntermediate`를 채우고, `ApplyCycleListFilter`가 `IsListItemVisible`을 호출하도록 한 문장으로 교체했으며, `ChkShowIntermediate_Changed` 핸들러(본문 1줄, `ApplyCycleListFilter()` 호출)를 추가했다.
- `ReviewerWindow.xaml`에 '중간 단계도 보기' CheckBox(기본 해제, 좌측 버튼 스택 맨 아래)와 `IsIntermediate` 회색(`#9E9E9E`) DataTrigger를 추가했고, 우측 DataGrid `선택Z` 열 제목을 `사용 Z`로 바꿨다(측정값 옆 위치·바인딩·폭 55는 유지).

## Verification (실데이터 5개 날짜, D:\Data 읽기 전용)

Framework csc로 컴파일한 `ReviewerLabelProbe.exe`가 `20260601`(옛 JSON), `20260811`(옛 JSON), `20260910`, `20260914`, `20260915`(SIDE Z 범위)의 cycle.json 919개를 리플렉션으로 라벨링하고, `compare.js`가 편집 전(baseline) 대비 편집 후(after1=Task1 tracer, after2=Task2 final)를 비교했다.

- Task 1 (tracer 모드) `compare.js` 출력: `PASS` — `{"20260601":{"result":8,"pending":0,"datum":0,"zsuffix":0},"20260811":{"result":411,"pending":0,"datum":0,"zsuffix":0},"20260910":{"result":42,"pending":0,"datum":74,"zsuffix":0},"20260914":{"result":121,"pending":0,"datum":46,"zsuffix":0},"20260915":{"result":111,"pending":48,"datum":58,"zsuffix":0}}`
- Task 2 (final 모드) `compare.js` 출력: `PASS` — `{"20260601":{"result":8,"pending":0,"datum":0,"zsuffix":0},"20260811":{"result":411,"pending":0,"datum":0,"zsuffix":0},"20260910":{"result":42,"pending":0,"datum":74,"zsuffix":0},"20260914":{"result":121,"pending":0,"datum":46,"zsuffix":0},"20260915":{"result":111,"pending":48,"datum":58,"zsuffix":24}}`
- 계획 단계 예상치는 `20260915` 대기 tick "약 28개"였으나 실측은 48개다. 원인: 계획 단계 예상치는 문서화된 단일 제품(z=1..6 6 tick)만 손으로 센 값이고, `20260915` 폴더에는 여러 제품/사이클이 섞여 있어 스케일이 다르다. `compare.js`의 정확도 검증은 이 총합 카운트가 아니라 (a) `IsFailTick`이 전 tick에서 baseline과 완전히 동일, (b) 불량 tick이 절대 숨겨지지 않음(fail tick hidden 검사), (c) 중간 라벨 정규식(`INTER`)과 시각/z 접두어 불변, (d) 지정된 5개 실 tick(141716056=기준점, 141718976=대기+C13-14만, 141720305=` z3` 추가, 141721557·141740917=완전 동일)의 명시적 assertion으로 이루어졌으며 전부 통과했다. 카운트 불일치는 로직 결함이 아니라 계획 단계 예상치의 표본 크기 차이다.
- `zsuffix=24`(20260915) — 실제로 사용 Z 요약이 붙은 결과 줄 24건, 나머지 결과 줄(20260601·20260811·20260910·20260914 전부, 20260915의 비범위 결과 줄)은 문구가 baseline과 완전히 동일했다.
- Debug|x64 빌드: Task 1·Task 2 각각 `build=0`, `error CS`/`error MC` 0건. Release 빌드는 실행하지 않았다(D:\Data 배포 exe 보호).
- 가독성 게이트(삼항·null 병합·null 조건·switch 식·날짜 주석·서명·3연속 논리연산자) 3개 파일 전부 0.
- 배선 게이트(ReviewerWindow.xaml.cs): `branch_kw=0`, `newlist=0`, `codelines=9`(≤15).
- 범위 게이트: 변경 파일 정확히 3개(`CycleResultDto.cs`, `ReviewerWindow.xaml`, `ReviewerWindow.xaml.cs`) — 검사 로직·Serializer·CSV·MainView.xaml.cs·csproj 무수정.
- 컬럼 인접성: `측정값` 다음 줄이 `사용 Z`(`adjacent=1`), 구 헤더 `선택Z` 0건.
- 삭제 줄 수: `CycleResultDto.cs` 0, `ReviewerWindow.xaml` 1, `ReviewerWindow.xaml.cs` 11 — 전부 허용치 이내.

## Deviations from Plan

**오케스트레이터 수정 (f6b00b95):** 실행 에이전트가 사용 Z 요약을 줄 맨 끝(" z3")에 붙여 "… 종합 OK z3" 로 읽히는 문제가 남아 있었다. 오케스트레이터 결정대로 판정 바로 뒤 "사용 z3" / "사용 z3·z4" 로 옮겼다(USED_Z_PREFIX="사용 ", Build() 에서 판정 다음에 SEP+요약 삽입, 줄 끝 추가 제거). 실데이터 20260915 확인: `14:17:20  z=05  NG  사용 z3  SIDE_SHOT_1_C13-14 ← …`, 중간 tick `z=01  기준점` / `z=03  대기  SIDE_SHOT_1_C13-14`. Debug|x64 빌드 PASS, 추가 줄 하드룰 0.

그 외 — 계획대로 정확히 실행했다. Task 1에서 실수로 Task 2 내용(BuildUsedZSummary)을 CycleResultDto.cs 편집 중 먼저 넣었다가, tracer 검증 전에 되돌려 Task 1만 단독으로 빌드·검증·커밋한 뒤 Task 2에서 다시 추가했다(계획의 tracer→feedback gate 순서를 지키기 위한 작업 순서 조정, 최종 커밋 내용에는 영향 없음).

## Human-Check (비차단, UAT 권장)

리뷰어 → 날짜 폴더 20260915 열기:
1. 기본 목록에 z=01~04 줄 없음, z=05 줄 끝에 ` z3` 확인
2. '중간 단계도 보기' 체크 시 회색 '대기'/'기준점' 줄 등장
3. '불량만 보기' 체크 시 빨간 결과 줄만 남음(중간 단계 줄은 여전히 안 보임)
4. 우측 표 '사용 Z' 열이 측정값 옆에 보임

## Commits

- `a80b8eb8` feat(quick-260915-k5g): 리뷰어 목록 중간 단계 tick 기본 숨김 + 중간 단계도 보기
- `7b4da178` feat(quick-260915-k5g): 리뷰어 결과 줄 사용 Z 요약 + 사용 Z 열 제목

## Self-Check: PASSED

- FOUND: WPF_Example/UI/ViewModel/CycleResultDto.cs
- FOUND: WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
- FOUND: WPF_Example/UI/Reviewer/ReviewerWindow.xaml
- FOUND: a80b8eb8
- FOUND: 7b4da178
