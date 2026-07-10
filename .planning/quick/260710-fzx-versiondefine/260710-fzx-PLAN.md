---
phase: quick-260710-fzx
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/VersionDefine.cs
  - WPF_Example/DatumMeasurement.csproj
  - WPF_Example/Properties/AssemblyInfo.cs
  - WPF_Example/Utility/RecipeFileHelper.cs
autonomous: true
requirements: [VERSIONDEFINE-01]

must_haves:
  truths:
    - "빌드 산출물 DatumMeasurement.exe 의 FileVersion 이 1.4.0.0 이다"
    - "AssemblyVersion / AssemblyFileVersion / RecipeFileHelper.GetVersion() 세 곳이 모두 VersionDefine.VERSION 단일 상수를 참조한다"
    - "하드코딩된 24.11.6.1 / 24.12.10.02 문자열이 WPF_Example 전체에 0건이다"
    - "MenuBar 의 Platform 표시가 GetVersion() 반환값(1.4.0.0)을 자동으로 따라간다 (MenuBar 파일 무수정)"
    - "Debug/x64 Rebuild 가 PASS 한다"
  artifacts:
    - path: "WPF_Example/VersionDefine.cs"
      provides: "버전 단일 소스 (VERSION/BUILD_DATE const + Version 어트리뷰트 changelog)"
      contains: "public const string VERSION"
    - path: "WPF_Example/DatumMeasurement.csproj"
      provides: "VersionDefine.cs 컴파일 등록"
      contains: "VersionDefine.cs"
  key_links:
    - from: "WPF_Example/Properties/AssemblyInfo.cs"
      to: "ReringProject.VersionDefine.VERSION"
      via: "AssemblyVersion/AssemblyFileVersion 어트리뷰트 인자 (컴파일 타임 const)"
      pattern: "VersionDefine\\.VERSION"
    - from: "WPF_Example/Utility/RecipeFileHelper.cs"
      to: "ReringProject.VersionDefine.VERSION"
      via: "GetVersion() 직접 반환"
      pattern: "VersionDefine\\.VERSION"
---

<objective>
버전 관리 단일 소스 `VersionDefine.cs` 도입 (FinalVision 패턴 이식, 복사 아님).

현재 버전이 3곳에 흩어져 있고 값도 불일치한다:
- AssemblyInfo.cs:54 `AssemblyVersion("24.11.6.1")`
- AssemblyInfo.cs:55 `AssemblyFileVersion("24.12.10.02")` ← 위와 다름
- RecipeFileHelper.cs:180 `GetVersion()` 이 런타임에 exe FileVersion 을 읽어 MenuBar 에 표시

Purpose: `VersionDefine.VERSION` const 하나를 이 세 곳이 모두 참조하도록 일원화. 이후 버전 변경은 한 파일에서만.
Output: 신규 VersionDefine.cs 1개 + 기존 3파일 수정. 시작 버전 1.4.0.0 / BUILD_DATE 2026-07-10.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

<facts>
오케스트레이터 검증 완료 사실 (재조사 불필요):
- App.config 에 DatumMeasurement 자기 자신 bindingRedirect 없음 → 버전 하향(24.x → 1.4) 안전.
- csproj 는 classic-style(packages.config) → 신규 .cs 는 수동 Compile Include 필수. 누락 시 CS0103.
- csproj 루트 레벨 Compile Include 삽입 지점: 337행 `<Compile Include="SystemHandler.cs" />` 인접 (같은 들여쓰기 레벨).
- RecipeFileHelper.cs 의 `System.Reflection` 은 181행에서 **inline 완전한정**(`System.Reflection.Assembly...`)으로만 사용 — `using System.Reflection` 문 자체가 없음. GetVersion 본문 교체 시 제거할 using 없음.
- RecipeFileHelper.cs 15행 `using System.Diagnostics;` 는 GetDLLVersion(187행)의 `FileVersionInfo` 가 계속 사용 → **절대 제거 금지**.
- AlligatorAlgMil.dll 은 bin/x64/Debug/ 에 실물 존재 → GetDLLVersion 정상 동작 → **건드리지 말 것**.
- RootNamespace = ReringProject. 신규 파일 namespace 는 `ReringProject` (FinalVision 의 FinalVisionProject 아님).
</facts>

<reference_pattern>
FinalVision 원본 D:\Backup\파이널비전\FinalVision\WPF_Example\VersionDefine.cs 의 **구조만** 참조:
- `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class VersionAttribute : Attribute` (Number/Date/Change 3 public string 자동 프로퍼티)
- `[Version(...)]` 어트리뷰트를 클래스 위에 스택으로 쌓아 changelog 를 코드에 기록
- `public static class VersionDefine { public const string VERSION = ...; public const string BUILD_DATE = ...; }`
원본 내용/namespace/과거 이력은 복사하지 말 것. 우리는 [Version] 1개만 시작.
</reference_pattern>

<current_code>
AssemblyInfo.cs 53~55행:
```
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("24.11.6.1")]
[assembly: AssemblyFileVersion("24.12.10.02")]
```

RecipeFileHelper.cs 180~184행:
```
public string GetVersion() {
    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
    return fvi.FileVersion;
}
```
</current_code>
</context>

<tasks>

<task type="auto">
  <name>Task 1: VersionDefine.cs 신설 + 3파일을 단일 상수 참조로 교체 (원자적 4파일)</name>
  <files>WPF_Example/VersionDefine.cs, WPF_Example/DatumMeasurement.csproj, WPF_Example/Properties/AssemblyInfo.cs, WPF_Example/Utility/RecipeFileHelper.cs</files>
  <action>
**코딩 규칙 (엄수):** C# 7.2 / .NET Framework 4.8 — C# 8.0+ 기능(nullable ref, switch expression, record) 금지. 삼항 `?:` 신규 사용 금지 → if-else. 헝가리언 접두사 기존 표기 유지. 회귀 0. 초보자 가독성 우선. 수정/추가 라인에 `//260710 hbk` 주석.

**1) 신규 `WPF_Example/VersionDefine.cs` 생성 (Allman brace, namespace ReringProject):**
   - 파일 최상단에 `//260710 hbk` 주석으로 목적 명시(버전 관리 단일 소스).
   - `using System;`
   - `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]` 를 단 `public class VersionAttribute : Attribute` — `Number` / `Date` / `Change` 3개 public string 자동 프로퍼티(get/set).
   - `VersionDefine` 클래스 위에 `[Version(...)]` **정확히 1개**:
     - Number = "1.4.0.0"
     - Date = "2026-07-10"
     - Change = 아래 한국어 내용을 한 문자열로(각 문장 마침표로 구분):
       "버전 관리를 VersionDefine.cs 로 일원화(AssemblyVersion/AssemblyFileVersion/RecipeFileHelper.GetVersion 이 모두 단일 상수 참조. 기존엔 AssemblyVersion 24.11.6.1 과 AssemblyFileVersion 24.12.10.02 가 불일치). 죽은 코드 스윕 2,310줄 삭제(csproj 미등록 파일 6개 + 참조 0 메서드/필드 + 주석블록/미사용 using). skip-사유 문자열(DATUM_FAIL/ALIGN_FAIL/NO_IMAGE)을 SkipReason 상수로 통합해 오타 시 silent 오동작 제거."
   - `public static class VersionDefine` 안에:
     - `public const string VERSION = "1.4.0.0";`
     - `public const string BUILD_DATE = "2026-07-10";`
   - **주의: 반드시 `const` (컴파일 타임 상수). `static readonly` 로 만들면 AssemblyVersion 어트리뷰트 인자로 못 써 CS0182 오류.**

**2) `WPF_Example/DatumMeasurement.csproj` — VersionDefine.cs 컴파일 등록:**
   - 337행 `<Compile Include="SystemHandler.cs" />` 인접(같은 들여쓰기 레벨)에 `<Compile Include="VersionDefine.cs" />` 삽입. 경로 구분자는 백슬래시(단일 파일이라 무관하나 규칙 준수). classic-style 이라 이 등록이 없으면 CS0103.

**3) `WPF_Example/Properties/AssemblyInfo.cs` — 54/55행 하드코딩 → 상수 참조:**
   - 54행 → `[assembly: AssemblyVersion(ReringProject.VersionDefine.VERSION)]      //260710 hbk 버전 단일 소스`
   - 55행 → `[assembly: AssemblyFileVersion(ReringProject.VersionDefine.VERSION)]  //260710 hbk 버전 단일 소스`
   - 53행 주석처리된 `// [assembly: AssemblyVersion("1.0.*")]` 은 그대로 두거나 함께 삭제 — executor 판단(가독성 우선).

**4) `WPF_Example/Utility/RecipeFileHelper.cs` — GetVersion() 이 상수 직접 반환:**
   - 180~184행 본문을 교체하여 `return ReringProject.VersionDefine.VERSION;` 만 반환. `//260710 hbk` 주석 추가.
   - **`using System.Diagnostics;`(15행) 제거 금지** — GetDLLVersion 이 FileVersionInfo 사용.
   - **GetDLLVersion(186행) / label_DLLVersion 절대 무수정.**
   - `System.Reflection` 은 inline 한정 사용뿐이라 제거할 using 없음(그대로 둘 것).

**절대 금지:** MenuBar.xaml / MenuBar.xaml.cs 수정(GetVersion 반환값만 바뀌면 표시 자동 반영). 과거 마일스톤 이력 seed. 계획에 없는 파일 수정. carry-over 버그 3건(DeviceHandler.cs:83,89 / BaslerCamera.cs:660 / Logging.cs:366) 수정.
  </action>
  <verify>
    <automated>"/c/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -t:Rebuild</automated>
  </verify>
  <done>
- Debug/x64 Rebuild PASS.
- `grep -n "VersionDefine.cs" WPF_Example/DatumMeasurement.csproj` → 1건.
- `grep -rn "24.11.6.1\|24.12.10.02" WPF_Example/` → 0건.
- PowerShell `(Get-Item WPF_Example\bin\x64\Debug\DatumMeasurement.exe).VersionInfo.FileVersion` → `1.4.0.0`.
  </done>
</task>

</tasks>

<verification>
전체 검증 (task 완료 후):
1. Debug/x64 **Rebuild** PASS — Git Bash 는 `-p:` 사용(`/p:` 는 MSB1008 경로 오류).
   `"/c/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -t:Rebuild`
2. 산출물 FileVersion: PowerShell `(Get-Item WPF_Example\bin\x64\Debug\DatumMeasurement.exe).VersionInfo.FileVersion` → `1.4.0.0`.
3. 하드코딩 잔존 0: `grep -rn "24.11.6.1\|24.12.10.02" WPF_Example/` → 0건.
4. 등록 확인: `grep -n "VersionDefine.cs" WPF_Example/DatumMeasurement.csproj` → 1건.
</verification>

<success_criteria>
- VersionDefine.cs 존재(namespace ReringProject, const VERSION="1.4.0.0", const BUILD_DATE="2026-07-10", [Version] 어트리뷰트 1개).
- AssemblyVersion/AssemblyFileVersion/GetVersion() 3곳 모두 VersionDefine.VERSION 참조.
- 하드코딩 버전 문자열 0건.
- Debug/x64 Rebuild PASS, exe FileVersion = 1.4.0.0.
- atomic commit 1개(4파일, code only).
</success_criteria>

<output>
완료 후 `.planning/quick/260710-fzx-versiondefine/260710-fzx-SUMMARY.md` 생성.
SUMMARY 에 반드시 기록할 육안 확인 항목: MenuBar 의 "Platform : 24.12.10.02" → "Platform : 1.4.0.0" 로 표시 변경됨(의도된 변경, 다음 앱 실행 시 확인).
</output>
