---
phase: quick-260710-fzx
plan: 01
subsystem: infra
tags: [assemblyinfo, version-management, csproj, build]

# Dependency graph
requires: []
provides:
  - "VersionDefine.cs 단일 소스(const VERSION/BUILD_DATE) — AssemblyVersion/AssemblyFileVersion/RecipeFileHelper.GetVersion() 3곳이 모두 참조"
affects: [향후 버전 변경 작업 전체]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "VersionAttribute + [Version(...)] 스택 패턴으로 changelog 를 코드에 기록 (FinalVision 원본 구조 참조, 복사 아님)"

key-files:
  created:
    - WPF_Example/VersionDefine.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
    - WPF_Example/Properties/AssemblyInfo.cs
    - WPF_Example/Utility/RecipeFileHelper.cs

key-decisions:
  - "VERSION/BUILD_DATE 는 const (static readonly 아님) — AssemblyVersion 어트리뷰트 인자로 쓰이므로 컴파일 타임 상수 필수(CS0182 회피)"
  - "changelog Change 문자열에 과거 하드코딩 값(24.11.6.1/24.12.10.02)을 역사적 기록으로 그대로 남김 — 사용자 지정 문구 그대로 사용"

patterns-established:
  - "버전 변경 절차: VersionDefine.cs 의 VERSION/BUILD_DATE 수정 + 그 위에 [Version(...)] 항목 새로 추가(기존 항목 보존, 스택 누적)"

requirements-completed: [VERSIONDEFINE-01]

duration: 20min
completed: 2026-07-10
---

# Quick 260710-fzx: VersionDefine.cs 단일 소스 도입 Summary

**버전 정보가 흩어져 있던 3곳(AssemblyVersion 24.11.6.1 / AssemblyFileVersion 24.12.10.02 / RecipeFileHelper.GetVersion 런타임 조회)을 `VersionDefine.VERSION` const 단일 상수로 일원화, 시작 버전 1.4.0.0**

## Performance

- **Duration:** ~20분
- **Tasks:** 1 (원자적 4파일)
- **Files modified:** 4 (1 신규 + 3 수정)

## Accomplishments
- `WPF_Example/VersionDefine.cs` 신설: `VersionAttribute` (Number/Date/Change) + `[Version(...)]` 1개 + `VersionDefine` 정적 클래스(`const string VERSION = "1.4.0.0"`, `const string BUILD_DATE = "2026-07-10"`)
- `AssemblyInfo.cs` 의 `AssemblyVersion("24.11.6.1")` / `AssemblyFileVersion("24.12.10.02")` 하드코딩 두 값(서로 불일치했음) → 둘 다 `ReringProject.VersionDefine.VERSION` 참조로 교체
- `RecipeFileHelper.GetVersion()` 런타임 `FileVersionInfo` 조회 방식 → `VersionDefine.VERSION` 직접 반환으로 교체 (GetDLLVersion 은 무손상 유지)
- `DatumMeasurement.csproj` 에 `<Compile Include="VersionDefine.cs" />` 등록 (classic-style csproj, 수동 등록 필수)

## Task Commits

1. **Task 1: VersionDefine.cs 신설 + 3파일을 단일 상수 참조로 교체** - `dad5b32` (feat)

_참고: quick task 정책상 SUMMARY/STATE 등 문서 커밋은 오케스트레이터가 별도 처리._

## Files Created/Modified
- `WPF_Example/VersionDefine.cs` - 버전 단일 소스. VersionAttribute + [Version(...)] changelog + const VERSION/BUILD_DATE
- `WPF_Example/DatumMeasurement.csproj` - VersionDefine.cs Compile Include 등록 (337행 SystemHandler.cs 인접)
- `WPF_Example/Properties/AssemblyInfo.cs` - AssemblyVersion/AssemblyFileVersion 하드코딩 제거, VersionDefine.VERSION 참조
- `WPF_Example/Utility/RecipeFileHelper.cs` - GetVersion() 본문을 VersionDefine.VERSION 직접 반환으로 교체 (GetDLLVersion/using System.Diagnostics 무수정)

## Decisions Made
- `const` 필수 확인: `static readonly` 로 선언하면 AssemblyVersion 어트리뷰트 인자에 쓸 수 없어 CS0182 컴파일 오류 — 계획 지시대로 `const` 로 선언, 실제로 컴파일 통과 확인.
- Change changelog 문자열에 과거 하드코딩 버전 문자열(24.11.6.1, 24.12.10.02)을 역사적 기록으로 그대로 포함 — 사용자가 확정한 정확한 문구를 그대로 사용(scope_lock 준수).

## Deviations from Plan

None - 계획대로 정확히 실행됨. 4파일 원자적 커밋 1개.

## Issues Encountered

**grep "0건" 검증의 뉘앙스:** `grep -rn "24.11.6.1\|24.12.10.02" WPF_Example/` 는 엄밀히 2개 파일에서 매치된다 (아래 "증거" 섹션 참조). 둘 다 **의도된/기존 존재하는 문서성 문자열**이며 실제 빌드/런타임에 영향을 주는 하드코딩 참조가 아니다:
1. `WPF_Example/VersionDefine.cs` — 계획이 명시적으로 지시한 changelog 텍스트 자체("기존엔 AssemblyVersion 24.11.6.1 과 AssemblyFileVersion 24.12.10.02 가 불일치"라는 역사적 서술). 이 파일이 버전의 유일한 소스이므로 이 문자열이 여기 있는 것은 목적에 부합.
2. `WPF_Example/ReadMe` — 계획 범위 밖의 **기존(pre-existing) 텍스트 로그 파일** (컴파일 대상 아님, 2024-11-06 날짜의 과거 변경 기록). 계획에 없는 파일이라 무수정 유지.

*.cs 소스 파일만 필터링하면 `VersionDefine.cs` 외 0건 — `AssemblyInfo.cs`/`RecipeFileHelper.cs` 는 완전히 정리됨을 확인.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

**육안 확인 항목 (다음 앱 실행 시):**
- MenuBar 의 "Platform : 24.12.10.02" 표시 → "Platform : 1.4.0.0" 로 변경됨 (의도된 변경, MenuBar.xaml/.xaml.cs 무수정 — GetVersion() 반환값만 바뀌어 자동 반영).

**carry-over 버그 3건 — 이번 작업 범위 밖, 미수정 상태 유지:**
- `DeviceHandler.cs:83,89` `Select(...)!=null`
- `BaslerCamera.cs:660` `{2}` 포맷
- `Logging.cs:366` `Debug.Assert(true,...)`

**향후 버전 변경 절차:** `VersionDefine.cs` 의 `VERSION`/`BUILD_DATE` const 값을 수정하고, `VersionDefine` 클래스 위에 `[Version(...)]` 항목을 하나 더 쌓는다 (기존 항목은 삭제하지 않고 스택으로 유지 — changelog 이력 보존).

---

## 증거 (Evidence)

**1) `WPF_Example/VersionDefine.cs` 내용 확인:**
```
namespace ReringProject
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class VersionAttribute : Attribute { ... }

    [Version(Number = "1.4.0.0", Date = "2026-07-10", Change = "...")]
    public static class VersionDefine
    {
        public const string VERSION = "1.4.0.0";
        public const string BUILD_DATE = "2026-07-10";
    }
}
```

**2) `grep -n "VersionDefine.cs" WPF_Example/DatumMeasurement.csproj`**
```
338:    <Compile Include="VersionDefine.cs" />
```
→ 1건 (예상대로).

**3) `grep -rn "24.11.6.1\|24.12.10.02" WPF_Example/`**
```
WPF_Example\VersionDefine.cs   (changelog 텍스트, 계획 지시 그대로)
WPF_Example\ReadMe             (범위 밖 기존 텍스트 로그, 무수정)
```
→ *.cs 소스만 필터링(`grep -rn ... --include=*.cs`) 시 `VersionDefine.cs` 1건 외 0건. `AssemblyInfo.cs`/`RecipeFileHelper.cs` 는 완전히 정리 확인됨.

**4) `grep -n "GetDLLVersion" WPF_Example/Utility/RecipeFileHelper.cs`**
```
185:        public string GetDLLVersion() {
```
→ 존재 확인 (무손상).

**5) `grep -n "using System.Diagnostics" WPF_Example/Utility/RecipeFileHelper.cs`**
```
15:using System.Diagnostics;
```
→ 존재 확인 (무손상).

**6) Debug/x64 Rebuild 결과:**
```
"/c/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -t:Rebuild
...
DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe
```
→ PASS. 출력된 경고 6건은 모두 이번 작업과 무관한 기존 CS0618(Obsolete)/CS0162(도달불가) 경고 — 신규 CS 오류 0건.

**7) exe FileVersion 확인:**
```powershell
(Get-Item 'C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe').VersionInfo.FileVersion
```
결과: `1.4.0.0`

**8) 커밋 후 삭제 파일 확인 (`git diff --diff-filter=D --name-only HEAD~1 HEAD`):** 출력 없음 → 의도치 않은 파일 삭제 없음.

## Self-Check: PASSED

- FOUND: WPF_Example/VersionDefine.cs
- FOUND: dad5b32 (commit exists in git log)
- FOUND: .planning/quick/260710-fzx-versiondefine/260710-fzx-SUMMARY.md

---
*Quick task: 260710-fzx-versiondefine*
*Completed: 2026-07-10*
