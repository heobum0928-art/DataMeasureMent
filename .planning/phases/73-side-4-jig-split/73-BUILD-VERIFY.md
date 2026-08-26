# Phase 73 — 빌드 검증 규격 (전 plan 공통)

plan-checker 가 **실제로 명령을 실행해** 확인한 결과다. 추측으로 바꾸지 말 것.
이 파일이 빌드 관련 단일 소스다. 각 plan 은 이 파일을 참조한다.

---

## 1. msbuild 호출 형식 — `/p:` 금지, `-p:` 사용

이 환경은 Git Bash(MSYS)다. `/p:` `/v:m` `/nologo` 는 **경로로 변환돼 깨진다**:

```
MSBUILD : error MSB1008: 프로젝트를 하나만 지정할 수 있습니다.
  '/nologo' -> 'C:/Program Files/Git/nologo'
```

**반드시 대시(`-`) 형식을 쓴다.** 값에 `;` 가 들어가면 `%3B` 로 이스케이프한다.

## 2. SIMUL_MODE ON 빌드

```bash
# ⚠ $SCRATCHPAD 는 이 셸에 정의돼 있지 않다(echo 하면 빈값).
#    설정하지 않고 실행하면 -p:OutDir="/b73on/" 가 되어 C:\b73on\ 에 산출된다.
#    실행 전에 세션 스크래치패드 절대경로로 직접 설정할 것.
SCR="/c/Users/tech/AppData/Local/Temp/claude/<session>/scratchpad"   # ← 실제 경로로 교체
test -n "$SCR" && mkdir -p "$SCR" || { echo "SCR 미설정 — 중단"; exit 1; }
MSBuild.exe WPF_Example/DatumMeasurement.csproj -t:Rebuild \
  -p:Configuration=Debug -p:Platform=x64 \
  -p:OutDir="$SCR/b73on/" -p:IntermediateOutputPath="$SCR/objon/" -v:m -nologo
```

## 3. SIMUL_MODE OFF 빌드 — `Release|x64` 를 쓰면 안 된다

**`Release|x64` 는 SIMUL-OFF 가 아니다.** 이 PC 로컬 `WPF_Example/DatumMeasurement.csproj:72~74` 가
`Release|x64` 에도 `TRACE;SIMUL_MODE` 를 켜 놨다(로컬 전용 변경, 커밋 금지 대상).
그대로 쓰면 **ON 을 두 번 도는 것**이고 `#if !SIMUL_MODE` 31곳은 한 번도 컴파일되지 않는다.
`Release|AnyCPU` 는 `AllowUnsafeBlocks` 부재로 CS0227 컴파일 실패다(현재도 깨져 있음).

**csproj 를 수정하지 말고** `DefineConstants` 를 커맨드라인에서 덮어쓴다:

```bash
MSBuild.exe WPF_Example/DatumMeasurement.csproj -t:Rebuild \
  -p:Configuration=Debug -p:Platform=x64 \
  -p:DefineConstants=TRACE%3BDEBUG \
  -p:OutDir="$SCR/b73off/" -p:IntermediateOutputPath="$SCR/objoff/" -v:m -nologo
```

⚠ **`-t:Rebuild` 는 필수다.** 증분 상태로 돌리면 컴파일이 스킵돼 **경고 0줄**이 나온다
(실행 확인: exit 0 / warning 0). 그 결과로 아래 18/16 기준을 적용하면 판정이 통째로 무의미해진다.
`-p:IntermediateOutputPath` 도 스크래치로 돌려 `obj/` 캐시 영향을 배제한다.

SIMUL-OFF 가 실제로 적용됐는지는 **CS0162 가 2→0 으로 사라지는지**로 교차 확인한다
(CS0162 는 SIMUL 전용 분기에서만 나온다).

## 4. 경고 baseline — 73-01 실행 시점에 값이 바뀐다

`Custom/Sequence/SequenceHandler.cs` 의 `TopSideInspectionAction` 생성자 호출이
`[System.Obsolete]`(`Custom/Sequence/Top/Action_TopSideInspection.cs:233`) 대상이라 CS0618 을 낸다.
컴파일 패스가 2회 돌아 **호출 1건당 경고 2줄**이 나온다.

73-01 Task 3 이 `RegisterActions()` 의 Side 호출 1줄을 **4줄로 늘리므로** CS0618 이 6줄 늘어난다.

| 시점 | SIMUL ON (Debug\|x64) | SIMUL OFF (DefineConstants 덮어쓰기) |
|---|---|---|
| 73-01 착수 전 (현 baseline) | **12줄** (CS0618×10 + CS0162×2) | **10줄** (CS0618×10) |
| 73-01 Task 3 완료 후 ~ phase 종료 | **18줄** (CS0618×16 + CS0162×2) | **16줄** (CS0618×16) |

CS0162(도달 불가 코드)는 SIMUL 전용 분기라 OFF 빌드에서는 사라진다.

### 통과 기준 (숫자보다 이게 우선)

1. **에러 0** — 절대 조건
2. **새로운 경고 코드 종류 0건** — 위 표에 없는 코드(CS0219, CS4014, CS0168 등)가 나오면 실패
3. 줄 수는 위 표와 일치. 불일치 시 **원인을 규명**하고, 숫자를 맞추려는 다음 행위는 **금지**다:
   - `[System.Obsolete]` 어트리뷰트 제거
   - `#pragma warning disable` 삽입
   - `-nowarn:` / `NoWarn` 추가

경고 코드 종류 집계:
```bash
MSBuild.exe ... -v:m -nologo 2>&1 | grep -oE "CS[0-9]{4}" | sort | uniq -c
```

## 5. 프로세스 종료 금지

빌드 산출물이 잠기면(`D:\Data\` 에 실행 중인 앱) **프로세스를 죽이지 않는다.**
위 명령처럼 `-p:OutDir` 를 스크래치패드로 돌려 **컴파일만** 검증하고, 잠김 사실을 보고한다.

## 6. csproj 커밋 금지

`WPF_Example/DatumMeasurement.csproj` 는 이 PC 로컬 전용(SIMUL_MODE 강제)이다.
끝까지 unstaged 로 둔다. `git status --porcelain` 에 ` M` (앞칸 공백)으로만 나와야 한다.
