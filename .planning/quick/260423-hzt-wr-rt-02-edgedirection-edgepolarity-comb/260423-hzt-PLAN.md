---
id: 260423-hzt
title: "WR-RT-02 EdgeDirection/EdgePolarity ComboBox 처리"
mode: quick
status: planned
date: 2026-04-23
---

# Quick Task 260423-hzt — WR-RT-02 EdgeDirection/EdgePolarity ComboBox 처리

## Goal

PropertyGrid(PropertyTools)에 노출되는 `EdgeDirection` / `EdgePolarity` 문자열 프로퍼티를 자유 텍스트 대신 ComboBox로 표시하여 `LTOR`, `Lto R` 같은 오타 입력으로 측정이 실패하는 경로를 차단한다.

원본 결함: `.planning/bugs.md` WR-RT-02 (🟡 Warning, Phase 1 + Quick 260409-e3v 기원).

## Approach

- C# `enum`으로 바꾸지 않는다 — `ParamBase` INI 직렬화 및 기존 레시피 하위호환을 유지하기 위해 `string` 유지.
- PropertyTools 기본 ComboBox 메커니즘인 `[ItemsSourceProperty("<PropName>")]`를 사용.
  - `CameraParam.LightGroupName` / `DeviceName` 에서 이미 확립된 프로젝트 패턴.
- 옵션 소스는 공용 정적 클래스 `EdgeOptionLists` 하나로 단일화 — 각 모델은 `[Browsable(false)]` 인스턴스 프로퍼티 래퍼로 노출.

### 값 도메인 (코드 분석으로 확정)

| 분류 | 값 | 근거 |
|------|----|------|
| EdgeDirection | `LtoR`, `RtoL`, `TtoB`, `BtoT` | `TeachingWindow.xaml.cs:30`, `MeasurementAlgorithm.cs:130-136` |
| EdgePolarity (FAI/Measurement) | `DarkToLight`, `LightToDark` | `TeachingWindow.xaml.cs:31`, `MeasurementAlgorithm.cs:177` |
| EdgePolarity (Datum, Halcon raw) | `all`, `positive`, `negative` | `DatumConfig.cs:53` (주석 명시) |

## Files

### 신규
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — 3개 정적 `List<string>` 모음

### 수정 (PropertyTools로 PropertyGrid에 노출되는 클래스만)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — EdgeDirection, EdgePolarity
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — EdgePolarity (Halcon raw 옵션)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs` — EdgeDirection, EdgePolarity
- `WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineDistanceMeasurement.cs` — EdgeDirection, EdgePolarity
- `WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineAngleMeasurement.cs` — EdgeDirection, EdgePolarity
- `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToPointDistanceMeasurement.cs` — EdgeDirection, EdgePolarity
- `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs` — EdgeDirection, EdgePolarity
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` — EdgePolarity 만
- `WPF_Example/DatumMeasurement.csproj` — 신규 `EdgeOptionLists.cs` `<Compile Include>` 추가

### 범위 밖
- `WPF_Example/Halcon/Models/RoiDefinition.cs` — `[DataContract]` 데이터 모델이며 PropertyGrid에 직접 노출되지 않는다 (`TeachingWindow`가 자체 ComboBox로 처리). 수정 불필요.
- `EdgeSelection`, `LineOrientation` 등 인접 문자열 — 결함 보고서가 두 항목(`EdgeDirection`/`EdgePolarity`)만 지목. 범위 밖.

## Tasks

### T1. 공용 옵션 리스트 추가
- Create `EdgeOptionLists.cs` with three `public static readonly List<string>` — `Directions`, `FAIPolarities`, `DatumPolarities`.
- Add `<Compile Include>` entry in `DatumMeasurement.csproj` (classic-style project).

### T2. PropertyGrid 노출 클래스에 ComboBox 바인딩 적용
- 각 파일의 `EdgeDirection` / `EdgePolarity` 프로퍼티 위에 `[ItemsSourceProperty(nameof(<PropName>List))]` 부착.
- 각 클래스에 `[Browsable(false)] public List<string> <PropName>List { get { return EdgeOptionLists.<X>; } }` 추가.
- `DatumConfig.EdgePolarity`는 `DatumPolarities` 사용.
- 주석 규칙 준수: 수정/추가 라인에 `//260423 hbk WR-RT-02 ComboBox 처리`.

### T3. 컴파일 확인
- 프로젝트 경로 구조상 로컬 MSBuild 빌드는 HALCON/Pylon 의존이 있어 CI 환경에서 어려움 — **수기 검토**(syntax + using 조회)로 갈음.

## Verify

- 각 수정 파일에 `[ItemsSourceProperty(...)]`가 붙었는가.
- 리스트 프로퍼티가 `[Browsable(false)]` + 정적 소스 참조로 작성되었는가.
- `EdgeOptionLists.cs`가 `.csproj`에 포함되었는가.
- FAI vs Datum 폴라리티가 올바른 리스트를 참조하는가 (혼동 방지).

## Done

- PropertyGrid에서 `EdgeDirection` / `EdgePolarity` 선택 시 드롭다운이 표시되고 자유 입력이 불가한 상태.
- 기존 INI 레시피의 문자열 값(`LtoR`, `DarkToLight`, `all` 등)은 변함없이 로드/저장 가능 (string 유지).
