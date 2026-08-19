---
phase: quick-260819-mbt
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
autonomous: true
requirements: [MBT-01]

must_haves:
  truths:
    - "Datum 트리 노드를 선택했을 때 우측 PropertyGrid 에서 `PixelToUM_Offset`/`MotorXPos`/`MotorYPos`/`FrameWidth`/`FrameHeight`/`PartNo` 6개 legacy 필드가 **모든** `EDatumAlgorithm` 값(TwoLineIntersect/CircleTwoHorizontal/VerticalTwoHorizontal/VerticalTwoHorizontalDualImage 등)에서 숨겨진다 — `IsHiddenForAlgorithm` 맨 위, 기존 `TwoLineAngleToleranceDeg` 무조건 hide 줄 바로 다음에 무조건 hide 줄 1개를 추가하는 것으로 구현한다."
    - "`PixelResolution` 필드는 hide 목록에 **포함되지 않으며** PropertyGrid 에서 계속 노출된다 — mm/pixel 캘리브레이션에 실사용되는 별개 필드이므로 절대 건드리지 않는다."
    - "`IsHiddenForAlgorithm` 아래 `switch (alg)` 의 4개 case 분기 로직은 문자 하나도 바뀌지 않는다 — 새로 추가하는 것은 `switch` **이전**의 무조건 hide 줄 1개뿐이다."
    - "`DatumConfig.cs` 파일 전체에서 이 변경으로 늘어나는 줄은 정확히 1줄(1283→1284)이며, 그 외 다른 어떤 줄도 바뀌지 않는다. 다른 파일(`CameraSlaveParam.cs` 포함)은 전혀 손대지 않는다."
    - "빌드는 PASS 하고 경고 수는 착수 전과 동일한 baseline 12줄(CS0618×10 + CS0162×2)을 유지한다 — 새 경고 0건."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "IsHiddenForAlgorithm 에 6개 legacy CameraSlaveParam 필드 무조건 hide 줄 1개 추가"
      contains: "if (name == \"PixelToUM_Offset\" || name == \"MotorXPos\" || name == \"MotorYPos\" || name == \"FrameWidth\" || name == \"FrameHeight\" || name == \"PartNo\") return true;"
  key_links:
    - from: "GetProperties() (L1153) — DynamicPropertyHelper.FilterProperties(this, attrs, name => IsHiddenForAlgorithm(name, alg), sourceNames)"
      to: "IsHiddenForAlgorithm 신규 무조건 hide 줄"
      via: "PropertyGrid 가 매 렌더링마다 이 람다를 통해 각 프로퍼티명을 IsHiddenForAlgorithm 에 넘기므로, 신규 줄이 alg 값과 무관하게 항상 평가되어 6개 필드를 걸러낸다"
      pattern: "if \\(name == \"PixelToUM_Offset\" \\|\\| name == \"MotorXPos\""
---

<objective>
Datum 트리 노드 선택 시 우측 PropertyGrid("General" 계열)에 보이는 항목 중, Datum 검출 로직에서 전혀 쓰이지 않는
`CameraSlaveParam` 상속 legacy 필드 6개(`PixelToUM_Offset`/`MotorXPos`/`MotorYPos`/`FrameWidth`/`FrameHeight`/`PartNo`)를
`IsHiddenForAlgorithm` 의 기존 무조건-hide 패턴(`TwoLineAngleToleranceDeg`)과 동일한 방식으로 숨긴다.

이미 조사 완료: 6개 필드는 `WPF_Example/Sequence/Param/CameraSlaveParam.cs` L22-34 에 선언되어 있고,
`DatumConfig` 가 이를 상속하기 때문에 PropertyGrid 에 노출되지만, `grep -rn "datum\.(필드명)"` 전수 조사 결과
Datum 관련 코드 어디서도 읽히지 않는다(실사용 0건, 재확인 완료 — 아래 착수 전 검증 참고).
`PixelResolution` 은 바로 옆에 선언돼 있지만 mm/pixel 캘리브레이션에 실사용되는 **별개 필드**라 절대 건드리지 않는다.

Purpose: 실제 검출에 아무 영향 없는 죽은 legacy 필드가 PropertyGrid 를 어지럽히는 것을 정리해 사용자가
Datum 설정 시 헷갈리지 않게 한다.

Output: `DatumConfig.cs` 1개 파일, `IsHiddenForAlgorithm` 안에 무조건 hide 줄 1개 추가(1283→1284줄). 커밋 1개.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**착수 전 필수 확인 (30초). 하나라도 다르면 즉시 중단하고 사용자에게 보고 — 아래 줄번호가 무효화된다:**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
git rev-parse --short HEAD          # 기대: 38fff26 (다르면 계속 진행하되 verify 블록의 BASE 를 이 값으로 갱신)
git status --porcelain              # 기대: " M WPF_Example/DatumMeasurement.csproj" 단 1줄 (+ 미추적 .planning/quick/* 디렉터리)
git status --porcelain -- $F        # 기대: 출력 없음 (clean)
wc -l $F                            # 기대: 1283
sed -n '1170,1172p' $F
```
기대 출력(마지막 명령, 3줄):
```
        private static bool IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) {
            if (name == "TwoLineAngleToleranceDeg") return true; // 모든 알고리즘에서 PropertyGrid 숨김 (직각 게이트 로직은 DatumFindingService 에 보존)
            switch (alg) {
```

**재확인용 (착수 전 플래너가 이미 확인함, 실행 중 재검증만 하면 됨):**
```bash
grep -c "PixelResolution" $F                                                    # 기대 0 (DatumConfig.cs 는 PixelResolution 을 아예 참조하지 않음)
grep -c "TwoLineAngleToleranceDeg" $F                                            # 기대 2
grep -rn "datum\.\(PixelToUM_Offset\|MotorXPos\|MotorYPos\|FrameWidth\|FrameHeight\|PartNo\)" WPF_Example --include="*.cs" | grep -v CameraSlaveParam.cs | grep -v CameraParam.cs   # 기대 출력 없음 (0건)
```

**⚠ 워킹트리 오염 주의 (이 프로젝트 최대 사고 위험):**
`WPF_Example/DatumMeasurement.csproj` 에 **커밋하면 안 되는 로컬 설정**이 떠 있다 —
Debug `OutputPath=D:\Data\`, Release `DefineConstants` 의 `SIMUL_MODE`.
저장소에 들어가면 현장 배포본이 시뮬레이션 모드로 나간다.
→ **`git add -A` / `git add .` / `git commit -a` 절대 금지.** 반드시 대상 파일 1개만 경로로 스테이징한다.
</context>

<ground_rules>
### G-1. 허용되는 변환은 정확히 1종
`IsHiddenForAlgorithm` 의 기존 L1171(`if (name == "TwoLineAngleToleranceDeg") return true; ...`) **바로 다음**,
`switch (alg) {` **바로 이전** 위치에 새 줄 1개를 삽입한다. 그 외 어떤 줄도 추가/삭제/수정하지 않는다.

- **범위 확장 금지**: `switch (alg)` 아래 4개 case 분기(TwoLineIntersect/CircleTwoHorizontal/VerticalTwoHorizontal/VerticalTwoHorizontalDualImage)의
  hide 조건은 절대 건드리지 않는다. `CameraSlaveParam.cs`/`CameraParam.cs` 무접촉. `DatumConfig.cs` 안의 다른 메서드도 무접촉.
- **필드 목록 고정**: hide 대상은 정확히 6개 — `PixelToUM_Offset`/`MotorXPos`/`MotorYPos`/`FrameWidth`/`FrameHeight`/`PartNo`.
  `PixelResolution` 은 **절대 포함하지 않는다**(이름이 비슷해서 실수하기 쉬우니 삽입 후 반드시 재확인).
- **코딩 컨벤션**: 삼항 `?:` 금지(해당 없음 — 기존 패턴처럼 `if (...) return true;` 한 줄 early-return), 헝가리언은 이 라인엔 해당 없음,
  기존 L1171 과 동일한 스타일(들여쓰기 12칸, `if (name == "...") return true; // 주석`)을 그대로 따른다.

### G-2. 정확한 삽입 텍스트 (한 글자도 다르게 쓰지 말 것)
```csharp
            if (name == "PixelToUM_Offset" || name == "MotorXPos" || name == "MotorYPos" || name == "FrameWidth" || name == "FrameHeight" || name == "PartNo") return true; // Datum 미사용 legacy CameraSlaveParam 필드 숨김
```
들여쓰기는 L1171 과 동일하게 공백 12칸.

### G-3. 빌드 규칙
- 앱이 `D:\Data\` 에서 실행 중일 수 있다 → **프로세스 종료 절대 금지.** 스크래치 `OutputPath` 로 컴파일만 검증.
  잠김 실패 시 `OutputPath` 이름만 바꿔 재시도. 그래도 안 되면 **죽이지 말고 사용자에게 보고.**
- **`//p:` 금지, `-p:` 사용** (`/` 섞이면 Git Bash 가 `MSB1001` 로 죽는다).
- **경고 baseline = 12줄 (CS0618×10 + CS0162×2).** "경고 0" 을 통과 기준으로 쓰면 항상 거짓 실패.
- OutputPath 후행 백슬래시는 반드시 `\\` 로 쓸 것 (`"$SCR\mbt\"` 는 bash 문법 에러).

### G-4. 커밋 위생
`git add -A`/`git add .`/`git commit -a` 절대 금지. `git add WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` 로 대상 파일만 스테이징.
커밋 후 `WPF_Example/DatumMeasurement.csproj` 가 여전히 unstaged(` M`)인지 반드시 확인.
</ground_rules>

<tasks>

<task type="auto">
  <name>Task 1: IsHiddenForAlgorithm 에 6개 legacy 필드 무조건 hide 줄 추가</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs</files>
  <action>
**0단계 — 기준점 고정:**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=$(git rev-parse --short HEAD)
[ -f "$SCR/mbt-git-baseline.txt" ] || git status --porcelain > "$SCR/mbt-git-baseline.txt"
git show $BASE:$F > "$SCR/mbt-base-datumconfig.txt"
wc -l "$SCR/mbt-base-datumconfig.txt"   # 1283 이어야 함
```
G-3 빌드를 착수 전 상태에서 1회 돌려 경고 줄 수를 `$SCR/mbt-baseline-warn.log` 에 저장한다(아래 빌드 커맨드 패턴과 동일, 리다이렉트만 별도 파일로).

---

**1단계 — 편집.** Edit 도구로 L1171(`if (name == "TwoLineAngleToleranceDeg") return true; ...`) 다음, L1172(`switch (alg) {`) 이전에
아래 1줄을 정확히 삽입한다. 기존 두 줄(L1171, L1172였던 `switch` 줄)은 문자 하나도 바꾸지 않는다.

삽입할 줄 (G-2 와 동일, 들여쓰기 공백 12칸):
```csharp
            if (name == "PixelToUM_Offset" || name == "MotorXPos" || name == "MotorYPos" || name == "FrameWidth" || name == "FrameHeight" || name == "PartNo") return true; // Datum 미사용 legacy CameraSlaveParam 필드 숨김
```

삽입 후 L1170-1173 은 정확히 다음 4줄이어야 한다:
```
        private static bool IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) {
            if (name == "TwoLineAngleToleranceDeg") return true; // 모든 알고리즘에서 PropertyGrid 숨김 (직각 게이트 로직은 DatumFindingService 에 보존)
            if (name == "PixelToUM_Offset" || name == "MotorXPos" || name == "MotorYPos" || name == "FrameWidth" || name == "FrameHeight" || name == "PartNo") return true; // Datum 미사용 legacy CameraSlaveParam 필드 숨김
            switch (alg) {
```

---

**2단계 — 빌드 + 정적 검증 (커밋 전).** verify 블록의 자동화 검증 전부와 G-3 빌드를 여기서 실행한다.

**3단계 — 커밋. `git add -A` 금지, 대상 파일만:**
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
git diff --cached --name-only          # 정확히 1줄이어야 함
git commit -m "fix(260819-mbt): Datum PropertyGrid 에서 미사용 legacy CameraSlaveParam 필드 6종 숨김"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M" (unstaged) 여야 함
```
  </action>
  <verify>
    <automated>
# [1] 라인 수 정확히 +1 (1283→1284) + 삽입 지점 밖(L1-1171, 기존 1172-1283→새 1173-1284) 완전 무변경
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
[ "$(wc -l < $F)" = "1284" ] && \
diff <(sed -n '1,1171p' "$SCR/mbt-base-datumconfig.txt") <(sed -n '1,1171p' $F) && \
diff <(sed -n '1172,1283p' "$SCR/mbt-base-datumconfig.txt") <(sed -n '1173,1284p' $F) && \
echo "T1 OUT-OF-SCOPE UNCHANGED PASS"
    </automated>
    <automated>
# [2] 신규 줄 1172 정확한 내용 + 앞뒤 문맥(L1170-1173) 정확
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs && \
[ "$(sed -n '1170p' $F)" = '        private static bool IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) {' ] && \
[ "$(sed -n '1171p' $F)" = '            if (name == "TwoLineAngleToleranceDeg") return true; // 모든 알고리즘에서 PropertyGrid 숨김 (직각 게이트 로직은 DatumFindingService 에 보존)' ] && \
[ "$(sed -n '1172p' $F)" = '            if (name == "PixelToUM_Offset" || name == "MotorXPos" || name == "MotorYPos" || name == "FrameWidth" || name == "FrameHeight" || name == "PartNo") return true; // Datum 미사용 legacy CameraSlaveParam 필드 숨김' ] && \
[ "$(sed -n '1173p' $F)" = '            switch (alg) {' ] && \
echo "T1 NEW LINE EXACT PASS"
    </automated>
    <automated>
# [3] 필드명 6종이 새 줄 1곳에서만 각 1회 등장 + PixelResolution 미포함 + TwoLineAngleToleranceDeg 카운트 불변 + 코드베이스 전역 재확인
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs && \
[ "$(grep -oE 'PixelToUM_Offset|MotorXPos|MotorYPos|FrameWidth|FrameHeight|PartNo' $F | wc -l)" = "6" ] && \
[ "$(grep -nE 'PixelToUM_Offset|MotorXPos|MotorYPos|FrameWidth|FrameHeight|PartNo' $F | cut -d: -f1 | sort -u)" = "1172" ] && \
[ "$(grep -c 'PixelResolution' $F)" = "0" ] && \
[ "$(grep -c 'TwoLineAngleToleranceDeg' $F)" = "2" ] && \
[ -z "$(grep -rn 'datum\.\(PixelToUM_Offset\|MotorXPos\|MotorYPos\|FrameWidth\|FrameHeight\|PartNo\)' WPF_Example --include='*.cs' | grep -v CameraSlaveParam.cs | grep -v CameraParam.cs)" ] && \
echo "T1 FIELD SCOPE + PIXELRESOLUTION UNTOUCHED PASS"
    </automated>
    <automated>
# [4] CameraSlaveParam.cs / CameraParam.cs 무접촉 + 커밋 위생 (⚠ git add/commit 이후에 실행)
cd /c/Info/Project/DataMeasurement && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
[ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
git show --name-only --format= HEAD | grep -q 'DatumConfig.cs' && \
[ "$(git show --name-only --format= HEAD | grep -cE 'CameraSlaveParam.cs|CameraParam.cs|DatumMeasurement.csproj')" = "0" ] && \
git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
diff <(cut -c4- "$SCR/mbt-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[01]$' && \
echo "T1 HYGIENE PASS"
    </automated>
    <automated>
# [5] msbuild Debug|x64 스크래치 OutDir 빌드 — 성공 + 경고가 baseline(12줄: CS0618x10 + CS0162x2)과 동일
cd /c/Info/Project/DataMeasurement && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" && \
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\mbt-build\\" \
  -t:Rebuild -v:minimal -nologo > "$SCR/mbt-build.log" 2>&1; \
grep -c ": warning " "$SCR/mbt-build.log"   # baseline 과 동일한 12 이어야 함(다르면 신규 경고 발생, 즉시 보고)
tail -5 "$SCR/mbt-build.log"                # "Build succeeded." 확인
    </automated>
  </verify>
  <done>
`IsHiddenForAlgorithm` L1172 에 6개 legacy 필드(`PixelToUM_Offset`/`MotorXPos`/`MotorYPos`/`FrameWidth`/`FrameHeight`/`PartNo`)를
모든 `EDatumAlgorithm` 에서 무조건 숨기는 줄 1개가 추가되고, `PixelResolution` 은 어디에도 포함되지 않는다.
파일 전체 라인 수 1283→1284, 신규 줄 이외 모든 줄이 바이트 단위로 무변경, `switch (alg)` 이하 4개 case 분기 무접촉,
`CameraSlaveParam.cs`/`CameraParam.cs`/`.csproj` 무접촉. msbuild Debug|x64 성공 + 경고 12줄 baseline 동일. 커밋 1개, 스테이징 파일 정확히 1개.
  </done>
</task>

</tasks>

<verification>
1. `wc -l DatumConfig.cs` → 1284 (정확히 1줄 증가)
2. L1170-1173 이 정확히 목표 4줄과 문자 단위로 일치 (`TwoLineAngleToleranceDeg` 줄 → 신규 6필드 줄 → `switch (alg) {`)
3. 신규 줄 밖의 모든 줄이 BASE(HEAD `38fff26`)와 diff 0
4. 6개 필드명이 파일 전체에서 정확히 1줄(1172)에만 등장, `PixelResolution` 은 파일에서 여전히 0건
5. `git show --name-only HEAD` → `DatumConfig.cs` 단 1개 파일만 (CameraSlaveParam.cs/CameraParam.cs/csproj 미포함)
6. `git status --porcelain -- DatumMeasurement.csproj` → 여전히 ` M`(unstaged)
7. msbuild Debug|x64 성공 + 경고 12줄 baseline 동일
</verification>

<success_criteria>
- Datum PropertyGrid 에서 `PixelToUM_Offset`/`MotorXPos`/`MotorYPos`/`FrameWidth`/`FrameHeight`/`PartNo` 6개 필드가 모든 알고리즘 타입에서 숨겨짐
- `PixelResolution` 은 계속 노출됨 (hide 대상에서 제외 확정)
- `IsHiddenForAlgorithm` 의 `switch (alg)` 이하 기존 4개 case 분기 로직 100% 보존
- `DatumConfig.cs` 외 무변경(`CameraSlaveParam.cs`/`CameraParam.cs`/csproj 포함), 빌드 PASS(경고 12줄 baseline), 커밋 1개
</success_criteria>

<output>
완료 후 `.planning/quick/260819-mbt-datum-hide-legacy-camera-fields/260819-mbt-SUMMARY.md` 생성
</output>
