---
phase: quick-260807-jhg
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/DatumMeasurement.csproj
autonomous: true
requirements: [BUILD-OUTPATH-ABS-01]

must_haves:
  truths:
    - "Release|x64 PropertyGroup 의 OutputPath 가 절대경로 `D:\\Data\\` 로 고정되어, 리포지토리가 어느 드라이브/어느 폴더 깊이에 체크아웃되어 있든 Release|x64 빌드 산출물이 항상 실제 배포 폴더 D:\\Data\\ 에 떨어진다"
    - "Release|x64 PropertyGroup 에서 기존의 4단계 상대 부모경로 OutputPath 값이 파일 전체에서 완전히 사라진다 (0건)"
    - "csproj 안의 나머지 3개 OutputPath (Debug|AnyCPU=bin\\x64\\Debug\\, Release|AnyCPU=bin\\Release\\, Debug|x64=bin\\x64\\Debug\\) 는 단 1바이트도 변하지 않는다 — 파일 내 OutputPath 총 개수는 여전히 4개이고 그중 3개는 `bin\\` 으로 시작한다"
    - "이번 편집이 파일에 만든 변경은 정확히 1줄이다 — 편집 직전 스냅샷 대비 diff 가 삭제 1줄 / 추가 1줄만 보여준다"
    - "작업 시작 시점에 이미 워킹트리에 존재하던 미커밋 변경(Release|x64 의 DefineConstants 가 `TRACE;SIMUL_MODE` → `TRACE` 로 SIMUL_MODE 제거된 상태)이 그대로 보존된다 — 되돌리거나 재적용하지 않는다"
    - "Debug/x64 재빌드가 신규 error CS 0 건으로 통과한다 (Debug 는 이번 변경의 영향을 받지 않으므로 무회귀 확인 목적)"
  artifacts:
    - path: "WPF_Example/DatumMeasurement.csproj"
      provides: "Release|x64 구성의 드라이브/깊이 비의존 절대 OutputPath"
      contains: "<OutputPath>D:\\Data\\</OutputPath>"
  key_links:
    - from: "Release|x64 PropertyGroup 의 OutputPath"
      to: "실제 배포 폴더 D:\\Data\\ (네이티브 DLL·Setting.ini·데이터 폴더가 실재하는 위치)"
      via: "MSBuild 가 OutputPath 를 프로젝트 파일 디렉터리 기준 상대경로로 해석하던 것을 절대경로로 대체 — WPF_Example\\ 기준 4단계 상승은 C: 체크아웃에서 드라이브 루트에 클램프되어 C:\\Data\\ 로 잘못 착지했었다"
      pattern: "<OutputPath>D:\\\\Data\\\\</OutputPath>"
    - from: "동일 PropertyGroup 의 DefineConstants (미커밋 선행 변경, TRACE)"
      to: "이번 편집"
      via: "인접 라인이지만 무관한 변경 — 편집 시 건드리지 않고, 커밋 시에는 파일 단위 스테이징 특성상 함께 실려간다는 점을 커밋 메시지에 명시"
      pattern: "<DefineConstants>TRACE</DefineConstants>"
---

<objective>
`WPF_Example/DatumMeasurement.csproj` 의 Release|x64 PropertyGroup 에서 `OutputPath` 를 4단계 상대 부모경로에서 절대경로 `D:\Data\` 로 교체한다.

Purpose: 기존 상대경로는 "체크아웃이 D: 드라이브의 특정 폴더 깊이에 있다"는 운영 PC 전제로 작성된 값이다. 이 개발 체크아웃(`C:\code\DataMeasurement\WPF_Example\`)에서는 Windows 가 `..` 를 드라이브 루트에서 클램프하는 바람에 에러 없이 `C:\Data\` 로 착지하는데, 그곳은 네이티브 DLL·`Setting.ini`·데이터 폴더가 없는 껍데기 폴더다. 이 세션에서 실제로 두 건의 사고를 유발했다 — (1) 잔존 `C:\Data\DatumMeasurement.exe` 프로세스로 인한 파일 잠금 복사 오류, (2) 시작 시 `AlligatorAlgMil.dll` `FileNotFoundException`. 절대경로로 바꾸면 리포지토리 위치와 무관하게 Release|x64 산출물이 항상 진짜 배포 폴더에 착지한다.

Output: 1줄 값 교체 + 무회귀 검증(정적 grep + 1줄 diff + Debug/x64 빌드).
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@WPF_Example/DatumMeasurement.csproj
</context>

<scope_boundary>
범위는 Release|x64 PropertyGroup 안의 `OutputPath` **한 줄**뿐이다.

건드리지 말 것:
- 같은 PropertyGroup 의 나머지 라인 전부 (`DefineConstants`, `Optimize`, `DebugType`, `PlatformTarget`, `ErrorReport`, `CodeAnalysisRuleSet`, `Prefer32Bit`, `AllowUnsafeBlocks`)
- 다른 모든 PropertyGroup (Debug|AnyCPU, Release|AnyCPU, Debug|x64)
- `ItemGroup` 의 `HintPath` 들 (이것들도 `..\` 로 시작하지만 무관하다)
- 워킹트리에 이미 있던 미커밋 변경 (아래 precondition 참고)

리팩토링·정리·"김에 같이" 수정 금지. 값 하나 교체다.
</scope_boundary>

<tasks>

<task type="auto">
  <name>Task 1: Release|x64 OutputPath 를 절대경로 D:\Data\ 로 교체</name>

  <files>WPF_Example/DatumMeasurement.csproj</files>

  <precondition>
  작업 시작 전, 이 파일의 워킹트리에는 이미 미커밋 변경 1건이 존재한다: Release|x64 PropertyGroup 의 `DefineConstants` 가 `TRACE;SIMUL_MODE` 에서 `TRACE` 로 바뀌어 있다(실HW SIDE PC 용 SIMUL_MODE 제거, 이번 작업과 무관한 사용자의 선행 변경).
  `git diff -- WPF_Example/DatumMeasurement.csproj` 로 이 상태를 먼저 확인하라. 이 변경은 **보존 대상**이다 — 되돌리거나 다시 적용하지 말고, 편집 시 해당 라인을 건드리지 마라. 만약 이 선행 변경이 보이지 않거나 다른 내용으로 바뀌어 있으면 중단하고 보고하라(전제가 깨진 것이므로 diff 기반 검증이 무의미해진다).
  </precondition>

  <read_first>
  `WPF_Example/DatumMeasurement.csproj` 의 60~85행 구간을 읽어 두 개의 x64 PropertyGroup 경계를 눈으로 확인하라. Debug|x64 (약 60~71행) 와 Release|x64 (약 72~82행) 가 인접해 있고 **둘 다 OutputPath 를 가지고 있다.** 편집 대상은 `Condition="'$(Configuration)|$(Platform)' == 'Release|x64'"` 쪽 하나뿐이다. Debug|x64 의 `bin\x64\Debug\` 를 잘못 건드리면 이 PC 의 일상 빌드가 깨진다.
  </read_first>

  <action>
  1. 편집 **직전** 스냅샷을 뜬다 (1줄 diff 증명용):
     `SNAP="$SCRATCH/260807-jhg-csproj.before"` (SCRATCH = 이 세션 스크래치패드 디렉터리) 에 현재 `WPF_Example/DatumMeasurement.csproj` 를 그대로 복사한다.

  2. Edit 툴로 `Condition="'$(Configuration)|$(Platform)' == 'Release|x64'"` PropertyGroup 안의 유일한 `OutputPath` 엘리먼트의 **내용만** 절대경로 `D:\Data\` 로 교체한다. 결과 라인은 다음과 정확히 일치해야 한다(들여쓰기 4칸 유지):

     `    <OutputPath>D:\Data\</OutputPath>`

     교체 전 값은 상위 디렉터리로 네 단계 거슬러 올라가는 상대경로 형태이며, 그대로 이 PropertyGroup 안에 유일하게 존재한다. Edit 의 old_string 은 그 OutputPath 라인 하나만 잡되, 동일 문자열이 Debug|x64 쪽과 겹치지 않음을 확인하고(겹치지 않는다 — Debug|x64 는 `bin\x64\Debug\` 다) 유일 매칭으로 치환한다. `replace_all` 은 쓰지 마라.

     후행 백슬래시를 반드시 남길 것 — MSBuild 의 OutputPath 는 디렉터리 구분자로 끝나야 하며, 파일 내 다른 3개 OutputPath 도 모두 그 규약을 따른다.

  3. 그 외 어떤 라인도 편집하지 않는다. 특히 바로 다음 줄의 `DefineConstants` 는 precondition 에서 확인한 선행 미커밋 변경이므로 원형 그대로 둔다.

  4. 커밋 시 주의: `git add` 는 파일 단위이므로, 이 커밋에는 위 선행 `DefineConstants` 변경이 **불가피하게 함께 실린다**. 이를 숨기지 말고 커밋 메시지 본문에 두 변경(OutputPath 절대경로화 + 선행 SIMUL_MODE 제거 동반 커밋)을 명시하라. 부분 스테이징을 흉내내려고 패치를 손으로 만들거나 stash 를 쓰지 마라 — 한 줄 변경 대비 워킹트리 손상 위험이 크다.
  </action>

  <verify>
    <automated>
    # 리포지토리 루트에서 실행. SNAP 은 Action 1단계에서 뜬 편집 직전 스냅샷 경로.
    F=WPF_Example/DatumMeasurement.csproj

    # (a) 신규 절대경로가 정확히 1건 존재
    test "$(grep -c -F '<OutputPath>D:\Data\</OutputPath>' "$F" || true)" = "1"

    # (b) 기존 4단계 상대경로 OutputPath 가 0건
    test "$(grep -c -F '<OutputPath>..\..\..\..\Data\</OutputPath>' "$F" || true)" = "0"

    # (c) 나머지 3개 OutputPath 무변경: 총 4개, 그중 bin\ 로 시작하는 것 3개
    test "$(grep -c -F '<OutputPath>' "$F" || true)" = "4"
    test "$(grep -c -F '<OutputPath>bin\' "$F" || true)" = "3"

    # (d) 이번 편집이 만든 변경은 정확히 1줄 (삭제 1 + 추가 1 = diff 본문 2줄)
    test "$(diff "$SNAP" "$F" | grep -c '^[<>]' || true)" = "2"

    # (e) 선행 미커밋 변경 보존 확인
    test "$(grep -c -F '<DefineConstants>TRACE</DefineConstants>' "$F" || true)" = "1"

    # (f) Debug/x64 빌드 무회귀 (Debug 는 이번 변경 무영향 — 확인 비용이 싸서 수행)
    "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
      WPF_Example/DatumMeasurement.csproj -t:Build -p:Configuration=Debug -p:Platform=x64 -v:minimal
    # → "0 Error" 확인. MSBuild.exe 가 위 경로에 없으면 다음 순서로 폴백:
    #   C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe
    #   C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/MSBuild.exe
    #   C:/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe
    </automated>
  </verify>

  <done>
  - Release|x64 PropertyGroup 의 OutputPath 가 `D:\Data\` (후행 백슬래시 포함) 이다.
  - 4단계 상대 부모경로 OutputPath 값이 파일에서 0건이다.
  - 파일 내 OutputPath 는 여전히 총 4개이며 그중 3개가 `bin\` 로 시작한다 (Debug|AnyCPU, Release|AnyCPU, Debug|x64 무변경).
  - 편집 직전 스냅샷 대비 diff 본문이 정확히 2줄(삭제 1 / 추가 1)이다.
  - Release|x64 의 `DefineConstants` 가 `TRACE` 로 보존되어 있다.
  - Debug/x64 MSBuild 빌드가 error 0 으로 통과한다.
  </done>
</task>

</tasks>

<out_of_scope>
- **Release/x64 빌드 실행 금지.** 이번 검증에 포함하지 않는다. `D:\Data\Setting.ini` 가 현재 다른 레거시 프로그램의 스키마를 담고 있다는 알려진 후속 리스크가 있으며, 오케스트레이터가 이를 별도로 사용자에게 제기한다. 본 계획 범위 밖이다.
- `C:\Data\` 잔여 폴더/프로세스 정리
- 다른 구성(Debug|AnyCPU / Release|AnyCPU / Debug|x64)의 OutputPath 조정
- `Prefer32Bit`, `CodeAnalysisRuleSet` 등 Release|x64 의 다른 수상한 설정 정리
</out_of_scope>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 빌드 산출물 → 파일시스템 쓰기 위치 | MSBuild 가 OutputPath 로 지정된 디렉터리에 실행 파일·DLL 을 덮어쓴다. 값이 틀리면 의도치 않은 디렉터리를 덮어쓸 수 있다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-jhg-01 | Tampering | Release\|x64 `OutputPath` 오타(예: `D:\Dat\`, 후행 백슬래시 누락)로 인한 엉뚱한 디렉터리 덮어쓰기 | medium | mitigate | verify (a) 가 최종 라인을 `grep -F` 완전일치로 확인 — 오타·백슬래시 누락 시 0건이 되어 실패한다 |
| T-jhg-02 | Tampering | 인접 Debug\|x64 PropertyGroup 의 `OutputPath` 오편집 | medium | mitigate | read_first 로 두 그룹 경계 확인 + verify (c) 가 `bin\` OutputPath 3건 유지를 강제 + verify (d) 1줄 diff |
| T-jhg-03 | Tampering | 선행 미커밋 `DefineConstants` 변경의 무단 되돌림 | medium | mitigate | precondition 로 사전 확인 + verify (e) `TRACE` 보존 확인 + verify (d) 1줄 diff |
| T-jhg-04 | Denial of Service | Release 빌드가 실사용 배포 폴더 `D:\Data\` 를 직접 덮어써, 그곳에서 구동 중인 프로세스와 파일 잠금 충돌 | low | accept | 의도된 동작이다(운영 PC 의 원래 설계). 이번 계획은 Release 빌드를 실행하지 않으므로 노출 없음. 실행 시점 잠금 이슈는 사용자가 프로세스 종료로 처리한다. |
| T-jhg-SC | Tampering | npm/pip/cargo 설치 | — | n/a | 패키지 설치 없음 — 이 작업은 기존 csproj 의 값 1개 교체이며 신규 의존성을 추가하지 않는다 |
</threat_model>

<verification>
Task 1 의 automated 검증 (a)~(f) 가 전부 통과하면 계획 전체가 검증된 것이다. 별도의 계획 수준 추가 검증은 없다.
</verification>

<success_criteria>
- Release|x64 빌드 출력 경로가 리포지토리 체크아웃 위치와 무관하게 `D:\Data\` 로 결정론적으로 해석된다.
- 다른 3개 구성의 출력 경로는 무변경이다.
- 파일 변경량은 정확히 1줄이다.
- Debug/x64 빌드 무회귀.
</success_criteria>

<output>
Create `.planning/quick/260807-jhg-side-pc-release-x64-outputpath-data-to-d/260807-jhg-SUMMARY.md` when done
</output>
