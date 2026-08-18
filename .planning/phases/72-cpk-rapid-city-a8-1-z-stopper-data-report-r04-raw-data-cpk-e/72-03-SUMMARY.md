---
phase: 72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e
plan: 03
subsystem: offline-repeat-run
tags: [material-index, repeat-run, reviewer-ui, accumulate, dto]
requires:
  - "CycleResultSerializer.BuildDto 6번째 optional 파라미터 nIndexNumber (기존)"
  - "72-02 (실행 순서 의존만 — 코드 의존 없음)"
provides:
  - "RepeatRunService.MaterialIndexNumber / RepeatRunService.MATERIAL_NOT_SET"
  - "BatchRunService.MaterialIndexNumber / BatchRunService.MATERIAL_NOT_SET"
  - "ReviewerWindow 자재번호 입력(txt_materialIndex) + 누적 실행(chk_repeatAccumulate)"
  - "SIMUL 환경에서 자재별 RAW DATA 열 분리를 검증할 수 있는 오프라인 경로"
affects:
  - "WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs"
  - "WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs"
  - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml"
  - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs"
tech-stack:
  added: []
  patterns:
    - "sentinel-constant — 인라인 -1 대신 MATERIAL_NOT_SET 상수로 미지정 표현"
    - "caller-set property before Start — 서비스가 Stop() 에서 리셋하지 않고 호출자가 매 실행 지정"
    - "twin-file parity — RepeatRunService/BatchRunService 를 동일 형태로 동시 수정"
    - "input whitelist — int.TryParse 로 정수만 통과 (자유 텍스트 injection 차단)"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs"
    - "WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs"
    - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml"
    - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs"
decisions:
  - "MATERIAL_NOT_SET(-1) 을 두 서비스에 각각 선언 — 쌍둥이 파일 독립성 유지, 공용 상수 클래스 신설은 과잉"
  - "Stop() 에서 MaterialIndexNumber 를 리셋하지 않음 — 다음 실행 시작 시 호출자가 다시 지정하는 계약"
  - "BatchRunService 는 UI 입력을 붙이지 않음 — 기본값 -1 이므로 동작 회귀 0, 쌍둥이 정합만 유지"
  - "누적 판단 bAccumulate/prevCycles 를 클릭 시점 로컬로 캡처해 람다에 클로저 — 실행 중 체크박스를 바꿔도 그 회차 동작이 흔들리지 않는다"
  - "빈 입력은 경고 없이 미지정(-1) 폴백 — 기존 사용 흐름을 막지 않기 위함"
metrics:
  duration: "약 9분"
  completed: "2026-08-18"
  tasks: 2
  files: 4
---

# Phase 72 Plan 03: 오프라인 반복검사 자재번호 입력 Summary

`RepeatRunService`/`BatchRunService` 가 `BuildDto()` 에 자재번호를 안 넘겨 결과 DTO 의 `IndexNumber` 가 항상 -1 이던 것을, 서비스 프로퍼티 + ReviewerWindow 입력 UI 로 연결해 SIMUL 환경에서 자재별 RAW DATA 열 분리를 검증할 수 있게 했다.

## What Was Built

### Task 1 — 두 서비스에 MaterialIndexNumber 전파 (`f615e5f`)

`RepeatRunService.cs` / `BatchRunService.cs` 에 동일 형태로 3가지 추가:

- `public const int MATERIAL_NOT_SET = -1;` — `CycleResultDto.IndexNumber` 기본값과 같은 sentinel. 인라인 -1 금지.
- `public int MaterialIndexNumber { get; set; } = MATERIAL_NOT_SET;` — `TargetCount` 프로퍼티 바로 뒤.
- `HandleFinish` 안 `BuildDto(...)` 호출에 6번째 인자 `MaterialIndexNumber` 추가. 읽기는 기존 `lock (_lock)` 블록 안이라 별도 동기화 불필요.

`Stop()` 은 손대지 않았다 — `MaterialIndexNumber` 를 리셋하지 않는 것이 의도된 계약이다.

### Task 2 — ReviewerWindow 자재번호 입력 + 누적 UI (`aae896c`)

`ReviewerWindow.xaml` 좌측 패널: `<Separator>` 와 `btn_repeatRun` 사이에 안내 `TextBlock` + `txt_materialIndex` TextBox + `chk_repeatAccumulate` CheckBox 삽입 (기존 `FontSize="11"` / `Foreground="#334155"` 관례 준수).

`ReviewerWindow.xaml.cs` `Button_RepeatRun_Click`:

- 이미지 목록 가드 직후 자재번호 파싱 블록 추가. 빈 입력 → `MATERIAL_NOT_SET`, 숫자 아님 → 경고 후 return.
- `_repeatCycles = null;` 을 `bAccumulate` 조건부로 변경하고 `prevCycles` 로컬에 캡처.
- `_repeatService.MaterialIndexNumber = nMaterialIndex;` 를 생성 직후 대입.
- `OnRepeatComplete` 안에서 누적 시 `prevCycles + cycles` 병합, `nTotal` 로 진행 라벨/Export 버튼 갱신. `Dispatcher.Invoke` 마샬링 유지.
- 기존 삼항 `cycles != null ? cycles.Count : 0` 제거 (if-else 대체).

## Verification

| 항목 | 결과 |
|------|------|
| Task 1 acceptance grep (10건) | 전부 기대값 일치 (prop/call/sentinel 각 1, 구 호출부 0, 삼항 0) |
| Task 2 acceptance grep (8건) | 전부 기대값 일치 (txt/chk/assign/parse/merge 각 1, 구 삼항 0, `Dispatcher.Invoke` 2) |
| msbuild Debug/x64 (scratch OutDir) | exit 0, CS 에러 0, XAML 컴파일 에러 0 |
| 빌드 경고 | 12줄 (CS0618×10 + CS0162×2) = baseline, 신규 경고 0 |
| 회귀 — `InspectionSequence.cs` | 무수정 (TCP $TEST 경로 3개 BuildDto 호출부 그대로) |
| 회귀 — `CycleResultSerializer.cs` | 무수정 |
| 파일 삭제 | `--diff-filter=D` 두 커밋 모두 결과 없음 |

## must_haves 대응

| Truth | 근거 |
|-------|------|
| 자재번호 입력 → 그 회차 DTO 의 IndexNumber 에 반영 | `txt_materialIndex` → `nMaterialIndex` → `_repeatService.MaterialIndexNumber` → `BuildDto` 6번째 인자 |
| 빈 입력 → -1 유지 | `IsNullOrWhiteSpace` 시 파싱 스킵, 초기값 `MATERIAL_NOT_SET` 그대로 |
| 누적 체크 → 두 실행 결과가 하나로 병합 | `prevCycles` 캡처 + `merged.AddRange(cycles)` |
| TCP $TEST 경로 무영향 | `InspectionSequence.cs` 무수정, 신규 파라미터 기본값 -1 |

## Coding Rules 준수

- 삼항 연산자 미사용 — 오히려 기존 1건 제거 (4파일 전부 `[^?]\? .+ : ` 0 matches)
- 헝가리언 (`nMaterialIndex` / `szMaterial` / `bAccumulate` / `nTotal`)
- Allman 브레이스 (4파일 모두 기존 스타일 유지)
- C# 7.2 문법만 사용 (자동 프로퍼티 초기화 = C# 6, `out` 은 사전 선언 변수)
- 신규 파일 없음 → csproj 수정 불필요

## Threat Model 대응

- **T-72-03 (Tampering)** — mitigate 적용됨. `int.TryParse` 로 정수만 통과, 실패 시 경고 후 return. 자유 텍스트가 시트명/파일명으로 흐르는 경로 차단.
- **T-72-04 (DoS)** — accept 그대로. 누적은 명시적 체크가 필요하고 창을 닫으면 해제된다.

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## 다음 plan 참고사항

- `BatchRunService.MaterialIndexNumber` 는 프로퍼티만 있고 UI 입력 경로가 없다 — 일괄검사에서도 자재번호를 쓰려면 `InspectionListView` 쪽에 입력을 붙여야 한다(이번 Phase 범위 밖, `InspectionListView.xaml.cs` 는 K&R 브레이스임에 주의).
- 누적 실행 중 `_repeatService` 가 새로 생성되므로 이전 서비스 인스턴스의 자재번호는 남지 않는다 — 매 실행마다 TextBox 값을 다시 읽는다.
- 자재번호 열 분리 실제 확인은 `RepeatExcelExportService` 출력에서 해야 한다(72-04 이후 체크포인트).

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` — FOUND
- `WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs` — FOUND
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` — FOUND
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — FOUND
- commit `f615e5f` — FOUND
- commit `aae896c` — FOUND
