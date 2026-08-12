---
phase: quick-260812-fye
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/SequenceHandler.cs
  - WPF_Example/Sequence/SequenceHandler.cs
autonomous: true
requirements: [LOG-UX-01]

must_haves:
  truths:
    - "[A] 브리핑이 지목한 4곳(Bottom PASS / Tray PASS / RESET 수신 / RESET 클린슬레이트)의 로그 문자열에서 `//YYMMDD hbk` 개발자 태그가 사라진다 — 운영자가 로그창에서 알아볼 수 없는 커밋태그를 더 이상 보지 않는다"
    - "[A-EXT] 같은 5개 파일 안의 '같은 버그' 25곳(문자열 내부 태그) 전부에서 태그가 사라진다 — 계획 중 grep 으로 발견된 21곳 추가. 같은 로그 계열([ALIGN_TEST] PASS 는 깨끗한데 [ALIGN_TEST] 실패는 태그가 남는) 반쪽 정리를 만들지 않는다"
    - "[A-경계] 문자열 밖의 '진짜 코드 주석' `//YYMMDD hbk` 는 단 하나도 지워지지 않는다 — 특히 `Custom/SystemHandler.cs` 의 `//260626 hbk 로그 후 off`, `InspectionSequence.cs` 의 `szFaiName = \"FAI\"; //260629 hbk ...` 는 그대로 남는다"
    - "[B] 개발 진단 전용 `Logging.PrintLog` 호출 9개가 통째로 사라진다 — InspectionSequence 3([ALIGN]×2/[ALIGN2]×1) + Action_FAIMeasurement 2([FAI CrossZ IMG]/[QueueFaiCapture]) + Sequence/SequenceHandler 3([STARTUP-WHITE] f1/f2/f3) + Custom/SystemHandler 1([V1Scope] StartAll 폴백)"
    - "[B] 삭제된 로그에만 쓰이던 지역변수/계측 스캐폴딩이 함께 사라져 신규 `warning CS` 0 을 유지한다 — `datumDetectRotDeg`, `szShotNameForLog`, `crossZCapturedRoleLabel/MeasName/Z`, `swTotal/swStage/msPrep/msOrigin/msSnapshot/msCaptureEnqueue`"
    - "[B-경계] 다른 곳에서도 쓰이는 심볼은 남는다 — `CROSS_Z_ROLE_SUFFIX_A`(746/780/1378/1390/1409 사용), `GetExecutionZIndex()`, `TakeCrossZImageCopy()` 는 무변경"
    - "[B-경계] 운영자에게 의미 있는 실패 로그는 살아남는다 — `InspectionSequence.cs` 의 `[ALIGN2] ... 패턴2 매칭 실패 → 단일 패턴 θ 폴백` (ELogType.Error) 은 삭제 대상이 아니다"
    - "[B-경계] `[STARTUP-WHITE]` 삭제는 `WPF_Example/Sequence/SequenceHandler.cs` 의 (f1)(f2)(f3) 3개뿐이다 — `App.xaml.cs` 의 (a)(e), `MainWindow.xaml.cs` 의 (b)(c)(d) 는 이번 범위 밖이라 diff 에 등장하지 않는다"
    - "[C] 3개 로그 문구가 초보 운영자도 읽을 수 있는 한글로 바뀐다 — `[RUN-GATE] blocked:` → 한글, 영어 원문 `Calibration test requests are blocked...` → 한글, `라이브 이미지로 폴백(회귀 0)` → `(회귀 0)` 제거"
    - "[C] 문구만 바뀌고 판정 로직은 완전 무변경 — if/else 분기, 반환값, `string.Format` 인자 개수와 순서가 편집 전과 동일하다"
    - "[D] `[임시 수동Z트리거]` 로그 3줄은 diff 에 전혀 나타나지 않는다 (완전 무변경)"
    - "[D] `[ALIGN_CALIB]` START/STEP/END/ABORT 와 `[MainRun] TestResultPacket.Target empty` 도 diff 에 나타나지 않는다"
    - "`WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` 는 1바이트도 변하지 않는다 — 작업 전후 `git diff` 해시가 `73a89c282724fedf25b7dcf8919b09251578d789` 로 동일"
    - "Debug/x64 컴파일이 신규 `error CS` 0 / 신규 `warning CS` 0 으로 통과한다"
  artifacts:
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "[A] 문자열 내 태그 19곳 제거(ALIGN_TEST 15 + RESET 3 + V1Scope 1) / [B] [V1Scope] StartAll 폴백 로그 1개 삭제 / [D] 임시 수동Z트리거·ALIGN_CALIB·MainRun 무변경"
      contains: "임시 수동Z트리거"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "[A] 문자열 내 태그 6곳 제거(CycleLightOff 2, PREP CrossZ 1, PREP Shot-not-found 1, V1Scope Datum 1, V1Cycle 1) / [B] [ALIGN]·[ALIGN2] 진단 PrintLog 3개 + datumDetectRotDeg 삭제"
      contains: "패턴2 매칭 실패"
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "[B] [FAI CrossZ IMG] + [QueueFaiCapture] 로그 2개와 전용 지역변수/Stopwatch 계측 삭제 / [C] '(회귀 0)' 제거"
      contains: "라이브 이미지로 폴백"
    - path: "WPF_Example/Custom/Sequence/SequenceHandler.cs"
      provides: "[C] [RUN-GATE] blocked 로그를 한글로 재작성(포맷 인자 2개 순서 유지)"
      contains: "검사 시작 차단"
    - path: "WPF_Example/Sequence/SequenceHandler.cs"
      provides: "[B] [STARTUP-WHITE] (f1)(f2)(f3) 3개 삭제 / [C] Calibration 영문 로그 한글화"
      contains: "요청 거부"
  key_links:
    - from: "[A-1] Custom/SystemHandler.cs — Bottom Align PASS 로그"
      to: "태그 ` //260626 hbk` 제거"
      via: "문자열 끝 태그만 삭제, 포맷 인자 4개 무변경"
      pattern: "\\[ALIGN_TEST\\] Bottom slot=\\{0\\} PASS off=\\(\\{1:0\\.000\\},\\{2:0\\.000\\}\\) theta=\\{3:0\\.000\\}"
    - from: "[A-2] Custom/SystemHandler.cs — Tray Align PASS 로그"
      to: "태그 ` //260630 hbk` 제거"
      via: "문자열 끝 태그만 삭제, 포맷 인자 3개 무변경"
      pattern: "\\[ALIGN_TEST\\] Tray PASS off=\\(\\{0:0\\.000\\},\\{1:0\\.000\\}\\) theta=\\{2:0\\.000\\}"
    - from: "[A-3] Custom/SystemHandler.cs — $RESET 수신 로그"
      to: "태그 ` //260807 hbk` 제거"
      via: "문자열 끝 태그만 삭제, 포맷 인자 2개 무변경"
      pattern: "\\[RESET\\] site=\\{0\\} 수신 — _lastPrepZIndex=0, 시퀀스 리셋 결과=\\{1\\}"
    - from: "[A-4] Custom/SystemHandler.cs — $RESET 클린 슬레이트 로그"
      to: "태그 ` //260807 hbk` 제거"
      via: "문자열 끝 태그만 삭제, 포맷 인자 1개 무변경"
      pattern: "\\[RESET\\] Seq=\\{0\\} 클린 슬레이트 완료"
    - from: "[A-경계] Custom/SystemHandler.cs — ApplyCoaxLightForSlot"
      to: "문자열 내 태그만 제거, 같은 줄 뒤쪽 진짜 주석은 보존"
      via: "`\"...예외: {0} //260626 hbk\", ex.Message);   //260626 hbk 로그 후 off` 에서 앞쪽 하나만 삭제"
      pattern: "//260626 hbk 로그 후 off"
    - from: "[B-1] InspectionSequence.cs — Phase 54 ALIGN-01 진단 좌표 덤프"
      to: "PrintLog 호출 3개 전부 삭제"
      via: "`[ALIGN] ` cur/d/patAngDeg 덤프, `[ALIGN2] ` p2cur/baseline 덤프, `[ALIGN] ` datumDetectAngleDeg 덤프"
      pattern: "\\[ALIGN\\] \" \\+ \\(datum\\.DatumName"
    - from: "[B-2] Action_FAIMeasurement.cs — 표시 이미지 교체 로그"
      to: "PrintLog + szShotNameForLog + crossZCaptured* 3개 삭제"
      via: "`[FAI CrossZ IMG] Shot=... Meas=... Role=... Z=...` 이 유일 소비자"
      pattern: "\\[FAI CrossZ IMG\\]"
    - from: "[B-3] Action_FAIMeasurement.cs — QueueFaiCapture 성능 계측"
      to: "PrintLog + Stopwatch 계측 스캐폴딩 + try/finally 껍데기 삭제"
      via: "`[QueueFaiCapture] fai={0} prep={1}ms ... total={5}ms` 이 finally 의 유일 내용"
      pattern: "\\[QueueFaiCapture\\] fai=\\{0\\} prep=\\{1\\}ms"
    - from: "[B-4] Sequence/SequenceHandler.cs — Phase 43.2 기동 지연 계측"
      to: "PrintLog 3개 + 바로 위 설명 주석 3줄 삭제"
      via: "`[STARTUP-WHITE] (f1) INI parse done` / `(f2) OnRecipeChanged done` / `(f3) ExecOnLoad done`"
      pattern: "\\[STARTUP-WHITE\\] \\(f[123]\\)"
    - from: "[B-5] Custom/SystemHandler.cs — z=0 StartAll 폴백 진단"
      to: "PrintLog + string.Format 통째로 삭제"
      via: "`[V1Scope] Seq={0} z=0: DatumConfigs 비어있음(또는 트리거 미해결) — StartAll 폴백.`"
      pattern: "z=0: DatumConfigs 비어있음"
    - from: "[C-1] Custom/Sequence/SequenceHandler.cs — RUN 게이트 차단 로그"
      to: "`[검사 시작 차단] {0} 시작 못 함 — {1}이(가) 이미 검사 중`"
      via: "인자 = ResolveSequenceName(eTargetSeqId), sBlockingSeqName — 2개, 순서 유지"
      pattern: "\\[RUN-GATE\\] blocked: target=\\{0\\}, busy=\\{1\\}"
    - from: "[C-2] Sequence/SequenceHandler.cs — 캘리브레이션 요청 거부"
      to: "`[요청 거부] 캘리브레이션 요청은 자동 검사 흐름에서 처리하지 않음`"
      via: "포맷 인자 0개, `return false;` 무변경"
      pattern: "Calibration test requests are blocked from automatic sequence execution\\."
    - from: "[C-3] Action_FAIMeasurement.cs — 교시 이미지 폴백 안내"
      to: "`(회귀 0)` 만 제거, 나머지 문구 유지"
      via: "`[FAI CrossZ] role \" + szRoleLabel + \" 교시 이미지 미설정 — 라이브 이미지로 폴백`"
      pattern: "라이브 이미지로 폴백\\(회귀 0\\)"
    - from: "[D] Custom/SystemHandler.cs — 임시 수동Z트리거"
      to: "diff 무등장"
      via: "디버그 전용 기능의 로그 — 이번 '사용자용 로그 정리' 취지와 무관"
      pattern: "\\[임시 수동Z트리거\\]"
---

<objective>
운영자(초보자 기준)가 읽는 **시퀀스 실행 경로 Trace 로그**를 정리한다.

지금 로그창에는 (a) 개발 중 남긴 커밋태그 `//260626 hbk` 가 **로그 문자열 안에 박혀서 그대로 출력**되고,
(b) 이미 끝난 조사용 진단 로그(원시 row/col 덤프, 기동 타이밍, 성능 브레이크다운)가 계속 쏟아지고,
(c) 영어 원문·개발자 용어(`(회귀 0)`, `blocked: target=`)가 섞여 있다.

이 세 가지를 걷어내고, **운영자가 시퀀스 상태를 이해하는 데 실제로 필요한 로그만 쉬운 말로** 남긴다.

**Output:**
- A그룹 — 문자열 내부 `//YYMMDD hbk` 태그 **25곳** 제거 (2곳은 B그룹 삭제로 함께 소멸 → 총 27개 지점)
- B그룹 — 개발 진단 전용 `Logging.PrintLog` **9개** 삭제 + 그 로그에만 쓰이던 지역변수/계측 스캐폴딩 정리
- C그룹 — 로그 문구 **3곳** 한글화·단순화 (판정 로직 무변경)
- D그룹 — `[임시 수동Z트리거]` / `[ALIGN_CALIB]` / `[MainRun]` **완전 무변경**
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**코딩 규칙 (이 프로젝트 상시 규칙 — 위반 시 리뷰 반려):**
- 삼항연산자 `?:` **금지** → 반드시 `if / else` (이번 작업은 순삭제/문자열 편집이라 신규 삼항이 생길 일이 없다)
- C# 7.2 (`nullable`, `switch expression`, `record` 등 8.0+ 문법 금지)
- 각 파일의 **기존 브레이스 스타일 유지**. `Action_FAIMeasurement.cs` / `Custom/Sequence/SequenceHandler.cs` / `Sequence/SequenceHandler.cs` = K&R(`{` 같은 줄),
  `Custom/SystemHandler.cs` / `InspectionSequence.cs` = 혼재(주변 줄을 그대로 따라간다)
- 신규/수정 주석은 `quick-260812:` 접두 + **짧게**, 비자명한 "왜"만.
  `//YYMMDD hbk` 날짜 주석 규칙은 2026-06-11 부로 폐기됐다 — 새로 달지 말 것.
- **A그룹은 "주석 이사"가 아니라 "삭제"다.** 문자열에서 태그를 떼면서 같은 내용을 코드 주석으로 옮겨 적지 않는다.
  (대부분 이미 바로 위/아래에 진짜 주석으로 같은 맥락이 있다.)

***

## ⚠ 계획 중 발견 — 브리핑 대비 확장된 부분 (A-EXT)

브리핑은 A그룹을 **4곳**(+B로 소멸하는 1곳)으로 열거했다. 계획 단계 grep 결과, **같은 5개 파일 안에
같은 버그(문자열 내부 태그)가 총 27개 지점**에 있다. 4곳만 고치면 결과가 이렇게 된다:

```
[ALIGN_TEST] Tray PASS off=(1.234,5.678) theta=0.012            ← 깨끗 (수정됨)
[ALIGN_TEST] Tray 미티칭 — 모델 없음 NG //260630 hbk            ← 더러움 (미수정)
[ALIGN_TEST] Tray grab 실패(null) — NG //260630 hbk             ← 더러움 (미수정)
```

같은 로그 계열에서 성공 줄만 깨끗하고 실패 줄에 개발자 태그가 남는 건 "운영자가 보기 편하게" 라는
목적을 정면으로 배반한다. 그래서 **A-EXT = 같은 5개 파일 안의 나머지 21곳도 함께 제거**한다.

- 브리핑 4곳(A-1~A-4)은 **잠긴 결정 — 반드시 수정**한다.
- A-EXT 21곳은 **동일 버그·동일 파일·순수 문자열 접미부 삭제**로 로직 위험 0 이다.
- 만약 개발자가 A-EXT 를 원치 않으면 A-EXT 만 떼어내도 B/C/D 는 전혀 영향받지 않는다(독립적).

### 문자열 내부 태그 전체 목록 (27지점, 편집 전 라인번호 — 밀릴 수 있으니 문자열 앵커 사용)

| 파일 | 라인 | 로그 마커 | 처리 |
|------|------|-----------|------|
| Custom/SystemHandler.cs | 294 | `[V1Scope] Seq={0} z=0:` | **B로 통째 삭제** (태그 동반 소멸) |
| Custom/SystemHandler.cs | 335 | `[V1Scope] ZIndex={0} 매칭 Shot 0건` | A-EXT |
| Custom/SystemHandler.cs | 383,407,417,433,443 | `[ALIGN_TEST]` Bottom 계열 | A-EXT (5) |
| Custom/SystemHandler.cs | **451** | `[ALIGN_TEST] Bottom slot={0} PASS` | **A-1 (브리핑)** |
| Custom/SystemHandler.cs | 476 | `[ALIGN_TEST] RunBottomAlign 예외` | A-EXT |
| Custom/SystemHandler.cs | 490,498,511,519 | `[ALIGN_TEST]` Tray 계열 | A-EXT (4) |
| Custom/SystemHandler.cs | **526** | `[ALIGN_TEST] Tray PASS` | **A-2 (브리핑)** |
| Custom/SystemHandler.cs | 547,581,614 | `[ALIGN_TEST]` 예외 계열 | A-EXT (3) |
| Custom/SystemHandler.cs | **888** | `[RESET] site={0} 수신` | **A-3 (브리핑)** |
| Custom/SystemHandler.cs | 1007 | `[RESET] Seq={0} 실행 중` — 태그 **2개** 연속 | A-EXT (둘 다 제거) |
| Custom/SystemHandler.cs | **1014** | `[RESET] Seq={0} 클린 슬레이트 완료` | **A-4 (브리핑)** |
| Action_FAIMeasurement.cs | 513 | `[FAI CrossZ IMG]` | **B로 통째 삭제** (태그 동반 소멸) |
| InspectionSequence.cs | 389, 891 | `[CycleLightOff]` | A-EXT (2) |
| InspectionSequence.cs | 657 | `[PREP CrossZ]` | A-EXT |
| InspectionSequence.cs | 757 | `[PREP] Shot not found` | A-EXT |
| InspectionSequence.cs | 1448 | `[V1Scope] Datum '...'` | A-EXT |
| InspectionSequence.cs | 1874 | `[V1Cycle] BuildScopedResponse 빈 결과` | A-EXT |

### 🚫 태그처럼 보이지만 **진짜 코드 주석** — 절대 건드리지 않는다

문자열 밖에 있는 `//YYMMDD hbk` 주석은 이 5개 파일에 수십 개 있고 **전부 보존**한다. 특히 헷갈리는 것:

```csharp
// Custom/SystemHandler.cs:614 — 한 줄에 둘 다 있다. 앞쪽(문자열 안)만 지운다.
    "[ALIGN_TEST] ApplyCoaxLightForSlot 예외: {0} //260626 hbk", ex.Message);   //260626 hbk 로그 후 off
//                                            ^^^^^^^^^^^^^ 삭제                ^^^^^^^^^^^^^^^^^^^^^^^^ 보존

// Custom/SystemHandler.cs:368 — 문자열 밖. 보존.
    bool bIsBottom = packet.AlignTarget == "BOTTOM"; //260626 hbk BOTTOM 전용 슬롯 라우팅

// InspectionSequence.cs:1803 — 문자열 밖. 보존.
    szFaiName = "FAI"; //260629 hbk FAIName null/빈 문자열 → "FAI" 폴백

// InspectionSequence.cs:1622,1645 / Action_FAIMeasurement.cs:1002,1014 — 전부 문자열 밖. 보존.
```

> **판별 기준:** 태그가 `Logging.PrintLog(...)` 의 **포맷 문자열 리터럴 안**에 있으면 삭제,
> 세미콜론/닫는 괄호 **뒤**에 있으면 코드 주석이므로 보존.
> 정규식만으로는 구분이 안 된다(주석 안에도 `"` 가 자주 등장) — **반드시 눈으로 확인**할 것.

***

## 🚫 절대 건드리면 안 되는 것

1. **`WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`**
   - 사용자의 **별건** 미커밋 실험. 이번 범위 완전히 밖. 읽지도 고치지도 말고 커밋에도 넣지 말 것.
   - 작업 시작 baseline: `git diff --numstat` = `6  2`,
     `git diff -- <파일> | git hash-object --stdin` = `73a89c282724fedf25b7dcf8919b09251578d789`.
     **작업 후에도 동일해야 한다.**

2. **`[임시 수동Z트리거]` (Custom/SystemHandler.cs 904/916/925 부근, 3줄)** — D그룹.
   "임시"라고 명시된 **디버그 전용 기능**의 로그다. 사용자용 로그 정리 취지와 무관 → 완전 무변경.

3. **`[ALIGN_CALIB]` START/STEP/END/ABORT (Custom/SystemHandler.cs 683~820)** — 이미 쉬운 문구. 무변경.

4. **`[MainRun] TestResultPacket.Target empty ...` (Custom/SystemHandler.cs 31~33)** — 무변경.

5. **`[STARTUP-WHITE]` 중 이번 범위 밖인 것** — `App.xaml.cs` 의 `(a)`/`(e)` + 라벨 없는 1개(L41, `catch` 블록의 스플래시 실패 로그, `ELogType.Error`),
   `MainWindow.xaml.cs` 의 `(b)`/`(c)`/`(d)`, base `WPF_Example/SystemHandler.cs`(Custom 아님)의 `(f)`/`(g)`(L248/L262, `LoadRecipe` 레시피 로드 시작/완료 계측).
   ⚠ 전체 리포지토리에서 `grep STARTUP-WHITE` 하면 총 11개가 나온다(plan-checker 재검증, 2026-08-12). **삭제 대상은 `WPF_Example/Sequence/SequenceHandler.cs` 의 (f1)(f2)(f3) 3개뿐이고, 나머지 8개(App.xaml.cs 3 + MainWindow.xaml.cs 3 + base SystemHandler.cs 2)는 전부 범위 밖이라 손대지 않는다.**

6. **`[ALIGN2] ... 패턴2 매칭 실패 → 단일 패턴 θ 폴백` (InspectionSequence.cs 2302)**
   — `ELogType.Error`. 운영자에게 의미 있는 실패 경고다. B그룹 삭제 대상 3개에 **포함되지 않는다.**

<interfaces>
<!-- 실행자가 코드베이스를 탐색하지 않아도 되도록 편집 대상 원문을 그대로 옮겨둔다. -->
<!-- 라인번호는 편집 전 기준이며 편집 중 밀린다 — Edit 은 반드시 문자열 앵커로 지정할 것. -->

### B그룹 삭제 대상 원문

```csharp
// ── InspectionSequence.cs L2269~2275 (주석 1줄 + PrintLog 6줄) 전체 삭제
            //260618 hbk Phase 54 ALIGN-01 진단 로그 — 매칭/θ 수치 확인용 (CO-54-04)
            Logging.PrintLog((int)ELogType.Trace, "[ALIGN] " + (datum.DatumName ?? "")
                + " cur=(" + curRow.ToString("F1") + "," + curCol.ToString("F1") + ")"
                + " d=(" + dRow.ToString("F1") + "," + dCol.ToString("F1") + ")"
                + " patAngDeg=" + curAngleDeg.ToString("F3") + " refPatAngDeg=" + datum.RefMatchAngleDeg.ToString("F3")
                + " thetaDeg=" + (thetaRad * 180.0 / System.Math.PI).ToString("F3") + " src=pattern"
                + " score=" + curScore.ToString("F3") + " angleExtentDeg=" + datum.PatternAngleExtentDeg.ToString("F1"));
// → 위 지역변수(dRow/dCol/thetaRad/curRow/curCol/curAngleDeg/curScore)는 아래 로직에서 계속 쓴다. 변수는 남긴다.

// ── InspectionSequence.cs L2293~2298 (PrintLog 6줄) 삭제. 감싸는 if 블록/refBaseline/curBaseline/thetaRad 대입은 유지
                    Logging.PrintLog((int)ELogType.Trace, "[ALIGN2] " + (datum.DatumName ?? "")
                        + " p2cur=(" + cur2Row.ToString("F1") + "," + cur2Col.ToString("F1") + ")"
                        + " refBaseDeg=" + (refBaseline * 180.0 / System.Math.PI).ToString("F3")
                        + " curBaseDeg=" + (curBaseline * 180.0 / System.Math.PI).ToString("F3")
                        + " thetaDeg=" + (thetaRad * 180.0 / System.Math.PI).ToString("F3")
                        + " score2=" + cur2Score.ToString("F3") + " (baseline θ)");

// ── InspectionSequence.cs L2350~2358 (주석 3줄 + 지역변수 1줄 + PrintLog 5줄) 전체 삭제
            //260618 hbk Phase 54 ALIGN-01 carry-over#1 확증로그: datum 검출각(수평 결합선) 회전분 vs 패턴 θ.
            //  strip θ회전 적용 후 datumDetectRotDeg 가 patternThetaDeg 로 수렴(편차~0)하면 datum 검출각 정확 = 먼 측정점 정상.
            //  축정렬 strip 가설은 0.1-0.2° 편차. UAT 1회로 메커니즘 확증.
            double datumDetectRotDeg = datum.DetectedAngleDeg - (datum.RefAngleRad * 180.0 / System.Math.PI);
            Logging.PrintLog((int)ELogType.Trace, "[ALIGN] " + datumKey
                + " datumDetectAngleDeg=" + datum.DetectedAngleDeg.ToString("F3")
                + " datumDetectRotDeg=" + datumDetectRotDeg.ToString("F3")
                + " vs patternThetaDeg=" + (thetaRad * 180.0 / System.Math.PI).ToString("F3")
                + " (strip θ-rot applied)");
// → datumKey 는 바로 아래 lock 블록 `_datumTransforms[datumKey] = alignRigid;` 에서 쓴다. 남긴다.

// ── Sequence/SequenceHandler.cs L162~171 — 주석+PrintLog 3쌍 삭제 (사이 로직 2줄은 유지)
            //260615 hbk Phase 43.2: [STARTUP-WHITE] (f1) — INI 파싱 완료 시점 (A 구간 종료)
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (f1) INI parse done: {0} ms", ReringProject.App.StartupWatch.ElapsedMilliseconds);

            OnRecipeChanged?.Invoke(this, new RecipeChangedEventArgs(name));    // ← 유지
            //260615 hbk Phase 43.2: [STARTUP-WHITE] (f2) — OnRecipeChanged 완료 시점 (B 구간 종료)
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (f2) OnRecipeChanged done: {0} ms", ReringProject.App.StartupWatch.ElapsedMilliseconds);

            if (result) ExecOnLoad(name);                                       // ← 유지
            //260615 hbk Phase 43.2: [STARTUP-WHITE] (f3) — ExecOnLoad 완료 시점 (C 구간 종료)
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (f3) ExecOnLoad done: {0} ms", ReringProject.App.StartupWatch.ElapsedMilliseconds);

// ── Custom/SystemHandler.cs L293~294 (PrintLog 2줄) 삭제. 바로 위 L281~286 설명 주석 중
//     "…Trace 로그만 남기고 StartAll 로 안전 폴백한다…" 문구도 사실과 달라지므로 그 문장만 손질(아래 action 참조)
                Logging.PrintLog((int)ELogType.Trace,
                    string.Format("[V1Scope] Seq={0} z=0: DatumConfigs 비어있음(또는 트리거 미해결) — StartAll 폴백. //260722 hbk", seq.Name));
                return seq.StartAll(packet);   // ← 유지
```

### Action_FAIMeasurement.cs — [FAI CrossZ IMG] 관련 (4지점)

```csharp
// L349~351 — 선언 3줄 삭제 (주석에 "표시 이미지 교체 로그용" 이라고 용도가 명시돼 있다)
                                    string crossZCapturedRoleLabel = null; // 표시 이미지 교체 로그용 — role(A/B) 표시
                                    string crossZCapturedMeasName = null;  // 표시 이미지 교체 로그용 — 측정명
                                    int crossZCapturedZ = UNSET_ZINDEX;    // 표시 이미지 교체 로그용 — 캡처 당시 z

// L424~431 — 대입 5줄 + 껍데기만 남는 if 삭제. 바깥 if 와 TakeCrossZImageCopy 대입은 유지
                                                crossZRoleImage = parentSeq2.TakeCrossZImageCopy(szCapturedRoleKey);   // ← 유지
                                                if (crossZRoleImage != null)
                                                {
                                                    if (szCapturedRoleKey.EndsWith(CROSS_Z_ROLE_SUFFIX_A, StringComparison.Ordinal)) crossZCapturedRoleLabel = "A";
                                                    else crossZCapturedRoleLabel = "B";
                                                    crossZCapturedMeasName = meas.MeasurementName;
                                                    if (crossZCapturedMeasName == null) crossZCapturedMeasName = meas.TypeName;
                                                    crossZCapturedZ = parentSeq2.GetExecutionZIndex();
                                                }
// → `if (crossZRoleImage != null) { }` 가 통째로 빈 껍데기가 되므로 그 if 도 삭제한다.
// → CROSS_Z_ROLE_SUFFIX_A(L46 const) 는 L746/780/1378/1390/1409 에서 계속 쓴다. 절대 삭제 금지.

// L512~513 — 지역변수 + PrintLog 2줄 삭제. 위아래 표시 이미지 교체 로직(509~511)은 유지
                                                string szShotNameForLog = ShotParam != null ? ShotParam.ShotName : "";
                                                Logging.PrintLog((int)ELogType.Trace, "[FAI CrossZ IMG] Shot=" + szShotNameForLog + ", Meas=" + crossZCapturedMeasName + ", Role=" + crossZCapturedRoleLabel + ", Z=" + crossZCapturedZ + " //260729 hbk quick-fix(260729-hwb)");
```

### Action_FAIMeasurement.cs — QueueFaiCapture 계측 (Task 3, 유일한 구조 변경)

```csharp
private void QueueFaiCapture(FAIConfig fai, ...) {                 // L962  유지
    if (fai == null) return;                                        // L963  유지
    // 260810 hbk quick-debug(capture-render-per-fai-slow) 계측: …   // L964~970  삭제 (계측 설명 전용)
    var swTotal = Stopwatch.StartNew();                             // L971  삭제
    var swStage = new Stopwatch();                                  // L972  삭제
    long msPrep = -1, msOrigin = -1, msSnapshot = -1, msCaptureEnqueue = -1;  // L973  삭제
    try {                                                           // L974  삭제
        …                                                           // L975~1053  본문 유지 + 4칸 내어쓰기(dedent)
        swStage.Restart();                                          // L978  삭제
        msPrep = swStage.ElapsedMilliseconds;                       // L1006 삭제
        swStage.Restart();                                          // L1008 삭제
        msOrigin = swStage.ElapsedMilliseconds;                     // L1028 삭제
        if (saver == null || sharedSrc == null) return;             // L1030 유지 (try 제거해도 동작 동일)
        swStage.Restart();                                          // L1034 삭제
        msSnapshot = swStage.ElapsedMilliseconds;                   // L1038 삭제
        swStage.Restart();                                          // L1040 삭제
        msCaptureEnqueue = swStage.ElapsedMilliseconds;             // L1052 삭제
    }                                                               // L1053 삭제
    finally {                                                       // L1054 삭제
        // reused/setup/dispBase/dump 는 … (주석 4줄)               // L1055~1058 삭제
        Logging.PrintLog((int)ELogType.Trace,                       // L1059~1061 삭제
            "[QueueFaiCapture] fai={0} prep={1}ms origin={2}ms snapshot={3}ms captureEnqueue={4}ms total={5}ms",
            fai.FAIName, msPrep, msOrigin, msSnapshot, msCaptureEnqueue, swTotal.ElapsedMilliseconds);
    }                                                               // L1062 삭제
}                                                                   // L1063 유지
```

> `finally` 의 유일한 내용이 이 로그다. `catch` 는 없다 → try/finally 를 제거해도 예외 전파·return 동작이 100% 동일하다.
> `using System.Diagnostics;`(L3) 는 **그대로 둔다** (미사용 using 은 컴파일 경고를 내지 않는다).

### C그룹 원문 → 대체

```csharp
// [C-1] Custom/Sequence/SequenceHandler.cs L69~71
            //260805 hbk Phase 69: 차단 사실을 남겨야 "왜 안 눌렸는지" 사후 추적이 된다(사용자 클릭 빈도라 로그 폭주 없음).  ← 주석 유지
            Logging.PrintLog((int)ELogType.Trace, "[RUN-GATE] blocked: target={0}, busy={1}",
                ResolveSequenceName(eTargetSeqId), sBlockingSeqName);          ← 인자줄 무변경
// 새 문자열: "[검사 시작 차단] {0} 시작 못 함 — {1}이(가) 이미 검사 중"

// [C-2] Sequence/SequenceHandler.cs L346
                Logging.PrintLog((int)ELogType.Trace, "Calibration test requests are blocked from automatic sequence execution.");
// 새 문자열: "[요청 거부] 캘리브레이션 요청은 자동 검사 흐름에서 처리하지 않음"
//  → 감싸는 `if ((ETestType)packet.TestType == ETestType.Calibration)` 과 `return false;` 무변경

// [C-3] Action_FAIMeasurement.cs L1334
                Logging.PrintLog((int)ELogType.Trace, "[FAI CrossZ] role " + szRoleLabel + " 교시 이미지 미설정 — 라이브 이미지로 폴백(회귀 0)");
// 새 문자열: … + " 교시 이미지 미설정 — 라이브 이미지로 폴백"      ← "(회귀 0)" 만 제거
```

### 빌드 (산출물 잠김 대비)

`bin/x64/Debug/DatumMeasurement.exe` 가 실행 중이면 잠긴다. **프로세스를 절대 죽이지 않는다.**
스크래치 OutDir 로 컴파일만 검증한다:

```
"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
  "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 \
  //p:OutputPath="$TEMP/gsd-fye-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-fye-scratch/obj/" \
  //v:minimal //nologo
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: A그룹 — 로그 문자열 안에 박힌 개발자 커밋태그 25곳 제거</name>
  <files>WPF_Example/Custom/SystemHandler.cs, WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
**하는 일 하나뿐:** `Logging.PrintLog(...)` 의 **포맷 문자열 리터럴 안**에 들어있는 ` //YYMMDD hbk...` 텍스트를 지운다.
문자열 앞뒤 공백까지 깔끔하게(태그 바로 앞 스페이스도 함께 제거). **다른 건 아무것도 바꾸지 않는다.**

**(A) `WPF_Example/Custom/SystemHandler.cs` — 19곳**

`[ALIGN_TEST]` 15곳(383/407/417/433/443/**451**/476/490/498/511/519/**526**/547/581/614),
`[RESET]` 3곳(**888**/1007/**1014**), `[V1Scope]` 1곳(335).
(굵은 4곳 = 브리핑 A-1~A-4. 나머지 15곳 = A-EXT, `<context>` 의 확장 근거 참조.)

작업 방법: 파일 전체를 훑으며 `Logging.PrintLog` / `string.Format` 의 **첫 인자 문자열 리터럴** 안에
`//26` 이 있는 줄만 골라 태그를 제거한다.

특히 주의할 2줄:
```csharp
// L614 — 한 줄에 태그 2개. 앞쪽(문자열 안)만 제거, 뒤쪽(코드 주석)은 보존.
    "[ALIGN_TEST] ApplyCoaxLightForSlot 예외: {0} //260626 hbk", ex.Message);   //260626 hbk 로그 후 off
// →  "[ALIGN_TEST] ApplyCoaxLightForSlot 예외: {0}", ex.Message);   //260626 hbk 로그 후 off

// L1007 — 문자열 안에 태그 2개 연속. 둘 다 제거.
    "[RESET] Seq={0} 실행 중(State={1}) — 상태 리셋 건너뜀(스레드 안전). 사이클 종료 후 $RESET 재전송 필요. //260807 hbk //260810 hbk 원자적 판정으로 교체",
// →  "[RESET] Seq={0} 실행 중(State={1}) — 상태 리셋 건너뜀(스레드 안전). 사이클 종료 후 $RESET 재전송 필요.",
```

**294행(`[V1Scope] Seq={0} z=0:`)은 건드리지 않는다** — Task 2 에서 로그 자체가 삭제된다.

**(B) `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — 6곳**

389(`[CycleLightOff] ... abnormal`), 657(`[PREP CrossZ]`), 757(`[PREP] Shot not found`),
891(`[CycleLightOff] ... path={1}`), 1448(`[V1Scope] Datum '...'`), 1874(`[V1Cycle] BuildScopedResponse`).

1448/1874 는 `+` 문자열 연결의 마지막 조각 안에 태그가 있다:
```csharp
// L1448
    "[V1Scope] Datum '" + datumName + "' SourceShotName 미해결 — 첫 owned Action(index=" + nFirstOwnedIndex + ")을 DatumPhase 트리거로 사용. //260722 hbk");
// →  … 트리거로 사용.");
```

**513행(`[FAI CrossZ IMG]`)은 건드리지 않는다** — Task 2 에서 로그 자체가 삭제된다.

**(C) 절대 하지 말 것**
- 문자열 **밖**(세미콜론/닫는괄호 뒤)의 `//YYMMDD hbk` 코드 주석은 단 하나도 지우지 않는다.
  헷갈리기 쉬운 것: `Custom/SystemHandler.cs` L368, L838, L864, L882 / `InspectionSequence.cs` L1622, L1645, L1803, L1821.
- 태그를 지우면서 같은 내용을 **새 코드 주석으로 옮겨 적지 않는다**(중복 주석 금지).
- `quick-260812:` 주석도 이 태스크에선 **달지 않는다** — 자명한 삭제라 "왜"가 필요 없다.
- 포맷 인자(`{0}`, `{1}`…)와 뒤따르는 인자 목록은 **한 글자도** 건드리지 않는다.
- `[임시 수동Z트리거]`(904/916/925), `[ALIGN_CALIB]`(683~820), `[MainRun]`(31~33) 무변경.
  이 셋에는 애초에 문자열 내 태그가 없으므로, 이 태스크에서 자연히 안 건드려진다.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && S=WPF_Example/Custom/SystemHandler.cs; I=WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs; echo "=== [기대 0] ALIGN_TEST 문자열내 태그(614 진짜주석 제외) ===" && grep 'ALIGN_TEST\].*//26' $S | grep -v '로그 후 off' | wc -l && echo "=== [기대 0] RESET 태그 ===" && grep -c '\[RESET\].*//26' $S; echo "=== [기대 1] V1Scope 태그 (294 = Task2 삭제분만 잔존) ===" && grep -c 'V1Scope\].*//26' $S; echo "=== [기대 0] InspectionSequence 5종 마커 ===" && grep -c 'CycleLightOff\].*//26' $I; grep -c 'PREP CrossZ\].*//26' $I; grep -c 'PREP\] Shot not found.*//26' $I; grep -c 'V1Scope\] Datum.*//26' $I; grep -c 'V1Cycle\].*//26' $I; echo "=== [기대 1] 진짜 코드주석 보존 확인 ===" && grep -c '로그 후 off' $S && grep -c 'szFaiName = "FAI"; //260629 hbk' $I && grep -c 'AlignTarget == "BOTTOM"; //260626 hbk' $S && echo "=== [기대 3] D그룹 임시 수동Z트리거 무변경 ===" && grep -c '임시 수동Z트리거' $S && echo "=== [기대 0] D그룹 diff 무등장 ===" && git diff -U0 -- $S | grep -c '임시 수동Z트리거'; git diff -U0 -- $S | grep -c 'ALIGN_CALIB'; git diff -U0 -- $S | grep -c '\[MainRun\]'; echo "=== 컴파일(스크래치 OutDir) ===" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="$TEMP/gsd-fye-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-fye-scratch/obj/" //v:minimal //nologo 2>&1 | grep -iE "error CS|warning CS|Build succeeded" | head -20</automated>
  </verify>
  <done>
- `ALIGN_TEST].*//26`(614 제외) 0건, `[RESET].*//26` 0건, InspectionSequence 5종 마커 전부 0건.
- `V1Scope].*//26` 은 정확히 1건(294행 = Task 2 삭제 예정분)만 남는다.
- 진짜 코드 주석 3종(`로그 후 off`, `szFaiName = "FAI"; //260629 hbk`, `AlignTarget == "BOTTOM"; //260626 hbk`) 각 1건 보존.
- `임시 수동Z트리거` 3건 그대로, diff 에 `임시 수동Z트리거`/`ALIGN_CALIB`/`[MainRun]` 0건.
- 신규 `error CS` 0건, 신규 `warning CS` 0건.
  </done>
</task>

<task type="auto">
  <name>Task 2: B그룹 — 개발 진단 전용 PrintLog 8개 삭제 + 전용 지역변수 정리</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs, WPF_Example/Sequence/SequenceHandler.cs, WPF_Example/Custom/SystemHandler.cs</files>
  <action>
`<interfaces>` 의 "B그룹 삭제 대상 원문" 을 앵커로 삼아 **호출을 통째로 삭제**한다.
(9개 중 `[QueueFaiCapture]` 는 구조 변경이 따라오므로 Task 3 에서 처리 → 이 태스크는 8개.)

**(A) `InspectionSequence.cs` — 3개 (Phase 54 ALIGN-01 당시 진단 로그, 기능은 이미 완성)**

1. `[ALIGN]` cur/d/patAngDeg 덤프 (L2269 주석 1줄 + L2270~2275 PrintLog) → **7줄 삭제**
2. `[ALIGN2]` p2cur/baseline 덤프 (L2293~2298) → **6줄 삭제**.
   감싸는 `if (svc.TryFindPose(...))` 블록과 `refBaseline`/`curBaseline`/`thetaRad` 대입 3줄은 **유지**.
   삭제 후 블록 본문은 3줄(refBaseline/curBaseline/thetaRad)만 남는다 — 정상.
3. `[ALIGN]` datumDetect 확증 로그 (L2350~2352 주석 3줄 + L2353 `datumDetectRotDeg` + L2354~2358 PrintLog) → **9줄 삭제**.
   `datumDetectRotDeg` 는 이 로그의 유일 소비자다. `datumKey` 는 바로 아래 lock 블록에서 쓰므로 **유지**.

> 🚫 **L2302 `[ALIGN2] ... 패턴2 매칭 실패 → 단일 패턴 θ 폴백` 은 삭제 금지.**
> `ELogType.Error` 이고 운영자에게 의미 있는 실패 경고다. 이 태스크 후 파일에 `[ALIGN` 로그는 이것 1개만 남아야 한다.

**(B) `Action_FAIMeasurement.cs` — 1개 + 전용 변수 정리**

4. `[FAI CrossZ IMG]` (L513) + 바로 위 `szShotNameForLog`(L512) → **2줄 삭제**.
   위쪽 표시 이미지 교체 로직(`ResultHalconImage.Dispose()` / `= crossZRoleImage.CopyImage()` / `bShotDisplayImageReplaced = true`)은 **유지**.
5. 이제 죽은 변수 3개 정리(grep 으로 확인 완료 — 소비자가 위 로그뿐):
   - L349~351 선언 3줄 삭제
   - L424~431 의 `if (crossZRoleImage != null) { …5줄… }` **블록 통째 삭제**
     (안이 전부 로그용 대입이라 빈 껍데기가 된다). 바로 위 `crossZRoleImage = parentSeq2.TakeCrossZImageCopy(szCapturedRoleKey);` 와
     그것을 감싸는 바깥 `if (bCaptureOk && crossZRoleImage == null && ...)` 는 **유지**.
   - 🚫 `CROSS_Z_ROLE_SUFFIX_A`(L46 const)는 L746/780/1378/1390/1409 에서 계속 쓴다 — **삭제 금지**.

**(C) `WPF_Example/Sequence/SequenceHandler.cs`(base) — 3개**

6. `[STARTUP-WHITE] (f1)` (L162 주석 + L163) → 2줄 삭제
7. `[STARTUP-WHITE] (f2)` (L166 주석 + L167) → 2줄 삭제
8. `[STARTUP-WHITE] (f3)` (L170 주석 + L171) → 2줄 삭제
   사이의 `OnRecipeChanged?.Invoke(...)` 와 `if (result) ExecOnLoad(name);` 는 **유지**.
   Phase 43.2 기동 지연 조사는 이미 SIGNED_OFF 됐다.

> 🚫 **`App.xaml.cs`(a)(e)+라벨없음 1개(L41), `MainWindow.xaml.cs`(b)(c)(d), base `WPF_Example/SystemHandler.cs`(f)(g, L248/L262) 의 `[STARTUP-WHITE]` 는 이번 범위 밖.**
> 세 파일 모두 `files_modified` 에 없다 — 열지도 말 것.

**(D) `Custom/SystemHandler.cs` — 1개**

9. `[V1Scope] Seq={0} z=0: DatumConfigs 비어있음 ... StartAll 폴백` (L293~294 PrintLog 2줄) 삭제.
   바로 아래 `return seq.StartAll(packet);` **유지**.
   위쪽 설명 주석(L281~286) 은 유지하되, 마지막 문장 `…이므로 Trace 로그만 남기고 StartAll 로 안전 폴백한다(회귀 0, T-68-16
   — z>=1 분기의 "매칭 0건" Error 로그와는 성격이 다름).` 가 사실과 어긋나므로 다음처럼 짧게 손질한다:
   ```
   //  레시피 구성(운영 오류 아님)이므로 StartAll 로 안전 폴백한다.
   //  quick-260812: 진단 로그 제거 — 정상 폴백 경로라 운영자에게 알릴 내용이 없다.
   ```
   (앞 문장들은 그대로 두고 마지막 두 줄만 교체)

**(E) 공통 금지사항**
- 삭제로 빈 줄이 연속 2줄 이상 생기면 1줄로 정리한다(그 외 서식은 손대지 않는다).
- 로직 줄(대입/호출/분기/return)은 단 하나도 지우지 않는다. 지우는 건 로그와 **로그 전용 지역변수**뿐이다.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && S=WPF_Example/Custom/SystemHandler.cs; I=WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs; F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs; B=WPF_Example/Sequence/SequenceHandler.cs; echo "=== [기대 1] InspectionSequence 잔존 ALIGN 로그 = Error 폴백 1개뿐 ===" && grep -c '\[ALIGN2\?\] ' $I && echo "=== [기대 1] 그 1개가 '패턴2 매칭 실패' 인지 ===" && grep -c '패턴2 매칭 실패' $I && echo "=== [기대 0] datumDetectRotDeg ===" && grep -c 'datumDetectRotDeg' $I; echo "=== [기대 0] FAI CrossZ IMG / szShotNameForLog / crossZCaptured ===" && grep -c 'FAI CrossZ IMG' $F; grep -c 'szShotNameForLog' $F; grep -c 'crossZCaptured' $F; echo "=== [기대 6] CROSS_Z_ROLE_SUFFIX_A 보존 (46/746/780/1378/1390/1409) ===" && grep -c 'CROSS_Z_ROLE_SUFFIX_A' $F && echo "=== [기대 0] base SequenceHandler(Sequence/) STARTUP-WHITE ===" && grep -c 'STARTUP-WHITE' $B; echo "=== [기대 3/3/2] 범위밖 STARTUP-WHITE 는 그대로 (App.xaml.cs 3 + MainWindow.xaml.cs 3 + base WPF_Example/SystemHandler.cs 2) ===" && grep -c 'STARTUP-WHITE' WPF_Example/App.xaml.cs && grep -c 'STARTUP-WHITE' WPF_Example/MainWindow.xaml.cs && grep -c 'STARTUP-WHITE' WPF_Example/SystemHandler.cs && echo "=== [기대 0] 범위밖 3파일 diff 무등장 ===" && git status --porcelain -- WPF_Example/App.xaml.cs WPF_Example/MainWindow.xaml.cs WPF_Example/SystemHandler.cs | wc -l && echo "=== [기대 0] V1Scope z=0 폴백 로그 + 잔존 문자열내 태그 ===" && grep -c 'z=0: DatumConfigs 비어있음' $S; grep -c 'V1Scope\].*//26' $S; echo "=== [기대 3] D그룹 무변경 ===" && grep -c '임시 수동Z트리거' $S && echo "=== 컴파일(스크래치 OutDir) ===" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="$TEMP/gsd-fye-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-fye-scratch/obj/" //v:minimal //nologo 2>&1 | grep -iE "error CS|warning CS|Build succeeded" | head -20</automated>
  </verify>
  <done>
- `[ALIGN]`/`[ALIGN2]` 로그 잔존 1건 = `패턴2 매칭 실패` Error 로그(보존 대상). `datumDetectRotDeg` 0건.
- `FAI CrossZ IMG` / `szShotNameForLog` / `crossZCaptured*` 0건, `CROSS_Z_ROLE_SUFFIX_A` 6건 보존(46/746/780/1378/1390/1409).
- base `Sequence/SequenceHandler.cs`(파일 A) 의 `STARTUP-WHITE` 0건. `App.xaml.cs` 3건 / `MainWindow.xaml.cs` 3건 / base `WPF_Example/SystemHandler.cs` 2건 그대로이고 세 파일 모두 미변경(`git status` 0줄).
- `z=0: DatumConfigs 비어있음` 0건, `V1Scope].*//26` 0건(Task 1 잔여분까지 소멸).
- 삭제된 PrintLog 누적 8개. 신규 `error CS` 0건, 신규 `warning CS` 0건.
  </done>
</task>

<task type="auto">
  <name>Task 3: QueueFaiCapture 계측 제거 + C그룹 문구 한글화 + 최종 검증</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs, WPF_Example/Custom/Sequence/SequenceHandler.cs, WPF_Example/Sequence/SequenceHandler.cs</files>
  <action>
**(A) B그룹 마지막 1개 — `Action_FAIMeasurement.cs` `QueueFaiCapture` 성능 계측 제거 (L962~1063)**

`<interfaces>` 의 "QueueFaiCapture 계측" 표를 그대로 따른다. 삭제 항목:
- L964~970 계측 배경 설명 주석 7줄
- L971~973 `swTotal` / `swStage` / `msPrep,msOrigin,msSnapshot,msCaptureEnqueue`
- L974 `try {` 와 L1053 `}`, L1054~1062 `finally { …주석 4줄 + PrintLog 3줄… }`
- 본문 안의 `swStage.Restart();` 4곳(978/1008/1034/1040)과 `ms* = swStage.ElapsedMilliseconds;` 4곳(1006/1028/1038/1052)

**유지 항목:** L962 시그니처, L963 `if (fai == null) return;`, L975~1053 본문 로직 전부
(중간의 `if (saver == null || sharedSrc == null) return;` 포함), L1063 메서드 닫는 중괄호.

`try/finally` 를 없앤 뒤 본문을 **4칸 내어쓰기(dedent)** 한다. `catch` 가 없고 `finally` 내용이 로그뿐이므로
예외 전파와 중간 `return` 동작은 **100% 동일**하다.

메서드 위 요약 주석(L955~961)은 기능 설명이라 **유지**한다. 계측 관련 문장은 L964~970 에만 있다.

> **자기검증:** 편집 후 `git diff -w -- <파일>`(공백 무시)을 보면 **삭제된 줄만** 나타나야 한다.
> 내어쓰기 외에 본문이 바뀌었다면 실수다. verify 에 이 확인이 들어있다.

`using System.Diagnostics;`(L3)는 **그대로 둔다** — 미사용 using 은 컴파일 경고를 내지 않고, 건드리면 diff만 늘어난다.

**(B) C그룹 — 문구만 쉬운 한글로. 로직 무변경.**

1. `Custom/Sequence/SequenceHandler.cs` L70 — 문자열만 교체(둘째 줄 인자 목록 **무변경**):
```csharp
            Logging.PrintLog((int)ELogType.Trace, "[검사 시작 차단] {0} 시작 못 함 — {1}이(가) 이미 검사 중",
                ResolveSequenceName(eTargetSeqId), sBlockingSeqName);
```
   포맷 인자 2개, 순서 그대로. 위 L69 주석과 아래 `return true;` 무변경.

2. `Sequence/SequenceHandler.cs` L346 — 영어 원문 → 한글:
```csharp
                Logging.PrintLog((int)ELogType.Trace, "[요청 거부] 캘리브레이션 요청은 자동 검사 흐름에서 처리하지 않음");
```
   감싸는 `if ((ETestType)packet.TestType == ETestType.Calibration)` 과 `return false;` 무변경.

3. `Action_FAIMeasurement.cs` L1334 — `(회귀 0)` 만 제거:
```csharp
                Logging.PrintLog((int)ELogType.Trace, "[FAI CrossZ] role " + szRoleLabel + " 교시 이미지 미설정 — 라이브 이미지로 폴백");
```
   바로 아래 L1343 의 `[FAI CrossZ] role 교시 이미지 로드 실패(...)` 는 이미 쉬운 문구다 — **무변경**.

**(C) 최종 전체 검증**
verify 블록이 A/B/C/D 4개 그룹 + Picker 파일 baseline + 변경 파일 목록 + 빌드를 한 번에 확인한다.
`git status --porcelain` 에 **정확히 6개 파일**만 나와야 한다: 이번 작업 5개 + Picker(사용자 별건, `M` 이지만 내용 무변경).
`.planning/quick/...` 디렉터리(`??`)는 별개다.

**(D) 빌드**
정상 경로 `//t:Rebuild` 를 먼저 시도한다. 산출물이 실행 중인 앱에 잠겨 MSB3021/3026/3027/3030 이 나면
**절대 프로세스를 죽이지 말고** 스크래치 OutDir 컴파일로 대체한 뒤 SUMMARY 에 "산출물 잠김 → 스크래치 컴파일 검증" 이라고 남긴다.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && S=WPF_Example/Custom/SystemHandler.cs; I=WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs; F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs; B=WPF_Example/Sequence/SequenceHandler.cs; C=WPF_Example/Custom/Sequence/SequenceHandler.cs; P=WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs; echo "=== [B] 기대 0: QueueFaiCapture 계측 잔재 ===" && grep -c 'QueueFaiCapture\]' $F; grep -c 'swStage\|swTotal\|msPrep\|msOrigin\|msSnapshot\|msCaptureEnqueue' $F; echo "=== [B] 기대 1: QueueFaiCapture 메서드는 살아있음 ===" && grep -c 'private void QueueFaiCapture' $F && echo "=== [B] 공백무시 diff = 순삭제만 (+ 로 시작하는 줄 0 기대, C그룹 3줄 제외분은 아래서 별도확인) ===" && git diff -w -- $F | grep -c '^+[^+]'; echo "=== [C] 기대 0/1 ===" && grep -c 'RUN-GATE' $C; grep -c '검사 시작 차단' $C; grep -c 'Calibration test requests' $B; grep -c '요청 거부' $B; grep -c '라이브 이미지로 폴백(회귀 0)' $F; echo "=== [C] 기대 2: 폴백 문구 2줄 유지 ===" && grep -c '라이브 이미지로 폴백' $F && echo "=== [C] 포맷 인자 무변경 확인 (기대 1) ===" && grep -c 'ResolveSequenceName(eTargetSeqId), sBlockingSeqName' $C && echo "=== [A] 기대 0: 문자열내 태그 전멸 ===" && grep 'ALIGN_TEST\].*//26' $S | grep -v '로그 후 off' | wc -l; grep -c '\[RESET\].*//26' $S; grep -c 'V1Scope\].*//26' $S; grep -c 'CycleLightOff\].*//26' $I; grep -c 'PREP CrossZ\].*//26' $I; grep -c 'PREP\] Shot not found.*//26' $I; grep -c 'V1Scope\] Datum.*//26' $I; grep -c 'V1Cycle\].*//26' $I; echo "=== [D] 기대 0: 무변경 3종이 diff 에 없음 ===" && git diff -U0 -- $S | grep -c '임시 수동Z트리거'; git diff -U0 -- $S | grep -c 'ALIGN_CALIB'; git diff -U0 -- $S | grep -c '\[MainRun\]'; echo "=== [금지] Picker baseline: 6 2 + 73a89c282724fedf25b7dcf8919b09251578d789 기대 ===" && git diff --numstat -- $P 2>/dev/null && git diff -- $P 2>/dev/null | git hash-object --stdin && echo "=== 변경 파일 목록: 5개 M + Picker M 기대 ===" && git status --porcelain && echo "=== Debug/x64 Rebuild (잠기면 스크래치 폴백) ===" && ("/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //t:Rebuild //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo 2>&1 | grep -iE "error|warning CS|Build succeeded" | head -25 || "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="$TEMP/gsd-fye-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-fye-scratch/obj/" //v:minimal //nologo 2>&1 | grep -iE "error CS|warning CS|Build succeeded" | head -25)</automated>
  </verify>
  <done>
- **[A]** 8개 마커 grep 전부 0건 — 문자열 내부 개발자 태그 25곳 전멸. 진짜 코드 주석은 보존.
- **[B]** 누적 9개 PrintLog 삭제. `QueueFaiCapture]` 0건, 계측 변수 0건, 메서드 자체는 1건 생존.
  `git diff -w` 의 추가(`+`) 줄이 C-3 한 줄뿐 — 내어쓰기 외 본문 무변경의 증거.
- **[C]** `RUN-GATE` 0 / `검사 시작 차단` 1, `Calibration test requests` 0 / `요청 거부` 1,
  `라이브 이미지로 폴백(회귀 0)` 0 / `라이브 이미지로 폴백` 2. 포맷 인자줄 무변경 확인 1건.
- **[D]** diff 에 `임시 수동Z트리거` / `ALIGN_CALIB` / `[MainRun]` 각 0건.
- `PickerCenterCalibrationService.cs` numstat `6 2`, diff 해시 `73a89c282724fedf25b7dcf8919b09251578d789` — baseline 동일.
- `git status --porcelain` 에 이번 작업 5개 파일 + Picker(내용 무변경)만 등장.
- Debug/x64 빌드 성공, 신규 `error CS` 0건 / 신규 `warning CS` 0건.
  </done>
</task>

</tasks>

<verification>
1. **A그룹(태그 제거)** — 마커 앵커 grep 8종이 전부 0. 문자열 밖 코드 주석 3종 표본이 그대로 살아있음.
2. **B그룹(로그 삭제)** — 삭제된 `Logging.PrintLog` 총 9개:
   InspectionSequence 3 + Action_FAIMeasurement 2 + Sequence/SequenceHandler 3 + Custom/SystemHandler 1.
   전용 지역변수 6종(`datumDetectRotDeg`, `szShotNameForLog`, `crossZCaptured*`, `swTotal/swStage/ms*`) 0건.
   공유 심볼(`CROSS_Z_ROLE_SUFFIX_A`, `datumKey`, `thetaRad` 등) 보존.
3. **C그룹(문구)** — 3곳 문자열만 교체. `git diff` 에서 해당 줄의 변경이 **문자열 리터럴 안에 국한**되고
   조건문/반환값/인자 목록이 컨텍스트로만 등장(변경 줄 아님).
4. **D그룹(무변경)** — `git diff -U0` 에 `임시 수동Z트리거`/`ALIGN_CALIB`/`[MainRun]` 0건.
5. **범위 밖 파일** — `App.xaml.cs`, `MainWindow.xaml.cs`, base `WPF_Example/SystemHandler.cs`, `PickerCenterCalibrationService.cs` 미변경.
6. **빌드** — Debug/x64 신규 `error CS` 0 / 신규 `warning CS` 0.
</verification>

<success_criteria>
- 시퀀스 실행 경로 로그에서 개발자 커밋태그가 **한 개도 출력되지 않는다**.
- 이미 끝난 조사용 진단 로그 9개가 사라져 로그창 잡음이 줄었다.
- 남은 로그 3곳이 초보 운영자가 읽어도 뜻이 통하는 한글 문구가 됐다.
- 판정 로직·제어 흐름·포맷 인자는 단 한 곳도 바뀌지 않았다(회귀 0).
- `[임시 수동Z트리거]` 와 `PickerCenterCalibrationService.cs` 는 diff 에 나타나지 않는다.
- Debug/x64 빌드가 신규 에러/경고 0 으로 통과한다.
</success_criteria>

<output>
완료 후 `.planning/quick/260812-fye-sequence-trace-log-cleanup/260812-fye-SUMMARY.md` 를 작성한다.

SUMMARY 에 반드시 포함:
- 그룹별 처리 건수 (A: 25곳 / B: 9개 PrintLog + 전용변수 6종 / C: 3곳 / D: 0곳)
- **A-EXT 확장 사실** — 브리핑 4곳 → 실제 25곳으로 확장한 근거와, 확장분이 순수 문자열 접미부 삭제였다는 점
- `QueueFaiCapture` 의 `try/finally` 해체 사실과 "예외 전파·return 동작 동일" 근거
- 보존 판단한 로그 목록 (`[ALIGN2] 패턴2 매칭 실패`, `[ALIGN_CALIB]`, `[MainRun]`, `[임시 수동Z트리거]`, 범위 밖 `[STARTUP-WHITE]` 8개: App.xaml.cs 3 + MainWindow.xaml.cs 3 + base SystemHandler.cs 2)
- 빌드 방식 (정상 Rebuild 인지 / 산출물 잠김으로 스크래치 컴파일인지)
</output>
</content>
</invoke>
