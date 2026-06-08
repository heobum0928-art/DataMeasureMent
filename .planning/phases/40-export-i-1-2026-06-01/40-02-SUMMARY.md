---
phase: 40-export-i-1-2026-06-01
plan: "02"
subsystem: infra
tags: [closedxml, nuget, excel, xlsx, net48, openxml, sixlabors]

# Dependency graph
requires:
  - phase: 40-export-i-1-2026-06-01
    plan: "01"
    provides: "CycleResultDto JSON 영속화 — xlsx export 의 데이터 소스"
provides:
  - "ClosedXML 0.105.0 + 전이 의존성 packages.config/csproj 등록"
  - "ExcelExportSmokeTest.TryCreateWorkbook() — .NET 4.8 런타임 XLWorkbook 생성 검증"
  - "Plan 04 (ExcelExportService) 선행 의존성 완전 해소"
affects:
  - 40-04 (ExcelExportService)

# Tech tracking
tech-stack:
  added:
    - "ClosedXML 0.105.0 (MIT, netstandard2.0)"
    - "ClosedXML.Parser 2.0.0 (netstandard2.0)"
    - "DocumentFormat.OpenXml 3.1.1 (net46)"
    - "DocumentFormat.OpenXml.Framework 3.1.1 (net46)"
    - "ExcelNumberFormat 1.1.0 (netstandard2.0)"
    - "RBush.Signed 4.0.0 (netstandard2.0)"
    - "SixLabors.Fonts 1.0.0 (netstandard2.0)"
  patterns:
    - "classic NuGet (packages.config) 전이 의존성 수동 등록 패턴 — packages 폴더 실제 복원 버전 확인 후 수동 추가"
    - "smoke test static class 패턴 — ExcelExportSmokeTest (테스트 프레임워크 없는 환경에서 런타임 어셈블리 로드 검증)"

key-files:
  created:
    - "WPF_Example/Custom/Export/ExcelExportSmokeTest.cs"
  modified:
    - "WPF_Example/packages.config"
    - "WPF_Example/DatumMeasurement.csproj"

key-decisions:
  - "SixLabors.Fonts 1.0.0 채택 — 계획서 2.1.3 은 netstandard2.0 폴더 부재로 .NET 4.8 로드 불가. ClosedXML 0.105.0 실제 최소 요건은 1.0.0 이며 런타임 PASS 확인"
  - "Microsoft.Bcl.HashCode 미설치 — ClosedXML 0.105.0 전이 의존성에 포함되지 않음(ASSUMED 항목); 설치 불필요"
  - "App.config binding redirect 추가 없음 — DocumentFormat.OpenXml 3.1.1 이 기존 System.Memory/Unsafe redirect 와 충돌 없음(Pitfall 2 미발생)"

patterns-established:
  - "ExcelExportSmokeTest 패턴: 테스트 프레임워크 없는 WPF 환경에서 외부 라이브러리 런타임 로드를 static bool TryCreateWorkbook(out string error) 로 검증"

requirements-completed: [OUT-02]

# Metrics
duration: 45min
completed: 2026-06-01
---

# Phase 40 Plan 02: ClosedXML NuGet 수동 등록 + 런타임 Smoke Test Summary

**ClosedXML 0.105.0 + 6종 전이 의존성을 classic NuGet packages.config 에 수동 등록하고, .NET 4.8 런타임에서 XLWorkbook 생성 + xlsx 저장을 smoke test 로 검증 완료 — Plan 04 xlsx export 선행 의존성 해소**

## Performance

- **Duration:** 약 45분
- **Started:** 2026-06-01T00:30:00Z
- **Completed:** 2026-06-01T02:00:00Z (human-verify PASS 포함)
- **Tasks:** 3 (Task 1 auto + Task 2 auto + Task 3 checkpoint — 사용자 승인)
- **Files modified:** 3 (packages.config, DatumMeasurement.csproj, ExcelExportSmokeTest.cs)

## Accomplishments

- ClosedXML 0.105.0 및 6종 전이 의존성을 packages.config + csproj 에 등록, msbuild Debug/x64 Rebuild 0 errors 달성
- ExcelExportSmokeTest.cs 작성 — TryCreateWorkbook() 이 XLWorkbook 생성 → xlsx 저장 → true 반환하는 smoke test
- .NET 4.8 런타임에서 SixLabors.Fonts 1.0.0 + DocumentFormat.OpenXml 3.1.1 어셈블리 로드 성공 확인 (사용자 2026-06-01 PASS 승인)
- Plan 04 (ExcelExportService) 선행 BLOCKING 의존성 완전 해소

## 확정된 패키지 버전 (실제 설치 결과)

| 패키지 | 버전 | TFM | 비고 |
|--------|------|-----|------|
| ClosedXML | 0.105.0 | netstandard2.0 | 핵심 라이브러리 |
| ClosedXML.Parser | 2.0.0 | netstandard2.0 | ClosedXML 전이 의존성 |
| DocumentFormat.OpenXml | 3.1.1 | net46 | xlsx 기반 포맷 |
| DocumentFormat.OpenXml.Framework | 3.1.1 | net46 | OpenXml 전이 의존성 |
| ExcelNumberFormat | 1.1.0 | netstandard2.0 | 셀 숫자 포맷 |
| RBush.Signed | 4.0.0 | netstandard2.0 | 공간 인덱스 (ClosedXML 내부) |
| SixLabors.Fonts | **1.0.0** | netstandard2.0 | **계획서 2.1.3 에서 변경** (아래 편차 참조) |
| Microsoft.Bcl.HashCode | (미설치) | — | ClosedXML 0.105.0 미의존 — 불필요 |

## Task Commits

1. **Task 1: ClosedXML + 전이 의존성 설치 및 packages.config/csproj 등록** — `1a70634` (chore)
2. **Task 2: new XLWorkbook() 런타임 로드 smoke test** — `de83744` (feat)
3. **Task 3: checkpoint:human-verify** — 사용자 PASS 승인 2026-06-01 (코드 커밋 없음)

## Files Created/Modified

- `WPF_Example/packages.config` — ClosedXML 0.105.0 + 6종 전이 의존성 package 항목 추가
- `WPF_Example/DatumMeasurement.csproj` — ClosedXML 등 신규 어셈블리 `<Reference><HintPath>` 추가
- `WPF_Example/Custom/Export/ExcelExportSmokeTest.cs` — TryCreateWorkbook() smoke test 신규 생성

## Decisions Made

1. **SixLabors.Fonts 1.0.0 채택**: 계획서는 2.1.3 을 명시했으나, SixLabors.Fonts 2.1.3 패키지에는 netstandard2.0 lib 폴더가 없어 .NET 4.8 환경에서 어셈블리 로드 불가. ClosedXML 0.105.0 의 실제 최소 요건은 1.0.0 이며, 런타임 smoke test 에서 PASS 확인.

2. **Microsoft.Bcl.HashCode 미설치**: 계획서 ASSUMED 항목으로 기재되었으나 ClosedXML 0.105.0 의 실제 전이 의존성에 포함되지 않음. 설치 불필요.

3. **App.config binding redirect 추가 없음**: DocumentFormat.OpenXml 3.1.1 이 기존 System.Memory 4.5.5 / System.Runtime.CompilerServices.Unsafe 6.0.0 redirect 와 버전 충돌 없음. RESEARCH Pitfall 2 미발생.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] SixLabors.Fonts 버전 1.0.0 으로 변경**
- **Found during:** Task 1 (전이 의존성 설치 후 packages 폴더 검증)
- **Issue:** 계획서 인터페이스 섹션에 SixLabors.Fonts 2.1.3 으로 명시되었으나, nuget.org 2.1.3 패키지에는 netstandard2.0 lib 폴더가 존재하지 않아 .NET 4.8 어셈블리 로드 불가
- **Fix:** ClosedXML 0.105.0 이 실제 의존하는 SixLabors.Fonts 1.0.0 으로 대체 설치 및 등록
- **Verification:** Task 3 smoke test 에서 SixLabors.Fonts 어셈블리 로드 성공, XLWorkbook() 반환 true — 사용자 PASS 승인

---

**Total deviations:** 1 auto-fixed (Rule 1 - 버전 불일치로 인한 런타임 로드 불가 수정)
**Impact on plan:** 필수 수정 — 2.1.3 유지 시 .NET 4.8 어셈블리 로드 예외로 smoke test 실패. 스코프 변경 없음.

## Issues Encountered

- Microsoft.Bcl.HashCode 가 ClosedXML 0.105.0 전이 의존성이 아님을 설치 후 packages 폴더 확인으로 발견 — 계획서 ASSUMED 항목이므로 미설치가 올바른 동작

## User Setup Required

None - 외부 서비스 설정 불필요. NuGet 패키지는 packages/ 폴더에 이미 복원됨.

## Next Phase Readiness

- **Plan 04 (ExcelExportService)** 실행 가능 — ClosedXML 선행 의존성 완전 해소
- Plan 04 에서 `ExcelExportSmokeTest` 클래스는 의존성 검증 완료 후 제거 가능 (파일 헤더 주석에 명시)
- `using ClosedXML.Excel;` + `new XLWorkbook()` 패턴이 .NET 4.8 + Debug/x64 환경에서 정상 동작함을 검증 완료

## Self-Check: PASSED

- `WPF_Example/Custom/Export/ExcelExportSmokeTest.cs` FOUND
- `WPF_Example/packages.config` 내 ClosedXML 항목 FOUND
- 커밋 `1a70634` FOUND
- 커밋 `de83744` FOUND

---
*Phase: 40-export-i-1-2026-06-01*
*Completed: 2026-06-01*
