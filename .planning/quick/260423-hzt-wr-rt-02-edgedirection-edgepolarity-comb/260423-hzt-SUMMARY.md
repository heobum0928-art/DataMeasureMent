---
id: 260423-hzt
title: "WR-RT-02 EdgeDirection/EdgePolarity ComboBox 처리"
mode: quick
status: complete
date: 2026-04-23
---

# Quick Task 260423-hzt — SUMMARY

## Outcome

PropertyGrid에 노출되는 `EdgeDirection` / `EdgePolarity` 문자열 프로퍼티에 PropertyTools `ItemsSourceProperty` 바인딩을 적용, 자유 텍스트 입력을 제거하고 드롭다운만 허용하도록 변경.

WR-RT-02(🟡 Warning, `.planning/bugs.md`) 구조적 해소. `LTOR` / `Lto R` 오타로 인한 측정 실패 경로 차단.

## Changes

### 신규
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs`
  - `Directions` — `LtoR, RtoL, TtoB, BtoT`
  - `FAIPolarities` — `DarkToLight, LightToDark`
  - `DatumPolarities` — `all, positive, negative` (Halcon `MeasurePos` 원시 transition 값)

### 수정 (PropertyGrid-노출 클래스 7개)
- `FAIConfig.cs` — EdgeDirection + EdgePolarity
- `DatumConfig.cs` — EdgePolarity (Halcon raw 리스트)
- `Measurements/EdgePairDistanceMeasurement.cs` — 2 속성
- `Measurements/LineToLineDistanceMeasurement.cs` — 2 속성
- `Measurements/LineToLineAngleMeasurement.cs` — 2 속성
- `Measurements/PointToPointDistanceMeasurement.cs` — 2 속성
- `Measurements/PointToLineDistanceMeasurement.cs` — 2 속성
- `Measurements/CircleDiameterMeasurement.cs` — EdgePolarity만

### Build
- `WPF_Example/DatumMeasurement.csproj` — `EdgeOptionLists.cs` `<Compile Include>` 추가 (라인 208)

## Pattern

각 클래스 내 동일 패턴:
```csharp
[ItemsSourceProperty(nameof(EdgeDirectionList))]
public string EdgeDirection { get; set; } = "LtoR";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
```

## Compatibility

- 프로퍼티 타입은 `string`을 유지 — `ParamBase` 리플렉션 직렬화와 기존 INI 레시피 그대로 로드/저장.
- 기존 저장값(`LtoR`, `DarkToLight`, `all` 등)이 모두 리스트 항목과 일치 → 마이그레이션 불필요.
- `RoiDefinition`은 `[DataContract]` 데이터 모델이며 PropertyGrid에 직접 노출되지 않으므로 범위 밖. `TeachingWindow.xaml.cs`는 자체 ComboBox를 하드코드로 유지(별도 경로).

## Verification

- `Grep ItemsSourceProperty|EdgeOptionLists` — 7개 Inspection 클래스 전부에 속성 + List 래퍼 존재 확인.
- DatumConfig만 `EdgeOptionLists.DatumPolarities` 참조, 나머지는 `FAIPolarities` (FAI/측정 레이어 값 도메인).
- `FAIConfig.ToRoiDefinition()` 내 값 복사 경로 영향 없음 (여전히 문자열 그대로 전달).

## Out of Scope

- `EdgeSelection` / `LineOrientation` — 결함 보고서는 `EdgeDirection`/`EdgePolarity` 두 항목만 지목. 후속 작업 대상.
- `RoiDefinition.cs` — PropertyGrid 미노출.
- 빌드 검증 — 로컬 Halcon/Pylon 런타임 필요, syntax + 기존 패턴(`CameraParam.cs` 참조)으로 확인.

## Related

- 원본 결함: `.planning/bugs.md` WR-RT-02
- 기원 phase: Phase 1 + Quick 260409-e3v
- Phase 11 (신설 예정) 전 선행 마무리 — bugs.md의 "Quick Task 대기" 항목 해소
