---
quick_id: 260827-hdf
slug: phase-74-75-3
date: 2026-08-27
type: quick
status: in-progress
files_modified:
  - .planning/phases/74-pattern-model-brush-masking/74-02-PLAN.md
  - .planning/phases/74-pattern-model-brush-masking/74-05-PLAN.md
  - .planning/phases/75-align-corrected-image-evidence/75-01-PLAN.md
  - .planning/phases/75-align-corrected-image-evidence/75-03-PLAN.md
---

# Quick 260827-hdf — Phase 74/75 계획 검증 결함 3건 반영

Phase 74/75 plan 검증(이 세션, 코드베이스·HALCON 문서·실빌드 대조)에서 나온 결함을
**계획 문서에만** 반영한다.

## 🔴 절대 제약

**`WPF_Example/` 소스는 단 한 줄도 바꾸지 않는다.** 이 quick 은 `.planning/phases/` 의
마크다운 4개만 수정한다. `git status` 에 `WPF_Example/` 경로가 나오면 실패다
(단, 세션 시작 시점부터 이미 unstaged 였던 `WPF_Example/DatumMeasurement.csproj` 는 제외 — 건드리지 않고 그대로 둔다).

---

## B-1 (블로커) — 74-05-PLAN.md Task 2

**증상:** 계획은 브러시 패널을 `canvasToolbar` Border 안 Grid 의 `Grid.Row="1"` 에 넣으라고 한다.
그런데 그 Border 는 `WPF_Example/UI/ContentItem/MainView.xaml:144` 에서 **`Height="36"` 으로 고정**돼 있다.

```
<Border x:Name="canvasToolbar" Grid.Row="0" Background="#111827"
        Padding="8,4" Height="36">
```

Padding 8 을 빼면 내부 가용 높이 28px. Row 0 의 버튼(`Height="28"`)이 그걸 다 쓴다 →
**Row 1 은 0px**. 패널이 안 보이거나 넘쳐서 아래 HALCON HWND 영역을 침범한다(계획이 피하려던
airspace 문제 재발). 74-05 must_have("[브러시] 를 누르면 창 밖 패널이 열린다")와 74-06 UAT-E 가 그대로 실패한다.

계획은 "`RowDefinitions` 가 없다" 는 것까지는 정확히 확인했으나 `Height="36"` 을 놓쳤다.

**수정:** `Height="36"` → `MinHeight="36"`. 루트 Grid Row 0 가 `Height="Auto"` 라 자동으로 늘어난다.
루트 `Grid.Row` 번호는 하나도 안 바뀌므로 계획의 "재배치 0" 원칙이 유지된다.

**반영 대상:** `<interfaces>` 의 XAML 배치 설명, Task 2 `<action>` 단계, `<acceptance_criteria>`,
plan `<verification>`.

---

## M-1 (중간) — 74-02-PLAN.md Task 3 acceptance

**증상:** 통과 불가능한 검증 기준.

> `grep -n "private void ViewerHost_HMouseUp"` 로 얻은 줄 번호와 `grep -n "_isBrushStroking = false;"`
> 첫 줄 번호의 차이가 **5 이하**

Task 2 가 이미 `StartBrushMasking` / `StopBrushMasking`(파일 앞쪽 ~760줄)에 같은 문장을 넣는다.
따라서 "첫 줄 번호" 는 항상 그쪽이 되고 `ViewerHost_HMouseUp`(1359)과의 차이는 600 이상 →
**올바르게 구현해도 무조건 실패**한다.

인수인계에 "숫자를 맞추려 코드를 지우는 사고 이력" 이 적혀 있어 위험도가 높다.

**수정:** `awk '/private void ViewerHost_HMouseUp/,/^        }$/'` 범위 안에서 검사하는 기준으로 교체.

---

## M-2 (중간) — 75-01 / 75-03-PLAN.md

**증상:** `RunCorrectedRecheck` 가 `RunBottomAlign` 의 `finally` 에서 **동기** 실행된다 = PLC 응답이
나가기 전에 돈다. 그런데 내부에서

- `_matcher.TryFindPose` **4회** (1차 검출 2 + 재매칭 2)
- 전체 이미지 `AffineTransImage` 1회

를 수행한다. 검색 인자는 `FULL_SEARCH_LEN=99999`, `downsample=1.0` 전역 탐색이다.
CONTEXT D-75-01 의 "매칭 1회 추가 = 수십 ms" 추정보다 비용이 크다.

**핵심:** 1차 검출 2회는 **불필요**하다. `Run()` 이 이미 검출했고 그 결과가
`AlignResult.HasDetection` / `DetectedRow1` / `DetectedCol1` / `DetectedRow2` / `DetectedCol2` 에
그대로 들어 있으며, `RecordAlignVerify` 가 그 `res` 를 손에 쥐고 있다.

**수정:**
1. 75-01 에 이미 검출된 pose 를 받는 **오버로드**를 추가한다. `HasDetection == false` 면 기존
   자체검출 경로로 폴백한다(회귀 0).
2. 75-01 acceptance 의 `_matcher.TryFindPose == 4` 를 오버로드 구조에 맞게 조정한다.
3. 75-03 은 그 오버로드를 호출하도록 수정한다.
4. `RunCorrectedRecheck` **소요시간(ms)** 을 `[ALIGN_VERIFY]` 로그에 남긴다 —
   PLC 가 응답 22ms 뒤 다음 요청을 보내는 알려진 이슈(`project_plc_rapid_retest_start_reject`)
   때문에 실측 근거가 필요하다.

---

## Tasks

- [ ] T1 — 74-05-PLAN.md 에 B-1 반영 (interfaces + action + acceptance + verification)
- [ ] T2 — 74-02-PLAN.md Task 3 acceptance 기준 교체 (M-1)
- [ ] T3 — 75-01-PLAN.md 에 오버로드 + 소요시간 로그 + acceptance 조정 (M-2)
- [ ] T4 — 75-03-PLAN.md 가 오버로드를 쓰도록 수정 (M-2)
- [ ] T5 — `WPF_Example/` 무변경 확인 + 커밋

## Acceptance

- `git status --porcelain WPF_Example/` 출력이 세션 시작 시점과 동일(`M DatumMeasurement.csproj` 한 줄뿐)
- 74-05-PLAN.md 에 `MinHeight="36"` 이 등장하고, Task 2 acceptance 에 그 검증이 있다
- 74-02-PLAN.md 에서 `첫 줄 번호` 기반 기준이 사라지고 `awk` 범위 기준으로 바뀌었다
- 75-01-PLAN.md 에 pose 를 받는 오버로드 시그니처와 소요시간 로그 지시가 있다
- 75-03-PLAN.md 가 그 오버로드를 호출한다
