---
phase: quick-260818-ruh
plan: 01
subsystem: inspection-sequence
tags: [refactor, cross-z, behavior-preserving, enum-switch]
requires: []
provides: ["ECrossZGate enum", "ResolveCrossZGate", "TakeCrossZRoleImageIfFirst", "MarkCrossZHalfPending"]
affects: ["WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"]
tech-stack:
  added: []
  patterns: ["명시적 상태 enum + 고전 switch(C# 7.2) 디스패치", "부수효과 호출을 분류 함수 밖에 잔류시키는 순수/비순수 분리"]
key-files:
  created: []
  modified: ["WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"]
decisions:
  - "판정(순수)과 캡처(부수효과)를 분리하되, 부수효과 호출은 switch 앞/case 첫 줄에 그대로 남겨 원본 실행 순서를 눈에 보이게 보존"
  - "갈래 #2/#3 을 enum 멤버로 쪼개지 않고 NotMyTick case 안 if-else 로 유지 (원본 구조와 시각적 1:1)"
  - "case BothReady 는 break — return 이 아님 (공용 실행 경로 fall-through 동치)"
metrics:
  duration: "~25m"
  completed: "2026-08-18"
  tasks: 2
  files: 1
  commits: 1
---

# Quick 260818-ruh: 크로스-Z 게이트 ECrossZGate enum + switch 재구성 Summary

`Action_FAIMeasurement.ProcessOneMeasurement()` 의 크로스-Z 게이트(중첩 if 7갈래)를
`private enum ECrossZGate` 5멤버 + 고전 `switch` 5-case 로 재구성한 **의미 보존 변환**.
분기 조건 / 실행 순서 / 부수효과 시점 / `measuredCount` 증감 / `faiAllPass` 갱신 전부 동일.

- **커밋:** `a77e471` refactor(260818-ruh): 크로스-Z 게이트를 ECrossZGate enum + switch 로 재구성 (7갈래 1:1, 동작 무변경)
- **변경 파일:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 1개 (+109 / −57)
- **기준 커밋(BASE):** `b6a8cb8` (파일 마지막 변경 커밋 `12fa8aa`, 착수 시 clean, 1669줄)

---

## 신규 구조 (현재 줄번호)

| 요소 | 줄 |
|------|----|
| `private enum ECrossZGate {` (5멤버) | L46–L52 |
| 게이트 블록 전체 | L556–L625 |
| `switch (eGate)` | L595 |
| `private ECrossZGate ResolveCrossZGate(...)` | L682–L687 |
| `private void TakeCrossZRoleImageIfFirst(...)` | L695–L701 |
| `private void MarkCrossZHalfPending(...)` | L703–L720 |
| 공용 실행 경로 진입 `ResolveDatumTransform` | L626 (원본 L618, 무변경) |

---

## 7갈래 + 부수효과 1:1 대조표

원본 스냅샷 `scratchpad/ruh-before-gate.txt`(74줄, 원본 L544–617)와 현재 파일을 갈래별로 대조.

| # | 조건 | 원본 동작 | switch 후 위치 | measuredCount | faiAllPass | 증거 |
|---|------|-----------|----------------|---------------|------------|------|
| 1 | `IsZIndexMisconfigured` true | `MarkMeasurementZIndexMisconfigured` + `faiAllPass=false` + `++` + `return` | `case Misconfigured:` **L597–L601** | `++` (L600) | `false` (L599) | 원본 L550–557 → 현재 L574–577(분류) + L597–601(본문). `MarkMeasurementZIndexMisconfigured` 파일 카운트 3 (착수 전 3) |
| 2 | `!bRelevant` && 비프로토콜 | `MarkMeasurementCrossZIncomplete(meas,false,false,parentSeq2)` + `false` + `++` + `return` | `case NotMyTick:` **if-참 L603–L608** | `++` (L607) | `false` (L606) | 원본 L570–579 → 현재 L602–609. 인자 `(meas, false, false, parentSeq2)` 문자열 동일 |
| 3 | `!bRelevant` && 프로토콜 | **아무 상태변화 없이 `return`** | `case NotMyTick:` **if-거짓 → L609 `return;`** | **증가 안 함** (L607 이 `if (bNonProtocolCycle)` 블록 L604–608 안에만 존재) | **무변경** | GATE-3 GUARD PASS — `sed -n '/case ECrossZGate.NotMyTick:/,/case ECrossZGate.CaptureFailed:/p'` 구간에서 `if (bNonProtocolCycle)` **1개**, `measuredCount++` **1개**, `MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2)` **1개**. 즉 `++` 가 if 밖으로 나오지 않았음이 기계 증명됨. 인라인 주석 `// 프로토콜: 이 tick 은 이 측정과 무관 — 상태변화 없음(안전망, 무변경)` 도 L609 에 보존 |
| 4 | `!bCaptureOk` | `ClearResult()` + `NO_IMAGE` + `LastJudgement=false` + `false` + `++` + `return` | `case CaptureFailed:` **L610–L616** | `++` (L615) | `false` (L614) | 원본 L580–588 → 현재 L610–616, 5문장 순서 동일. `SkipReason.NO_IMAGE` 파일 카운트 2 (착수 전 2), `meas.ClearResult()` 7 (착수 전 7) |
| 5 | **부수효과:** `bCaptureOk && crossZRoleImage==null && !IsNullOrEmpty(szCapturedRoleKey) && parentSeq2!=null` | `crossZRoleImage = parentSeq2.TakeCrossZImageCopy(szCapturedRoleKey)` | `TakeCrossZRoleImageIfFirst` (본체 L695–L701, 조건식 L696) — 호출은 **HalfPending L618 / BothReady L622, 각 case 의 첫 줄** | — | — | ① 조건식 verbatim: `diff <(원본 grep) <(현재 grep)` → **차이 0** ② 호출부 앵커 grep `^[[:space:]]*TakeCrossZRoleImageIfFirst\(parentSeq2` → **정확히 2** ③ `TakeCrossZImageCopy(szCapturedRoleKey)` 파일 카운트 **1** (착수 전 1) ④ switch 앞(L562–L594)에는 호출 0건 — Misconfigured/NotMyTick/CaptureFailed 에서 평가되지 않음 |
| 6a | `!bCompleted` && 비프로토콜 | `MarkMeasurementCrossZIncomplete(meas,true,false,parentSeq2)` + `false` + `++` + `return` | `MarkCrossZHalfPending` **if-참 L705–L709** (호출 L619, 이후 L620 `return`) | `++` (L719, 두 갈래 합류점) | `false` (L707) | 원본 L596–602 → 현재 L706–707. 인자 `(meas, true, false, parentSeq2)` 문자열 동일 |
| 6b | `!bCompleted` && 프로토콜 | `MarkMeasurementCrossZIncomplete(meas,true,true,parentSeq2)` + `false` + `++` + `return` | `MarkCrossZHalfPending` **else L710–L718** | `++` (L719) | `false` (L717) | 원본 L603–612 → 현재 L710–717. T-HWB-01 주석 5줄(L711–715) 그대로 이동. 인라인 주석 `// 프로토콜 Z1(비완성 index)…` 은 L719 `measuredCount++` 에 보존 |
| 7 | `bCompleted` | fall-through → `ResolveDatumTransform` | `case BothReady:` **L621–L623 → `break`** | — | — | `grep -cE '^[[:space:]]*break; // 완성 index'` → **1** (L623). `return` 아님. `break` → switch 탈출(L624) → `if (bHasAnyZIndex)` 블록 탈출(L625) → **L626 `ResolveDatumTransform`** 로 흐름 = 원본 L616 주석 + L617 `}` + L618 과 동치 |

---

## 추가 명시 확인 4건

### 1. `bHasAnyZIndex == false` 무진입 회귀 0
게이트 앞 5줄(주석 2 + `dualMeasForGate` 선언 + `bHasAnyZIndex` 선언 + `if (bHasAnyZIndex)`) 이 글자 하나 안 바뀌었다.

```
diff <(sed -n '1,5p' ruh-before-gate.txt) <(sed -n '556,560p' Action_FAIMeasurement.cs)
→ 차이 0  ("PREAMBLE(주석2+선언2+if) DIFF 0")
```

이후 `if (bHasAnyZIndex)`(현 L633) / `if (bHasAnyZIndex) szAlgoEntry = ...`(현 L645) 분기가 여전히 같은 두 변수를 읽는다 — 아래 4번 diff 로 함께 증명됨.

### 2. 호출 순서/횟수 보존 (Misconfigured 경로에서 캡처/프로토콜 조회 미호출)
구조가 `if (bMisconfigured) { eGate = Misconfigured; } else { ProcessCrossZCaptureTick(...); bNonProtocolCycle = ...IsProtocolDrivenCycle(); eGate = ResolveCrossZGate(...); }` (L575–L592).
즉 **`IsZIndexMisconfigured` 가 true 면 `ProcessCrossZCaptureTick`(L581) 과 `IsProtocolDrivenCycle()`(L590) 에 도달 자체를 못 한다** — 원본 단락(short-circuit) 순서와 동일.

- `IsZIndexMisconfigured(` 파일 카운트 **2** (호출 1 + 선언 1, 착수 전 2)
- `^[[:space:]]*ProcessCrossZCaptureTick\(dualMeasForGate` **1**, `^[[:space:]]*private void ProcessCrossZCaptureTick\(` **1**, 파일 전체 `ProcessCrossZCaptureTick` **3** (착수 전 3)
- `parentSeq2.IsProtocolDrivenCycle()` 파일 카운트 **1** (착수 전 1)

`ResolveCrossZGate` 는 완전 순수 — 본체 4줄(L683–686)이 인자 3개 bool 만 읽고 아무것도 쓰지 않는다.

### 3. 부수효과 #5 실행 상태 집합 = {HalfPending, BothReady} 정확히 둘
원본 74줄에서 이 문장(원본 L589–595)의 위치는 `if (!bCaptureOk){…return;}`(원본 L580–588) **뒤**,
`if (!bCompleted){…return;}`(원본 L596–615) **앞**이다. 따라서 원본에서 이 문장에 도달하는 상태는

- Misconfigured → 원본 L556 `return` 으로 이미 탈출 ✗
- NotMyTick → 원본 L578 `return` 으로 탈출 ✗
- CaptureFailed → 원본 L587 `return` 으로 탈출 ✗
- **HalfPending → 도달 ✓** (원본 L589 평가 후 L596 if 진입)
- **BothReady → 도달 ✓** (원본 L589 평가 후 L616 fall-through)

리팩토링 후에도 동일: 호출부는 L618(HalfPending 첫 줄) / L622(BothReady 첫 줄) **2곳뿐**이고 switch 앞에는 0건.
"중복이니 switch 앞으로 합치자"는 변환을 명시적으로 거부했다 — 합치면 NotMyTick/Misconfigured 에서도 평가되어 캡처 이미지가 뒤바뀐다.

### 4. 범위 밖 무변경
`git diff` hunk 가 정확히 **3덩어리**뿐이다:

```
@@ -41,0  +42,12  @@   (a) enum 삽입
@@ -549..-616 / +562..+624 @@ (b) 게이트 블록
@@ -669,0 +678,44 @@   (c) 신규 메서드 3개
```

경계 밖 직접 diff 도 0:

- `diff <(HEAD:파일 sed 517,548p) <(현재 529,560p)` → **PRE-GATE (Datum 게이트 2개 포함) DIFF 0**
  (`IsDatumFailed` L539–545 / `IsDatumRefUnresolvable` L549–554 무접촉)
- `diff <(HEAD:파일 sed 618,669p) <(현재 626,677p)` → **POST-GATE DIFF 0**
  (`ResolveDatumTransform` / `InjectDatumOrigin` / `TryExecuteCrossZMeasurement` / 집계 경로 전부 무변경)

---

## 파일 전역 불변 카운트 (착수 전 실측 = 현재)

| 패턴 | 착수 전 | 현재 |
|------|--------|------|
| `measuredCount++` | 8 | **8** |
| `faiAllPass = false` | 8 | **8** |
| `meas.ClearResult()` | 7 | **7** |
| `MarkMeasurementCrossZIncomplete(` | 4 | **4** |
| `SkipReason.NO_IMAGE` | 2 | **2** |
| `TakeCrossZImageCopy(szCapturedRoleKey)` | 1 | **1** |
| `parentSeq2.IsProtocolDrivenCycle()` | 1 | **1** |
| `IsZIndexMisconfigured(` | 2 | **2** |
| `ProcessCrossZCaptureTick` | 3 | **3** |
| `MarkMeasurementZIndexMisconfigured` | 3 | **3** |

→ `INVARIANT COUNTS PASS`

구조 검증: enum 1 / `case ECrossZGate.*:` 앵커 **5** / `case EStep.*:` 앵커 **6**(무회귀) /
신규 메서드 3 / `TakeCrossZRoleImageIfFirst(parentSeq2` 앵커 **2** / `MarkCrossZHalfPending(meas` **1** /
`break; // 완성 index` **1** / `switch (eGate)` **1** / C# 8 문법(switch expression, pattern `when`) **0** /
`=> ` **1**(baseline L52 `ShotParam => Param as ShotConfig`, 증가 없음) → `ALL STRUCTURE CHECKS PASS`

위생: `260729-e9q` **2**, `260729-hwb` **8**, `Phase 68 D-02a/D-05` **1** (전부 착수 전과 동일, 삭제 0건) /
코드 삼항 **0**(정제 grep 잔여 1줄은 L1281 주석) → `HYGIENE PASS`

---

## 원형 유지 대상 + 근거 (리팩토링하지 **않은** 판단)

| 원형 유지 대상 | 왜 손대지 않았나 |
|----------------|------------------|
| `ProcessCrossZCaptureTick` 호출을 switch 앞(L581)에 잔류 | 이 호출은 `StoreCrossZImage` 로 **실제 저장을 수행**하는 부수효과 함수다. 분류 함수 안으로 옮기면 "판정" 이름 뒤에 저장이 숨어 다음 사람이 호출 순서를 자유롭게 바꿀 위험이 생긴다 |
| `IsZIndexMisconfigured` 호출을 switch 앞(L574)에 잔류 | 원본은 이게 true 면 `ProcessCrossZCaptureTick` 을 **호출하지 않는다**. 이 단락 순서를 유지하려면 두 호출이 같은 if/else 안에 있어야 한다 |
| `bNonProtocolCycle` 을 else 블록(L590) 안에서만 계산 | 원본은 Misconfigured 경로에서 `IsProtocolDrivenCycle()` 을 호출하지 않는다. switch 앞으로 무조건 끌어올리면 호출 횟수가 늘어난다(현재 순수 함수라 관측 불가하지만 무회귀 원칙상 호출 횟수도 보존) |
| 부수효과 #5 를 두 case(L618/L622)에 중복 호출 | 한 곳으로 합치면 실행 상태 집합이 {HalfPending, BothReady} → 전체 5상태로 바뀐다. 캡처 이미지 뒤바뀜(T-ruh-03) 직결 |
| 갈래 #2/#3 을 enum 멤버로 쪼개지 않음 | `NotMyTick` 안의 `if (bNonProtocolCycle)` 이 원본 구조와 시각적으로 1:1 이라 대조 비용이 가장 낮다. 멤버로 쪼개면 `bNonProtocolCycle` 계산 시점을 분류 함수로 끌고 들어가야 해 순수성이 깨진다 |
| 조건식 첫 항 `bCaptureOk` 유지 (L696) | switch 구조상 이미 참임이 보장되지만 **원문 verbatim 이 대조의 근거**라서 그대로 남겼다 |
| `default:` 미추가 | 5개 멤버를 전부 다루고 있어 default 를 넣으면 감사되지 않은 6번째 경로가 생긴다 (L593–594 에 명문화) |

---

## 빌드 결과

- `msbuild DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Rebuild` (scratch OutputPath, 실행 중 프로세스 무접촉)
- **성공**, error **0**
- 경고 **12줄** — `CS0618 × 10` + `CS0162 × 2`, 착수 전 baseline(`ruh-baseline-warn.txt`)과 **완전 동일**
- 신규 `CS0219` / `CS0168`(미사용 지역변수) **0건** — out 변수 5개를 선언 시 초기화했고 전부 사용됨
- 비-SIMUL(`#else`) 빌드 생략. 근거: 편집 구역(원본 L517–668)에 `#if` **0개** (착수 전 `grep -c '#if'` → 0). 조건부 컴파일 경계를 가로지르지 않는다

## 리포지토리 위생

- 스테이징/커밋된 파일 **정확히 1개** (`git show --stat --name-only --format= HEAD` → 1줄)
- `WPF_Example/DatumMeasurement.csproj` 의 로컬 미커밋 설정(Debug `OutputPath=D:\Data\`, Release `SIMUL_MODE`)은 커밋 후에도 **` M`(unstaged)** 로 그대로 남음 — 확인됨
- 워킹트리 dirty 집합이 baseline(`ruh-git-baseline.txt`) 대비 대상 파일 하나만 변동
- `.planning/quick/260818-ruh-z-enum-switch-100/` 는 untracked 유지 (오케스트레이터가 처리)

---

## 사용자 UAT 요청 3항목

코드 레벨 무회귀는 기계 검증으로 끝났지만, **실사용 확인 3건**을 부탁드립니다.

1. **프로토콜 사이클 크로스-Z 측정 (갈래 6b → 7)**
   Z1 tick 에서 화면이 `CROSS_Z_INCOMPLETE` 로 표시되고, Z2 tick 에서 정상 측정값이 나오는지 확인.
   → `case BothReady:` 가 `break` 로 공용 실행 경로에 흘러가는지의 실사용 증명입니다.

2. **캡처 이미지가 올바른 role 이미지인지 (갈래 #5, 이번 작업 최대 위험 지점)**
   저장된 FAI 캡처 PNG 가 리팩토링 전과 **같은 이미지**인지 대조해 주세요.
   부수효과 실행 시점이 어긋나면 여기서만 증상이 나타납니다.

3. **RUN 버튼(비프로토콜)으로 크로스-Z 항목 실행 (갈래 2 / 6a)**
   PASS 로 조용히 집계되지 않고 **NG 로 표시**되는지 확인.

---

## Deviations from Plan

None — 플랜에 적힌 코드/배치/순서를 그대로 적용했습니다. 자동 수정(Rule 1~3) 발생 0건.

## Known Stubs

없음.

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — FOUND (수정 반영됨)
- `.planning/quick/260818-ruh-z-enum-switch-100/260818-ruh-SUMMARY.md` — FOUND
- 커밋 `a77e471` — FOUND (`git log`)
</content>
