---
phase: quick-260812-na0
plan: 01
subsystem: ui-diagnostics
tags: [halcon, teaching-ux, score-grading, wpf, tcp-protocol]

# Dependency graph
requires:
  - "quick-260812-m8i (TeachDiagnostics.cs — ETeachGrade/ClassifyScore/ToStatusLine/GradeBrush)"
provides:
  - "Datum 완료 모달 등급 2곳 — 패턴1(rs)/패턴2(rs2) ClassifyScore + ToStatusLine 배선"
  - "AlignShapeMatchService.TryTeach 스코어 노출 오버로드 2개(무-슬롯/슬롯) — 기존 오버로드 2개는 diff 삭제줄 0으로 완전 보존"
  - "Tray/Bottom Align 티칭 성공 라벨 등급 — Math.Min(dScore1, dScore2) 기반"
  - "PickerCenterCalibrationService.TryAddStep out double dScore 시그니처 확장 — 호출부 2곳(TCP/수동 UI) 갱신"
  - "$ALIGN_CALIB TCP STEP 성공 로그에 score+grade 추가"
  - "Bottom 수동 피커캘 스텝 라벨 등급(문구만, 색 무변경)"
affects:
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
  - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
  - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
  - WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
  - WPF_Example/Custom/SystemHandler.cs
  - "다음 Quick(260812 #3 — Calib TCP 자동경로의 화면 노출)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "판정 로직과 완전히 분리된 표시 전용 헬퍼(Quick #1 TeachDiagnostics)를 '이미 계산되고 버려지던 지역변수'에만 연결 — 새 HALCON 호출 0, 새 판정분기 0. 카운트 기반 검증보다 강한 보증으로 diff 삭제줄 0(Align 서비스)/스냅샷 대비 삭제줄 0(Picker 서비스)을 요구해 회귀를 원천 차단."
    - "판정 엔진(Align/Calib) 내부 지역변수를 밖으로 빼는 두 가지 다른 전략 — 오버로드 추가(Align, 기존 시그니처 완전 불변 요구) vs 시그니처 확장(Calib, 컴파일러가 호출부 누락 강제검출). 요구사항 차이에 따라 전략을 다르게 선택."
    - "미커밋 실험 보존 커밋 절차 — 스냅샷/패치 백업 → git apply -R 로 실험만 워킹트리에서 제거 → 그 상태로 커밋 → 백업본으로 워킹트리 복원. 실패 시 즉시 백업 복원 + ABORT_* 코드로 중단(실제 exit 1 게이트, 산문 설명 아님)."

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
    - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
    - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
    - WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
    - WPF_Example/Custom/SystemHandler.cs

key-decisions:
  - "Align: 코어 TryTeach 본문을 '옮기지' 않고 캡처 필드 2개(_lastTeachScore1/2) + 위임 전용 오버로드 2개를 추가 — 100줄 이동 diff(전사 오류 위험)를 피하고 diff 삭제줄 0을 확보. 캡처는 각 가드(if (!bRefN) {...})의 닫는 } 바로 다음 줄에만 두어 실패 경로 stale 유출을 방지(직전 2줄에 return false; 존재로 기계적 검증)."
  - "Calib: 오버로드 추가 대신 TryAddStep 시그니처 자체를 out double dScore 로 확장 — 호출부가 2곳뿐이라 컴파일러가 누락을 강제 검출하는 게 더 안전. 사용자의 미커밋 ±5° 실험(FindShapeModel 인자)은 스냅샷 대비 삭제줄 0으로 한 글자도 건드리지 않음."
  - "lbl_calStatus 는 이 파일에 36곳 대입이 있어 이번엔 문구(●/▲/✕)만 넣고 Foreground(색)는 건드리지 않음 — 일부만 칠하면 나머지 35곳이 stale 색으로 남는 회귀를 피함."
  - "MIN_SCORE/FIND_MIN_SCORE 는 private const 를 public 으로 바꾸지 않고 읽기전용 프로퍼티(TeachMinScore/FindMinScore)를 신설 — private→public 전환은 그 자체로 삭제줄(원래 줄 대체)이 발생해 하드제약 2/3(diff 삭제줄 0)을 깨뜨리기 때문. 값(0.5)을 UI 쪽에 매직넘버로 복사하는 것도 금지해 이중소스를 만들지 않음."
  - "Datum.PatternMinScore 기본값 0.0 문제는 별도 가드를 추가하지 않음 — InvokeCreatePatternModel 상단에서 이미 datum.EnsurePerRoiDefaults() 가 PatternMinScore<=0 → 0.6 을 복원하므로, 분류 시점엔 항상 0 초과. 가드를 추가하면 그 자체가 새 제어흐름(하드제약 1 위반)이 됨."
  - "사용자의 PickerCenterCalibrationService.cs ±5° 탐색범위 실험(검증되지 않은 동작 변경)은 커밋에 싣지 않으면서도 워킹트리에는 그대로 남기기 위해, 백업→git apply -R→커밋→복원의 4단계 절차를 실제 shell 게이트(각 단계 실패 시 즉시 백업 복원 + exit 1)로 실행 — 절차 중 어느 단계도 실패하지 않아 정상 완주(RESTORE_OK)."

requirements-completed: [TEACH-UX-01]

# Metrics
duration: ~25min
completed: 2026-08-12
---

# Quick Task 260812-na0: 티칭/캘리브레이션 매칭 점수 등급 실배선 (2/3) Summary

**Datum(rs/rs2)·Align(_lastTeachScore1/2)·Calib(score[0].D) 세 경로에서 이미 계산되고 버려지던 매칭 점수를 `out`/캡처로 꺼내 `TeachDiagnostics.ClassifyScore`에 연결 — 새 HALCON 호출 0, 판정 로직 diff 삭제줄 0(Align/Calib 서비스), 사용자 미커밋 실험 완전 보존**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-08-12
- **Tasks:** 3 of 3
- **Files modified:** 6 (MainView 1, AlignShapeMatchService 1, TrayVisionView 1, BottomVisionView 1, PickerCenterCalibrationService 1, SystemHandler 1)

## Accomplishments

### Task 1 — Datum 완료 모달 등급 (`MainView.xaml.cs`, 커밋 `38bb1ae`)

- 패턴 2 성공 문구(`alignMsg`): `rs2`(이미 `TryFindPose`가 채운 지역변수) → `ClassifyScore(rs2, datum.PatternMinScore)` → `ToStatusLine(grade2, ...)`로 감쌈. `"\n"`은 `ToStatusLine` 밖에 둬 등급 기호가 줄 맨 앞에 오도록 함.
- 완료 모달 본문(`ShowConfirmation` 1번째 인자): `rs` → `ClassifyScore(rs, datum.PatternMinScore)` → `ToStatusLine(grade1, ...)`로 감쌈. `ShowConfirmation(` 호출줄/`MessageBoxButton.YesNo);` 줄은 무변경.
- 새 `find`/`svc.` 호출 0건 — `rs`/`rs2`는 순수 재사용.

### Task 2 — Align 스코어 노출 (`AlignShapeMatchService.cs` + Tray/Bottom, 커밋 `1b00964`)

- `AlignShapeMatchService.cs`: 오직 삽입만(diff 삭제줄 0).
  - `TeachMinScore` 읽기전용 프로퍼티(`MIN_SCORE` 그대로 노출).
  - 캡처 필드 `_lastTeachScore1/2`(초기값 `double.NaN`) 신설.
  - 코어 `TryTeach` 안, `if (!bRef1) {...}` / `if (!bRef2) {...}` 가드의 닫는 `}` 바로 다음 줄에 캡처 대입 1줄씩(성공 경로 전용 — 직전 2줄에 `return false;` 존재로 검증).
  - 신규 오버로드 2개(무-슬롯/슬롯) — 기존 코어를 그대로 호출하고 `bOk==true`일 때만 캡처값을 `out dScore1/dScore2`로 전달. `public bool TryTeach(` 선언 4건(기존 2 + 신규 2), 파라미터 개수가 전부 달라 오버로드 모호성 없음.
- `TrayVisionView.xaml.cs`/`BottomVisionView.xaml.cs`: 호출 인자에 `out dScore1, out dScore2,` 삽입(기존 줄 무변경) → 성공 분기에서 `Math.Min(dScore1, dScore2)` → `ClassifyScore` → `ToStatusLine`/`GradeBrush` 페어로 라벨 갱신. 실패/예외 분기는 Quick #1 상태 그대로.

### Task 3 — Calib 스코어 노출 + 실험 보존 커밋 (`PickerCenterCalibrationService.cs`/`SystemHandler.cs`/`BottomVisionView.xaml.cs`, 커밋 `9935b80`)

- `PickerCenterCalibrationService.cs`: 오직 삽입만(스냅샷 대비 삭제줄 0).
  - `FindMinScore` 읽기전용 프로퍼티(`FIND_MIN_SCORE` 그대로 노출).
  - `TryAddStep` 시그니처에 `out double dScore,` 추가(`out string error)` 앞) + `foundCol = 0.0;` 다음 `dScore = 0.0;` 초기화.
  - `foundCol = dCol;` 다음 `dScore = score[0].D;`(finally 의 `score.Dispose()` 이전, try 블록 안에서 복사).
  - 검색 호출 블록(사용자의 ±5° 실험 3줄 + `FindShapeModel` 인자 전체)은 **읽지도 않고 한 글자도 편집하지 않음**.
- `SystemHandler.cs`(Allman): `using TeachDiag/ETeachGrade` 별칭 2줄 → `double dScore;` 선언 → 호출 인자에 `out dScore,` 추가 → 성공 로그를 `"[ALIGN_CALIB] STEP {0} OK score={1:F3} grade={2}"` 로 교체(포맷/인자 줄만, `Logging.PrintLog((int)ELogType.Trace,` 줄 자체는 무변경).
- `BottomVisionView.xaml.cs`(피커캘 패널만, Task 2 의 Align 티칭 구역은 재편집하지 않음): `double calScore;` 선언 → 호출 인자에 `out calScore,` 추가 → 스텝 성공 라벨을 `ToStatusLine(calGrade, ...)` 로 감쌈. **`lbl_calStatus.Foreground` 는 건드리지 않음**(문구만).
- **사용자 실험 보존 커밋 절차** — 아래 "사용자 실험 처리 결과" 섹션 참고.

## Task Commits

Each task was committed atomically:

1. **Task 1: Datum 완료 모달 등급 표시** - `38bb1ae` (feat)
2. **Task 2: Align 스코어 노출 오버로드 + 성공 라벨 등급** - `1b00964` (feat)
3. **Task 3: Calib out dScore 노출 + 등급 로그/라벨 (실험 미포함)** - `9935b80` (feat, 특수 격리 커밋 절차)

**Plan metadata:** 본 SUMMARY.md 및 STATE.md/ROADMAP.md는 오케스트레이터가 별도 커밋(실행자는 커밋하지 않음).

_Note: 이 quick task는 TDD 대상이 아님(표시값 배선, 판정 분기 없음) — RED/GREEN 게이트 해당 없음._

## 브리핑 정정 사실

Datum 완료 안내는 브리핑이 추정한 `CustomMessageBox.Show` 가 아니라 **`CustomMessageBox.ShowConfirmation("모델 생성 완료", ..., MessageBoxButton.YesNo)`**(Recipe Save 확인 모달)이었다. `ShowConfirmation(` 호출줄 자체와 `MessageBoxButton.YesNo);` 줄은 무변경으로 두고, 본문 문자열 인자 한 줄만 `ToStatusLine(grade1, ...)` 로 감쌌다.

## `datum.PatternMinScore` 기본값 0.0 이슈

`DatumConfig.PatternMinScore` 의 선언 기본값은 `0.0` 이라 그대로 `ClassifyScore(rs, 0.0)` 에 넣으면 거의 전부 Good 으로 오분류될 수 있다. 하지만 `InvokeCreatePatternModel` 은 티칭 전에 `datum.EnsurePerRoiDefaults()` 를 이미 호출하고, 그 내부(`DatumConfig`)가 `if (PatternMinScore <= 0.0) PatternMinScore = 0.6;` 로 복원하므로 **분류 시점에는 항상 0 초과**다. 이 사실을 근거로 별도 가드(`if`)를 추가하지 않았다 — 가드를 추가하면 그 자체가 새 제어흐름이라 하드제약 1(판정 로직 무변경) 위반이 되기 때문이다.

## 하드 제약 검증 결과 (실제 명령 출력)

### (a) 하드제약 2 — Align 서비스 diff 삭제줄 0

```
$ git diff HEAD~1 -- WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs | grep -cE '^-[^-]'
0
```
→ 기존 오버로드 2개 시그니처와 코어 `TryTeach` 본문의 기존 줄 전부가 바이트 단위로 동일함을 증명.

### (b) 하드제약 3 — Picker 스냅샷 대비 삭제줄 0

```
$ diff -u /tmp/gsd-na0/picker-base.cs WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | grep -cE '^-[^-]'
0
```
→ 사용자의 ±5° 실험(`htRad`/`TupleGenConst`/`TupleRad(5,...)`/`-htRad`/`2.0 * htRad`)과 `FindShapeModel` 호출 인자 전체가 한 글자도 바뀌지 않았음을 증명. (`git diff` 가 아니라 편집 직전 스냅샷 대비 diff 로 판정 — `git diff` 자체는 실험 hunk 를 포함하므로 무의미.)

### (c) 하드제약 4a — HEAD 커밋에 실험 0건 / 우리 변경 1건

```
$ git show HEAD:WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | grep -c 'TupleRad(5, out htRad)'
0
$ git show HEAD:WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | grep -c 'out double dScore,'
1
```

### (d) 하드제약 4b — 워킹트리 diff 는 실험만(1건), `dScore` 는 0건(= 우리 변경은 HEAD 에 있음)

```
$ git diff -- WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | grep -c 'TupleRad(5, out htRad)'
1
$ git diff -- WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | grep -c 'dScore'
0
```

### (e) 하드제약 4c — 복원 확인

```
$ diff /tmp/gsd-na0/picker-full.cs WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs && echo RESTORE_OK
RESTORE_OK
```
→ 커밋 직전 백업본(`picker-full.cs` = 실험+우리변경)과 커밋 후 복원본이 바이트 단위로 완전 동일.

### (f) 하드제약 1 (새 HALCON 호출 0) — 6개 파일 전부

```
Task1 MainView 추가/삭제줄:            0건 (HOperatorSet./svc./TryFindPose(/return/if(/else 등)
Task2 Align 서비스 추가줄:              0건 (HOperatorSet./FindShapeModel(/_matcher.)
Task2 Tray/Bottom 추가줄:               0건 (HOperatorSet./TryCreateModel(/TryFindPose(/.Grab( 등)
Task3 Picker 추가줄(스냅샷 대비):        0건 (HOperatorSet./FindShapeModel(/htRad/TupleRad/Math.PI)
Task3 SystemHandler/Bottom 추가줄:       0건 (HOperatorSet./.Grab(/TryLoadModel(/TryComputePickerCenter( 등)
```

### (g) 하드제약 5 (표시 레이어 봉인) — 전부 0건

각 파일 화이트리스트 역검사(추가줄이 지역변수 선언/`out` 인자/`ClassifyScore`/`ToStatusLine`·`GradeBrush`/`Logging.PrintLog` 포맷/`//quick-260812` 주석 밖으로 새는지) 전부 **0건**. MainView 는 추가로 판정심볼(`svc.`/`TryFindPose(`/`datum.RefMatch`/`return`/`if (`/`else`/`ShowConfirmation(` 등) 검사도 **0건** 통과.

### (h) 등급 실배선 카운트

`ClassifyScore` 호출: MainView 2 / Tray 1 / Bottom 2 / SystemHandler 1 = **6건**(레포 전체 grep 은 `TeachDiagnostics.cs` 의 선언 1건을 더해 7건 — 선언은 Quick #1 산출물, 무변경).

### (i) stale 색 규칙 유지

`lbl_teachStatus` Text/Foreground 대입 수 — Tray **6/6**, Bottom **9/9**(Quick #1 이 확립한 동수 규칙 유지). `lbl_calStatus.Foreground` 는 이번 변경에서 **0건**(문구만 추가 — 36곳 중 일부만 칠하면 stale 색이 남기 때문).

### (j) 최종 워킹트리 범위

```
$ git status --porcelain
 M WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
?? .planning/quick/260812-na0-teach-diag-2-score-grading/
```
→ Picker 파일은 사용자 실험(±5°)만 워킹트리에 남고, 우리 변경(`dScore`/`FindMinScore`)은 이미 HEAD 커밋(`9935b80`)에 들어가 있어 `git status` diff 에는 보이지 않는다.

### (k) 무변경 파일

```
$ git status --porcelain -- WPF_Example/Halcon/Algorithms/PatternMatchService.cs WPF_Example/Halcon/Algorithms/TeachDiagnostics.cs
(빈 출력)
```
→ 두 파일 모두 이번 Quick 전체(Task 1~3)에서 단 한 번도 열리거나 수정되지 않음.

## 등급 임계값 (실제 경계)

`ClassifyScore`(Quick #1 산출물, `GOOD_MARGIN = 0.15`): `score < minScore` → Bad, `minScore <= score < minScore+0.15` → Weak, `score >= minScore+0.15` → Good.

- **Datum**: `PatternMinScore` 기본 복원값 0.6 → Bad `<0.6`, Weak `0.6~0.749`, **Good `>=0.75`**.
- **Align/Calib**: `MIN_SCORE`/`FIND_MIN_SCORE` = 0.5 → Bad `<0.5`, Weak `0.5~0.649`, **Good `>=0.65`**.

이 경계는 코드 상수 그대로이며 현장 데이터로 튜닝이 필요할 수 있다(예: 실측 티칭 점수 분포가 0.65~0.7 대에 몰려 있으면 Weak 가 과다 표시될 수 있음).

## 사용자 실험 처리 결과

`PickerCenterCalibrationService.cs` 의 미커밋 ±5° 탐색범위 실험(`git stash pop` 으로 워킹트리에 복원되어 있던 것)을 아래 절차로 처리했다:

1. **백업** — Task 1 시작 직후(편집 전) `picker-base.cs` + `experiment.patch` 스냅샷 확보. Task 3 커밋 직전 `picker-full.cs`(실험+우리변경) 추가 백업.
2. **`git apply -R`** — `experiment.patch` 를 워킹트리에서 역적용 → 파일이 `HEAD + 우리 변경` 상태로 전환(실험 완전 제거). 1차 시도로 성공(`-3` 폴백 불필요).
3. **검증** — 역적용 후 `TupleRad(5, out htRad)` 0건 + `out double dScore,` 1건 확인.
4. **사전 빌드** — 커밋될 정확한 조합(실험 없음 + 우리 변경 있음)을 실제로 컴파일해 확인(이 조합은 이전에 한 번도 빌드된 적 없었음) — 통과(exit 0, error CS 0).
5. **커밋** — 경로 명시(`git add "$PK" SystemHandler.cs BottomVisionView.xaml.cs`)로 `9935b80` 생성. 실험은 커밋에 포함되지 않음.
6. **복원** — `picker-full.cs` 를 워킹트리로 복사 → `diff` 로 백업본과 바이트 단위 동일 확인(`RESTORE_OK`).

절차 중 `ABORT_*` 코드는 한 번도 발생하지 않았다 — 모든 게이트(백업 존재/apply 성공/되돌림 확인/사전빌드/복원 확인)가 순서대로 통과했다. 최종 워킹트리는 실험이 그대로 남아 있고(`git status --porcelain` 에 ` M PickerCenterCalibrationService.cs` 1건), 우리 변경은 `9935b80` 커밋에 안전하게 들어가 있다.

## 빌드 검증

- Task 1: `MSBuild /p:Configuration=Debug /p:Platform=x64 /v:minimal` — **성공(exit 0)**. 정상 경로(앱 미실행 상태라 잠기지 않음, 스크래치 폴백 불필요).
- Task 2: 동일 정상 빌드 — **성공(exit 0)**.
- Task 3: (1) 커밋 전 사전빌드(실험 없음+우리변경 조합) — **성공(exit 0)**. (2) 실험 복원 후 `MSBuild /t:Rebuild` 전체 재빌드 — **성공(exit 0)**.
- 전 빌드 신규 `error CS` **0건**. 신규 `warning CS` **0건** — 나타난 경고 전부(`CS0618` obsolete 클래스, `CS0162` VirtualCamera 도달불가 코드)는 이번 6개 변경 파일과 무관한 pre-existing 경고(Quick #1 SUMMARY 와 동일 목록).

## Issues Encountered

None. `git apply -R` 1차 시도로 성공(3-way 폴백 불필요), 프리빌드/최종빌드 모두 1차 시도로 통과.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- Quick #3(Calib TCP `$ALIGN_CALIB` 자동경로의 **화면** 노출)은 이번 범위 밖 — 로그(`[ALIGN_CALIB] STEP {0} OK score={1:F3} grade={2}`)까지만 이번에 배선됨. 화면 노출은 별도 UI 배선이 필요.
- `AlignShapeMatchService.TeachMinScore` / `PickerCenterCalibrationService.FindMinScore` 읽기전용 프로퍼티가 확정돼 Quick #3 이 코드 수정 없이 바로 참조 가능.
- `PickerCenterCalibrationService.cs` 사용자 ±5° 실험은 워킹트리에 완전 보존(`git status --porcelain` 1줄, 커밋에는 미포함) — 다음 세션에서 실험을 검증하거나 정식 커밋할지는 사용자 판단 대기.
- Datum/Align/Calib 등급 표시 실기 UAT 권장: 실제 티칭/스텝 진행 시 점수 경계(Good/Weak/Bad 전환) 근처에서 표시 문구·색이 자연스러운지 육안 확인.

---
*Phase: quick-260812-na0*
*Completed: 2026-08-12*

## Self-Check: PASSED

- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
- FOUND: WPF_Example/Custom/UI/TrayVisionView.xaml.cs
- FOUND: WPF_Example/Custom/UI/BottomVisionView.xaml.cs
- FOUND: WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND commit: 38bb1ae (Task 1)
- FOUND commit: 1b00964 (Task 2)
- FOUND commit: 9935b80 (Task 3)
