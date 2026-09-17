# Phase 78: 리뷰어 NG 원인 분석 - Research

**Researched:** 2026-09-17
**Domain:** WPF 결과 리뷰어(cycle.json 기반) 규칙 기반 원인 추정 + ClosedXML NG 누적 엑셀 + cycle.json 진단 필드 확장
**Confidence:** HIGH (전 발견이 실제 소스 코드 grep/Read + 실제 운영 데이터(D:/Data, 읽기 전용) 대조로 확인됨. 임계값 숫자만 ASSUMED)

## Summary

Phase 78 은 새 검사/UI 프레임워크를 도입하지 않는다 — 기존 `CycleResultDto`(cycle.json 단일 소스) · `ReviewerListLabelBuilder` · `ReviewMeasurementRow` · `ExcelExportService`/`RepeatExcelExportService`(ClosedXML 0.105.0) 위에 순수 함수 계층 하나("원인 규칙 엔진")를 얹고, 그 결과를 리뷰어 화면과 새 NG 누적 엑셀에 노출하는 작업이다. 이미 존재하는 데이터(측정값/공차/판정/SkipReason/SelectedZIndex/overlay 좌표)만으로 R1~R4, R7, R9 규칙은 전부 구현 가능하다. R5(기준점 흔들림)와 R8(초점 범위 끝값)의 정확도를 높이려면 D-78-08 이 요구하는 "기준점 각도·원점·매칭 점수, Z 후보별 점수"를 cycle.json 에 새로 기록해야 하는데, 이 데이터는 이미 런타임 객체(`DatumConfig`, `Action_FAIMeasurement.ZFocusRunResult`)에 존재하고 Algorithm 로그에도 찍히고 있어 "새로 계산"이 아니라 "이미 있는 값을 DTO 로 복사"하는 작업이다.

가장 중요한 발견 두 가지: (1) Datum 진단값은 두 갈래다 — `DatumFindingService`(HorizontalOnly 방식)는 `DatumConfig.DetectedOriginRow/Col/AngleDeg/EdgeCount/FitRMSE` 를 이미 채우지만, `InspectionSequence.TryComposeAlign`(패턴매칭 방식)의 매칭 점수(`curScore`)는 지역 변수일 뿐 `DatumConfig` 에 저장되지 않는다(2-패턴 baseline 보정용 `Align2Score`만 저장됨) — 이 phase 에서 `AlignMatchScore` 급의 새 필드가 하나 더 필요하다. (2) "엑셀 export" 삭제(D-78-06)는 `ExcelExportService.Export()` 메서드 1개 + 버튼만 지우는 것이지 클래스 전체 삭제가 아니다 — `BuildJudgementText`/`LoadCaptureImageBytes`/`TryInsertCaptureImage`/`ApplyCaptureColumnWidth`는 `RepeatExcelExportService`(유지 대상)가 그대로 재사용한다.

**Primary recommendation:** 원인 규칙 엔진은 새 순수정적 클래스(신규 파일 금지 → `CycleResultDto.cs` 안에 `NgCauseAnalyzer` static class 로 추가)로 만들어 `CycleResultDto`(및 필요 시 같은 날짜 폴더의 인접 cycle.json 리스트)만 입력받게 하고, 화면(`ReviewMeasurementRow`/`ReviewerWindow.xaml.cs` 배선)과 엑셀(새 `NgAccumulationExportService` — 이것도 `Custom/Export/ExcelExportService.cs` 안에 추가하거나 기존 export 파일 중 하나에 얹어 신규 .cs 파일을 피한다) 양쪽에서 그 결과(원인/근거/확인할 일 3 문자열)를 그대로 소비하게 한다.

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-78-01:** 목적은 NG 가 나면 왜 났는지 분석. 기능의 가치는 "원인 추정"이 핵심이고, 엑셀 저장·화면 표시는 그 결과를 전달하는 수단이다.
- **D-78-02:** NG 만 엑셀로 저장하고 한 파일에 계속 누적한다. 이미 들어간 사이클은 중복으로 넣지 않는다.
- **D-78-03:** 원인 분석은 규칙 기반 자동 판단(오프라인, 근거 숫자 표시). 외부 AI 사용 안 함.
- **D-78-04:** 원인 결과는 리뷰어 화면(NG 행 선택 시)과 NG 누적 엑셀 둘 다에 보여 준다.
- **D-78-05:** 사용자는 비전 초보 — 쉬운 한국어, 원인 1줄 + 근거 1줄 + 확인할 일 1줄.
- **D-78-06:** 리뷰어에서 "차트 이미지 캡처 점검" 버튼과 사이클 1건 "엑셀 export" 버튼을 삭제한다. 반복검사 묶음과 Align 정합 조회는 유지한다.
- **D-78-07:** 리뷰어는 실제 촬영 사진(OriginImageFileName 우선, 없으면 기존 경로)을 띄운다. 이번 phase 에 포함.
- **D-78-08:** 기준점 각도·원점·매칭 점수와 Z 후보별 선명도 점수를 cycle.json 에 기록만 추가한다(검사 로직·판정 불변, 옛 파일 호환).

### Claude's Discretion (열린 항목, 제안값 — 계획 단계에서 확정)

| ID | 항목 | 제안 |
|---|---|---|
| O-78-01 | NG 누적 엑셀 파일 위치·이름 | `ResultSavePath` 아래 고정 파일 1개(예: `NG_분석_누적.xlsx`), 열린 상태면 안내 후 중단 |
| O-78-03 | "NG" 범위 | 측정 NG + DETECT FAIL + NO IMAGE + 기준점 실패 포함. `Z_RANGE_PENDING`·중간 tick 제외 |
| O-78-04 | 중복 판단 키 | cycle 폴더 경로(`CycleFolderPath`) + 측정명 |
| O-78-05 | 화면 표시 위치 | NG 행 선택 시 WPF 영역(Halcon 창 위 airspace 회피 — 헤더/그리드 아래 패널)에 원인 3줄 |
| O-78-07 | 엑셀 열 구성 | 시각·레시피·Shot·FAI·측정명·측정값·공칭·공차·판정·사용 Z·**추정 원인·근거·확인할 일**·사진 경로 |

### Deferred Ideas (OUT OF SCOPE)

없음 — CONTEXT.md 에 별도 "Deferred Ideas" 섹션 없음. 원인 규칙 후보 표(R1~R9)의 임계값(같은 방향 판정 비율, 추세 N, 경계 %)은 "화면에 노출하지 않는 내부 const"로 명시 — 정확한 숫자는 계획 단계 재량.

## Project Constraints (from CLAUDE.md)

- C# 7.2 고정. 삼항 `?:` / 이항 `??`·`??=` / null 조건 `?.`·`?[]` / C# 8 `switch` 식 전부 금지 — `if/else`, 명시적 null 체크, 전통 `switch`(+ `break`)만 사용.
- 헝가리언 접두사(`b`/`n`/`sz`/`d`/`hv`), 매직넘버 금지(named const), 3개 이상 `&&`/`||` 조건은 이름 있는 bool 로 선추출.
- 새 UI 로직은 ViewModel/서비스 계층에만. `ReviewerWindow.xaml.cs` 는 배선(이벤트 핸들러 → 메서드 호출 1줄)만. `MainView.xaml.cs` 신규 로직 0.
- 날짜 주석(`//YYMMDD hbk`) 신규 금지(2026-06-11 정책 전환) — 이 phase 의 모든 새 주석은 `// Phase 78 NGA-xx: ...` 형식.
- `HImage`/`HObject`/`HTuple` 은 반드시 Dispose(`try { } finally { try { x.Dispose(); } catch { } }`). 이 phase 는 신규 Halcon 객체를 만들지 않을 가능성이 높지만(순수 데이터 규칙), 오버레이/이미지 재로드 경로를 건드리면 이 규칙이 적용된다.
- **신규 .cs 파일 금지** — csproj `<Compile Include>` 를 늘리면 안 된다(78-CONTEXT.md 핸드오프 제약). 새 클래스는 전부 기존 파일에 추가.
- `D:\Data*` 는 읽기 전용. Release 빌드 금지(OutputPath 가 배포 exe 를 덮어씀). Debug|x64 빌드만.
- 검사 로직·PLC 응답·CSV 포맷 불변. cycle.json 은 필드 추가만 허용, 옛 파일도 로드되어야 한다(Newtonsoft 기본 역직렬화 — 없는 필드는 C# 기본값/이니셜라이저 값으로 채워짐, 이 프로젝트는 이미 이 패턴을 여러 번 씀).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| NGA-01 | 원인 규칙 엔진 | "Architecture Patterns > 원인 규칙 엔진" + "Common Pitfalls" — 입력 데이터 인벤토리와 R1~R9 규칙별 정확한 필드 매핑을 아래 표에 정리 |
| NGA-02 | 화면 표시 | "Architecture Patterns > 화면 배치" + ReviewerWindow.xaml 구조 분석(airspace 회피 위치) |
| NGA-03 | NG 누적 엑셀 | "Don't Hand-Roll" + "Code Examples > ClosedXML 열기-추가 패턴" — 기존에 없는 패턴이라 새로 설계, 근거 있는 참조 패턴 제시 |
| NGA-04 | 리뷰어 기능 정리 | "Pitfall: ExcelExportService 부분 삭제" — 삭제 범위를 정확히 한정 |
| NGA-05 | 회귀 0·옛 데이터 호환 | "Runtime State Inventory" + "Common Pitfalls" — Newtonsoft 기본값 폴백 확인 |
| NGA-06 | 리뷰어 실제 촬영 사진 | "Architecture Patterns > D-78-07 실제 사진" — OriginImageFileName 저장 시점/무조건성 확인 |
| NGA-07 | 진단 값 cycle.json 기록 | "Architecture Patterns > D-78-08 진단 값 기록" — Datum 2갈래(HorizontalOnly/Align) + ZFocusRunResult 기록 지점 확정 |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| 원인 규칙 평가(NGA-01) | Backend(순수 로직, `ReringProject.UI` 네임스페이스 내 static 클래스) | — | `CycleResultDto` 만 입력받는 순수 함수 — HALCON/DB/네트워크 의존 없음. UI 프로젝트 안에 있지만 논리적으로는 "도메인 로직" 계층(기존 `ReviewerListLabelBuilder` 와 동일 위치) |
| 화면 표시(NGA-02) | Frontend(WPF code-behind 배선 + XAML) | Backend(원인 문자열 생성) | 표시 문자열은 규칙 엔진이 만들고, code-behind 는 바인딩만 — CLAUDE.md MVVM 규칙과 기존 `ReviewMeasurementRow` 패턴 일치 |
| NG 누적 엑셀(NGA-03) | Backend(파일 I/O, ClosedXML) | Frontend(버튼 배선) | 기존 `RepeatExcelExportService`/`ExcelExportService` 와 동일 계층 — `Custom/Export/` |
| 리뷰어 기능 정리(NGA-04) | Frontend(XAML 버튼 제거 + code-behind 핸들러 제거) | Backend(사용되지 않게 되는 메서드는 유지, 삭제 안 함 — 공유 헬퍼) | `ChartImageCapture`/`ExcelExportService` 클래스 자체는 `CpkReportExportService`/`RepeatExcelExportService` 가 재사용 중이라 존치 |
| 실제 촬영 사진(NGA-06) | Frontend(ReviewerWindow.xaml.cs 이미지 로드 경로) | — | `CycleResultDto.FaiResultDto.OriginImageFileName` 필드는 이미 존재 — 소비 지점만 교체 |
| 진단 값 기록(NGA-07) | Backend(Sequence/Algorithm 계층 — `InspectionSequence`, `Action_FAIMeasurement`, `DatumConfig`) | — | 검사 스레드가 이미 계산한 값을 DTO 로 복사 — 신규 계산 없음 |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ClosedXML | 0.105.0 [VERIFIED: WPF_Example/packages.config:5] | xlsx 읽기/쓰기 (`XLWorkbook`) | 이미 프로젝트에 설치·사용 중(`ExcelExportService`/`RepeatExcelExportService`/`CpkReportExportService`). 새로 설치할 패키지 없음 |
| Newtonsoft.Json | 13.0.3 [VERIFIED: WPF_Example/packages.config] | cycle.json 직렬화/역직렬화 | `CycleResultSerializer` 가 이미 사용, `TypeNameHandling.None` 고정(RCE 방지) 패턴 유지 필수 |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ClosedXML.Parser | 2.0.0 [VERIFIED: WPF_Example/packages.config:6] | ClosedXML 전이 의존성(수식 파싱) | 직접 호출 안 함 — ClosedXML 내부 의존성, 별도 조치 불필요 |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ClosedXML 열기→추가(append) | EPPlus, NPOI | 이미 설치된 라이브러리(ClosedXML)로 충분 — 새 의존성 추가는 CLAUDE.md 제약(신규 패키지 최소화 원칙) 및 프로젝트 관례에 반함. 불필요 |
| 규칙 엔진을 새 .cs 파일로 | 기존 파일(`CycleResultDto.cs`)에 static class 추가 | 78-CONTEXT.md 핸드오프가 "신규 .cs 파일 금지(csproj 스테이징 금지)"를 명시 — 새 파일은 옵션에서 배제 |

**Installation:** 없음 — 이 phase 는 새 NuGet 패키지를 설치하지 않는다. `packages.config`/csproj 변경 불필요.

**Version verification:** ClosedXML 0.105.0 / Newtonsoft.Json 13.0.3 은 `WPF_Example/packages.config` 에서 직접 확인됨(로컬 파일 읽기, npm/pip 레지스트리 조회 대상 아님 — .NET Framework classic packages.config 프로젝트이며 이미 설치·빌드된 버전).

## Package Legitimacy Audit

이 phase 는 새 외부 패키지를 설치하지 않는다(위 Standard Stack 참조 — ClosedXML/Newtonsoft 는 기존 설치본 재사용). Package Legitimacy Gate 대상 없음.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (신규 설치 없음) | — | — | — | — | — | N/A |

**Packages removed due to [SLOP] verdict:** 없음
**Packages flagged as suspicious [SUS]:** 없음

## Architecture Patterns

### System Architecture Diagram

```
[날짜 폴더 선택] (ReviewerWindow.Button_LoadFolder_Click)
        │
        ▼
[cycle.json 목록 로드] (LoadCycleFolders → CycleResultSerializer.Load 매 폴더)
        │  각 tick 의 CycleResultDto (Newtonsoft, TypeNameHandling.None)
        ▼
┌────────────────────────────────────────────────────────────┐
│ NgCauseAnalyzer (신규, 순수 함수 — CycleResultDto.cs 안에 추가)│
│                                                                │
│  입력: 현재 cycle 의 CycleResultDto                            │
│        + (R6/R9 추세 규칙용) 같은 날짜 폴더의 인접 cycle.json 들 │
│        + SkipReason 상수, MeasurementResultDto.SelectedZIndex, │
│          FaiResultDto.LastOverlays(FAI-DistLine Points[0]/[1]),│
│          (NGA-07 신규) DatumDiagnosticDto, ZCandidateScoreDto  │
│                                                                │
│  R1 NO_IMAGE ─┐                                               │
│  R2 DATUM_FAIL/ALIGN_FAIL/DATUM_REF_MISSING ─┤                │
│  R3 MEASURE_FAIL ─┤                                            │
│  R4 ZINDEX_MISCONFIGURED/CROSS_Z_INCOMPLETE ─┤  단일 대표 원인  │
│  R5 같은 기준선 여러 측정 공통 이동(overlay Points[0] diff) ─┤   │  (구체성 순위)
│  R6 최근 N 사이클 평균이 공차중심에서 한쪽 편향 ─┤              │  + 나머지 "함께 의심"
│  R7 그 사이클에서 그 측정만 NG(단독) ─┤                        │
│  R8 SelectedZIndex == Shot.ZIndexEnd(범위 끝) ─┤                │
│  R9 공차 경계 근처 + 직전 사이클 OK ─┘                          │
│                                                                │
│  출력: NgCauseResult { 원인1줄, 근거1줄, 확인할일1줄, 나머지원인목록 } │
└────────────────────────────────────────────────────────────┘
        │                                          │
        ▼ (측정 행 클릭 시)                         ▼ (버튼 클릭 시, D-78-02 전체 누적)
[화면 3줄 패널]                          [NgAccumulationExportService]
 ReviewerWindow.xaml 새 패널               ClosedXML: 기존 파일 열기(있으면)
 (Halcon 창 airspace 회피 위치)             → CycleFolderPath+측정명 dedupe
 ReviewMeasurementRow 가 필드로 보유         → NG 행만 append → wb.Save()
                                            (파일 잠김 → 경고 다이얼로그 후 중단)
```

### Recommended Project Structure

새 파일 없음(제약). 기존 파일에 추가되는 논리적 구획:

```
WPF_Example/UI/ViewModel/CycleResultDto.cs
  ├── DatumDiagnosticDto (신규 class, NGA-07)         # DatumConfig 진단값 DTO
  ├── ZCandidateScoreDto (신규 class, NGA-07)          # Z 후보별 점수 DTO
  ├── NgCauseResult (신규 class, NGA-01)               # 원인/근거/확인할일 3필드 + 나머지 원인 목록
  └── NgCauseAnalyzer (신규 static class, NGA-01)      # R1~R9 규칙 평가

WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
  └── CauseText/EvidenceText/ActionText 프로퍼티 추가(NGA-01/02) — 생성자에서 NgCauseAnalyzer 호출

WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)
  ├── 원인 3줄 패널(신규 XAML, NGA-02) — Row2 신설(Halcon 창 아래, 컬럼2~4 관통)
  ├── btn_chartSmoke 버튼 + Button_ChartSmoke_Click 삭제(NGA-04)
  ├── btn_exportExcel 버튼 + Button_ExportExcel_Click 삭제(NGA-04)
  ├── btn_ngAccumExport 버튼(신규, NGA-03) + 핸들러(배선만)
  └── 이미지 로드 경로(NGA-06): ResultImagePath → OriginImageFileName 우선 순서로 교체(3개 지점)

WPF_Example/Custom/Export/ExcelExportService.cs
  ├── Export(CycleResultDto, string) 메서드 삭제(NGA-04) — BuildJudgementText 등 헬퍼는 유지
  └── NgAccumulationExportService (신규 static class, NGA-03) — 같은 파일에 추가 또는
      RepeatExcelExportService.cs 에 추가(둘 다 Custom/Export 네임스페이스, 신규 파일 아님)

WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  └── AlignMatchScore/AlignMatchRow/AlignMatchCol 신규 런타임 필드(NGA-07, Align 경로 전용)

WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  ├── TryComposeAlign 안에서 datum.AlignMatchScore = curScore 등 대입(NGA-07)
  └── TakeTickDatumDiagnosticsSnapshot() 신규 private 메서드(NGA-07, 기존 TakeTickDatumImagesSnapshot 패턴 복제)

WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  └── ExecuteZRangeSelection 안에서 lstResults(ZFocusRunResult) → meas 소유 후보 점수 리스트로 write-back(NGA-07)

WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
  └── BuildDto 가 measDto.ZCandidateScores / (선택) datum diagnostics 복사(NGA-07)
```

### Pattern 1: 원인 규칙 엔진 — 순수 함수, DTO 만 입력

**What:** `CycleResultDto`(및 R6/R9 용 같은 날짜 폴더 인접 cycle 목록)만 받아 문자열 3개(원인/근거/확인할 일)를 반환하는 static 클래스. HALCON/DB/네트워크 의존 없음 — `ReviewerListLabelBuilder`(같은 파일, 이미 존재)와 동일한 설계 원칙.
**When to use:** 화면 3줄 패널과 NG 누적 엑셀 둘 다 이 클래스 하나만 호출.
**Example (기존 패턴 — 그대로 복제할 시그니처 스타일):**
```csharp
// Source: WPF_Example/UI/ViewModel/CycleResultDto.cs:496 (ReviewerListLabelBuilder.Build 시그니처 스타일)
public static class NgCauseAnalyzer
{
    public static NgCauseResult Analyze(CycleResultDto cycle, MeasurementResultDto measurement,
        FaiResultDto ownerFai, List<CycleResultDto> recentCyclesSameDate)
    {
        // 가드 절: null 방어 우선(기존 ReviewerListLabelBuilder 관례)
        if (cycle == null || measurement == null) { return NgCauseResult.Empty(); }
        // R1~R9 순서대로 평가, 첫 매치를 대표 원인으로("가장 구체적인 것 1개")
        // ...
    }
}
```

### Pattern 2: R1~R9 규칙 → 데이터 매핑 (실제 코드 확인 결과)

| # | 원인 | 판정 조건(정확한 필드) | 신규 데이터 필요? |
|---|------|------------------------|-------------------|
| R1 | 사진이 안 찍힘 | `measurement.LastSkipReason == SkipReason.NO_IMAGE` [VERIFIED: SkipReason.cs:8] | 아니오 |
| R2 | 기준점을 못 찾음 | `LastSkipReason` ∈ {`DATUM_FAIL`, `ALIGN_FAIL`, `DATUM_REF_MISSING`} [VERIFIED: SkipReason.cs:6-10] | 아니오(판정) / 근거 숫자 강화하려면 NGA-07 |
| R3 | 측정 선을 못 찾음 | `LastSkipReason == SkipReason.MEASURE_FAIL` [VERIFIED: SkipReason.cs:18] | 아니오 |
| R4 | 설정 오류 | `LastSkipReason` ∈ {`ZINDEX_MISCONFIGURED`, `CROSS_Z_INCOMPLETE`} [VERIFIED: SkipReason.cs:12,16] | 아니오 |
| R5 | 기준점 위치 흔들림 | 같은 FAI 의 `LastOverlays`(`RoiId=="FAI-DistLine"`) `Points[0]`(기준선 발끝, DistLine 은 `[datum foot, edge point]` 순서 — 실측 `D:/Data/Result/20260916/164530363_cycle/cycle.json:145-159` 로 확인)가 여러 측정에서 같은 방향·비슷한 크기로 어긋났는지 비교. 정밀도 강화는 NGA-07 `DatumDiagnosticDto.OriginRow/Col` 사이클 간 비교 | 기본판정 불필요 / 정밀화는 NGA-07 |
| R6 | 보정값/티칭 치우침 | 같은 (Shot,FAI,MeasurementName) 최근 N 사이클(`recentCyclesSameDate`, 같은 날짜 폴더 cycle.json 들 시간순)의 `LastMeasuredValue - NominalValue` 평균이 한쪽으로 지속 편향 | 아니오(cycle.json 리스트만 있으면 됨) — 09-16 실측: C13 계열 전부 +0.19~+0.24mm 초과(공차 0.03) |
| R7 | 한 곳만 벗어남 | 같은 사이클(`cycle.Shots` 전체 순회)에서 NG 측정이 1건뿐 | 아니오 |
| R8 | 초점 범위 부족 | `measurement.SelectedZIndex` == 그 Shot 의 `ZIndexEnd`(범위 끝) — Shot 설정은 레시피(`ShotConfig`) 소유라 cycle.json 에는 없음 → **Shot 이름으로 현재 레시피의 `ShotConfig.ZIndexEnd` 를 조회**해야 한다(런타임 `SystemHandler.Handle.Sequences.RecipeManager.Shots` 순회) | 판정에 레시피 조회 필요(신규 필드 아님, 조회 로직만 추가) |
| R9 | 공차 경계에서 흔들림 | `abs(LastMeasuredValue - Nominal) / Tolerance >= (1 - 경계%)` 이고 직전 같은 (Shot,FAI,MeasurementName) 사이클이 OK | 아니오 |

**R8 구현 시 주의:** `Shot.ZIndexEnd` 는 `CycleResultDto` 에 없다(레시피 설정이지 cycle 결과가 아님) — 리뷰어가 로드한 cycle.json 은 과거 데이터인데, 레시피는 **현재** 상태만 조회 가능(레시피가 그 사이 바뀌었으면 R8 판정이 부정확할 수 있음 — Open Question 으로 명시).

### Pattern 3: D-78-07 실제 촬영 사진 — 저장은 이미 무조건적, 소비 지점만 교체

**What:** `fai.LastOriginImageFileName` 은 `Action_FAIMeasurement.QueueFaiCapture`(→`ResolveFaiCaptureFileNames`)가 **모든 FAI 측정마다 설정(setting 게이트 없음)** — `CaptureImageSaveService.BuildFilePath(false, originName, ts)` 로 동기 결정되고, 비동기 워커가 실제 PNG/JPG 를 쓴다. `CycleResultSerializer.BuildDto` 가 `fai.LastOriginImageFileName` 을 `FaiResultDto.OriginImageFileName` 으로 그대로 복사(이미 cycle.json 에 있음, [VERIFIED: CycleResultSerializer.cs:97-110]).
**When to use:** 리뷰어 이미지 로드 3개 지점 전부(`DisplayCycle`:168-171, `MeasurementGrid_SelectionChanged`:256-259, DualImage 아님 일반 측정 경로) `ResultImagePath` 대신 **FAI 단위** `OriginImageFileName` 우선, 파일 없으면 `ResultImagePath` 폴백.
**주의 — Shot 단위 vs FAI 단위 전환:** 현재 `DisplayCycle`(cycle 전체 보기)은 `firstShot.ResultImagePath`(Shot 단위, `ShotResultDto`) 하나만 로드한다. `OriginImageFileName` 은 **FAI 단위**(`FaiResultDto`) 필드다 — Shot 에 FAI 가 여러 개면 "cycle 전체 보기"에서 로드할 origin 사진이 여러 장일 수 있다(단, `QueueFaiCapture` 호출 시 `szSharedOriginPath` 가 같은 Shot 의 FAI 들에 공유되면 — `AggregateFaiResult` 호출부의 `szSharedOriginPath` 파라미터 — 보통 같은 Shot 내 FAI 들은 origin 파일을 공유한다. 하지만 크로스-Z 처럼 FAI 마다 다른 원본을 저장하는 경로도 있다(`ResolveFaiCaptureFileNames`:1685-1689 주석 참조) — cycle 전체 보기에서는 첫 FAI 의 OriginImageFileName 을 쓰면 된다(기존 firstShot 패턴과 동일 단순화).
**Example:**
```csharp
// Source: WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs:164-172 (기존 코드, 교체 대상)
var firstShot = cycle.Shots.FirstOrDefault();
if (firstShot != null
    && !string.IsNullOrEmpty(firstShot.ResultImagePath)
    && File.Exists(firstShot.ResultImagePath))
{
    halconViewer.LoadImage(firstShot.ResultImagePath);
}
// 교체 방향(NGA-06): firstShot.FAIs 의 첫 FAI.OriginImageFileName 우선 시도 →
//   없거나 File.Exists 실패 시에만 firstShot.ResultImagePath 폴백.
//   같은 패턴을 MeasurementGrid_SelectionChanged(:254-260)의 row.OwnerFai.OriginImageFileName 에도 적용.
```

### Pattern 4: D-78-08 진단 값 기록 — 이미 계산된 값을 DTO 로 복사

**Datum (2갈래 존재 — 반드시 둘 다 처리):**

1. **HorizontalOnly 방식** (`DatumFindingService.cs:1035-1041`): `config.DetectedOriginRow/DetectedOriginCol/DetectedAngleDeg/DetectedEdgeCount/DetectedFitRMSE` 가 이미 채워짐(런타임 필드, INI 비저장). 로그: `[Datum.HorizontalOnly] ok — datum={0} origin=({1:F1}, {2:F1}) angle={3:F3}deg edges={4}` [VERIFIED: DatumFindingService.cs:1080-1082].
2. **Align(패턴매칭) 방식** (`InspectionSequence.TryComposeAlign`, `InspectionSequence.cs:3394-3410`): `curRow/curCol/curAngleDeg/curScore` 는 **지역 변수** — `DatumConfig` 에 저장되지 않는다. 2-패턴 baseline 보정이 켜진 경우만 `datum.Align2Score`(2번째 패턴 점수, `InspectionSequence.cs:3435`)가 저장된다. **NGA-07 은 `curScore`(1번째/주 패턴 매칭 점수)를 저장할 새 필드가 필요하다** — 예: `DatumConfig.AlignMatchScore`(+`AlignMatchRow`/`AlignMatchCol`, 기존 `Align2Score`/`Align2Status` 이웃에 추가, `[Category("Datum|Result")]` PropertyGrid 노출 목록에도 추가하는 게 일관적).

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:3394-3405 (현재 코드)
double curRow, curCol, curAngleDeg, curScore;
if (!svc.TryFindPose(refImage, datum.PatternEngine, modelPath, ... , out curRow, out curCol, out curAngleDeg, out curScore, out error, ...))
{
    return false; // ALIGN_FAIL
}
// NGA-07 추가 지점 — TryFindPose 성공 직후:
//   datum.AlignMatchScore = curScore;
//   datum.AlignMatchRow = curRow;
//   datum.AlignMatchCol = curCol;
//   datum.AlignMatchAngleDeg = curAngleDeg;
```

**Z 후보별 점수** — `Action_FAIMeasurement.ExecuteZRangeSelection`(:2372-2397)이 `RunZFocusCandidates` → `List<ZFocusRunResult> lstResults`(각 항목 `ZIndex/Ok/Score/Value/Error/Overlays`, `Action_FAIMeasurement.cs:2743-2749`)를 이미 만든다. 현재는 `LogZFocusSelection` 으로 로그 한 줄(`[ZFocus] 선택 — ... 후보 z3=28.28, z4=30.14, z5=32.52 → z5 ...`)만 남기고 버려진다([VERIFIED: 실측 `D:/Data/Algorithm/2026-09-16_Algorithm.log:73,247,422,596`]). NGA-07 은 `chosen` 채택 직후(:2393-2394 부근) `lstResults` 를 `meas`(MeasurementBase, 이미 `LastFitScore`/`LastSelectedZIndex` 런타임 필드 보유 — `MeasurementBase.cs:121-125`) 가 들고 있는 새 리스트 필드(예: `List<double> LastZCandidateIndices`, `List<double> LastZCandidateScores`, 병렬 배열 — 또는 작은 struct 리스트)에 write-back 하면 된다. `CycleResultSerializer.BuildDto` 가 `MeasurementResultDto.ZCandidateScores`(신규 `List<ZCandidateScoreDto>`, `CycleResultDto.cs` 에 추가)로 복사.

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:2389-2394 (현재 코드)
List<ZFocusRunResult> lstResults = RunZFocusCandidates(meas, transform, pixRes);
ZFocusRunResult chosen = PickZFocusResult(lstResults);
LogZFocusSelection(meas, lstResults, chosen, swMeasureExec);
RecordMeasurementResult(meas, false, chosen.Ok, chosen.Value, chosen.Error, chosen.Overlays, overlayAcc, faiOverlays, dctAlgoUsed, swMeasureExec, acc);
meas.LastSelectedZIndex = chosen.ZIndex;
// NGA-07 추가 지점 — RecordMeasurementResult 는 ClearResult 를 부르므로 그 뒤에:
//   meas.LastZCandidateScores = BuildCandidateScoreList(lstResults); // 새 헬퍼, 병렬 리스트 or 튜플 리스트
```

**주의 — 이전 Phase 77 결정과의 충돌:** `77-03-SUMMARY.md` 의 결정 사항 "점수(LastFitScore)는 화면·CSV·cycle.json 어디에도 기록하지 않는다(D-77-07 ⑥)"는 D-78-08 이 명시적으로 재개방(supersede)한 결정이다. 계획 단계에서 이 충돌을 플래너/유저에게 다시 확인시킬 필요는 없다 — 78-CONTEXT.md D-78-08 이 사용자 확정 결정이므로 우선한다. 다만 구현 시 "화면(리뷰어 측정표/오버레이 라벨)에는 여전히 점수를 노출하지 않고, cycle.json 에만 추가"를 유지할지, 원인 3줄 패널의 "근거 1줄"에 점수를 노출할지는 계획 단계에서 확정해야 한다(D-78-05 "근거 숫자 표시"와 D-77-07 "점수 비노출" 사이의 절충 — 근거 문구는 "z5(범위 끝)를 선택함" 같은 정성적 표현으로도 R8 근거를 충분히 전달 가능, 원점수 노출은 선택).

**datum tick 연결(연구 질문 2 — "각 tick 은 자기 cycle 폴더를 쓴다"):** Datum 검출은 보통 z1(또는 z1/z2 크로스-Z)에서 1회 일어나고, 그 뒤 측정 tick(z3+)들은 같은 `DatumConfig` 객체(사이클 동안 살아있는 인스턴스, `InspectionSequence.DatumConfigs`)의 **마지막 검출값**을 그대로 읽는다 — 즉 z3/z4/z5 tick 의 cycle.json 에 기록되는 Datum 진단값은 "이번 tick 에서 새로 검출한 값"이 아니라 "이 사이클에서 가장 최근에 검출된 값"이다. 이는 의도된 동작이며(재검출 안 함, D-78-08 "기록만 추가" 범위와 일치) R5 규칙이 "이 측정을 지배한 기준점이 최근에 얼마나 흔들렸는지"를 알기에 오히려 적합하다.

### Anti-Patterns to Avoid

- **`ExcelExportService.cs` 파일/클래스 전체 삭제:** `BuildJudgementText`/`LoadCaptureImageBytes`/`TryInsertCaptureImage`/`ApplyCaptureColumnWidth` 는 `RepeatExcelExportService.cs:294,346,366-367` 가 재사용 중 [VERIFIED: grep 결과]. `Export(CycleResultDto, string)` 퍼블릭 메서드만 삭제.
- **`ChartImageCapture.cs` 파일/클래스 전체 삭제:** `RenderHistogramPng`/`RenderTrendPng`/`TryInsertChartPicture` 는 `CpkReportExportService.cs:403-407` 가 사용 중(유지 대상 기능, D-78-06). `TrySaveSmokePng`(스모크 테스트 전용)만 버튼과 함께 삭제 대상.
- **Z 후보 점수를 재계산:** `RunZFocusCandidates` 를 다시 호출하거나 별도 스코어러를 새로 만들지 않는다 — 이미 `ExecuteZRangeSelection` 안에 결과가 있다(D-77-02 "재계산 없음" 원칙 유지).
- **cycle.json 스키마를 깨는 변경:** 기존 필드 이름/타입 변경 금지, 추가만. Newtonsoft 기본 동작(없는 JSON 필드 → C# 기본값/프로퍼티 이니셜라이저)로 하위호환이 자동 보장되므로 `[JsonProperty(Required = ...)]` 류를 걸지 않는다.
- **NG 누적 엑셀에서 매번 새 파일 생성:** D-78-02 "한 파일에 계속 누적" — `wb.SaveAs(newPath)` 패턴(기존 3개 export 서비스가 전부 이 패턴)을 그대로 복사하면 안 된다. `File.Exists(path)` 로 분기해 `new XLWorkbook(path)`(열기) vs `new XLWorkbook()`(신규) 를 선택해야 한다.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SkipReason → 사람이 읽는 문구 변환 | 새 매핑 테이블 | `ReviewerListLabelBuilder.BuildReasonText`/`ReviewMeasurementRow` 의 기존 3분기 로직 재사용(문구 상수도 `ReviewMeasurementRow.JUDGE_MEASURE_FAIL` 등 기존 const 재사용) | 이미 3곳(그리드/목록/엑셀)이 이 문구를 공유 — 규칙 엔진이 네 번째 소스가 되면 문구가 갈라진다 |
| 선택 Z 텍스트 포맷 | 새 "z5" 포맷터 | `MeasurementBase.FormatSelectedZ`/`ParseSelectedZ`(단일 소스, 77-03 이 확립한 패턴) | R8 근거 문구에 "z5" 를 넣을 때도 이 메서드만 호출 |
| xlsx 셀 이미지 삽입/판정 텍스트/캡쳐 대기-폴링 | 새 구현 | `ExcelExportService.LoadCaptureImageBytes`/`TryInsertCaptureImage`/`BuildJudgementText`(internal, 같은 어셈블리라 `NgAccumulationExportService` 에서 바로 호출 가능) | 이미 해결된 문제(JPEG EOI 마커 확인, 폭/비율 스케일, 판정 3~5분기) — 재작성하면 버그 재도입 위험 |
| 날짜 폴더 전체 cycle.json 스캔 | 새 파일 열거 로직 | `ReviewerWindow.LoadCycleFolders`(기존 "cycle.json 존재하는 하위 폴더만 수집" 로직, `:56` 부근)와 동일 패턴 | NG 누적 엑셀이 "현재 로드된 날짜 폴더"를 쓸지 "폴더 재선택"을 쓸지는 계획 단계 결정이지만, 스캔 로직 자체는 기존 것 재사용 |

**Key insight:** 이 프로젝트는 이미 "데이터는 `CycleResultDto` 하나, 소비자는 여러 개(그리드/목록/오버레이/CSV/xlsx)" 원칙을 3개 phase(40/77/78)에 걸쳐 지켜왔다. Phase 78 도 새 데이터 표현을 만들지 않고 이 원칙 위에 규칙 엔진 1개를 추가하는 것으로 충분하다 — 그렇게 하지 않으면(예: 원인 판정을 화면과 엑셀에서 각각 다시 구현) D-78-04 "화면·엑셀 동일 내용" 요구가 구조적으로 깨진다.

## Runtime State Inventory

> Rename/refactor 아님 — 이 phase 는 필드 추가·버튼 삭제이지 이름 변경/마이그레이션이 아니다. 다만 "회귀 0·옛 데이터 호환"(NGA-05)이 요구사항이므로 관련 항목만 점검.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| 저장된 데이터(cycle.json) | 기존 파일에 `DatumDiagnostics`/`ZCandidateScores` 등 필드가 없음(당연 — 아직 안 만듦). Newtonsoft 는 없는 JSON 필드를 C# 기본값/이니셜라이저로 채운다(`CycleResultSerializer.Load`, `TypeNameHandling.None`, try/catch→null) — 옛 파일도 크래시 없이 로드됨 | 코드 편집만(새 필드에 안전한 기본값 지정), 데이터 마이그레이션 불필요 |
| 저장된 데이터(CSV) | `MeasurementHistoryCsvWriter/Loader` 는 `COLUMN_COUNT=14` 고정 + 옵션 열(`COL_SELECTED_Z=15`) 가드 패턴 확립됨(77-03) — 이 phase 는 CSV 를 건드리지 않는다(D-78-08 은 cycle.json 만) | 없음 — CSV 변경 없음 |
| 라이브 서비스 설정 | 없음(리뷰어/규칙 엔진은 오프라인 cycle.json 을 읽는 UI 전용 기능, PLC/외부 서비스 설정과 무관) | 해당 없음 |
| OS 등록 상태 | 없음 | 해당 없음 |
| 시크릿/환경변수 | 없음 | 해당 없음 |
| 빌드 산출물 | 신규 .cs 파일 금지이므로 csproj 변경 없음 → 재설치/재빌드 특이사항 없음, Debug\|x64 빌드만 재확인 | 없음 |

## Common Pitfalls

### Pitfall 1: `ExcelExportService`/`ChartImageCapture` 삭제 범위 오판
**What goes wrong:** 버튼을 지우면서 그 버튼이 부르던 클래스/파일 전체를 지워, `RepeatExcelExportService`(반복도 엑셀 export, 유지 대상)나 `CpkReportExportService`(CPK 리포트 export, 유지 대상)가 컴파일 실패한다.
**Why it happens:** 클래스 이름(`ExcelExportService`)이 버튼 이름("엑셀 export")과 1:1로 보여서 착각하기 쉽다.
**How to avoid:** grep `ExcelExportService\.` / `ChartImageCapture\.` 전체 호출부를 먼저 나열하고, 이 phase 가 지우는 것은 **퍼블릭 진입점 1개(`Export(CycleResultDto,string)`)와 스모크 메서드 1개(`TrySaveSmokePng`)뿐**임을 계획에 명시.
**Warning signs:** 빌드 시 `RepeatExcelExportService.cs` 또는 `CpkReportExportService.cs` 에서 `CS0103`(이름 없음) 에러.

### Pitfall 2: R8 판정이 레시피 조회에 의존 — 리뷰어는 "과거" cycle.json, 레시피는 "현재"
**What goes wrong:** `Shot.ZIndexEnd` 는 cycle.json 에 없다(레시피 설정). 오래된 cycle.json 을 열었을 때 그 사이 레시피가 바뀌었으면(범위 확장/축소) R8("선택 Z == 범위 끝")이 지금 레시피 기준으로 오판될 수 있다.
**Why it happens:** cycle.json 은 결과의 스냅샷이지만 레시피 설정은 전역 가변 상태(`SystemHandler.Handle.Sequences.RecipeManager`)다.
**How to avoid:** R8 판정 시 레시피 미스매치 가능성을 "확인할 일" 문구에 암시하거나(예: "PLC Z 범위를 넓혀 달라고 요청 (현재 레시피 기준)"), Open Question 으로 남겨 계획 단계에서 "레시피가 그 cycle 때와 같다고 가정한다" 를 명시적으로 결정하게 한다.
**Warning signs:** 사용자가 옛 cycle.json 을 열었는데 R8 판정이 현재 레시피와 맞지 않는다는 피드백.

### Pitfall 3: R6/R9(추세) 규칙이 "같은 날짜 폴더"를 다시 스캔하며 성능 저하
**What goes wrong:** NG 행 하나를 클릭할 때마다 R6 규칙이 그 날짜 폴더 전체(최대 1000+ 개, 실측 20260915 폴더 1112개)를 디스크에서 다시 읽으면 클릭마다 수백 ms~초 단위 지연이 생길 수 있다.
**Why it happens:** `ReviewerWindow.LoadCycleFolders` 가 이미 그 날짜 폴더의 cycle.json 전부를 `_allCycleItems` 로 메모리에 들고 있다는 사실을 규칙 엔진이 모르고 별도로 재스캔하면 중복 I/O.
**How to avoid:** `NgCauseAnalyzer.Analyze` 의 `recentCyclesSameDate` 파라미터는 `ReviewerWindow` 가 이미 로드해 둔 `_allCycleItems`(또는 그 안의 `CycleResultDto` 목록)를 그대로 넘기게 설계 — 규칙 엔진은 파일 I/O 를 하지 않는다(순수 함수 원칙 재확인).
**Warning signs:** 대량 cycle.json 폴더(1000+)에서 행 클릭 시 UI 프리즈.

### Pitfall 4: NG 누적 엑셀 파일 잠금(사용자가 Excel 로 열어둔 상태)
**What goes wrong:** ClosedXML `wb.Save()`/`SaveAs()` 가 `IOException`(공유 위반)을 던지며 예외가 밖으로 새거나, 조용히 실패해 사용자가 "저장됐다"고 오인.
**Why it happens:** 기존 3개 export 서비스는 전부 `SaveFileDialog` 로 **새** 파일을 만들어 이 문제가 없었다 — NG 누적 엑셀은 처음으로 "기존 파일 열기→쓰기" 패턴이라 이 리스크가 새로 생긴다.
**How to avoid:** `wb.Save()`(또는 `SaveAs(path)`) 를 `try/catch (IOException)` 로 감싸고, 실패 시 `CustomMessageBox` 로 "파일이 열려 있습니다. 닫고 다시 시도하세요" 안내 후 중단(O-78-01 제안과 일치). 임시 파일에 먼저 쓰고 `File.Replace`/`File.Move` 하는 원자적 쓰기까지는 이 phase 범위를 넘어설 수 있음(계획 단계 판단).
**Warning signs:** UAT 중 엑셀을 열어둔 채 버튼을 누르면 앱이 처리되지 않은 예외로 죽는다.

### Pitfall 5: `curScore`/`ZFocusRunResult` write-back 시 이전 사이클 잔재
**What goes wrong:** `MeasurementBase` 는 이미 "`LastFitScore = 0.0; LastSelectedZIndex = SELECTED_Z_NONE;` // Phase 77: 이전 사이클 선택 점수 잔재 방지"([VERIFIED: MeasurementBase.cs:191-192])라는 명시적 초기화 지점이 있다 — 새 후보 점수 리스트 필드를 추가하면서 이 초기화 지점에 리셋을 빠뜨리면, Z 범위가 없는 측정(SupportsEdgeStrengthScore=false)이나 이전 사이클의 후보 목록이 다음 사이클/다음 측정에 잔류해 cycle.json 에 잘못된 값이 찍힐 수 있다.
**Why it happens:** 새 필드를 기존 초기화 블록(`ClearResult` 류) 밖에 추가하기 쉽다.
**How to avoid:** `MeasurementBase.cs:191-192` 근처(기존 `LastFitScore`/`LastSelectedZIndex` 리셋과 같은 지점)에 새 후보 리스트 필드의 `= null`(또는 `Clear()`) 을 반드시 같이 넣는다.
**Warning signs:** Z 범위 미사용 Shot 의 측정에서 cycle.json 에 `ZCandidateScores` 가 비어있지 않게 찍힘.

## Code Examples

### 기존 "규칙 → 문구" 패턴 (그대로 복제할 스타일)
```csharp
// Source: WPF_Example/UI/ViewModel/CycleResultDto.cs:210-228 (ReviewerListLabelBuilder.BuildReasonText)
private static string BuildReasonText(MeasurementResultDto m)
{
    if (m.LastSkipReason == SkipReason.DATUM_FAIL)
    {
        return "DETECT FAIL";
    }
    else if (m.LastSkipReason == SkipReason.NO_IMAGE)
    {
        return "NO IMAGE";
    }
    else if (m.LastSkipReason == SkipReason.MEASURE_FAIL)
    {
        return ReviewMeasurementRow.JUDGE_MEASURE_FAIL;
    }
    else
    {
        return "공차이탈"; // LastHasResult && !LastJudgement
    }
}
```

### ClosedXML "열기 vs 새로 만들기" 패턴 (이 프로젝트에 아직 없음 — 신규 설계, ClosedXML 공식 API 기반)
```csharp
// Source: ClosedXML 공식 API — XLWorkbook(string path) 생성자로 기존 파일을 연다(패키지 내 공개 API,
//  이 프로젝트에 실사용 예는 아직 없어 [CITED: ClosedXML GitHub README/공식 문서, 생성자 오버로드] 로 표기).
//  이 프로젝트의 기존 SaveAs 패턴(ExcelExportService.cs:52-134)과 짝을 이루는 "열기" 절반만 새로 필요하다.
bool bFileExists = File.Exists(outputPath);
XLWorkbook wb;
if (bFileExists)
{
    wb = new XLWorkbook(outputPath); // 기존 파일 열기
}
else
{
    wb = new XLWorkbook(); // 신규 생성 — 기존 3개 export 서비스와 동일
}
using (wb)
{
    IXLWorksheet ws;
    bool bSheetExists = wb.Worksheets.Contains(SHEET_NAME);
    if (bSheetExists)
    {
        ws = wb.Worksheet(SHEET_NAME);
    }
    else
    {
        ws = wb.Worksheets.Add(SHEET_NAME);
        WriteHeaderRow(ws); // 기존 3개 export 서비스와 동일한 헤더 작성 패턴
    }
    // dedupe: CycleFolderPath 열을 미리 읽어 HashSet 구성(O-78-04) 후 없는 것만 append
    try
    {
        wb.Save(); // 기존 파일이면 Save(), 신규면 SaveAs(outputPath) — 분기 필요
    }
    catch (IOException ex)
    {
        // Pitfall 4 — 파일 잠김. CustomMessageBox 안내 후 중단.
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| 사이클 1건 xlsx export(수동 저장 위치 선택) | 없음(삭제) — NG 만 자동 누적 파일 1개 | Phase 78 (D-78-06/D-78-02) | 운영자가 매 사이클 파일을 따로 관리할 필요 없음, 대신 파일 잠금/용량 관리가 새 관심사가 됨 |
| Z 후보 점수는 로그에만(`[ZFocus]`), 화면/JSON 비노출 | cycle.json 에 기록(진단용), 화면 노출 여부는 계획 단계 결정 | Phase 78 (D-78-08, Phase 77 D-77-07 ⑥ 재개방) | 리뷰어가 처음으로 "왜 이 Z 를 골랐는지" 를 재현 가능(로그 파일 접근 없이) |
| Datum 패턴매칭 점수(`curScore`)는 로그(`[ALIGN2]`)에만, 1차 패턴 점수는 로그에도 없음(2차만 로그) | `DatumConfig.AlignMatchScore` 신규 필드로 영속화 | Phase 78 (D-78-08) | R2/R5 규칙과 PropertyGrid Datum\|Result 카테고리 양쪽에서 재사용 가능해짐 |

**Deprecated/outdated:**
- "차트 이미지 캡처 점검" 버튼(`TrySaveSmokePng`): 오프스크린 렌더 동작을 수동으로 점검하던 개발기 스모크 테스트 — CpkReportExportService 가 이미 실사용으로 검증됐으므로 더 이상 필요 없다는 사용자 판단(D-78-06).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | R5(기준점 흔들림)/R6(치우침)/R9(경계) 임계값 구체 숫자(같은 방향 판정 비율 %, 추세 N, 경계 %) | Pattern 2 표, R5/R6/R9 행 | 임계값이 너무 타이트하면 오탐(정상을 흔들림으로 오판), 너무 느슨하면 09-15/09-16 실사례를 놓쳐 성공 기준 3(R5/R6/R8 실판정)을 못 채운다. 실측(09-15 z1 원점 row/col 사이클간 최대 ~2px≈5µm 이동, 09-16 C13 계열 전부 +0.19~0.24mm 초과)을 계획 단계 임계값 결정의 출발점으로 쓸 것 — 2026-09-15 Algorithm 로그 원점 값(예: 6601.9,634.2 → 6602.9,633.5 → 6601.0,633.3, [VERIFIED: D:/Data/Algorithm/2026-09-15_Algorithm.log:204,430,508])을 근거로 제시 |
| A2 | NG 누적 엑셀 파일명(`NG_분석_누적.xlsx`)과 저장 위치(`ResultSavePath` 바로 아래) | User Constraints > O-78-01 | 위치가 사용자 기대와 다르면 못 찾음 — CONTEXT.md 자체가 "제안값"이라고 명시한 항목이라 계획 단계에서 재확인 필요 |
| A3 | R8 판정 시 "지금 레시피의 ZIndexEnd" 를 그 cycle 당시 값으로 간주 | Common Pitfalls > Pitfall 2 | 레시피가 그 사이 바뀐 오래된 cycle.json 열람 시 오판 가능 — 낮은 빈도 리스크로 판단(레시피는 자주 안 바뀜) |
| A4 | `NgAccumulationExportService` 를 `ExcelExportService.cs` 또는 `RepeatExcelExportService.cs` 중 어느 기존 파일에 추가할지 | Recommended Project Structure | 신규 .cs 파일 금지 제약을 지키는 두 옵션 중 택1 — 계획 단계에서 파일 크기/응집도로 결정 |
| A5 | ClosedXML `XLWorkbook(string path)` 생성자로 기존 xlsx 를 열 때 시트/셀 스타일이 보존된다 | Code Examples > ClosedXML 열기 패턴 | ClosedXML 공식 동작이라고 알려져 있으나(훈련 지식), 이 프로젝트에서 실제로 열기→저장 왕복을 실측 검증한 적 없음 — 계획 단계에서 소규모 tracer 로 1회 실증 필요 |

## Open Questions

1. **NG 누적 엑셀이 "현재 로드된 날짜 폴더"만 대상인가, 폴더 재탐색인가?**
   - What we know: D-78-02 는 "날짜와 상관없이 한 파일에 계속 추가"라고만 함 — 트리거 시점(버튼 클릭 시 무엇을 스캔하는지)은 미정.
   - What's unclear: `ReviewerWindow` 가 이미 로드한 `_allCycleItems`(현재 열린 날짜 폴더만) 기준으로 append 하는지, 별도 폴더 선택 다이얼로그를 또 띄우는지.
   - Recommendation: 기존 "날짜 폴더 열기" 버튼 흐름 재사용 — 리뷰어가 이미 로드한 날짜 폴더의 NG 만 누적(사용자가 여러 날짜를 각각 열고 누를 때마다 그 날짜분만 추가, 중복은 dedupe 키로 자동 방지)이 가장 단순하고 기존 UI 패턴과 일치. 계획 단계에서 확정.

2. **R8 판정과 레시피 현재값 의존성을 사용자에게 어떻게 알릴 것인가?**
   - What we know: Pitfall 2 에서 설명한 구조적 한계.
   - What's unclear: "확인할 일" 문구에 이 한계를 명시할지, 그냥 현재 레시피 기준으로 판정하고 넘어갈지.
   - Recommendation: 문구에 "(현재 레시피 기준)" 을 붙여 최소한의 투명성 확보 — 계획 단계 재량.

## Environment Availability

> 외부 도구/서비스 의존 없음 — HALCON 24.11, ClosedXML 0.105.0, Newtonsoft.Json 13.0.3 모두 이미 설치·빌드 확인된 기존 의존성이며 이 phase 가 새로 추가하는 런타임 의존성이 없다. 섹션 스킵.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | 없음(xUnit/NUnit/MSTest 프로젝트 부재, CLAUDE.md 명시) — 대신 Framework `csc.exe` 로 컴파일한 리플렉션 프로브(probe) + 실제 cycle.json 데이터 실측을 사용 (Quick 260915-k5g, Phase 77-03 이 확립한 패턴) |
| Config file | 없음 |
| Quick run command | (Task 별 probe 컴파일 1회) `cd $H && MSYS_NO_PATHCONV=1 /c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe -nologo -platform:x64 -out:NgCauseProbe.exe -lib:C:/code/DataMeasurement/WPF_Example/bin/x64/Debug -r:DatumMeasurement.exe NgCauseProbe.cs && ./NgCauseProbe.exe` |
| Full suite command | Debug\|x64 빌드: `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` (Release 빌드 금지 — D:/Data 배포 exe 보호) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| NGA-01 | R1~R9 규칙이 09-15/09-16/09-17 실 cycle.json 데이터에서 기대한 원인으로 분류됨 | 리플렉션 probe + 실데이터 비교(before/after) | csc probe → `D:/Data/Result/20260915,20260916,20260917` cycle.json 전체 리플렉션 순회, 기대 원인 assertion | ❌ Wave 0 — `NgCauseProbe.cs` 스크래치 파일(프로젝트 밖, `.planning` 또는 임시 경로) |
| NGA-02 | 원인 3줄이 NG 행 선택 시 표시, code-behind 무수정(배선만) | grep 게이트(branch_kw/newlist/codelines) + 빌드 PASS | `grep -cE 'if|switch' ReviewerWindow.xaml.cs` (배선 라인 수 한도 확인, 77-03/260915-k5g 관례) | ❌ Wave 0 — 게이트 스크립트 |
| NGA-03 | NG 누적 엑셀이 재실행해도 중복 행 없이 append | probe(ClosedXML 열기→셀 수 확인) + 수동 2회 실행 비교 | csc probe 로 실제 xlsx 열어 행 수/CycleFolderPath 컬럼 유일성 확인 | ❌ Wave 0 |
| NGA-04 | 버튼 2개 삭제, 나머지 기능 컴파일·동작 유지 | 빌드 PASS + grep(삭제 확인) | `grep -c "btn_chartSmoke\|btn_exportExcel" ReviewerWindow.xaml` == 0 | 기존 파일, probe 불필요 |
| NGA-05 | 옛 cycle.json(신규 필드 없음) 로드 시 크래시 없음, 규칙 엔진이 "데이터 없음"으로 건너뜀 | probe — `20260601`/`20260811`(Phase 77-03 이 이미 옛 JSON 표본으로 사용한 폴더) cycle.json 로 회귀 확인 | 기존 `ReviewerLabelProbe.exe` 패턴 재사용(260915-k5g) | ❌ Wave 0(probe 확장) |
| NGA-06 | 리뷰어가 OriginImageFileName 우선 로드, 없으면 ResultImagePath 폴백 | probe + 실 cycle.json 경로 File.Exists 확인 | `D:/Data/Result/202609*` 의 OriginImageFileName 실존 여부 샘플링 | ❌ Wave 0 |
| NGA-07 | Datum/Z후보 진단값이 cycle.json 에 기록되고 옛 파일과 호환 | 빌드 PASS + SIMUL 1회 수동 사이클 + cycle.json diff 확인 | Debug\|x64 빌드 후 SIMUL 사이클 1회 실행, 생성된 cycle.json 에 신규 필드 존재 확인(사람 확인 — 실행 하네스 부재는 Phase 39/77 SUMMARY 들과 동일 사유) | ❌ Wave 0, 코드 편집 후 수동 확인 |

### Sampling Rate

- **Per task commit:** Debug\|x64 빌드(`MSBuild.exe ... -p:Configuration=Debug -p:Platform=x64`) — 매 task 편집 직후
- **Per wave merge:** probe 재실행(변경된 규칙/필드 대상) + 실 cycle.json 표본(20260601/20260811 옛 JSON, 20260915/20260916 신규 Z 범위 데이터) 비교
- **Phase gate:** `/gsd-verify-work` 전 전체 probe 스위트 + 09-15/09-16/09-17 실데이터로 성공 기준 3(R5/R6/R8 실판정) 확인

### Wave 0 Gaps

- [ ] `NgCauseProbe.cs`(scratch, csc 컴파일용) — R1~R9 판정 assertion, 260915-k5g `ReviewerLabelProbe.cs`/77-03 검증 스크립트 패턴 확장
- [ ] xlsx append/dedupe 확인용 별도 probe 섹션(ClosedXML 로 임시 xlsx 열기→행 수 확인)
- [ ] SIMUL 1회 실행으로 cycle.json 신규 필드 생성 확인 절차(사람 확인, 테스트 하네스 부재는 이 프로젝트 전 phase 공통 제약)

## Security Domain

> `security_enforcement` 설정 키가 `.planning/config.json` 에 없음 → 기본 활성. 다만 이 프로젝트는 오프라인 산업 장비 PC(인터넷 없음, D-78-03 명시)이며 이 phase 는 외부 입력을 신규로 받지 않는다(cycle.json 은 이미 `TypeNameHandling.None` + try/catch→null 로 보호된 내부 파일, xlsx 출력 경로도 사용자 로컬 파일시스템).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | 해당 기능 없음(로컬 장비 PC, 기존 LoginManager 범위 밖) |
| V3 Session Management | no | 해당 없음 |
| V4 Access Control | no | 해당 없음 |
| V5 Input Validation | yes | cycle.json 역직렬화는 기존 `TypeNameHandling.None` + try/catch→null(RCE 방지, `CycleResultSerializer.Load` 기존 패턴 유지) — 신규 필드도 동일 패턴 상속(자동) |
| V6 Cryptography | no | 해당 없음 |

### Known Threat Patterns for 이 phase

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| 손상/악의적 cycle.json (외부에서 손을 댄 파일) 역직렬화 | Tampering | 기존 `CycleResultSerializer.Load` 의 `TypeNameHandling.None` + try/catch(RCE 방지 명시 주석) — 이 phase 가 추가하는 필드도 같은 `JsonConvert.DeserializeObject<CycleResultDto>` 경로를 그대로 타므로 별도 조치 불필요 |
| xlsx 출력 경로 조작(path traversal) | Tampering | 출력 경로는 `SaveFileDialog`(기존 3개 export 서비스 패턴) 또는 고정 `ResultSavePath` 조합 — 사용자 자유 입력 경로 문자열을 그대로 `Path.Combine` 하지 않는다(기존 `CaptureImageSaveService.SanitizeFilePart` 패턴 참고, 이 phase 의 파일명이 사용자 입력에서 오지 않으므로 리스크 낮음) |

## Sources

### Primary (HIGH confidence)
- `C:/code/DataMeasurement/WPF_Example/UI/ViewModel/CycleResultDto.cs` — DTO 구조, `ReviewerListLabelBuilder` 규칙 패턴
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` — BuildDto/SaveAsync/Load
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` — 사유 상수 단일 소스
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — ZFocusRunResult/ExecuteZRangeSelection/QueueFaiCapture/CaptureImageSaveService 연동
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — TryComposeAlign(Align2Score/curScore), TakeTickDatumImagesSnapshot 패턴
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — DetectedOriginRow/Col/AngleDeg/EdgeCount/FitRMSE/Align2Score
- `C:/code/DataMeasurement/WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — [Datum.HorizontalOnly] 로그 지점
- `C:/code/DataMeasurement/WPF_Example/UI/Reviewer/ReviewerWindow.xaml`, `.xaml.cs` — 버튼/이미지 로드/레이아웃
- `C:/code/DataMeasurement/WPF_Example/Custom/Export/ExcelExportService.cs`, `RepeatExcelExportService.cs`, `ChartImageCapture.cs`, `CpkReportExportService.cs` — 재사용 헬퍼 경계
- `C:/code/DataMeasurement/WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` — 행 DTO, JudgeText 3~5분기
- `C:/code/DataMeasurement/WPF_Example/packages.config` — ClosedXML 0.105.0/Newtonsoft.Json 13.0.3 실제 설치 버전
- `D:/Data/Result/20260916/164530363_cycle/cycle.json`(외 인접 3건) — 실 데이터 구조·FAI-DistLine Points 순서 확인(읽기 전용)
- `D:/Data/Algorithm/2026-09-16_Algorithm.log`, `2026-09-15_Algorithm.log` — [ZFocus]/[Datum.HorizontalOnly] 실측 로그(읽기 전용)
- `.planning/phases/77-side-z-focus-select/77-CONTEXT.md`, `77-03-SUMMARY.md` — Phase 77 결정/패턴(FormatSelectedZ, D-77-07 ⑥ 점수 비노출 — 이번 phase 가 재개방)
- `.planning/quick/260915-k5g-z/260915-k5g-SUMMARY.md`, `260915-k5g-PLAN.md` — 리뷰어 목록 단순화, csc probe 검증 패턴, MSBuild 명령

### Secondary (MEDIUM confidence)
- ClosedXML `XLWorkbook(string path)` 생성자를 통한 "기존 파일 열기" 동작 — 공식 API 존재는 확실하나 이 저장소에서 실사용 예가 없어 `[CITED: ClosedXML 공식 문서/README]`로 표기(계획 단계 tracer 로 1회 실증 권고, Assumption A5)

### Tertiary (LOW confidence)
- R5/R6/R9 구체적 임계값 숫자(Assumption A1) — 실측 데이터로 방향은 확인했으나 최종 숫자는 미확정

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — 신규 패키지 없음, 기존 설치본 버전 파일에서 직접 확인
- Architecture: HIGH — 모든 데이터 흐름/필드를 실제 소스 코드 라인 번호로 확인, ClosedXML "열기" 패턴만 MEDIUM(이 저장소에 실사용 예 없음)
- Pitfalls: HIGH — Pitfall 1(삭제 범위), 5(초기화 누락)는 실제 코드 라인 근거, Pitfall 2/3/4 는 구조적 추론(논리적으로 확실)
- 임계값 숫자(R5/R6/R9): LOW — 실측 데이터 4개 표본으로 방향만 확인, 정확한 %/N 은 계획 단계 재량(Assumption A1)

**Research date:** 2026-09-17
**Valid until:** 2026-10-17 (30일 — cycle.json 스키마와 ClosedXML 버전이 자주 바뀌지 않는 안정적 영역)
