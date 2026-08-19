---
phase: quick-260819-fik
plan: 01
subsystem: WPF_Example/Custom/Sequence/Inspection
tags: [refactor, extract-method, halcon, simul-mode]
requires: []
provides:
  - "Action_FAIMeasurement.AcquireShotImage() — RunGrab 촬영 구역(28줄) 순수 추출"
  - "Action_FAIMeasurement.UpdateViewerCopy(HImage) — RunGrab 표시사본 처리(11줄) 순수 추출"
affects:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
tech-stack:
  added: []
  patterns: ["Extract Method (behavior-preserving, no ternary/no reformat)"]
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "swGrabTotal / ShotParam.SetImage / image.Dispose() / Step=Measure 는 RunGrab 잔류 (계약 보존)"
  - "parentSeqForView 이중방어 if/else 를 원형 그대로 UpdateViewerCopy 안으로 이동 (단순화 금지)"
  - "신규 파라미터 이름 image 유지 (헝가리언 접두 생략 — 바이트 동치 보존 목적, §G-5 예외)"
metrics:
  duration: "약 45분"
  completed: "2026-08-19"
---

# Phase quick-260819-fik Plan 01: RunGrab → AcquireShotImage/UpdateViewerCopy 추출 Summary

`Action_FAIMeasurement.RunGrab()`(HEAD 7708808, L379–433)에서 촬영 구역 28줄을
`AcquireShotImage()`로, 표시사본 처리 11줄을 `UpdateViewerCopy(HImage image)`로
순수 Extract Method 했다 — 토큰 변경 0건, 판정 로직/검사 흐름/저장 결과 무변경.

## 사용자 원문 기준 재확인

**"판정 로직·검사 흐름·저장 결과는 단 하나도 바뀌면 안 된다"**,
**"커밋 메시지 주장만 믿지 말고 커밋마다 동작 무변경을 코드로 직접 재확인"**.

아래 7개 절은 전부 실제 실행한 명령의 출력을 그대로 인용한다(요약/의역 없음).

---

## ① 바이트 동치 증명 (핵심)

| 추출 | 원본 범위(@7708808) | dedent | 신규 실행문 | diff 결과 |
|------|---------------------|--------|-------------|-----------|
| ① `AcquireShotImage` | L383–410 (28줄) | 4칸(16→12) | `return image;` 1줄 | **빈 출력** (diff exit 0) |
| ② `UpdateViewerCopy` | L415–425 (11줄) | 8칸(20→12) | 없음 | **빈 출력** (diff exit 0) |

실행 명령과 결과:
```
$ diff <(git show 7708808:$F | sed -n '383,410p' | sed 's/^[[:space:]]*//') \
       <(sed -n '405,432p' $F | sed 's/^[[:space:]]*//')
(빈 출력, exit 0)

$ diff <(git show 7708808:$F | sed -n '415,425p' | sed 's/^[[:space:]]*//') \
       <(sed -n '442,452p' $F | sed 's/^[[:space:]]*//')
(빈 출력, exit 0)
```
현재 파일에서 `AcquireShotImage()` 선언은 L404, 본문 L405–432(28줄) + `return image;`(L433) + `}`(L434).
`UpdateViewerCopy(HImage image)` 선언은 L441, 본문 L442–452(11줄) + `}`(L453).
선행공백을 제거하면 옮겨간 39줄이 BASE(7708808) 원본과 **1바이트도 다르지 않다** — 손으로 재입력하지 않고
`sed` 로 원본 조각을 잘라 그대로 붙여넣었기 때문에 토큰 변경이 물리적으로 불가능했다.

---

## ② 라인 멀티셋 대조 — 삭제된 실행줄 0

`comm -23 <(base 정규화·정렬) <(after 정규화·정렬)` — base 에는 있는데 after 에는 없는 줄(=삭제)을 구한다:
```
$ comm -23 "$SCR/fik-base-lines.txt" <(sed 's/^[[:space:]]*//; s/[[:space:]]*$//' $F | sort) | wc -l
0
```
**삭제된 줄 0줄.** 순수 추출이므로 추가만 있고 삭제는 없어야 하며, 실제로 그렇다.

추가된 줄(`comm -13`, after 에만 있는 줄) = **정확히 20줄**:
- 신규 메서드 선언 2줄 (`private HImage AcquireShotImage() {` / `private void UpdateViewerCopy(HImage image) {`)
- 신규 설명 주석(⚠ 포함) 11줄 — Task1 헤더 4줄 + Task2 헤더 4줄 + 빈 줄 1줄(파일 전체 diff 상 위치 이동으로 집계) + 세부 사유 줄
- 신규 호출 2줄 (`HImage image = AcquireShotImage();` / `UpdateViewerCopy(image);`)
- `return image;` 1줄
- 신규 메서드 닫는 `}` 2줄
- (파일 전체 wc -l: 1747 → 1767, 순증가 +20 = 위 합계와 일치)

파일 순증가 20줄 = (Task1 39삽입−28삭제=+11) + (Task2 20삽입−11삭제=+9) = **+20**. `git diff --stat` 실측과 일치.

---

## ③ ⭐2-빌드 표 (이번 작업 고유 리스크)

| 빌드 | 명령 | error CS | warning CS | CS0162 | 판정 |
|------|------|----------|-----------|--------|------|
| SIMUL (Debug\|x64) | `MSBuild ... -p:Configuration=Debug -p:Platform=x64` | 0 | 12 | 2 | PASS |
| 비-SIMUL (Release\|x64 + `-p:DefineConstants=TRACE`) | `MSBuild ... -p:Configuration=Release -p:Platform=x64 -p:DefineConstants=TRACE` | 0 | 10 | **0** | PASS |

착수 전 baseline(`fik-base-simul.log` / `fik-base-nosimul.log`) 과 Task1 이후(`fik-t1-*.log`) 와
Task2 이후(`fik-t2-*.log`) **3개 시점 모두 동일한 수치**(SIMUL error0/warning12, 비-SIMUL error0/warning10/CS0162=0)를
확인했다. 신규 `CS0219|CS0168|CS0177|CS0165|CS0103|CS1027|CS1028` 은 6개 로그 전부 0건.

**왜 2-빌드가 필요했는지**: 추출 구역(`AcquireShotImage`) 안에 `#if SIMUL_MODE`/`#else`/`#endif` 가
통째로 들어 있다. SIMUL(Debug) 만 빌드하면 `#else` 분기(비-SIMUL 전용 실기 grab 코드)가 아예 컴파일되지
않으므로, 거기서 발생할 수 있는 회귀(예: 메서드 경계를 잘못 넘겨 `bIsLiveGrabAttempt` 를 헬퍼 밖에 남기면
`#else` 블록에서 "정의되지 않음" CS0103 이 뜨는 식)를 SIMUL 빌드는 절대 잡지 못한다. 반대도 마찬가지다.

**왜 `-p:DefineConstants=TRACE` 가 필수였는지**: 워킹트리 `DatumMeasurement.csproj` 의 Release|x64
`DefineConstants` 에 로컬로 `SIMUL_MODE` 가 이미 섞여 있다(사용자 로컬 오염, 커밋 대상 아님). 그냥
`-p:Configuration=Release` 만 주고 빌드하면 SIMUL 경로가 그대로 컴파일되는 **가짜 비-SIMUL 검증**이 된다.
`-p:DefineConstants=TRACE` 로 명령줄에서 덮어써야 진짜 `#else` 분기가 컴파일되고, 그 증거가
`warning CS0162`(SIMUL 전용 도달불가 코드, `VirtualCamera.cs`) 가 **0건**이라는 사실이다 — 실측 결과
정확히 0건으로 나와 override 가 실제로 작동했음을 확인했다.

---

## ④ 잔류 결정 4건 — "왜 헬퍼로 옮기지 않았는가"

| 항목 | 잔류 위치 | 옮기면 무엇이 깨지는가 |
|------|-----------|------------------------|
| `var swGrabTotal = Stopwatch.StartNew();` | `RunGrab` (3번째 줄) | 헬퍼로 옮기면 tact 측정 구간이 "촬영만"으로 좁아져 `[SEQ] Grab` 로그의 "촬영 완료 (n.nn초)" 숫자가 달라진다 |
| `ShotParam.SetImage(image);` | `RunGrab` (8번째 줄) | 측정 소스(데이터 경로) 설정이다 — "ViewerCopy"(표시 전용) 이름의 헬퍼에 넣으면 책임 경계가 흐려지고, 조기 return 등으로 호출 안 될 위험이 생긴다 |
| `image.Dispose();` | `RunGrab` (10번째 줄) | 원본 주석 그대로 "누수 방지 — 조건과 무관하게 항상 수행" 계약이다. 헬퍼 안으로 들어가면 그 헬퍼에 향후 early-return 이 추가될 때 이 계약이 조용히 깨질 여지가 생긴다. 호출부에 남겨야 "항상 수행"이 코드로 눈에 보인다 |
| `Step = (int)EStep.Measure;` | `RunGrab` (바깥 `if` 밖, 16번째 줄) | 이미지가 없어도(즉 `ShotParam.HasImage`가 이미 true 라 바깥 if 를 안 타도) 항상 다음 스텝으로 진행하는 기존 lenient 동작이다. 위치를 옮기면 이 무조건성이 깨진다 |

---

## ⑤ RunGrab 최종 골격 (본문 정확히 17줄)

```
        private void RunGrab() {                                              [decl]
            if (ShotParam != null && !ShotParam.HasImage) {                   [1]
                //260818 hbk [SEQ] Grab 단계 tact 측정용 ...                   [2]
                var swGrabTotal = Stopwatch.StartNew();                       [3]
                HImage image = AcquireShotImage();                            [4]
                if (image != null) {                                          [5]
                    //260618 hbk Phase 54 ALIGN-01 ...                        [6]
                    //  레벨링 이미지회전 → 패턴매칭 ROI 좌표변환으로 대체. ...  [7]
                    ShotParam.SetImage(image); // 측정 소스(데이터 경로) ...    [8]
                    UpdateViewerCopy(image);                                  [9]
                    image.Dispose(); // 누수 방지 — 조건과 무관하게 항상 수행.  [10]
                }                                                             [11]
                //260818 hbk [SEQ] Grab 단계 요약 (tact 포함)                  [12]
                LogSeqStep("Grab", string.Format("검사 이미지 촬영 완료 ...     [13]
                    swGrabTotal.Elapsed.TotalSeconds));                       [14]
            }                                                                 [15]
            Step = (int)EStep.Measure;                                       [16]
        }                                                                     [17]
```

위치 고정 검증(automated verify [3])이 실측한 값과 완전히 일치:
`swGrabTotal`=3, `AcquireShotImage()`=4, `if(image!=null)`=5, `SetImage`=8, `UpdateViewerCopy(image)`=9,
`Dispose()`=10, `LogSeqStep`=13, `Elapsed`=14, **`Step = (int)EStep.Measure;`=16**(바깥 `if` 밖, 마지막 줄 앞),
마지막 줄(17)=`}`, 본문 안 `#if`/`#else`/`#endif` **0건**(조건부 컴파일은 `AcquireShotImage` 전용).

---

## ⑥ 원형 보존 항목

- **`parentSeqForView` 중복 방어를 지우지 않은 이유**: 원본은
  `InspectionSequence parentSeqForView; if (ShotParam != null) parentSeqForView = ...; else parentSeqForView = null;`
  형태다. `RunGrab` 의 바깥 `if (image != null)` 블록에 진입한 시점이면 이미 `ShotParam != null` 이 보장되므로
  이 null 체크는 이론상 항상 true 다. 그래도 "바깥에서 보장하니 단순화" 하면 순수 이동이 아니게 되므로
  헬퍼 `UpdateViewerCopy` 안에 **원형 그대로** 옮겼다 — 현재 L443–445 3줄, if/else 각 1건씩(`grep -c` 로 파일
  전역 카운트 1건씩 확인).
- **파라미터 이름을 `image` 로 둔 이유**: §G-5 예외 — 신규 식별자는 원칙상 헝가리언 접두(`h`/`p` 등)를 붙여야
  하지만, `RunGrab` 호출부의 지역변수 이름이 이미 `image` 다. 파라미터 이름을 바꾸면 헬퍼 본문 안에서
  `image` 를 참조하는 모든 토큰이 바뀌어(`image.CopyImage()` 등) 바이트 동치 증명이 깨진다. 그래서 이번
  1건에 한해 헝가리언 접두를 생략하고 `image` 를 유지했다.
- **보존 주석 5계열 삭제 0건** (현재 줄번호):
  - `260811 hbk plc-spec-260811-alignment` — L406(선언 옆), L425(하드웨어 에러 위) — `AcquireShotImage` 안 이동. (L860 은 `GrabOrLoadDatumImage` 소속, 무접촉)
  - `quick-260813-jnh` — L416 — `AcquireShotImage` 의 `#else` 분기 안. (L851 은 `GrabOrLoadDatumImage` 소속, 무접촉)
  - `260810 hbk quick-260810-egx` — L442(`UpdateViewerCopy` 본문 첫 줄). (L792/L896 은 다른 메서드 소속, 무접촉)
  - `260618 hbk Phase 54 ALIGN-01` — L385(`RunGrab` 잔류, `SetImage` 위). (L186/L335/L1326/L1727 은 다른 메서드 소속, 무접촉)
  - `260818 hbk [SEQ]` — L381, L404(tact)/L440(요약) — `RunGrab` 잔류.

---

## ⑦ 사용자 UAT 요청 (실기)

정적 증명(바이트 동치·라인 멀티셋·2-빌드)이 커버하지 못하는 것은 **실행 시 동작**뿐이다. 아래 3개만 확인 요청:

1. **SIMUL 로 1 사이클 검사** → `[SEQ] Grab` 로그의 "검사 이미지 촬영 완료 (n.nn초)" 가 이전과 같은 형태로 찍히는지
2. **표시 사본 경로** — 화면에 검사 이미지가 이전과 동일하게 뜨는지 (`DisableViewerDuringAutoInspect` ON/OFF 각 1회)
3. **실기: 카메라 grab 실패**를 인위적으로 만들었을 때 `$RESULT` 가 F 가 아니라 **E** 로 나가는지(하드웨어 에러 경로,
   `MarkCycleHardwareError()` 경유)

---

## 커밋

| # | 해시 | 메시지 | 변경 파일 |
|---|------|--------|-----------|
| 1 | `fbd5a2a` | refactor(260819-fik): RunGrab 촬영 구역을 AcquireShotImage 로 추출 (순수 이동, 동작 무변경) | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` |
| 2 | `a90ba05` | refactor(260819-fik): RunGrab 표시사본 처리를 UpdateViewerCopy 로 추출 (순수 이동, 동작 무변경) | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` |

`git diff --name-only 7708808 HEAD` = 1개 파일 (`Action_FAIMeasurement.cs`) — `DatumMeasurement.csproj` 는
두 커밋 모두에서 unstaged(` M`)로 그대로 남았다(무접촉 확인).

## Deviations from Plan

None - plan executed exactly as written. 모든 automated verify 블록(T1 STRUCTURE/BYTE-EQUIV/INVARIANT/HYGIENE,
T2 STRUCTURE/BYTE-EQUIV/RUNGRAB SHAPE/INVARIANT/HYGIENE)이 그대로 PASS 했고, §G-7 2-빌드 4회(baseline 1회 +
Task1 1회 + Task2 1회, 각각 SIMUL/비-SIMUL 2종)가 모두 동일한 수치(error 0/warning 12, error 0/warning 10/CS0162 0)로
일치했다. 코드 삼항 0건(기존 주석 L1318 1줄만), `=> ` 카운트 baseline 1 유지.

## Known Stubs

없음 — 이 plan 은 순수 리팩토링이며 신규 UI/데이터 경로를 추가하지 않았다.

## Threat Flags

없음 — 신규 네트워크 엔드포인트/인증 경로/파일 접근 패턴/스키마 변경 없음. 순수 코드 이동.

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (수정됨, 1767줄)
- FOUND: 커밋 `fbd5a2a` (`git log --oneline --all | grep fbd5a2a`)
- FOUND: 커밋 `a90ba05` (`git log --oneline --all | grep a90ba05`)
