---
phase: quick-260819-tcs
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [TCS-01, TCS-02, TCS-03]

must_haves:
  truths:
    - "[TCS-01] `\"[FAIMeasurement]\"` 리터럴이 박힌 `Logging.PrintLog` 호출 17곳(플래너 실측 — 원본 라인 273/290/320/331/344/370/386/452/742/874/878/1362/1374/1432/1460/1466/1772, 사용자 원 카운트 17과 정확히 일치)이 전부 `private const string LOG_TAG = \"[FAIMeasurement] \";`(공백 포함, 클래스 상단 기존 `private const` 클러스터 — `UNSET_ZINDEX`/`CROSS_Z_ROLE_SUFFIX_A/B`/`CROSS_Z_DATUM_KEY_PREFIX` 바로 다음)를 참조하는 `LOG_TAG + \"...\"` 형태로 치환된다."
    - "[TCS-01] 순수 표기 치환이다 — 17곳 전부 최종 런타임 문자열(콘솔/로그 파일에 찍히는 실제 텍스트)이 치환 전후 1글자도 다르지 않다. 이유: 태그 뒤 공백이 상수 자체(`\"[FAIMeasurement] \"`, 닫는 대괄호와 다음 단어 사이 공백까지 포함)에 이미 포함돼 있어, 각 호출부는 `LOG_TAG + \"Datum '\" + ...` 처럼 공백 없이 바로 다음 단어부터 시작하는 문자열을 이어 붙이면 원본과 완전히 같은 문자열이 재구성된다 — 공백을 호출부마다 손으로 입력할 필요가 없어 17곳 중 실수로 공백이 빠지거나 중복되는 사고를 원천 차단한다(오케스트레이터가 지정한 '가장 실수 적은 방식' 선택)."
    - "[TCS-02] `IsCrossZDatumBothStored`(원본 L1100)/`TryReDetectCrossZDatumFromStore`(원본 L1109) 인라인은 조사 후 실행하지 않기로 결정 — 각각 외부 호출자 정확히 1곳(둘 다 `TryGrabOrLoadCrossZDatumImages` L1038/L1040), 각각 존재 이유를 설명하는 1문장 주석 보유. 인라인 시 이 설명 주석을 호출부(`if (!bRelevant) { ... }` 분기, 이미 비자명함)로 옮기거나 버려야 하는데 둘 다 손해. 사용자 원 요청이 '고려'(선택)로 명시함 — 이 두 함수는 이번 플랜에서 1바이트도 건드리지 않는다."
    - "[TCS-03] `EvaluateCrossZGate`(원본 L659) 내부 `switch (eGate)`(원본 L691-720, 30줄) 은 손대지 않는다 — 대신 `private enum ECrossZGate { ... }` 선언(원본 L46) 바로 위에, `NotMyTick`/`HalfPending` 두 case 가 내부에서 `bNonProtocolCycle`(`parentSeq2.IsProtocolDrivenCycle()` 로 읽음) 이라는 두 번째 불리언을 또 분기하므로 이 enum 이 완전한 상태표가 아니라 상위(top-level) 분류일 뿐임을 명시하는 순수 주석 4줄만 추가한다. `switch (eGate)` 블록 본문·`bNonProtocolCycle` 선언/사용·`IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 세 구역은 오늘 세 번째(hyk→sgg→이번) 재검증이므로, 이번엔 재검증 대신 삽입 지점(파일 상단 46번째 줄 부근) 자체가 이 세 구역과 물리적으로 떨어져 있음을 grep -n 라인번호로 확인하는 것으로 대체한다."
    - "그렙 카운트 — `LOG_TAG` 전체 출현 **18**회(선언1+호출17). `[FAIMeasurement]`(대괄호 포함 리터럴) 전체 출현 **1**회(LOG_TAG 선언 자신의 값 안에만, 호출부 17곳에는 0). `LOG_TAG + \"` 정확히 **17**회."
    - "17곳 중 3곳(원본 L273/L742/L1772 — Datum/Measurement/SHOT 세 접두어 각 1개씩 대표)을 치환 후 실제 코드에서 손으로 짚어 `LOG_TAG + \"Datum '\" + ...` 형태이고 원본 `\"[FAIMeasurement] Datum '\" + ...` 과 이어붙인 결과가 동일함을 확인, SUMMARY.md 에 before/after 텍스트를 그대로 기록한다."
    - "빌드 PASS — `error CS` 0건, `warning CS` 정확히 12건(baseline, CS0618×10+CS0162×2) 유지. 신규 CS0219/CS0168/CS0103/CS0161 0건."
    - "파일 최종 줄수 — **1781**줄(1775+6). 내역: LOG_TAG 상수 삽입(+2 — 안내주석1줄+선언1줄, 순수 삽입 컨텍스트 무변경) + ECrossZGate 문서주석 삽입(+4, 순수 삽입 컨텍스트 무변경) + 17곳 sed 치환(줄수 무변화, 각 줄 내용만 치환 → git diff 상 add17/del17). 누적 `git diff --numstat` add=23/del=17."
    - "`Action_FAIMeasurement.cs` 단 1개 파일만 변경(단일 커밋). `WPF_Example/DatumMeasurement.csproj`(로컬 미커밋 오염, 항상 존재)는 커밋 후에도 git status 에 unstaged `M` 으로 남는다 — `git add` 는 대상 파일 경로 직접 지정만 사용, `git add -A`/`-a` 금지."
    - "삼항 `?:` 신규 도입 0건, C# 7.2. 신규 상수/주석은 이 구역 기존 스타일(들여쓰기 8칸, `private const` 클러스터 순서 유지) 그대로. 파일 인코딩 손상 0건(UTF-8 BOM 유지, LF 개행 유지, CRLF 유입 0건), 한글 주석 손상 0건. LOG_TAG 상수·ECrossZGate 문서주석(한글 텍스트 포함) 삽입은 Edit 도구만 사용(bash/python heredoc 금지). 17곳 sed 치환은 순수 ASCII 기계적 치환(신규 한글 타이핑 없음)이라 sed 허용."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "LOG_TAG 상수 신설(17곳 로그 태그 리터럴 중복 제거) + ECrossZGate 문서주석 추가(코드 무변경, 설명 강화)"
      contains: "private const string LOG_TAG = \"[FAIMeasurement] \";"
  key_links:
    - from: "17개 Logging.PrintLog 호출부"
      to: "LOG_TAG"
      via: "문자열 연결 시작 토큰"
      pattern: "LOG_TAG \\+ \""
---

<objective>
`Action_FAIMeasurement.cs`(오늘 9차례 리팩토링 완료 — fik/gf1/hyk/j6j/q9t/rle/s05/sgg/sxj, 전부 "동작 무변경" 검증됨, HEAD=`6b626ea`, 현재 **1775줄**) 사용자 원 백로그 "우선순위 3 (nit) — 선택" 3항목을 처리하는 **오늘의 마지막 bundle**. 앞선 5개 bundle(q9t/rle/s05/sgg/sxj)보다 명시적으로 리스크가 낮은 선택 항목들이다.

오케스트레이터가 이미 3항목을 직접 조사해 판단을 내렸다 — 아래 판단을 그대로 사용하고 재검토하지 않는다:

1. **LOG_TAG 상수화(유일한 실제 코드 변경)**: `"[FAIMeasurement]"` 리터럴이 박힌 `Logging.PrintLog` 호출 17곳(사용자 원 카운트와 정확히 일치)을 `private const string LOG_TAG = "[FAIMeasurement] ";`(태그 뒤 공백까지 포함) 로 추출, 17곳 전부 `LOG_TAG + "..."` 형태로 치환. 태그 뒤 공백을 상수 안에 넣기로 결정한 이유: 공백을 호출부마다 손으로 입력할 필요가 없어져 17곳 중 하나라도 공백을 빠뜨리거나 중복하는 사고를 원천 차단.
2. **`IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 인라인 — 하지 않기로 결정, 코드 무변경**: 각각 외부 호출자 정확히 1곳, 각각 존재 이유를 설명하는 주석 보유. 인라인하면 자기설명적 이름이 사라지고 설명 주석을 이미 비자명한 호출부(`if (!bRelevant)` 분기)로 옮기거나 버려야 함 — 손해가 더 큼. 사용자 원 요청이 "고려"(선택)로 명시. **이번 플랜에서 이 두 함수는 1바이트도 건드리지 않는다.**
3. **`ECrossZGate`의 `NotMyTick`/`HalfPending` 내부 `bNonProtocolCycle` 이중분기 — 문서화만, enum 무변경**: enum 을 확장하면 오늘 세 번째(hyk→sgg→이번)로 `switch (eGate)` 블록 전체 제어흐름을 재검증해야 하는데, 순전히 문서적 가치를 위해 그 위험을 또 감수할 이유가 없음. 대신 `private enum ECrossZGate { ... }` 선언 바로 위에 "이 enum 은 완전한 상태표가 아니라 상위 분류일 뿐" 이라고 명시하는 주석 4줄만 추가한다. **`switch (eGate)` 블록·`bNonProtocolCycle` 선언/사용은 1바이트도 건드리지 않는다.**

Purpose: 오늘 백로그의 남은 선택 항목을 낮은 리스크로 정리하고, 실제 코드 변경(1건)은 순수 표기 치환이며 나머지 2건은 조사 후 "변경하지 않는 것이 맞다"는 판단 자체를 문서로 남긴다.
Output: 파일 1개 수정(새 파일 0개), 상수 1개 신설, 커밋 1개.
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
| HEAD | **`6b626ea`** |
| 워킹트리 | ` M WPF_Example/DatumMeasurement.csproj` 1건뿐(커밋 금지 로컬 설정 — 항상 존재) |
| 대상 파일 | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — **1775줄**, UTF-8 BOM 있음, LF |
| 기존 `private const` 클러스터 | L71-76: `UNSET_ZINDEX`(L72) / `CROSS_Z_ROLE_SUFFIX_A`(L73) / `CROSS_Z_ROLE_SUFFIX_B`(L74) / `CROSS_Z_DATUM_KEY_PREFIX`(L76, 그 위 L75 안내주석) |
| `ECrossZGate` enum | L42-52, 기존 4줄 안내주석(L42-45) + `private enum ECrossZGate {`(L46) ~ `}`(L52) |
| `switch (eGate)` 블록 | `EvaluateCrossZGate`(L659) 안, L691(`switch (eGate)`)~L720(`}`), 정확히 30줄 |
| `IsCrossZDatumBothStored` | L1098-1104(안내주석 2줄+본문5줄, 7줄) — 유일 호출자 `TryGrabOrLoadCrossZDatumImages` L1038 |
| `TryReDetectCrossZDatumFromStore` | L1106-1113(안내주석 3줄+본문5줄, 8줄) — 유일 호출자 `TryGrabOrLoadCrossZDatumImages` L1040 |
| `LOG_TAG`/`\[FAIMeasurement\]` 사전오염 확인 | `LOG_TAG` 파일 내 출현 **0건**(자기참조 오염 사전 확인). `\[FAIMeasurement\]` 출현 정확히 **17건**(아래 표) |

### 17곳 좌표 (원본 라인, `grep -n '\[FAIMeasurement\]'` 실측 — 전부 접두어 바로 뒤가 공백 1개 + 대문자 단어로 시작하는 균일 형태)

L273(Datum), L290(Datum), L320(Datum, 변수 dn), L331(Datum), L344(Datum), L370(Datum, 변수 dn), L386(Datum), L452(SHOT), L742(Measurement), L874(SHOT), L878(SHOT), L1362(Measurement), L1374(Measurement), L1432(Measurement), L1460(Measurement), L1466(Measurement), L1772(SHOT)

17곳 전부 정확히 `"[FAIMeasurement] `(여는따옴표+태그+대괄호+공백 1개) 로 시작 — 이 뒤에 바로 `Datum`/`SHOT`/`Measurement` 등 대문자 단어가 이어진다(공백 2개나 0개인 예외 없음, grep 으로 이미 확인 완료).
</context>

<tasks>

<task type="auto">
  <name>Task 1: LOG_TAG 상수화(17곳) + ECrossZGate 문서주석 추가 [TCS-01, TCS-02, TCS-03]</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
### 0. 착수 전 재확인 (스크래치 baseline 스냅샷, 파일 수정 0)
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
mkdir -p "$SCR/tcs"
git rev-parse --short HEAD   # 기대 6b626ea
wc -l "$F"                   # 기대 1775
git show 6b626ea:"$F" > "$SCR/tcs/base.cs"
diff "$SCR/tcs/base.cs" "$F"   # 빈 출력 기대(워킹트리가 HEAD와 이 파일에서 동일함을 확인)
grep -c 'LOG_TAG' "$F"                        # 기대 0 (자기참조 오염 사전 확인)
grep -c '\[FAIMeasurement\]' "$F"             # 기대 17
```
카운트가 크게 다르면(예: 17이 아니면) 즉시 중단하고 실제 값을 보고한다 — 몰래 진행하지 않는다.

### 1. 17곳 sed 일괄 치환 (Bash — 순수 ASCII 기계적 치환, 신규 한글 타이핑 없음이라 sed 허용)
⚠ **반드시 LOG_TAG 상수 삽입(Step 2)보다 먼저 실행한다** — 순서를 바꾸면 상수 자신의 정의 줄까지 이 sed 가 잡아 `LOG_TAG = LOG_TAG + "";` 로 자기참조 오염시킨다(컴파일 에러).
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
sed -i 's/"\[FAIMeasurement\] /LOG_TAG + "/g' "$F"
grep -c '\[FAIMeasurement\]' "$F"    # 기대 0 (17곳 전부 치환됨, 상수는 아직 선언 전)
grep -c 'LOG_TAG + "' "$F"           # 기대 17
```

### 2. LOG_TAG 상수 선언 삽입 (Edit 도구 — 한글 주석 포함, heredoc 금지)
old_string (기존 `private const` 클러스터 마지막 항목 + 빈 줄 + 다음 멤버, 파일 내 유일):
```
        //260722 hbk Phase 68 D-06/D-09: Datum 크로스-Z 저장소 키 접두사 — 측정 키(ShotName|MeasName)와 네임스페이스 구분.
        private const string CROSS_Z_DATUM_KEY_PREFIX = "DATUM|";

        public ShotConfig ShotParam => Param as ShotConfig;
```
new_string:
```
        //260722 hbk Phase 68 D-06/D-09: Datum 크로스-Z 저장소 키 접두사 — 측정 키(ShotName|MeasName)와 네임스페이스 구분.
        private const string CROSS_Z_DATUM_KEY_PREFIX = "DATUM|";
        //260819 hbk quick-260819-tcs: 로그 태그 리터럴 17곳 중복 제거 — 문자열 값(태그 뒤 공백 포함) 은 그대로, 표기만 상수 참조로 치환. 공백을 상수 안에 둬서 호출부마다 손으로 입력할 필요를 없앴다.
        private const string LOG_TAG = "[FAIMeasurement] ";

        public ShotConfig ShotParam => Param as ShotConfig;
```
⚠ 상수 값은 `"[FAIMeasurement] "` — 닫는 대괄호 뒤 공백 1개, 그 다음 닫는 따옴표. 공백을 빠뜨리면 17곳 전부 로그 텍스트가 `[FAIMeasurement]Datum` 처럼 공백 없이 붙어 출력되는 회귀가 생긴다.

### 3. ECrossZGate 문서주석 추가 (Edit 도구 — 순수 주석 삽입, `switch (eGate)`/`bNonProtocolCycle`/`IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 는 전혀 건드리지 않음)
old_string (기존 안내주석 마지막 줄 + enum 선언, 파일 내 유일):
```
        //  리팩토링 전후 대조가 가능하기 때문이다.
        private enum ECrossZGate {
```
new_string:
```
        //  리팩토링 전후 대조가 가능하기 때문이다.
        //260819 hbk quick-260819-tcs 문서화 전용(로직 무변경) — NotMyTick/HalfPending 두 case 내부의
        //  bNonProtocolCycle 분기(parentSeq2.IsProtocolDrivenCycle() 로 읽음, 위 주석 참고)까지 합치면
        //  실제 상태 조합은 5개보다 많다. 즉 이 enum 은 완전한 상태표가 아니라 상위(top-level) 분류이며,
        //  두 번째 축(bNonProtocolCycle)은 의도적으로 enum 멤버로 인코딩하지 않는다.
        private enum ECrossZGate {
```

### 4. 커밋 (대상 파일 1개만 경로 지정 스테이징)
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only   # 반드시 1줄만 출력되는지 확인 후 커밋
git commit -m "refactor(260819-tcs): 로그 태그 17곳을 LOG_TAG 상수로 통합 + ECrossZGate 문서주석 추가 (P3 nit)"
```
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
RC=0
eq() { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }

echo "== 줄수(결정론적, wc -l) =="
eq "final line count 1781" "$(wc -l < "$F" | tr -d ' ')" "1781"

echo "== LOG_TAG 카운트/리터럴 잔존 =="
eq "LOG_TAG 전체 출현 = 18(선언1+호출17)" "$(grep -o 'LOG_TAG' "$F" | wc -l)" "18"
eq "LOG_TAG + \" 패턴 = 17" "$(grep -oF 'LOG_TAG + "' "$F" | wc -l)" "17"
eq "선언문 정확 일치" "$(grep -cF 'private const string LOG_TAG = "[FAIMeasurement] ";' "$F")" "1"
eq "[FAIMeasurement] 리터럴 잔존 = 1(선언 자신만)" "$(grep -cF '[FAIMeasurement]' "$F")" "1"

echo "== ECrossZGate 문서주석 =="
eq "신규 문서주석 삽입 확인" "$(grep -cF '두 번째 축(bNonProtocolCycle)은 의도적으로 enum 멤버로 인코딩하지 않는다' "$F")" "1"
eq "enum 멤버 5개 무변경" "$(grep -cF 'private enum ECrossZGate {' "$F")" "1"

exit $RC
```
    </automated>
    <automated>
```bash
# 손대면 안 되는 3구역 byte-identical 증명 (동적 앵커 — 라인번호 하드코딩 금지, +6 시프트 자동 흡수)
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/tcs/base.cs"
RC=0
dif() { if [ -z "$2" ]; then echo "OK   $1"; else echo "FAIL $1 | diff:"; echo "$2" | head -40; RC=1; fi; }

# 1) switch (eGate) 블록 30줄
BS=$(grep -n '^                switch (eGate)$' "$SB" | head -1 | cut -d: -f1)
CS=$(grep -n '^                switch (eGate)$' "$F" | head -1 | cut -d: -f1)
echo "INFO switch(eGate) base=$BS current=$CS"
dif "switch(eGate) 30줄 byte-identical" "$(diff <(sed -n "${BS},$((BS+29))p" "$SB") <(sed -n "${CS},$((CS+29))p" "$F"))"

# 2) IsCrossZDatumBothStored + TryReDetectCrossZDatumFromStore 16줄(안내주석+본문 x2, 사이 빈줄 포함)
BS2=$(grep -n '// 양 role(A/B) 저장 완료 여부만 판정(클론 미취득)' "$SB" | head -1 | cut -d: -f1)
CS2=$(grep -n '// 양 role(A/B) 저장 완료 여부만 판정(클론 미취득)' "$F" | head -1 | cut -d: -f1)
echo "INFO IsCrossZDatumBothStored base=$BS2 current=$CS2"
dif "IsCrossZDatumBothStored/TryReDetectCrossZDatumFromStore 16줄 byte-identical" "$(diff <(sed -n "${BS2},$((BS2+15))p" "$SB") <(sed -n "${CS2},$((CS2+15))p" "$F"))"

# 3) bNonProtocolCycle 선언/사용 4개소 문자열 그대로 존재(제거/변형 없음)
dif "bNonProtocolCycle 참조 4곳 무변경" "$(diff <(grep -oF 'bNonProtocolCycle' "$SB") <(grep -oF 'bNonProtocolCycle' "$F"))"

exit $RC
```
    </automated>
    <automated>
```bash
# 17곳 sed 치환 결과 표본 3곳 수기 대조용 출력 (자동 실패 조건은 없음 — SUMMARY.md 에 그대로 옮겨 적을 것)
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
echo "== 표본 1 (원본 L273, Datum) =="
grep -n "LOG_TAG + \"Datum '\" + misName" "$F"
echo "== 표본 2 (원본 L742, Measurement) =="
grep -n "LOG_TAG + \"Measurement '\" + measName + \"' failed" "$F"
echo "== 표본 3 (원본 L1772, SHOT) =="
grep -n "LOG_TAG + \"SHOT '\" + shotName + \"' 검사 이미지 없음" "$F"
```
    </automated>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
RC=0
eq() { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }

echo "== numstat(단일 커밋, 정확히 add23/del17) =="
NUMSTAT=$(git diff --numstat HEAD~1 HEAD -- "$F")
eq "add=23" "$(echo "$NUMSTAT" | cut -f1)" "23"
eq "del=17" "$(echo "$NUMSTAT" | cut -f2)" "17"
exit $RC
```
    </automated>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSB" WPF_Example/DatumMeasurement.csproj -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\tcs-t1\\" -v:minimal -nologo > "$SCR/tcs-t1-build.log" 2>&1
[ "$(grep -c ': error ' "$SCR/tcs-t1-build.log")" = "0" ] && [ "$(grep -c ': warning CS' "$SCR/tcs-t1-build.log")" = "12" ] && echo "BUILD PASS (error0/warning12, clean Rebuild)"
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
eq "커밋 파일 1개" "$(git show --name-only --format='' HEAD | grep -c .)" "1"
eq "csproj unstaged 유지" "$(git status --porcelain)" " M WPF_Example/DatumMeasurement.csproj"
python3 -c "
data = open(r'$F', 'rb').read()
print('CR bytes:', data.count(b'\r'))
" | grep -q '^CR bytes: 0$' && echo "OK   CRLF 오염 0(python3 바이트 카운트)"
exit $RC
```
    </automated>
  </verify>
  <done>`LOG_TAG` 상수 신설, `"[FAIMeasurement]"` 리터럴 17곳 전부 `LOG_TAG + "..."` 로 치환(런타임 출력 문자열 무변경, 표본 3곳 수기 대조 완료). `ECrossZGate` enum 선언 위에 문서주석 4줄 추가(로직 무변경). `switch (eGate)` 블록/`bNonProtocolCycle`/`IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 전부 byte-identical 확인. `IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 인라인은 조사 후 미실행 결정을 SUMMARY.md 에 기록. 파일 1781줄, 빌드 error0/warning12(clean Rebuild), 파일 1개만 커밋, csproj unstaged 유지, 인코딩 손상 0건.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

이 플랜은 순수 내부 리팩토링(로그 태그 문자열 상수화 + 주석 추가)으로, 신뢰 경계를 넘는 입력·외부 통신·권한 변경이 없다. 참고용으로 기존 경계만 기록한다.

| Boundary | Description |
|----------|--------------|
| 검사 로그(Trace/Error) → 파일/콘솔 출력 | 사용자가 문제 진단 시 읽는 로그 텍스트가 이번 변경(태그 표기 방식만 변경)으로 1글자도 달라지지 않아야 함 — 달라지면 기존 로그 기반 트러블슈팅 절차(reference_seq_log_tag_to_code_map.md)가 깨짐 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-------------------|
| T-tcs-01 | T (변조) | `LOG_TAG` 상수 값에서 태그 뒤 공백 누락 | mitigate | must_haves + verify 에서 상수 선언문 정확 일치(`grep -cF`)와 표본 3곳 수기 대조(원본 텍스트와 이어붙인 결과 비교)로 이중 검증 — 공백이 빠지면 17곳 전부 로그가 `[FAIMeasurement]Datum` 처럼 붙어 출력되는 회귀지만 컴파일은 통과하므로 정적 카운트만으로는 부족해 텍스트 재구성 확인까지 요구 |
| T-tcs-02 | T (변조) | 17곳 sed 치환이 아직 존재하지 않는 `LOG_TAG` 상수 정의 자체를 잘못 잡아 자기참조 오염 | mitigate | 실행 순서를 명시적으로 고정(sed 치환 → 상수 삽입) + Step 0 에서 `LOG_TAG` 사전 출현 0건 확인 + Step 1 직후 `[FAIMeasurement]` 잔존 0건(아직 상수가 없으므로) 확인 — 순서가 바뀌면 컴파일 에러(자기참조 const)로 즉시 드러나 은폐 위험은 낮지만, verify 단계에서 카운트로 사전 차단 |
| T-tcs-03 | T (변조) | `ECrossZGate` 문서주석 삽입 시 `switch (eGate)`/`bNonProtocolCycle` 판정 로직 실수로 훼손 | mitigate | must_haves + verify 에서 동적 앵커(grep -n) 기반 byte-identical diff 2건(switch 블록 30줄, bNonProtocolCycle 참조 4곳) — 오늘 세 번째 재검증이 아니라 "삽입 지점이 이 구역과 물리적으로 분리돼 있음"을 diff 로 증명하는 방식으로 검증 비용을 낮춤 |

</threat_model>

<verification>

### 실패 시 대응
- **Step 0 카운트 불일치(17이 아님)** → 원문이 계획 시점과 달라졌다는 뜻. `grep -n '\[FAIMeasurement\]'` 로 실제 좌표를 재탐색해 진행 여부를 사용자에게 보고. 몰래 카운트를 재정의하지 않는다.
- **sed 치환 후 `[FAIMeasurement]` 잔존 > 0** → 정규식이 매치 못한 예외 형태가 있다는 뜻. 해당 줄을 grep -n 으로 찾아 개별 Edit 으로 처리(정규식을 억지로 넓히지 않는다).
- **줄수(wc -l) 불일치(1781 아님)** → LOG_TAG/ECrossZGate 삽입 블록 중 하나가 계획과 다르게 적용된 것. `git diff`로 실제 삽입 줄을 대조.
- **byte-identical diff FAIL(switch/bNonProtocolCycle)** → 즉시 중단. Step 3 Edit 이 의도치 않게 다른 위치를 건드렸을 가능성 — old_string 유일성을 재확인.
- **numstat 불일치(add23/del17 아님)** → 순서(sed 먼저 → 상수/주석 삽입)가 지켜지지 않았거나 예외 케이스가 있었다는 뜻. `git diff` 로 실제 변경분 전체를 눈으로 대조.
- **BOM/LF/CRLF 손상 감지** → 즉시 중단하고 `git diff` 로 손상 범위 확인 후 보고(자동 복구 시도 금지).
- **빌드 산출물 잠김** → `OutputPath` 이름만 바꿔 재시도. **프로세스 종료 금지.**

### 런타임 UAT
정적 검증(카운트+표본 대조+byte-identical diff+numstat+빌드)만으로 회귀 0 을 주장한다 — 로그 텍스트 표기 방식만 바뀌었을 뿐 판정 로직은 전혀 건드리지 않는다. 실기 확인이 필요하면: Shot 1개 검사 후 로그 파일(Error 레벨)에서 `[FAIMeasurement]` 로 시작하는 라인이 이전과 동일한 형태(태그+공백+본문)로 찍히는지 확인.

</verification>

<success_criteria>
- `private const string LOG_TAG = "[FAIMeasurement] ";` 신설(클래스 상단 기존 `private const` 클러스터 안, 태그 뒤 공백 포함)
- `Logging.PrintLog` 호출 17곳 전부 `LOG_TAG + "..."` 로 치환, 런타임 출력 문자열 무변경(표본 3곳 수기 대조로 확인)
- `IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` — 조사 후 미실행 결정, 1바이트도 변경 없음(diff 로 확인)
- `ECrossZGate` enum 선언 위 문서주석 4줄 추가 — `switch (eGate)` 블록·`bNonProtocolCycle` 선언/사용은 byte-identical
- `wc -l` 최종 줄수 정확 일치(1775 → 1781), `git diff --numstat` add=23/del=17, 빌드 error0/warning12(clean Rebuild)
- `Action_FAIMeasurement.cs` 단 1개 파일만 1커밋으로 변경, `DatumMeasurement.csproj` 는 끝까지 unstaged
- UTF-8 BOM 유지 + LF 개행 유지(CRLF 오염 0건) + 한글 주석 손상 0건
- 신규 코드 삼항 `?:` 0건, C# 7.2, 이 파일 기존 스타일 그대로
- 오늘 백로그(fik/gf1/hyk/j6j/q9t/rle/s05/sgg/sxj/tcs) 전 항목 처리 완료 — 우선순위 3(nit) 3항목까지 전부 마무리
</success_criteria>

<output>
완료 후 `.planning/quick/260819-tcs-fai-refactor-bundle-f/260819-tcs-SUMMARY.md` 작성(Edit/Write 도구 사용 — heredoc 금지, 한글 인코딩 보존). Item 2(인라인 미실행)와 Item 3(문서화만) 의 판단 근거를 SUMMARY.md 의 Decisions Made 섹션에 이 PLAN.md 의 objective 문구 그대로 기록할 것. 17곳 중 표본 3곳의 before/after 텍스트도 표로 포함할 것.
</output>
