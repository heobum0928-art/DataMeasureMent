---
phase: quick-260710-di7
plan: 01
subsystem: sequence-inspection
tags: [cleanup, dead-code, constants, skip-reason]
requires: []
provides:
  - "SkipReason 단일 소스 상수 (DATUM_FAIL/ALIGN_FAIL/NO_IMAGE)"
affects:
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
  - WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs
  - WPF_Example/Custom/Export/ExcelExportService.cs
  - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
tech-stack:
  added: []
  patterns:
    - "skip-사유 문자열은 SkipReason 정적 클래스 상수로만 선언, 나머지 실행 코드는 참조만"
key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/SkipReason.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
    - WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs
    - WPF_Example/Custom/Export/ExcelExportService.cs
    - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "Bottom_Sequence_0428.zip 은 git 미추적(ignored) 상태였음 → git rm 대신 rm -f 로 디스크에서만 삭제"
  - "삭제 메서드명(ClassifyFai/TryRunDatumPhase) 을 설명 주석에 리터럴로 남기지 않음 — verify grep 재출현 방지 목적으로 우회 표현 사용"
  - "ExcelExportService(ReringProject.Export)/ReviewMeasurementRow(ReringProject.UI) 에 using ReringProject.Sequence 신규 추가"
metrics:
  duration: "~30분"
  completed: "2026-07-10"
---

# Phase quick-260710-di7 Plan 01: 죽은 코드 정리 + skip-사유 문자열 상수화 Summary

시퀀스 계층에서 호출부 0건인 죽은 메서드 3개(ClassifyFai, TryRunDatumPhase x2)와 방치된 백업 zip을 삭제하고, `"DATUM_FAIL"`/`"ALIGN_FAIL"`/`"NO_IMAGE"` skip-사유 문자열이 8개 파일에 복붙되어 있던 것을 `SkipReason` 단일 상수 클래스로 통합했다. 런타임 동작·문자열 값은 100% 동일(회귀 0), Debug/x64 빌드 PASS.

## Task 1: 죽은 코드 3메서드 + 백업 zip 삭제

**삭제 전 재검증 결과** (`grep -rn "ClassifyFai\|TryRunDatumPhase" --include=*.cs WPF_Example/`):
```
InspectionSequence.cs:611  (주석)
InspectionSequence.cs:631  private EVisionResultType ClassifyFai(FAIConfig fai)
InspectionSequence.cs:771  public bool TryRunDatumPhase(HImage image, out string error)
InspectionSequence.cs:807  public bool TryRunDatumPhase(HImage image1, HImage image2, out string error)
```
기대대로 정의부(및 주석)만 출현, 다른 파일 호출부 0건 확인 → 계획대로 3개 메서드 전부 삭제 진행.

**삭제 내용:**
- `ClassifyFai(FAIConfig fai)` — 살아있는 `ClassifyMeasurement(MeasurementBase)`(:612)로 완전 대체되어 죽은 코드였음. 전체 삭제.
- `TryRunDatumPhase(HImage image, out string error)` — 호출부 0건. 전체 삭제.
- `TryRunDatumPhase(HImage image1, HImage image2, out string error)` — 호출부 0건. 전체 삭제.
- `:611` 주석에서 `(ClassifyFai 측정 단위 복제)` 문구 제거(주석 나머지 보존).
- 삭제 사유 주석은 메서드명을 리터럴로 다시 적지 않고 우회 서술("죽은 FAI 단위 분류 메서드", "미사용 Datum phase 실행 오버로드 2종")로 작성 — verify grep(기대 0)이 우발적으로 자기 자신의 주석에 걸리는 것을 방지.
- `ClearDatumTransforms`/`MarkDatumFailed`/`IsDatumFailed`/`ClassifyMeasurement` 등 인접 살아있는 헬퍼는 무손상.

**Bottom_Sequence_0428.zip:** `git ls-files`/`git status --short --ignored` 로 확인한 결과 이미 git 미추적(ignored) 상태였다(`!!` 표시). 계획의 "파일이 없거나 untracked 이면 rm -f 후 SUMMARY 기록" 분기 적용 → `rm -f` 로 디스크에서 삭제. `git rm` 불필요(추적 대상이 아니었음).

**Verify 결과:**
```
$ grep -rn "ClassifyFai\|TryRunDatumPhase" --include=*.cs WPF_Example/ | wc -l
0
```
Debug/x64 빌드 PASS (아래 통합 빌드 로그 참조).

## Task 2: skip-사유 문자열 SkipReason 단일 상수화

**0단계 grep 전체 출현 확정** (`grep -rn '"DATUM_FAIL"\|"ALIGN_FAIL"\|"NO_IMAGE"' --include=*.cs WPF_Example/`):
- 실행 코드(치환 대상): `Action_FAIMeasurement.cs:526,527,656,677`, `InspectionSequence.cs:615`, `MeasurementHistoryCsvWriter.cs:114,115`, `MeasurementHistoryCsvLoader.cs:180,182,185,187`, `RepeatMeasurementStats.cs:108`, `ExcelExportService.cs:91,95`(구 라인번호), `ReviewMeasurementRow.cs:83,87`
- 주석 전용(수정 대상 아님, 그대로 둠): `CycleResultSerializer.cs:128`, `MeasurementBase.cs:65`, `RepeatMeasurementStats.cs:57`, `CycleResultDto.cs:89`, `ReviewMeasurementRow.cs:28`

**신규 파일** `WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` (Allman 스타일, namespace `ReringProject.Sequence`):
```csharp
public static class SkipReason
{
    public const string DATUM_FAIL = "DATUM_FAIL";
    public const string ALIGN_FAIL = "ALIGN_FAIL";
    public const string NO_IMAGE = "NO_IMAGE";
}
```
값은 기존 리터럴과 바이트 단위 동일.

**csproj 등록:** `WPF_Example/DatumMeasurement.csproj` 의 `MeasurementBase.cs` Compile Include 바로 아래에 `SkipReason.cs` 추가(classic-style csproj, 자동 포함 안 됨).

**실행 코드 치환 (8개 파일 전부, 값 무변경):**
- `Action_FAIMeasurement.cs` — 526,527 (`ALIGN_FAIL`/`DATUM_FAIL` 대입), 656 (비교), 677 (`NO_IMAGE` 대입)
- `InspectionSequence.cs` — 615 (`ClassifyMeasurement` 내부 비교)
- `MeasurementHistoryCsvWriter.cs` — 114,115 (`MapJudgement` 비교+return)
- `MeasurementHistoryCsvLoader.cs` — 180,182,185,187 (비교+대입)
- `RepeatMeasurementStats.cs` — 108 (비교)
- `ExcelExportService.cs` — 91→92, 95→96 (비교) — namespace `ReringProject.Export` 이므로 `using ReringProject.Sequence;` 신규 추가
- `ReviewMeasurementRow.cs` — 83→84, 87→88 (비교) — namespace `ReringProject.UI` 이므로 `using ReringProject.Sequence;` 신규 추가

**Verify 결과:**
```
$ grep -rn '"DATUM_FAIL"\|"ALIGN_FAIL"\|"NO_IMAGE"' --include=*.cs WPF_Example/ | grep -v "SkipReason.cs"
WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs:128:  // null or "DATUM_FAIL"   (주석)
WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:65:         // ... "DATUM_FAIL" ...    (주석)
WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs:57:  /// ... "DATUM_FAIL" ...   (주석)
WPF_Example/UI/ViewModel/CycleResultDto.cs:89:                        /// ... "DATUM_FAIL" ...    (주석)
WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs:29:                  /// ... "DATUM_FAIL" ...       (주석)
```
잔존 5건 전부 `///` 또는 `//` 주석 라인. 실행문(대입/return/비교) 리터럴 0건 — 기대치 충족.

## 빌드 검증

MSBuild 경로: `C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe`
명령: `MSBuild.exe WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Build`

Task 1 이후 빌드: PASS (경고만, 신규 오류 0)
Task 2 이후 빌드: PASS
```
  DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe
```
경고는 전부 기존에 존재하던 것(TopSequence/BottomSequence/TopInspectionAction/BottomInspectionAction Obsolete 경고, VirtualCamera.cs:237 CS0162 접근 불가 코드) — 이번 작업과 무관, 손대지 않음(scope 밖).

## Deviations from Plan

None — plan 그대로 실행됨. Task 1 재검증에서 호출부가 추가로 발견된 항목 없음(전부 삭제 진행). Bottom_Sequence_0428.zip 은 예상과 달리 git 미추적(ignored) 상태였음 — plan 의 대체 분기("파일 없거나 untracked 시 rm -f")를 그대로 적용, 별도 조치 불필요.

## Commits

- `5598532` refactor(di7): 죽은 코드 3메서드 삭제 (ClassifyFai, TryRunDatumPhase x2)
- `a51331a` refactor(di7): skip-사유 문자열 SkipReason 단일 상수로 통합

## Self-Check

- `WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` — FOUND
- `WPF_Example/DatumMeasurement.csproj` 에 `SkipReason.cs` Include — FOUND (grep 확인)
- commit `5598532` — FOUND (`git log --oneline --all | grep 5598532`)
- commit `a51331a` — FOUND (`git log --oneline --all | grep a51331a`)

## Self-Check: PASSED
