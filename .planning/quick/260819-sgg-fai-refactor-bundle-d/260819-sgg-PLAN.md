---
phase: quick-260819-sgg
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [SGG-01]

must_haves:
  truths:
    - "[SGG-01] ProcessCrossZCaptureTick(원본 L1622-1657, 41줄, 외부 호출부 정확히 1곳: L683)의 4개 out 파라미터(bRelevant/bCaptureOk/bCompleted/szCapturedRoleKey)가 이름 있는 필드를 가진 신규 `CrossZCaptureTickResult` 클래스(class, 파일 상단 `ShotMeasureAccumulator` 와 동일한 필드+K&R 스타일 정확히 따름)로 교체된다. 시그니처: `private CrossZCaptureTickResult ProcessCrossZCaptureTick(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)` (out 파라미터 0개)."
    - "원본의 제어흐름(4개 지점 — 진입 직후 all-default 초기화, bRelevant=false 인 조기 return, capturedImage==null 인 조기 return, 정상 종료)이 정확히 동일하게 보존된다 — 각 return 지점에서 `return;` → `return result;` 로만 바뀌고, 그 외 조건/순서/부수효과(StoreCrossZImage 호출 등)는 1도 바뀌지 않는다."
    - "호출부(`EvaluateCrossZGate`, 원본 L670-673/683/688/715/719)에서 4개 지역변수 선언이 제거되고, `CrossZCaptureTickResult tickResult = new CrossZCaptureTickResult();` 로 if/else 진입 전에 all-default 인스턴스로 선치화된다(원본 4개 out 변수가 false/false/false/null 로 선초기화되던 것과 동치 — `bMisconfigured` 가 true 인 분기는 `ProcessCrossZCaptureTick` 을 애초에 호출하지 않으므로 이 all-default 상태가 그대로 남아야 원본과 동일하다). `else` 블록 안에서만 `tickResult = ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2);` 로 호출한다(원본과 동일하게 `if(bMisconfigured)` 분기에서는 호출 안 함)."
    - "`ResolveCrossZGate(bRelevant, bCaptureOk, bCompleted)` → `ResolveCrossZGate(tickResult.Relevant, tickResult.CaptureOk, tickResult.Completed)` 로, `TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage)` (L715/L719 2곳) → `TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage)` 로 바뀐다 — `ResolveCrossZGate`/`TakeCrossZRoleImageIfFirst` 두 메서드 자신의 시그니처는 1글자도 바뀌지 않는다(호출부 인자 표현식만 변경)."
    - "`switch (eGate)` 블록(원본 L692-721)의 5개 case 라벨과 그 본문은, L715/L719 두 줄(TakeCrossZRoleImageIfFirst 인자 표현식)을 제외하고 byte-identical 하다 — 오늘 quick-260819-hyk 가 6-경로 bool-매핑 표로 검증 완료한 구역이므로 그 외 어떤 것도 건드리지 않는다. `bNonProtocolCycle` 선언/대입/사용(4곳)은 이번 변경과 무관 — 무변경."
    - "빌드 PASS — error CS 0건, warning CS 정확히 12건(baseline, CS0618×10+CS0162×2) 유지. 신규 CS0219/CS0168/CS0103/CS0161(미할당) 0건."
    - "파일 최종 줄수 — **1777**줄(1771+6). 내역: (a) 호출부 지역변수 4줄→`tickResult` 1줄 치환 구간 순감소 -1줄(19→18줄), (b) case 본문 2곳 인자 표현식 치환은 줄수 변화 없음(각 0줄), (c) `CrossZCaptureTickResult` 클래스 신설 + `ProcessCrossZCaptureTick` out→리턴값 전환 구간 순증가 +7줄(41→48줄). 플래너가 old_string/new_string 을 줄 단위 손계산으로 실측 검증(각 블록 라인 번호를 1부터 끝까지 나열해 카운트) — 4개 Edit 각각의 순변화값(-1/0/0/+7)을 합산한 결정론적 값이다."
    - "Action_FAIMeasurement.cs 단 1개 파일만 변경(단일 커밋). WPF_Example/DatumMeasurement.csproj(로컬 미커밋 오염, 항상 존재)는 커밋 후에도 git status 에 unstaged M 으로 남는다 — git add 는 대상 파일 경로 직접 지정만 사용, `git add -A`/`-a` 금지."
    - "파일 인코딩 손상 0건 — UTF-8 BOM 유지 + LF 개행 유지(CRLF 유입 0건), 한글 주석/문자열 손상 0건. Edit 도구만 사용(bash/python heredoc 금지, 한글 텍스트 작성 시 특히)."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "CrossZCaptureTickResult 클래스 신설(ProcessCrossZCaptureTick 바로 위) — out 4개 파라미터를 이름 있는 필드로 교체"
      contains: "private class CrossZCaptureTickResult {"
  key_links:
    - from: "EvaluateCrossZGate else 블록"
      to: "ProcessCrossZCaptureTick"
      via: "직접 호출, 반환값을 tickResult 에 대입(out 아님)"
      pattern: "tickResult = ProcessCrossZCaptureTick\\(dualMeasForGate, parentSeq2\\);"
    - from: "EvaluateCrossZGate (ResolveCrossZGate 호출/HalfPending·BothReady case)"
      to: "CrossZCaptureTickResult 필드(Relevant/CaptureOk/Completed/CapturedRoleKey)"
      via: "tickResult.필드명 읽기"
      pattern: "tickResult\\.(Relevant|CaptureOk|Completed|CapturedRoleKey)"
---

<objective>
`Action_FAIMeasurement.cs`(오늘 7차례 리팩토링 완료 — fik/gf1/hyk/j6j/q9t/rle/s05, 전부 "동작 무변경" 검증됨, HEAD=`74957dd`, 현재 1771줄) 사용자 요청 Bundle D:

`ProcessCrossZCaptureTick`(원본 L1622-1657)의 4개 `out` 파라미터(`bRelevant`/`bCaptureOk`/`bCompleted`/`szCapturedRoleKey`)를 이름 있는 필드를 가진 소형 클래스 `CrossZCaptureTickResult` 로 교체하는 순수 기계적 리팩토링. `out` 4개 → 리턴값 1개(구조체 대신 필드 4개짜리 클래스)로 바뀌지만, 호출부(`EvaluateCrossZGate`)의 판정 로직·순서·조건은 1도 바뀌지 않는다.

Purpose: 이름 없는 4-tuple(out out out out) 대신 이름 있는 필드로 호출부 가독성을 높인다. 동작은 단 하나도 바뀌지 않는다.
Output: 파일 1개 수정(새 파일 0개), 클래스 1개 신설, 커밋 1개.

⚠ **위험 구역 근접 경고(사용자 명시)**: 이 플랜이 유일한 호출부(`EvaluateCrossZGate`)에서 건드리는 지점은 딱 3곳 — (1) 진입부 지역변수 선언~else 블록의 `ResolveCrossZGate` 호출까지(원본 L670-688), (2) `case HalfPending:` 본문의 `TakeCrossZRoleImageIfFirst` 인자 표현식 1줄(원본 L715), (3) `case BothReady:` 본문의 같은 표현식 1줄(원본 L719). `switch (eGate)` 블록 자체(case 라벨들, 다른 case 본문, `default:` 부재 등)는 오늘 quick-260819-hyk 가 6-경로 bool-매핑 표로 이미 정밀 검증한 구역이라 **1바이트도 건드리지 않는다** — `ResolveCrossZGate`/`TakeCrossZRoleImageIfFirst` 두 메서드 자신의 시그니처도 무변경.

⚠ **효율 지침(사용자 명시)**: 스크래치 git 저장소/실측 시뮬레이션 없이, 현재 파일을 Read/Grep 으로 직접 확인 후 old_string/new_string 을 손으로 줄 단위 나열해 카운트하는 방식으로 최종 줄수(1777)를 결정론적으로 산출했다(스크래치 적용 없이 순수 산술). 실행 단계에서도 이 값을 그대로 신뢰하고 재검증할 필요 없다 — 단, Task 0 사전 확인에서 old_string 매치 여부(정확히 1건)만 grep 으로 재확인한다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md

### 착수 시점 고정값 (플래너 실측, 이번 세션)

| 항목 | 값 |
|---|---|
| HEAD | **`74957dd`** |
| 워킹트리 | ` M WPF_Example/DatumMeasurement.csproj` 1건뿐(커밋 금지 로컬 설정 — 항상 존재) |
| 대상 파일 | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — **1771줄**, UTF-8 BOM 있음, LF |
| `ProcessCrossZCaptureTick` | 원본 L1622-1657(41줄, 주석 5줄+시그니처 1줄+여는중괄호 1줄+본문 33줄+닫는중괄호 1줄), 외부 호출부 정확히 1곳: L683 |
| `EvaluateCrossZGate` | L659-724, 이번 플랜이 건드리는 범위는 L670-688(19줄)과 L715/L719(각 1줄)뿐 |
| `ShotMeasureAccumulator`(스타일 전례) | L60-67, `private class` + K&R(여는 중괄호 같은 줄) + `public` 필드(프로퍼티 아님) |
| baseline grep 카운트(변경 전, 플래너 실측) | `TakeCrossZRoleImageIfFirst(` =3(정의1+호출2), `ResolveCrossZGate(` =2(정의1+호출1), `IsZIndexMisconfigured(` =2(정의1+호출1), `bNonProtocolCycle` =7(전역, 이번 플랜 무변경 대상), `ProcessCrossZCaptureTick(` =2(정의1+호출1) |
| 앵커 유일성 확인(플래너 실측) | `bool bRelevant = false;`=1, `case ECrossZGate.HalfPending:`=1, `case ECrossZGate.BothReady:`=1, `private void ProcessCrossZCaptureTick(`=1 — 4곳 모두 유일 매치 |
| 예상 최종 줄수 | **1777**(1771+6) — Edit A(-1)+Edit B(0)+Edit C(0)+Edit D(+7), 손계산 실측(아래 각 Edit 의 old/new 코드블록 줄수를 직접 세어 합산) |

### Edit A 대상 — EvaluateCrossZGate 진입부~ResolveCrossZGate 호출 원문 (old_string, 19줄)

```csharp
                bool bRelevant = false;
                bool bCaptureOk = false;
                bool bCompleted = false;
                string szCapturedRoleKey = null;
                bool bNonProtocolCycle = false;
                ECrossZGate eGate;
                bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);
                if (bMisconfigured)
                {
                    eGate = ECrossZGate.Misconfigured;
                }
                else
                {
                    ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2, out bRelevant, out bCaptureOk, out bCompleted, out szCapturedRoleKey);
                    // 수동으로 RUN 버튼을 눌러 검사할 때는 한 번만 찍어서 크로스-Z 두 장이 절대 안 모인다 —
                    //  조용히 넘어가면 "안 잰 것도 합격"되는 버그가 있어 NG 처리한다.
                    // 자동(PLC) 촬영은 다음 번에 나머지가 채워지니 그냥 기다린다.
                    bNonProtocolCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle();
                    eGate = ResolveCrossZGate(bRelevant, bCaptureOk, bCompleted);
```

### Edit A 결과 — new_string (18줄, 플래너가 손계산으로 줄수 실측)

```csharp
                //260819 hbk quick-260819-sgg: ProcessCrossZCaptureTick 4-out → CrossZCaptureTickResult. tickResult 를
                //  all-default 로 미리 선언 — bMisconfigured 분기는 ProcessCrossZCaptureTick 을 안 부르므로 원본 초기값과 동치.
                CrossZCaptureTickResult tickResult = new CrossZCaptureTickResult();
                bool bNonProtocolCycle = false;
                ECrossZGate eGate;
                bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);
                if (bMisconfigured)
                {
                    eGate = ECrossZGate.Misconfigured;
                }
                else
                {
                    tickResult = ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2);
                    // 수동으로 RUN 버튼을 눌러 검사할 때는 한 번만 찍어서 크로스-Z 두 장이 절대 안 모인다 —
                    //  조용히 넘어가면 "안 잰 것도 합격"되는 버그가 있어 NG 처리한다.
                    // 자동(PLC) 촬영은 다음 번에 나머지가 채워지니 그냥 기다린다.
                    bNonProtocolCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle();
                    eGate = ResolveCrossZGate(tickResult.Relevant, tickResult.CaptureOk, tickResult.Completed);
```

### Edit B 대상 — case HalfPending 본문 원문 (old_string, 4줄 — switch 블록 나머지는 절대 건드리지 않음)

```csharp
                    case ECrossZGate.HalfPending:
                        TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage);
                        MarkCrossZHalfPending(meas, parentSeq2, bNonProtocolCycle, ref acc.FaiAllPass, ref acc.MeasuredCount);
                        return false;
```

### Edit B 결과 — new_string (4줄, 2번째 줄의 인자 표현식만 변경)

```csharp
                    case ECrossZGate.HalfPending:
                        TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage);
                        MarkCrossZHalfPending(meas, parentSeq2, bNonProtocolCycle, ref acc.FaiAllPass, ref acc.MeasuredCount);
                        return false;
```

### Edit C 대상 — case BothReady 본문 원문 (old_string, 3줄 — switch 블록 나머지는 절대 건드리지 않음)

```csharp
                    case ECrossZGate.BothReady:
                        TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage);
                        return true; // 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)
```

### Edit C 결과 — new_string (3줄, 2번째 줄의 인자 표현식만 변경)

```csharp
                    case ECrossZGate.BothReady:
                        TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage);
                        return true; // 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)
```

### Edit D 대상 — ProcessCrossZCaptureTick 원문 (old_string, 41줄 그대로 — 주석 5줄+시그니처+여는중괄호+본문33줄+닫는중괄호)

```csharp
        // 크로스-Z 촬영 한 번(tick)을 처리한다 — 이 측정과 무관한지 / 촬영에 실패했는지 / A·B 가
        //  다 모였는지 세 가지로 판정한다. 새로 촬영하지 않고 이미 찍어둔 사진을 재사용하는 게
        //  기본이다(교시 경로가 지정돼 있으면 그 파일을 대신 읽는다).
        // 이번에 실제로 캡처된 이미지가 어느 쪽(A/B)인지 호출부에 알려준다 — 화면/저장이 항상
        //  고정 이미지만 보여주던 문제를 막기 위해, 호출부가 이 정보로 실제 측정 이미지를 표시/저장한다.
        private void ProcessCrossZCaptureTick(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2, out bool bRelevant, out bool bCaptureOk, out bool bCompleted, out string szCapturedRoleKey)
        {
            bRelevant = false;
            bCaptureOk = false;
            bCompleted = false;
            szCapturedRoleKey = null;
            if (parentSeq2 == null || ShotParam == null)
            {
                return;
            }
            int nCurZ = parentSeq2.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == dualMeas.ZIndexA;
            bool bIsRoleB = nCurZ == dualMeas.ZIndexB;
            bRelevant = bIsRoleA || bIsRoleB;
            if (!bRelevant)
            {
                return; // 이 tick 은 이 측정의 ZIndexA/B 어느 쪽도 아님 — 상태변화 없음(안전망)
            }
            string baseKey = BuildCrossZMeasurementKey(dualMeas);
            string roleKey;
            if (bIsRoleA) roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            else roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            using (HImage capturedImage = LoadCrossZRoleImage(bIsRoleA, dualMeas))
            {
                if (capturedImage == null)
                {
                    return; // 캡처 실패 — 호출부가 NG 처리
                }
                parentSeq2.StoreCrossZImage(roleKey, capturedImage);
                bCaptureOk = true;
                szCapturedRoleKey = roleKey;
            }
            string keyA = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            string keyB = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            bCompleted = parentSeq2.HasCrossZImage(keyA) && parentSeq2.HasCrossZImage(keyB);
        }
```

### Edit D 결과 — new_string (48줄, 클래스 신설 8줄 + 빈줄 1줄 + 기존 주석 5줄 + 시그니처/중괄호 2줄 + 본문 32줄, 플래너가 손계산으로 줄수 실측)

```csharp
        //260819 hbk quick-260819-sgg: ProcessCrossZCaptureTick 의 4개 out 파라미터를 이름 있는 필드로 교체 —
        //  파일 상단 ShotMeasureAccumulator 와 동일한 필드(프로퍼티 아님)+K&R 스타일을 따른다.
        private class CrossZCaptureTickResult {
            public bool Relevant;
            public bool CaptureOk;
            public bool Completed;
            public string CapturedRoleKey;
        }

        // 크로스-Z 촬영 한 번(tick)을 처리한다 — 이 측정과 무관한지 / 촬영에 실패했는지 / A·B 가
        //  다 모였는지 세 가지로 판정한다. 새로 촬영하지 않고 이미 찍어둔 사진을 재사용하는 게
        //  기본이다(교시 경로가 지정돼 있으면 그 파일을 대신 읽는다).
        // 이번에 실제로 캡처된 이미지가 어느 쪽(A/B)인지 호출부에 알려준다 — 화면/저장이 항상
        //  고정 이미지만 보여주던 문제를 막기 위해, 호출부가 이 정보로 실제 측정 이미지를 표시/저장한다.
        private CrossZCaptureTickResult ProcessCrossZCaptureTick(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)
        {
            CrossZCaptureTickResult result = new CrossZCaptureTickResult();
            if (parentSeq2 == null || ShotParam == null)
            {
                return result;
            }
            int nCurZ = parentSeq2.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == dualMeas.ZIndexA;
            bool bIsRoleB = nCurZ == dualMeas.ZIndexB;
            result.Relevant = bIsRoleA || bIsRoleB;
            if (!result.Relevant)
            {
                return result; // 이 tick 은 이 측정의 ZIndexA/B 어느 쪽도 아님 — 상태변화 없음(안전망)
            }
            string baseKey = BuildCrossZMeasurementKey(dualMeas);
            string roleKey;
            if (bIsRoleA) roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            else roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            using (HImage capturedImage = LoadCrossZRoleImage(bIsRoleA, dualMeas))
            {
                if (capturedImage == null)
                {
                    return result; // 캡처 실패 — 호출부가 NG 처리
                }
                parentSeq2.StoreCrossZImage(roleKey, capturedImage);
                result.CaptureOk = true;
                result.CapturedRoleKey = roleKey;
            }
            string keyA = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            string keyB = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            result.Completed = parentSeq2.HasCrossZImage(keyA) && parentSeq2.HasCrossZImage(keyB);
            return result;
        }
```
</context>

<tasks>

<task type="auto">
  <name>Task 1: ProcessCrossZCaptureTick out 4개 → CrossZCaptureTickResult 클래스 리턴값 전환 [SGG-01]</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
### 0. 착수 전 재확인
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
wc -l "$F"   # 기대 1771
grep -cF 'private void ProcessCrossZCaptureTick(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2, out bool bRelevant, out bool bCaptureOk, out bool bCompleted, out string szCapturedRoleKey)' "$F"   # 기대 1
grep -cF 'ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2, out bRelevant, out bCaptureOk, out bCompleted, out szCapturedRoleKey);' "$F"   # 기대 1
grep -cF 'CrossZCaptureTickResult' "$F"   # 기대 0 (아직 미생성 — 자기참조 오염 사전 확인)
```
줄번호가 계획 시점(원본 L659-724 / L1622-1657)과 다르면 grep -n 으로 실제 위치를 재탐색하되, 아래 old_string 텍스트 자체(context 섹션의 "Edit A/B/C/D 대상")는 그대로 사용 — 내용은 변형하지 않는다. 각 old_string 은 grep -cF 로 정확히 1건 매치되는지 먼저 확인할 것(플래너가 이미 사전 확인 완료 — 재확인만).

### 1. Edit 도구로 4개 치환 (순서 무관, 서로 겹치는 구간 없음)

- **Edit A**: old_string = context 섹션 "Edit A 대상"(19줄) 그대로. new_string = "Edit A 결과"(18줄) 그대로.
- **Edit B**: old_string = context 섹션 "Edit B 대상"(4줄) 그대로. new_string = "Edit B 결과"(4줄, 2번째 줄만 인자 표현식 변경) 그대로.
- **Edit C**: old_string = context 섹션 "Edit C 대상"(3줄) 그대로. new_string = "Edit C 결과"(3줄, 2번째 줄만 인자 표현식 변경) 그대로.
- **Edit D**: old_string = context 섹션 "Edit D 대상"(41줄) 그대로. new_string = "Edit D 결과"(48줄, 클래스 신설 + out→리턴값 전환) 그대로.

⚠ Edit B/C 는 `switch (eGate)` 블록의 딱 2줄(TakeCrossZRoleImageIfFirst 호출)만 바꾼다 — case 라벨, `MarkCrossZHalfPending` 호출, `return` 문, 다른 어떤 case 도 손대지 않는다(quick-260819-hyk 검증 구역 보존).
⚠ `ResolveCrossZGate`/`TakeCrossZRoleImageIfFirst` 두 메서드 자신의 정의(시그니처)는 이 파일 어디에서도 수정하지 않는다 — 호출부 인자 표현식만 바뀐다.
⚠ Edit D 에서 `CrossZCaptureTickResult` 는 `class`(struct 아님) — 필드는 `public` 필드(프로퍼티 아님), 여는 중괄호는 클래스 선언과 같은 줄(K&R) — 파일 상단 `ShotMeasureAccumulator`(L60-67) 스타일 그대로.
⚠ `ProcessCrossZCaptureTick` 내부 지역변수명은 `result`(호출부의 `tickResult` 와 다른 이름 — 서로 다른 스코프이므로 이름이 같을 필요 없음, 헝가리언/삼항 규칙 위반 아님).

### 2. 커밋 (대상 파일 1개만 경로 지정 스테이징)
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only   # 반드시 1줄만 출력되는지 확인 후 커밋
git commit -m "refactor(260819-sgg): ProcessCrossZCaptureTick out 4개를 CrossZCaptureTickResult 로 교체"
```
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

echo "== 줄수(결정론적, wc -l) ==" && \
[ "$(wc -l < "$F" | tr -d ' ')" = "1777" ] && echo "  OK 1777줄" && \

echo "== 카운트(자기참조 오염 주의) ==" && \
[ "$(grep -oF 'ProcessCrossZCaptureTick(' "$F" | wc -l)" = "2" ] && echo "  OK ProcessCrossZCaptureTick( = 2 (선언1+호출1, 무변경)" && \
[ "$(grep -cF 'class CrossZCaptureTickResult' "$F")" = "1" ] && echo "  OK class CrossZCaptureTickResult 선언 정확히 1건" && \
[ "$(grep -oF 'tickResult.' "$F" | wc -l)" = "7" ] && echo "  OK tickResult. 필드읽기 정확히 7건(Resolve 3 + HalfPending 2 + BothReady 2)" && \
[ "$(grep -cF 'out bool bRelevant' "$F")" = "0" ] && [ "$(grep -cF 'out bool bCaptureOk' "$F")" = "0" ] && [ "$(grep -cF 'out bool bCompleted' "$F")" = "0" ] && [ "$(grep -cF 'out string szCapturedRoleKey' "$F")" = "0" ] && echo "  OK out 파라미터 4개 완전 제거" && \

echo "== 시그니처/자매 메서드 무변경 ==" && \
[ "$(grep -oF 'TakeCrossZRoleImageIfFirst(' "$F" | wc -l)" = "3" ] && echo "  OK TakeCrossZRoleImageIfFirst( = 3 (정의1+호출2, 무변경)" && \
[ "$(grep -oF 'ResolveCrossZGate(' "$F" | wc -l)" = "2" ] && echo "  OK ResolveCrossZGate( = 2 (정의1+호출1, 무변경)" && \
[ "$(grep -oF 'IsZIndexMisconfigured(' "$F" | wc -l)" = "2" ] && echo "  OK IsZIndexMisconfigured( = 2 (정의1+호출1, 무변경)" && \
[ "$(grep -oF 'bNonProtocolCycle' "$F" | wc -l)" = "7" ] && echo "  OK bNonProtocolCycle = 7 (전역 무변경)" && \
[ "$(grep -c 'private bool ResolveCrossZGate(bool bRelevant, bool bCaptureOk, bool bCompleted)' "$F")" = "1" ] && echo "  OK ResolveCrossZGate 시그니처(3-bool) 무변경" && \

echo "== switch 블록 보존(case 라벨 5개 전부 그대로) ==" && \
[ "$(grep -cF 'case ECrossZGate.Misconfigured:' "$F")" = "1" ] && \
[ "$(grep -cF 'case ECrossZGate.NotMyTick:' "$F")" = "1" ] && \
[ "$(grep -cF 'case ECrossZGate.CaptureFailed:' "$F")" = "1" ] && \
[ "$(grep -cF 'case ECrossZGate.HalfPending:' "$F")" = "1" ] && \
[ "$(grep -cF 'case ECrossZGate.BothReady:' "$F")" = "1" ] && echo "  OK switch case 라벨 5개 전부 무변경" && \
[ "$(grep -oF 'MarkCrossZHalfPending(' "$F" | wc -l)" = "1" ] && echo "  OK MarkCrossZHalfPending( 호출 무변경" && \

echo "== 외부 호출부 무변경(EvaluateCrossZGate 자신의 시그니처) ==" && \
[ "$(grep -cF 'private bool EvaluateCrossZGate(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc,' "$F")" = "1" ] && echo "  OK EvaluateCrossZGate 시그니처 무변경" && \

echo "== 인코딩/한글 보존 ==" && \
[ "$(head -c 3 "$F" | xxd -p)" = "efbbbf" ] && echo "  OK UTF-8 BOM 유지" && \
[ "$(grep -c $'\r' "$F")" = "0" ] && echo "  OK LF 유지(CRLF 오염 없음)" && \

echo "== 위생 ==" && \
[ "$(git show --name-only --format='' HEAD | grep -c .)" = "1" ] && \
[ "$(git status --porcelain)" = " M WPF_Example/DatumMeasurement.csproj" ] && echo "  OK 파일1개, csproj unstaged" && \

echo "== (정보용, 하드게이트 아님) numstat ==" && \
git diff --numstat HEAD~1 HEAD -- "$F"
```
    </automated>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSB" WPF_Example/DatumMeasurement.csproj -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\sgg-t1\\" -v:minimal -nologo > "$SCR/sgg-t1-build.log" 2>&1
[ "$(grep -c ': error ' "$SCR/sgg-t1-build.log")" = "0" ] && [ "$(grep -c ': warning CS' "$SCR/sgg-t1-build.log")" = "12" ] && echo "BUILD PASS (error0/warning12, clean Rebuild)"
```
    </automated>
    <automated>
```bash
# 두 case(HalfPending/BothReady) 호출식이 완전히 동일한 문자열로 치환됐는지 직접 대조(byte-identical 대용)
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
[ "$(grep -oF 'TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage);' "$F" | wc -l)" = "2" ] && echo "  OK 두 case 모두 완전히 동일한 호출식으로 치환(HalfPending+BothReady)"
[ "$(grep -oF 'bCaptureOk, szCapturedRoleKey' "$F" | wc -l)" = "0" ] && echo "  OK 옛 지역변수 조합(bCaptureOk, szCapturedRoleKey) 잔존 0건"
```
    </automated>
  </verify>
  <done>CrossZCaptureTickResult 클래스 신설(ShotMeasureAccumulator 와 동일 스타일: class+public 필드+K&R). ProcessCrossZCaptureTick 이 out 4개 대신 CrossZCaptureTickResult 를 반환하도록 전환, 원본과 동일한 4개 return 지점/조건/부수효과 보존. EvaluateCrossZGate 호출부가 tickResult 지역변수로 재배선(ResolveCrossZGate 3-인자 + TakeCrossZRoleImageIfFirst 2곳 각 2-인자). switch 블록의 case 라벨/다른 본문은 완전 무변경. ResolveCrossZGate/TakeCrossZRoleImageIfFirst 자신의 시그니처 무변경. 파일 1777줄. 빌드 error0/warning12(clean Rebuild). 파일 1개만 커밋, csproj unstaged 유지.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

이 플랜은 순수 내부 리팩토링(out 파라미터 4개 → 이름 있는 필드를 가진 클래스 리턴값)으로, 신뢰 경계를 넘는 입력·외부 통신·권한 변경이 없다. 참고용으로 기존 경계만 기록한다.

| Boundary | Description |
|----------|--------------|
| Halcon 캡처 결과(bCaptureOk/CapturedRoleKey) → NG 판정(SkipReason.NO_IMAGE) 경로 | 캡처 실패/완성 신호가 판정 게이트(FAI NG)로 흘러가는 경로 — 이번 변경은 이 신호를 담는 그릇(out 4개 → 클래스 필드 4개)만 바꾸고 신호 자체의 계산/전달 조건은 1도 바꾸지 않음 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-------------------|
| T-sgg-01 | T (변조) | EvaluateCrossZGate 의 `tickResult` 선초기화 | mitigate | must_haves 에서 `bMisconfigured` 분기일 때 `ProcessCrossZCaptureTick` 이 호출되지 않아도 `tickResult` 가 all-default(Relevant=false/CaptureOk=false/Completed=false/CapturedRoleKey=null) 인스턴스로 미리 선언돼 있는지 grep+빌드로 검증 — out 4개의 원본 선초기화(false/false/false/null)와 동치가 깨지면 Misconfigured 분기 이후 다른 tick 에서 미묘한 stale 값 참조 위험이 생기므로(현재는 switch 의 Misconfigured case 가 tickResult 를 읽지 않아 실질 영향 없지만, 구조적 동치성을 하드 검증) |
| T-sgg-02 | I (정보노출/오작동) | switch(eGate) 의 case HalfPending/BothReady 두 줄만 정확히 치환 | mitigate | must_haves + grep 기반 case 라벨 5개 전수 대조(무변경 확인) + 두 case 의 TakeCrossZRoleImageIfFirst 호출식이 완전히 동일한 문자열로 치환됐는지 직접 카운트 검증 — 오늘 quick-260819-hyk 가 6-경로 bool-매핑 표로 이미 정밀 검증한 구역이므로, 실수로 다른 줄을 건드리면 그 검증이 무효화되기 때문 |

</threat_model>

<verification>

### 실패 시 대응
- **Edit old_string 매치 실패** → 원문이 계획 시점과 달라졌다는 뜻. grep -n 으로 실제 위치를 재탐색해 old_string 을 실제 원문으로 재구성(내용 자체는 절대 변형하지 말 것). 매치가 2건 이상 나오면 즉시 중단 — old_string 범위를 넓혀 유일 매치가 되도록 조정.
- **줄수(wc -l) 불일치** → new_string 을 실수로 다르게 작성했다는 뜻. git diff 로 실제 삽입/삭제된 줄을 눈으로 대조해 원인 파악 후 수정. 기대값을 몰래 완화하지 않는다.
- **`tickResult.` 카운트(7) 불일치** → Edit B/C 중 하나가 누락됐거나 Edit A 의 ResolveCrossZGate 인자 3개 중 일부가 안 바뀐 것. git diff 로 3개 Edit 모두 적용됐는지 확인.
- **BOM/LF 손상 감지** → 즉시 중단하고 git diff 로 손상 범위 확인 후 보고(자동 복구 시도 금지).
- **빌드 산출물 잠김** → OutputPath 이름만 바꿔 재시도. **프로세스 종료 금지.**

### 런타임 UAT
정적 검증(grep 카운트+wc -l+빌드+switch 블록 byte-identical 대조는 플래너가 사전 실측)만으로 회귀 0 을 주장한다 — 순수 out→리턴값 전환이라 판정 로직 접근 없음. 실기 확인이 필요하면 크로스-Z 측정(ZIndexA/B 둘 다 -1 아닌 DualImageEdgeDistanceMeasurement 1개 이상 포함된 레시피)이 있는 Shot 을 프로토콜 사이클로 2회(A/B 각 1회) 촬영해, 이전과 동일하게 두 번째 촬영에서 BothReady 판정 및 측정 실행이 이어지는지, 수동 RUN 1회만 눌렀을 때 이전과 동일하게 NotMyTick/HalfPending 경로에서 NG 처리(bNonProtocolCycle 분기)가 그대로 동작하는지 확인.

</verification>

<success_criteria>
- `CrossZCaptureTickResult` 클래스 신설(`private class`, `public` 필드 4개: Relevant/CaptureOk/Completed/CapturedRoleKey) — `ShotMeasureAccumulator` 와 동일한 K&R+필드 스타일
- `ProcessCrossZCaptureTick` 시그니처가 `out` 파라미터 0개, `CrossZCaptureTickResult` 반환으로 전환 — 원본과 동일한 4개 return 지점/조건/부수효과(StoreCrossZImage 등) 보존
- `EvaluateCrossZGate` 호출부가 `tickResult` 지역변수(all-default 선초기화)로 재배선 — `bMisconfigured` 분기에서는 여전히 `ProcessCrossZCaptureTick` 호출 안 함
- `switch (eGate)` 블록은 `case HalfPending`/`case BothReady` 의 `TakeCrossZRoleImageIfFirst` 인자 표현식 2줄만 변경, 그 외 case 라벨/본문/`default:` 부재 전부 byte-identical
- `ResolveCrossZGate`/`TakeCrossZRoleImageIfFirst` 두 메서드 자신의 시그니처 무변경(호출부 인자 표현식만 변경)
- `wc -l` 최종 줄수 정확 일치(1771 → 1777), 빌드 error0/warning12(clean Rebuild)
- `Action_FAIMeasurement.cs` 단 1개 파일만 1커밋으로 변경, `DatumMeasurement.csproj` 는 끝까지 unstaged
- UTF-8 BOM 유지 + LF 개행 유지(CRLF 오염 0건) + 한글 주석/문자열 손상 0건
- 신규 코드 삼항 `?:` 0건, C# 7.2, 이 파일 기존 스타일(클래스=K&R, 메서드=Allman) 그대로
</success_criteria>

<output>
완료 후 `.planning/quick/260819-sgg-fai-refactor-bundle-d/260819-sgg-SUMMARY.md` 작성(Edit/Write 도구 사용 — heredoc 금지, 한글 인코딩 보존).
</output>
