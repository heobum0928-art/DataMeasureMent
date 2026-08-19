---
phase: quick-260819-sxj
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [SXJ-01]

must_haves:
  truths:
    - "[SXJ-01] `IsZIndexMisconfigured`(원본 L1380, 정확히 1개 외부 호출부: `EvaluateCrossZGate` L675)와 `IsDatumZIndexMisconfigured`(원본 L1455, 정확히 1개 외부 호출부: `ProcessDatumDualImage` L269)의 본문이 새 공용 private 메서드 `IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)` 로 위임되고, 두 원본 메서드 자신의 시그니처(파라미터 타입/이름/개수)와 외부 호출부 표현식은 1글자도 바뀌지 않는다."
    - "유일한 실질 차이인 `bBothUnset` 가드(원본 `IsDatumZIndexMisconfigured` L1464-1468)가 새 공용 메서드에 그대로 보존된다 — 이 가드를 실수로 빠뜨리면 크로스-Z 를 쓰지 않는(둘 다 -1) 일반 Datum 전부가 오설정으로 오판정되는 회귀가 생긴다(빌드는 통과하지만 판정만 조용히 깨짐 — `IsDatumZIndexMisconfigured` 의 유일한 호출부 `ProcessDatumDualImage` 는 모든 Datum 에 대해 무조건 호출되므로, 대부분을 차지하는 '크로스-Z 미사용(-1/-1)' Datum 전부가 영향권이다)."
    - "구조적 동치 증명 2건(기계적 diff, 손 판독 아님) — (1) 공용 메서드 본문(21줄) 은 원본 `IsDatumZIndexMisconfigured` 본문(HEAD L1457-1477, `datum.ZIndexA`→`zIndexA`/`datum.ZIndexB`→`zIndexB` 치환)과 byte-identical. (2) 공용 메서드 본문에서 `bBothUnset` 가드 5줄(bool 선언+if+{+return false+}) 을 제외한 나머지 16줄은 원본 `IsZIndexMisconfigured` 본문(HEAD L1382-1397, `dualMeas.ZIndexA`→`zIndexA`/`dualMeas.ZIndexB`→`zIndexB`/`parentSeq2`→`parentSeq` 치환)과 byte-identical. 이 2건이 통과하면 병합 로직 = 'Datum 버전 원문 그대로'이자 동시에 '= Dual 버전 원문 + 가드 5줄만 추가'라는 것이 텍스트 레벨에서 증명된다."
    - "5-케이스 진리표가 병합 코드에서 원본 두 함수 각각의 동작과 정확히 일치함을 실행자가 병합된 실제 코드를 손으로 짚어 확인하고 SUMMARY.md 에 표 그대로 기록한다(아래 진리표 참조) — 자동 diff 증명 2건이 '텍스트 동치'를, 이 트레이스가 '의미 동치'를 보강한다. 특히 (a) 케이스는 가드가 없었다면 `bSameValue = (-1 == -1)` 이 `true` 가 되어 오판정됐을 자리라는 점을 명시적으로 확인한다."
    - "제어흐름 순서 불변식 — `if (bSingleSet)` → `if (bBothUnset)` → `if (bSameValue)` → 존재확인(`bAExists` 선언) 이 공용 메서드 안에서 이 순서 그대로(줄번호 오름차순)다. 순서가 바뀌면(특히 `bBothUnset` 가 `bSameValue` 뒤로 밀리면) -1/-1 입력이 `bSameValue` 단계에서 먼저 `true` 로 오판정된다 — `grep -n` 줄번호 비교로 검증."
    - "그레프 카운트 무변경/신규 — `IsZIndexMisconfigured(` = 2(선언1+호출1, 병합 전과 동일 카운트), `IsDatumZIndexMisconfigured(` = 2(선언1+호출1, 병합 전과 동일 카운트), `IsCrossZIndexPairMisconfigured(` = 3(신규 선언1+위임호출2)."
    - "빌드 PASS — `error CS` 0건, `warning CS` 정확히 12건(baseline, CS0618×10+CS0162×2) 유지. 신규 CS0219/CS0168/CS0103/CS0161 0건."
    - "파일 최종 줄수 — **1775**줄(1777-2). 내역: Edit A(원본 22줄→신규 39줄, 순증가 **+17**: 공용메서드 24줄[시그니처1+{1+본문21+}1]+안내주석6줄+빈줄1줄+얇은래퍼8줄[기존주석3+신규주석1+시그니처1+{1+본문1+}1]) + Edit B(원본 27줄→신규 8줄, 순감소 **-19**: 기존주석3+신규주석1+시그니처1+{1+본문1+}1). 플래너가 old_string/new_string 각각을 줄 단위로 손계산해 합산한 결정론적 값."
    - "`Action_FAIMeasurement.cs` 단 1개 파일만 변경(단일 커밋). `WPF_Example/DatumMeasurement.csproj`(로컬 미커밋 오염, 항상 존재)는 커밋 후에도 git status 에 unstaged `M` 으로 남는다 — `git add` 는 대상 파일 경로 직접 지정만 사용, `git add -A`/`-a` 금지."
    - "삼항 `?:` 신규 도입 0건, C# 7.2, Allman 브레이스 스타일(이 구역 기존 스타일 그대로: 메서드 여는 중괄호는 자기 줄) 유지. 파일 인코딩 손상 0건(UTF-8 BOM 유지, LF 개행 유지, CRLF 유입 0건), 한글 주석 손상 0건. Edit 도구만 사용(bash/python heredoc 금지, 한글 텍스트 작성 시 특히)."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "IsCrossZIndexPairMisconfigured 공용 메서드 신설 — IsZIndexMisconfigured/IsDatumZIndexMisconfigured 가 이 메서드로 위임(시그니처/호출부 무변경)"
      contains: "private bool IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)"
  key_links:
    - from: "IsZIndexMisconfigured"
      to: "IsCrossZIndexPairMisconfigured"
      via: "얇은 위임 호출(본문 1줄)"
      pattern: "return IsCrossZIndexPairMisconfigured\\(dualMeas\\.ZIndexA, dualMeas\\.ZIndexB, parentSeq2\\);"
    - from: "IsDatumZIndexMisconfigured"
      to: "IsCrossZIndexPairMisconfigured"
      via: "얇은 위임 호출(본문 1줄)"
      pattern: "return IsCrossZIndexPairMisconfigured\\(datum\\.ZIndexA, datum\\.ZIndexB, parentSeq\\);"
---

<objective>
`Action_FAIMeasurement.cs`(오늘 8차례 리팩토링 완료 — fik/gf1/hyk/j6j/q9t/rle/s05/sgg, 전부 "동작 무변경" 검증됨, HEAD=`87cea82`, 현재 **1777줄**) 사용자 요청 Bundle E — **오늘 백로그 중 최고 위험 항목**:

`IsZIndexMisconfigured`(L1380)와 `IsDatumZIndexMisconfigured`(L1455) — 거의 동일한 20여줄짜리 두 메서드를 공용 private 헬퍼 `IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)` 로 병합한다.

**두 함수의 유일한 실질 차이**: `IsDatumZIndexMisconfigured` 에만 있는 `bBothUnset` 가드(둘 다 -1 이면 조기 `return false`). 이 가드는 버그가 아니라 **호출부 전제 차이 때문에 존재**한다 — `IsZIndexMisconfigured` 의 유일한 호출부(`EvaluateCrossZGate`)는 이미 "둘 중 하나라도 설정됨"을 확인한 뒤에만 호출하므로 (-1,-1) 입력이 구조적으로 도달 불가능하다. 반면 `IsDatumZIndexMisconfigured` 의 유일한 호출부(`ProcessDatumDualImage`)는 모든 Datum 에 무조건 호출되므로, 크로스-Z 를 쓰지 않는(압도적으로 흔한) (-1,-1) Datum 이 항상 이 경로를 탄다 — 가드가 없으면 전부 오설정 오판정.

**결론**: Datum 버전이 Dual 버전의 상위호환(superset)이다. 가드를 추가해도 `IsZIndexMisconfigured` 쪽은 그 분기에 절대 도달할 수 없으므로 동작 무변화(behaviorally inert), 반면 Dual 버전에는 없던 안전장치가 생긴다. 따라서 가장 안전한 병합은: **primitive 파라미터(int/int/InspectionSequence)를 받는 공용 헬퍼 하나에 Datum 버전의(가드 포함) 로직을 두고, 두 원본 함수는 자기 시그니처를 그대로 유지한 채 헬퍼로 위임하는 얇은 래퍼로 축소**하는 것 — 호출부는 파일 전체에서 단 하나도 바뀌지 않는다.

Purpose: 거의 동일한 두 함수를 안전하게 1개로 합쳐 중복을 제거하되, 두 호출부의 서로 다른 전제(도달 가능한 입력 범위가 다름)를 깨지 않는다. 동작은 하나도 바뀌지 않는다.
Output: 파일 1개 수정(새 파일 0개), 공용 메서드 1개 신설, 커밋 1개.

⚠ **위험 근거(사용자 명시 — 오늘 백로그 최고 위험)**: 이 병합은 "거의 동일"이라는 이유로 `bBothUnset` 가드를 실수로 누락하기 쉬운 함정이다. 누락하면 빌드는 통과하지만 크로스-Z 미사용 Datum 전부가 조용히 오설정 NG 로 오판정된다. 그래서 이 플랜의 검증은 (1) 기계적 diff 동치 증명 2건 + (2) 제어흐름 순서 불변식 + (3) 5-케이스 진리표 수기 트레이스, 3중으로 겹친다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md

### 착수 시점 고정값 (플래너 실측, 이번 세션 — 오케스트레이터의 손분석을 파일 대조로 독립 재검증 완료)

| 항목 | 값 |
|---|---|
| HEAD | **`87cea82`** |
| 워킹트리 | ` M WPF_Example/DatumMeasurement.csproj` 1건뿐(커밋 금지 로컬 설정 — 항상 존재) |
| 대상 파일 | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — **1777줄**, UTF-8 BOM 있음, LF, CRLF 0건 |
| `UNSET_ZINDEX` | L72, `private const int UNSET_ZINDEX = -1;` |
| `DoesZIndexExistInRecipe` | `InspectionSequence.cs` L1343, `public bool DoesZIndexExistInRecipe(int nZIndex)` — int 하나 받음(공용 헬퍼 시그니처와 정합) |
| `ZIndexA`/`ZIndexB` 타입 | `DualImageEdgeDistanceMeasurement.cs`/`DatumConfig.cs` 둘 다 `public int ZIndexA { get; set; } = -1;` 형태(int, 동일 기본값) |
| `IsZIndexMisconfigured` | L1380, 시그니처 `(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)`, 외부 호출부 정확히 1곳: `EvaluateCrossZGate` L675(`bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);`) — 이 호출은 L664 `bHasAnyZIndex = ... != UNSET_ZINDEX \|\| ... != UNSET_ZINDEX` 가 참인 `if(bHasAnyZIndex)` 블록 안에서만 실행됨(즉 (-1,-1) 입력 도달 불가) |
| `IsDatumZIndexMisconfigured` | L1455, 시그니처 `(DatumConfig datum, InspectionSequence parentSeq)`, 외부 호출부 정확히 1곳: `ProcessDatumDualImage` L269(`bool bDatumZIndexMisconfigured = IsDatumZIndexMisconfigured(datum, parentSeq);`) — `VerticalTwoHorizontalDualImage` 타입 Datum 전부에 대해 **무조건** 호출(가드 없이 상태 확인 없음) |
| `IsCrossZIndexPairMisconfigured` | 병합 전 파일 내 출현 **0건**(자기참조 오염 사전 확인) |
| `bBothUnset`/`bAUnset`/`bBUnset` | 병합 전 이 두 함수 밖에서 사용 0건(grep 확인 완료 — 이름 충돌 없음) |

### Edit A 대상 — `IsZIndexMisconfigured` 원문 (old_string, HEAD L1377-1398, 22줄)

```csharp
        //260722 hbk Phase 68 D-05: DualImage 측정의 ZIndexA/ZIndexB 오설정 판정 — 단일설정/동일값/존재하지 않는
        //  z_index 참조 → true. 호출부가 이미 "둘 중 하나라도 설정됨"을 확인한 뒤에만 호출한다(둘 다 -1 미설정인
        //  기존 레시피는 이 검사 자체를 타지 않음 — D-07 회귀 0). 조용한 폴백(ResolveDatumModelPath 의 Shots[0] 류) 금지.
        private bool IsZIndexMisconfigured(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)
        {
            bool bAUnset = dualMeas.ZIndexA == UNSET_ZINDEX;
            bool bBUnset = dualMeas.ZIndexB == UNSET_ZINDEX;
            bool bSingleSet = bAUnset != bBUnset;
            if (bSingleSet)
            {
                return true;
            }
            bool bSameValue = dualMeas.ZIndexA == dualMeas.ZIndexB;
            if (bSameValue)
            {
                return true;
            }
            bool bAExists = parentSeq2 != null && parentSeq2.DoesZIndexExistInRecipe(dualMeas.ZIndexA);
            bool bBExists = parentSeq2 != null && parentSeq2.DoesZIndexExistInRecipe(dualMeas.ZIndexB);
            bool bBothExist = bAExists && bBExists;
            return !bBothExist;
        }
```

### Edit A 결과 — new_string (39줄: 공용메서드 30줄[안내주석6+시그니처1+{1+본문21+}1]+빈줄1+얇은래퍼8줄)

```csharp
        //260819 hbk quick-260819-sxj: IsZIndexMisconfigured / IsDatumZIndexMisconfigured 공용 로직 추출.
        //  두 호출부의 전제가 다르다 — EvaluateCrossZGate 는 호출 전 "둘 중 하나라도 설정됨"을
        //  이미 확인했고(둘 다 -1 인 채로 여기 들어올 수 없음), ProcessDatumDualImage 는 모든 Datum에
        //  대해 무조건 호출한다(둘 다 -1 인 일반 Datum이 훨씬 흔함). 그래서 bBothUnset 가드가
        //  반드시 있어야 하며, 이 가드가 있어도 EvaluateCrossZGate 쪽 동작은 바뀌지 않는다(그 경로는
        //  가드에 도달할 수 없는 입력만 들어오기 때문 — 도달 불가능이지 무관한 게 아니다).
        private bool IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)
        {
            bool bAUnset = zIndexA == UNSET_ZINDEX;
            bool bBUnset = zIndexB == UNSET_ZINDEX;
            bool bSingleSet = bAUnset != bBUnset;
            if (bSingleSet)
            {
                return true;
            }
            bool bBothUnset = bAUnset && bBUnset;
            if (bBothUnset)
            {
                return false; // 미설정(-1/-1) — 게이트 미해당, 기존 static 경로(D-07)
            }
            bool bSameValue = zIndexA == zIndexB;
            if (bSameValue)
            {
                return true;
            }
            bool bAExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(zIndexA);
            bool bBExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(zIndexB);
            bool bBothExist = bAExists && bBExists;
            return !bBothExist;
        }

        //260722 hbk Phase 68 D-05: DualImage 측정의 ZIndexA/ZIndexB 오설정 판정 — 단일설정/동일값/존재하지 않는
        //  z_index 참조 → true. 호출부가 이미 "둘 중 하나라도 설정됨"을 확인한 뒤에만 호출한다(둘 다 -1 미설정인
        //  기존 레시피는 이 검사 자체를 타지 않음 — D-07 회귀 0). 조용한 폴백(ResolveDatumModelPath 의 Shots[0] 류) 금지.
        //260819 hbk quick-260819-sxj: 본문을 IsCrossZIndexPairMisconfigured 로 위임(로직 무변경, 시그니처 무변경).
        private bool IsZIndexMisconfigured(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)
        {
            return IsCrossZIndexPairMisconfigured(dualMeas.ZIndexA, dualMeas.ZIndexB, parentSeq2);
        }
```

### Edit B 대상 — `IsDatumZIndexMisconfigured` 원문 (old_string, HEAD L1452-1478, 27줄)

```csharp
        // 기준점의 A/B 짝 설정 오류 판정 — 하나만 설정됐거나, 같은 값이거나, 존재하지 않는 값을
        //  가리키면 오류다. 단 둘 다 "설정 안 함"(-1)인 경우는 정상이니 먼저 걸러내야 한다 —
        //  안 그러면 -1 과 -1 이 같은 값이라고 오판정한다.
        private bool IsDatumZIndexMisconfigured(DatumConfig datum, InspectionSequence parentSeq)
        {
            bool bAUnset = datum.ZIndexA == UNSET_ZINDEX;
            bool bBUnset = datum.ZIndexB == UNSET_ZINDEX;
            bool bSingleSet = bAUnset != bBUnset;
            if (bSingleSet)
            {
                return true;
            }
            bool bBothUnset = bAUnset && bBUnset;
            if (bBothUnset)
            {
                return false; // 미설정(-1/-1) — 게이트 미해당, 기존 static 경로(D-07)
            }
            bool bSameValue = datum.ZIndexA == datum.ZIndexB;
            if (bSameValue)
            {
                return true;
            }
            bool bAExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(datum.ZIndexA);
            bool bBExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(datum.ZIndexB);
            bool bBothExist = bAExists && bBExists;
            return !bBothExist;
        }
```

### Edit B 결과 — new_string (8줄: 기존주석3+신규주석1+시그니처1+{1+본문1+}1)

```csharp
        // 기준점의 A/B 짝 설정 오류 판정 — 하나만 설정됐거나, 같은 값이거나, 존재하지 않는 값을
        //  가리키면 오류다. 단 둘 다 "설정 안 함"(-1)인 경우는 정상이니 먼저 걸러내야 한다 —
        //  안 그러면 -1 과 -1 이 같은 값이라고 오판정한다.
        //260819 hbk quick-260819-sxj: 본문을 IsCrossZIndexPairMisconfigured 로 위임(로직 무변경, 시그니처 무변경).
        private bool IsDatumZIndexMisconfigured(DatumConfig datum, InspectionSequence parentSeq)
        {
            return IsCrossZIndexPairMisconfigured(datum.ZIndexA, datum.ZIndexB, parentSeq);
        }
```

### 🔴 5-케이스 진리표 — 공용 헬퍼 `IsCrossZIndexPairMisconfigured(zIndexA, zIndexB, parentSeq)` 의 유일한 진실 원본

| # | 입력 | 기대 반환 | 원본 두 함수와의 대응 |
|---|---|---|---|
| (a) | zIndexA=-1, zIndexB=-1 (둘 다 미설정) | **false** | `IsZIndexMisconfigured`: 호출부(`EvaluateCrossZGate`)가 이 입력을 원천 차단(도달 불가) — 가드 분기는 이 함수 입장에선 "죽은 코드처럼 보이지만 안전한 코드"다. `IsDatumZIndexMisconfigured`: 이 케이스가 **주 사용 경로**(크로스-Z 미사용 Datum) — 가드 없으면 (b)로 오판정된다. |
| (b) | 정확히 하나만 -1 (예: zIndexA=-1, zIndexB=0) | **true** | 두 원본 함수 공통 — `bSingleSet` 분기, 병합 전/후 무변경. |
| (c) | 둘 다 설정 + 동일값 (예: zIndexA=zIndexB=2) | **true** | 두 원본 함수 공통 — `bSameValue` 분기, 병합 전/후 무변경. |
| (d) | 둘 다 설정 + 다른값 + 레시피에 둘 다 존재 | **false** | 두 원본 함수 공통 — `bBothExist=true` → `!bBothExist=false`, 병합 전/후 무변경. |
| (e) | 둘 다 설정 + 다른값 + 최소 하나 레시피에 미존재 | **true** | 두 원본 함수 공통 — `bBothExist=false` → `!bBothExist=true`, 병합 전/후 무변경. |

⚠ **(a) 가 유일한 위험 케이스다.** 가드(`bBothUnset` 분기)가 누락된 채 (a) 를 넣으면 `bSameValue = (zIndexA == zIndexB) = (-1 == -1) = true` 가 되어 **true(오설정)** 를 반환한다 — 이것이 정확히 오늘 요청에서 "실수로 가드가 빠지면" 이라고 지목한 회귀다. Diff 증명 2(아래 verify)가 가드 5줄이 실제로 존재함을 기계적으로 이미 반증하지만, 실행자는 커밋 전 이 표를 병합된 실제 코드 옆에 놓고 (a)~(e) 를 손으로 짚어 SUMMARY.md 에 그대로 옮겨 적는다.
</context>

<tasks>

<task type="auto">
  <name>Task 1: IsZIndexMisconfigured / IsDatumZIndexMisconfigured → IsCrossZIndexPairMisconfigured 공용 헬퍼로 병합 [SXJ-01]</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
### 0. 착수 전 재확인 + baseline 스냅샷 (스크래치, 파일 수정 0)
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
mkdir -p "$SCR/sxj"
git rev-parse --short HEAD   # 기대 87cea82
wc -l "$F"                   # 기대 1777
git show 87cea82:"$F" > "$SCR/sxj/base.cs"
diff "$SCR/sxj/base.cs" "$F"   # 빈 출력 기대(워킹트리가 이 파일에서 HEAD와 동일함을 확인)
grep -cF 'private bool IsZIndexMisconfigured(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)' "$F"   # 기대 1
grep -cF 'private bool IsDatumZIndexMisconfigured(DatumConfig datum, InspectionSequence parentSeq)' "$F"   # 기대 1
grep -c 'IsCrossZIndexPairMisconfigured' "$F"   # 기대 0 (자기참조 오염 사전 확인)
```
줄번호가 계획 시점(HEAD L1377-1398 / L1452-1478)과 다르면 grep -n 으로 실제 위치를 재탐색하되, old_string 텍스트 자체(context 섹션 "Edit A/B 대상")는 절대 변형하지 않는다. 각 old_string 은 위 grep -cF 로 정확히 1건 매치되는지 이미 확인됨.

### 1. Edit 도구로 2개 치환 (Edit 도구 사용 — heredoc 금지, 한글 인코딩 보존)
- **Edit A**: old_string = context 섹션 "Edit A 대상"(22줄) 그대로. new_string = "Edit A 결과"(39줄) 그대로.
- **Edit B**: old_string = context 섹션 "Edit B 대상"(27줄) 그대로. new_string = "Edit B 결과"(8줄) 그대로.

⚠ 공용 헬퍼 `IsCrossZIndexPairMisconfigured` 는 `int zIndexA, int zIndexB, InspectionSequence parentSeq` — **primitive 파라미터**여야 한다(DualImageEdgeDistanceMeasurement/DatumConfig 타입을 받지 않는다 — 이게 진짜 "공용"이 되는 핵심 조건).
⚠ `bBothUnset` 가드 5줄(`bool bBothUnset = ...;` / `if (bBothUnset)` / `{` / `return false; // ...` / `}`)을 **절대 누락하지 않는다** — 이 플랜 전체의 존재 이유다.
⚠ 두 원본 함수(`IsZIndexMisconfigured`/`IsDatumZIndexMisconfigured`) 자신의 시그니처는 1글자도 바꾸지 않는다 — 본문만 위임 1줄로 축소.
⚠ 브레이스 스타일은 이 구역 기존 스타일(Allman — 메서드 여는 `{` 는 자기 줄) 그대로. 삼항 `?:` 신규 도입 금지.

### 2. 수동 진리표 트레이스 (필수 — 커밋 전)
context 섹션의 "5-케이스 진리표" (a)~(e) 를, Edit 적용 후 실제 `IsCrossZIndexPairMisconfigured` 코드를 옆에 놓고 한 줄씩 손으로 짚어가며 확인한다. 특히 (a) 는 `bBothUnset` 분기가 `bSameValue` 분기보다 먼저 평가되어 조기 `return false` 로 빠짐을 직접 확인한다. 이 표를 그대로(값 포함) SUMMARY.md 에 옮겨 적는다.

### 3. 커밋 (대상 파일 1개만 경로 지정 스테이징)
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only   # 반드시 1줄만 출력되는지 확인 후 커밋
git commit -m "refactor(260819-sxj): IsZIndexMisconfigured/IsDatumZIndexMisconfigured 를 IsCrossZIndexPairMisconfigured 공용 헬퍼로 병합 (bBothUnset 가드 보존, 시그니처/호출부 무변경)"
```
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/sxj/base.cs"
RC=0
eq()  { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }
dif() { if [ -z "$2" ]; then echo "OK   $1"; else echo "FAIL $1 | diff:"; echo "$2" | head -30; RC=1; fi; }

echo "== 줄수(결정론적, wc -l) =="
eq "final line count 1775" "$(wc -l < "$F" | tr -d ' ')" "1775"

echo "== 시그니처/호출부 무변경 =="
eq "IsZIndexMisconfigured 시그니처 무변경" "$(grep -cF 'private bool IsZIndexMisconfigured(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)' "$F")" "1"
eq "IsDatumZIndexMisconfigured 시그니처 무변경" "$(grep -cF 'private bool IsDatumZIndexMisconfigured(DatumConfig datum, InspectionSequence parentSeq)' "$F")" "1"
eq "EvaluateCrossZGate 호출부 무변경" "$(grep -cF 'bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);' "$F")" "1"
eq "ProcessDatumDualImage 호출부 무변경" "$(grep -cF 'bool bDatumZIndexMisconfigured = IsDatumZIndexMisconfigured(datum, parentSeq);' "$F")" "1"

echo "== 그레프 카운트(선언+호출) =="
eq "IsZIndexMisconfigured( = 2(선언1+호출1)" "$(grep -oF 'IsZIndexMisconfigured(' "$F" | wc -l)" "2"
eq "IsDatumZIndexMisconfigured( = 2(선언1+호출1)" "$(grep -oF 'IsDatumZIndexMisconfigured(' "$F" | wc -l)" "2"
eq "IsCrossZIndexPairMisconfigured( = 3(선언1+위임호출2)" "$(grep -oF 'IsCrossZIndexPairMisconfigured(' "$F" | wc -l)" "3"

echo "== 공용 헬퍼 시그니처(primitive 파라미터) =="
eq "헬퍼 시그니처 정확" "$(grep -cF 'private bool IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)' "$F")" "1"

echo "== 위임 호출식 정확 =="
eq "IsZIndexMisconfigured 위임 1줄" "$(grep -cF 'return IsCrossZIndexPairMisconfigured(dualMeas.ZIndexA, dualMeas.ZIndexB, parentSeq2);' "$F")" "1"
eq "IsDatumZIndexMisconfigured 위임 1줄" "$(grep -cF 'return IsCrossZIndexPairMisconfigured(datum.ZIndexA, datum.ZIndexB, parentSeq);' "$F")" "1"

exit $RC
```
    </automated>
    <automated>
```bash
# 구조적 동치 증명 2건 — 병합 로직이 원본 두 함수의 텍스트와 byte-identical 임을 기계적으로 증명
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/sxj/base.cs"
RC=0
dif() { if [ -z "$2" ]; then echo "OK   $1"; else echo "FAIL $1 | diff:"; echo "$2" | head -30; RC=1; fi; }

S=$(grep -n '^        private bool IsCrossZIndexPairMisconfigured' "$F" | head -1 | cut -d: -f1)
echo "INFO 헬퍼 시그니처 줄번호 S=$S"

# 증명(1): 헬퍼 본문(21줄, S+2~S+22) == 원본 IsDatumZIndexMisconfigured 본문(base L1457-1477, datum.->zIndex 치환)
ACTUAL1=$(sed -n "$((S+2)),$((S+22))p" "$F")
EXPECTED1=$(sed -n '1457,1477p' "$SB" | sed 's/datum\.ZIndexA/zIndexA/g; s/datum\.ZIndexB/zIndexB/g')
dif "diff증명1: 헬퍼본문 == 원본Datum본문(치환)" "$(diff <(printf '%s\n' "$ACTUAL1") <(printf '%s\n' "$EXPECTED1"))"

# 증명(2): 헬퍼 본문에서 bBothUnset 가드 5줄(S+9~S+13) 제외한 16줄 == 원본 IsZIndexMisconfigured 본문(base L1382-1397, dualMeas./parentSeq2 치환)
ACTUAL2=$(sed -n "$((S+2)),$((S+8))p;$((S+14)),$((S+22))p" "$F")
EXPECTED2=$(sed -n '1382,1397p' "$SB" | sed 's/dualMeas\.ZIndexA/zIndexA/g; s/dualMeas\.ZIndexB/zIndexB/g; s/parentSeq2/parentSeq/g')
dif "diff증명2: (헬퍼본문-가드5줄) == 원본Dual본문(치환)" "$(diff <(printf '%s\n' "$ACTUAL2") <(printf '%s\n' "$EXPECTED2"))"

# 가드 5줄 자체가 실제로 그 자리(S+9~S+13)에 있는지 직접 확인
GUARD=$(sed -n "$((S+9)),$((S+13))p" "$F")
EXPGUARD='            bool bBothUnset = bAUnset && bBUnset;
            if (bBothUnset)
            {
                return false; // 미설정(-1/-1) — 게이트 미해당, 기존 static 경로(D-07)
            }'
dif "가드 5줄 존재 확인(위치 S+9~S+13)" "$(diff <(printf '%s\n' "$GUARD") <(printf '%s\n' "$EXPGUARD"))"

exit $RC
```
    </automated>
    <automated>
```bash
# 제어흐름 순서 불변식 — bSingleSet -> bBothUnset -> bSameValue -> bAExists 순서(줄번호 오름차순)
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
RC=0
eq() { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }

eq "if (bSingleSet) 파일 전체 정확히 1건(헬퍼에만 존재)" "$(grep -cF 'if (bSingleSet)' "$F")" "1"
eq "if (bBothUnset) 파일 전체 정확히 1건(헬퍼에만 존재)" "$(grep -cF 'if (bBothUnset)' "$F")" "1"
eq "if (bSameValue) 파일 전체 정확히 1건(헬퍼에만 존재)" "$(grep -cF 'if (bSameValue)' "$F")" "1"

L1=$(grep -nF 'if (bSingleSet)' "$F" | head -1 | cut -d: -f1)
L2=$(grep -nF 'if (bBothUnset)' "$F" | head -1 | cut -d: -f1)
L3=$(grep -nF 'if (bSameValue)' "$F" | head -1 | cut -d: -f1)
L4=$(grep -nF 'bool bAExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(zIndexA);' "$F" | head -1 | cut -d: -f1)
echo "INFO 줄번호 L1(bSingleSet)=$L1 L2(bBothUnset)=$L2 L3(bSameValue)=$L3 L4(bAExists)=$L4"

if [ "$L1" -lt "$L2" ] && [ "$L2" -lt "$L3" ] && [ "$L3" -lt "$L4" ]; then
  echo "OK   순서 불변식: bSingleSet < bBothUnset < bSameValue < bAExists"
else
  echo "FAIL 순서 불변식 깨짐 — bBothUnset 가드가 bSameValue 뒤로 밀리면 (-1,-1) 오판정 회귀"
  RC=1
fi
exit $RC
```
    </automated>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSB" WPF_Example/DatumMeasurement.csproj -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\sxj-t1\\" -v:minimal -nologo > "$SCR/sxj-t1-build.log" 2>&1
[ "$(grep -c ': error ' "$SCR/sxj-t1-build.log")" = "0" ] && [ "$(grep -c ': warning CS' "$SCR/sxj-t1-build.log")" = "12" ] && echo "BUILD PASS (error0/warning12, clean Rebuild)"
```
    </automated>
    <automated>
```bash
echo "== 인코딩/위생 =="
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
RC=0
eq() { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }

eq "UTF-8 BOM 유지" "$(head -c 3 "$F" | xxd -p)" "efbbbf"
eq "LF 유지(CRLF 오염 0)" "$(grep -c $'\r' "$F")" "0"
eq "커밋 파일 1개" "$(git show --name-only --format='' HEAD | grep -c .)" "1"
eq "csproj unstaged 유지" "$(git status --porcelain)" " M WPF_Example/DatumMeasurement.csproj"
exit $RC
```
    </automated>
  </verify>
  <done>`IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)` 공용 헬퍼 신설, `bBothUnset` 가드 보존(구조적 diff 증명 2건 + 순서 불변식으로 검증). `IsZIndexMisconfigured`/`IsDatumZIndexMisconfigured` 는 시그니처·외부 호출부 무변경, 본문만 1줄 위임으로 축소. 5-케이스 진리표 수기 트레이스 완료 및 SUMMARY.md 기록. 파일 1775줄, 빌드 error0/warning12(clean Rebuild), 파일 1개만 커밋, csproj unstaged 유지, 인코딩 손상 0건.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

이 플랜은 순수 내부 리팩토링(거의 동일한 두 private 메서드를 공용 헬퍼로 병합)으로, 신뢰 경계를 넘는 입력·외부 통신·권한 변경이 없다. 참고용으로 기존 경계만 기록한다.

| Boundary | Description |
|----------|--------------|
| 레시피(INI/JSON) 의 ZIndexA/ZIndexB 값 → 오설정 판정 → Datum/측정 NG 처리 경로 | 사용자가 편집 가능한 레시피 값이 판정 게이트(FAI/Datum NG)로 흘러가는 경로 — 이번 변경은 이 판정 로직을 담는 함수 2개를 1개로 합치되, 각 호출부가 원래 갖고 있던 입력 도달범위(reachability) 전제는 1도 바꾸지 않음 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-------------------|
| T-sxj-01 | T (변조) | `IsCrossZIndexPairMisconfigured` 의 `bBothUnset` 가드 누락 | mitigate | must_haves + verify 의 구조적 diff 증명 2건(가드 5줄이 실제로 그 위치에 있는지 byte-level 확인) + 순서 불변식(줄번호 비교) + 5-케이스 진리표 수기 트레이스 — 3중 검증. 가드가 누락되면 빌드는 통과하지만 크로스-Z 미사용 Datum(주 사용 경로) 전부가 조용히 오설정 NG 로 오판정되는 회귀이므로, 정적 검증만으로는 부족해 텍스트 동치 증명까지 요구 |
| T-sxj-02 | I (정보노출/오작동) | `IsZIndexMisconfigured`/`IsDatumZIndexMisconfigured` 두 얇은 래퍼의 시그니처·호출부 | mitigate | must_haves + grep 카운트(선언+호출 각 2건, 병합 전과 동일 카운트) — 병합 후에도 두 함수의 외부 계약(파라미터 타입/이름/개수, 호출부 표현식)이 1글자도 바뀌지 않았음을 증명. 계약이 바뀌면 파일 어딘가의 호출부가 컴파일 에러 없이 조용히 다른 타입을 넘기는 위험이 생기기 때문 |

</threat_model>

<verification>

### 실패 시 대응
- **Edit old_string 매치 실패** → 원문이 계획 시점과 달라졌다는 뜻. `grep -n` 으로 실제 위치를 재탐색해 old_string 을 실제 원문으로 재구성(내용 자체는 절대 변형하지 말 것). 매치가 2건 이상 나오면 즉시 중단.
- **줄수(wc -l) 불일치** → new_string 을 실수로 다르게 작성했다는 뜻. `git diff` 로 실제 삽입/삭제 줄을 눈으로 대조. 기대값을 몰래 완화하지 않는다.
- **diff증명1/2 불일치** → `bBothUnset` 가드가 누락됐거나, 변수명 치환(zIndexA/zIndexB/parentSeq)이 원본 값과 어긋난 것. `sed` 치환 결과를 직접 눈으로 대조해 원인 파악.
- **순서 불변식 FAIL** → 가드가 엉뚱한 위치로 이동한 것. 즉시 중단하고 Edit A 를 재검토.
- **BOM/LF 손상 감지** → 즉시 중단하고 `git diff` 로 손상 범위 확인 후 보고(자동 복구 시도 금지).
- **빌드 산출물 잠김** → `OutputPath` 이름만 바꿔 재시도. **프로세스 종료 금지.**

### 런타임 UAT
정적 검증(diff 증명 2건 + 순서 불변식 + 진리표 트레이스 + 빌드)만으로 회귀 0 을 주장한다 — 판정 로직 자체는 원본 그대로 이동했을 뿐 새로 계산되지 않는다. 실기 확인이 필요하면: (1) 크로스-Z 를 쓰지 않는(ZIndexA/B 둘 다 -1) 일반 Datum 을 포함한 Shot 을 검사해 이전과 동일하게 정상 검출됨을 확인(가드가 살아있다는 실기 증거), (2) ZIndexA/B 가 존재하지 않는 z_index 를 가리키는 DualImage 측정을 하나 만들어 이전과 동일하게 오설정 NG 로 보고되는지 확인.

</verification>

<success_criteria>
- `IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)` 공용 헬퍼 신설 — primitive 파라미터, `bBothUnset` 가드 포함
- `IsZIndexMisconfigured`/`IsDatumZIndexMisconfigured` 시그니처·외부 호출부(각 1곳) 무변경, 본문만 위임 1줄로 축소
- 구조적 diff 증명 2건 PASS — 헬퍼 == 원본 Datum 본문(치환), (헬퍼-가드) == 원본 Dual 본문(치환)
- 제어흐름 순서 불변식 PASS — `bSingleSet` < `bBothUnset` < `bSameValue` < `bAExists`
- 5-케이스 진리표 수기 트레이스 완료, SUMMARY.md 에 표 기록
- `wc -l` 최종 줄수 정확 일치(1777 → 1775), 빌드 error0/warning12(clean Rebuild)
- `Action_FAIMeasurement.cs` 단 1개 파일만 1커밋으로 변경, `DatumMeasurement.csproj` 는 끝까지 unstaged
- UTF-8 BOM 유지 + LF 개행 유지(CRLF 오염 0건) + 한글 주석 손상 0건
- 신규 코드 삼항 `?:` 0건, C# 7.2, 이 파일 기존 스타일(Allman 브레이스) 그대로
</success_criteria>

<output>
완료 후 `.planning/quick/260819-sxj-fai-refactor-bundle-e/260819-sxj-SUMMARY.md` 작성(Edit/Write 도구 사용 — heredoc 금지, 한글 인코딩 보존). 5-케이스 진리표를 실제 확인한 값과 함께 표로 포함할 것.
</output>
